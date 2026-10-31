using Clipper2Lib;

namespace Clivus.Core;

/// <summary>
/// Os polígonos das sombras na Clipper2, em milímetros (a mesma escala de
/// <see cref="ShadowUnion"/>): de anel em metro para caminho inteiro e de
/// volta. Um lugar só para a conversão que a sombra do relevo, os pedaços do
/// chão e o escopo usam.
/// </summary>
internal static class ShadowPaths
{
    /// <summary>Metro para milímetro.</summary>
    internal const double Scale = 1000;

    /// <summary>O anel em milímetros (menos de 3 pontos: caminho vazio).</summary>
    internal static Path64 Path(IReadOnlyList<(double X, double Y)> ring)
    {
        var caminho = new Path64(ring.Count);
        foreach (var (x, y) in ring)
            if (double.IsFinite(x) && double.IsFinite(y)) caminho.Add(new Point64(Math.Round(x * Scale), Math.Round(y * Scale)));

        return caminho;
    }

    /// <summary>Os anéis em milímetros, sem os que não fecham área.</summary>
    internal static Paths64 Paths(IEnumerable<IReadOnlyList<(double X, double Y)>> rings)
    {
        var caminhos = new Paths64();
        foreach (var anel in rings)
        {
            var caminho = Path(anel);
            if (caminho.Count >= 3) caminhos.Add(caminho);
        }

        return caminhos;
    }

    /// <summary>De volta a metro: só os caminhos com área (mais de <paramref name="minArea"/> mm²).</summary>
    internal static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Rings(Paths64 paths, double minArea = 1) =>
        paths
            .Where(c => c.Count >= 3 && Math.Abs(Clipper.Area(c)) > minArea)
            .Select(c => (IReadOnlyList<(double X, double Y)>)c.Select(p => (p.X / Scale, p.Y / Scale)).ToList())
            .ToList();

    /// <summary>O retângulo de (x0, y0) a (x1, y1), em milímetros, anti-horário.</summary>
    internal static Path64 Rect(double x0, double y0, double x1, double y1)
    {
        var (a, b) = ((long)Math.Round(x0 * Scale), (long)Math.Round(y0 * Scale));
        var (c, d) = ((long)Math.Round(x1 * Scale), (long)Math.Round(y1 * Scale));
        return [new Point64(a, b), new Point64(c, b), new Point64(c, d), new Point64(a, d)];
    }

    /// <summary>A caixa (em metro) dos caminhos; null sem ponto.</summary>
    internal static (double MinX, double MinY, double MaxX, double MaxY)? Bounds(Paths64 paths)
    {
        if (paths.Count == 0 || paths.All(c => c.Count == 0)) return null;

        var r = Clipper.GetBounds(paths);
        return (r.left / Scale, r.top / Scale, r.right / Scale, r.bottom / Scale);
    }
}
