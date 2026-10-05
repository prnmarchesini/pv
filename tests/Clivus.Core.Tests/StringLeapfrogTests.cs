namespace Clivus.Core.Tests;

/// <summary>Leapfrog e traçado livre por cliques (elétrica, 11.4).</summary>
public class StringLeapfrogTests
{
    private static RoutingCell C(int mesa, int coluna, int fileira) => new(mesa, coluna, fileira);

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogNaFileiraInteiraAlternaComOPositivoNoPrimeiroEONegativoNoSegundo()
    {
        var s = StringRouting.WholeRow(StringRoutingTests.Uma28(), C(0, 0, 0), RoutingKind.Leapfrog, out var problema)!;

        Assert.Null(problema);
        Assert.Equal(14, s.ModuleCount);
        Assert.Equal([0, 2, 4, 6, 8, 10, 12, 13, 11, 9, 7, 5, 3, 1], s.Cells.Select(c => c.Column));
        Assert.All(s.Cells, c => Assert.Equal(0, c.Row));
        Assert.Equal(C(0, 0, 0), s.Positive);
        Assert.Equal(C(0, 1, 0), s.Negative);
        Assert.Equal(RoutingKind.Leapfrog, s.Segments.Single().Kind);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void AFileiraInteiraComecaNaPontaMaisPertoDoClique()
    {
        var direita = StringRouting.WholeRow(StringRoutingTests.Uma28(), C(0, 11, 1), RoutingKind.Leapfrog, out _)!;
        Assert.Equal(C(0, 13, 1), direita.Positive);
        Assert.Equal(C(0, 12, 1), direita.Negative);

        var convencional = StringRouting.WholeRow(StringRoutingTests.Duas14(), C(1, 2, 0), RoutingKind.Conventional, out _)!;
        Assert.Equal(14, convencional.ModuleCount);
        Assert.Equal(C(1, 6, 0), convencional.Positive);
        Assert.Equal(C(0, 0, 0), convencional.Negative);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogDeFileiraImparTambemFechaAoLadoDoComeco()
    {
        var s = StringRouting.WholeRow(new StringArrangement([new ArrangementTable(5, 1)]), C(0, 0, 0), RoutingKind.Leapfrog, out _)!;
        Assert.Equal([0, 2, 4, 3, 1], s.Cells.Select(c => c.Column));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ULivreNaMetadeDaMesaPegaQuatorzeEmbaixoESobe()
    {
        // Mesa 2V de 28 módulos (14 colunas): U na metade (colunas 1 a 7), 14 módulos.
        var arranjo = new StringArrangement([new ArrangementTable(14, 2)]);
        var b = new RouteBuilder(arranjo, []);

        Assert.Null(b.Click(C(0, 0, 0), RoutingKind.Conventional));
        Assert.Null(b.Click(C(0, 6, 0), RoutingKind.Conventional));
        Assert.Null(b.Click(C(0, 6, 1), RoutingKind.Conventional));
        Assert.Null(b.Click(C(0, 0, 1), RoutingKind.Conventional));
        var u = b.Finish(out _)!;

        Assert.Equal(14, u.ModuleCount);
        Assert.Equal(C(0, 0, 0), u.Positive);
        Assert.Equal(C(0, 0, 1), u.Negative);   // o − volta para o lado do +
        Assert.Equal(3, u.Segments.Count);

        // A outra metade, o mesmo U espelhado: as duas strings cobrem a mesa.
        var b2 = new RouteBuilder(arranjo, u.Cells);
        b2.Click(C(0, 13, 0), RoutingKind.Conventional);
        b2.Click(C(0, 7, 0), RoutingKind.Conventional);
        b2.Click(C(0, 7, 1), RoutingKind.Conventional);
        b2.Click(C(0, 13, 1), RoutingKind.Conventional);
        var u2 = b2.Finish(out _)!;

        Assert.Null(StringRouting.WhyInvalid(arranjo, [u, u2]));
        Assert.Equal(0, StringRouting.Uncovered(arranjo, [u, u2]));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void CadaTrechoPodeSerConvencionalOuLeapfrog()
    {
        var arranjo = StringRoutingTests.Uma28();
        var b = new RouteBuilder(arranjo, []);

        b.Click(C(0, 0, 0), RoutingKind.Conventional);
        Assert.Null(b.Click(C(0, 0, 1), RoutingKind.Conventional));       // sobe
        Assert.Null(b.Click(C(0, 6, 1), RoutingKind.Leapfrog));           // leapfrog de 0 a 6 em cima
        var s = b.Finish(out _)!;

        // Em cima: 0 (já), 2, 4, 6, 5, 3, 1 — o − fica ao lado de onde o trecho começou.
        Assert.Equal([C(0, 0, 0), C(0, 0, 1), C(0, 2, 1), C(0, 4, 1), C(0, 6, 1), C(0, 5, 1), C(0, 3, 1), C(0, 1, 1)], s.Cells);
        Assert.Equal(RoutingKind.Conventional, s.KindOfStep(1));
        Assert.Equal(RoutingKind.Leapfrog, s.KindOfStep(2));
        Assert.Equal(RoutingKind.Leapfrog, s.KindOfStep(7));
        Assert.Equal(s, StringRoute.Parse(s.ToText()));
        Assert.StartsWith("C1,L7|", s.ToText());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogNaoPassaPorModuloOcupado()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), [C(0, 3, 0)]);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);

        Assert.NotNull(b.Click(C(0, 6, 0), RoutingKind.Leapfrog));
        Assert.Single(b.Cells);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void AFileiraDeCimaNaoAtravessaMesa1V()
    {
        var misto = new StringArrangement([new ArrangementTable(4, 2), new ArrangementTable(4, 1), new ArrangementTable(4, 2)]);

        Assert.Null(StringRouting.WholeRow(misto, C(0, 0, 1), RoutingKind.Leapfrog, out var porque));
        Assert.NotNull(porque);
        Assert.NotNull(StringRouting.WholeRow(misto, C(1, 0, 0), RoutingKind.Leapfrog, out _));

        var b = new RouteBuilder(misto, []);
        b.Click(C(0, 0, 1), RoutingKind.Conventional);
        Assert.NotNull(b.Click(C(2, 3, 1), RoutingKind.Conventional));
    }
}
