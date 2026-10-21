using System.Text.Json.Serialization;

namespace Clivus.Core;

/// <summary>O tipo da seção da canaleta.</summary>
public enum ChannelKind
{
    /// <summary>Trapezoidal (Figura 7 do memorial): base b, talude z = x/y, altura y, espessura e.</summary>
    Trapezoidal,

    /// <summary>Meia-cana: semicírculo de raio r, espessura e.</summary>
    HalfPipe,
}

/// <summary>
/// A seção de uma canaleta (passo 28.1). As medidas são internas (a água);
/// a espessura é da parede de concreto, para o sólido e o volume.
///
/// O revestimento entra junto porque é ele que diz a rugosidade (n de
/// Manning) e a velocidade máxima antes da abrasão: concreto 0,014 e
/// 4,5 m/s no memorial.
/// </summary>
public sealed record ChannelSection
{
    /// <summary>O nome que aparece na lista (ex. "Trap 30/60").</summary>
    public string Name { get; init; } = string.Empty;

    public ChannelKind Kind { get; init; }

    /// <summary>Base b, em m (trapezoidal).</summary>
    public double Base { get; init; }

    /// <summary>Talude z = x / y, horizontal por vertical (trapezoidal).</summary>
    public double SideSlope { get; init; }

    /// <summary>Altura y, em m (trapezoidal). Na meia-cana a altura é o raio.</summary>
    public double Height { get; init; }

    /// <summary>Raio r, em m (meia-cana).</summary>
    public double Radius { get; init; }

    /// <summary>Espessura e da parede, em m.</summary>
    public double Thickness { get; init; }

    /// <summary>O revestimento (ex. "Concreto").</summary>
    public string Lining { get; init; } = string.Empty;

    /// <summary>Coeficiente de rugosidade de Manning do revestimento.</summary>
    public double Manning { get; init; }

    /// <summary>Velocidade máxima do revestimento, em m/s.</summary>
    public double MaxVelocity { get; init; }

    /// <summary>A altura útil (até a borda), em m.</summary>
    [JsonIgnore]
    public double UsefulHeight => Kind == ChannelKind.HalfPipe ? Radius : Height;

    /// <summary>A abertura x de cada lado (trapezoidal), em m.</summary>
    [JsonIgnore]
    public double SideOpening => Kind == ChannelKind.Trapezoidal ? SideSlope * Height : 0;

    /// <summary>A largura na borda B, em m: b + 2x na trapezoidal, 2r na meia-cana.</summary>
    [JsonIgnore]
    public double TopWidth => Kind == ChannelKind.HalfPipe ? 2 * Radius : Base + 2 * SideOpening;

    /// <summary>
    /// A área molhada A (m²) para uma lâmina h. Na trapezoidal as paredes
    /// seguem acima da altura (a conta da lâmina não depende de y, como no
    /// memorial); na meia-cana a lâmina vai até o raio.
    /// </summary>
    public double WetArea(double h)
    {
        if (h <= 0) return 0;

        if (Kind == ChannelKind.Trapezoidal) return (Base + SideSlope * h) * h;

        h = Math.Min(h, Radius);
        var theta = Theta(h);
        return Radius * Radius * (theta - Math.Sin(theta)) / 2;
    }

    /// <summary>O perímetro molhado P (m) para uma lâmina h.</summary>
    public double WetPerimeter(double h)
    {
        if (h <= 0) return 0;

        if (Kind == ChannelKind.Trapezoidal) return Base + 2 * h * Math.Sqrt(1 + SideSlope * SideSlope);

        h = Math.Min(h, Radius);
        return Radius * Theta(h);
    }

    /// <summary>A largura da superfície da água (m) para uma lâmina h.</summary>
    public double WaterWidth(double h)
    {
        if (h <= 0) return 0;

        if (Kind == ChannelKind.Trapezoidal) return Base + 2 * SideSlope * h;

        h = Math.Min(h, Radius);
        return 2 * Math.Sqrt(h * (2 * Radius - h));
    }

    /// <summary>O raio hidráulico Rh = A / P (m) para uma lâmina h.</summary>
    public double HydraulicRadius(double h)
    {
        var p = WetPerimeter(h);
        return p > 0 ? WetArea(h) / p : 0;
    }

    /// <summary>A área de concreto da seção (m²), para o volume: a parede de espessura e em volta da altura útil.</summary>
    [JsonIgnore]
    public double WallArea => Kind == ChannelKind.HalfPipe
        ? Math.PI / 2 * ((Radius + Thickness) * (Radius + Thickness) - Radius * Radius)
        : ExternalArea - (Base + SideOpening) * Height;

    /// <summary>A área da seção externa (m²), para a escavação: o vão interno mais a parede.</summary>
    [JsonIgnore]
    public double ExternalArea
    {
        get
        {
            if (Kind == ChannelKind.HalfPipe) return Math.PI / 2 * (Radius + Thickness) * (Radius + Thickness);

            // O contorno externo é o interno afastado da espessura e: fundo
            // e abaixo, paredes paralelas afastadas e na normal delas.
            var (bExt, alturaExt) = ExternalBaseAndHeight();
            return (bExt + SideSlope * alturaExt) * alturaExt;
        }
    }

    /// <summary>A base e a altura do trapézio externo (afastado da espessura).</summary>
    private (double Base, double Height) ExternalBaseAndHeight()
    {
        var raiz = Math.Sqrt(1 + SideSlope * SideSlope);

        // A parede afastada de e na normal corta a horizontal do fundo
        // externo (e abaixo do interno) a e·(√(1+z²) − z) a mais de cada lado.
        var bExt = Base + 2 * Thickness * (raiz - SideSlope);
        return (bExt, Height + Thickness);
    }

    /// <summary>O contorno interno da seção, em coordenadas (largura, altura), do fundo como zero e o eixo no meio.</summary>
    public IReadOnlyList<(double X, double Y)> InnerOutline(int segments = 16)
    {
        if (Kind == ChannelKind.Trapezoidal)
            return [(-Base / 2 - SideOpening, Height), (-Base / 2, 0), (Base / 2, 0), (Base / 2 + SideOpening, Height)];

        return Arco(Radius, segments);
    }

    /// <summary>O contorno externo da seção, mesma convenção (o fundo externo fica em −e).</summary>
    public IReadOnlyList<(double X, double Y)> OuterOutline(int segments = 16)
    {
        if (Kind == ChannelKind.Trapezoidal)
        {
            var (bExt, alturaExt) = ExternalBaseAndHeight();
            var topo = bExt / 2 + SideSlope * alturaExt;
            return [(-topo, Height), (-bExt / 2, -Thickness), (bExt / 2, -Thickness), (topo, Height)];
        }

        // O semicírculo externo, com o centro na borda: o fundo externo fica em −e.
        var r = Radius + Thickness;
        return Arco(r, segments).Select(p => (p.X, p.Y - Thickness)).ToList();
    }

    /// <summary>O semicírculo de raio r com o centro em (0, r) do fundo interno.</summary>
    private IReadOnlyList<(double X, double Y)> Arco(double r, int segments)
    {
        var pontos = new List<(double, double)>(segments + 1);

        for (var i = 0; i <= segments; i++)
        {
            var a = Math.PI + Math.PI * i / segments;
            pontos.Add((r * Math.Cos(a), Radius + r * Math.Sin(a)));
        }

        return pontos;
    }

    private double Theta(double h) => 2 * Math.Acos(Math.Clamp(1 - h / Radius, -1, 1));

    /// <summary>Por que a seção não serve, ou null se as medidas fazem sentido.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name)) return Tr.T("A seção precisa de um nome.");
        if (!(Thickness >= 0) || !double.IsFinite(Thickness)) return Tr.T("A espessura não pode ser negativa.");
        if (!(Manning > 0) || !double.IsFinite(Manning)) return Tr.T("O n de Manning precisa ser maior que zero.");
        if (!(MaxVelocity > 0) || !double.IsFinite(MaxVelocity)) return Tr.T("A velocidade máxima do revestimento precisa ser maior que zero.");

        if (Kind == ChannelKind.Trapezoidal)
        {
            if (!(Base > 0) || !double.IsFinite(Base)) return Tr.T("A base b precisa ser maior que zero.");
            if (!(SideSlope >= 0) || !double.IsFinite(SideSlope)) return Tr.T("O talude z não pode ser negativo.");
            if (!(Height > 0) || !double.IsFinite(Height)) return Tr.T("A altura y precisa ser maior que zero.");
        }
        else if (!(Radius > 0) || !double.IsFinite(Radius))
        {
            return Tr.T("O raio precisa ser maior que zero.");
        }

        return null;
    }

    /// <summary>As seções que vêm no projeto novo: a trapezoidal do memorial e uma meia-cana.</summary>
    public static IReadOnlyList<ChannelSection> Defaults { get; } =
    [
        new() { Name = "Trap 30/60", Kind = ChannelKind.Trapezoidal, Base = 0.30, SideSlope = 0.6, Height = 0.35, Thickness = 0.10, Lining = "Concreto", Manning = 0.014, MaxVelocity = 4.5 },
        new() { Name = "Meia-cana 30", Kind = ChannelKind.HalfPipe, Radius = 0.15, Thickness = 0.08, Lining = "Concreto", Manning = 0.014, MaxVelocity = 4.5 },
    ];

    /// <summary>Os revestimentos da lista, com o n de Manning (Porto, condição boa) e a velocidade máxima (DNIT, Tabela 31).</summary>
    public static IReadOnlyList<(string Name, double Manning, double MaxVelocity)> Linings { get; } =
    [
        ("Concreto", 0.014, 4.5),
        ("Alvenaria de tijolos", 0.013, 2.5),
        ("Grama comum firmemente implantada", 0.030, 1.8),
        ("Revestimento betuminoso", 0.016, 3.0),
        ("Aglomerados resistentes", 0.020, 2.0),
        ("Terra (sem revestimento)", 0.020, 0.8),
    ];
}
