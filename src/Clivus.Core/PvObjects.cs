using System.Globalization;
using System.Text;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O que um objeto é no PVsyst: o material dele, com o nome que o PVcase usa.</summary>
public enum PvObjectKind
{
    /// <summary>O tronco da árvore ("Tree_trunk").</summary>
    TreeTrunk,

    /// <summary>A copa da árvore ("Tree_crown").</summary>
    TreeCrown,

    /// <summary>Um equipamento em campo: subestação, trafo, inversor, combiner ("Station").</summary>
    Station,
}

/// <summary>
/// Um objeto que faz sombra no PVsyst, como malha de triângulos.
/// </summary>
/// <param name="Id">O id da geometria no arquivo (com o GUID de quem ele é no desenho).</param>
/// <param name="Name">O nome que vai na geometria.</param>
/// <param name="Kind">O que ele é (o material).</param>
/// <param name="Vertices">Os vértices, em metro, no sistema do desenho.</param>
/// <param name="Triangles">Os triângulos, três índices cada.</param>
public sealed record PvObject(string Id, string Name, PvObjectKind Kind, IReadOnlyList<Point3> Vertices, IReadOnlyList<int> Triangles);

/// <summary>Duas fiadas que se cruzam em planta, e quanto.</summary>
public sealed record PvCrossing(PvFrame A, PvFrame B, double Area);

/// <summary>
/// Tudo o que vai para o PVsyst além das mesas (Renan, 31/10/2026: "quero
/// exportar todos objetos"), como o PVcase manda: as árvores, os
/// equipamentos em campo (a "Station" do PVcase) e o terreno num CSV ao lado.
/// A cerca o Clivus não desenha.
///
/// A árvore vai como o Clivus a calcula na sombra: dois cilindros, o tronco e
/// a copa (o "pirulito" do 9.4), com <see cref="CylinderSides"/> lados. O
/// equipamento, como a caixa do bloco dele, na posição e no giro do desenho,
/// com os triângulos na ordem das caixas do PVcase.
///
/// Só vai o que está a até <see cref="Margin"/> das mesas exportadas: o
/// escopo é das mesas, e o que está longe delas não faz sombra nelas.
/// </summary>
public static class PvObjects
{
    /// <summary>Até quanto das mesas exportadas (em planta) vão as árvores, os equipamentos e o terreno, em metro.</summary>
    public const double Margin = 100.0;

    /// <summary>Os lados do cilindro do tronco e da copa.</summary>
    public const int CylinderSides = 12;

    /// <summary>Os triângulos de uma caixa, os do PVcase: 0 a 3 a base, 4 a 7 o topo na mesma ordem.</summary>
    public static readonly IReadOnlyList<int> BoxTriangles = [0, 1, 2, 0, 2, 3, 0, 1, 5, 0, 4, 5, 1, 2, 6, 1, 5, 6, 0, 3, 7, 0, 4, 7, 2, 3, 7, 2, 6, 7, 4, 5, 6, 4, 6, 7];

    /// <summary>
    /// A árvore: o tronco e a copa, cilindros com o pé em (x, y, chão), como
    /// <see cref="TreeSpec.Cylinders"/> os dá para a sombra.
    /// </summary>
    public static IReadOnlyList<PvObject> Tree(Guid id, string name, double x, double y, double ground, TreeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var cilindros = spec.Cylinders(x, y, ground);
        return
        [
            Cylinder($"arvore-{id:N}-tronco", name, PvObjectKind.TreeTrunk, cilindros[0]),
            Cylinder($"arvore-{id:N}-copa", name, PvObjectKind.TreeCrown, cilindros[1]),
        ];
    }

    /// <summary>
    /// Um cilindro vertical como prisma de <see cref="CylinderSides"/> lados:
    /// os vértices da base, depois os do topo, os lados e as duas tampas.
    /// </summary>
    public static PvObject Cylinder(string id, string name, PvObjectKind kind, ShadowCylinder cylinder)
    {
        ArgumentNullException.ThrowIfNull(cylinder);

        const int n = CylinderSides;
        var vertices = new List<Point3>(2 * n);

        foreach (var z in new[] { cylinder.Bottom, cylinder.Top })
            for (var k = 0; k < n; k++)
            {
                var a = 2 * Math.PI * k / n;
                vertices.Add(new Point3(cylinder.X + cylinder.Radius * Math.Cos(a), cylinder.Y + cylinder.Radius * Math.Sin(a), z));
            }

        var triangulos = new List<int>(3 * (2 * n + 2 * (n - 2)));

        for (var k = 0; k < n; k++)
        {
            var b = (k + 1) % n;
            triangulos.AddRange([k, b, b + n, k, b + n, k + n]);
        }

        for (var k = 1; k < n - 1; k++)
        {
            triangulos.AddRange([0, k + 1, k]);
            triangulos.AddRange([n, n + k, n + k + 1]);
        }

        return new PvObject(id, name, kind, vertices, triangulos);
    }

    /// <summary>Uma caixa: os quatro cantos da base e os quatro do topo, na mesma ordem.</summary>
    /// <exception cref="ArgumentException">Sem quatro cantos em cada anel.</exception>
    public static PvObject Box(string id, string name, PvObjectKind kind, IReadOnlyList<Point3> bottom, IReadOnlyList<Point3> top)
    {
        if (bottom is not { Count: 4 } || top is not { Count: 4 })
            throw new ArgumentException("A caixa precisa de quatro cantos na base e quatro no topo.");

        return new PvObject(id, name, kind, [.. bottom, .. top], BoxTriangles);
    }

    /// <summary>A caixa das fiadas em planta, ou null sem fiada.</summary>
    public static (double MinX, double MinY, double MaxX, double MaxY)? Extent(IReadOnlyList<PvFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var cantos = frames.SelectMany(f => f.Corners).ToList();
        if (cantos.Count == 0) return null;

        return (cantos.Min(c => c.X), cantos.Min(c => c.Y), cantos.Max(c => c.X), cantos.Max(c => c.Y));
    }

    /// <summary>Se algum dos pontos cai na caixa alargada por <paramref name="margin"/>.</summary>
    public static bool Near((double MinX, double MinY, double MaxX, double MaxY) extent, IEnumerable<Point3> points, double margin = Margin) =>
        points.Any(p => p.X >= extent.MinX - margin && p.X <= extent.MaxX + margin && p.Y >= extent.MinY - margin && p.Y <= extent.MaxY + margin);

    /// <summary>
    /// As fiadas de mesas diferentes que se cruzam em planta (mais de
    /// <paramref name="minimumArea"/> de área em comum): o PVsyst não aceita
    /// uma mesa atravessando outra, e no campo elas não cabem. Em ordem da
    /// maior área para a menor.
    /// </summary>
    public static IReadOnlyList<PvCrossing> Crossings(IReadOnlyList<PvFrame> frames, double minimumArea = 0.01)
    {
        ArgumentNullException.ThrowIfNull(frames);

        // Relativo ao primeiro canto: em UTM a área perderia casas na conta.
        var (x0, y0) = frames.Count > 0 && frames[0].Corners.Count > 0 ? (frames[0].Corners[0].X, frames[0].Corners[0].Y) : (0.0, 0.0);
        var planos = frames.Select(f => AntiHorario(f.Corners.Select(c => (c.X - x0, c.Y - y0)).ToList())).ToList();
        var caixas = planos.Select(p => (MinX: p.Min(c => c.X), MinY: p.Min(c => c.Y), MaxX: p.Max(c => c.X), MaxY: p.Max(c => c.Y))).ToList();
        var ordem = Enumerable.Range(0, frames.Count).OrderBy(i => caixas[i].MinX).ToList();
        var cruzadas = new List<PvCrossing>();

        for (var a = 0; a < ordem.Count; a++)
        {
            var i = ordem[a];

            for (var b = a + 1; b < ordem.Count; b++)
            {
                var j = ordem[b];
                if (caixas[j].MinX > caixas[i].MaxX) break;
                if (frames[i].Table == frames[j].Table) continue;
                if (caixas[j].MinY > caixas[i].MaxY || caixas[j].MaxY < caixas[i].MinY) continue;

                var area = Area(Recortar(planos[i], planos[j]));
                if (area > minimumArea) cruzadas.Add(new PvCrossing(frames[i], frames[j], area));
            }
        }

        return cruzadas.OrderByDescending(c => c.Area).ToList();
    }

    /// <summary>
    /// Os pontos do terreno a até <paramref name="margin"/> da caixa das
    /// mesas, um por vértice do TIN (repetidos ao milímetro saem uma vez).
    /// </summary>
    public static IReadOnlyList<Point3> TerrainPoints(IEnumerable<Triangle> triangles, (double MinX, double MinY, double MaxX, double MaxY) extent, double margin = Margin)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        var vistos = new HashSet<(long, long, long)>();
        var pontos = new List<Point3>();

        foreach (var t in triangles)
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                if (!p.IsFinite || p.X < extent.MinX - margin || p.X > extent.MaxX + margin || p.Y < extent.MinY - margin || p.Y > extent.MaxY + margin) continue;
                if (vistos.Add(((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000), (long)Math.Round(p.Z * 1000)))) pontos.Add(p);
            }

        return pontos;
    }

    /// <summary>
    /// O CSV do terreno, como o PVsyst importa ("Import ground data", e como o
    /// "_terrain.csv" do PVcase): uma linha por ponto, X Y Z em metro separados
    /// por espaço, relativos à mesma origem local do PV Collada, Z para cima.
    /// </summary>
    public static string TerrainCsv(IReadOnlyList<Point3> points, Point3 origin)
    {
        ArgumentNullException.ThrowIfNull(points);

        var texto = new StringBuilder(points.Count * 28);
        foreach (var p in points)
        {
            texto.Append((p.X - origin.X).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append((p.Y - origin.Y).ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append((p.Z - origin.Z).ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
        }

        return texto.ToString();
    }

    private static List<(double X, double Y)> AntiHorario(List<(double X, double Y)> p)
    {
        var dobro = 0.0;
        for (var i = 0; i < p.Count; i++) dobro += p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y;

        if (dobro < 0) p.Reverse();
        return p;
    }

    /// <summary>O recorte de um polígono convexo por outro (Sutherland–Hodgman), os dois anti-horários.</summary>
    private static List<(double X, double Y)> Recortar(List<(double X, double Y)> sujeito, List<(double X, double Y)> recorte)
    {
        static bool Dentro((double X, double Y) p, (double X, double Y) a, (double X, double Y) b) =>
            (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X) >= 0;

        static (double X, double Y) Encontro((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) a, (double X, double Y) b)
        {
            var d = (p1.X - p2.X) * (a.Y - b.Y) - (p1.Y - p2.Y) * (a.X - b.X);
            if (Math.Abs(d) < 1e-15) return p2;

            var t = ((p1.X - a.X) * (a.Y - b.Y) - (p1.Y - a.Y) * (a.X - b.X)) / d;
            return (p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
        }

        var saida = sujeito;

        for (var i = 0; i < recorte.Count && saida.Count > 0; i++)
        {
            var a = recorte[i];
            var b = recorte[(i + 1) % recorte.Count];
            var entrada = saida;
            saida = [];
            var s = entrada[^1];

            foreach (var e in entrada)
            {
                if (Dentro(e, a, b))
                {
                    if (!Dentro(s, a, b)) saida.Add(Encontro(s, e, a, b));
                    saida.Add(e);
                }
                else if (Dentro(s, a, b))
                {
                    saida.Add(Encontro(s, e, a, b));
                }

                s = e;
            }
        }

        return saida;
    }

    private static double Area(List<(double X, double Y)> p)
    {
        if (p.Count < 3) return 0;

        var dobro = 0.0;
        for (var i = 0; i < p.Count; i++) dobro += p[i].X * p[(i + 1) % p.Count].Y - p[(i + 1) % p.Count].X * p[i].Y;

        return Math.Abs(dobro) / 2;
    }
}
