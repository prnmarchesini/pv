namespace Clivus.Geo;

/// <summary>
/// O traçado 3D de uma string sobre os módulos (elétrica, 11.7; regra
/// elétrica 5): módulo a módulo, pelo centro da face de cada um, sempre a
/// <c>lift</c> metro acima do PLANO do módulo que a linha está cruzando.
/// Nunca uma reta entre dois pontos distantes, que enterraria num módulo
/// mais alto no caminho ou flutuaria sobre um mais baixo (o defeito do
/// PVcase): onde a linha passa de uma face para outra (borda de módulo, vão
/// entre mesas de inclinações diferentes) entra um vértice, na cota da face
/// de cada lado. Dentro de uma face o trecho é reto no plano dela.
///
/// As faces são quadriláteros planos e convexos (a mesa é monolito, regra
/// sagrada 2), dados pelos quatro cantos 3D.
/// </summary>
public static class StringPath
{
    /// <summary>Quanto o traçado fica acima do plano do módulo, em metro.</summary>
    public const double DefaultLift = 0.05;

    /// <summary>Tolerância de "em cima da borda", em metro.</summary>
    private const double Borda = 1e-6;

    /// <summary>
    /// O traçado.
    /// </summary>
    /// <param name="caminho">As faces dos módulos da string, na ordem elétrica (do + ao −).</param>
    /// <param name="faces">
    /// Todas as faces por onde a linha pode passar (as das mesas do grupo):
    /// são as que dão a cota no meio do caminho. As do caminho podem estar
    /// ou não; entram de qualquer jeito.
    /// </param>
    /// <param name="lift">A altura acima do plano do módulo.</param>
    /// <returns>Os vértices, o primeiro no centro do módulo do + e o último no do −.</returns>
    /// <exception cref="ArgumentException">Caminho vazio, ou face que não é um quadrilátero finito e não vertical.</exception>
    public static IReadOnlyList<Point3> Build(IReadOnlyList<IReadOnlyList<Point3>> caminho, IReadOnlyList<IReadOnlyList<Point3>> faces, double lift = DefaultLift)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        ArgumentNullException.ThrowIfNull(faces);
        if (caminho.Count == 0) throw new ArgumentException("O caminho da string está vazio.", nameof(caminho));
        if (!double.IsFinite(lift) || lift < 0) throw new ArgumentOutOfRangeException(nameof(lift));

        var planos = caminho.Concat(faces).Select(f => new Plano(f)).DistinctBy(p => p.Chave).ToList();
        var centros = caminho.Select(f => new Plano(f)).Select(p => p.Centro(lift)).ToList();
        var todosOsCentros = planos.Select(p => p.Centro(0)).ToList();

        // Cada vértice leva a marca de centro de módulo: esses ficam sempre.
        var pontos = new List<(Point3 P, bool Centro)> { (centros[0], true) };

        for (var i = 1; i < centros.Count; i++)
        {
            var a = centros[i - 1];
            var b = centros[i];

            // A ligação que pula módulo (leapfrog) é um arco no plano dos
            // módulos, não uma reta sobre o pulado (05/10/2026, Renan: "a
            // simbologia precisa ser de leapfrog também, igual no
            // configurador"): o arco sai para a esquerda do sentido da
            // ligação, então a ida e a volta ficam em lados opostos.
            var lado = LadoCurto(caminho[i - 1]);
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var distancia = Math.Sqrt(dx * dx + dy * dy);

            // Pulo é quando a reta passaria por cima do centro de outro
            // módulo (o pulado); vizinho, subida de fileira e vão entre
            // mesas não são pulo.
            if (distancia > 1e-9 && PassaPorCentro(a, b, todosOsCentros, 0.25 * lado))
            {
                var (nx, ny) = (-dy / distancia, dx / distancia);
                var (cx, cy) = ((a.X + b.X) / 2 + nx * ArcoDoPulo * lado, (a.Y + b.Y) / 2 + ny * ArcoDoPulo * lado);

                for (var k = 1; k < PontosDoArco; k++)
                {
                    var t = (double)k / PontosDoArco;
                    var x = (1 - t) * (1 - t) * a.X + 2 * (1 - t) * t * cx + t * t * b.X;
                    var y = (1 - t) * (1 - t) * a.Y + 2 * (1 - t) * t * cy + t * t * b.Y;
                    var z = Cota(planos, x, y, lift) ?? a.Z + (b.Z - a.Z) * t;
                    pontos.Add((new Point3(x, y, z), false));
                }

                pontos.Add((b, true));
                continue;
            }

            var ts = new SortedSet<double>();
            foreach (var plano in planos)
                foreach (var t in plano.Cruzamentos(a, b))
                    if (t > Borda && t < 1 - Borda) ts.Add(Math.Round(t, 9));

            foreach (var t in ts)
            {
                var x = a.X + (b.X - a.X) * t;
                var y = a.Y + (b.Y - a.Y) * t;
                var z = Cota(planos, x, y, lift) ?? a.Z + (b.Z - a.Z) * t;
                pontos.Add((new Point3(x, y, z), false));
            }

            pontos.Add((b, true));
        }

        return Enxugar(pontos);
    }

    /// <summary>O quanto o arco do pulo se afasta da reta: a flecha é metade disto, em lados curtos do módulo.</summary>
    private const double ArcoDoPulo = 0.5;

    /// <summary>Quantos trechos retos fazem o arco do pulo.</summary>
    private const int PontosDoArco = 10;

    /// <summary>Se a reta de a até b (em planta) passa a menos de <paramref name="folga"/> do centro de algum módulo no meio do caminho.</summary>
    private static bool PassaPorCentro(Point3 a, Point3 b, IReadOnlyList<Point3> centros, double folga)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var comprimento2 = dx * dx + dy * dy;

        foreach (var c in centros)
        {
            var t = ((c.X - a.X) * dx + (c.Y - a.Y) * dy) / comprimento2;
            if (t <= 0.1 || t >= 0.9) continue;

            var px = a.X + dx * t - c.X;
            var py = a.Y + dy * t - c.Y;
            if (px * px + py * py < folga * folga) return true;
        }

        return false;
    }

    /// <summary>O lado curto do módulo, em planta (a largura ao longo da fileira).</summary>
    private static double LadoCurto(IReadOnlyList<Point3> f)
    {
        double D(Point3 p, Point3 q) => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
        return Math.Min(D(f[0], f[1]), D(f[1], f[2]));
    }

    /// <summary>A cota do traçado em (x, y): o plano mais alto entre as faces que contêm o ponto, mais a altura; null fora de toda face.</summary>
    public static double? Cota(IReadOnlyList<IReadOnlyList<Point3>> faces, double x, double y, double lift = DefaultLift) =>
        Cota(faces.Select(f => new Plano(f)).ToList(), x, y, lift);

    private static double? Cota(IReadOnlyList<Plano> planos, double x, double y, double lift)
    {
        double? maior = null;
        foreach (var plano in planos)
            if (plano.Contem(x, y) && (maior is null || plano.Z(x, y) > maior)) maior = plano.Z(x, y);

        return maior + lift;
    }

    /// <summary>Tira os vértices de cruzamento que ficaram em linha reta com os vizinhos (borda entre módulos do mesmo plano).</summary>
    private static List<Point3> Enxugar(List<(Point3 P, bool Centro)> pontos)
    {
        var saida = new List<(Point3 P, bool Centro)>();

        foreach (var p in pontos)
        {
            if (saida.Count > 0 && Distancia(saida[^1].P, p.P) < 1e-6)
            {
                if (p.Centro) saida[^1] = p;
                continue;
            }

            saida.Add(p);
        }

        for (var i = saida.Count - 2; i >= 1; i--)
            if (!saida[i].Centro && DistanciaAoSegmento(saida[i].P, saida[i - 1].P, saida[i + 1].P) < 1e-6) saida.RemoveAt(i);

        return saida.Select(p => p.P).ToList();
    }

    private static double Distancia(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static double DistanciaAoSegmento(Point3 p, Point3 a, Point3 b)
    {
        var (dx, dy, dz) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var l2 = dx * dx + dy * dy + dz * dz;
        if (l2 < 1e-18) return Distancia(p, a);

        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy + (p.Z - a.Z) * dz) / l2, 0, 1);
        return Distancia(p, new Point3(a.X + dx * t, a.Y + dy * t, a.Z + dz * t));
    }

    /// <summary>Uma face: o quadrilátero em planta e o plano dele.</summary>
    private sealed class Plano
    {
        private readonly Point3[] _c;
        private readonly double _nx, _ny, _nz, _d;
        private readonly double _x0, _x1, _y0, _y1;
        private readonly double _sentido;

        internal Plano(IReadOnlyList<Point3> cantos)
        {
            if (cantos is null || cantos.Count != 4 || cantos.Any(c => !c.IsFinite))
                throw new ArgumentException("A face do módulo precisa de quatro cantos finitos.", nameof(cantos));

            _c = [.. cantos];

            // O plano pela média das duas diagonais (robusto a um canto
            // levemente fora, mas a face é plana).
            var u = (X: _c[2].X - _c[0].X, Y: _c[2].Y - _c[0].Y, Z: _c[2].Z - _c[0].Z);
            var v = (X: _c[3].X - _c[1].X, Y: _c[3].Y - _c[1].Y, Z: _c[3].Z - _c[1].Z);
            _nx = u.Y * v.Z - u.Z * v.Y;
            _ny = u.Z * v.X - u.X * v.Z;
            _nz = u.X * v.Y - u.Y * v.X;
            if (Math.Abs(_nz) < 1e-12) throw new ArgumentException("A face do módulo está em pé (vertical).", nameof(cantos));

            var cx = _c.Average(c => c.X);
            var cy = _c.Average(c => c.Y);
            var cz = _c.Average(c => c.Z);
            _d = -(_nx * cx + _ny * cy + _nz * cz);

            _x0 = _c.Min(c => c.X);
            _x1 = _c.Max(c => c.X);
            _y0 = _c.Min(c => c.Y);
            _y1 = _c.Max(c => c.Y);

            // A orientação dos cantos em planta (horário ou anti-horário).
            var area = 0.0;
            for (var i = 0; i < 4; i++) area += _c[i].X * _c[(i + 1) % 4].Y - _c[(i + 1) % 4].X * _c[i].Y;
            _sentido = Math.Sign(area);

            Chave = string.Join(';', _c.Select(c => $"{c.X:R},{c.Y:R},{c.Z:R}"));
        }

        internal string Chave { get; }

        internal double Z(double x, double y) => -(_nx * x + _ny * y + _d) / _nz;

        internal Point3 Centro(double lift)
        {
            var x = _c.Average(c => c.X);
            var y = _c.Average(c => c.Y);
            return new Point3(x, y, Z(x, y) + lift);
        }

        /// <summary>Se (x, y) está dentro do quadrilátero em planta, borda inclusive (convexo).</summary>
        internal bool Contem(double x, double y)
        {
            if (x < _x0 - Borda || x > _x1 + Borda || y < _y0 - Borda || y > _y1 + Borda || _sentido == 0) return false;

            for (var i = 0; i < 4; i++)
            {
                var a = _c[i];
                var b = _c[(i + 1) % 4];
                var l = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                var lado = ((b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X)) / Math.Max(l, 1e-12);
                if (lado * _sentido < -Borda) return false;
            }

            return true;
        }

        /// <summary>Os parâmetros t (0 a 1) em que o segmento a→b, em planta, cruza a borda do quadrilátero.</summary>
        internal IEnumerable<double> Cruzamentos(Point3 a, Point3 b)
        {
            if (Math.Max(a.X, b.X) < _x0 - Borda || Math.Min(a.X, b.X) > _x1 + Borda || Math.Max(a.Y, b.Y) < _y0 - Borda || Math.Min(a.Y, b.Y) > _y1 + Borda)
                yield break;

            var (rx, ry) = (b.X - a.X, b.Y - a.Y);

            for (var i = 0; i < 4; i++)
            {
                var p = _c[i];
                var q = _c[(i + 1) % 4];
                var (sx, sy) = (q.X - p.X, q.Y - p.Y);
                var den = rx * sy - ry * sx;
                if (Math.Abs(den) < 1e-15) continue;   // paralelos: as pontas da outra borda dão o cruzamento

                var t = ((p.X - a.X) * sy - (p.Y - a.Y) * sx) / den;
                var s = ((p.X - a.X) * ry - (p.Y - a.Y) * rx) / den;
                if (t >= 0 && t <= 1 && s >= -1e-12 && s <= 1 + 1e-12) yield return t;
            }
        }
    }
}
