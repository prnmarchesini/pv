using System.Globalization;

namespace UFV.Core;

/// <summary>Um módulo da fileira de baixo com o seu valor e o seu veredito.</summary>
/// <param name="Column">A coluna do módulo.</param>
/// <param name="Station">A estação do meio da ponta baixa.</param>
/// <param name="Clearance">A altura livre da ponta baixa ao terreno, ou null sem terreno.</param>
/// <param name="Verdict">O que a análise da ponta baixa disse. Off quando não há terreno para medir.</param>
public sealed record ModuleReport(int Column, double Station, double? Clearance, AnalysisVerdict Verdict);

/// <summary>Um pilar com os seus valores e os seus vereditos.</summary>
/// <param name="Pillar">O pilar calculado (5.5), com terreno, topo, altura livre, enterro e comprimento.</param>
/// <param name="LengthVerdict">O que a análise de comprimento disse. Off sem comprimento.</param>
/// <param name="EmbedmentVerdict">
/// O que a análise de enterro disse. Off sem comprimento — e, com o
/// comprimento ideal do 5.5, sempre Inside quando há comprimento: o enterro
/// é o mínimo da configuração por construção, e a configuração válida
/// garante mínimo ≤ máximo. Esta análise só pintaria com comprimento de
/// pilar imposto de fora, o que não existe hoje. Está aqui para o dia em que
/// existir, e há teste guardando que hoje é Inside ou Off.
/// </param>
public sealed record PillarReport(PillarResult Pillar, AnalysisVerdict LengthVerdict, AnalysisVerdict EmbedmentVerdict)
{
    /// <summary>
    /// O veredito que pinta o pilar. Um pilar é uma entidade com uma cor e
    /// uma camada, e duas análises falam dele: vence a de comprimento, que
    /// é a que pode pintar; a de enterro só entra se a de comprimento não
    /// pintou. Sem pintura, o de comprimento (Inside ou Off).
    /// </summary>
    public AnalysisVerdict PaintVerdict =>
        LengthVerdict.Color is not null ? LengthVerdict
        : EmbedmentVerdict.Color is not null ? EmbedmentVerdict
        : LengthVerdict;
}

/// <summary>
/// O resultado das análises de uma mesa: cada módulo e cada pilar com os
/// seus valores, e o que cada análise configurada marcou.
///
/// É o passo 5.6. Nada aqui decide geometria: a mesa já foi colocada (5.4)
/// e os pilares já foram calculados (5.5). Aqui só se pergunta às regras de
/// análise (4.3) o que pintar, e se guarda a resposta ao lado do valor — o
/// plano de requisitos manda cada módulo carregar "como propriedade
/// intrínseca o valor da sua ponta baixa", e cada pilar o comprimento e o
/// enterro. Cor e camada de cada peça estão nos vereditos; a mesa marcada
/// (<see cref="Marked"/>) não tem cor nem camada aqui, porque no plano a
/// "cor própria" é da mesa na borda e a marcada "é estudada à mão" — como
/// ela aparece na tela é decisão do desenho, registrada em PROGRESSO.md.
/// </summary>
/// <param name="Label">O letreiro da mesa.</param>
/// <param name="Marked">Se a fileira marcou a mesa (não cabe), e por quê em <paramref name="MarkedReason"/>.</param>
/// <param name="MarkedReason">O motivo da marca, ou null.</param>
/// <param name="Modules">A fileira de baixo, coluna a coluna.</param>
/// <param name="Pillars">Os pilares, na ordem da tabela.</param>
/// <param name="LongitudinalSlopeRadians">A declividade longitudinal da mesa, nunca negativa.</param>
/// <param name="SlopeVerdict">O que a análise de declividade disse.</param>
/// <param name="EdgeVerdict">O que a análise de borda disse.</param>
public sealed record TableReport(
    string Label,
    bool Marked,
    string? MarkedReason,
    IReadOnlyList<ModuleReport> Modules,
    IReadOnlyList<PillarReport> Pillars,
    double LongitudinalSlopeRadians,
    AnalysisVerdict SlopeVerdict,
    AnalysisVerdict EdgeVerdict)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Todos os vereditos que pedem pintura: um por módulo, um por pilar (o
    /// <see cref="PillarReport.PaintVerdict"/>), o da declividade e o da
    /// borda. Uma peça, um veredito.
    /// </summary>
    public IEnumerable<AnalysisVerdict> Painted =>
        Modules.Select(m => m.Verdict)
            .Concat(Pillars.Select(p => p.PaintVerdict))
            .Append(SlopeVerdict)
            .Append(EdgeVerdict)
            .Where(v => v.Color is not null);

    /// <summary>Quantas peças cada análise pinta.</summary>
    public IReadOnlyDictionary<AnalysisKind, int> PaintedByKind =>
        Painted.GroupBy(v => v.Kind).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Quantos módulos ficaram fora da faixa da ponta baixa (abaixo ou acima).</summary>
    public int ModulesOutsideBand => Modules.Count(m => m.Verdict.Outcome is AnalysisOutcome.Below or AnalysisOutcome.Above);

    /// <summary>A linha que descreve o relatório para o usuário.</summary>
    public string Describe()
    {
        var pintadas = PaintedByKind;

        string Conta(AnalysisKind kind, string nome) =>
            pintadas.TryGetValue(kind, out var n) && n > 0 ? $", {n} {nome}" : string.Empty;

        return $"{Label}: {Modules.Count} módulo(s) na fileira de baixo, {Pillars.Count} pilar(es)"
            + (Marked ? $", MARCADA ({MarkedReason})" : string.Empty)
            + Conta(AnalysisKind.LowEdge, "ponta(s) baixa(s) fora")
            + Conta(AnalysisKind.PillarLength, "pilar(es) compridos")
            + Conta(AnalysisKind.Embedment, "enterro(s) fora")
            + (SlopeVerdict.Color is not null
                ? $", declividade de {(LongitudinalSlopeRadians * 180 / Math.PI).ToString("0.#", Brasil)}° acima do limite"
                : string.Empty)
            + (EdgeVerdict.Color is not null ? ", na borda da área" : string.Empty);
    }
}

/// <summary>Monta o relatório de uma mesa.</summary>
public static class TableAnalysis
{
    /// <summary>
    /// Avalia a mesa contra as regras de análise.
    /// </summary>
    /// <param name="solved">As cotas escolhidas pela fileira (5.4).</param>
    /// <param name="samples">O terreno amostrado sob a mesa (5.2), de onde vem a altura livre de cada módulo.</param>
    /// <param name="length">O comprimento da mesa, para a cota da ponta baixa em cada estação.</param>
    /// <param name="pillars">Os pilares calculados (5.5).</param>
    /// <param name="cell">A célula em planta (5.1), de onde vem "na borda".</param>
    /// <param name="rules">As regras de análise (4.3).</param>
    /// <param name="configuration">Os limites (4.1).</param>
    /// <exception cref="InvalidOperationException">Regras ou configuração que não fecham.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Comprimento que não é medida, estação de módulo fora do comprimento,
    /// ou cota da mesa que não é número (as regras recusam valor não finito).
    /// </exception>
    public static TableReport Evaluate(
        SolvedTable solved,
        TableSamples samples,
        double length,
        TablePillars pillars,
        PlacedTable cell,
        AnalysisRules rules,
        SystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(solved);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(pillars);
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(configuration);

        if (rules.WhyInvalid is { } porRegras)
            throw new InvalidOperationException($"As regras de análise não fecham: {porRegras}.");

        if (configuration.WhyInvalid is { } porConfig)
            throw new InvalidOperationException($"A configuração não fecha: {porConfig}.");

        if (!double.IsFinite(length) || length < RowDistributor.MenorMedida)
            throw new ArgumentOutOfRangeException(nameof(length), length, "O comprimento da mesa não é uma medida válida.");

        // A amostra e o comprimento chegam de fontes separadas: estação além
        // do comprimento é amostra de outra mesa, e daria cota extrapolada.
        foreach (var m in samples.LowEdge)
        {
            if (m.Station < -1e-9 || m.Station > length + 1e-9)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), m.Station,
                    "Há módulo com estação fora do comprimento da mesa.");
            }
        }

        var modulos = samples.LowEdge
            .Select(m =>
            {
                if (m.GroundZ is not { } terreno || !double.IsFinite(terreno))
                    return new ModuleReport(m.Column, m.Station, null, Desligado(AnalysisKind.LowEdge));

                var cota = solved.StartElevation + (solved.EndElevation - solved.StartElevation) * m.Station / length;
                var livre = cota - terreno;

                return new ModuleReport(m.Column, m.Station, livre, rules.Evaluate(AnalysisKind.LowEdge, livre, configuration));
            })
            .ToList();

        var pilares = pillars.Pillars
            .Select(p => p.Length is { } comprimento
                ? new PillarReport(
                    p,
                    rules.Evaluate(AnalysisKind.PillarLength, comprimento, configuration),
                    rules.Evaluate(AnalysisKind.Embedment, p.Embedment, configuration))
                : new PillarReport(p, Desligado(AnalysisKind.PillarLength), Desligado(AnalysisKind.Embedment)))
            .ToList();

        // Asen, como no 5.3 e no giro da matriz (5.5): a mesa é rígida e o
        // desnível entre as pontas é L·sen(giro).
        var declividade = Math.Asin(Math.Clamp(Math.Abs(solved.EndElevation - solved.StartElevation) / length, 0, 1));

        return new TableReport(
            solved.Label,
            solved.Marked,
            solved.Reason,
            modulos,
            pilares,
            declividade,
            rules.Evaluate(AnalysisKind.LongitudinalSlope, declividade, configuration),
            rules.EvaluateEdge(cell.PartlyOutside));
    }

    /// <summary>
    /// O veredito de quem não tem o que medir: Off, sem cor. Não é Inside —
    /// "dentro" numa peça sem terreno seria dizer que conferiu.
    /// </summary>
    private static AnalysisVerdict Desligado(AnalysisKind kind) => new(kind, AnalysisOutcome.Off, null, null);
}
