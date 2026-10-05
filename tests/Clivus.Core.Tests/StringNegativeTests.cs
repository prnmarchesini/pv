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

    /// <summary>
    /// O U em quatro cliques (05/10/2026, Renan: "clicar no 1, no 4, no de
    /// baixo do 4 e aí no de baixo do 1 fechando com o −").
    /// </summary>
    [Fact]
    [Trait("Etapa", "11")]
    public void OUEmQuatroCliques()
    {
        var b = new RouteBuilder(new StringArrangement([new ArrangementTable(7, 2)]), []);

        Assert.Null(b.Click(C(0, 0, 1), RoutingKind.Conventional));   // + no 1 (fileira de cima)
        Assert.Null(b.Click(C(0, 3, 1), RoutingKind.Conventional));   // o 4
        Assert.Null(b.Click(C(0, 3, 0), RoutingKind.Conventional));   // o de baixo do 4
        var s = b.FinishAt(C(0, 0, 0), RoutingKind.Conventional, out var problema);   // − no de baixo do 1

        Assert.Null(problema);
        Assert.Equal(8, s!.ModuleCount);
        Assert.Equal(C(0, 0, 1), s.Positive);
        Assert.Equal(C(0, 0, 0), s.Negative);
    }

    /// <summary>
    /// Clique em diagonal no convencional vira um L: desce (ou sobe) pela
    /// coluna e anda pela fileira; se esse caminho passa por módulo usado,
    /// tenta pela fileira primeiro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "11")]
    public void DiagonalNoConvencionalViraL()
    {
        var mesa = new StringArrangement([new ArrangementTable(7, 2)]);
        var b = new RouteBuilder(mesa, []);
        b.Click(C(0, 0, 1), RoutingKind.Conventional);
        b.Click(C(0, 6, 1), RoutingKind.Conventional);    // a fileira de cima inteira

        // Do 7 de cima direto ao 1 de baixo: desce pela coluna do 7 e volta pela de baixo.
        var s = b.FinishAt(C(0, 0, 0), RoutingKind.Conventional, out var problema);

        Assert.Null(problema);
        Assert.Equal(14, s!.ModuleCount);
        Assert.Equal(C(0, 6, 0), s.Cells[7]);
        Assert.Equal(C(0, 0, 0), s.Negative);
    }

    /// <summary>
    /// Clicar num módulo por onde a linha já passa recua a string até o
    /// anterior a ele (05/10/2026: "a linha vai do 1 ao 4, se eu clicasse no 3
    /// ela pararia no 2").
    /// </summary>
    [Fact]
    [Trait("Etapa", "11")]
    public void CliqueNoMeioDaLinhaRecuaAteOAnterior()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);
        b.Click(C(0, 3, 0), RoutingKind.Conventional);

        Assert.Null(b.Click(C(0, 2, 0), RoutingKind.Conventional));

        Assert.Equal([C(0, 0, 0), C(0, 1, 0)], b.Cells);
        Assert.Equal([new RoutingSegment(1, RoutingKind.Conventional)], b.Segments);

        // Continua dali normalmente.
        Assert.Null(b.Click(C(0, 5, 0), RoutingKind.Conventional));
        Assert.Equal(6, b.Cells.Count);

        // No + não recua (Desfazer trecho é para isso).
        Assert.NotNull(b.Click(C(0, 0, 0), RoutingKind.Conventional));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MenosNoMeioDaLinhaFechaNele()
    {
        var b = new RouteBuilder(StringRoutingTests.Uma28(), []);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);
        b.Click(C(0, 9, 0), RoutingKind.Conventional);

        var s = b.FinishAt(C(0, 5, 0), RoutingKind.Conventional, out var problema);

        Assert.Null(problema);
        Assert.Equal(6, s!.ModuleCount);
        Assert.Equal(C(0, 5, 0), s.Negative);
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
