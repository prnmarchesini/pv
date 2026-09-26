using UFV.Geo;

namespace UFV.Core;

/// <summary>Uma mesa processada de ponta a ponta: da célula em planta ao relatório das análises.</summary>
/// <param name="Cell">A célula em planta (5.1).</param>
/// <param name="Orientation">Para que lado a mesa sobe (5.1).</param>
/// <param name="Samples">
/// O terreno amostrado sob ela na posição sem giro (5.2): é o que decidiu
/// as cotas viáveis e o alinhamento.
/// </param>
/// <param name="FinalSamples">
/// O terreno reamostrado sob ela na posição final, com o giro (5.5): é o
/// que o relatório usa. A ponta baixa na estação s fica em planta em
/// s·cos(giro), até 28 cm antes da posição sem giro a 10°, e a regra
/// sagrada 4 é conferida onde o módulo está de fato.
/// </param>
/// <param name="Viable">As cotas viáveis (5.3).</param>
/// <param name="Solved">As cotas escolhidas pela fileira (5.4).</param>
/// <param name="Pillars">Os pilares, com a matriz de colocação final (5.5).</param>
/// <param name="Report">O resultado das análises (5.6).</param>
public sealed record ProcessedTable(
    PlacedTable Cell,
    RowOrientation Orientation,
    TableSamples Samples,
    TableSamples FinalSamples,
    ViableElevations Viable,
    SolvedTable Solved,
    TablePillars Pillars,
    TableReport Report)
{
    /// <summary>O letreiro da mesa (F1.3).</summary>
    public string Label => Cell.Label;

    /// <summary>A matriz que põe a geometria local da mesa no lugar, com cota e giro.</summary>
    public Transform Placement => Pillars.Placement;
}

/// <summary>Uma fileira processada.</summary>
/// <param name="Row">A fileira em planta (5.1).</param>
/// <param name="Solution">O alinhamento (5.4), com os trechos.</param>
/// <param name="Tables">As mesas, na ordem da fileira.</param>
/// <param name="Warnings">
/// O que vale avisar sem marcar: hoje, mesas vizinhas cujas pontas altas
/// se aproximam além do espaçamento por causa do giro (a ponta alta se
/// desloca ao longo da fileira por fundo × sen(tilt) × sen(giro), até
/// 0,30 m com os padrões).
/// </param>
public sealed record ProcessedRow(
    PlanRow Row, RowSolution Solution, IReadOnlyList<ProcessedTable> Tables, IReadOnlyList<string> Warnings)
{
    /// <summary>Quantas mesas a fileira marcou.</summary>
    public int MarkedCount => Tables.Count(t => t.Solved.Marked);

    /// <summary>Quantos pilares estouraram (regra sagrada 1 ou fora do terreno).</summary>
    public int PillarProblemCount => Tables.Sum(t => t.Pillars.ProblemCount);

    /// <summary>A linha que descreve a fileira para o usuário.</summary>
    public string Describe() =>
        $"F{Row.Number}: {Solution.Describe()}"
        + (PillarProblemCount > 0 ? $", {PillarProblemCount} pilar(es) com problema" : string.Empty);
}

/// <summary>
/// A costura dos passos 5.1 a 5.6 para uma fileira: célula → amostra → cotas
/// viáveis → alinhamento → pilares → análises. É o que o comando de uma
/// fileira (5.8) e o da área inteira (5.9) chamam, e o que o desenho (5.7)
/// consome.
///
/// Não decide nada por conta própria: cada passo é o que já foi testado em
/// separado, na ordem. O que fica aqui é a ordem e a regra sagrada 5 em
/// forma de dado — nada sai daqui sem cota vinda do terreno, e a mesa sem
/// terreno sai marcada, nunca com cota inventada.
/// </summary>
public static class RowPipeline
{
    /// <summary>
    /// Processa uma fileira da distribuição.
    /// </summary>
    /// <param name="row">A fileira em planta.</param>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="tiltRadians">A inclinação transversal da mesa.</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="settings">Configuração e regras de análise.</param>
    /// <param name="step">O passo da grade de cotas (5.3).</param>
    public static ProcessedRow ProcessRow(
        PlanRow row,
        TableGeometry geometry,
        double tiltRadians,
        Tin terrain,
        ProjectSettings settings,
        double step = ViableElevations.DefaultStep)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");

        if (row.Tables.Count == 0)
            throw new ArgumentException("A fileira não tem mesa.", nameof(row));

        var config = settings.Configuration;

        // 5.1 → 5.2 → 5.3, mesa a mesa.
        var orientacoes = new List<RowOrientation>();
        var amostras = new List<TableSamples>();
        var viaveis = new List<ViableElevations>();

        foreach (var celula in row.Tables)
        {
            var orientacao = RowOrientation.Resolve(celula, config.UpslopeAzimuthRadians);
            var colocacao = TablePlacement.Plan(celula, orientacao, tiltRadians, 0);
            var amostra = TerrainSampler.Sample(geometry, colocacao, terrain);

            orientacoes.Add(orientacao);
            amostras.Add(amostra);
            viaveis.Add(ViableElevations.Compute(amostra, geometry.Length, config, step));
        }

        // 5.4: a fileira inteira de uma vez, com o vão real entre as células.
        //
        // O solver encadeia o FIM local de uma mesa ao INÍCIO local da
        // seguinte. Quando o comprimento local corre contra a fileira (a
        // configuração padrão: mesa olhando para o norte, +X local para
        // oeste), a estação zero de cada mesa está na ponta de lá, e a junta
        // com a vizinha seguinte na fileira é entre o início desta e o fim
        // daquela. Então as mesas entram no solver na ordem inversa — na
        // ordem das estações locais — e os resultados voltam à ordem da
        // fileira. Sem isto, mesa sim mesa não saía marcada numa rampa.
        var ordem = Enumerable.Range(0, row.Tables.Count).ToList();
        if (!orientacoes[0].LengthRunsWithRow) ordem.Reverse();

        var entradas = new List<RowTable>();

        for (var j = 0; j < ordem.Count; j++)
        {
            var i = ordem[j];
            var anterior = j == 0 ? -1 : ordem[j - 1];

            var vao = anterior < 0
                ? 0
                : Math.Max(0, RowSolver.GapBetween(row.Tables[Math.Min(i, anterior)], row.Tables[Math.Max(i, anterior)]));

            entradas.Add(new RowTable(row.Tables[i].Label, vao, viaveis[i]));
        }

        var solucao = RowSolver.Solve(entradas, config);

        var resolvidas = new SolvedTable[row.Tables.Count];
        for (var j = 0; j < ordem.Count; j++) resolvidas[ordem[j]] = solucao.Tables[j];

        // 5.5 → 5.6. O relatório usa o terreno reamostrado na posição final,
        // com o giro: é onde o módulo está de fato.
        var mesas = new List<ProcessedTable>();

        for (var i = 0; i < row.Tables.Count; i++)
        {
            var pilares = PillarCalculator.Compute(
                geometry, row.Tables[i], orientacoes[i], tiltRadians, resolvidas[i], terrain, config);

            var finais = TerrainSampler.Sample(geometry, pilares.Placement, terrain);

            var relatorio = TableAnalysis.Evaluate(
                resolvidas[i], finais, geometry.Length, pilares, row.Tables[i], settings.Analyses, config);

            mesas.Add(new ProcessedTable(
                row.Tables[i], orientacoes[i], amostras[i], finais, viaveis[i], resolvidas[i], pilares, relatorio));
        }

        return new ProcessedRow(row, solucao, mesas, Avisos(mesas, geometry, tiltRadians, orientacoes[0].LengthRunsWithRow));
    }

    /// <summary>
    /// Os avisos de pontas altas que se aproximam além do espaçamento.
    ///
    /// Com o giro g, o canto alto do início local sai da célula por
    /// D·sen(tilt)·sen(g) quando g é positivo; o do fim local sai por
    /// D·sen(tilt)·sen(−g) − L·(1 − cos g) quando g é negativo (o
    /// encurtamento do comprimento em planta compensa quase tudo). Em cada
    /// junta, a soma do que as duas mesas avançam uma sobre a outra é
    /// comparada com o vão em planta.
    /// </summary>
    private static IReadOnlyList<string> Avisos(
        List<ProcessedTable> mesas, TableGeometry geometry, double tiltRadians, bool comprimentoComAFileira)
    {
        var avisos = new List<string>();
        var d = geometry.Depth;
        var l = geometry.Length;

        double NoInicio(ProcessedTable m) => Math.Max(0, d * Math.Sin(tiltRadians) * Math.Sin(m.Pillars.LongitudinalTiltRadians));

        double NoFim(ProcessedTable m) => Math.Max(0,
            d * Math.Sin(tiltRadians) * Math.Sin(-m.Pillars.LongitudinalTiltRadians)
            - l * (1 - Math.Cos(m.Pillars.LongitudinalTiltRadians)));

        for (var i = 0; i + 1 < mesas.Count; i++)
        {
            var a = mesas[i];
            var b = mesas[i + 1];
            var vao = Math.Max(0, RowSolver.GapBetween(a.Cell, b.Cell));

            // Quem encosta em quem: com o comprimento correndo com a fileira,
            // o fim local de A toca o início local de B; senão, o contrário.
            var avanco = comprimentoComAFileira ? NoFim(a) + NoInicio(b) : NoInicio(a) + NoFim(b);

            if (avanco > vao + 1e-6)
            {
                avisos.Add(
                    $"as pontas altas de {a.Label} e {b.Label} avançam {avanco:0.00} m uma sobre a outra "
                    + $"e o vão entre elas é {vao:0.00} m: o giro faz as mesas se cruzarem");
            }
        }

        return avisos;
    }
}
