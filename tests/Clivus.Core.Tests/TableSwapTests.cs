using Clivus.Core;
using Clivus.Geo;
using Xunit;

namespace Clivus.Core.Tests;

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

    // ---- esquerda e direita pelo norte do desenho (Renan, 10/10/2026) ----

    /// <summary>
    /// Esquerda é a ponta de menor X, qualquer que seja o sentido em que a
    /// fileira foi desenhada; quase norte-sul, a ponta sul.
    /// </summary>
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(30.0, RowSide.Left, SwapAnchor.Start)]
    [InlineData(30.0, RowSide.Right, SwapAnchor.End)]
    [InlineData(210.0, RowSide.Left, SwapAnchor.End)]
    [InlineData(210.0, RowSide.Right, SwapAnchor.Start)]
    [InlineData(0.0, RowSide.Left, SwapAnchor.Start)]
    [InlineData(180.0, RowSide.Left, SwapAnchor.End)]
    [InlineData(90.0, RowSide.Left, SwapAnchor.Start)]     // desenhada para o norte: o início é o sul
    [InlineData(90.0, RowSide.Right, SwapAnchor.End)]
    [InlineData(270.0, RowSide.Left, SwapAnchor.End)]      // desenhada para o sul: o fim é o sul
    [InlineData(270.0, RowSide.Right, SwapAnchor.Start)]
    [InlineData(90.5, RowSide.Left, SwapAnchor.Start)]     // quase norte-sul, um tico para oeste: ainda o sul
    [InlineData(-89.5, RowSide.Left, SwapAnchor.End)]      // quase sul, um tico para leste: ainda o sul
    public void EsquerdaEDireitaPeloNorte(double graus, RowSide lado, SwapAnchor esperado) =>
        Assert.Equal(esperado, TableSwap.AnchorOf(lado, graus * Math.PI / 180));

    /// <summary>Uma mesa da fileira de rumo <paramref name="graus"/>, a <paramref name="s"/> m da origem ao longo dela.</summary>
    private static PlacedTable NaFileira(double graus, double s, double comprimento, int tipo, int numero, string sufixo = "")
    {
        var rumo = graus * Math.PI / 180;
        var d = (X: Math.Cos(rumo), Y: Math.Sin(rumo));
        var n = (X: -d.Y, Y: d.X);
        var o = new Point3(1000, 2000, 0);
        const double fundo = 4.6;

        Point3 P(double a, double t) => new(o.X + d.X * a + n.X * t, o.Y + d.Y * a + n.Y * t, 0);

        return new PlacedTable(3, numero, P(s, 0), rumo, comprimento, fundo, [P(s, 0), P(s + comprimento, 0), P(s + comprimento, fundo), P(s, fundo)], false, tipo, Suffix: sufixo);
    }

    /// <summary>
    /// Sem refazer a fileira, a troca de uma mesa de 28 por uma de 14 deixa a
    /// ponta do lado escolhido no lugar, nos dois sentidos de desenho.
    /// </summary>
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(30.0, RowSide.Left)]
    [InlineData(30.0, RowSide.Right)]
    [InlineData(210.0, RowSide.Left)]
    [InlineData(210.0, RowSide.Right)]
    [InlineData(270.0, RowSide.Left)]
    [InlineData(270.0, RowSide.Right)]
    public void TrocaSemRefazerDeixaAPontaDoLadoNoLugar(double graus, RowSide lado)
    {
        var antiga = NaFileira(graus, 0, 18.7, 0, 2);
        var r = TableSwap.Plan(antiga, new TableFootprint(9.5, 4.6), 1, 1, Gap, TableSwap.AnchorOf(lado, antiga.DirectionRadians), null);
        var nova = Assert.Single(r.Tables);

        // Norte-sul: esquerda é o sul (Y); senão, o X.
        Func<Point3, double> eixo = Math.Abs(Math.Cos(antiga.DirectionRadians)) < TableSwap.NorthSouthCosine ? p => p.Y : p => p.X;

        if (lado == RowSide.Left) Assert.Equal(antiga.Corners.Min(eixo), nova.Corners.Min(eixo), 9);
        else Assert.Equal(antiga.Corners.Max(eixo), nova.Corners.Max(eixo), 9);
    }

    /// <summary>
    /// "Quando eu troco a estrutura do meio, ele não trava a ponta certo, ele
    /// tá travando sempre a ponta da esquerda" (Renan, 10/10/2026): a F3.2 de
    /// 28 virou duas de 14 que passam 1,1 m na F3.3; refazer a fileira com o
    /// lado escolhido deixa parada a ponta desse lado de cada trecho, e a
    /// fileira cresce para o outro lado. Nos dois sentidos de desenho e numa
    /// fileira norte-sul.
    /// </summary>
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(30.0, RowSide.Left)]
    [InlineData(30.0, RowSide.Right)]
    [InlineData(210.0, RowSide.Left)]
    [InlineData(210.0, RowSide.Right)]
    [InlineData(90.0, RowSide.Left)]
    [InlineData(90.0, RowSide.Right)]
    [InlineData(270.0, RowSide.Left)]
    [InlineData(270.0, RowSide.Right)]
    public void RefazerAFileiraTravaAPontaDoLadoEscolhido(double graus, RowSide lado)
    {
        var pegadas = new[] { new TableFootprint(18.7, 4.6), new TableFootprint(9.5, 4.6) };

        var trecho1 = new[]
        {
            NaFileira(graus, 0, 18.7, 0, 1),
            NaFileira(graus, 18.7 + 0.8, 9.5, 1, 2, "a"),
            NaFileira(graus, 18.7 + 0.8 + 9.5 + 0.8, 9.5, 1, 2, "b"),   // passa 1,1 m na F3.3
            NaFileira(graus, 2 * 18.7 + 1.6, 18.7, 0, 3),
        };
        var trecho2 = new[] { NaFileira(graus, 2 * 18.7 + 1.6 + 18.7 + 12, 18.7, 0, 4) };   // depois de um vão de 12 m

        var r = TableSwap.Respace(trecho1.Concat(trecho2).Reverse().ToList(), pegadas, Gap, maxGap: 5.0, lado);

        Assert.Equal(["F3.1", "F3.2a", "F3.2b", "F3.3", "F3.4"], r.Select(t => t.Label));
        Assert.Equal([0, 1, 1, 0, 0], r.Select(t => t.Kind));

        var rumo = graus * Math.PI / 180;
        var norteSul = Math.Abs(Math.Cos(rumo)) < TableSwap.NorthSouthCosine;
        Func<Point3, double> eixo = norteSul ? p => p.Y : p => p.X;
        double Menor(IEnumerable<PlacedTable> ms) => ms.SelectMany(m => m.Corners).Min(eixo);
        double Maior(IEnumerable<PlacedTable> ms) => ms.SelectMany(m => m.Corners).Max(eixo);

        var novo1 = r.Take(4).ToList();
        var novo2 = r.Skip(4).ToList();

        // A ponta travada de cada trecho fica parada.
        if (lado == RowSide.Left)
        {
            Assert.Equal(Menor(trecho1), Menor(novo1), 9);
            Assert.Equal(Menor(trecho2), Menor(novo2), 9);
        }
        else
        {
            Assert.Equal(Maior(trecho1), Maior(novo1), 9);
            Assert.Equal(Maior(trecho2), Maior(novo2), 9);
        }

        // O trecho 1 cresce 1,1 m (ao longo da fileira) para o lado solto.
        var cresce = 1.1 * Math.Abs(norteSul ? Math.Sin(rumo) : Math.Cos(rumo));
        if (lado == RowSide.Left) Assert.Equal(Maior(trecho1) + cresce, Maior(novo1), 9);
        else Assert.Equal(Menor(trecho1) - cresce, Menor(novo1), 9);

        // Entre vizinhas do trecho, o espaçamento da configuração, ao longo da fileira.
        var d = (X: Math.Cos(rumo), Y: Math.Sin(rumo));
        double Estacao(Point3 p) => (p.X - 1000) * d.X + (p.Y - 2000) * d.Y;
        for (var i = 1; i < novo1.Count; i++)
            Assert.Equal(Gap, Estacao(novo1[i].Origin) - Estacao(novo1[i - 1].Corners[1]), 9);
        Assert.Equal(9.5, novo1[1].Length, 9);

        // Sem lado (Regerar fileira > Manter): o início de cada trecho, como antes.
        var semLado = TableSwap.Respace(trecho1.Concat(trecho2).ToList(), pegadas, Gap, maxGap: 5.0);
        Assert.Equal(0, Estacao(semLado[0].Origin), 9);
        Assert.Equal(2 * 18.7 + 1.6 + 18.7 + 12, Estacao(semLado[4].Origin), 9);
    }

    /// <summary>
    /// Uma mesa curta posta por cima de uma longa (troca sem refazer que
    /// passou da vizinha) não abre trecho falso: o fim do trecho é o maior
    /// fim visto, não o da última mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void MesaDentroDeOutraNaoQuebraOTrecho()
    {
        var pegadas = new[] { new TableFootprint(18.7, 4.6), new TableFootprint(9.5, 4.6) };
        var fileira = new[]
        {
            NaFileira(0, 0, 18.7, 0, 1),
            NaFileira(0, 2, 9.5, 1, 2),       // dentro da F3.1
            NaFileira(0, 18.7 + 0.8, 18.7, 0, 3),
        };

        var r = TableSwap.Respace(fileira, pegadas, Gap, maxGap: 5.0, RowSide.Left);

        // Um trecho só: a ponta esquerda fica e as três se encostam nela (com
        // trecho falso, a F3.3 ficaria em 19,5, por cima da F3.2).
        Assert.Equal(0, r[0].Origin.X - 1000, 9);
        Assert.Equal(18.7 + 0.8 + 9.5 + 0.8, r[2].Origin.X - 1000, 9);
        Assert.Equal(Gap, r[1].Origin.X - r[0].Corners[1].X, 9);
        Assert.Equal(Gap, r[2].Origin.X - r[1].Corners[1].X, 9);
    }

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(0)]
    [InlineData(11)]
    public void QuantidadeForaDeUmADezRecusa(int quantas) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TableSwap.Plan(Antiga(), new TableFootprint(9.5, 4.6), 1, quantas, Gap, SwapAnchor.Start, null));
}
