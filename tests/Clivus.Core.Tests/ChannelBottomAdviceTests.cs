using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Os trechos ruins do fundo explicados (pedido do Renan de 21/10/2026: "tem
/// como especificar melhor, tipo, quanto tem que afundar o solo, ou se vai
/// transbordar?"). Parede de 0,35 m e mínima de 0,5 % em todos.
/// </summary>
public class ChannelBottomAdviceTests
{
    private const double Parede = 0.35;

    private static ChannelProfileResult Canaleta(params (double D, double Z)[] terreno) =>
        ChannelProfile.Build(new TerrainProfile(terreno.Select(p => new ProfilePoint(p.D, p.D, 0, p.Z, true)).ToList(), false, 0),
            Parede, ChannelBottomMode.FollowTerrain, 0.005, 20);

    [Fact]
    [Trait("Etapa", "32")]
    public void BarrigaMaisFundaQueAParedeTransbordaEDizQuantoCavar()
    {
        // Desce 1 m até a 20, sobe 0,5 m até a 40 e desce de novo.
        var c = Assert.Single(ChannelBottomAdvice.Of(Canaleta((0, 100), (20, 99), (40, 99.5), (60, 99)), Parede));

        Assert.Equal(BottomProblem.Overflow, c.Problem);
        Assert.Equal(0.5, c.PondDepth, 9);
        // O fundo na mínima desde a ponta de cima passaria em 98,55 na 40; ele está em 99,15.
        Assert.Equal(0.6, c.Cut, 9);
        Assert.Contains("transborda", c.What(0.005, Parede));
        Assert.Equal("aprofundar 0,60 m", c.Fix);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void BarrigaRasaEncheEPassa()
    {
        var c = Assert.Single(ChannelBottomAdvice.Of(Canaleta((0, 100), (20, 99), (40, 99.2), (60, 99)), Parede));

        Assert.Equal(BottomProblem.Pond, c.Problem);
        Assert.Equal(0.2, c.PondDepth, 9);
        Assert.Equal(0.3, c.Cut, 9);
        Assert.Equal("barriga de 0,20 m: enche e passa", c.What(0.005, Parede));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void SubindoDesdeAPontaDeCimaAAguaVolta()
    {
        var c = Assert.Single(ChannelBottomAdvice.Of(Canaleta((0, 100), (20, 100.5), (40, 99)), Parede));

        Assert.Equal(BottomProblem.Backward, c.Problem);
        Assert.Equal(0, c.PondDepth);
        Assert.Equal(0.6, c.Cut, 9);
        Assert.Equal("sobe: a água volta", c.What(0.005, Parede));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void PlanoDeixaAAguaParada()
    {
        var c = Assert.Single(ChannelBottomAdvice.Of(Canaleta((0, 100), (20, 99.6), (40, 99.6), (60, 99)), Parede));

        Assert.Equal(BottomProblem.Flat, c.Problem);
        Assert.Equal(0.1, c.Cut, 9);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void AbaixoDaMinimaEAguaLenta()
    {
        var c = Assert.Single(ChannelBottomAdvice.Of(Canaleta((0, 100), (20, 99.98), (40, 99.5)), Parede));

        Assert.Equal(BottomProblem.Slow, c.Problem);
        Assert.Equal(0.08, c.Cut, 9);
        Assert.Equal("0,1% < 0,5%: água lenta", c.What(0.005, Parede));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void OGraficoEscreveANotaEmbaixoDoTrecho()
    {
        var canaleta = Canaleta((0, 100), (20, 99), (40, 99.5), (60, 99));
        var c = Assert.Single(ChannelBottomAdvice.Of(canaleta, Parede));

        var grafico = ProfileChart.Build(new ChartInput
        {
            Terrain = canaleta.Points.Select(p => (p.Distance, p.Terrain)).ToList(),
            Bottom = canaleta.Points.Select(p => (p.Distance, p.Bottom)).ToList(),
            WallHeight = Parede,
            Segments = canaleta.Segments,
            Alerts = canaleta.InvertedSegments,
            Notes = [new ChartNote(c.Run.From, c.Run.To, [c.What(0.005, Parede), c.Fix])],
        });

        var conserto = Assert.Single(grafico.Texts, t => t.Text == "aprofundar 0,60 m");
        var oQue = Assert.Single(grafico.Texts, t => t.Text.Contains("transborda"));
        Assert.Equal(30, conserto.X, 9);
        Assert.True(oQue.Y > conserto.Y, "o que acontece vem em cima do conserto");
        Assert.True(oQue.Y < grafico.YOf(99 - Parede), "a nota fica embaixo do fundo");
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void AMemoriaDizOQueAAguaFazEQuantoCavar()
    {
        var canaleta = Canaleta((0, 100), (20, 99), (40, 99.5), (60, 99));
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", ChannelSection.Defaults[0], canaleta, null));

        Assert.Contains(c.Alerts, a => a.Contains("plano ou sobe de 20,0 a 40,0 m") && a.Contains("transborda") && a.Contains("aprofundar 0,60 m"));
    }
}
