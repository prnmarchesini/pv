using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>
/// Passo 25.3: declividade e direção de descida do terreno, triângulo a
/// triângulo. É a base das flechas (26), do perfil (27) e do caminho da
/// água (25.4, 30).
/// </summary>
public class TinSlopeTests
{
    [Fact]
    [Trait("Etapa", "25")]
    public void RampaDeCincoPorCentoDescendoParaOLeste()
    {
        var achou = TerrenoDeDrenagem.Rampa().TryGetSlope(57.3, 81.2, out var d);

        Assert.True(achou);
        Assert.Equal(0.05, d.Ratio, 9);
        Assert.Equal(5.0, d.Percent, 7);
        Assert.Equal(Math.Atan(0.05) * 180 / Math.PI, d.Degrees, 7);
        Assert.Equal(1.0, d.DownhillX, 9);
        Assert.Equal(0.0, d.DownhillY, 9);
        Assert.False(d.IsFlat(0.001));
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void PlanoHorizontalNaoTemDirecao()
    {
        var achou = TerrenoDeDrenagem.Plano().TryGetSlope(33, 44, out var d);

        Assert.True(achou);
        Assert.Equal(0.0, d.Ratio, 12);
        Assert.True(d.IsFlat(0.001));
        Assert.Equal(0.0, d.DownhillX);
        Assert.Equal(0.0, d.DownhillY);
    }

    [Theory]
    [InlineData(150, 100, 1, 0)]
    [InlineData(50, 100, -1, 0)]
    [InlineData(100, 150, 0, 1)]
    [InlineData(100, 50, 0, -1)]
    [Trait("Etapa", "25")]
    public void NoMorroAAguaDesceParaForaDoCentro(double x, double y, double ex, double ey)
    {
        var achou = TerrenoDeDrenagem.Morro().TryGetSlope(x + 0.7, y + 0.3, out var d);

        Assert.True(achou);
        // A grade de 5 m facetada desvia a direção poucos graus da radial.
        Assert.True(d.DownhillX * ex + d.DownhillY * ey > 0.95, $"direção ({d.DownhillX:0.###}, {d.DownhillY:0.###}) longe da radial ({ex}, {ey})");
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void ForaDoTerrenoNaoResponde()
    {
        Assert.False(TerrenoDeDrenagem.Rampa().TryGetSlope(-5, 50, out _));
        Assert.False(TerrenoDeDrenagem.Rampa().TryGetSlope(double.NaN, 50, out _));
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void GradienteBateComACotaDosDoisLados()
    {
        // Conferência independente da fórmula: num triângulo qualquer, a
        // cota a um passo pequeno na direção x e y bate com o gradiente.
        var tin = TerrenoDeDrenagem.Vale();

        foreach (var (x, y) in new[] { (12.3, 45.6), (151.1, 3.3), (97.2, 180.4) })
        {
            Assert.True(tin.TryGetSlope(x, y, out var d));
            Assert.True(tin.TryGetZ(x, y, out var z0));
            Assert.True(tin.TryGetZ(x + 0.01, y, out var zx));
            Assert.True(tin.TryGetZ(x, y + 0.01, out var zy));

            Assert.Equal((zx - z0) / 0.01, d.DzDx, 6);
            Assert.Equal((zy - z0) / 0.01, d.DzDy, 6);
        }
    }
}
