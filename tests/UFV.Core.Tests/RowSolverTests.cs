using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// O alinhamento na fileira: a cota de cada mesa, a fileira inteira de uma
/// vez, com os degraus permitidos e o menor estouro.
///
/// Os testes do plano: terreno plano dá degrau zero; rampa uniforme; calombo
/// dentro e fora da tolerância. E as garantias: todo degrau permitido, toda
/// mesa com cota, a que não cabe nivelada e marcada, vão grande quebra.
/// </summary>
public class RowSolverTests
{
    private const double Grau = Math.PI / 180;
    private const double Comprimento = 18.702;
    private const double Vao = 0.50;

    private static double Estacao(int coluna) => 0.10 + coluna * (1.303 + 0.02) + 1.303 / 2;

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    /// <summary>
    /// Uma fileira de mesas sobre um terreno dado por uma função da posição
    /// ao longo da fileira (em metro, do início da primeira mesa).
    /// </summary>
    private static List<RowTable> Fileira(int quantas, Func<double, double?> terreno, SystemConfiguration config, double vao = Vao)
    {
        var mesas = new List<RowTable>();

        for (var i = 0; i < quantas; i++)
        {
            var inicio = i * (Comprimento + vao);

            var pontaBaixa = Enumerable.Range(0, 14)
                .Select(c => new LowEdgeSample(c, Estacao(c), 0, 0, terreno(inicio + Estacao(c))))
                .ToList();

            var viaveis = ViableElevations.Compute(new TableSamples([], pontaBaixa), Comprimento, config);

            mesas.Add(new RowTable($"F1.{i + 1}", i == 0 ? 0 : vao, viaveis));
        }

        return mesas;
    }

    /// <summary>Todo degrau está em {0} ∪ [mínimo, máximo], sempre.</summary>
    private static void DegrausPermitidos(RowSolution solucao, SystemConfiguration config)
    {
        foreach (var trecho in solucao.Runs)
        {
            foreach (var degrau in trecho.Steps)
            {
                var d = Math.Abs(degrau);

                Assert.True(d < 1e-9 || (d >= config.MinStep - 1e-9 && d <= config.MaxStep + 1e-9),
                    $"degrau de {degrau} m fora de {{0}} ∪ [{config.MinStep}, {config.MaxStep}]");
            }
        }
    }

    // ------------------------------------------------------- os do plano

    /// <summary>Terreno plano: toda mesa cabe, nenhuma marcada, degrau zero em toda junta.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void TerrenoPlanoDaDegrauZero()
    {
        var solucao = RowSolver.Solve(Fileira(8, _ => 700, Config()), Config());

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(8, trecho.Tables.Count);
        Assert.Equal(0, trecho.MarkedCount);
        Assert.Equal(0, trecho.ViolationCount);
        Assert.All(trecho.Steps, d => Assert.Equal(0, d, 9));
        Assert.All(trecho.Tables, t => Assert.Equal(t.StartElevation, t.EndElevation, 9));
        Assert.All(trecho.Tables, t => Assert.InRange(t.StartElevation, 700.30 - 1e-9, 700.80 + 1e-9));
    }

    /// <summary>
    /// Rampa uniforme de 3 cm/m: toda mesa cabe (acompanhando a rampa, dentro
    /// dos 10°), nenhuma marcada, e os degraus, se houver, são pequenos —
    /// a rampa sobe 0,58 m por mesa e o degrau máximo é 0,50.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaUniformeAcompanhaSemMarcar()
    {
        var solucao = RowSolver.Solve(Fileira(8, s => 700 + 0.03 * s, Config()), Config());

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(0, trecho.MarkedCount);
        Assert.Equal(0, trecho.ViolationCount);
        DegrausPermitidos(solucao, Config());

        // As mesas sobem com o terreno: a última começa bem acima da primeira.
        Assert.True(trecho.Tables[^1].StartElevation > trecho.Tables[0].StartElevation + 3);

        // E cada mesa inclina no sentido da rampa.
        Assert.All(trecho.Tables, t => Assert.True(t.EndElevation > t.StartElevation));
    }

    /// <summary>
    /// Rampa íngreme demais para a mesa acompanhar sozinha (3 cm/m com limite
    /// de 1°, que é 1,75 cm/m: a mesa sobe 0,33 m onde o terreno sobe 0,58),
    /// mas os degraus dão conta: a fileira sobe por degraus de até 0,50 m e
    /// nenhuma mesa é marcada. É o "alinhamento entre mesas primeiro".
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaSobeEmDegrausQuandoAMesaNaoPodeInclinar()
    {
        var config = Config() with { MaxLongitudinalSlope = 1 * Grau, MaxStep = 0.50 };
        var solucao = RowSolver.Solve(Fileira(6, s => 700 + 0.03 * s, config), config);

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(0, trecho.MarkedCount);
        DegrausPermitidos(solucao, config);
        Assert.Contains(trecho.Steps, d => d > 0.05);
    }

    /// <summary>
    /// Calombo de 0,60 m sob um módulo da terceira mesa, tolerância de um:
    /// a mesa fica com um módulo fora, não é marcada, e o resto da fileira
    /// nem percebe.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CalomboDentroDaToleranciaNaoMarca()
    {
        var config = Config() with { BumpToleranceModules = 1 };
        var calombo = 2 * (Comprimento + Vao) + Estacao(7);

        var solucao = RowSolver.Solve(Fileira(5, s => Math.Abs(s - calombo) < 0.01 ? 700.60 : 700, config), config);

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(0, trecho.MarkedCount);
        Assert.Equal(1, trecho.ViolationCount);
        Assert.Equal(1, trecho.Tables[2].Violations);
        Assert.False(trecho.Tables[2].Marked);
        DegrausPermitidos(solucao, config);
    }

    /// <summary>
    /// O mesmo calombo com tolerância zero: a terceira mesa não cabe, fica
    /// nivelada e marcada com o motivo, e as outras quatro seguem inteiras.
    /// O motor não move nem apaga.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CalomboForaDaToleranciaMarcaSoAquelaMesa()
    {
        var calombo = 2 * (Comprimento + Vao) + Estacao(7);

        var solucao = RowSolver.Solve(Fileira(5, s => Math.Abs(s - calombo) < 0.01 ? 700.60 : 700, Config()), Config());

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(1, trecho.MarkedCount);
        Assert.True(trecho.Tables[2].Marked);
        Assert.NotNull(trecho.Tables[2].Reason);
        Assert.Equal(trecho.Tables[2].StartElevation, trecho.Tables[2].EndElevation, 9);

        foreach (var i in new[] { 0, 1, 3, 4 })
        {
            Assert.False(trecho.Tables[i].Marked);
            Assert.Equal(0, trecho.Tables[i].Violations);
        }

        DegrausPermitidos(solucao, Config());
        Assert.Equal(5, trecho.Tables.Count);
    }

    /// <summary>
    /// O caso de uso central do plugin: terreno inclinado. Rampas de 6, 8 e
    /// 10 % (3,4°, 4,6° e 5,7°, dentro dos 10°), subindo e descendo, com uma
    /// mesa e com seis: nenhuma marcada, nenhum estouro. A primeira versão
    /// marcava uma mesa sozinha a 6 % e lançava exceção com duas.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0.06, 1)]
    [InlineData(0.06, 6)]
    [InlineData(0.08, 6)]
    [InlineData(0.10, 6)]
    [InlineData(-0.06, 6)]
    [InlineData(-0.10, 6)]
    [InlineData(-0.10, 1)]
    public void RampasIngremesDentroDoLimiteNaoMarcam(double rampa, int quantas)
    {
        var solucao = RowSolver.Solve(Fileira(quantas, s => 700 + rampa * s, Config()), Config());

        Assert.Equal(0, solucao.MarkedCount);
        Assert.Equal(0, solucao.Runs.Sum(r => r.ViolationCount));
        DegrausPermitidos(solucao, Config());
    }

    // ------------------------------------------------------- garantias

    /// <summary>
    /// Um paredão entre a terceira e a quarta mesa, degrau máximo de 0,50:
    /// não dá para subir tudo numa junta, então UMA mesa é marcada — a
    /// quarta, que serve de escada inclinada — e as outras cinco ficam
    /// inteiras. Todo degrau continua permitido e toda mesa tem cota. A
    /// primeira versão marcava duas (a terceira, que cabia, a 1,28 m do
    /// chão) e, com 2,1 m ou mais, lançava exceção.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(2.0)]
    [InlineData(2.1)]
    [InlineData(4.0)]
    public void ParedaoMaiorQueODegrauMarcaSoAEscada(double altura)
    {
        var paredao = 3 * (Comprimento + Vao) - Vao / 2;

        var solucao = RowSolver.Solve(Fileira(6, s => s < paredao ? 700 : 700 + altura, Config()), Config());

        var trecho = Assert.Single(solucao.Runs);

        Assert.Equal(1, trecho.MarkedCount);
        Assert.True(trecho.Tables[3].Marked, "a escada devia ser a quarta mesa");
        Assert.True(trecho.Tables[3].EndElevation > trecho.Tables[3].StartElevation, "a escada devia inclinar");

        foreach (var i in new[] { 0, 1, 2, 4, 5 })
        {
            Assert.False(trecho.Tables[i].Marked);
            Assert.Equal(0, trecho.Tables[i].Violations);
        }

        Assert.All(trecho.Tables, t => Assert.True(double.IsFinite(t.StartElevation) && double.IsFinite(t.EndElevation)));
        DegrausPermitidos(solucao, Config());
    }

    /// <summary>
    /// Estouro tolerado vale mais que degrau: numa rampa de 3 % com limite
    /// de 1° e tolerância de dois módulos, a fileira sobe por degraus sem
    /// gastar a tolerância. Se degrau valesse mais que estouro, ela deixaria
    /// módulos fora para economizar degrau.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EstouroValeMaisQueDegrau()
    {
        var config = Config() with { MaxLongitudinalSlope = 1 * Grau, BumpToleranceModules = 2 };
        var solucao = RowSolver.Solve(Fileira(6, s => 700 + 0.03 * s, config), config);

        Assert.Equal(0, solucao.MarkedCount);
        Assert.Equal(0, solucao.Runs[0].ViolationCount);
        Assert.Contains(solucao.Runs[0].Steps, d => d > 0.03);
    }

    /// <summary>
    /// Degrau mínimo determinístico: terreno em 700 nas três primeiras mesas
    /// e em 700,60 nas três últimas. Sem a regra, o ótimo usa um degrau
    /// pequeno (6 cm: as mesas inclinam e absorvem o resto do salto); com
    /// mínimo de 0,20, o degrau é 0 ou pelo menos 0,20, e a fileira não
    /// marca ninguém.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DegrauMinimoImpedeODegrauPequeno()
    {
        var salto = 3 * (Comprimento + Vao) - Vao / 2;
        Func<double, double?> terreno = s => s < salto ? 700 : 700.60;

        var comMinimoCfg = Config() with { MinStep = 0.20 };
        var semMinimo = RowSolver.Solve(Fileira(6, terreno, Config()), Config());
        var comMinimo = RowSolver.Solve(Fileira(6, terreno, comMinimoCfg), comMinimoCfg);

        Assert.Equal(0, semMinimo.MarkedCount);
        Assert.Equal(0, comMinimo.MarkedCount);

        // Sem mínimo há um degrau pequeno; com mínimo, nenhum degrau em (0, 0,20).
        Assert.Contains(semMinimo.Runs[0].Steps, d => Math.Abs(d) > 1e-9 && Math.Abs(d) < 0.20 - 1e-9);
        Assert.DoesNotContain(comMinimo.Runs[0].Steps, d => Math.Abs(d) > 1e-9 && Math.Abs(d) < 0.20 - 1e-9);
        Assert.Contains(comMinimo.Runs[0].Steps, d => Math.Abs(d) >= 0.20 - 1e-9);
    }

    /// <summary>
    /// Força bruta contra a programação dinâmica: com duas mesas e passo
    /// grosso, enumera-se todo par (início, fim) viável de cada mesa mais as
    /// opções marcadas com os mesmos cinco giros, com o mesmo custo, e o
    /// mínimo tem que ser o que a DP achou. Pega janela, ordem de critérios
    /// e carimbo de uma vez, em terrenos aleatórios.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ForcaBrutaConcordaComAProgramacaoDinamica()
    {
        const double passo = 0.05;
        var sorteio = new Random(57);

        for (var caso = 0; caso < 12; caso++)
        {
            var rampa = (sorteio.NextDouble() - 0.5) * 0.16;
            var salto = sorteio.NextDouble() < 0.5 ? 0 : sorteio.NextDouble() * 2.5;
            var config = Config() with { BumpToleranceModules = sorteio.Next(2), MaxStep = 0.3 + sorteio.NextDouble() * 0.3 };

            var mesas = new List<RowTable>();

            for (var i = 0; i < 2; i++)
            {
                var inicio = i * (Comprimento + Vao);
                var pontaBaixa = Enumerable.Range(0, 14)
                    .Select(c => new LowEdgeSample(c, Estacao(c), 0, 0, 700 + rampa * (inicio + Estacao(c)) + (i == 1 ? salto : 0)))
                    .ToList();

                mesas.Add(new RowTable($"F1.{i + 1}", i == 0 ? 0 : Vao, ViableElevations.Compute(new TableSamples([], pontaBaixa), Comprimento, config, passo)));
            }

            var dp = RowSolver.Solve(mesas, config);
            var custoDp = Custo(dp.Runs[0], mesas);

            var custoBruto = ForcaBruta(mesas, config, passo);

            Assert.Equal(custoBruto, custoDp, 6);
        }
    }

    /// <summary>O custo de uma solução, pela mesma régua do solver: marca ≫ estouro ≫ degrau ≫ giro.</summary>
    private static double Custo(SolvedRun trecho, List<RowTable> mesas)
    {
        var marca = 1e5 * (mesas.Sum(m => m.Viable.ModuleCount) + 1);
        var custo = 0.0;

        for (var i = 0; i < trecho.Tables.Count; i++)
        {
            var t = trecho.Tables[i];
            var estouros = mesas[i].Viable.Violations(t.StartElevation, t.EndElevation);
            custo += (t.Marked ? marca : 0) + estouros * 1e5 + Math.Abs(t.EndElevation - t.StartElevation) * 1e-3;
        }

        foreach (var d in trecho.Steps) custo += Math.Abs(d);

        return custo;
    }

    /// <summary>Enumera tudo, em duas mesas, com as mesmas opções do solver.</summary>
    private static double ForcaBruta(List<RowTable> mesas, SystemConfiguration config, double passo)
    {
        var marca = 1e5 * (mesas.Sum(m => m.Viable.ModuleCount) + 1);
        var giroChaves = (long)Math.Floor(Math.Tan(config.MaxLongitudinalSlope!.Value) * Comprimento / passo + 1e-9);
        long[] giros = [0, giroChaves / 2, giroChaves, -giroChaves / 2, -giroChaves];

        // As opções de cada mesa: (z0, z1, custo da mesa).
        List<(double Z0, double Z1, double Custo)> Opcoes(ViableElevations v)
        {
            var opcoes = new List<(double, double, double)>();
            var chaves = v.Starts.Select(st => st.Key(passo)).ToList();
            var baixa = chaves.Min() - 60;
            var alta = chaves.Max() + 60;

            for (var k = baixa; k <= alta; k++)
            {
                var z0 = k * passo;

                for (var e = baixa - giroChaves; e <= alta + giroChaves; e++)
                {
                    var z1 = e * passo;
                    var estouros = v.Violations(z0, z1);
                    var giro = Math.Abs(z1 - z0) * 1e-3;

                    if (v.IsViable(z0, z1))
                        opcoes.Add((z0, z1, estouros * 1e5 + giro));
                    else if (giros.Contains(e - k))
                        opcoes.Add((z0, z1, marca + estouros * 1e5 + giro));
                }
            }

            return opcoes;
        }

        var a = Opcoes(mesas[0].Viable);
        var b = Opcoes(mesas[1].Viable);
        var degrauMax = Math.Floor(config.MaxStep / passo + 1e-9) * passo;
        var melhor = double.PositiveInfinity;

        foreach (var (z0a, z1a, ca) in a)
        {
            foreach (var (z0b, z1b, cb) in b)
            {
                var d = Math.Abs(z0b - z1a);
                if (d > degrauMax + 1e-9) continue;

                melhor = Math.Min(melhor, ca + cb + d);
            }
        }

        return melhor;
    }


    /// <summary>
    /// Com degrau mínimo de 0,20 m, um degrau de 0,10 não existe: ou zero
    /// ou pelo menos 0,20. Numa rampa suave, a fileira escolhe.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DegrauMinimoEZeroOuPeloMenosOMinimo()
    {
        var config = Config() with { MinStep = 0.20, MaxStep = 0.50, MaxLongitudinalSlope = 1 * Grau };
        var solucao = RowSolver.Solve(Fileira(8, s => 700 + 0.03 * s, config), config);

        DegrausPermitidos(solucao, config);
        Assert.Contains(solucao.Runs[0].Steps, d => Math.Abs(d) >= 0.20 - 1e-9);
        Assert.Equal(0, solucao.MarkedCount);
    }

    /// <summary>
    /// Vão de 20 m no meio (a área tem um furo): a fileira quebra em dois
    /// trechos, resolvidos separadamente, sem degrau entre eles.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VaoMaiorQueOLimiteQuebraAFileira()
    {
        var mesas = Fileira(6, _ => 700, Config());
        mesas[3] = mesas[3] with { GapBefore = 20 };

        var solucao = RowSolver.Solve(mesas, Config());

        Assert.Equal(2, solucao.Runs.Count);
        Assert.Equal(3, solucao.Runs[0].Tables.Count);
        Assert.Equal(3, solucao.Runs[1].Tables.Count);
        Assert.Equal("F1.4", solucao.Runs[1].Tables[0].Label);
        Assert.Equal(6, solucao.Tables.Count);
    }

    /// <summary>Vão exatamente no limite não quebra.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VaoNoLimiteNaoQuebra()
    {
        var mesas = Fileira(4, _ => 700, Config());
        mesas[2] = mesas[2] with { GapBefore = Config().MaxGapBeforeBreak };

        Assert.Single(RowSolver.Solve(mesas, Config()).Runs);
    }

    /// <summary>
    /// Mesa sem terreno embaixo no meio da fileira: fica marcada, com o
    /// motivo do 5.3, nivelada, e as vizinhas continuam alinhadas entre si
    /// através dela com degraus permitidos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaSemTerrenoFicaMarcadaEAFileiraSegue()
    {
        var mesas = Fileira(5, _ => 700, Config());

        var semTerreno = ViableElevations.Compute(
            new TableSamples([], Enumerable.Range(0, 14).Select(c => new LowEdgeSample(c, Estacao(c), 0, 0, null)).ToList()),
            Comprimento, Config());
        mesas[2] = mesas[2] with { Viable = semTerreno };

        var solucao = RowSolver.Solve(mesas, Config());
        var trecho = Assert.Single(solucao.Runs);

        Assert.True(trecho.Tables[2].Marked);
        Assert.Contains("sem terreno", trecho.Tables[2].Reason!);
        Assert.Equal(1, trecho.MarkedCount);
        DegrausPermitidos(solucao, Config());
    }

    /// <summary>Fileira inteira sem terreno: tudo marcado, e o resultado diz.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraInteiraSemTerrenoFicaTodaMarcada()
    {
        var mesas = Fileira(3, _ => null, Config());
        var solucao = RowSolver.Solve(mesas, Config());

        Assert.Equal(3, solucao.MarkedCount);
        Assert.All(solucao.Tables, t => Assert.Contains("sem terreno", t.Reason!));
        Assert.Contains("3 marcada(s)", solucao.Describe());
    }

    /// <summary>
    /// Marcar vale mais que qualquer estouro, e estouro vale mais que
    /// qualquer degrau: entre uma solução que marca uma mesa e outra que
    /// deixa dois módulos fora em duas mesas (com tolerância), vence a
    /// segunda. Terreno: um calombo de 0,60 m sob um módulo de cada uma de
    /// duas mesas vizinhas, tolerância um.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EstouroToleradoVenceDeMarca()
    {
        var config = Config() with { BumpToleranceModules = 1 };
        var c1 = 1 * (Comprimento + Vao) + Estacao(3);
        var c2 = 2 * (Comprimento + Vao) + Estacao(10);

        var solucao = RowSolver.Solve(
            Fileira(4, s => Math.Abs(s - c1) < 0.01 || Math.Abs(s - c2) < 0.01 ? 700.60 : 700, config), config);

        Assert.Equal(0, solucao.MarkedCount);
        Assert.Equal(2, solucao.Runs[0].ViolationCount);
    }

    /// <summary>
    /// A solução é a mesma toda vez (nada aleatório), e uma fileira de 200
    /// mesas resolve em menos de dois segundos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EDeterministaERapido()
    {
        var mesas = Fileira(200, s => 700 + 0.01 * s + 0.2 * Math.Sin(s / 7), Config());

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var a = RowSolver.Solve(mesas, Config());
        relogio.Stop();

        var b = RowSolver.Solve(mesas, Config());

        Assert.Equal(a.Tables, b.Tables);
        Assert.True(relogio.ElapsedMilliseconds < 2000, $"levou {relogio.ElapsedMilliseconds} ms");
        DegrausPermitidos(a, Config());
    }

    /// <summary>
    /// Toda mesa não marcada da solução é viável pela definição do 5.3, e
    /// toda marcada não é: o solver não inventa cota.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AMarcaDizAVerdadeSobreAViabilidade()
    {
        var sorteio = new Random(55);

        for (var caso = 0; caso < 10; caso++)
        {
            var rampa = (sorteio.NextDouble() - 0.5) * 0.08;
            var config = Config() with { BumpToleranceModules = sorteio.Next(3), MaxStep = 0.3 + sorteio.NextDouble() * 0.4 };

            var mesas = Fileira(12, s => 700 + rampa * s + (sorteio.NextDouble() < 0.05 ? 0.7 : 0) + sorteio.NextDouble() * 0.15, config);
            var solucao = RowSolver.Solve(mesas, config);

            for (var i = 0; i < mesas.Count; i++)
            {
                var resolvida = solucao.Tables[i];
                var viavel = mesas[i].Viable.IsViable(resolvida.StartElevation, resolvida.EndElevation);

                Assert.Equal(!viavel, resolvida.Marked);
                if (!resolvida.Marked)
                    Assert.Equal(mesas[i].Viable.Violations(resolvida.StartElevation, resolvida.EndElevation), resolvida.Violations);
            }

            DegrausPermitidos(solucao, config);
        }
    }

    // ------------------------------------------------------- vão em planta

    [Fact]
    [Trait("Etapa", "5")]
    public void OVaoEntreMesasVemDaPlanta()
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(100, 0, 0), new Point3(100, 50, 0), new Point3(0, 50, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(100, 0, 0) };
        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Left, 6, 0.75, new TableFootprint(20, 4));

        var fileira = layout.Rows[0].Tables;

        Assert.Equal(0.75, RowSolver.GapBetween(fileira[0], fileira[1]), 9);
        Assert.Equal(0.75, RowSolver.GapBetween(fileira[1], fileira[2]), 9);
    }

    // ------------------------------------------------------------- recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraVaziaERecusada()
    {
        Assert.Throws<ArgumentException>(() => RowSolver.Solve([], Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void PassosDeGradeDiferentesSaoRecusados()
    {
        var mesas = Fileira(2, _ => 700, Config());
        var outroPasso = ViableElevations.Compute(
            new TableSamples([], Enumerable.Range(0, 14).Select(c => new LowEdgeSample(c, Estacao(c), 0, 0, 700)).ToList()),
            Comprimento, Config(), step: 0.02);
        mesas[1] = mesas[1] with { Viable = outroPasso };

        Assert.Throws<ArgumentException>(() => RowSolver.Solve(mesas, Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void VaoNegativoERecusado()
    {
        var mesas = Fileira(2, _ => 700, Config());
        mesas[1] = mesas[1] with { GapBefore = -0.1 };

        Assert.Throws<ArgumentException>(() => RowSolver.Solve(mesas, Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ConfiguracaoQuebradaERecusada()
    {
        var mesas = Fileira(2, _ => 700, Config());

        Assert.Throws<InvalidOperationException>(() => RowSolver.Solve(mesas, Config() with { MaxStep = -1 }));
    }
}
