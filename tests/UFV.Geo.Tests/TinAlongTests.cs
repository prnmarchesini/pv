namespace UFV.Geo.Tests;

/// <summary>
/// A cota mais alta do terreno ao longo de um segmento: o máximo de verdade,
/// triângulo a triângulo, e não o máximo de alguns pontos amostrados.
/// </summary>
public class TinAlongTests
{
    private static Point3 P(double x, double y, double z) => new(x, y, z);

    /// <summary>Um "telhado": sobe até a crista x = 50 e desce, sobre [0,100]².</summary>
    private static Tin Telhado()
    {
        double Z(double x) => 100 + (x <= 50 ? x : 100 - x) * 0.2;

        Point3 Q(double x, double y) => P(x, y, Z(x));

        return new Tin(
        [
            new Triangle(Q(0, 0), Q(50, 0), Q(50, 100)),
            new Triangle(Q(0, 0), Q(50, 100), Q(0, 100)),
            new Triangle(Q(50, 0), Q(100, 0), Q(100, 100)),
            new Triangle(Q(50, 0), Q(100, 100), Q(50, 100)),
        ]);
    }

    /// <summary>
    /// O segmento de x = 30 a x = 70 passa pela crista em x = 50, a meio
    /// caminho, onde nenhuma ponta nem meio-ponto o amostraria por sorte:
    /// o máximo é 110, na crista.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OMaximoEstaNaCristaMesmoLongeDasPontas()
    {
        Assert.True(Telhado().TryGetMaxZAlong(30, 20, 70, 20, out var z));
        Assert.Equal(110, z, 9);

        // Num segmento que não chega à crista, o máximo é numa ponta.
        Assert.True(Telhado().TryGetMaxZAlong(10, 20, 30, 20, out var z2));
        Assert.Equal(106, z2, 9);
    }

    /// <summary>Crista a um quarto do segmento, o caso da revisão do 5.2.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CristaAUmQuartoDoSegmentoEVista()
    {
        Assert.True(Telhado().TryGetMaxZAlong(45, 10, 65, 10, out var z));
        Assert.Equal(110, z, 9);
    }

    /// <summary>Segmento na diagonal, cruzando várias arestas: o máximo continua na crista.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SegmentoDiagonalTambem()
    {
        Assert.True(Telhado().TryGetMaxZAlong(20, 5, 80, 95, out var z));
        Assert.Equal(110, z, 9);
    }

    /// <summary>Segmento com uma ponta fora do terreno: sem resposta, e não o máximo do pedaço de dentro.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PontaForaDoTerrenoENaoTerResposta()
    {
        Assert.False(Telhado().TryGetMaxZAlong(-10, 20, 30, 20, out _));
        Assert.False(Telhado().TryGetMaxZAlong(30, 20, 120, 20, out _));
        Assert.False(Telhado().TryGetMaxZAlong(-10, 20, 120, 20, out _));
    }

    /// <summary>Um buraco na triangulação no meio do segmento também é não ter resposta.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void BuracoNoMeioENaoTerResposta()
    {
        Point3 Q(double x, double y) => P(x, y, 100);

        var comFresta = new Tin(
        [
            new Triangle(Q(0, 0), Q(49.9, 0), Q(49.9, 100)),
            new Triangle(Q(0, 0), Q(49.9, 100), Q(0, 100)),
            new Triangle(Q(50.1, 0), Q(100, 0), Q(100, 100)),
            new Triangle(Q(50.1, 0), Q(100, 100), Q(50.1, 100)),
        ]);

        Assert.False(comFresta.TryGetMaxZAlong(30, 20, 70, 20, out _));
        Assert.True(comFresta.TryGetMaxZAlong(10, 20, 40, 20, out var z));
        Assert.Equal(100, z, 9);
    }

    /// <summary>Segmento inteiro dentro de um triângulo só: o máximo é numa ponta.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DentroDeUmTrianguloSoOMaximoEstaNumaPonta()
    {
        Assert.True(Telhado().TryGetMaxZAlong(10, 5, 40, 5, out var z));
        Assert.Equal(108, z, 9);
    }

    /// <summary>Segmento de comprimento zero é o ponto.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SegmentoDeComprimentoZeroEOPonto()
    {
        Assert.True(Telhado().TryGetMaxZAlong(25, 25, 25, 25, out var z));
        Assert.Equal(105, z, 9);
    }

    /// <summary>Segmento por cima de uma aresta da triangulação (x = 50, a crista): 110 o caminho todo.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SegmentoSobreUmaArestaDaMalha()
    {
        Assert.True(Telhado().TryGetMaxZAlong(50, 10, 50, 90, out var z));
        Assert.Equal(110, z, 9);
    }

    /// <summary>
    /// Concorda com a amostragem fina: em segmentos aleatórios, o máximo ao
    /// longo é maior ou igual à cota em qualquer ponto amostrado, e igual ao
    /// máximo amostrado a menos do que a amostragem perde entre pontos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ConcordaComAmostragemFina()
    {
        var telhado = Telhado();
        var sorteio = new Random(52);

        for (var caso = 0; caso < 200; caso++)
        {
            var x0 = 5 + sorteio.NextDouble() * 90;
            var y0 = 5 + sorteio.NextDouble() * 90;
            var x1 = 5 + sorteio.NextDouble() * 90;
            var y1 = 5 + sorteio.NextDouble() * 90;

            Assert.True(telhado.TryGetMaxZAlong(x0, y0, x1, y1, out var maximo));

            var amostrado = double.NegativeInfinity;

            for (var i = 0; i <= 1000; i++)
            {
                var t = i / 1000.0;
                Assert.True(telhado.TryGetZ(x0 + t * (x1 - x0), y0 + t * (y1 - y0), out var z));
                amostrado = Math.Max(amostrado, z);
            }

            Assert.True(maximo >= amostrado - 1e-9);
            Assert.True(maximo <= amostrado + 0.01);
        }
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void EntradaNaoFinitaOuTerrenoVazioNaoRespondem()
    {
        Assert.False(Telhado().TryGetMaxZAlong(double.NaN, 0, 10, 0, out _));
        Assert.False(new Tin([]).TryGetMaxZAlong(0, 0, 10, 0, out _));
    }
}
