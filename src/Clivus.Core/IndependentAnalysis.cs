using System.Globalization;

namespace Clivus.Core;

/// <summary>As análises independentes do menu Análises (passos 8.9 a 8.11).</summary>
public enum IndependentKind
{
    /// <summary>Altura livre da ponta baixa do módulo, em cada pilar (PB).</summary>
    LowEdge,

    /// <summary>Altura livre da ponta alta do módulo, em cada pilar (PA).</summary>
    HighEdge,

    /// <summary>Declividade da mesa ao longo da fileira.</summary>
    Slope,

    /// <summary>Parte livre do pilar: o comprimento fora da terra (P).</summary>
    PillarAbove,

    /// <summary>Parte enterrada do pilar (E). Renan, 02/10/2026: "enterrado menos de x m ou mais de x m?".</summary>
    PillarBuried,

    /// <summary>Comprimento total do pilar (PT). Renan, 02/10/2026: "preciso também pilares maior que x m".</summary>
    PillarLength,
}

/// <summary>Em que faixa um valor cai.</summary>
public enum Band
{
    Below,
    Inside,
    Above,
}

/// <summary>
/// A regra de uma análise: abaixo de X, uma cor; acima de Y, outra; entre
/// os dois, sem cor. Qualquer um dos dois lados pode ficar desligado (null).
/// Melhorias.docx, 01/10/2026: "inferior a X pintar de (escolher cor),
/// superior a X pintar de (escolher cor)".
/// </summary>
/// <param name="Below">O limite de baixo, ou null.</param>
/// <param name="BelowColor">A cor de quem fica abaixo dele.</param>
/// <param name="Above">O limite de cima, ou null.</param>
/// <param name="AboveColor">A cor de quem fica acima dele.</param>
/// <param name="PaintPieces">
/// Se pinta também as peças (módulos nas pontas, contorno na declividade,
/// pilares nos pilares), além dos textos.
/// </param>
public sealed record ThresholdRule(double? Below, RgbColor BelowColor, double? Above, RgbColor AboveColor, bool PaintPieces)
{
    /// <summary>Se a regra serve.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>Por que a regra não serve, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            if (Below is null && Above is null) return Tr.T("ligue pelo menos um dos dois limites");
            if (Below is { } b && !double.IsFinite(b)) return Tr.T("o limite de baixo não é um número");
            if (Above is { } a && !double.IsFinite(a)) return Tr.T("o limite de cima não é um número");

            if (Below is { } baixo && Above is { } cima && baixo > cima)
                return Tr.T("o limite de baixo é maior que o de cima");

            return null;
        }
    }

    /// <summary>A faixa do valor. Igual ao limite fica dentro.</summary>
    public Band Classify(double valor)
    {
        if (Below is { } baixo && valor < baixo) return Band.Below;
        if (Above is { } cima && valor > cima) return Band.Above;
        return Band.Inside;
    }

    /// <summary>A cor do valor, ou null quando ele fica dentro.</summary>
    public RgbColor? ColorOf(double valor) => Classify(valor) switch
    {
        Band.Below => BelowColor,
        Band.Above => AboveColor,
        _ => null,
    };
}

/// <summary>Quantos valores caíram em cada faixa; <see cref="Missing"/> são os sem valor (sem terreno).</summary>
public sealed record BandCount(int Below, int Inside, int Above, int Missing)
{
    /// <summary>Todos, com e sem valor.</summary>
    public int Total => Below + Inside + Above + Missing;

    /// <summary>A contagem de uma lista de valores; NaN conta como sem valor.</summary>
    public static BandCount Of(ThresholdRule regra, IEnumerable<double> valores)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(valores);

        int abaixo = 0, dentro = 0, acima = 0, sem = 0;

        foreach (var v in valores)
        {
            if (!double.IsFinite(v))
            {
                sem++;
                continue;
            }

            switch (regra.Classify(v))
            {
                case Band.Below: abaixo++; break;
                case Band.Above: acima++; break;
                default: dentro++; break;
            }
        }

        return new BandCount(abaixo, dentro, acima, sem);
    }
}

/// <summary>
/// O que as análises independentes têm em comum: o texto, a camada, a regra
/// padrão e a gravação da regra no desenho.
/// </summary>
public static class IndependentAnalysis
{
    /// <summary>A versão do registro da regra no desenho.</summary>
    private const string Versao = "1";

    /// <summary>O nome da análise para o usuário.</summary>
    public static string Name(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => Tr.T("ponta baixa"),
        IndependentKind.HighEdge => Tr.T("ponta alta"),
        IndependentKind.Slope => Tr.T("declividade"),
        IndependentKind.PillarBuried => Tr.T("parte enterrada do pilar"),
        IndependentKind.PillarLength => Tr.T("comprimento total do pilar"),
        _ => Tr.T("parte livre do pilar (fora da terra)"),
    };

    /// <summary>Se a análise é de pilar (um valor por pilar, escrito no pilar).</summary>
    public static bool IsPillar(IndependentKind tipo) =>
        tipo is IndependentKind.PillarAbove or IndependentKind.PillarBuried or IndependentKind.PillarLength;

    /// <summary>
    /// O valor do pilar para a análise: a parte livre, a enterrada ou o
    /// comprimento total; NaN quando o pilar não tem (sem terreno).
    /// </summary>
    public static double PillarValue(IndependentKind tipo, PillarIdentity pilar)
    {
        ArgumentNullException.ThrowIfNull(pilar);

        return tipo switch
        {
            IndependentKind.PillarBuried => pilar.GroundZ is null ? double.NaN : pilar.Embedment,
            IndependentKind.PillarLength => pilar.Length ?? double.NaN,
            _ => pilar.FreeHeight ?? double.NaN,
        };
    }

    /// <summary>O texto que vai ao desenho: "PB 0,45", "PA 2,10", "P 1,86", "5,0%".</summary>
    public static string Label(IndependentKind tipo, double valor, SlopeUnit unidade) => tipo switch
    {
        IndependentKind.LowEdge => Tr.F("PB {0:0.00}", valor),
        IndependentKind.HighEdge => Tr.F("PA {0:0.00}", valor),
        IndependentKind.PillarAbove => Tr.F("P {0:0.00}", valor),
        IndependentKind.PillarBuried => Tr.F("E {0:0.00}", valor),
        IndependentKind.PillarLength => Tr.F("PT {0:0.00}", valor),
        _ => unidade == SlopeUnit.Degrees ? $"{valor.ToString("0.0", Tr.Culture)}°" : $"{valor.ToString("0.0", Tr.Culture)}%",
    };

    /// <summary>A camada dos textos da análise. Só para o usuário ligar e desligar: a identidade vai no XData.</summary>
    public static string LayerName(IndependentKind tipo) => PluginInfo.PrefixoDeDados + "_TXT_" + tipo switch
    {
        IndependentKind.LowEdge => "PONTA_BAIXA",
        IndependentKind.HighEdge => "PONTA_ALTA",
        IndependentKind.Slope => "DECLIVIDADE",
        IndependentKind.PillarBuried => "PILAR_ENTERRADO",
        IndependentKind.PillarLength => "PILAR_TOTAL",
        _ => "PILAR",
    };

    /// <summary>A chave do registro da regra no dicionário do desenho.</summary>
    public static string StorageKey(IndependentKind tipo) => "ANALISE_" + tipo.ToString().ToUpperInvariant();

    /// <summary>
    /// A declividade na unidade escolhida, sempre positiva: % (desnível
    /// sobre a distância em planta) ou graus.
    /// </summary>
    public static double SlopeValue(double desnivel, double emPlanta, SlopeUnit unidade)
    {
        var tangente = Math.Abs(desnivel) / emPlanta;
        return unidade == SlopeUnit.Degrees ? Math.Atan(tangente) * 180 / Math.PI : tangente * 100;
    }

    /// <summary>
    /// A regra com que a análise nasce. Ponta baixa: abaixo de 0,30 m
    /// vermelho, acima de 1,20 m azul (a faixa padrão da configuração);
    /// ponta alta: acima de 3,00 m; declividade: acima de 10%; parte livre do
    /// pilar: acima de 3,00 m; parte enterrada: abaixo de 1,10 m (a cravação
    /// mínima de costume); comprimento total: acima de 4,50 m. Padrões meus,
    /// o Renan muda na janela.
    /// </summary>
    public static ThresholdRule Default(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => new ThresholdRule(0.30, RgbColor.Red, 1.20, RgbColor.Blue, PaintPieces: false),
        IndependentKind.HighEdge => new ThresholdRule(null, RgbColor.Red, 3.00, RgbColor.Blue, PaintPieces: false),
        IndependentKind.Slope => new ThresholdRule(null, RgbColor.Blue, 10.0, RgbColor.Red, PaintPieces: false),
        IndependentKind.PillarBuried => new ThresholdRule(1.10, RgbColor.Red, null, RgbColor.Blue, PaintPieces: false),
        IndependentKind.PillarLength => new ThresholdRule(null, RgbColor.Blue, 4.50, RgbColor.Red, PaintPieces: false),
        _ => new ThresholdRule(null, RgbColor.Blue, 3.00, RgbColor.Red, PaintPieces: false),
    };

    /// <summary>A regra como texto, campo a campo, para o registro do desenho.</summary>
    public static IReadOnlyList<string> Encode(ThresholdRule regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        return
        [
            Versao,
            regra.Below is { } b ? b.ToString("R", CultureInfo.InvariantCulture) : "",
            regra.BelowColor.ToHex(),
            regra.Above is { } a ? a.ToString("R", CultureInfo.InvariantCulture) : "",
            regra.AboveColor.ToHex(),
            regra.PaintPieces ? "1" : "0",
        ];
    }

    /// <summary>A regra lida do registro; ilegível ou de outra versão, a padrão do tipo.</summary>
    public static ThresholdRule Decode(IReadOnlyList<string> campos, IndependentKind tipo)
    {
        ArgumentNullException.ThrowIfNull(campos);

        if (campos.Count != 6 || campos[0] != Versao) return Default(tipo);

        if (!Opcional(campos[1], out var abaixo) || !Opcional(campos[3], out var acima)) return Default(tipo);
        if (!RgbColor.TryParseHex(campos[2], out var corAbaixo) || !RgbColor.TryParseHex(campos[4], out var corAcima)) return Default(tipo);

        var regra = new ThresholdRule(abaixo, corAbaixo, acima, corAcima, campos[5] == "1");

        return regra.IsValid ? regra : Default(tipo);
    }

    /// <summary>A linha da quantificação para o usuário.</summary>
    public static string Describe(IndependentKind tipo, ThresholdRule regra, BandCount conta, SlopeUnit unidade)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(conta);

        var partes = new List<string>();

        if (regra.Below is { } b) partes.Add(Tr.F("{0} abaixo de {1}", conta.Below, Limite(tipo, b, unidade)));
        partes.Add(Tr.F("{0} dentro", conta.Inside));
        if (regra.Above is { } a) partes.Add(Tr.F("{0} acima de {1}", conta.Above, Limite(tipo, a, unidade)));
        if (conta.Missing > 0) partes.Add(Tr.F("{0} sem valor", conta.Missing));

        return Tr.F("{0}: {1} (total {2})", Name(tipo), string.Join(", ", partes), conta.Total);
    }

    private static string Limite(IndependentKind tipo, double valor, SlopeUnit unidade) =>
        tipo == IndependentKind.Slope
            ? (unidade == SlopeUnit.Degrees ? $"{valor.ToString("0.0", Tr.Culture)}°" : $"{valor.ToString("0.0", Tr.Culture)}%")
            : $"{valor.ToString("0.00", Tr.Culture)} m";

    private static bool Opcional(string texto, out double? valor)
    {
        valor = null;
        if (string.IsNullOrEmpty(texto)) return true;

        if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var lido) || !double.IsFinite(lido))
            return false;

        valor = lido;
        return true;
    }
}
