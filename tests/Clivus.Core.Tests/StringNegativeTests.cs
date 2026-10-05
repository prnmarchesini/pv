namespace Clivus.Core.Tests;

/// <summary>
/// O − marcado com Ctrl + clique (05/10/2026, Renan: "Em convencional eu
/// deveria clicar no último módulo e ficar o símbolo. Em leapfrog eu deveria
/// clicar no módulo dois"): o clique do − fecha a string.
/// </summary>
public class StringNegativeTests
{
    private static RoutingCell C(int mesa, int coluna, int fileira) => new(mesa, coluna, fileira);

    [Fact]
    [Trait("Etapa", "11")]
    public void ConvencionalOMenosNoUltimoModuloFechaAString()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        Assert.Null(b.Click(C(0, 0, 0), RoutingKind.Conventional));

        var s = b.FinishAt(C(0, 13, 0), RoutingKind.Conventional, out var problema);

        Assert.Null(problema);
        Assert.Equal(Enumerable.Range(0, 14).Select(c => C(0, c, 0)), s!.Cells);
        Assert.Equal(C(0, 13, 0), s.Negative);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogOMenosNoVizinhoDoMaisLigaAFileiraInteira()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        Assert.Null(b.Click(C(0, 0, 0), RoutingKind.Leapfrog));

        var s = b.FinishAt(C(0, 1, 0), RoutingKind.Leapfrog, out var problema);

        Assert.Null(problema);
        Assert.Equal(14, s!.ModuleCount);
        Assert.Equal(C(0, 0, 0), s.Positive);
        Assert.Equal(C(0, 1, 0), s.Negative);
        // Ida pelos ímpares (1, 3, 5... em contagem de 1) e volta pelos pares.
        Assert.Equal([0, 2, 4, 6, 8, 10, 12, 13, 11, 9, 7, 5, 3, 1], s.Cells.Select(c => c.Column));
        Assert.Equal(Enumerable.Range(0, 14).Select(c => C(0, c, 0)).ToHashSet(), s.Cells.ToHashSet());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogParaOOutroLadoComecaNaPontaDaDireita()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        b.Click(C(0, 13, 1), RoutingKind.Leapfrog);

        var s = b.FinishAt(C(0, 12, 1), RoutingKind.Leapfrog, out var problema);

        Assert.Null(problema);
        Assert.Equal(14, s!.ModuleCount);
        Assert.Equal(C(0, 12, 1), s.Negative);
        Assert.All(s.Cells, c => Assert.Equal(1, c.Row));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogComPontoDeViradaTerminaNoMenosClicado()
    {
        // + na 1, clique simples na 7 (o fim do trecho), − na 2: meia fileira.
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        b.Click(C(0, 0, 0), RoutingKind.Leapfrog);
        Assert.Null(b.Click(C(0, 6, 0), RoutingKind.Leapfrog));

        var s = b.FinishAt(C(0, 1, 0), RoutingKind.Leapfrog, out var problema);

        Assert.Null(problema);
        Assert.Equal(7, s!.ModuleCount);
        Assert.Equal(C(0, 1, 0), s.Negative);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MenosQueNaoFechaORecusaSemEstragarAMontagem()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);

        // Sem o + ainda.
        Assert.Null(b.FinishAt(C(0, 3, 0), RoutingKind.Conventional, out var semMais));
        Assert.NotNull(semMais);

        b.Click(C(0, 0, 0), RoutingKind.Leapfrog);

        // Leapfrog com o − longe do +.
        Assert.Null(b.FinishAt(C(0, 5, 0), RoutingKind.Leapfrog, out var longe));
        Assert.NotNull(longe);

        // O − no próprio +.
        Assert.Null(b.FinishAt(C(0, 0, 0), RoutingKind.Conventional, out var mesmo));
        Assert.NotNull(mesmo);

        // A montagem continua só com o +.
        Assert.Equal([C(0, 0, 0)], b.Cells);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void LeapfrogAtravessaAsDuasMesasDe14()
    {
        var b = new RouteBuilder(StringRoutingTests.Duas14(), []);
        b.Click(C(0, 0, 0), RoutingKind.Leapfrog);

        var s = b.FinishAt(C(0, 1, 0), RoutingKind.Leapfrog, out var problema);

        Assert.Null(problema);
        Assert.Equal(14, s!.ModuleCount);
        Assert.Contains(C(1, 6, 0), s.Cells);
    }
}
