using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A bifila numa usina mista (passo 38.3): cada mesa com o enterro T3 do
/// tipo dela. Antes, a usina usava o T3 do primeiro tipo marcado para todas, e
/// uma bifila de 2,20 m de enterro ao lado de uma monoposte de 1,50 saía com o
/// pilar errado em um dos dois.
/// </summary>
public class BifilaPlantTests
{
    private const double Tilt = 10 * Math.PI / 180;

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));
        return new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);
    }

    /// <summary>A monoposte de 14 módulos (9,4 m): curta, ela entra na sobra da fileira de bifilas.</summary>
    private static TableGeometry Monoposte(double? t3)
    {
        var mesa = new TableLayout(new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033), 14, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);
        var estrutura = new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0) { MinEmbedment = t3 };
        return TableGeometry.Local(mesa, estrutura.Pillars(mesa), estrutura);
    }

    private static TableGeometry Bifila()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        return TableGeometry.Local(p.Layout, p.Frame.Pillars(p.Layout), p.Frame);
    }

    private static ProcessedPlant Processar(TableGeometry[] geos)
    {
        var config = ProjectSettings.Default.Configuration;
        var area = new[] { new Point3(0, 0, 0), new Point3(90, 0, 0), new Point3(90, 40, 0), new Point3(0, 40, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 40, 0) };
        var pegadas = geos.Select(g => new TableFootprint(g.Length, g.Depth * Math.Cos(Tilt))).ToList();

        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, pegadas, [28, 14], config.UpslopeAzimuthRadians);

        // PB alta o bastante para a mão francesa da GC800 (a bifila pede 0,832).
        var settings = ProjectSettings.Default with { Configuration = config with { MinLowEdge = 0.85, MaxLowEdge = 1.5 } };

        return PlantPipeline.ProcessAll(layout, geos, Tilt, [28, 14], [720, 720], Plano((_, _) => 700), settings);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void CadaTipoUsaOEnterroDaSuaEstrutura()
    {
        var usina = Processar([Bifila(), Monoposte(1.5)]);

        var porTipo = usina.Rows.SelectMany(r => r.Tables).GroupBy(t => t.Cell.Kind).ToDictionary(g => g.Key, g => g.ToList());
        Assert.True(porTipo.ContainsKey(0) && porTipo.ContainsKey(1), "a usina precisa ter os dois tipos para o teste valer");

        Assert.All(porTipo[0].SelectMany(t => t.Pillars.Pillars), p => Assert.Equal(2.2, p.Embedment, 12));
        Assert.All(porTipo[1].SelectMany(t => t.Pillars.Pillars), p => Assert.Equal(1.5, p.Embedment, 12));
    }

    /// <summary>
    /// A mesa avulsa do motor (pontas à mão, Recalcular de uma mesa) também usa
    /// o T3 da estrutura dela: antes, a mesma bifila saía com 2,20 de enterro na
    /// Usina e com o da configuração nas Pontas (revisão do 38.3).
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void AMesaAvulsaUsaOT3DaEstruturaDela()
    {
        var geo = Bifila();
        var usina = Processar([geo, Monoposte(1.5)]);
        var mesa = usina.Rows.SelectMany(r => r.Tables).First(t => t.Cell.Kind == 0);

        var settings = ProjectSettings.Default with { Configuration = ProjectSettings.Default.Configuration with { MinLowEdge = 0.85, MaxLowEdge = 1.5 } };
        var avulsa = RowPipeline.ProcessFixed(
            mesa.Cell, geo, Tilt, Plano((_, _) => 700), settings, mesa.Solved.StartElevation, mesa.Solved.EndElevation, null).Tables[0];

        Assert.All(avulsa.Pillars.Pillars, p => Assert.Equal(2.2, p.Embedment, 12));
        Assert.Equal(mesa.Pillars.Pillars.Select(p => p.Length), avulsa.Pillars.Pillars.Select(p => p.Length));
    }

    /// <summary>A regra do T3 num lugar só: com T3 o enterro é ele e o máximo sobe junto; sem T3, a mesma configuração.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void ARegraDoT3EUmaSo()
    {
        var config = ProjectSettings.Default.Configuration;
        var comT3 = config.WithEmbedmentOf(new TableFrame(3, 2.5, 0.15, 0.07, 3, 0) { MinEmbedment = 2.2 });

        Assert.Equal(2.2, comT3.MinEmbedment, 12);
        Assert.Equal(Math.Max(config.MaxEmbedment, 2.2), comT3.MaxEmbedment, 12);
        Assert.Same(config, config.WithEmbedmentOf(new TableFrame(3, 2.5, 0.15, 0.07, 3, 0)));
        Assert.Same(config, config.WithEmbedmentOf(null));
        Assert.Equal(comT3, ProjectSettings.Default.ForTable(new TableFrame(3, 2.5, 0.15, 0.07, 3, 0) { MinEmbedment = 2.2 }).Configuration);
    }

    /// <summary>Tipo sem T3 fica com o enterro da configuração da usina, como sempre ficou.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void TipoSemT3FicaComOEnterroDaConfiguracao()
    {
        var usina = Processar([Bifila(), Monoposte(null)]);

        var mono = usina.Rows.SelectMany(r => r.Tables).Where(t => t.Cell.Kind == 1).SelectMany(t => t.Pillars.Pillars).ToList();
        Assert.NotEmpty(mono);
        Assert.All(mono, p => Assert.Equal(ProjectSettings.Default.Configuration.MinEmbedment, p.Embedment, 12));
    }
}
