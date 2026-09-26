using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// O resultado das análises de uma mesa: cada módulo e cada pilar com o
/// seu valor e o veredito das regras de análise. Nada de geometria aqui —
/// o que se trava é que o valor certo chega à regra certa, e que peça sem
/// terreno sai Off, nunca "dentro".
/// </summary>
public class TableAnalysisTests
{
    private const double Grau = Math.PI / 180;
    private const double Tilt = 20 * Grau;

    private static Tin Plano(double cota)
    {
        Point3 P(double x, double y) => new(x, y, cota);

        return new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);
    }

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    private static AnalysisRules Regras() => AnalysisRules.Default;

    /// <summary>Tudo que o relatório precisa, para uma mesa nivelada numa cota, sobre um plano.</summary>
    private static TableReport Relatorio(
        double z0, double z1, double terreno = 700, AnalysisRules? regras = null, SystemConfiguration? config = null,
        bool naBorda = false, bool marcada = false) =>
        Relatorio(out _, z0, z1, terreno, regras, config, naBorda, marcada);

    private static TableReport Relatorio(
        out TableSamples amostras,
        double z0, double z1, double terreno = 700, AnalysisRules? regras = null, SystemConfiguration? config = null,
        bool naBorda = false, bool marcada = false, Tin? tin = null)
    {
        var geo = Geometria();
        var cfg = config ?? Config();
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };

        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, 8, 0.5, new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)), cfg.UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[2] with { PartlyOutside = naBorda };
        var orientacao = RowOrientation.Resolve(cell, cfg.UpslopeAzimuthRadians);

        tin ??= Plano(terreno);
        amostras = TerrainSampler.Sample(geo, TablePlacement.Plan(cell, orientacao, Tilt, 0), tin);
        var resolvida = new SolvedTable("F2.3", z0, z1, 0, marcada, marcada ? "motivo de teste" : null);
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, resolvida, tin, cfg);

        return TableAnalysis.Evaluate(resolvida, amostras, geo.Length, pilares, cell, regras ?? Regras(), cfg);
    }

    // ------------------------------------------------------- ponta baixa

    /// <summary>Mesa em 700,50 sobre 700: toda ponta baixa a 0,50, dentro da faixa, nada pintado.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DentroDaFaixaNadaEPintado()
    {
        var relatorio = Relatorio(700.50, 700.50);

        Assert.Equal(14, relatorio.Modules.Count);
        Assert.All(relatorio.Modules, m => Assert.Equal(0.50, m.Clearance!.Value, 9));
        Assert.All(relatorio.Modules, m => Assert.Equal(AnalysisOutcome.Inside, m.Verdict.Outcome));
        Assert.Equal(0, relatorio.ModulesOutsideBand);
        Assert.Empty(relatorio.Painted);
        Assert.False(relatorio.Marked);
    }

    /// <summary>Mesa em 700,25: toda ponta baixa a 0,25, abaixo do mínimo, vermelha na camada da análise.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AbaixoDoMinimoPintaDeVermelho()
    {
        var relatorio = Relatorio(700.25, 700.25);

        Assert.All(relatorio.Modules, m =>
        {
            Assert.Equal(AnalysisOutcome.Below, m.Verdict.Outcome);
            Assert.Equal(RgbColor.Red, m.Verdict.Color);
            Assert.Equal(Regras().LowEdge.Layer, m.Verdict.Layer);
        });
        Assert.Equal(14, relatorio.ModulesOutsideBand);
        Assert.Equal(14, relatorio.PaintedByKind[AnalysisKind.LowEdge]);
    }

    /// <summary>
    /// Mesa girada: 700,20 no início e 701,00 no fim. A ponta baixa sobe
    /// pela mesa, e as colunas do começo ficam abaixo de 0,30 enquanto as do
    /// fim ficam acima de 0,80: vermelho de um lado, azul do outro, e o
    /// valor de cada módulo é o da estação dele.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaGiradaTemVermelhoNumLadoEAzulNoOutro()
    {
        var relatorio = Relatorio(700.20, 701.00);
        var comprimento = Geometria().Length;

        foreach (var m in relatorio.Modules)
        {
            var esperada = 700.20 + 0.80 * m.Station / comprimento - 700;
            Assert.Equal(esperada, m.Clearance!.Value, 9);
        }

        Assert.Equal(AnalysisOutcome.Below, relatorio.Modules[0].Verdict.Outcome);
        Assert.Equal(AnalysisOutcome.Above, relatorio.Modules[^1].Verdict.Outcome);
        Assert.Equal(RgbColor.Blue, relatorio.Modules[^1].Verdict.Color);
        Assert.Contains(relatorio.Modules, m => m.Verdict.Outcome == AnalysisOutcome.Inside);

        // Fora da faixa conta os dois lados.
        var abaixo = relatorio.Modules.Count(m => m.Verdict.Outcome == AnalysisOutcome.Below);
        var acima = relatorio.Modules.Count(m => m.Verdict.Outcome == AnalysisOutcome.Above);
        Assert.True(abaixo > 0 && acima > 0);
        Assert.Equal(abaixo + acima, relatorio.ModulesOutsideBand);
    }

    /// <summary>A estação e a coluna de cada módulo são as da amostra, na ordem.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AEstacaoEAColunaVemDaAmostra()
    {
        var relatorio = Relatorio(out var amostras, 700.50, 700.50);

        for (var i = 0; i < relatorio.Modules.Count; i++)
        {
            Assert.Equal(i, relatorio.Modules[i].Column);
            Assert.Equal(amostras.LowEdge[i].Station, relatorio.Modules[i].Station, 12);
        }
    }

    /// <summary>
    /// Módulo sem terreno embaixo: altura livre nula, veredito Off, nada
    /// pintado. O terreno acaba em x = 100 e a mesa, olhando para o norte,
    /// cresce para oeste a partir da célula; com o terreno só a leste de
    /// x = 100... a célula está em x ≈ 38 a 57, então o terreno é recortado
    /// para acabar em x = 55 (a mesa F2.3 vai de x ≈ 48 a 67): os módulos a oeste ficam sem terreno.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ModuloSemTerrenoSaiOffENaoDentro()
    {
        Point3 P(double x, double y) => new(x, y, 700);
        var recortado = new Tin(
        [
            new Triangle(P(55, -100), P(300, -100), P(300, 300)),
            new Triangle(P(55, -100), P(300, 300), P(55, 300)),
        ]);

        var relatorio = Relatorio(out _, 700.50, 700.50, tin: recortado);

        var semTerreno = relatorio.Modules.Where(m => m.Clearance is null).ToList();
        var comTerreno = relatorio.Modules.Where(m => m.Clearance is not null).ToList();

        Assert.NotEmpty(semTerreno);
        Assert.NotEmpty(comTerreno);
        Assert.All(semTerreno, m => Assert.Equal(AnalysisOutcome.Off, m.Verdict.Outcome));
        Assert.All(comTerreno, m => Assert.Equal(AnalysisOutcome.Inside, m.Verdict.Outcome));
        Assert.DoesNotContain(relatorio.Painted, v => v.Kind == AnalysisKind.LowEdge);
    }

    // ------------------------------------------------------- pilares

    /// <summary>
    /// Sem limite de pilar configurado, a análise de comprimento fica Off; o
    /// enterro é o mínimo, dentro da faixa. Com o limite em 1,50 m, os
    /// pilares (0,50 + 1,16 + 0,90 = 2,56 m) passam e ficam azuis.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OComprimentoDoPilarSoEPintadoComLimite()
    {
        var semLimite = Relatorio(700.50, 700.50);

        Assert.All(semLimite.Pillars, p => Assert.Equal(AnalysisOutcome.Off, p.LengthVerdict.Outcome));
        Assert.All(semLimite.Pillars, p => Assert.Equal(AnalysisOutcome.Inside, p.EmbedmentVerdict.Outcome));

        var comLimite = Relatorio(700.50, 700.50, regras: Regras() with { PaintPillarsLongerThan = 1.50 });
        var esperado = 0.50 + Geometria().PillarRow * Math.Sin(Tilt) + 0.90;

        Assert.All(comLimite.Pillars, p =>
        {
            Assert.Equal(esperado, p.Pillar.Length!.Value, 6);
            Assert.Equal(AnalysisOutcome.Above, p.LengthVerdict.Outcome);
            Assert.Equal(RgbColor.Blue, p.LengthVerdict.Color);
            Assert.Equal(p.LengthVerdict, p.PaintVerdict);
        });
        Assert.Equal(comLimite.Pillars.Count, comLimite.PaintedByKind[AnalysisKind.PillarLength]);
    }

    /// <summary>
    /// Com o comprimento ideal, o enterro é o mínimo por construção: a
    /// análise de enterro é Inside quando há comprimento e Off quando não,
    /// nunca pinta. Em cotas aleatórias, inclusive mesas abaixo do chão.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEnterroNuncaPintaComOComprimentoIdeal()
    {
        var sorteio = new Random(58);

        for (var caso = 0; caso < 20; caso++)
        {
            var z0 = 698 + sorteio.NextDouble() * 4;
            var z1 = z0 + (sorteio.NextDouble() - 0.5) * 2;
            var relatorio = Relatorio(z0, z1);

            Assert.All(relatorio.Pillars, p =>
            {
                Assert.True(p.EmbedmentVerdict.Outcome is AnalysisOutcome.Inside or AnalysisOutcome.Off);
                Assert.Null(p.EmbedmentVerdict.Color);
            });
        }
    }

    /// <summary>
    /// Tudo pintado ao mesmo tempo: ponta baixa abaixo, pilar comprido,
    /// declividade acima do limite e borda. Uma peça, um veredito: 14 módulos
    /// + 7 pilares + 1 declividade + 1 borda.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void TudoPintadoContaUmaVezPorPeca()
    {
        var relatorio = Relatorio(
            700.25, 701.25,
            regras: Regras() with { PaintPillarsLongerThan = 1.0 },
            config: Config() with { MaxLongitudinalSlope = 2 * Grau },
            naBorda: true);

        var pilares = relatorio.Pillars.Count;

        // Nem todo módulo está fora: a mesa gira de 0,25 a 1,25 de folga, e os
        // do meio ficam dentro. Uma peça, um veredito.
        Assert.True(relatorio.ModulesOutsideBand > 0 && relatorio.ModulesOutsideBand < 14);
        Assert.Equal(relatorio.ModulesOutsideBand + pilares + 1 + 1, relatorio.Painted.Count());
        Assert.Equal(1, relatorio.PaintedByKind[AnalysisKind.LongitudinalSlope]);
        Assert.Equal(1, relatorio.PaintedByKind[AnalysisKind.EdgeTable]);
        Assert.Equal(pilares, relatorio.PaintedByKind[AnalysisKind.PillarLength]);
        Assert.Contains("ponta(s) baixa(s) fora", relatorio.Describe());
        Assert.Contains("pilar(es) compridos", relatorio.Describe());
    }

    /// <summary>Pilar sem comprimento (mesa abaixo do chão) fica Off nas duas análises, e o problema vem junto.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PilarSemComprimentoFicaOffEComOProblema()
    {
        var relatorio = Relatorio(698.00, 698.00, regras: Regras() with { PaintPillarsLongerThan = 1.0 });

        Assert.All(relatorio.Pillars, p =>
        {
            Assert.NotNull(p.Pillar.Problem);
            Assert.Equal(AnalysisOutcome.Off, p.LengthVerdict.Outcome);
            Assert.Equal(AnalysisOutcome.Off, p.EmbedmentVerdict.Outcome);
        });
    }

    // ------------------------------------------------------- declividade e borda

    /// <summary>Mesa girada 3 m em 18,7 (asen: 9,2°): dentro dos 10°, nada. Com o limite em 5°, pinta e o Describe diz os graus.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ADeclividadeEPintadaQuandoPassaDoLimite()
    {
        var dentro = Relatorio(700.50, 703.50);

        Assert.Equal(Math.Asin(3.0 / Geometria().Length), dentro.LongitudinalSlopeRadians, 12);
        Assert.Equal(AnalysisOutcome.Inside, dentro.SlopeVerdict.Outcome);

        var fora = Relatorio(700.50, 703.50, config: Config() with { MaxLongitudinalSlope = 5 * Grau });

        Assert.Equal(AnalysisOutcome.Above, fora.SlopeVerdict.Outcome);
        Assert.Contains("declividade de 9,2°", fora.Describe());
        Assert.Equal(1, fora.PaintedByKind[AnalysisKind.LongitudinalSlope]);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void AMesaNaBordaEPintadaComACorDaBorda()
    {
        var relatorio = Relatorio(700.50, 700.50, naBorda: true);

        Assert.Equal(AnalysisOutcome.Above, relatorio.EdgeVerdict.Outcome);
        Assert.Equal(Regras().EdgeRule.Color, relatorio.EdgeVerdict.Color);
        Assert.Contains("na borda", relatorio.Describe());
        Assert.Equal(1, relatorio.PaintedByKind[AnalysisKind.EdgeTable]);
    }

    // ------------------------------------------------------- ligar e desligar

    /// <summary>Análise desligada devolve Off em toda peça, mesmo com valor fora.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AnaliseDesligadaNaoPinta()
    {
        var regras = Regras().With(AnalysisKind.LowEdge, Regras().LowEdge with { Enabled = false });
        var relatorio = Relatorio(700.25, 700.25, regras: regras);

        Assert.All(relatorio.Modules, m => Assert.Equal(AnalysisOutcome.Off, m.Verdict.Outcome));
        Assert.All(relatorio.Modules, m => Assert.Equal(0.25, m.Clearance!.Value, 9));
        Assert.Empty(relatorio.Painted);
    }

    /// <summary>A marca da fileira passa pelo relatório com o motivo.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AMarcaDaFileiraPassaPeloRelatorio()
    {
        var relatorio = Relatorio(700.50, 700.50, marcada: true);

        Assert.True(relatorio.Marked);
        Assert.Equal("motivo de teste", relatorio.MarkedReason);
        Assert.Contains("MARCADA", relatorio.Describe());
    }

    // ------------------------------------------------------- recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void RegrasOuConfiguracaoQuebradasSaoRecusadas()
    {
        Assert.Throws<InvalidOperationException>(() => Relatorio(700.5, 700.5, regras: Regras() with { PaintPillarsLongerThan = -1 }));

        // A configuração quebrada chega direto ao Evaluate, com pilares
        // calculados por uma configuração boa: é a guarda do próprio
        // relatório que tem que recusar.
        var geo = Geometria();
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var layout = RowDistributor.Distribute(area, [new Point3(10, 10, 0), new Point3(10, 190, 0)], LineSide.Right, 8, 0.5, new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)), Config().UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[2];
        var orientacao = RowOrientation.Resolve(cell, Config().UpslopeAzimuthRadians);
        var amostras = TerrainSampler.Sample(geo, TablePlacement.Plan(cell, orientacao, Tilt, 0), Plano(700));
        var resolvida = new SolvedTable("F2.3", 700.5, 700.5, 0, false, null);
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, resolvida, Plano(700), Config());

        Assert.Throws<InvalidOperationException>(() =>
            TableAnalysis.Evaluate(resolvida, amostras, geo.Length, pilares, cell, Regras(), Config() with { MinLowEdge = 9 }));

        // E amostra de outra mesa (estação além do comprimento) é recusada.
        var estranha = new TableSamples([], [new LowEdgeSample(0, geo.Length + 1, 0, 0, 700)]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TableAnalysis.Evaluate(resolvida, estranha, geo.Length, pilares, cell, Regras(), Config()));
    }
}
