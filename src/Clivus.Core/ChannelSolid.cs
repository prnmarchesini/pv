using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// A geometria 3D da canaleta (passo 29.1): uma sequência de seções (anéis em
/// forma de U: a parede de concreto, contorno externo e interno) em planos
/// verticais ao longo do eixo, com o fundo interno na cota do fundo. O plugin
/// liga cada par de anéis num sólido (loft regrado) e une tudo.
///
/// Só entram as seções que mudam alguma coisa: as pontas, as quebras do
/// traçado em planta e as quebras do fundo que passam de uma tolerância. Uma
/// canaleta de 300 m com um vértice a cada aresta de triângulo vira dezenas
/// de seções, não centenas.
/// </summary>
public static class ChannelSolid
{
    /// <summary>Tolerância da simplificação do fundo, em m.</summary>
    public const double BottomTolerance = 0.02;

    /// <summary>Acima deste ângulo (em graus) entre dois trechos em planta, é quebra do traçado.</summary>
    public const double PlanTurnDegrees = 0.5;

    /// <summary>
    /// Os anéis da canaleta. <paramref name="points"/> é o perfil (posição em
    /// planta e cota do fundo, da montante à jusante). Na quebra do traçado o
    /// anel fica no plano da bissetriz, com a largura esticada por 1/cos(θ/2)
    /// para a parede manter a espessura dos dois lados.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Point3>> Rings(IReadOnlyList<ChannelProfilePoint> points, ChannelSection section, int arcSegments = 12)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(section);

        var escolhidos = Simplify(points);
        if (escolhidos.Count < 2) return [];

        var forma = UShape(section, arcSegments);
        var aneis = new List<IReadOnlyList<Point3>>(escolhidos.Count);

        for (var k = 0; k < escolhidos.Count; k++)
        {
            var p = escolhidos[k];

            // A direção em planta antes e depois do ponto.
            var antes = k > 0 ? Direcao(escolhidos[k - 1], p) : Direcao(p, escolhidos[k + 1]);
            var depois = k < escolhidos.Count - 1 ? Direcao(p, escolhidos[k + 1]) : antes;

            var (tx, ty) = (antes.X + depois.X, antes.Y + depois.Y);
            var norma = Math.Sqrt(tx * tx + ty * ty);
            if (norma < 1e-9) (tx, ty, norma) = (depois.X, depois.Y, 1);
            (tx, ty) = (tx / norma, ty / norma);

            // O eixo da largura: perpendicular à bissetriz, para a esquerda de quem desce.
            var (lx, ly) = (-ty, tx);

            // cos(θ/2) entre a bissetriz e cada trecho: estica a largura na quebra.
            var cosMeio = Math.Max(0.2, antes.X * tx + antes.Y * ty);
            var estica = 1 / cosMeio;

            aneis.Add(forma.Select(q => new Point3(p.X + lx * q.X * estica, p.Y + ly * q.X * estica, p.Bottom + q.Y)).ToList());
        }

        return aneis;
    }

    /// <summary>
    /// O U da parede em coordenadas (largura, altura), fechado, sem repetir o
    /// primeiro ponto: o contorno externo da borda esquerda à direita, depois o
    /// interno de volta.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> UShape(ChannelSection section, int arcSegments = 12)
    {
        var externo = section.OuterOutline(arcSegments);
        var interno = section.InnerOutline(arcSegments);
        return [.. externo, .. interno.Reverse()];
    }

    /// <summary>As seções que importam: pontas, quebras em planta e quebras do fundo além da tolerância.</summary>
    public static IReadOnlyList<ChannelProfilePoint> Simplify(IReadOnlyList<ChannelProfilePoint> points)
    {
        if (points.Count <= 2) return points;

        var manter = new bool[points.Count];
        manter[0] = manter[^1] = true;

        // Quebras do traçado em planta.
        var limite = Math.Cos(PlanTurnDegrees * Math.PI / 180);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var a = Direcao(points[i - 1], points[i]);
            var b = Direcao(points[i], points[i + 1]);
            if (a.X * b.X + a.Y * b.Y < limite) manter[i] = true;
        }

        // Douglas-Peucker do fundo (distância × cota) entre as quebras mantidas.
        var inicio = 0;
        for (var i = 1; i < points.Count; i++)
        {
            if (!manter[i]) continue;
            Douglas(points, inicio, i, manter);
            inicio = i;
        }

        return points.Where((_, i) => manter[i]).ToList();
    }

    private static void Douglas(IReadOnlyList<ChannelProfilePoint> p, int a, int b, bool[] manter)
    {
        if (b - a < 2) return;

        var (d0, z0, d1, z1) = (p[a].Distance, p[a].Bottom, p[b].Distance, p[b].Bottom);
        var pior = -1;
        var maior = BottomTolerance;

        for (var i = a + 1; i < b; i++)
        {
            var t = d1 > d0 ? (p[i].Distance - d0) / (d1 - d0) : 0;
            var erro = Math.Abs(p[i].Bottom - (z0 + (z1 - z0) * t));
            if (erro > maior) (maior, pior) = (erro, i);
        }

        if (pior < 0) return;

        manter[pior] = true;
        Douglas(p, a, pior, manter);
        Douglas(p, pior, b, manter);
    }

    private static (double X, double Y) Direcao(ChannelProfilePoint a, ChannelProfilePoint b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var n = Math.Sqrt(dx * dx + dy * dy);
        return n < 1e-12 ? (1, 0) : (dx / n, dy / n);
    }

    /// <summary>
    /// Os triângulos da superfície fechada da canaleta (as faces entre anéis
    /// vizinhos e as duas tampas), para o Ver 3D e para o desenho em malha
    /// quando o sólido não sai.
    /// </summary>
    public static IReadOnlyList<(Point3 A, Point3 B, Point3 C)> Triangles(IReadOnlyList<IReadOnlyList<Point3>> rings)
    {
        var triangulos = new List<(Point3, Point3, Point3)>();
        if (rings.Count < 2) return triangulos;

        var n = rings[0].Count;

        for (var k = 0; k < rings.Count - 1; k++)
        {
            var (a, b) = (rings[k], rings[k + 1]);
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                triangulos.Add((a[i], b[i], b[j]));
                triangulos.Add((a[i], b[j], a[j]));
            }
        }

        // As tampas: o U é côncavo, então em leque a partir de cada lado da
        // parede (a metade externa e a interna se correspondem ponto a ponto).
        foreach (var anel in new[] { rings[0], rings[^1] })
        {
            var metade = n / 2;
            for (var i = 0; i < metade - 1; i++)
            {
                var (e0, e1) = (anel[i], anel[i + 1]);
                var (i0, i1) = (anel[n - 1 - i], anel[n - 2 - i]);
                triangulos.Add((e0, e1, i1));
                triangulos.Add((e0, i1, i0));
            }
        }

        return triangulos;
    }
}
