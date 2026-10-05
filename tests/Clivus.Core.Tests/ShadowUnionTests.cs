namespace Clivus.Core.Tests;

/// <summary>A mancha da sombra ao longo do período: a união dos contornos de cada passo (05/10/2026).</summary>
public class ShadowUnionTests
{
    private static IReadOnlyList<(double X, double Y)> Quadrado(double x, double y, double lado) =>
        [(x, y), (x + lado, y), (x + lado, y + lado), (x, y + lado)];

    [Fact]
    [Trait("Etapa", "9")]
    public void DoisQuadradosSobrepostosViramUmaBordaSo()
    {
        var uniao = ShadowUnion.Union([Quadrado(0, 0, 2), Quadrado(1, 0, 2)]);

        var anel = Assert.Single(uniao);
        Assert.Equal(6, ShadowUnion.Area(anel), 3);   // 3 x 2
        Assert.Equal(4, anel.Count);                   // as linhas do meio saem
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void PassosEmEscadaDaoABordaComDentes()
    {
        // A sombra andando a cada passo: quadrados em diagonal, sobrepostos.
        var passos = Enumerable.Range(0, 6).Select(i => Quadrado(i * 0.5, i * 0.5, 1)).ToList();

        var anel = Assert.Single(ShadowUnion.Union(passos));

        // A borda tem os degraus (dentes), não o retângulo que envolve tudo.
        Assert.True(anel.Count > 8, $"só {anel.Count} vértices");
        Assert.True(ShadowUnion.Area(anel) < 3.5 * 3.5);
        Assert.Equal(6 * 1 - 5 * 0.25, ShadowUnion.Area(anel), 3);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void SombrasSeparadasFicamSeparadasEVazioNaoDesenha()
    {
        Assert.Equal(2, ShadowUnion.Union([Quadrado(0, 0, 1), Quadrado(5, 5, 1)]).Count);
        Assert.Empty(ShadowUnion.Union([]));
        Assert.Empty(ShadowUnion.Union([[(0, 0), (1, 1)]]));
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ABordaDensificadaTemPassoCurtoEPassaPelosCantos()
    {
        var anel = ShadowUnion.Densify(Quadrado(0, 0, 10), 1);

        Assert.Equal(40, anel.Count);
        Assert.Contains((10.0, 0.0), anel);
        Assert.All(anel.Zip(anel.Skip(1).Append(anel[0])), par =>
            Assert.True(Math.Sqrt(Math.Pow(par.Second.X - par.First.X, 2) + Math.Pow(par.Second.Y - par.First.Y, 2)) <= 1 + 1e-9));
    }
}
