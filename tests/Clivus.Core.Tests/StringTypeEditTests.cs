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
    // 05/10/2026: o requisito mudou. Espelhar era trocar o + com o −; o
    // Renan reprovou ("o espelhar deveria colocar o + e − para o lado
    // esquerdo"): agora reflete o traçado de um lado ao outro da mesa.
    public void EspelharRefleteOTracadoEOMaisEOMenosVaoParaOOutroLado()
    {
        var (biblioteca, tipo) = UmTipoComDuasStrings();
        var copia = biblioteca.Clone(tipo.Id)!;

        Assert.Null(biblioteca.Mirror(copia.Id));
        var espelhado = biblioteca.Find(copia.Id)!;

        for (var i = 0; i < tipo.Routes.Count; i++)
        {
            var o = tipo.Routes[i];
            var e = espelhado.Routes[i];
            Assert.Equal(o.Cells.Select(c => C(0, 13 - c.Column, c.Row)), e.Cells);
            Assert.Equal(C(0, 13 - o.Positive.Column, o.Positive.Row), e.Positive);
            Assert.Equal(C(0, 13 - o.Negative.Column, o.Negative.Row), e.Negative);
            Assert.Equal(o.Segments, e.Segments);
            Assert.True(e.IsWellFormed);
        }

        // O original não muda; espelhar duas vezes volta ao que era.
        Assert.Equal(tipo, biblioteca.Find(tipo.Id));
        biblioteca.Mirror(copia.Id);
        Assert.Equal(tipo.Routes, biblioteca.Find(copia.Id)!.Routes);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EspelharMantemOTipoDeCadaTrechoETrocaAOrdemDasMesas()
    {
        var (_, tipo) = UmTipoComDuasStrings();
        var s = tipo.Routes[1];          // sobe (C), leapfrog até 6 (L), convencional até 13 (C)
        var m = s.MirroredAcross(tipo.Arrangement);

        for (var i = 1; i < s.Cells.Count; i++)
            Assert.Equal(s.KindOfStep(i), m.KindOfStep(i));
        Assert.Equal(m, StringRoute.Parse(m.ToText()));

        // Duas mesas 7x2: a coluna 0 da mesa 0 vira a coluna 6 da mesa 1.
        var duas = new StringArrangement([new ArrangementTable(7, 2), new ArrangementTable(7, 2)]);
        var r = new StringRoute([C(0, 0, 0), C(0, 1, 0), C(1, 0, 0)], [new RoutingSegment(2, RoutingKind.Conventional)]);
        Assert.Equal(new[] { C(1, 6, 0), C(1, 5, 0), C(0, 6, 0) }, r.MirroredAcross(duas).Cells);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EspelharArranjoNaoSimetricoERecusado()
    {
        var biblioteca = new StringLibrary([]);
        var tipo = biblioteca.Add(new StringArrangement([new ArrangementTable(14, 2), new ArrangementTable(7, 2)]), new ArrangementSketch(1.1, 2.3, [0.5]));
        var s = new StringRoute([C(0, 0, 0), C(0, 1, 0)], [new RoutingSegment(1, RoutingKind.Conventional)]);
        Assert.Null(biblioteca.SetStrings(tipo.Id, [s]));

        Assert.NotNull(biblioteca.Mirror(tipo.Id));
        Assert.Equal(s, biblioteca.Find(tipo.Id)!.Routes.Single());
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
