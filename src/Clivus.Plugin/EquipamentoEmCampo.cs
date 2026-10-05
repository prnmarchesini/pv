using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O retângulo de um equipamento elétrico em campo (12.3, 13.2, 14.6): uma
/// referência de bloco próprio do equipamento (caixa 3D com a dimensão do
/// cadastro e a tag escrita no topo), na camada CLIVUS_EQUIPAMENTO, com o
/// XData <see cref="EquipmentPlacement"/>. O ponto de inserção é o centro da
/// base, que flutua 0,80 m acima do terreno. É representação: mover o bloco
/// não muda vínculo nenhum (regra elétrica 1).
/// </summary>
internal static class EquipamentoEmCampo
{
    private const string PrefixoDoBloco = PluginInfo.PrefixoDeDados + "_EQUIPAMENTO_";

    /// <summary>As referências em campo de cada equipamento (tipo, GUID).</summary>
    internal static Dictionary<(EquipmentKind Kind, Guid Id), ObjectId> Posicionados(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classe = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(BlockReference));
        var achados = new Dictionary<(EquipmentKind, Guid), ObjectId>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || !id.ObjectClass.IsDerivedFrom(classe)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is BlockReference b && ElectricalStore.LoadPlacement(b) is { } p)
                achados.TryAdd((p.Kind, p.Equipment), id);
        }

        return achados;
    }

    /// <summary>O que já está em campo, numa leitura só (para as listas da janela).</summary>
    internal static HashSet<(EquipmentKind Kind, Guid Id)> EmCampo(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        return [.. Posicionados(transacao, database).Keys];
    }

    /// <summary>
    /// Põe o equipamento em campo com o centro da base em <paramref name="baseCentro"/>
    /// (a cota já é a do terreno + 0,80 m). Se ele já estava em campo, o mesmo
    /// bloco é movido (re-alocar), e o desenho dele refeito com a tag e a
    /// dimensão do cadastro de agora.
    /// </summary>
    internal static ObjectId Posicionar(Database database, EquipmentInfo equipamento, Point3d baseCentro)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var definicao = GarantirBloco(transacao, database, equipamento);
        var id = Posicionados(transacao, database).GetValueOrDefault((equipamento.Kind, equipamento.Id));

        if (!id.IsNull)
        {
            var existente = (BlockReference)transacao.GetObject(id, OpenMode.ForWrite);
            existente.Position = baseCentro;
            existente.RecordGraphicsModified(true);
        }
        else
        {
            var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Equipamento, new RgbColor(90, 90, 90));
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            var referencia = new BlockReference(baseCentro, definicao) { Layer = camada };
            id = espaco.AppendEntity(referencia);
            transacao.AddNewlyCreatedDBObject(referencia, true);
            ElectricalStore.SavePlacement(transacao, referencia, new EquipmentPlacement(equipamento.Kind, equipamento.Id));
        }

        transacao.Commit();
        return id;
    }

    /// <summary>
    /// Depois de mudar o cadastro (tag, dimensão): refaz o desenho do bloco
    /// se o equipamento está em campo. A posição não muda. Se estava.
    /// </summary>
    internal static bool Redesenhar(Database database, EquipmentInfo equipamento)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        if (!Posicionados(transacao, database).TryGetValue((equipamento.Kind, equipamento.Id), out var id)) return false;

        GarantirBloco(transacao, database, equipamento);
        ((BlockReference)transacao.GetObject(id, OpenMode.ForWrite)).RecordGraphicsModified(true);

        transacao.Commit();
        return true;
    }

    /// <summary>
    /// Depois de editar o cadastro (dentro de <see cref="ConfiguracaoEletricaStore.Mudar"/>):
    /// se deu certo e o equipamento está em campo, a tag e a dimensão do
    /// retângulo acompanham. Devolve o porquê da edição, como veio.
    /// </summary>
    internal static string? RedesenharSeDeuCerto(Database database, ElectricalSetup setup, string? porque, EquipmentKind tipo, Guid id)
    {
        if (porque is null && setup.FindEquipment(tipo, id) is { } equipamento) Redesenhar(database, equipamento);
        return porque;
    }

    /// <summary>
    /// Tira o equipamento do campo (o cadastro dele foi apagado): apaga a
    /// referência e a definição do bloco. Se estava em campo.
    /// </summary>
    internal static bool Apagar(Database database, EquipmentKind tipo, Guid equipamento)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var apagou = false;
        if (Posicionados(transacao, database).TryGetValue((tipo, equipamento), out var id))
        {
            transacao.GetObject(id, OpenMode.ForWrite).Erase();
            apagou = true;
        }

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var nome = NomeDoBloco(tipo, equipamento);
        if (tabela.Has(nome))
        {
            var definicao = (BlockTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);
            if (definicao.GetBlockReferenceIds(true, false).Count == 0)
            {
                definicao.UpgradeOpen();
                definicao.Erase();
            }
        }

        transacao.Commit();
        return apagou;
    }

    private static string NomeDoBloco(EquipmentKind tipo, Guid equipamento) => $"{PrefixoDoBloco}{tipo}_{equipamento:N}";

    /// <summary>A definição do bloco do equipamento, criada ou refeita com a tag e a dimensão de agora.</summary>
    private static ObjectId GarantirBloco(Transaction transacao, Database database, EquipmentInfo e)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var nome = NomeDoBloco(e.Kind, e.Id);

        BlockTableRecord definicao;
        ObjectId id;

        if (tabela.Has(nome))
        {
            id = tabela[nome];
            definicao = (BlockTableRecord)transacao.GetObject(id, OpenMode.ForWrite);
            foreach (ObjectId velho in definicao) transacao.GetObject(velho, OpenMode.ForWrite).Erase();
        }
        else
        {
            tabela.UpgradeOpen();
            definicao = new BlockTableRecord { Name = nome, Origin = Point3d.Origin };
            id = tabela.Add(definicao);
            transacao.AddNewlyCreatedDBObject(definicao, true);
        }

        var cor = Cor(e.Kind);

        // A caixa: base no z = 0 do bloco (que é o terreno + 0,80 m).
        var caixa = new Solid3d { Layer = "0", Color = cor };
        caixa.CreateBox(e.Size.Width, e.Size.Length, e.Size.Height);
        caixa.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, e.Size.Height / 2)));
        definicao.AppendEntity(caixa);
        transacao.AddNewlyCreatedDBObject(caixa, true);

        // A tag, deitada no topo, lida de cima.
        var tag = new MText
        {
            Contents = TextoDeMText(e.Tag),
            Location = new Point3d(0, 0, e.Size.Height + 0.02),
            Attachment = AttachmentPoint.MiddleCenter,
            TextHeight = EquipmentFootprint.TagHeight(e.Tag.Length, e.Size.Width, e.Size.Length),
            Layer = "0",
            Color = Color.FromColorIndex(ColorMethod.ByAci, 7),
        };
        definicao.AppendEntity(tag);
        transacao.AddNewlyCreatedDBObject(tag, true);

        return id;
    }

    /// <summary>A tag como texto do MText: barra e chaves são códigos de formato, vão escapadas.</summary>
    private static string TextoDeMText(string texto) =>
        texto.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");

    private static Color Cor(EquipmentKind tipo) => tipo switch
    {
        EquipmentKind.ConsumerUnit => Color.FromRgb(170, 60, 170),
        EquipmentKind.Transformer => Color.FromRgb(230, 140, 30),
        _ => Color.FromRgb(40, 110, 200),
    };
}
