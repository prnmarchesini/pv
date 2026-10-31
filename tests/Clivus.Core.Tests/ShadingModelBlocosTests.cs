using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O índice de blocos do <see cref="ShadingModel"/> (42.1, 31/10/2026): no
/// terreno ondulado o raio anda até passar da face mais alta do desenho, e o
/// bloco de 8 × 8 células inteiro abaixo dele é pulado. A conta tem de dar
/// exatamente o mesmo que seguir o raio contra todas as faces das outras mesas.
/// </summary>
public class ShadingModelBlocosTests
{
    /// <summary>Uma mesa de 10 × 2 m inclinada 20°, na cota do morro em (x, y).</summary>
    private static IEnumerable<ShadowQuad> Mesa(double x, double y, double z, int grupo)
    {
        // Cinco módulos de 2 m ao longo, a borda baixa ao norte.
        for (var i = 0; i < 5; i++)
        {
            var x0 = x + 2 * i;
            yield return new ShadowQuad([new(x0, y, z), new(x0 + 2, y, z), new(x0 + 2, y - 1.88, z + 0.68), new(x0, y - 1.88, z + 0.68)], grupo);
        }
    }

    /// <summary>A fração de cada face pela força bruta: o raio de cada ponto contra todos os triângulos das outras mesas.</summary>
    private static double[] ForcaBruta(IReadOnlyList<ShadowQuad> faces, (double X, double Y, double Z) s)
    {
        var fracoes = new double[faces.Count];

        for (var k = 0; k < faces.Count; k++)
        {
            var sombra = 0;
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var p = Ponto(faces[k].Corners, (i + 0.5) / 4, (j + 0.5) / 2);
                    if (faces.Any(f => f.Group != faces[k].Group && (Bate(p, s, f.Corners[0], f.Corners[1], f.Corners[2]) || Bate(p, s, f.Corners[0], f.Corners[2], f.Corners[3]))))
                        sombra++;
                }
            }

            fracoes[k] = sombra / 8.0;
        }

        return fracoes;

        static Point3 Ponto(IReadOnlyList<Point3> f, double u, double v)
        {
            Point3 L(Point3 a, Point3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
            return L(L(f[0], f[1], u), L(f[3], f[2], u), v);
        }

        static bool Bate(Point3 p, (double X, double Y, double Z) s, Point3 a, Point3 b, Point3 c)
        {
            var (e1x, e1y, e1z) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            var (e2x, e2y, e2z) = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            var (hx, hy, hz) = (s.Y * e2z - s.Z * e2y, s.Z * e2x - s.X * e2z, s.X * e2y - s.Y * e2x);
            var det = e1x * hx + e1y * hy + e1z * hz;
            if (Math.Abs(det) < 1e-12) return false;

            var inv = 1 / det;
            var (tx, ty, tz) = (p.X - a.X, p.Y - a.Y, p.Z - a.Z);
            var u = (tx * hx + ty * hy + tz * hz) * inv;
            if (u < 0 || u > 1) return false;

            var (qx, qy, qz) = (ty * e1z - tz * e1y, tz * e1x - tx * e1z, tx * e1y - ty * e1x);
            var v = (s.X * qx + s.Y * qy + s.Z * qz) * inv;
            if (v < 0 || u + v > 1) return false;

            return (e2x * qx + e2y * qy + e2z * qz) * inv > 1e-4;
        }
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void NoTerrenoOnduladoOsBlocosDaoOMesmoQueAForcaBruta()
    {
        // 8 × 8 mesas num morro de 40 m de amplitude: o raio anda longe.
        static double Morro(double x, double y) => 700 + 20 * Math.Sin(x / 37.0) * Math.Cos(y / 29.0);

        var faces = new List<ShadowQuad>();
        var grupo = 0;
        for (var i = 0; i < 8; i++)
            for (var j = 0; j < 8; j++)
            {
                var (x, y) = (i * 13.0, j * 6.5);
                faces.AddRange(Mesa(x, y, Morro(x, y) + 0.8, grupo++));
            }

        var modelo = new ShadingModel(faces, []);
        var aleatorio = new Random(42);
        var comparados = 0;

        for (var n = 0; n < 12; n++)
        {
            var sol = new SunPosition(aleatorio.NextDouble() * 360, 4 + aleatorio.NextDouble() * 40, 0, 0);
            var esperado = ForcaBruta(faces, sol.Direction);
            var obtido = modelo.At(sol).Fractions;

            for (var k = 0; k < faces.Count; k++)
            {
                Assert.True(esperado[k] == obtido[k], $"sol {sol.AzimuthDegrees:0.0}°/{sol.ElevationDegrees:0.0}°, face {k}: força bruta {esperado[k]}, modelo {obtido[k]}");
                if (esperado[k] > 0) comparados++;
            }
        }

        // A cena tem sombra de verdade (senão o teste não provaria nada).
        Assert.True(comparados > 50, $"só {comparados} face(s) com sombra");
    }
}
