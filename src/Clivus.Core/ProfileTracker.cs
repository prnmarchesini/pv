namespace Clivus.Core;

/// <summary>Um ponto do caminho da linha: a distância no gráfico, a posição em planta, a cota do terreno e a do fundo (canaleta; NaN na linha).</summary>
public readonly record struct TrackPoint(double Distance, double X, double Y, double Terrain, double Bottom);

/// <summary>Um gráfico no desenho, com a linha dele: o que o rastreio precisa.</summary>
/// <param name="Origin">O canto de baixo à esquerda da moldura, em planta.</param>
/// <param name="Path">O caminho da linha no sentido do gráfico.</param>
public sealed record TrackedChart(Guid Id, string Name, (double X, double Y) Origin, ChartDrawing Chart, IReadOnlyList<TrackPoint> Path);

/// <summary>Onde o mouse está, nos dois lados: a distância, o ponto em planta e o ponto no gráfico.</summary>
public readonly record struct TrackHit(TrackedChart Chart, double Distance, double PlanX, double PlanY, double ChartX, double ChartY, double Terrain, double Bottom);

/// <summary>
/// O rastreio planta ↔ gráfico (32.2), como o Civil 3D faz entre o perfil e o
/// alinhamento: mouse no gráfico → o ponto da linha em planta; mouse na linha
/// em planta → o ponto no gráfico. Só a conta; o plugin desenha a seta.
/// </summary>
public static class ProfileTracker
{
    /// <summary>Mouse dentro do retângulo do gráfico: o ponto da linha naquela distância.</summary>
    public static TrackHit? FromChart(TrackedChart chart, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart.Path.Count < 2) return null;

        var (minX, minY, maxX, maxY) = chart.Chart.Bounds;
        var (ox, oy) = chart.Origin;
        if (x < ox + minX || x > ox + maxX || y < oy + minY || y > oy + maxY) return null;

        var d = Math.Clamp(x - ox, chart.Path[0].Distance, chart.Path[^1].Distance);
        return Hit(chart, d);
    }

    /// <summary>Mouse a menos de <paramref name="tolerance"/> da linha em planta: o mesmo ponto no gráfico.</summary>
    public static TrackHit? FromPlan(TrackedChart chart, double x, double y, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(chart);

        double melhor = double.MaxValue, distancia = 0;

        for (var i = 1; i < chart.Path.Count; i++)
        {
            var (a, b) = (chart.Path[i - 1], chart.Path[i]);
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var l2 = dx * dx + dy * dy;
            var t = l2 < 1e-18 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
            var (px, py) = (a.X + dx * t, a.Y + dy * t);
            var e = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));

            if (e < melhor)
            {
                melhor = e;
                distancia = a.Distance + (b.Distance - a.Distance) * t;
            }
        }

        return melhor <= tolerance ? Hit(chart, distancia) : null;
    }

    /// <summary>O ponto da distância dada, nos dois lados.</summary>
    public static TrackHit Hit(TrackedChart chart, double distance)
    {
        var p = chart.Path;
        var k = 1;
        while (k < p.Count - 1 && p[k].Distance < distance) k++;

        var (a, b) = (p[k - 1], p[k]);
        var t = b.Distance > a.Distance ? Math.Clamp((distance - a.Distance) / (b.Distance - a.Distance), 0, 1) : 0;

        double L(double u, double v) => u + (v - u) * t;

        var terreno = L(a.Terrain, b.Terrain);
        var fundo = double.IsNaN(a.Bottom) ? double.NaN : L(a.Bottom, b.Bottom);
        var serie = double.IsNaN(fundo) ? terreno : fundo;

        return new TrackHit(chart, distance, L(a.X, b.X), L(a.Y, b.Y),
            chart.Origin.X + distance, chart.Origin.Y + chart.Chart.YOf(serie), terreno, fundo);
    }
}
