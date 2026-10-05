namespace Clivus.Core.Tests;

/// <summary>Editar, apagar, clonar e espelhar um tipo de string (elétrica, 11.5).</summary>
public class StringTypeEditTests
{
    private static RoutingCell C(int mesa, int coluna, int fileira) => new(mesa, coluna, fileira);

    private static (StringLibrary Biblioteca, StringType Tipo) UmTipoComDuasStrings()
    {
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(StringRoutingTests.Uma28(), new ArrangementSketch(1.1, 2.3, []));
        var baixo = StringRouting.WholeRow(tipo.Arrangement, C(0, 0, 0), RoutingKind.Conventional, out _)!;
        var b = new RouteBuilder(tipo.Arrangement, baixo.Cells);
        b.Click(C(0, 0, 1), RoutingKind.Conventional);
        b.Click(C(0, 6, 1), RoutingKind.Leapfrog);
        b.Click(C(0, 13, 1), RoutingKind.Conventional);
        Assert.Null(biblioteca.SetStrings(tipo.Id, [baixo, b.Finish(out _)!]));
        return (biblioteca, biblioteca.Find(tipo.Id)!);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ClonarCopiaMesasDesenhoETracadoComNomeNovo()
    {
        var (biblioteca, tipo) = UmTipoComDuasStrings();

        var copia = biblioteca.Clone(tipo.Id)!;

        Assert.NotEqual(tipo.Id, copia.Id);
        Assert.Equal("Modelo 2", copia.Name);
        Assert.Equal(tipo.Arrangement, copia.Arrangement);
        Assert.Equal(tipo.Sketch, copia.Sketch);
        Assert.Equal(tipo.Routes, copia.Routes);
        Assert.Null(biblioteca.Clone(Guid.NewGuid()));
        Assert.Equal(2, biblioteca.Types.Count);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EspelharTrocaOPositivoComONegativoDeCadaString()
    {
        var (biblioteca, tipo) = UmTipoComDuasStrings();
        var copia = biblioteca.Clone(tipo.Id)!;

        Assert.Null(biblioteca.Mirror(copia.Id));
        var espelhado = biblioteca.Find(copia.Id)!;

        for (var i = 0; i < tipo.Routes.Count; i++)
        {
            Assert.Equal(tipo.Routes[i].Positive, espelhado.Routes[i].Negative);
            Assert.Equal(tipo.Routes[i].Negative, espelhado.Routes[i].Positive);
            Assert.Equal(tipo.Routes[i].Cells.Reverse(), espelhado.Routes[i].Cells);
            Assert.True(espelhado.Routes[i].IsWellFormed);
        }

        // O original não muda; espelhar duas vezes volta ao que era.
        Assert.Equal(tipo, biblioteca.Find(tipo.Id));
        biblioteca.Mirror(copia.Id);
        Assert.Equal(tipo.Routes, biblioteca.Find(copia.Id)!.Routes);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EspelharMantemOTipoDeCadaTrecho()
    {
        var (_, tipo) = UmTipoComDuasStrings();
        var s = tipo.Routes[1];          // sobe (C), leapfrog até 6 (L), convencional até 13 (C)
        var m = s.Mirrored();

        Assert.Equal(s.Segments.Select(t => t.Kind).Reverse(), m.Segments.Select(t => t.Kind));
        for (var i = 1; i < s.Cells.Count; i++)
            Assert.Equal(s.KindOfStep(i), m.KindOfStep(s.Cells.Count - i));
        Assert.Equal(m, StringRoute.Parse(m.ToText()));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EspelharTipoSemTracadoOuQueSumiuERecusado()
    {
        var biblioteca = new StringLibrary([]);
        var vazio = biblioteca.Add(StringRoutingTests.Uma28());

        Assert.NotNull(biblioteca.Mirror(vazio.Id));
        Assert.NotNull(biblioteca.Mirror(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EditarTiraUmaStringEApagarTiraOTipo()
    {
        var (biblioteca, tipo) = UmTipoComDuasStrings();

        Assert.True(biblioteca.RemoveString(tipo.Id, 0));
        Assert.Single(biblioteca.Find(tipo.Id)!.Routes);
        Assert.Equal(tipo.Routes[1], biblioteca.Find(tipo.Id)!.Routes[0]);
        Assert.False(biblioteca.RemoveString(tipo.Id, 5));
        Assert.False(biblioteca.RemoveString(Guid.NewGuid(), 0));

        Assert.True(biblioteca.Remove(tipo.Id));
        Assert.Empty(biblioteca.Types);
    }
}
