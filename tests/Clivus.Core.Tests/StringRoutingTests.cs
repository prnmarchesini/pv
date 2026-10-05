namespace Clivus.Core.Tests;

/// <summary>Polaridade e traçado convencional (elétrica, 11.3).</summary>
public class StringRoutingTests
{
    internal static StringArrangement Uma28() => new([new ArrangementTable(14, 2)]);

    internal static StringArrangement Duas14() => new([new ArrangementTable(7, 2), new ArrangementTable(7, 2)]);

    internal static RoutingCell C(int mesa, int coluna, int fileira) => new(mesa, coluna, fileira);

    [Fact]
    [Trait("Etapa", "11")]
    public void ConvencionalLigaEmSequenciaComOPositivoEONegativoEmPontasOpostas()
    {
        var b = new RouteBuilder(Uma28(), []);

        Assert.Null(b.Click(C(0, 0, 0), RoutingKind.Conventional));
        Assert.Null(b.Click(C(0, 13, 0), RoutingKind.Conventional));
        var s = b.Finish(out var problema)!;

        Assert.Null(problema);
        Assert.Equal(14, s.ModuleCount);
        Assert.Equal(Enumerable.Range(0, 14).Select(c => C(0, c, 0)), s.Cells);
        Assert.Equal(C(0, 0, 0), s.Positive);
        Assert.Equal(C(0, 13, 0), s.Negative);
        Assert.Equal([new RoutingSegment(13, RoutingKind.Conventional)], s.Segments);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void UmaMesa2VDe28ComDuasStringsDe14()
    {
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(Uma28(), new ArrangementSketch(1.1, 2.3, []));

        var baixo = new RouteBuilder(Uma28(), []);
        baixo.Click(C(0, 0, 0), RoutingKind.Conventional);
        baixo.Click(C(0, 13, 0), RoutingKind.Conventional);
        var s1 = baixo.Finish(out _)!;

        var cima = new RouteBuilder(Uma28(), s1.Cells);
        Assert.NotNull(cima.Click(C(0, 5, 0), RoutingKind.Conventional));   // módulo da outra string
        Assert.Null(cima.Click(C(0, 13, 1), RoutingKind.Conventional));
        Assert.Null(cima.Click(C(0, 0, 1), RoutingKind.Conventional));
        var s2 = cima.Finish(out _)!;

        Assert.Null(biblioteca.SetStrings(tipo.Id, [s1, s2]));
        var gravado = biblioteca.Find(tipo.Id)!;
        Assert.Equal(2, gravado.Routes.Count);
        Assert.Equal(0, StringRouting.Uncovered(gravado.Arrangement, gravado.Routes));
        Assert.True(gravado.CanGenerate);
        Assert.Equal(C(0, 13, 1), gravado.Routes[1].Positive);
        Assert.Equal(C(0, 0, 1), gravado.Routes[1].Negative);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ATiradaPelaFileiraAtravessaAsMesasDoTipo()
    {
        var b = new RouteBuilder(Duas14(), []);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);
        Assert.Null(b.Click(C(1, 6, 0), RoutingKind.Conventional));

        var s = b.Finish(out _)!;
        Assert.Equal(14, s.ModuleCount);
        Assert.Equal(C(0, 6, 0), s.Cells[6]);
        Assert.Equal(C(1, 0, 0), s.Cells[7]);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void CliqueQueNaoSegueFileiraNemColunaERecusado()
    {
        // 05/10/2026: no convencional a diagonal virou um L (teste próprio
        // em StringNegativeTests); no leapfrog ela continua recusada.
        var b = new RouteBuilder(Uma28(), []);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);

        Assert.NotNull(b.Click(C(0, 5, 1), RoutingKind.Leapfrog));       // diagonal no leapfrog
        Assert.NotNull(b.Click(C(0, 0, 0), RoutingKind.Conventional));   // o próprio +
        Assert.NotNull(b.Click(C(0, 14, 0), RoutingKind.Conventional));  // fora da mesa
        Assert.NotNull(b.Click(C(1, 0, 0), RoutingKind.Conventional));   // mesa que não existe
        Assert.Single(b.Cells);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OTrechoNaoPassaPorModuloQueJaTemString()
    {
        var b = new RouteBuilder(Uma28(), [C(0, 4, 0)]);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);

        Assert.NotNull(b.Click(C(0, 9, 0), RoutingKind.Conventional));
        Assert.Null(b.Click(C(0, 3, 0), RoutingKind.Conventional));
        Assert.Equal(4, b.Cells.Count);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void DesfazerTiraOUltimoTrechoEDepoisOPositivo()
    {
        var b = new RouteBuilder(Uma28(), []);
        b.Click(C(0, 0, 0), RoutingKind.Conventional);
        b.Click(C(0, 6, 0), RoutingKind.Conventional);
        b.Click(C(0, 6, 1), RoutingKind.Conventional);

        Assert.True(b.Undo());
        Assert.Equal(7, b.Cells.Count);
        Assert.True(b.Undo());
        Assert.Single(b.Cells);
        Assert.Null(b.Finish(out var umSo));
        Assert.NotNull(umSo);
        Assert.True(b.Undo());
        Assert.True(b.IsEmpty);
        Assert.False(b.Undo());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OTracadoDoTipoEConferido()
    {
        var a = new StringRoute([C(0, 0, 0), C(0, 1, 0)], [new RoutingSegment(1, RoutingKind.Conventional)]);
        var repete = new StringRoute([C(0, 1, 0), C(0, 2, 0)], [new RoutingSegment(1, RoutingKind.Conventional)]);
        var fora = new StringRoute([C(0, 13, 1), C(0, 14, 1)], [new RoutingSegment(1, RoutingKind.Conventional)]);
        var umSo = new StringRoute([C(0, 5, 0)], []);
        var torta = new StringRoute([C(0, 3, 0), C(0, 4, 0)], [new RoutingSegment(0, RoutingKind.Conventional)]);

        Assert.Null(StringRouting.WhyInvalid(Uma28(), [a]));
        Assert.NotNull(StringRouting.WhyInvalid(Uma28(), [a, repete]));
        Assert.NotNull(StringRouting.WhyInvalid(Uma28(), [fora]));
        Assert.NotNull(StringRouting.WhyInvalid(Uma28(), [umSo]));
        Assert.NotNull(StringRouting.WhyInvalid(Uma28(), [torta]));
        Assert.Equal(26, StringRouting.Uncovered(Uma28(), [a]));

        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(Uma28());
        Assert.NotNull(biblioteca.SetStrings(tipo.Id, [a, repete]));
        Assert.Empty(biblioteca.Find(tipo.Id)!.Routes);
        Assert.False(biblioteca.Find(tipo.Id)!.CanGenerate);
        Assert.NotNull(biblioteca.SetStrings(biblioteca.Add(StringArrangement.Empty).Id, [a]));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void TrocarAsMesasPorOutraGradeLimpaOTracadoEPelaMesmaMantem()
    {
        var biblioteca = new StringLibrary([]);
        var desenho = new ArrangementSketch(1.1, 2.3, []);
        var tipo = biblioteca.Add(Uma28(), desenho);
        var s = new StringRoute([C(0, 0, 0), C(0, 1, 0)], [new RoutingSegment(1, RoutingKind.Conventional)]);
        biblioteca.SetStrings(tipo.Id, [s]);

        biblioteca.SetArrangement(tipo.Id, Uma28(), desenho with { CellWidth = 1.2 });
        Assert.Single(biblioteca.Find(tipo.Id)!.Routes);

        biblioteca.SetArrangement(tipo.Id, Duas14(), new ArrangementSketch(1.1, 2.3, [0.5]));
        Assert.Empty(biblioteca.Find(tipo.Id)!.Routes);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OTracadoLongoVaiEVoltaDoDesenhoEmPedacos()
    {
        var duasDe28 = new StringArrangement([new ArrangementTable(14, 2), new ArrangementTable(14, 2)]);
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(duasDe28, new ArrangementSketch(1.1, 2.3, [0.5]));
        var s1 = new RouteBuilder(duasDe28, []);
        s1.Click(C(0, 0, 0), RoutingKind.Conventional);
        s1.Click(C(1, 13, 0), RoutingKind.Conventional);
        var a = s1.Finish(out _)!;
        var s2 = new RouteBuilder(duasDe28, a.Cells);
        s2.Click(C(1, 13, 1), RoutingKind.Conventional);
        s2.Click(C(0, 0, 1), RoutingKind.Conventional);
        Assert.Null(biblioteca.SetStrings(tipo.Id, [a, s2.Finish(out _)!]));
        biblioteca.Add(Uma28());

        var tipos = StringTypeRecords.Write(biblioteca.Types);
        var tracados = StringTypeRecords.WriteRoutes(biblioteca.Types);
        var lido = StringTypeRecords.Read(tipos, tracados, "de tipos de string");

        Assert.True(StringRouting.ToText(biblioteca.Types[0].Routes).Length > 200);
        Assert.True(tracados.Count > RecordTable.CamposDoCabecalho + StringRouteChunk.FieldCount);
        Assert.Null(lido.Problem);
        Assert.Equal(biblioteca.Types, lido.Items);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void TracadoEstragadoAvisaEOTipoFicaSemTracado()
    {
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(Uma28(), new ArrangementSketch(1.1, 2.3, []));
        biblioteca.SetStrings(tipo.Id, [new StringRoute([C(0, 0, 0), C(0, 1, 0)], [new RoutingSegment(1, RoutingKind.Conventional)])]);

        var estragado = RecordTable.Write(StringTypeRecords.RouteVersion, StringRouteChunk.FieldCount, [new StringRouteChunk(tipo.Id, 0, "C1|0.0.0,0.99.0")], p => p.ToFields());
        var lido = StringTypeRecords.Read(StringTypeRecords.Write(biblioteca.Types), estragado, "de tipos de string");

        Assert.NotNull(lido.Problem);
        Assert.Contains(tipo.Name, lido.Problem);
        Assert.Empty(lido.Items.Single().Routes);
    }

    [Theory]
    [Trait("Etapa", "11")]
    [InlineData("")]
    [InlineData("C1")]
    [InlineData("X1|0.0.0,0.1.0")]
    [InlineData("C1|0.0.0,0.0.0")]
    [InlineData("C1|0.0.0")]
    [InlineData("C2|0.0.0,0.1.0")]
    [InlineData("C1|0.0,0.1.0")]
    public void TextoDeStringEstragadoNaoViraString(string texto) =>
        Assert.Null(StringRoute.Parse(texto));
}
