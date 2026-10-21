using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clivus.Core;

/// <summary>Como as flechas são geradas (26.1).</summary>
public sealed record ArrowSettings
{
    /// <summary>Espaçamento da grade, em m.</summary>
    public double Spacing { get; init; } = 5;

    /// <summary>Se escreve o valor da declividade ao lado de cada flecha.</summary>
    public bool ShowValue { get; init; }

    /// <summary>Os quatro limites das faixas, em %.</summary>
    public IReadOnlyList<double> Limits { get; init; } = DrainageArrows.DefaultLimits;
}

/// <summary>Como o perfil das linhas e das canaletas é amostrado e desenhado (27).</summary>
public sealed record ProfileSettings
{
    /// <summary>Estaca a cada quantos metros.</summary>
    public double StationSpacing { get; init; } = 5;

    /// <summary>Trecho de declividade (o "% | grau" escrito sobre o perfil), em metros.</summary>
    public double SegmentLength { get; init; } = 20;

    /// <summary>Exagero vertical do gráfico; zero = automático (a altura perto de um terço do comprimento).</summary>
    public double VerticalExaggeration { get; init; }
}

/// <summary>Os parâmetros hidrológicos e hidráulicos do projeto (31.2). Padrão: o memorial de Valparaiso.</summary>
public sealed record HydrologyParameters
{
    /// <summary>De onde vêm K, a, b e c (ex. "Plúvio 2.1, Valparaiso-SP").</summary>
    public string Source { get; init; } = "Plúvio 2.1, Valparaiso-SP";

    public double K { get; init; } = 1544.886;
    public double A { get; init; } = 0.142;
    public double B { get; init; } = 24.953;
    public double C { get; init; } = 0.756;

    /// <summary>Período de retorno, em anos.</summary>
    public double ReturnPeriod { get; init; } = 25;

    /// <summary>Tempo de concentração mínimo, em min.</summary>
    public double MinimumTc { get; init; } = 5;

    /// <summary>Rugosidade de Kerby (0,5 no memorial).</summary>
    public double KerbyN { get; init; } = 0.5;

    /// <summary>Coeficiente de escoamento superficial C do método racional.</summary>
    public double RunoffC { get; init; } = 0.15;

    /// <summary>A linha da Tabela 39 do DNIT de onde saiu o C.</summary>
    public string RunoffSurface { get; init; } = "Prados e campinas";

    /// <summary>A cobertura do terreno (Tabela 31 do DNIT) e a velocidade máxima dela, em m/s.</summary>
    public string Cover { get; init; } = "Tufos de grama com solo exposto";

    public double CoverMaxVelocity { get; init; } = 1.20;

    /// <summary>Velocidade mínima de autolimpeza na canaleta, em m/s (DNIT).</summary>
    public double MinimumVelocity { get; init; } = 1.0;

    /// <summary>Declividade mínima da canaleta, em m/m (NBR 10844, 5.7.1).</summary>
    public double MinimumSlope { get; init; } = 0.005;

    /// <summary>Passo do arredondamento da altura sugerida, em m (decisão 1 do PROGRESSO).</summary>
    public double HeightStep { get; init; } = 0.05;

    /// <summary>Folga total da laje da transposição sobre a abertura, em m (memorial: B &lt; 0,92 → TSS 120).</summary>
    public double CrossingMargin { get; init; } = 0.28;

    /// <summary>As camadas das vias, separadas por ";": onde a canaleta cruza, entra transposição.</summary>
    public string RoadLayers { get; init; } = string.Empty;

    /// <summary>A intensidade de projeto (mm/h) com o tc dado.</summary>
    public double Intensity(double tcMinutes) => Rainfall.Intensity(K, A, B, C, ReturnPeriod, tcMinutes);

    /// <summary>Por que os parâmetros não servem, ou null.</summary>
    public string? Problem()
    {
        if (!(K > 0) || !double.IsFinite(A) || !double.IsFinite(B) || !(C > 0)) return Tr.T("Os parâmetros da chuva (K, a, b, c) não estão completos.");
        if (!(ReturnPeriod > 0)) return Tr.T("O período de retorno precisa ser maior que zero.");
        if (!(MinimumTc > 0)) return Tr.T("O tempo de concentração mínimo precisa ser maior que zero.");
        if (!(RunoffC > 0) || RunoffC > 1) return Tr.T("O coeficiente de escoamento C precisa estar entre 0 e 1.");
        if (!(CoverMaxVelocity > 0)) return Tr.T("A velocidade máxima da cobertura precisa ser maior que zero.");
        if (!(MinimumSlope > 0)) return Tr.T("A declividade mínima precisa ser maior que zero.");
        if (!(HeightStep > 0)) return Tr.T("O passo do arredondamento precisa ser maior que zero.");
        return null;
    }

    /// <summary>Velocidade máxima por cobertura, Tabela 31 do DNIT (a de cima da faixa).</summary>
    public static IReadOnlyList<(string Name, double Min, double Max)> Covers { get; } =
    [
        ("Grama comum firmemente implantada", 1.50, 1.80),
        ("Tufos de grama com solo exposto", 0.60, 1.20),
        ("Argila", 0.80, 1.30),
        ("Argila coloidal", 1.30, 1.80),
        ("Lodo", 0.35, 0.85),
        ("Areia fina", 0.30, 0.40),
        ("Areia média", 0.35, 0.45),
        ("Cascalho fino", 0.50, 0.80),
        ("Silte", 0.70, 1.20),
        ("Alvenaria de tijolos", 2.50, 2.50),
        ("Concreto de cimento portland", 4.50, 4.50),
        ("Aglomerados resistentes", 2.00, 2.00),
        ("Revestimento betuminoso", 3.00, 4.00),
    ];

    /// <summary>Coeficiente de escoamento por superfície, Tabela 39 do DNIT (faixa).</summary>
    public static IReadOnlyList<(string Name, double Min, double Max)> RunoffSurfaces { get; } =
    [
        ("Revestimento de concreto de cimento portland", 0.70, 0.90),
        ("Revestimento betuminoso", 0.80, 0.95),
        ("Revestimento primário", 0.40, 0.60),
        ("Solos sem revestimento com baixa permeabilidade", 0.40, 0.65),
        ("Solos sem revestimento com permeabilidade moderada", 0.10, 0.30),
        ("Taludes gramados", 0.50, 0.70),
        ("Prados e campinas", 0.10, 0.40),
        ("Áreas florestais", 0.10, 0.25),
        ("Terrenos cultivados em zonas altas", 0.15, 0.40),
        ("Terrenos cultivados em vales", 0.10, 0.30),
    ];
}

/// <summary>Como as bacias são calculadas (30.1).</summary>
public sealed record BasinSettings
{
    /// <summary>Lado da célula da grade, em m.</summary>
    public double CellSize { get; init; } = 2;

    /// <summary>A região analisada: "Areas" (as áreas de implantação), "Tudo" (o terreno) ou "Polilinha".</summary>
    public string Region { get; init; } = "Areas";

    /// <summary>O handle da polilinha fechada, quando a região é "Polilinha".</summary>
    public string RegionHandle { get; init; } = string.Empty;
}

/// <summary>
/// Tudo o que a drenagem guarda no desenho: flechas, perfil, parâmetros, a
/// biblioteca de seções e as bacias. Um JSON só, no dicionário CLIVUS; o que
/// faltar no JSON (desenho de versão anterior) sai com o padrão.
/// </summary>
public sealed record DrainageSettings
{
    public ArrowSettings Arrows { get; init; } = new();
    public ProfileSettings Profile { get; init; } = new();
    public HydrologyParameters Hydrology { get; init; } = new();
    public IReadOnlyList<ChannelSection> Sections { get; init; } = ChannelSection.Defaults;
    public BasinSettings Basins { get; init; } = new();

    /// <summary>A seção escolhida por último na janela da canaleta (o padrão do Converter).</summary>
    public string CurrentSection { get; init; } = ChannelSection.Defaults[0].Name;

    /// <summary>O assentamento escolhido por último na janela da canaleta.</summary>
    public ChannelBottomMode CurrentMode { get; init; } = ChannelBottomMode.FollowTerrain;

    /// <summary>A biblioteca com a seção acrescentada, ou trocada se já há uma com o mesmo nome.</summary>
    public DrainageSettings WithSection(ChannelSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var outras = Sections.Where(s => !string.Equals(s.Name, section.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        return this with { Sections = [.. outras, section with { Name = section.Name.Trim() }] };
    }

    /// <summary>A chave no dicionário do desenho.</summary>
    public const string StorageKey = "DRENAGEM";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Opcoes);

    /// <summary>Lê o JSON gravado; vazio ou ilegível volta o padrão, com o motivo.</summary>
    public static DrainageSettings Parse(string? json, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(json)) return new DrainageSettings();

        try
        {
            var lido = JsonSerializer.Deserialize<DrainageSettings>(json, Opcoes) ?? new DrainageSettings();

            // Lista nula ou torta no JSON vira a padrão, com o motivo: seção
            // nenhuma ou faixa faltando travava a janela.
            var flechas = lido.Arrows ?? new ArrowSettings();
            if (flechas.Limits is not { Count: 4 } || flechas.Limits.Any(l => !double.IsFinite(l)))
            {
                problem = "limites das flechas ilegíveis";
                flechas = flechas with { Limits = DrainageArrows.DefaultLimits };
            }

            var secoes = (lido.Sections ?? []).Where(s => s is not null).ToList();
            if (secoes.Count == 0)
            {
                if (lido.Sections is { Count: > 0 }) problem = "seções ilegíveis";
                secoes = [.. ChannelSection.Defaults];
            }

            return lido with
            {
                Arrows = flechas,
                Profile = lido.Profile ?? new ProfileSettings(),
                Hydrology = lido.Hydrology ?? new HydrologyParameters(),
                Sections = secoes,
                Basins = lido.Basins ?? new BasinSettings(),
            };
        }
        catch (JsonException erro)
        {
            problem = erro.Message;
            return new DrainageSettings();
        }
    }

    /// <summary>A seção da biblioteca pelo nome, ignorando maiúsculas.</summary>
    public ChannelSection? Section(string name) =>
        Sections.FirstOrDefault(s => string.Equals(s.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Corta o texto em pedaços que cabem num campo do registro do desenho.</summary>
    public static IReadOnlyList<string> Chunks(string text, int size = 240)
    {
        var pedacos = new List<string>();
        for (var i = 0; i < text.Length; i += size) pedacos.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        return pedacos;
    }
}
