using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// A casca convexa em planta de um punhado de pontos (cadeia monótona de
/// Andrew): o contorno que o grupo de mesas ganha no desenho (7.9, pedido
/// do Renan em 26/09/2026: "um contorno com um hatch, igual o PVcase").
/// Z é ignorado; o resultado é anti-horário, sem o primeiro ponto repetido.
/// </summary>
public static class ConvexHull
{
    /// <summary>A casca. Menos de três pontos distintos: os próprios pontos distintos, na ordem.</summary>
    public static IReadOnlyList<Point3> Of(IEnumerable<Point3> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var pontos = points
            .Where(p => p.IsFinite)
            .Select(p => new Point3(p.X, p.Y, 0))
            .Distinct()
            .OrderBy(p => p.X).ThenBy(p => p.Y)
            .ToList();

        if (pontos.Count < 3) return pontos;

        var baixo = new List<Point3>();

        foreach (var p in pontos)
        {
            while (baixo.Count >= 2 && Cruz(baixo[^2], baixo[^1], p) <= 0) baixo.RemoveAt(baixo.Count - 1);
            baixo.Add(p);
        }

        var cima = new List<Point3>();

        for (var i = pontos.Count - 1; i >= 0; i--)
        {
            var p = pontos[i];
            while (cima.Count >= 2 && Cruz(cima[^2], cima[^1], p) <= 0) cima.RemoveAt(cima.Count - 1);
            cima.Add(p);
        }

        baixo.RemoveAt(baixo.Count - 1);
        cima.RemoveAt(cima.Count - 1);

        var casca = baixo.Concat(cima).ToList();

        // Todos colineares: a cadeia degenera em dois pontos.
        return casca.Count >= 3 ? casca : [pontos[0], pontos[^1]];
    }

    /// <summary>O centro da casca (a média dos vértices).</summary>
    public static Point3 Centroid(IReadOnlyList<Point3> hull)
    {
        ArgumentNullException.ThrowIfNull(hull);

        if (hull.Count == 0) throw new ArgumentException("A casca não tem pontos.", nameof(hull));

        return new Point3(hull.Average(p => p.X), hull.Average(p => p.Y), 0);
    }

    private static double Cruz(Point3 o, Point3 a, Point3 b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}
