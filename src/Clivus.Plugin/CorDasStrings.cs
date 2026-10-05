using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A cor das strings pelo inversor (pedido do Renan em 05/10/2026: "cada
/// inversor pode ter uma cor, e cada string recebe a cor"): a polilinha do
/// traçado e os sinais dela (o + e o − e os círculos) ficam com a cor do
/// inversor; a string livre volta para ByLayer. É só representação: o
/// vínculo continua no XData da string (regra elétrica 1), e nada aqui o lê
/// para decidir coisa nenhuma.
/// </summary>
internal static class CorDasStrings
{
    /// <summary>A cor do AutoCAD: a RGB do inversor, ou ByLayer (null).</summary>
    internal static Color Cor(RgbColor? cor) =>
        cor is { } c ? Color.FromRgb(c.R, c.G, c.B) : Color.FromColorIndex(ColorMethod.ByLayer, 256);

    /// <summary>
    /// A cor de cada string pelo cadastro: a do inversor dela; livre, ou
    /// apontando para inversor que não está no cadastro, null (ByLayer).
    /// </summary>
    internal static Dictionary<Guid, RgbColor?> PeloCadastro(ElectricalSetup setup, IEnumerable<ElectricalString> strings)
    {
        var cores = new Dictionary<Guid, RgbColor?>();
        foreach (var s in strings) cores[s.Id] = s.IsAllocated ? setup.FindInverter(s.Inverter)?.Color : null;
        return cores;
    }

    /// <summary>
    /// Pinta as entidades das strings dadas (pelo GUID da string): a polilinha
    /// e os sinais. Só abre para escrita o que muda de cor. Quantas strings
    /// têm entidade no desenho.
    /// </summary>
    internal static int Pintar(Transaction transacao, Database database, IReadOnlyDictionary<Guid, RgbColor?> cores)
    {
        if (cores.Count == 0) return 0;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var classeDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
        var classeDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));
        var classeDoCirculo = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Circle));
        var pintadas = new HashSet<Guid>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || (id.ObjectClass != classeDaLinha && id.ObjectClass != classeDoTexto && id.ObjectClass != classeDoCirculo)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

            var dela = e is Polyline3d
                ? ElectricalStore.LoadString(e)?.Id
                : PluginXData.Load(e, StringSign.Tipo, 1, StringSign.FieldCount) is { } c ? StringSign.Parse(c)?.String : null;

            if (dela is not { } g || !cores.TryGetValue(g, out var cor)) continue;
            if (e is Polyline3d) pintadas.Add(g);

            var alvo = Cor(cor);
            if (e.Color.Equals(alvo)) continue;

            e.UpgradeOpen();
            e.Color = alvo;
        }

        return pintadas.Count;
    }

    /// <summary>
    /// Repinta pelo cadastro as strings do desenho (todas, ou só as do
    /// inversor dado), numa transação. Quantas strings foram conferidas.
    /// </summary>
    internal static int Repintar(Database database, Guid? inversor = null)
    {
        var setup = ConfiguracaoEletricaStore.Ler(database).Setup;

        using var transacao = database.TransactionManager.StartTransaction();
        var strings = ElectricalStore.Strings(transacao, database).Select(x => x.String).Where(s => inversor is not { } i || s.Inverter == i);
        var n = Pintar(transacao, database, PeloCadastro(setup, strings));
        transacao.Commit();
        return n;
    }
}
