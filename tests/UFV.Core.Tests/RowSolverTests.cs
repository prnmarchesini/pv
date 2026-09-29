namespace UFV.Core.Tests;

/// <summary>
/// A corrente (29/09/2026): a fileira resolvida como uma corrente de mesas em
/// que a ponta de uma e a ponta da vizinha têm a mesma PB, com o custo de
/// voar maior que o de enterrar.
///
/// Substitui os testes do alinhamento por degraus (etapa 5, 5.4), que o
/// Renan aboliu: "PONTAS de pilares SEMPRE na mesma altura", "melhor enfiar
/// o módulo na terra e pintar do que deixar uma ponta flutuando".
/// </summary>
public class RowSolverTests
{
    private const double Grau = Math.PI / 180;
    private const double Comprimento = 18.702;
    private const double Vao = 0.50;
    private const double PrimeiroPilar = 1.20;
    private const double UltimoPilar = 17.50;

    private static double Estacao(int coluna) => 0.10 + coluna * (1.303 + 0.02) + 1.303 / 2;

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    /// <summary>
    /// Uma fileira de mesas sobre um terreno dado por uma função da posição
    /// ao longo da fileira (em metro, do início da primeira mesa).
    /// </summary>
    private static List<ChainTable> Fileira(int quantas, Func<double, double?> terreno, double vao = Vao)
    {
        var mesas = new List<ChainTable>();

        for (var i = 0; i < quantas; i++)
        {
            var inicio = i * (Comprimento + vao);

            var modulos = Enumerable.Range(0, 14)
                .Select(c => new ChainModule(Estacao(c), terreno(inicio + Estacao(c))))
                .ToList();

            mesas.Add(new ChainTable(
                $"F1.{i + 1}", i == 0 ? 0 : vao, Comprimento, modulos,
                PrimeiroPilar, terreno(inicio + PrimeiroPilar), UltimoPilar, terreno(inicio + UltimoPilar)));
        }

        return mesas;
    }

    /// <summary>A cota da ponta baixa da mesa resolvida na estação dada.</summary>
    private static double Cota(SolvedTable mesa, double estacao) =>
        mesa.StartElevation + (mesa.EndElevation - mesa.StartElevation) * estacao / Comprimento;

    /// <summary>A PB de cada ponta: (primeira, última).</summary>
    private static (double Primeira, double Ultima) Pontas(ChainTable mesa, SolvedTable resolvida) =>
        (Cota(resolvida, mesa.FirstStation) - mesa.FirstGround!.Value, Cota(resolvida, mesa.LastStation) - mesa.LastGround!.Value);

    /// <summary>
    /// A regra: em cada trecho, a última ponta de uma mesa tem a PB da
    /// primeira ponta da seguinte, e as duas são a PB da junta que o solver
    /// relata. E a declividade de cada mesa fica no limite.
    /// </summary>
    private static void CorrenteFechada(IReadOnlyList<ChainTable> mesas, RowSolution solucao, SystemConfiguration config)
    {
        var porLetreiro = mesas.ToDictionary(m => m.Label);

        foreach (var trecho in solucao.Runs)
        {
            if (trecho.JointClearances.Count == 0) continue;

            Assert.Equal(trecho.Tables.Count + 1, trecho.JointClearances.Count);

            for (var i = 0; i < trecho.Tables.Count; i++)
            {
                var mesa = porLetreiro[trecho.Tables[i].Label];
                var (primeira, ultima) = Pontas(mesa, trecho.Tables[i]);

                Assert.Equal(trecho.JointClearances[i], primeira, 6);
                Assert.Equal(trecho.JointClearances[i + 1], ultima, 6);

                if (config.MaxLongitudinalSlope is { } limite)
                {
                    var giro = Math.Abs(trecho.Tables[i].EndElevation - trecho.Tables[i].StartElevation) / Comprimento;
                    Assert.True(giro <= Math.Sin(limite) + 1e-9, $"{mesa.Label}: giro de {Math.Asin(giro) / Grau:0.##}°, acima do limite");
                }
            }
        }
    }

    /// <summary>As alturas livres da ponta baixa de cada módulo.</summary>
    private static IEnumerable<double> Folgas(IReadOnlyList<ChainTable> mesas, RowSolution solucao)
    {
        var porLetreiro = mesas.ToDictionary(m => m.Label);

        foreach (var resolvida in solucao.Tables)
        {
            var mesa = porLetreiro[resolvida.Label];

            foreach (var m in mesa.Modules.Where(m => m.Ground is not null))
                yield return Cota(resolvida, m.Station) - m.Ground!.Value;
        }
    }

    // ------------------------------------------------------ o que é a regra

    /// <summary>Terreno plano: toda mesa na faixa, nenhuma marcada, toda junta na mesma PB.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void TerrenoPlanoTudoNaFaixaENaMesmaPb()
    {
        var mesas = Fileira(6, _ => 700.0);
        var solucao = RowSolver.Solve(mesas, Config());

        CorrenteFechada(mesas, solucao, Config());
        Assert.Equal(0, solucao.MarkedCount);
        Assert.Single(solucao.Runs);
        Assert.All(solucao.Runs[0].JointClearances, pb => Assert.Equal(Config().MinLowEdge, pb, 6));
    }

    /// <summary>Rampa de 8% ao longo da fileira (abaixo do limite de 10°): a corrente acompanha, nada fora da faixa.</summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0.08)]
    [InlineData(-0.08)]
    [InlineData(0.15)]
    public void RampaDentroDoLimiteAcompanhaSemMarcar(double rampa)
    {
        var mesas = Fileira(8, x => 700 + rampa * x);
        var solucao = RowSolver.Solve(mesas, Config());

        CorrenteFechada(mesas, solucao, Config());
        Assert.Equal(0, solucao.MarkedCount);
        Assert.All(Folgas(mesas, solucao), f => Assert.InRange(f, Config().MinLowEdge - 1e-6, Config().MaxLowEdge + 1e-6));
    }

    /// <summary>
    /// A cena da F30.6 (29/09/2026): uma mesa com um vale que nenhuma
    /// inclinação vence, entre vizinhas no plano. A ponta dela NÃO sobe
    /// acima da faixa para compensar o buraco: a junta fecha na mesma PB,
    /// as pontas até descem abaixo da faixa para o meio voar menos
    /// ("prefiro módulo na terra do que módulo voando"), e o que marca fica
    /// na mesa do vale e nas duas vizinhas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ValeNaMesaNaoLevantaAPonta()
    {
        // Vale de 1,2 m no meio da terceira mesa.
        double? Terreno(double x)
        {
            var meio = 2 * (Comprimento + Vao) + Comprimento / 2;
            var d = Math.Abs(x - meio);
            return 700 - (d < 4 ? 1.2 * (1 - d / 4) : 0);
        }

        var mesas = Fileira(5, Terreno);
        var solucao = RowSolver.Solve(mesas, Config());

        CorrenteFechada(mesas, solucao, Config());

        Assert.All(solucao.Runs[0].JointClearances, pb => Assert.True(pb <= Config().MaxLowEdge + 1e-6, $"ponta voando a {pb:0.00} m"));
        Assert.All(solucao.Runs[0].JointClearances, pb => Assert.True(pb >= 0, $"ponta enterrada a {pb:0.00} m para salvar um vale"));

        // O vale continua acima da faixa (não há como descer a mesa até ele
        // sem enterrar demais), menos do que ficaria com as pontas na faixa.
        var doVale = mesas[2];
        var resolvida = solucao.Tables[2];
        var maior = doVale.Modules.Max(m => Cota(resolvida, m.Station) - m.Ground!.Value);
        Assert.True(maior < Config().MinLowEdge + 1.2, $"o meio da mesa do vale ficou a {maior:0.00} m");

        // Marcadas: a do vale e, no máximo, as duas vizinhas, que dividem
        // com ela as pontas abaixadas. As mesas longe do vale não sentem.
        var marcadas = solucao.Tables.Where(t => t.Marked).Select(t => t.Label).ToList();
        Assert.Contains("F1.3", marcadas);
        Assert.All(marcadas, l => Assert.Contains(l, new[] { "F1.2", "F1.3", "F1.4" }));
    }

    /// <summary>
    /// Terreno mais íngreme que o limite de declividade (19° com limite de
    /// 10°, duas mesas, como a F40 do Itatiba): a mesa não consegue
    /// acompanhar e a corrente não abre. Voar é o pior: nenhuma ponta passa
    /// da faixa, e o que sai dela sai para baixo — enterrado, marcado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaAcimaDoLimiteEnterraEmVezDeVoar()
    {
        var rampa = Math.Tan(19 * Grau);
        var mesas = Fileira(2, x => 700 - rampa * x);
        var solucao = RowSolver.Solve(mesas, Config());

        CorrenteFechada(mesas, solucao, Config());

        Assert.True(solucao.MarkedCount > 0);
        Assert.All(solucao.Runs[0].JointClearances, pb => Assert.True(pb <= Config().MaxLowEdge + 1e-6, $"ponta voando a {pb:0.00} m"));

        var folgas = Folgas(mesas, solucao).ToList();
        var acima = folgas.Count(f => f > Config().MaxLowEdge + 1e-6);
        var abaixo = folgas.Count(f => f < Config().MinLowEdge - 1e-6);

        Assert.True(abaixo > acima, $"{abaixo} módulo(s) abaixo e {acima} acima: devia afundar mais do que voar");
        Assert.Contains(solucao.Tables, t => t.Reason is { } r && r.Contains("enterrado", StringComparison.Ordinal));
    }

    /// <summary>
    /// Com a mesa podendo escolher: o custo de voar é maior que o de ficar
    /// abaixo da faixa pela mesma distância. Um lombo que obriga a escolher
    /// entre as pontas acima ou o lombo abaixo fica com o lombo abaixo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EntreVoarEAfundarAfunda()
    {
        // Lombo de 0,9 m no meio de uma mesa sozinha: a faixa aceita 0,5 m.
        double? Terreno(double x)
        {
            var d = Math.Abs(x - Comprimento / 2);
            return 700 + (d < 3 ? 0.9 * (1 - d / 3) : 0);
        }

        var mesas = Fileira(1, Terreno);
        var solucao = RowSolver.Solve(mesas, Config());
        var folgas = Folgas(mesas, solucao).ToList();

        Assert.True(solucao.Tables[0].Marked);
        Assert.True(folgas.Max() <= Config().MaxLowEdge + 1e-6, $"módulo acima da faixa: {folgas.Max():0.00} m");
        Assert.True(folgas.Min() < Config().MinLowEdge, "o lombo deveria ter ficado abaixo da faixa");
    }

    /// <summary>A marca diz a verdade: marcada é exatamente a que tem mais módulos fora que a tolerância.</summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(2)]
    public void AMarcaDizAVerdade(int tolerancia)
    {
        var config = Config() with { BumpToleranceModules = tolerancia };
        var aleatorio = new Random(7);
        var ruido = Enumerable.Range(0, 400).Select(_ => aleatorio.NextDouble() * 0.9).ToArray();

        var mesas = Fileira(10, x => 700 + 0.05 * x + ruido[(int)(x * 2) % ruido.Length]);
        var solucao = RowSolver.Solve(mesas, config);

        CorrenteFechada(mesas, solucao, config);

        foreach (var resolvida in solucao.Tables)
        {
            var mesa = mesas.Single(m => m.Label == resolvida.Label);
            var fora = mesa.Modules.Count(m =>
            {
                var f = Cota(resolvida, m.Station) - m.Ground!.Value;
                return f < config.MinLowEdge - 1e-9 || f > config.MaxLowEdge + 1e-9;
            });

            Assert.Equal(fora, resolvida.Violations);
            Assert.Equal(fora > tolerancia, resolvida.Marked);
            Assert.Equal(resolvida.Marked, resolvida.Reason is not null);
        }
    }

    /// <summary>
    /// A programação dinâmica acha o ótimo da grade: numa fileira de duas
    /// mesas com grade grossa, a força bruta sobre todas as PBs das três
    /// juntas dá o mesmo custo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ForcaBrutaConcordaComAProgramacaoDinamica()
    {
        var config = Config();
        var pesos = ChainWeights.Default;
        const double passo = 0.05;

        double? Terreno(double x) => 700 + 0.12 * x + (x > 10 && x < 14 ? 0.7 : 0) - (x > 25 && x < 29 ? 0.6 : 0);

        var mesas = Fileira(2, Terreno);
        var solucao = RowSolver.Solve(mesas, config, pesos, passo);

        double Custo(double[] pbs)
        {
            var total = pbs.Sum(pb => pesos.TipCost(pb, config.MinLowEdge, config.MaxLowEdge));

            for (var i = 0; i < mesas.Count; i++)
            {
                var m = mesas[i];
                var za = m.FirstGround!.Value + pbs[i];
                var zb = m.LastGround!.Value + pbs[i + 1];

                if (Math.Abs(zb - za) > Math.Sin(config.MaxLongitudinalSlope!.Value) * (m.LastStation - m.FirstStation) + 1e-9)
                    return double.PositiveInfinity;

                foreach (var mod in m.Modules)
                {
                    var f = za + (zb - za) * (mod.Station - m.FirstStation) / (m.LastStation - m.FirstStation) - mod.Ground!.Value;
                    total += pesos.Cost(f, config.MinLowEdge, config.MaxLowEdge);
                }
            }

            return total;
        }

        var grade = Enumerable.Range(-36, 83).Select(k => k * passo).ToArray(); // de −1,80 a 2,30 m
        var melhor = double.PositiveInfinity;

        foreach (var a in grade)
            foreach (var b in grade)
                foreach (var c in grade)
                    melhor = Math.Min(melhor, Custo([a, b, c]));

        Assert.Equal(melhor, Custo(solucao.Runs[0].JointClearances.ToArray()), 6);
    }

    /// <summary>
    /// O Recalcular de uma mesa só: as pontas ficam presas na PB das
    /// vizinhas, e a corrente escolhe só o giro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PontaPresaFicaNaPbDaVizinha()
    {
        var mesas = Fileira(1, x => 700 + 0.06 * x);
        var solucao = RowSolver.Solve(mesas, Config(), firstTip: 0.55, lastTip: 0.40);

        CorrenteFechada(mesas, solucao, Config());
        Assert.Equal(0.55, solucao.Runs[0].JointClearances[0], 6);
        Assert.Equal(0.40, solucao.Runs[0].JointClearances[1], 6);

        var (primeira, ultima) = Pontas(mesas[0], solucao.Tables[0]);
        Assert.Equal(0.55, primeira, 6);
        Assert.Equal(0.40, ultima, 6);
    }

    /// <summary>
    /// A falha que a revisão de 29/09 achou: PBs presas fora da grade de
    /// 5 cm, com o desnível entre elas a 1 cm do limite de declividade. A
    /// grade grossa não pode arredondá-las para fora do limite e soltar as
    /// pontas: elas ficam exatamente onde as vizinhas estão.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0.33, 1)]
    [InlineData(0.47, -1)]
    public void PontaPresaForaDaGradeGrossaNoLimiteDaDeclividade(double primeira, int sentido)
    {
        var mesas = Fileira(1, _ => 700.0);
        var alcance = Math.Sin(Config().MaxLongitudinalSlope!.Value) * (UltimoPilar - PrimeiroPilar);
        var ultima = primeira + sentido * (alcance - 0.01);

        var solucao = RowSolver.Solve(mesas, Config(), firstTip: primeira, lastTip: ultima);

        CorrenteFechada(mesas, solucao, Config());
        Assert.Equal(primeira, solucao.Runs[0].JointClearances[0], 2);
        Assert.Equal(ultima, solucao.Runs[0].JointClearances[1], 2);
    }

    /// <summary>
    /// Pontas presas que a declividade não deixa ligar (vizinha mexida à
    /// mão): a mesa não derruba o Recalcular, resolve livre.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PontaPresaImpossivelSoltaEResolveLivre()
    {
        var mesas = Fileira(1, _ => 700.0);
        var solucao = RowSolver.Solve(mesas, Config(), firstTip: 0.30, lastTip: 5.0);

        CorrenteFechada(mesas, solucao, Config());
        Assert.False(solucao.Tables[0].Marked);
    }

    // ------------------------------------------------ o que fica de fora

    /// <summary>Vão maior que o limite quebra a fileira: dois trechos, cada um com a sua corrente.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VaoMaiorQueOLimiteQuebraAFileira()
    {
        var mesas = Fileira(4, x => 700 + 0.03 * x);
        mesas[2] = mesas[2] with { GapBefore = Config().MaxGapBeforeBreak + 0.5 };

        var solucao = RowSolver.Solve(mesas, Config());

        Assert.Equal(2, solucao.Runs.Count);
        Assert.Equal(2, solucao.Runs[0].Tables.Count);
        CorrenteFechada(mesas, solucao, Config());
    }

    /// <summary>Vão exatamente no limite não quebra.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VaoNoLimiteNaoQuebra()
    {
        var mesas = Fileira(3, _ => 700.0);
        mesas[1] = mesas[1] with { GapBefore = Config().MaxGapBeforeBreak };

        Assert.Single(RowSolver.Solve(mesas, Config()).Runs);
    }

    /// <summary>Mesa com módulo sem terreno: fica fora da corrente, marcada, sobre o terreno que tem; as outras seguem.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaSemTerrenoFicaMarcadaEAFileiraSegue()
    {
        var comprimento = Comprimento + Vao;
        var mesas = Fileira(5, x => x > 2 * comprimento + 3 && x < 2 * comprimento + 6 ? null : 700.0);

        var solucao = RowSolver.Solve(mesas, Config());
        var sem = solucao.Tables.Single(t => t.Label == "F1.3");

        Assert.True(sem.Marked);
        Assert.True(sem.Seated);
        Assert.Contains("sem terreno", sem.Reason, StringComparison.Ordinal);
        Assert.Equal(700 + Config().MinLowEdge, sem.StartElevation, 6);
        Assert.Equal(3, solucao.Runs.Count);
        Assert.Equal(4, solucao.Tables.Count(t => !t.Marked));
        CorrenteFechada(mesas, solucao, Config());
    }

    /// <summary>Fileira inteira fora do terreno: toda mesa marcada, em cota zero, dita.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraInteiraSemTerrenoFicaTodaMarcada()
    {
        var solucao = RowSolver.Solve(Fileira(3, _ => null), Config());

        Assert.All(solucao.Tables, t =>
        {
            Assert.True(t.Marked);
            Assert.Equal(0, t.StartElevation);
            Assert.NotNull(t.Reason);
        });
    }

    /// <summary>A tolerância de lombo deixa passar sem marca o que está dentro dela.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CalomboDentroDaToleranciaNaoMarca()
    {
        // Calombo estreito sob um módulo só.
        double? Terreno(double x) => 700 + (Math.Abs(x - Estacao(6)) < 0.5 ? 0.9 : 0);

        var config = Config() with { BumpToleranceModules = 1 };
        var solucao = RowSolver.Solve(Fileira(1, Terreno), config);

        Assert.False(solucao.Tables[0].Marked);
        Assert.Equal(1, solucao.Tables[0].Violations);
    }

    /// <summary>Mesmo resultado sempre, e rápido numa fileira de quarenta mesas com terreno irregular.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EDeterministaERapido()
    {
        var aleatorio = new Random(11);
        var ruido = Enumerable.Range(0, 2000).Select(_ => aleatorio.NextDouble() * 0.6).ToArray();
        var mesas = Fileira(40, x => 700 + 3 * Math.Sin(x / 40) + ruido[(int)(x * 2) % ruido.Length]);

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var uma = RowSolver.Solve(mesas, Config());
        relogio.Stop();

        var outra = RowSolver.Solve(mesas, Config());

        Assert.Equal(uma.Tables, outra.Tables);
        Assert.True(relogio.Elapsed.TotalSeconds < 2, $"a fileira levou {relogio.Elapsed.TotalSeconds:0.00} s");
        CorrenteFechada(mesas, uma, Config());
    }

    // ------------------------------------------------------------ recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraVaziaERecusada()
    {
        Assert.Throws<ArgumentException>(() => RowSolver.Solve([], Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void VaoNegativoERecusado()
    {
        var mesas = Fileira(2, _ => 700.0);
        mesas[1] = mesas[1] with { GapBefore = -0.1 };

        Assert.Throws<ArgumentException>(() => RowSolver.Solve(mesas, Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void MesaSemDuasPontasERecusada()
    {
        var mesas = Fileira(1, _ => 700.0);
        mesas[0] = mesas[0] with { LastStation = mesas[0].FirstStation };

        Assert.Throws<ArgumentException>(() => RowSolver.Solve(mesas, Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ConfiguracaoQuebradaERecusada()
    {
        var config = Config() with { MinLowEdge = 0.9, MaxLowEdge = 0.3 };

        Assert.Throws<InvalidOperationException>(() => RowSolver.Solve(Fileira(1, _ => 700.0), config));
    }
}
