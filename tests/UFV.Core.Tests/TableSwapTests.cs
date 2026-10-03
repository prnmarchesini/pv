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
        Assert.Equal(["F3.5a", "F3.5b"], r.Tables.Select(t => t.Label));
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

    /// <summary>A mesa vizinha da mesma fileira, deslocada ao longo dela, em planta.</summary>
    private static IReadOnlyList<Point3> Vizinha(PlacedTable antiga, double de, double ate, double fundoDe = 0, double fundoAte = 4.6)
    {
        var d = (X: Math.Cos(antiga.DirectionRadians), Y: Math.Sin(antiga.DirectionRadians));
        var n = (X: -d.Y, Y: d.X);
        Point3 P(double s, double t) => new(antiga.Origin.X + d.X * s + n.X * t, antiga.Origin.Y + d.Y * s + n.Y * t, 0);
        return [P(de, fundoDe), P(ate, fundoDe), P(ate, fundoAte), P(de, fundoAte)];
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OVaoAteAVizinhaEMedidoDoLadoSolto()
    {
        var antiga = Antiga();
        var antes = Vizinha(antiga, -19.5, -0.8);
        var depois = Vizinha(antiga, 18.7 + 1.3, 18.7 + 1.3 + 18.7);
        var maisLonge = Vizinha(antiga, 50, 60);
        var outraFileira = Vizinha(antiga, 19.0, 30, fundoDe: 6, fundoAte: 10.6);

        Assert.Equal(1.3, TableSwap.RoomBeyond(antiga, [antes, depois, maisLonge, outraFileira], SwapAnchor.Start)!.Value, 9);
        Assert.Equal(0.8, TableSwap.RoomBeyond(antiga, [antes, depois, maisLonge, outraFileira], SwapAnchor.End)!.Value, 9);
        Assert.Null(TableSwap.RoomBeyond(antiga, [antes, outraFileira], SwapAnchor.Start));
        Assert.Null(TableSwap.RoomBeyond(antiga, [depois], SwapAnchor.End));
    }

    /// <summary>
    /// Depois de trocar a F3.2 (28) por duas de 14 que passam 1,1 m na F3.3,
    /// reespaçar põe tudo com 0,8 m entre mesas, mantendo tipos e letreiros;
    /// o trecho depois do vão grande (recorte da área) fica onde estava.
    /// </summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void ReespacarMantemTiposELetreirosEAcertaOEspacamento()
    {
        var base28 = Antiga();
        var pegadas = new[] { new TableFootprint(18.7, 4.6), new TableFootprint(9.5, 4.6) };

        PlacedTable Em(double s, int tipo, int numero, string sufixo = "")
        {
            var p = TableSwap.Plan(base28, pegadas[tipo], tipo, 1, Gap, SwapAnchor.Start, null).Tables[0];
            var d = (X: Math.Cos(base28.DirectionRadians), Y: Math.Sin(base28.DirectionRadians));
            Point3 M(Point3 c) => new(c.X + d.X * s, c.Y + d.Y * s, 0);
            return p with { Number = numero, Suffix = sufixo, Origin = M(p.Origin), Corners = p.Corners.Select(M).ToList() };
        }

        var fileira = new[]
        {
            Em(0, 0, 1),
            Em(18.7 + 0.8, 1, 2, "a"),
            Em(18.7 + 0.8 + 9.5 + 0.8, 1, 2, "b"),     // passa 1,1 m na F3.3
            Em(2 * 18.7 + 1.6, 0, 3),
            Em(2 * 18.7 + 1.6 + 18.7 + 12, 0, 4),      // depois de um vão de 12 m
        };

        var r = TableSwap.Respace(fileira.Reverse().ToList(), pegadas, Gap, maxGap: 5.0);

        Assert.Equal(["F3.1", "F3.2a", "F3.2b", "F3.3", "F3.4"], r.Select(t => t.Label));
        Assert.Equal([0, 1, 1, 0, 0], r.Select(t => t.Kind));

        var inicios = r.Select(t => Estacao(base28, t.Origin)).ToList();
        Assert.Equal(0, inicios[0], 9);
        Assert.Equal(18.7 + 0.8, inicios[1], 9);
        Assert.Equal(18.7 + 0.8 + 9.5 + 0.8, inicios[2], 9);
        Assert.Equal(18.7 + 0.8 + 2 * (9.5 + 0.8), inicios[3], 9);
        Assert.Equal(2 * 18.7 + 1.6 + 18.7 + 12, inicios[4], 9);
        Assert.All(r, t => Assert.Equal(0, Fundo(base28, t.Origin), 9));
    }

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(0)]
    [InlineData(11)]
    public void QuantidadeForaDeUmADezRecusa(int quantas) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TableSwap.Plan(Antiga(), new TableFootprint(9.5, 4.6), 1, quantas, Gap, SwapAnchor.Start, null));
}
