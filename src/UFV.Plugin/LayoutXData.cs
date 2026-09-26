using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Grava e lê a identidade de mesa, pilar, módulo e face nas próprias
/// entidades, em XData: regra sagrada 3 gravada no desenho.
///
/// O formato do pacote mora em <see cref="PluginXData"/>. Os números vão em
/// texto invariante, formato redondo, como nos registros do desenho; "vazio"
/// é null (comprimento de pilar com problema, altura livre sem terreno).
/// </summary>
internal static class LayoutXData
{
    private const int VersaoDaMesa = 1;
    private const int VersaoDoPilar = 1;
    private const int VersaoDoModulo = 1;

    /// <summary>GUID, letreiro, cota inicial, cota final, inclinação, marcada, motivo.</summary>
    private const int CamposDaMesa = 7;

    /// <summary>GUID, mesa, número, estação, comprimento, enterro, altura livre, problema, terreno.</summary>
    private const int CamposDoPilar = 9;

    /// <summary>GUID, mesa, coluna, fileira, altura livre.</summary>
    private const int CamposDoModulo = 5;

    private const int VersaoDaFace = 1;

    /// <summary>GUID, módulo, mesa, coluna, fileira.</summary>
    private const int CamposDaFace = 5;

    internal static void SaveTable(Transaction transacao, Entity entidade, TableIdentity mesa) =>
        PluginXData.Save(
            transacao, entidade, TableIdentity.Tipo, VersaoDaMesa,
            mesa.Id.ToString("D"),
            mesa.Label,
            Numero(mesa.StartElevation),
            Numero(mesa.EndElevation),
            Numero(mesa.TiltRadians),
            mesa.Marked ? "1" : "0",
            mesa.Reason ?? string.Empty);

    internal static TableIdentity? LoadTable(Entity entidade)
    {
        var c = PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesa, CamposDaMesa);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id)) return null;
        if (!Real(c[2], out var z0) || !Real(c[3], out var z1) || !Real(c[4], out var tilt)) return null;

        var mesa = new TableIdentity(id, c[1], z0, z1, tilt, c[5] == "1", c[6].Length == 0 ? null : c[6]);

        return mesa.IsValid ? mesa : null;
    }

    internal static void SavePillar(Transaction transacao, Entity entidade, PillarIdentity pilar) =>
        PluginXData.Save(
            transacao, entidade, PillarIdentity.Tipo, VersaoDoPilar,
            pilar.Id.ToString("D"),
            pilar.Table.ToString("D"),
            pilar.Number.ToString(CultureInfo.InvariantCulture),
            Numero(pilar.Station),
            Opcional(pilar.Length),
            Numero(pilar.Embedment),
            Opcional(pilar.FreeHeight),
            pilar.Problem ?? string.Empty,
            Opcional(pilar.GroundZ));

    internal static PillarIdentity? LoadPillar(Entity entidade)
    {
        var c = PluginXData.Load(entidade, PillarIdentity.Tipo, VersaoDoPilar, CamposDoPilar);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;
        if (!int.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)) return null;
        if (!Real(c[3], out var estacao) || !Real(c[5], out var enterro)) return null;

        var pilar = new PillarIdentity(
            id, mesa, numero, estacao, RealOpcional(c[4]), enterro, RealOpcional(c[6]),
            c[7].Length == 0 ? null : c[7], RealOpcional(c[8]));

        return pilar.IsValid ? pilar : null;
    }

    internal static void SaveModule(Transaction transacao, Entity entidade, ModuleIdentity modulo) =>
        PluginXData.Save(
            transacao, entidade, ModuleIdentity.Tipo, VersaoDoModulo,
            modulo.Id.ToString("D"),
            modulo.Table.ToString("D"),
            modulo.Column.ToString(CultureInfo.InvariantCulture),
            modulo.Row.ToString(CultureInfo.InvariantCulture),
            Opcional(modulo.Clearance));

    internal static void SaveFace(Transaction transacao, Entity entidade, FaceIdentity face) =>
        PluginXData.Save(
            transacao, entidade, FaceIdentity.Tipo, VersaoDaFace,
            face.Id.ToString("D"),
            face.Module.ToString("D"),
            face.Table.ToString("D"),
            face.Column.ToString(CultureInfo.InvariantCulture),
            face.Row.ToString(CultureInfo.InvariantCulture));

    internal static FaceIdentity? LoadFace(Entity entidade)
    {
        var c = PluginXData.Load(entidade, FaceIdentity.Tipo, VersaoDaFace, CamposDaFace);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var modulo) || !Guid.TryParse(c[2], out var mesa)) return null;
        if (!int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var coluna)) return null;
        if (!int.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileira)) return null;

        var face = new FaceIdentity(id, modulo, mesa, coluna, fileira);

        return face.IsValid ? face : null;
    }

    internal static ModuleIdentity? LoadModule(Entity entidade)
    {
        var c = PluginXData.Load(entidade, ModuleIdentity.Tipo, VersaoDoModulo, CamposDoModulo);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;
        if (!int.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var coluna)) return null;
        if (!int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileira)) return null;

        var modulo = new ModuleIdentity(id, mesa, coluna, fileira, RealOpcional(c[4]));

        return modulo.IsValid ? modulo : null;
    }

    private static string Numero(double valor) => valor.ToString("R", CultureInfo.InvariantCulture);

    private static string Opcional(double? valor) => valor is { } v ? Numero(v) : string.Empty;

    private static bool Real(string texto, out double valor) =>
        double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor) && double.IsFinite(valor);

    private static double? RealOpcional(string texto) =>
        texto.Length > 0 && Real(texto, out var v) ? v : null;
}
