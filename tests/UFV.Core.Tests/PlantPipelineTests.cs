using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A área inteira: todas as fileiras, com os totais que o Renan vai
/// comparar com o PVcase (contagens e potência) e o tempo medido.
/// </summary>
public class PlantPipelineTests
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

    private static ProjectSettings Settings() => ProjectSettings.Default;

    private static PlanLayout Layout(TableGeometry geo, double largura = 200, double fundo = 60)
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(largura, 0, 0), new Point3(largura, fundo, 0), new Point3(0, fundo, 0) };
        // Linha norte-sul na borda oeste, fileiras à direita (leste), azimute
        // padrão (subida para o sul): fileiras leste-oeste desde y = 0.
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, fundo, 0) };

        return RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, Settings().Configuration.Pitch, Settings().Configuration.TableGap,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)), Settings().Configuration.UpslopeAzimuthRadians);
    }

    /// <summary>
    /// Retângulo de 200 × 60 no plano: 10 fileiras (pitch 6, fundo 4,5 m em
    /// planta: [0, 4,5], [6, 10,5], ..., [54, 58,5]) de 10 mesas (200/19,2;
    /// a que passaria da borda não entra). Os totais são contagem × mesa, e
    /// a potência é a soma dos módulos: regra sagrada 3.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OsTotaisSaoContagemVezesMesa()
    {
        var geo = Geometria();
        var layout = Layout(geo);

        var usina = PlantPipeline.ProcessAll(layout, geo, Tilt, 28, 720, Plano((_, _) => 700), Settings());

        Assert.Equal(layout.Rows.Count, usina.Rows.Count);
        Assert.Equal(layout.Tables.Count, usina.Tables.Count);
        Assert.Equal(usina.Tables.Count * 28, usina.ModuleCount);
        Assert.Equal(usina.Tables.Count * 28 * 0.72, usina.PowerKwp, 9);
        Assert.Equal(usina.Tables.Count * 7, usina.PillarCount);
        Assert.Equal(0, usina.MarkedCount);
        Assert.Equal(0, usina.PillarProblemCount);
        Assert.Contains("kWp", usina.Describe());
        Assert.True(usina.Elapsed > TimeSpan.Zero);
    }

    /// <summary>
    /// O progresso é chamado uma vez por fileira, na ordem, com o total.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OProgressoConta()
    {
        var geo = Geometria();
        var layout = Layout(geo, 100, 30);
        var chamadas = new List<(int, int)>();

        PlantPipeline.ProcessAll(layout, geo, Tilt, 28, 720, Plano((_, _) => 700), Settings(),
            (feitas, total) => chamadas.Add((feitas, total)));

        Assert.Equal(layout.Rows.Count, chamadas.Count);
        Assert.Equal(Enumerable.Range(1, layout.Rows.Count), chamadas.Select(c => c.Item1));
        Assert.All(chamadas, c => Assert.Equal(layout.Rows.Count, c.Item2));
    }

    /// <summary>
    /// Terreno de verdade em escala: 500 × 300 m (50 fileiras de 26 mesas,
    /// 1300 mesas, 36 mil módulos) com relevo ondulado, em menos de 30 s.
    /// (A décima primeira mesa de cada fileira passava da borda; não entra.)
    /// É a medição que o plano pede no 5.9, do lado do motor.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void UmaUsinaDeMilMesasProcessaEmSegundos()
    {
        var geo = Geometria();
        var layout = Layout(geo, 500, 300);
        var terreno = Plano((x, y) => 700 + 0.03 * x + 0.02 * y + 0.4 * Math.Sin(x / 23) * Math.Cos(y / 31));

        var usina = PlantPipeline.ProcessAll(layout, geo, Tilt, 28, 720, terreno, Settings());

        Assert.True(usina.Tables.Count > 1000, $"só {usina.Tables.Count} mesas");
        Assert.True(usina.Elapsed < TimeSpan.FromSeconds(30), $"levou {usina.Elapsed.TotalSeconds:0.0} s");
        Assert.True(usina.MarkedCount < usina.Tables.Count / 2, $"{usina.MarkedCount} marcadas de {usina.Tables.Count}");
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void EntradasImpossiveisSaoRecusadas()
    {
        var geo = Geometria();
        var layout = Layout(geo, 100, 30);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlantPipeline.ProcessAll(layout, geo, Tilt, 0, 720, Plano((_, _) => 700), Settings()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlantPipeline.ProcessAll(layout, geo, Tilt, 28, double.NaN, Plano((_, _) => 700), Settings()));
    }
}
