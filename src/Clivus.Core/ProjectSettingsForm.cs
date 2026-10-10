namespace Clivus.Core;

/// <summary>
/// O formulário da tela de configuração, nas unidades da tela: graus e
/// centímetros onde couber, texto como o projetista digita.
///
/// Mora no Core, e não na janela, porque é aqui que vive a única conversão
/// de unidade entre o que se vê e o que o motor usa — e ela precisa de
/// teste. A revisão do 4.4 apontou: com a conversão dentro da janela, trocar
/// <c>/100</c> por <c>/10</c> passava em todos os testes e só o Renan pegaria,
/// se olhasse o resumo em metros enquanto digitava centímetros.
///
/// A janela só liga cada caixa de texto a um campo daqui. Ela não sabe o que
/// é centímetro.
///
/// <b>Unidades da tela</b>: azimute e declividade em graus; ponta baixa,
/// enterro, degrau e espaçamento em centímetros; pitch e comprimento de
/// pilar em metro, porque é assim que se fala deles ("pitch de 6 m", "pilar
/// de 2,5 m").
/// </summary>
/// <param name="AzimuthDegrees">Para onde a mesa olha, em graus, 0 = norte.</param>
/// <param name="Pitch">Pitch entre mesas, em metro.</param>
/// <param name="MinStepCm">Degrau mínimo entre mesas vizinhas, em cm.</param>
/// <param name="MaxStepCm">Degrau máximo entre mesas vizinhas, em cm.</param>
/// <param name="TableGapCm">Espaçamento entre mesas vizinhas da fileira, em cm.</param>
/// <param name="BreakGapCm">Espaçamento que quebra a fileira, em cm.</param>
/// <param name="MinLowEdgeCm">Altura livre mínima da ponta baixa, em cm.</param>
/// <param name="MaxLowEdgeCm">Altura livre máxima da ponta baixa, em cm.</param>
/// <param name="BumpModules">Módulos por mesa que podem estourar a faixa.</param>
/// <param name="MinEmbedmentCm">Enterro mínimo do pilar, em cm.</param>
/// <param name="MaxEmbedmentCm">Enterro máximo do pilar, em cm.</param>
/// <param name="LimitSlope">Se há limite de declividade longitudinal.</param>
/// <param name="MaxSlopeDegrees">O limite, em graus. Só lido se <paramref name="LimitSlope"/>.</param>
/// <param name="PaintPillars">Se a análise pinta pilar acima de um comprimento.</param>
/// <param name="PillarLongerThan">O comprimento, em metro. Só lido se <paramref name="PaintPillars"/>.</param>
/// <param name="Rules">A regra de cada análise de faixa. As cores ficam como vieram, inclusive a "abaixo" de quem não tem mínimo.</param>
/// <param name="Edge">A regra da mesa na borda.</param>
public sealed record ProjectSettingsForm(
    string AzimuthDegrees,
    string Pitch,
    string MinStepCm,
    string MaxStepCm,
    string TableGapCm,
    string BreakGapCm,
    string MinLowEdgeCm,
    string MaxLowEdgeCm,
    string BumpModules,
    string MinEmbedmentCm,
    string MaxEmbedmentCm,
    bool LimitSlope,
    string MaxSlopeDegrees,
    bool PaintPillars,
    string PillarLongerThan,
    IReadOnlyDictionary<AnalysisKind, AnalysisRule> Rules,
    EdgeRule Edge,
    string MinTemperature = "0",
    string MaxTemperature = "40")
{
    private const double Grau = Math.PI / 180;

    /// <summary>
    /// Os campos numéricos e o nome de cada um, na ordem da tela, para a
    /// mensagem de erro dizer qual está errado.
    /// </summary>
    public static readonly IReadOnlyList<(string Field, string Label)> Labels =
    [
        (nameof(AzimuthDegrees), Tr.N("Azimute")),
        (nameof(Pitch), Tr.N("Pitch entre mesas")),
        (nameof(MinStepCm), Tr.N("Degrau mínimo")),
        (nameof(MaxStepCm), Tr.N("Degrau máximo")),
        (nameof(TableGapCm), Tr.N("Espaçamento entre mesas")),
        (nameof(BreakGapCm), Tr.N("Espaçamento que quebra a fileira")),
        (nameof(MinLowEdgeCm), Tr.N("Altura livre mínima")),
        (nameof(MaxLowEdgeCm), Tr.N("Altura livre máxima")),
        (nameof(BumpModules), Tr.N("Módulos que podem estourar")),
        (nameof(MinEmbedmentCm), Tr.N("Enterro mínimo")),
        (nameof(MaxEmbedmentCm), Tr.N("Enterro máximo")),
        (nameof(MaxSlopeDegrees), Tr.N("Declividade máxima")),
        (nameof(PillarLongerThan), Tr.N("Pintar pilar mais comprido que")),
        (nameof(MinTemperature), Tr.N("Temperatura mínima")),
        (nameof(MaxTemperature), Tr.N("Temperatura máxima")),
    ];

    /// <summary>O formulário que mostra estas configurações.</summary>
    /// <remarks>
    /// Os números vão com até 12 casas, e não 4: abrir a tela e salvar sem
    /// tocar em nada tem que regravar o que estava, e não uma versão
    /// arredondada. Doze casas de grau ou de centímetro é mais fino do que
    /// qualquer diferença que sobreviva ao <c>double</c> depois da conversão.
    /// </remarks>
    public static ProjectSettingsForm From(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var c = settings.Configuration;
        var a = settings.Analyses;

        var regras = new Dictionary<AnalysisKind, AnalysisRule>();
        foreach (var kind in AnalysisRules.RangedKinds) regras[kind] = a.Rule(kind);

        return new ProjectSettingsForm(
            AzimuthDegrees: Numero(c.FacingAzimuthDegrees),
            Pitch: Numero(c.Pitch),
            MinStepCm: Centimetros(c.MinStep),
            MaxStepCm: Centimetros(c.MaxStep),
            TableGapCm: Centimetros(c.TableGap),
            BreakGapCm: Centimetros(c.MaxGapBeforeBreak),
            MinLowEdgeCm: Centimetros(c.MinLowEdge),
            MaxLowEdgeCm: Centimetros(c.MaxLowEdge),
            BumpModules: c.BumpToleranceModules.ToString(Tr.Culture),
            MinEmbedmentCm: Centimetros(c.MinEmbedment),
            MaxEmbedmentCm: Centimetros(c.MaxEmbedment),
            LimitSlope: c.MaxLongitudinalSlopeDegrees is not null,
            MaxSlopeDegrees: c.MaxLongitudinalSlopeDegrees is { } graus ? Numero(graus) : "10",
            PaintPillars: a.PaintPillarsLongerThan is not null,
            PillarLongerThan: a.PaintPillarsLongerThan is { } pilar ? Numero(pilar) : Numero(2.5),
            Rules: regras,
            Edge: a.EdgeRule,
            MinTemperature: Numero(settings.MinTemperature),
            MaxTemperature: Numero(settings.MaxTemperature));
    }

    /// <summary>
    /// As configurações que os campos descrevem, ou null com o motivo. O
    /// motivo nomeia o campo, sempre: são vinte e tantas caixas.
    /// </summary>
    public ProjectSettings? TryParse(out string motivo)
    {
        foreach (var (campo, nome) in Labels)
        {
            var texto = Texto(campo);

            if (campo == nameof(MaxSlopeDegrees) && !LimitSlope) continue;
            if (campo == nameof(PillarLongerThan) && !PaintPillars) continue;

            var certo = campo == nameof(BumpModules)
                ? NumberInput.TryParseCount(texto, out _)
                : NumberInput.TryParseMeasure(texto, out _);

            if (certo) continue;

            motivo = string.IsNullOrWhiteSpace(texto)
                ? Tr.F("o campo \"{0}\" está em branco.", Tr.T(nome))
                : campo == nameof(BumpModules)
                    ? Tr.F("o campo \"{0}\" precisa ser um inteiro.", Tr.T(nome))
                    : Tr.F("não consigo ler o número do campo \"{0}\".", Tr.T(nome));

            return null;
        }

        NumberInput.TryParseMeasure(AzimuthDegrees, out var azimuteGraus);
        NumberInput.TryParseMeasure(Pitch, out var pitch);
        NumberInput.TryParseMeasure(MinStepCm, out var degrauMin);
        NumberInput.TryParseMeasure(MaxStepCm, out var degrauMax);
        NumberInput.TryParseMeasure(TableGapCm, out var espacamentoMesas);
        NumberInput.TryParseMeasure(BreakGapCm, out var espacamento);
        NumberInput.TryParseMeasure(MinLowEdgeCm, out var pontaMin);
        NumberInput.TryParseMeasure(MaxLowEdgeCm, out var pontaMax);
        NumberInput.TryParseCount(BumpModules, out var lombo);
        NumberInput.TryParseMeasure(MinEmbedmentCm, out var enterroMin);
        NumberInput.TryParseMeasure(MaxEmbedmentCm, out var enterroMax);

        double? declividade = null;

        if (LimitSlope)
        {
            NumberInput.TryParseMeasure(MaxSlopeDegrees, out var graus);
            declividade = graus * Grau;
        }

        double? pilar = null;

        if (PaintPillars)
        {
            NumberInput.TryParseMeasure(PillarLongerThan, out var acimaDe);
            pilar = acimaDe;
        }

        var configuracao = new SystemConfiguration(
            FacingAzimuthRadians: azimuteGraus * Grau,
            Pitch: pitch,
            MinLowEdge: pontaMin / 100,
            MaxLowEdge: pontaMax / 100,
            MinEmbedment: enterroMin / 100,
            MaxEmbedment: enterroMax / 100,
            MinStep: degrauMin / 100,
            MaxStep: degrauMax / 100,
            BumpToleranceModules: lombo,
            MaxLongitudinalSlope: declividade,
            TableGap: espacamentoMesas / 100,
            MaxGapBeforeBreak: espacamento / 100);

        foreach (var kind in AnalysisRules.RangedKinds)
        {
            if (Rules is null || !Rules.ContainsKey(kind) || Rules[kind] is null)
            {
                motivo = Tr.F("a regra da análise de {0} está ausente no formulário.", kind);
                return null;
            }
        }

        if (Edge is null)
        {
            motivo = Tr.T("a regra da mesa na borda está ausente no formulário.");
            return null;
        }

        var analises = new AnalysisRules(
            LowEdge: Rules![AnalysisKind.LowEdge],
            PillarLength: Rules[AnalysisKind.PillarLength],
            Embedment: Rules[AnalysisKind.Embedment],
            LongitudinalSlope: Rules[AnalysisKind.LongitudinalSlope],
            EdgeRule: Edge,
            PaintPillarsLongerThan: pilar);

        NumberInput.TryParseMeasure(MinTemperature, out var tMin);
        NumberInput.TryParseMeasure(MaxTemperature, out var tMax);

        var settings = new ProjectSettings(configuracao, analises, tMin, tMax);

        if (settings.WhyInvalid is { } porQue)
        {
            motivo = porQue + ".";
            return null;
        }

        motivo = string.Empty;
        return settings;
    }

    private string Texto(string campo) => campo switch
    {
        nameof(AzimuthDegrees) => AzimuthDegrees,
        nameof(Pitch) => Pitch,
        nameof(MinStepCm) => MinStepCm,
        nameof(MaxStepCm) => MaxStepCm,
        nameof(TableGapCm) => TableGapCm,
        nameof(BreakGapCm) => BreakGapCm,
        nameof(MinLowEdgeCm) => MinLowEdgeCm,
        nameof(MaxLowEdgeCm) => MaxLowEdgeCm,
        nameof(BumpModules) => BumpModules,
        nameof(MinEmbedmentCm) => MinEmbedmentCm,
        nameof(MaxEmbedmentCm) => MaxEmbedmentCm,
        nameof(MaxSlopeDegrees) => MaxSlopeDegrees,
        nameof(PillarLongerThan) => PillarLongerThan,
        nameof(MinTemperature) => MinTemperature,
        nameof(MaxTemperature) => MaxTemperature,
        _ => throw new ArgumentException($"Campo desconhecido: {campo}.", nameof(campo)),
    } ?? string.Empty;

    private static string Numero(double valor) => valor.ToString("0.############", Tr.Culture);

    private static string Centimetros(double metros) => Numero(metros * 100);
}
