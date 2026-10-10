using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// As características elétricas de um módulo, lidas de um arquivo .PAN do
/// PVsyst (roteamento, 22.3). Tensões em V, correntes em A, potência em W;
/// os coeficientes de temperatura em V/°C (tensão) e A/°C (corrente); o da
/// potência (muPmpReq) em %/°C, quando o PAN traz.
/// </summary>
public sealed record PanModule(
    string Manufacturer,
    string Model,
    double Pmax,
    double Voc,
    double Isc,
    double Vmp,
    double Imp,
    double VocCoefficient,
    double IscCoefficient,
    double? Noct,
    double? PowerCoefficient = null)
{
    public const int FieldCount = 11;

    /// <summary>
    /// O coeficiente relativo da Vmp (1/°C): o PAN não traz o da Vmp; com o
    /// da potência, β_Vmp ≈ γ_Pmp − α_Imp (≈ α_Isc relativo); sem ele, o da
    /// Voc em proporção (otimista: a Vmp cai mais rápido que a Voc).
    /// </summary>
    public double VmpRelativeCoefficient =>
        PowerCoefficient is { } g ? g / 100 - IscCoefficient / Isc : VocCoefficient / Voc;

    /// <summary>
    /// Lê o texto do .PAN (linhas "Chave=valor"; o PVsyst grava com ponto
    /// decimal). Nunca inventa valor: campo faltando ou ilegível vai para
    /// <paramref name="missing"/> pelo nome do .PAN, e o resultado é null.
    /// </summary>
    public static PanModule? Parse(string texto, out IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linha in texto.Split('\n'))
        {
            var i = linha.IndexOf('=');
            if (i <= 0) continue;
            var chave = linha[..i].Trim();
            // A primeira ocorrência vale (o bloco do módulo vem antes dos sub-blocos).
            valores.TryAdd(chave, linha[(i + 1)..].Trim());
        }

        var faltam = new List<string>();

        double Numero(string chave)
        {
            if (valores.TryGetValue(chave, out var t) && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v))
                return v;
            faltam.Add(chave);
            return double.NaN;
        }

        var pmax = Numero("PNom");
        var voc = Numero("Voc");
        var isc = Numero("Isc");
        var vmp = Numero("Vmp");
        var imp = Numero("Imp");
        var muVoc = Numero("muVocSpec");   // mV/°C
        var muIsc = Numero("muISC");       // mA/°C

        double? Opcional(string chave) =>
            valores.TryGetValue(chave, out var x) && double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        var noct = Opcional("NOCT");
        var muPmp = Opcional("muPmpReq");   // %/°C
        valores.TryGetValue("Manufacturer", out var fabricante);
        if (!valores.TryGetValue("Model", out var modelo) || string.IsNullOrWhiteSpace(modelo)) faltam.Add("Model");

        missing = faltam;
        if (faltam.Count > 0) return null;

        var m = new PanModule(fabricante ?? string.Empty, modelo!, pmax, voc, isc, vmp, imp, muVoc / 1000, muIsc / 1000, noct, muPmp);
        if (m.WhyInvalid() is { } porque)
        {
            missing = [porque];
            return null;
        }

        return m;
    }

    /// <summary>Por que os números não fazem sentido (null se fazem).</summary>
    public string? WhyInvalid()
    {
        if (string.IsNullOrWhiteSpace(Model)) return "Model";
        if (!(Pmax > 0) || !(Voc > 0) || !(Isc > 0) || !(Vmp > 0) || !(Imp > 0)) return Tr.T("potência, tensões e correntes têm que ser maiores que zero");
        if (Vmp > Voc || Imp > Isc) return Tr.T("Vmp maior que Voc ou Imp maior que Isc");
        if (!double.IsFinite(VocCoefficient) || !double.IsFinite(IscCoefficient)) return Tr.T("coeficiente de temperatura ilegível");
        return null;
    }

    public IReadOnlyList<string> ToFields() =>
    [
        Manufacturer, Model, Num(Pmax), Num(Voc), Num(Isc), Num(Vmp), Num(Imp), Num(VocCoefficient), Num(IscCoefficient),
        Noct is { } n ? Num(n) : string.Empty,
        PowerCoefficient is { } g ? Num(g) : string.Empty,
    ];

    public static PanModule? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount) return null;
        var v = new double[7];
        for (var i = 0; i < 7; i++)
            if (!RouteSettings.Real(c[2 + i], out v[i])) return null;
        double? Opcional(string t) => t.Length == 0 ? null : RouteSettings.Real(t, out var x) ? x : double.NaN;
        var noct = Opcional(c[9]);
        var gama = Opcional(c[10]);
        if (noct is double.NaN || gama is double.NaN) return null;

        var m = new PanModule(c[0], c[1], v[0], v[1], v[2], v[3], v[4], v[5], v[6], noct, gama);
        return m.WhyInvalid() is null ? m : null;
    }

    private static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
