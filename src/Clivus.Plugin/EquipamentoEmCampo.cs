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

    /// <summary>
    /// As referências em campo de cada equipamento (tipo, GUID). Procura pelas
    /// definições CLIVUS_EQUIPAMENTO_* (poucas), não pelo espaço do modelo
    /// inteiro (milhares de módulos). Um COPY do bloco leva o XData junto: o
    /// equipamento pode aparecer com mais de uma referência.
    /// </summary>
    internal static Dictionary<(EquipmentKind Kind, Guid Id), List<ObjectId>> Posicionados(Transaction transacao, Database database)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelo = SymbolUtilityServices.GetBlockModelSpaceId(database);
        var achados = new Dictionary<(EquipmentKind, Guid), List<ObjectId>>();

        foreach (ObjectId idDaDefinicao in tabela)
        {
            var definicao = (BlockTableRecord)transacao.GetObject(idDaDefinicao, OpenMode.ForRead);
            if (definicao.IsErased || !definicao.Name.StartsWith(PrefixoDoBloco, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (ObjectId id in definicao.GetBlockReferenceIds(true, false))
            {
                if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not BlockReference b || b.OwnerId != modelo) continue;
                if (ElectricalStore.LoadPlacement(b) is not { } p) continue;

                if (!achados.TryGetValue((p.Kind, p.Equipment), out var lista)) achados[(p.Kind, p.Equipment)] = lista = [];
                lista.Add(id);
            }
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
    /// dimensão do cadastro de agora. Devolve quantas cópias A MAIS do
    /// retângulo o desenho tem (COPY do usuário): elas não são apagadas, quem
    /// chama avisa.
    /// </summary>
    internal static int Posicionar(Database database, EquipmentInfo equipamento, Point3d baseCentro)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var definicao = GarantirBloco(transacao, database, equipamento);
        var ids = Posicionados(transacao, database).GetValueOrDefault((equipamento.Kind, equipamento.Id)) ?? [];

        if (ids.Count > 0)
        {
            var existente = (BlockReference)transacao.GetObject(ids[0], OpenMode.ForWrite);
            existente.Position = baseCentro;
            existente.RecordGraphicsModified(true);
        }
        else
        {
            var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Equipamento, new RgbColor(90, 90, 90));
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            var referencia = new BlockReference(baseCentro, definicao) { Layer = camada };
            espaco.AppendEntity(referencia);
            transacao.AddNewlyCreatedDBObject(referencia, true);
            ElectricalStore.SavePlacement(transacao, referencia, new EquipmentPlacement(equipamento.Kind, equipamento.Id));
        }

        transacao.Commit();
        return Math.Max(0, ids.Count - 1);
    }

    /// <summary>
    /// Depois de GRAVAR o cadastro (tag, dimensão): refaz o desenho do bloco
    /// do equipamento, se ele está em campo. A posição não muda. Se estava.
    /// </summary>
    internal static bool Redesenhar(Database database, EquipmentKind tipo, Guid id) => Redesenhar(database, tipo, [id]) > 0;

    /// <summary>O mesmo para vários do mesmo tipo, numa leitura do cadastro e numa transação. Quantos estavam em campo.</summary>
    internal static int Redesenhar(Database database, EquipmentKind tipo, IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return 0;

        var setup = ConfiguracaoEletricaStore.Ler(database).Setup;

        using var transacao = database.TransactionManager.StartTransaction();

        var posicionados = Posicionados(transacao, database);
        var feitos = 0;

        foreach (var id in ids)
        {
            if (setup.FindEquipment(tipo, id) is not { } equipamento || !posicionados.TryGetValue((tipo, id), out var referencias)) continue;

            GarantirBloco(transacao, database, equipamento);
            foreach (var referencia in referencias) ((BlockReference)transacao.GetObject(referencia, OpenMode.ForWrite)).RecordGraphicsModified(true);
            feitos++;
        }

        transacao.Commit();
        return feitos;
    }

    /// <summary>
    /// Tira o equipamento do campo (o cadastro dele foi apagado): apaga as
    /// referências (as cópias também: todas representam um cadastro que não
    /// existe mais) e a definição do bloco. Quantas referências apagou.
    /// </summary>
    internal static int Apagar(Database database, EquipmentKind tipo, Guid equipamento)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var ids = Posicionados(transacao, database).GetValueOrDefault((tipo, equipamento)) ?? [];
        foreach (var id in ids) transacao.GetObject(id, OpenMode.ForWrite).Erase();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var nome = NomeDoBloco(tipo, equipamento);
        if (tabela.Has(nome))
        {
            var definicao = (BlockTableRecord)transacao.GetObject(tabela[nome], OpenMode.ForRead);
            if (definicao.GetBlockReferenceIds(true, false).Cast<ObjectId>().All(r => r.IsErased))
            {
                definicao.UpgradeOpen();
                definicao.Erase();
            }
        }

        transacao.Commit();
        return ids.Count;
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
        EquipmentKind.Combiner => Color.FromRgb(0, 160, 120),
        _ => Color.FromRgb(40, 110, 200),
    };
}
