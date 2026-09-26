using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// As definições de bloco do pilar e do módulo, garantidas no desenho.
///
/// Decisão do Renan em 23/09/2026: módulo é bloco próprio, pilar é bloco
/// próprio. O pilar é um bloco só, uma caixa unitária que cada instância
/// escala para a largura, a profundidade e o comprimento dele — o comprimento
/// varia pilar a pilar, e um bloco por comprimento seria um bloco por pilar.
/// O módulo é um bloco por modelo (largura × altura × espessura), com a face
/// superior em z = 0, e cada instância leva a matriz da mesa.
///
/// A face superior NÃO fica dentro do bloco: é entidade separada, na camada
/// de face, com identidade própria (a decisão do Renan sobre o PVsyst, em
/// PROGRESSO.md). O bloco é conveniência para quem mexe no CAD; a face é o
/// que sai pela porta.
/// </summary>
internal static class LayoutBlocks
{
    /// <summary>O bloco do pilar: caixa unitária de x, y ∈ [−½, ½] e z ∈ [−1, 0], o topo na origem.</summary>
    internal const string Pilar = PluginInfo.PrefixoDeDados + "_PILAR";

    private const string PrefixoDoModulo = PluginInfo.PrefixoDeDados + "_MODULO_";

    /// <summary>Garante o bloco do pilar e devolve o id da definição.</summary>
    internal static ObjectId GarantirPilar(Transaction transacao, Database database)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

        if (tabela.Has(Pilar)) return tabela[Pilar];

        if (!tabela.IsWriteEnabled) tabela.UpgradeOpen();

        var definicao = new BlockTableRecord { Name = Pilar, Origin = Point3d.Origin };
        var id = tabela.Add(definicao);
        transacao.AddNewlyCreatedDBObject(definicao, true);

        // A caixa nasce centrada na origem; desce para o topo ficar em z = 0.
        var caixa = new Solid3d();
        caixa.CreateBox(1, 1, 1);
        caixa.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, -0.5)));
        CorPorBloco(caixa);

        definicao.AppendEntity(caixa);
        transacao.AddNewlyCreatedDBObject(caixa, true);

        return id;
    }

    /// <summary>
    /// Garante o bloco do módulo deste modelo e devolve o id da definição.
    /// A caixa vai de (0, 0, −espessura) a (largura, altura, 0): a face
    /// superior em z = 0 e o canto da ponta baixa esquerda na origem, como a
    /// geometria local da mesa.
    /// </summary>
    internal static ObjectId GarantirModulo(Transaction transacao, Database database, SolarModule modulo)
    {
        ArgumentNullException.ThrowIfNull(modulo);

        var nome = PrefixoDoModulo + NomeSeguro(modulo.Model);
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

        if (tabela.Has(nome)) return tabela[nome];

        if (!tabela.IsWriteEnabled) tabela.UpgradeOpen();

        var definicao = new BlockTableRecord { Name = nome, Origin = Point3d.Origin };
        var id = tabela.Add(definicao);
        transacao.AddNewlyCreatedDBObject(definicao, true);

        var caixa = new Solid3d();
        caixa.CreateBox(modulo.Width, modulo.Height, modulo.Thickness);
        caixa.TransformBy(Matrix3d.Displacement(new Vector3d(modulo.Width / 2, modulo.Height / 2, -modulo.Thickness / 2)));
        CorPorBloco(caixa);

        definicao.AppendEntity(caixa);
        transacao.AddNewlyCreatedDBObject(caixa, true);

        return id;
    }

    /// <summary>
    /// A entidade dentro do bloco na camada 0 com cor POR BLOCO (índice 0).
    ///
    /// Por camada não serve: a regra do AutoCAD é que entidade aninhada "por
    /// camada" na camada 0 toma a cor da CAMADA da instância, e não a cor
    /// posta na instância — a pintura das análises (5.6/5.7), que é uma cor
    /// na instância, não apareceria. A revisão do 5.7 pegou: módulo com
    /// ponta baixa fora e pilar com problema saíam brancos.
    /// </summary>
    private static void CorPorBloco(Entity entidade)
    {
        entidade.Layer = "0";
        entidade.ColorIndex = 0;
    }

    /// <summary>
    /// O nome do modelo como nome de bloco: só o que o AutoCAD aceita. Os
    /// caracteres proibidos viram sublinhado; dois modelos que só diferem
    /// neles cairiam no mesmo bloco, o que é aceitável para uma caixa da
    /// mesma medida e está registrado.
    /// </summary>
    private static string NomeSeguro(string modelo)
    {
        var texto = new System.Text.StringBuilder(modelo.Length);

        foreach (var c in modelo.Trim())
            texto.Append(LayerName.WhyInvalid(c.ToString()) is null && c != ' ' ? c : '_');

        var nome = texto.ToString();

        return nome.Length == 0 ? "SEM_MODELO" : nome;
    }
}
