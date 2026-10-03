using UFV.Core;
using UFV.Geo;
using Xunit;

namespace UFV.Core.Tests;

/// <summary>Trocar mesa em planta (passo 9.1).</summary>
public class TableSwapTests
{
    private const double Gap = 0.8;

    /// <summary>Uma mesa de 28 (18,7 m) a 30° do +X, fundo para a esquerda da fileira.</summary>
    private static PlacedTable Antiga(double comprimento = 18.7, double fundo = 4.6)
    {
        var rumo = 30 * Math.PI / 180;
        var d = (X: Math.Cos(rumo), Y: Math.Sin(rumo));
        var n = (X: -d.Y, Y: d.X);
        var o = new Point3(1000, 2000, 0);

        Point3 P(double s, double t) => new(o.X + d.X * s + n.X * t, o.Y + d.Y * s + n.Y * t, 0);

        return new PlacedTable(3, 5, o, rumo, comprimento, fundo, [P(0, 0), P(comprimento, 0), P(comprimento, fundo), P(0, fundo)], false);
    }

    private static double Estacao(PlacedTable antiga, Point3 p) =>
        (p.X - antiga.Origin.X) * Math.Cos(antiga.DirectionRadians) + (p.Y - antiga.Origin.Y) * Math.Sin(antiga.DirectionRadians);

    private static double Fundo(PlacedTable antiga, Point3 p) =>
        -(p.X - antiga.Origin.X) * Math.Sin(antiga.DirectionRadians) + (p.Y - antiga.Origin.Y) * Math.Cos(antiga.DirectionRadians);

    [Fact]
    [Trait("Etapa", "9")]
    public void UmaPorUmaTravadaNoInicioComecaOndeAAntigaComecava()
    {
        var antiga = Antiga();
        var r = TableSwap.Plan(antiga, new TableFootprint(9.5, 4.6), kind: 1, count: 1, Gap, SwapAnchor.Start, roomBeyond: Gap);

        var nova = Assert.Single(r.Tables);
        Assert.Equal(0, Estacao(antiga, nova.Origin), 9);
        Assert.Equal(9.5, Estacao(antiga, nova.Corners[1]), 9);
        Assert.Equal(1, nova.Kind);
        Assert.Equal("F3.5", nova.Label);
        Assert.Equal(antiga.DirectionRadians, nova.DirectionRadians);
        Assert.False(r.Overflows);
        Assert.Equal(9.5 - 18.7, r.Overflow, 9);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void UmaPorUmaTravadaNoFimTerminaOndeAAntigaTerminava()
    {
        var antiga = Antiga();
        var r = TableSwap.Plan(antiga, new TableFootprint(9.5, 4.6), kind: 1, count: 1, Gap, SwapAnchor.End, roomBeyond: Gap);

        var nova = Assert.Single(r.Tables);
        Assert.Equal(18.7, Estacao(antiga, nova.Corners[1]), 9);
        Assert.Equal(18.7 - 9.5, Estacao(antiga, nova.Origin), 9);
    }

    /// <summary>"A de 28 vou escolher colocar duas de 14": duas de 9,5 m com 0,8 entre elas passam 1,1 m da de 18,7.</summary>
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(SwapAnchor.Start)]
    [InlineData(SwapAnchor.End)]
    public void DuasPorUmaDizQuantoPassaENaoMexeNaVizinha(SwapAnchor lado)
    {
        var antiga = Antiga();
        var r = TableSwap.Plan(antiga, new TableFootprint(9.5, 4.6), kind: 1, count: 2, Gap, lado, roomBeyond: Gap);

        Assert.Equal(2, r.Tables.Count);
        Assert.True(r.Overflows);
        Assert.Equal(2 * 9.5 + Gap - 18.7, r.Overflow, 9);

        // Entre as duas, o espaçamento da configuração.
        Assert.Equal(Gap, Estacao(antiga, r.Tables[1].Origin) - Estacao(antiga, r.Tables[0].Corners[1]), 9);

        // O lado travado não sai do lugar.
        if (lado == SwapAnchor.Start) Assert.Equal(0, Estacao(antiga, r.Tables[0].Origin), 9);
        else Assert.Equal(18.7, Estacao(antiga, r.Tables[1].Corners[1]), 9);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ComVaoGrandeAteAVizinhaDuasCabem()
    {
        var r = TableSwap.Plan(Antiga(), new TableFootprint(9.5, 4.6), kind: 1, count: 2, Gap, SwapAnchor.Start, roomBeyond: Gap + 2.0);

        Assert.False(r.Overflows);
        Assert.Equal(2 * 9.5 + Gap - 18.7 - 2.0, r.Overflow, 9);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void SemVizinhaNuncaPassa()
    {
        var r = TableSwap.Plan(Antiga(), new TableFootprint(18.7, 4.6), kind: 0, count: 3, Gap, SwapAnchor.Start, roomBeyond: null);

        Assert.Equal(3, r.Tables.Count);
        Assert.False(r.Overflows);
    }

    /// <summary>O fundo fica do mesmo lado da antiga, com a medida do tipo novo, e a mesa nova é retângulo.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void FundoDoMesmoLadoComAMedidaNova()
    {
        var antiga = Antiga();
        var r = TableSwap.Plan(antiga, new TableFootprint(9.5, 2.3), kind: 1, count: 1, Gap, SwapAnchor.Start, roomBeyond: null);
        var c = r.Tables[0].Corners;

        Assert.Equal(0, Fundo(antiga, c[0]), 9);
        Assert.Equal(0, Fundo(antiga, c[1]), 9);
        Assert.Equal(2.3, Fundo(antiga, c[2]), 9);
        Assert.Equal(2.3, Fundo(antiga, c[3]), 9);
        Assert.Equal(9.5, Estacao(antiga, c[2]), 9);
        Assert.Equal(0, Estacao(antiga, c[3]), 9);
    }

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(0)]
    [InlineData(11)]
    public void QuantidadeForaDeUmADezRecusa(int quantas) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TableSwap.Plan(Antiga(), new TableFootprint(9.5, 4.6), 1, quantas, Gap, SwapAnchor.Start, null));
}
