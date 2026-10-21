namespace Clivus.Core;

/// <summary>
/// Chuva e escoamento superficial (passo 31.1), como no memorial de
/// Valparaiso (plano/drenagem/02-calculos-hidraulicos.md, itens 1 a 3).
/// </summary>
public static class Rainfall
{
    /// <summary>
    /// Intensidade pluviométrica pela equação IDF do lugar: I = K·T^a / (t + b)^c.
    /// I em mm/h, T em anos, t em minutos.
    /// </summary>
    public static double Intensity(double k, double a, double b, double c, double returnPeriodYears, double durationMinutes)
    {
        if (!(returnPeriodYears > 0) || !(durationMinutes > 0) || !(durationMinutes + b > 0))
            throw new ArgumentException(Tr.T("Período de retorno e duração da chuva precisam ser positivos."));

        return k * Math.Pow(returnPeriodYears, a) / Math.Pow(durationMinutes + b, c);
    }

    /// <summary>Tempo de concentração de Kirpich, em min: 57·(L³/ΔH)^0,385, L em km, ΔH em m.</summary>
    public static double Kirpich(double lengthMeters, double dropMeters)
    {
        if (!(lengthMeters > 0) || !(dropMeters > 0)) return double.NaN;

        var km = lengthMeters / 1000;
        return 57 * Math.Pow(km * km * km / dropMeters, 0.385);
    }

    /// <summary>
    /// Tempo de concentração de Kerby na forma do memorial, em min:
    /// 37·(N·L/S)^0,47, L em km, S em %, N a rugosidade de Kerby (0,5 no
    /// memorial). Não é a Kerby "de livro"; é a que reproduz a Tabela 1
    /// (bacia 1: 7,05 min), e a regra 6 manda seguir o memorial.
    /// </summary>
    public static double Kerby(double lengthMeters, double slopePercent, double kerbyN = 0.5)
    {
        if (!(lengthMeters > 0) || !(slopePercent > 0)) return double.NaN;

        return 37 * Math.Pow(kerbyN * lengthMeters / 1000 / slopePercent, 0.47);
    }

    /// <summary>
    /// O tempo de concentração adotado: o menor de Kirpich e Kerby (mais
    /// chuva, a favor da segurança), nunca abaixo do mínimo (5 min).
    /// </summary>
    public static double AdoptedTc(double kirpich, double kerby, double minimumMinutes = 5)
    {
        var candidatos = new[] { kirpich, kerby }.Where(double.IsFinite).ToList();
        var tc = candidatos.Count > 0 ? candidatos.Min() : minimumMinutes;
        return Math.Max(minimumMinutes, tc);
    }

    /// <summary>Velocidade do escoamento superficial V = L / tc, em m/s (L em m, tc em min).</summary>
    public static double SurfaceVelocity(double lengthMeters, double tcMinutes) =>
        tcMinutes > 0 ? lengthMeters / (tcMinutes * 60) : double.NaN;

    /// <summary>Vazão pelo método racional, em l/s: Q = C·I·A / 3600 (I em mm/h, A em m²).</summary>
    public static double RationalFlowLitersPerSecond(double runoffC, double intensityMmPerHour, double areaSquareMeters) =>
        runoffC * intensityMmPerHour * areaSquareMeters / 3600;

    /// <summary>O método racional vale para bacias até 50 ha.</summary>
    public const double RationalMaxAreaSquareMeters = 500_000;
}

/// <summary>Manning, lâmina normal, bordo livre e altura (passo 31.1, itens 4 a 7 do 02-calculos).</summary>
public static class Hydraulics
{
    /// <summary>Velocidade de Manning v = Rh^(2/3)·I^(1/2)/n, em m/s.</summary>
    public static double Velocity(ChannelSection section, double depth, double slope)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!(slope > 0) || !(depth > 0)) return 0;

        return Math.Pow(section.HydraulicRadius(depth), 2.0 / 3) * Math.Sqrt(slope) / section.Manning;
    }

    /// <summary>A vazão (m³/s) com a lâmina h e a declividade I.</summary>
    public static double Flow(ChannelSection section, double depth, double slope) =>
        Velocity(section, depth, slope) * section.WetArea(depth);

    /// <summary>
    /// A lâmina normal h (m) que leva a vazão Q (m³/s) com a declividade I,
    /// por bisseção. Na trapezoidal as paredes seguem acima de y; na
    /// meia-cana o teto é o raio, e vazão acima do que ela leva cheia volta
    /// NaN (afogada). Declividade zero ou negativa: NaN (a água não corre).
    /// </summary>
    public static double NormalDepth(ChannelSection section, double flow, double slope)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (!(slope > 0)) return double.NaN;
        if (!(flow > 0)) return 0;

        var alto = section.Kind == ChannelKind.HalfPipe ? section.Radius : Math.Max(1.0, section.Height);

        if (section.Kind == ChannelKind.HalfPipe)
        {
            if (Flow(section, alto, slope) < flow) return double.NaN;
        }
        else
        {
            while (Flow(section, alto, slope) < flow)
            {
                alto *= 2;
                if (alto > 1000) return double.NaN;
            }
        }

        var baixo = 0.0;

        for (var i = 0; i < 200 && alto - baixo > 1e-9; i++)
        {
            var meio = (baixo + alto) / 2;
            if (Flow(section, meio, slope) < flow) baixo = meio;
            else alto = meio;
        }

        return (baixo + alto) / 2;
    }

    /// <summary>
    /// Bordo livre f (m) pela Tabela 36 do DNIT (valetas revestidas), pela
    /// vazão em m³/s: até 0,25 → 10 cm; até 0,56 → 13; até 0,84 → 14; até
    /// 1,40 → 15; até 2,80 → 18; acima → 20.
    /// </summary>
    public static double Freeboard(double flowCubicMetersPerSecond) => flowCubicMetersPerSecond switch
    {
        <= 0.25 => 0.10,
        <= 0.56 => 0.13,
        <= 0.84 => 0.14,
        <= 1.40 => 0.15,
        <= 2.80 => 0.18,
        _ => 0.20,
    };

    /// <summary>Arredonda para cima no passo (0,05 m): a altura sugerida nunca fica abaixo da conta.</summary>
    public static double RoundUp(double value, double step)
    {
        if (!(step > 0)) return value;

        // A folga tira o arredondamento de 0,35000000001 para 0,40.
        return Math.Ceiling(value / step - 1e-9) * step;
    }

    /// <summary>
    /// A altura sugerida (m): na trapezoidal, h + f arredondado; na meia-cana,
    /// o menor raio (no passo) que leva a vazão com a lâmina mais o bordo livre
    /// dentro dele. NaN se a declividade não deixa a água correr.
    /// </summary>
    public static double SuggestedHeight(ChannelSection section, double flow, double slope, double step)
    {
        ArgumentNullException.ThrowIfNull(section);

        var f = Freeboard(flow);

        if (section.Kind == ChannelKind.Trapezoidal)
        {
            var h = NormalDepth(section, flow, slope);
            return double.IsNaN(h) ? double.NaN : RoundUp(h + f, step);
        }

        if (!(slope > 0)) return double.NaN;

        var r = RoundUp(Math.Max(section.Radius, step), step);

        for (var i = 0; i < 400; i++, r = RoundUp(r + step, step))
        {
            var tentativa = section with { Radius = r };
            var h = NormalDepth(tentativa, flow, slope);
            if (!double.IsNaN(h) && h + f <= r + 1e-9) return r;
        }

        return double.NaN;
    }
}

/// <summary>O dissipador de energia do pé da canaleta (DNIT IPR 736, desenho 1.19, DES), item 7 do 02-calculos.</summary>
public static class EnergyDissipator
{
    /// <summary>Os modelos do álbum (L1 em cm): DES 60-180 a DES 160-480.</summary>
    public static readonly IReadOnlyList<int> CatalogL1 = [60, 73, 80, 88, 90, 100, 108, 120, 125, 150, 160];

    /// <summary>L1 = B, L2 = 3·L1 e comprimento = 2·L1, em m.</summary>
    public static (double L1, double L2, double Length) Size(double topWidth) => (topWidth, 3 * topWidth, 2 * topWidth);

    /// <summary>O modelo do álbum com L1 que cobre a abertura B (m), ou null se passa do maior.</summary>
    public static string? Model(double topWidth)
    {
        foreach (var l1 in CatalogL1)
        {
            if (l1 / 100.0 + 1e-9 >= topWidth) return $"DES {l1}-{3 * l1}";
        }

        return null;
    }
}

/// <summary>A transposição da canaleta sob a via (DNIT IPR 736, desenho 1.9, TSS), item 8 do 02-calculos.</summary>
public static class ChannelCrossing
{
    /// <summary>Os modelos do álbum, pelo comprimento da laje (cm).</summary>
    public static readonly IReadOnlyList<int> CatalogLength = [120, 130, 140, 150, 170, 200];

    /// <summary>
    /// O modelo TSS pela abertura B (m): a laje precisa passar da abertura pela
    /// folga de cada lado. O memorial: "abertura inferior a 92 cm → TSS 120",
    /// ou seja, folga de 0,28 m no total.
    /// </summary>
    public static string? Model(double topWidth, double margin = 0.28)
    {
        foreach (var comprimento in CatalogLength)
        {
            if (topWidth + margin < comprimento / 100.0 - 1e-9) return $"TSS {comprimento}";
        }

        return null;
    }
}
