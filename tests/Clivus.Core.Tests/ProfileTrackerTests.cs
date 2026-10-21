namespace Clivus.Core.Tests;

/// <summary>Passo 32.2: o rastreio planta ↔ gráfico, e o gráfico da linha de análise (32.1).</summary>
public class ProfileTrackerTests
{
    /// <summary>Uma linha de 100 m para o leste, de (1000, 2000), subindo de 50 a 55 m; o gráfico em (1200, 1900).</summary>
    private static TrackedChart Grafico()
    {
        var caminho = Enumerable.Range(0, 11).Select(i => new TrackPoint(10 * i, 1000 + 10 * i, 2000, 50 + 0.5 * i, double.NaN)).ToList();
        var chart = ProfileChart.Build(new ChartInput
        {
            Title = "Linha 1",
            Terrain = caminho.Select(p => (p.Distance, p.Terrain)).ToList(),
            Segments = ProfileSegments.Split(d => 50 + 0.05 * d, 100, 20),
            VerticalExaggeration = 5,
            RunAndRise = true,
        });
        return new TrackedChart(Guid.NewGuid(), "Linha 1", (1200, 1900), chart, caminho);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void MouseNoGraficoAndaNaPlanta()
    {
        var g = Grafico();
        var hit = ProfileTracker.FromChart(g, 1200 + 37, 1900 + 5);

        Assert.NotNull(hit);
        Assert.Equal(37, hit.Value.Distance, 9);
        Assert.Equal(1037, hit.Value.PlanX, 9);
        Assert.Equal(2000, hit.Value.PlanY, 9);
        Assert.Equal(51.85, hit.Value.Terrain, 9);

        // Fora do retângulo do gráfico, nada.
        Assert.Null(ProfileTracker.FromChart(g, 900, 1900));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void MouseNaLinhaAndaNoGrafico()
    {
        var g = Grafico();
        var hit = ProfileTracker.FromPlan(g, 1063, 2000.4, 1.0);

        Assert.NotNull(hit);
        Assert.Equal(63, hit.Value.Distance, 9);
        Assert.Equal(1263, hit.Value.ChartX, 9);
        Assert.Equal(1900 + g.Chart.YOf(53.15), hit.Value.ChartY, 9);

        // Longe da linha (mais que a tolerância), nada.
        Assert.Null(ProfileTracker.FromPlan(g, 1063, 2003, 1.0));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void GraficoDaLinhaDizQuantoCorreEQuantoSobeComAEB()
    {
        var g = Grafico().Chart;

        // Trechos de 20 m subindo 1 m: "corre 20,0 m · sobe 1,00 m" e "5,0% | 2,9°".
        Assert.Equal(5, g.Texts.Count(t => t.Text.StartsWith("corre 20,0 m · sobe 1,00 m", StringComparison.Ordinal)));
        Assert.Contains(g.Texts, t => t.Text == "A");
        Assert.Contains(g.Texts, t => t.Text == "B");
        Assert.Contains(g.Texts, t => t.Text == "Cota (m)");
        Assert.Contains(g.Texts, t => t.Text.StartsWith("De A a B: corre 100,0 m e sobe 5,00 m", StringComparison.Ordinal));

        // O y do gráfico volta à cota: o rastreio usa isto.
        Assert.Equal(0, g.YOf(g.ZBase), 9);
    }
}
