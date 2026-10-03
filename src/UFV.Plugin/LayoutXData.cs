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
    /// <summary>
    /// Versão 2 desde o 7.1: ganhou o estado sujo e o motivo. A versão 1
    /// (etapa 5) continua a ser lida, como limpa.
    /// </summary>
    /// <summary>
    /// Versão 5 desde 27/09/2026: ganhou as alturas das pontas escolhidas à
    /// mão. A 4 (âncora, 7.7), a 3 (potência), a 2 (estado sujo) e a 1
    /// (etapa 5) continuam a ser lidas. Sempre acrescentar no fim: os testes
    /// de nível 2 leem campo por posição.
    /// </summary>
    private const int VersaoDaMesa = 6;
    private const int VersaoDaMesaCinco = 5;
    private const int VersaoDaMesaQuatro = 4;
    private const int VersaoDaMesaTres = 3;
    private const int VersaoDaMesaDois = 2;
    private const int VersaoDaMesaAntiga = 1;
    private const int VersaoDoPilar = 2;
    private const int VersaoDoPilarAntiga = 1;
    private const int VersaoDoModulo = 1;

    /// <summary>GUID, letreiro, cota inicial, cota final, inclinação, marcada, motivo, suja, motivo da sujeira, potência do módulo, âncora X, Y, Z, ponta à mão no primeiro e no último pilar.</summary>
    private const int CamposDaMesa = 16;

    /// <summary>Os quinze primeiros, na versão 5 (sem o nome do perfil, 8.6).</summary>
    private const int CamposDaMesaCinco = 15;

    /// <summary>Os treze primeiros, na versão 4.</summary>
    private const int CamposDaMesaQuatro = 13;

    /// <summary>Os dez primeiros, na versão 3.</summary>
    private const int CamposDaMesaTres = 10;

    /// <summary>Os nove primeiros, na versão 2.</summary>
    private const int CamposDaMesaDois = 9;

    /// <summary>Os sete primeiros, na versão 1.</summary>
    private const int CamposDaMesaAntiga = 7;

    /// <summary>GUID, mesa, número, estação, comprimento, enterro, altura livre, problema, terreno, altura livre na ponta baixa, na ponta alta.</summary>
    private const int CamposDoPilar = 11;

    /// <summary>Os nove primeiros, na versão 1 (sem as pontas).</summary>
    private const int CamposDoPilarAntigo = 9;

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
            mesa.Reason ?? string.Empty,
            mesa.Dirty ? "1" : "0",
            mesa.DirtyReason ?? string.Empty,
            Opcional(mesa.ModulePowerWatts),
            Opcional(mesa.Anchor?.X),
            Opcional(mesa.Anchor?.Y),
            Opcional(mesa.Anchor?.Z),
            Opcional(mesa.ManualFirstLowEdge),
            Opcional(mesa.ManualLastLowEdge),
            mesa.ProfileName ?? string.Empty);

    internal static TableIdentity? LoadTable(Entity entidade)
    {
        var c = PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesa, CamposDaMesa)
            ?? PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesaCinco, CamposDaMesaCinco)
            ?? PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesaQuatro, CamposDaMesaQuatro)
            ?? PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesaTres, CamposDaMesaTres)
            ?? PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesaDois, CamposDaMesaDois)
            ?? PluginXData.Load(entidade, TableIdentity.Tipo, VersaoDaMesaAntiga, CamposDaMesaAntiga);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id)) return null;
        if (!Real(c[2], out var z0) || !Real(c[3], out var z1) || !Real(c[4], out var tilt)) return null;

        // Leitura tolerante: suja é "1", e uma suja sem motivo ganha um motivo
        // genérico; limpa ignora o que houver no motivo. Rejeitar aqui apagaria
        // a identidade da mesa inteira por dois caracteres errados no XData,
        // e a identidade é o que a regra sagrada 3 protege.
        var suja = c.Count >= CamposDaMesaDois && c[7] == "1";
        var motivo = suja ? (c[8].Length > 0 ? c[8] : "motivo perdido") : null;
        var potencia = c.Count >= CamposDaMesaTres ? RealOpcional(c[9]) : null;

        if (potencia is { } w && (!double.IsFinite(w) || w <= 0)) potencia = null;

        UFV.Geo.Point3? ancora = null;

        if (c.Count >= CamposDaMesaQuatro && RealOpcional(c[10]) is { } ax && RealOpcional(c[11]) is { } ay && RealOpcional(c[12]) is { } az)
        {
            var p = new UFV.Geo.Point3(ax, ay, az);
            if (p.IsFinite) ancora = p;
        }

        // As pontas à mão valem aos pares: uma só gravada é lida como nenhuma.
        double? primeira = c.Count >= CamposDaMesaCinco ? RealOpcional(c[13]) : null;
        double? ultima = c.Count >= CamposDaMesaCinco ? RealOpcional(c[14]) : null;

        // O perfil de que a mesa é (8.6); antes da versão 6, não se sabe.
        var perfil = c.Count >= CamposDaMesa && c[15].Length > 0 ? c[15] : null;

        if (primeira is not { } a || ultima is not { } b || !double.IsFinite(a) || !double.IsFinite(b))
            primeira = ultima = null;

        var mesa = new TableIdentity(
            id, c[1], z0, z1, tilt, c[5] == "1", c[6].Length == 0 ? null : c[6], suja, motivo, potencia, ancora, primeira, ultima, perfil);

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
            Opcional(pilar.GroundZ),
            Opcional(pilar.LowEdgeClearance),
            Opcional(pilar.HighEdgeClearance));

    internal static PillarIdentity? LoadPillar(Entity entidade)
    {
        var c = PluginXData.Load(entidade, PillarIdentity.Tipo, VersaoDoPilar, CamposDoPilar)
            ?? PluginXData.Load(entidade, PillarIdentity.Tipo, VersaoDoPilarAntiga, CamposDoPilarAntigo);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;
        if (!int.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)) return null;
        if (!Real(c[3], out var estacao) || !Real(c[5], out var enterro)) return null;

        var pilar = new PillarIdentity(
            id, mesa, numero, estacao, RealOpcional(c[4]), enterro, RealOpcional(c[6]),
            c[7].Length == 0 ? null : c[7], RealOpcional(c[8]),
            c.Count >= CamposDoPilar ? RealOpcional(c[9]) : null,
            c.Count >= CamposDoPilar ? RealOpcional(c[10]) : null);

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

    private const int VersaoDaTag = 1;

    /// <summary>GUID, mesa, tipo, texto.</summary>
    private const int CamposDaTag = 4;

    internal static void SaveTag(Transaction transacao, Entity entidade, TagIdentity tag) =>
        PluginXData.Save(transacao, entidade, TagIdentity.Tipo, VersaoDaTag, tag.Id.ToString("D"), tag.Table.ToString("D"), tag.Kind.ToString(), tag.Text);

    internal static TagIdentity? LoadTag(Entity entidade)
    {
        var c = PluginXData.Load(entidade, TagIdentity.Tipo, VersaoDaTag, CamposDaTag);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;
        if (!Enum.TryParse<TagKind>(c[2], out var tipo)) return null;

        var tag = new TagIdentity(id, mesa, tipo, c[3]);

        return tag.IsValid ? tag : null;
    }

    private const int VersaoDoTextoDeAnalise = 1;

    /// <summary>GUID, mesa, análise, valor.</summary>
    private const int CamposDoTextoDeAnalise = 4;

    internal static void SaveAnalysisText(Transaction transacao, Entity entidade, AnalysisTextIdentity texto) =>
        PluginXData.Save(
            transacao, entidade, AnalysisTextIdentity.Tipo, VersaoDoTextoDeAnalise,
            texto.Id.ToString("D"), texto.Table.ToString("D"), texto.Kind.ToString(),
            texto.Value.ToString("R", CultureInfo.InvariantCulture));

    internal static AnalysisTextIdentity? LoadAnalysisText(Entity entidade)
    {
        var c = PluginXData.Load(entidade, AnalysisTextIdentity.Tipo, VersaoDoTextoDeAnalise, CamposDoTextoDeAnalise);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;
        if (!Enum.TryParse<IndependentKind>(c[2], out var tipo)) return null;
        if (!double.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)) return null;

        var texto = new AnalysisTextIdentity(id, mesa, tipo, valor);

        return texto.IsValid ? texto : null;
    }

    private const int VersaoDaNota = 1;

    /// <summary>GUID, mesa.</summary>
    private const int CamposDaNota = 2;

    internal static void SaveNote(Transaction transacao, Entity entidade, NoteIdentity nota) =>
        PluginXData.Save(transacao, entidade, NoteIdentity.Tipo, VersaoDaNota, nota.Id.ToString("D"), nota.Table.ToString("D"));

    internal static NoteIdentity? LoadNote(Entity entidade)
    {
        var c = PluginXData.Load(entidade, NoteIdentity.Tipo, VersaoDaNota, CamposDaNota);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var mesa)) return null;

        var nota = new NoteIdentity(id, mesa);

        return nota.IsValid ? nota : null;
    }

    private const int VersaoDaMarca = 1;

    /// <summary>GUID do grupo.</summary>
    private const int CamposDaMarca = 1;

    internal static void SaveGroupMark(Transaction transacao, Entity entidade, GroupMarkIdentity marca) =>
        PluginXData.Save(transacao, entidade, GroupMarkIdentity.Tipo, VersaoDaMarca, marca.Group.ToString("D"));

    internal static GroupMarkIdentity? LoadGroupMark(Entity entidade)
    {
        var c = PluginXData.Load(entidade, GroupMarkIdentity.Tipo, VersaoDaMarca, CamposDaMarca);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var grupo)) return null;

        var marca = new GroupMarkIdentity(grupo);

        return marca.IsValid ? marca : null;
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

    private const int VersaoDaArvore = 1;

    /// <summary>GUID, altura e largura do tronco, altura e largura da copa.</summary>
    private const int CamposDaArvore = 5;

    /// <summary>A árvore (9.4): o GUID e as medidas, no XData do bloco.</summary>
    internal static void SaveTree(Transaction transacao, Entity entidade, TreeIdentity arvore) =>
        PluginXData.Save(
            transacao, entidade, TreeIdentity.Tipo, VersaoDaArvore,
            arvore.Id.ToString("D"),
            Numero(arvore.Spec.TrunkHeight),
            Numero(arvore.Spec.TrunkWidth),
            Numero(arvore.Spec.CrownHeight),
            Numero(arvore.Spec.CrownWidth));

    internal static TreeIdentity? LoadTree(Entity entidade)
    {
        var c = PluginXData.Load(entidade, TreeIdentity.Tipo, VersaoDaArvore, CamposDaArvore);
        if (c is null) return null;

        if (!Guid.TryParse(c[0], out var id)) return null;
        if (!Real(c[1], out var ht) || !Real(c[2], out var lt) || !Real(c[3], out var hc) || !Real(c[4], out var lc)) return null;

        var arvore = new TreeIdentity(id, new TreeSpec(ht, lt, hc, lc));

        return arvore.IsValid ? arvore : null;
    }

    private static string Numero(double valor) => valor.ToString("R", CultureInfo.InvariantCulture);

    private static string Opcional(double? valor) => valor is { } v ? Numero(v) : string.Empty;

    private static bool Real(string texto, out double valor) =>
        double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor) && double.IsFinite(valor);

    private static double? RealOpcional(string texto) =>
        texto.Length > 0 && Real(texto, out var v) ? v : null;
}
