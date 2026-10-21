using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Os cruzamentos da canaleta com as vias (31.6): onde entra a transposição.
/// O toque conta (vértice encaixado na via com OSNAP), sem contar duas vezes
/// o vértice comum de dois trechos.
/// </summary>
public static class RoadCrossings
{
    /// <summary>Onde a canaleta cruza as vias, em planta, com a direção da canaleta ali.</summary>
    public static List<(double X, double Y, double DirX, double DirY)> Find(IReadOnlyList<Point3> canaleta, IReadOnlyList<IReadOnlyList<Point3>> vias)
    {
        var pontos = new List<(double, double, double, double)>();

        for (var i = 1; i < canaleta.Count; i++)
        {
            var (a, b) = (canaleta[i - 1], canaleta[i]);

            var n = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (n < 1e-9) continue;

            foreach (var via in vias)
            {
                for (var j = 1; j < via.Count; j++)
                {
                    // O toque conta (vértice da canaleta encaixado na via): a
                    // ponta de começo de cada trecho entra, a de fim não (a não
                    // ser no último), para o vértice comum não contar duas vezes.
                    var (c, d) = (via[j - 1], via[j]);
                    var den = (b.X - a.X) * (d.Y - c.Y) - (b.Y - a.Y) * (d.X - c.X);
                    if (Math.Abs(den) < 1e-12) continue;

                    var t = ((c.X - a.X) * (d.Y - c.Y) - (c.Y - a.Y) * (d.X - c.X)) / den;
                    var u = ((c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X)) / den;
                    const double folga = 1e-9;

                    var dentroT = t >= -folga && (t < 1 - folga || (i == canaleta.Count - 1 && t <= 1 + folga));
                    var dentroU = u >= -folga && (u < 1 - folga || (j == via.Count - 1 && u <= 1 + folga));
                    if (!dentroT || !dentroU) continue;

                    pontos.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, (b.X - a.X) / n, (b.Y - a.Y) / n));
                }
            }
        }

        return pontos;
    }
}
