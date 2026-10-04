using System.Globalization;

namespace Clivus.Core;

/// <summary>As análises que o plugin faz sobre o resultado, cada uma com regra própria.</summary>
public enum AnalysisKind
{
    /// <summary>Altura livre da ponta baixa do módulo ao terreno, na primeira fileira.</summary>
    LowEdge,

    /// <summary>Comprimento total do pilar: enterro mais o que fica de fora.</summary>
    PillarLength,

    /// <summary>O quanto o pilar entra no chão.</summary>
    Embedment,

    /// <summary>Inclinação da mesa no sentido da fileira, por mesa.</summary>
    LongitudinalSlope,

    /// <summary>Mesa que cai parcialmente fora da área.</summary>
    EdgeTable,
}

/// <summary>O que uma análise concluiu sobre um valor.</summary>
public enum AnalysisOutcome
{
    /// <summary>A análise está desligada, ou não há limite para ela. Não mediu.</summary>
    Off,

    /// <summary>Dentro do limite. Não pinta.</summary>
    Inside,

    /// <summary>Abaixo do mínimo. Pinta com a cor "abaixo".</summary>
    Below,

    /// <summary>Acima do máximo (ou, na borda, fora da área). Pinta com a cor "acima".</summary>
    Above,
}

/// <summary>
/// A regra de uma análise de faixa: se está ligada, em que camada pinta, e
/// com que cor para cada lado do estouro.
///
/// Os limites NÃO moram aqui de propósito. Ponta baixa, enterro e declividade
/// já têm faixa em <see cref="SystemConfiguration"/>; repeti-la seria criar um
/// segundo dono do mesmo número, e é assim que eles começam a divergir. O
/// único limite que é só da análise, o de comprimento de pilar, mora em
/// <see cref="AnalysisRules.PaintPillarsLongerThan"/>.
/// </summary>
/// <param name="Enabled">Se a análise roda e pinta.</param>
/// <param name="Layer">A camada onde a pintura desta análise vai. Uma por análise.</param>
/// <param name="BelowColor">
/// A cor de quem ficou abaixo do mínimo. Comprimento de pilar e declividade só
/// têm máximo, então nessas duas análises esta cor nunca é usada — a tela do
/// 4.4 deve escondê-la nelas (<see cref="AnalysisRules.HasMinimum"/>).
/// </param>
/// <param name="AboveColor">A cor de quem ficou acima do máximo.</param>
public sealed record AnalysisRule(
    bool Enabled,
    string Layer,
    RgbColor BelowColor,
    RgbColor AboveColor);

/// <summary>
/// A regra da mesa na borda: não há mínimo nem máximo, só "caiu fora ou não".
/// "A mesa que cai parcialmente fora é mantida e pintada inteira com uma cor
/// própria, para o engenheiro decidir" (plano de requisitos).
/// </summary>
/// <param name="Enabled">Se a análise roda e pinta.</param>
/// <param name="Layer">A camada onde a pintura desta análise vai.</param>
/// <param name="Color">A cor da mesa que caiu fora.</param>
public sealed record EdgeRule(bool Enabled, string Layer, RgbColor Color);

/// <summary>O que uma análise disse de um valor: o resultado e, se pinta, com o quê e onde.</summary>
/// <param name="Kind">A análise.</param>
/// <param name="Outcome">O resultado.</param>
/// <param name="Color">A cor, ou null quando não pinta.</param>
/// <param name="Layer">A camada, ou null quando não pinta.</param>
public sealed record AnalysisVerdict(
    AnalysisKind Kind,
    AnalysisOutcome Outcome,
    RgbColor? Color,
    string? Layer);

/// <summary>
/// As regras de análise do projeto: o que se pinta, de que cor, em que camada.
///
/// "Não se colore a esmo. A coloração segue um conjunto de regras de análise
/// configuradas pelo usuário. Dentro do limite, não pinta nada. Fora, pinta
/// com a cor que ele escolheu para aquele lado do estouro. Cada análise vai
/// em camada própria, para ligar e desligar e enxergar o padrão no terreno."
///
/// <b>Análise não trava nada.</b> A regra sagrada 4 continua mandando: a
/// ponta baixa manda, o pilar estoura se tiver que estourar. Isto só diz de
/// que cor o estouro aparece.
///
/// Os limites vêm da <see cref="SystemConfiguration"/>, passada na hora de
/// avaliar. O único que é só da análise é <see cref="PaintPillarsLongerThan"/>.
/// </summary>
/// <param name="LowEdge">Regra da ponta baixa; faixa da configuração.</param>
/// <param name="PillarLength">Regra do comprimento de pilar; limite em <see cref="PaintPillarsLongerThan"/>.</param>
/// <param name="Embedment">Regra do enterro; faixa da configuração.</param>
/// <param name="LongitudinalSlope">Regra da declividade; limite da configuração, se houver.</param>
/// <param name="EdgeRule">Regra da mesa na borda.</param>
/// <param name="PaintPillarsLongerThan">
/// Comprimento de pilar acima do qual a análise pinta, em metro, ou null para
/// não haver. O exemplo do plano: "pintar tudo acima de 2,50 m, porque compra
/// pilar de 2,20, 2,50 e 3,00 m".
///
/// O nome diz "pintar", e não "máximo", de propósito: o 4.1 apagou um
/// <c>MaxPillarLength</c> da configuração porque o Renan disse em 23/09/2026
/// que o plugin só calcula o pilar ideal e ele filtra. Isto não contradiz
/// aquilo — não há teto, só cor — e o nome diferente é para ninguém confundir
/// os dois na tela do 4.4. Nasce null por decisão minha (seção do 4.3 em
/// PROGRESSO.md): o número de onde a cor começa é dele.
/// </param>
public sealed record AnalysisRules(
    AnalysisRule LowEdge,
    AnalysisRule PillarLength,
    AnalysisRule Embedment,
    AnalysisRule LongitudinalSlope,
    EdgeRule EdgeRule,
    double? PaintPillarsLongerThan)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// A folga numérica na comparação com o limite: um valor a um nanômetro
    /// do limite, por arredondamento de seno, está no limite — não fora dele.
    ///
    /// É o número da tolerância geométrica da arquitetura (1e-6 m), e vale
    /// igual para a declividade em radianos: 1e-6 rad é 0,00006°, muito
    /// abaixo de qualquer diferença que um projetista consiga enxergar.
    /// </summary>
    private const double Tolerancia = 1e-6;

    /// <summary>
    /// Maior comprimento de pilar aceito como limite, em metro. Rede para
    /// erro de escala, como no resto do Core (ver a pendência do 4.1 sobre
    /// os vários donos deste número).
    /// </summary>
    private const double MaiorMedida = 50.0;

    private const string Prefixo = PluginInfo.PrefixoDeDados + "_ANALISE_";

    /// <summary>As análises de faixa: todas menos a borda, que não tem número.</summary>
    public static readonly IReadOnlyList<AnalysisKind> RangedKinds =
    [
        AnalysisKind.LowEdge,
        AnalysisKind.PillarLength,
        AnalysisKind.Embedment,
        AnalysisKind.LongitudinalSlope,
    ];

    /// <summary>
    /// As regras de partida: tudo ligado, vermelho abaixo e azul acima
    /// (decisão do Renan em 25/09/2026), magenta na borda, uma camada por
    /// análise, e sem limite de comprimento de pilar.
    /// </summary>
    public static readonly AnalysisRules Default = new(
        LowEdge: Faixa(Prefixo + "PONTA_BAIXA"),
        PillarLength: Faixa(Prefixo + "PILAR"),
        Embedment: Faixa(Prefixo + "ENTERRO"),
        LongitudinalSlope: Faixa(Prefixo + "DECLIVIDADE"),
        EdgeRule: new EdgeRule(Enabled: true, Layer: Prefixo + "BORDA", Color: RgbColor.Magenta),
        PaintPillarsLongerThan: null);

    /// <summary>A regra de uma análise de faixa.</summary>
    /// <exception cref="ArgumentException">Se for a borda, que não é de faixa.</exception>
    /// <exception cref="InvalidOperationException">Se a regra estiver ausente (null).</exception>
    public AnalysisRule Rule(AnalysisKind kind) =>
        RuleOrNull(kind)
        ?? throw new InvalidOperationException($"A regra da análise de {Nome(kind)} está ausente.");

    /// <summary>
    /// Se a análise tem mínimo. Comprimento de pilar e declividade só têm
    /// máximo, e a cor "abaixo" delas nunca é usada.
    /// </summary>
    public static bool HasMinimum(AnalysisKind kind) =>
        kind is AnalysisKind.LowEdge or AnalysisKind.Embedment;

    private AnalysisRule? RuleOrNull(AnalysisKind kind) => kind switch
    {
        AnalysisKind.LowEdge => LowEdge,
        AnalysisKind.PillarLength => PillarLength,
        AnalysisKind.Embedment => Embedment,
        AnalysisKind.LongitudinalSlope => LongitudinalSlope,
        _ => throw new ArgumentException(
            $"A análise {kind} não é de faixa; use {nameof(EdgeRule)}.", nameof(kind)),
    };

    /// <summary>Uma cópia com a regra de uma análise de faixa trocada.</summary>
    /// <exception cref="ArgumentException">Se for a borda, que não é de faixa.</exception>
    public AnalysisRules With(AnalysisKind kind, AnalysisRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return kind switch
        {
            AnalysisKind.LowEdge => this with { LowEdge = rule },
            AnalysisKind.PillarLength => this with { PillarLength = rule },
            AnalysisKind.Embedment => this with { Embedment = rule },
            AnalysisKind.LongitudinalSlope => this with { LongitudinalSlope = rule },
            _ => throw new ArgumentException(
                $"A análise {kind} não é de faixa; use {nameof(EdgeRule)}.", nameof(kind)),
        };
    }

    /// <summary>
    /// As camadas de todas as análises, na ordem de <see cref="RangedKinds"/>
    /// e a borda por último. Regra ausente entra como texto vazio, para a
    /// lista ter sempre cinco posições.
    /// </summary>
    public IEnumerable<string> Layers =>
        RangedKinds.Select(kind => RuleOrNull(kind)?.Layer ?? string.Empty)
            .Append(EdgeRule?.Layer ?? string.Empty);

    /// <summary>Se as regras fazem sentido entre si.</summary>
    public bool IsValid => WhyInvalid is null;

    /// <summary>O motivo de as regras não fecharem, em português, ou null.</summary>
    public string? WhyInvalid
    {
        get
        {
            // Regra ausente vem de arquivo com campo faltando (o 4.4 vai ler
            // isto de disco). Sem esta guarda, o motivo seria uma exceção de
            // referência nula, que não nomeia campo nenhum.
            foreach (var kind in RangedKinds)
            {
                if (RuleOrNull(kind) is null)
                    return $"a regra da análise de {Nome(kind)} está ausente";
            }

            if (EdgeRule is null)
                return $"a regra da análise de {Nome(AnalysisKind.EdgeTable)} está ausente";

            if (PaintPillarsLongerThan is { } teto
                && (!double.IsFinite(teto) || teto <= 0 || teto > MaiorMedida))
            {
                return "o limite de comprimento de pilar não é uma medida válida";
            }

            foreach (var kind in RangedKinds)
            {
                if (LayerName.WhyInvalid(Rule(kind).Layer) is { } motivo)
                    return $"a camada da análise de {Nome(kind)} {motivo}";
            }

            if (LayerName.WhyInvalid(EdgeRule.Layer) is { } borda)
                return $"a camada da análise de {Nome(AnalysisKind.EdgeTable)} {borda}";

            // O AutoCAD não distingue caixa em nome de camada: "CLIVUS_X" e
            // "clivus_x" são a mesma, e duas análises nela não se separam.
            var repetida = Layers
                .GroupBy(camada => camada, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(grupo => grupo.Count() > 1);

            if (repetida is not null)
                return $"a camada \"{repetida.Key}\" está em mais de uma análise";

            return null;
        }
    }

    /// <summary>
    /// O que a análise diz deste valor, com os limites da configuração.
    /// </summary>
    /// <param name="kind">Uma análise de faixa; a borda usa <see cref="EvaluateEdge"/>.</param>
    /// <param name="value">
    /// O valor medido, na unidade da análise: metro para ponta baixa, pilar
    /// e enterro; radiano para declividade. A declividade vale pelo módulo,
    /// porque o sentido da fileira é arbitrário.
    /// </param>
    /// <param name="configuration">De onde vêm os limites.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se o valor não for um número. "Não sei medir" não vira "está bom".
    /// </exception>
    /// <exception cref="InvalidOperationException">Se as regras ou a configuração não fecham.</exception>
    /// <exception cref="ArgumentException">Se a análise for a borda.</exception>
    public AnalysisVerdict Evaluate(AnalysisKind kind, double value, SystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var regra = Rule(kind);

        Conferir();

        if (configuration.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");

        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"O valor da análise de {Nome(kind)} não é um número.");
        }

        var (minimo, maximo) = Limites(kind, configuration);

        if (!regra.Enabled || (minimo is null && maximo is null))
            return new AnalysisVerdict(kind, AnalysisOutcome.Off, null, null);

        var medido = kind == AnalysisKind.LongitudinalSlope ? Math.Abs(value) : value;

        if (minimo is { } piso && medido < piso - Tolerancia)
            return new AnalysisVerdict(kind, AnalysisOutcome.Below, regra.BelowColor, regra.Layer);

        if (maximo is { } teto && medido > teto + Tolerancia)
            return new AnalysisVerdict(kind, AnalysisOutcome.Above, regra.AboveColor, regra.Layer);

        return new AnalysisVerdict(kind, AnalysisOutcome.Inside, null, null);
    }

    /// <summary>O que a análise de borda diz de uma mesa.</summary>
    /// <param name="partlyOutside">Se a mesa cai, mesmo que só em parte, fora da área.</param>
    /// <exception cref="InvalidOperationException">Se as regras não fecham.</exception>
    public AnalysisVerdict EvaluateEdge(bool partlyOutside)
    {
        Conferir();

        if (!EdgeRule.Enabled)
            return new AnalysisVerdict(AnalysisKind.EdgeTable, AnalysisOutcome.Off, null, null);

        return partlyOutside
            ? new AnalysisVerdict(AnalysisKind.EdgeTable, AnalysisOutcome.Above, EdgeRule.Color, EdgeRule.Layer)
            : new AnalysisVerdict(AnalysisKind.EdgeTable, AnalysisOutcome.Inside, null, null);
    }

    /// <summary>A linha que descreve as regras para o usuário.</summary>
    public string Describe()
    {
        if (WhyInvalid is { } motivo) return $"Regras de análise inválidas: {motivo}.";

        var ligadas = RangedKinds.Count(kind => Rule(kind).Enabled) + (EdgeRule.Enabled ? 1 : 0);

        var pilar = PaintPillarsLongerThan is { } teto
            ? $", pilar pintado acima de {teto.ToString("0.###", Brasil)} m"
            : ", sem limite de pilar";

        return $"{ligadas} análises ligadas de {RangedKinds.Count + 1}{pilar}";
    }

    /// <summary>
    /// Os limites de cada análise, tirados da configuração. Null de um lado é
    /// "não há limite desse lado"; dos dois, "a análise não tem o que medir".
    /// </summary>
    private (double? Minimo, double? Maximo) Limites(AnalysisKind kind, SystemConfiguration config) =>
        kind switch
        {
            AnalysisKind.LowEdge => (config.MinLowEdge, config.MaxLowEdge),
            AnalysisKind.Embedment => (config.MinEmbedment, config.MaxEmbedment),
            AnalysisKind.LongitudinalSlope => (null, config.MaxLongitudinalSlope),
            AnalysisKind.PillarLength => (null, PaintPillarsLongerThan),
            _ => throw new ArgumentException($"A análise {kind} não é de faixa.", nameof(kind)),
        };

    private void Conferir()
    {
        if (WhyInvalid is { } motivo)
            throw new InvalidOperationException($"As regras de análise não fecham: {motivo}.");
    }

    private static AnalysisRule Faixa(string camada) =>
        new(Enabled: true, Layer: camada, BelowColor: RgbColor.Red, AboveColor: RgbColor.Blue);

    private static string Nome(AnalysisKind kind) => kind switch
    {
        AnalysisKind.LowEdge => "ponta baixa",
        AnalysisKind.PillarLength => "comprimento de pilar",
        AnalysisKind.Embedment => "enterro",
        AnalysisKind.LongitudinalSlope => "declividade longitudinal",
        AnalysisKind.EdgeTable => "mesa na borda",
        _ => kind.ToString(),
    };
}
