using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>A casca convexa do contorno do grupo (7.9): quadrado com miolo, colineares, poucos pontos.</summary>
public class ConvexHullTests
{
    [Fact]
    [Trait("Etapa", "7")]
    public void QuadradoComPontosNoMioloDaOsQuatroCantos()
    {
        Point3[] pontos =
        [
            new(0, 0, 5), new(10, 0, 5), new(10, 10, 5), new(0, 10, 5),
            new(5, 5, 5), new(2, 7, 5), new(9, 1, 5), new(5, 10, 5),
        ];

        var casca = ConvexHull.Of(pontos);

        Assert.Equal(4, casca.Count);
        Assert.Contains(new Point3(0, 0, 0), casca);
        Assert.Contains(new Point3(10, 0, 0), casca);
        Assert.Contains(new Point3(10, 10, 0), casca);
        Assert.Contains(new Point3(0, 10, 0), casca);

        // Anti-horária: área assinada positiva.
        var area = 0.0;
        for (var i = 0; i < casca.Count; i++)
        {
            var a = casca[i];
            var b = casca[(i + 1) % casca.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        Assert.True(area > 0);
        Assert.Equal(new Point3(5, 5, 0), ConvexHull.Centroid(casca));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ColinearesEPoucosPontos()
    {
        Assert.Equal(2, ConvexHull.Of([new Point3(0, 0, 0), new Point3(5, 5, 0), new Point3(10, 10, 0)]).Count);
        Assert.Equal(2, ConvexHull.Of([new Point3(0, 0, 0), new Point3(1, 1, 0), new Point3(0, 0, 0)]).Count);
        Assert.Single(ConvexHull.Of([new Point3(3, 3, 0)]));
        Assert.Empty(ConvexHull.Of([]));
    }
}
