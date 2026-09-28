using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// As alturas das pontas escolhidas à mão (27/09/2026): o projetista diz a
/// altura livre da ponta baixa no primeiro e no último pilar, e a mesa é
/// refeita com elas. A ponta sem valor fica travada.
/// </summary>
public class ManualEndsTests
{
    private const double Grau = Math.PI / 180;
    private const double Tilt = 20 * Grau;

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));

        return new Tin(
        [
            new Triangle(P(-100, -100), P(400, -100), P(400, 400)),
            new Triangle(P(-100, -100), P(400, 400), P(-100, 400)),
        ]);
    }

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static PlacedTable Celula(TableGeometry geo)
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 60, 0), new Point3(0, 60, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 60, 0) };
        var config = ProjectSettings.Default.Configuration;

        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, config.Pitch, config.TableGap,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)), config.UpslopeAzimuthRadians);

        return layout.Rows[0].Tables[1];
    }

    private static (ProcessedTable Mesa, ManualEndsResult Resultado) Ajustar(Tin terreno, double? primeira, double? ultima)
    {
        var geo = Geometria();
        var celula = Celula(geo);
        var atual = RowPipeline.ProcessRow(new PlanRow(1, [celula]), geo, Tilt, terreno, ProjectSettings.Default).Tables[0];

        var resultado = ManualEnds.Apply(
            celula, geo, Tilt, terreno, ProjectSettings.Default,
            atual.Solved.StartElevation, atual.Solved.EndElevation, primeira, ultima);

        return (atual, resultado);
    }

    /// <summary>No plano, as duas alturas pedidas saem ao milímetro, e a mesa segue rígida (cota linear).</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void NoPlanoAsDuasAlturasSaemAoMilimetro()
    {
        var (_, r) = Ajustar(Plano((_, _) => 700), 0.50, 1.20);
        var pilares = r.Row.Tables[0].Pillars.Pillars;

        Assert.Equal(0.50, pilares[0].LowEdgeClearance!.Value, 3);
        Assert.Equal(1.20, pilares[^1].LowEdgeClearance!.Value, 3);
        Assert.Equal(0.50, r.FirstLowEdge, 3);
        Assert.Equal(1.20, r.LastLowEdge, 3);
        // 0,50 → 1,20 passa da faixa (0,30–0,80) no fim: marcada pela regra 4.
        Assert.True(r.Row.Tables[0].Solved.Marked);
        Assert.StartsWith(ManualEnds.Note, r.Row.Tables[0].Solved.Reason!, StringComparison.Ordinal);

        // Rígida: as alturas dos pilares do meio caem na reta entre as pontas.
        var s0 = pilares[0].Station;
        var s1 = pilares[^1].Station;

        foreach (var p in pilares)
            Assert.Equal(0.50 + (1.20 - 0.50) * (p.Station - s0) / (s1 - s0), p.LowEdgeClearance!.Value, 2);
    }

    /// <summary>A ponta sem valor fica travada onde estava ("tem uma ponta que está boa").</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void APontaSemValorFicaTravada()
    {
        var terreno = Plano((x, y) => 700 + 0.03 * x + 0.05 * y);
        var (atual, r) = Ajustar(terreno, null, 0.95);
        var pilares = r.Row.Tables[0].Pillars.Pillars;

        Assert.Equal(atual.Pillars.Pillars[0].LowEdgeClearance!.Value, pilares[0].LowEdgeClearance!.Value, 3);
        Assert.Equal(0.95, pilares[^1].LowEdgeClearance!.Value, 3);
    }

    /// <summary>Terreno inclinado nas duas direções: a conta refeita converge ao milímetro, e a cota vem do terreno.</summary>
    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(0.30, 0.30)]
    [InlineData(0.80, 0.35)]
    [InlineData(1.50, 0.40)]
    public void NaRampaConvergeAoMilimetro(double primeira, double ultima)
    {
        var terreno = Plano((x, y) => 700 + 0.12 * x - 0.04 * y);
        var (_, r) = Ajustar(terreno, primeira, ultima);
        var pilares = r.Row.Tables[0].Pillars.Pillars;

        Assert.Equal(primeira, pilares[0].LowEdgeClearance!.Value, 3);
        Assert.Equal(ultima, pilares[^1].LowEdgeClearance!.Value, 3);
        Assert.All(pilares, p => Assert.True(p.GroundZ is not null));
    }

    /// <summary>O que foge da regra é dito, e não impedido: giro acima do limite e ponta enterrada.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void OQueFogeDaRegraEDito()
    {
        var (_, ingreme) = Ajustar(Plano((_, _) => 700), 0.30, 4.50);
        Assert.Contains(ingreme.Warnings, a => a.Contains("acima do limite", StringComparison.Ordinal));
        Assert.Contains(ingreme.Warnings, a => a.Contains("fora da faixa", StringComparison.Ordinal));

        // Regra sagrada 4: passou do lombo (zero), a mesa é marcada, mesmo
        // com as cotas escolhidas à mão; o motivo diz quem escolheu e quanto.
        Assert.True(ingreme.Row.Tables[0].Solved.Marked);
        Assert.Contains(ManualEnds.Note, ingreme.Row.Tables[0].Solved.Reason!, StringComparison.Ordinal);
        Assert.Contains("fora da faixa", ingreme.Row.Tables[0].Solved.Reason!, StringComparison.Ordinal);

        var (_, enterrada) = Ajustar(Plano((_, _) => 700), -0.20, 0.50);
        Assert.Contains(enterrada.Warnings, a => a.Contains("ENTERRADA", StringComparison.Ordinal));
    }

    /// <summary>Dentro da faixa nas duas pontas, a mesa não é marcada e carrega só a nota.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void DentroDaFaixaNaoMarca()
    {
        var (_, r) = Ajustar(Plano((_, _) => 700), 0.40, 0.70);

        Assert.False(r.Row.Tables[0].Solved.Marked);
        Assert.Equal(ManualEnds.Note, r.Row.Tables[0].Solved.Reason);
        Assert.Empty(r.Warnings);
    }

    /// <summary>Altura que não é número não passa.</summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void AlturaQueNaoENumeroERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ajustar(Plano((_, _) => 700), double.NaN, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ajustar(Plano((_, _) => 700), 0.5, 99));
    }
}
