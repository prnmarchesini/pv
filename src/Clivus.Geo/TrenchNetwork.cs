namespace Clivus.Geo;

/// <summary>Um ponto sobre a rede de valas: em que trecho (aresta) e a que distância do ponto procurado.</summary>
public readonly record struct TrenchPoint(Point3 At, int Edge, double Distance);

/// <summary>
/// As valas de uma rota (roteamento, 17.5) como uma rede em planta: as
/// polilinhas que o usuário desenhou, ligadas onde se cruzam ou onde a ponta
/// de uma encosta (até <see cref="Snap"/>) em outra. Responde as três
/// perguntas do roteamento: a vala mais perto de um ponto dentro de um raio,
/// onde uma linha reta bate na primeira vala, e o caminho mais curto pela
/// rede entre dois pontos dela. Só XY; a cota é de quem desenha o cabo.
/// O traçado em planta é sempre o do usuário; a cota (a vala assentada no
/// terreno, 10/10/2026) é de quem desenha.
/// </summary>
public sealed class TrenchNetwork
{
    /// <summary>Ponta de vala a até esta distância de outra vala fica ligada a ela (m).</summary>
    public const double Snap = 0.5;

    private const double Eps = 1e-9;

    private readonly List<Point3> _nos = [];
    private readonly List<(int A, int B)> _arestas = [];
    private readonly List<List<(int Aresta, int Vizinho)>> _ligacoes = [];

    /// <summary>Quantos trechos retos a rede tem (depois de partida nos cruzamentos).</summary>
    public int EdgeCount => _arestas.Count;

    public TrenchNetwork(IEnumerable<IReadOnlyList<Point3>> trenches)
    {
        ArgumentNullException.ThrowIfNull(trenches);

        // Os trechos originais (só XY), sem os de comprimento zero.
        var trechos = new List<(Point3 A, Point3 B)>();
        foreach (var vala in trenches)
            for (var i = 0; i + 1 < vala.Count; i++)
            {
                var a = Plano(vala[i]);
                var b = Plano(vala[i + 1]);
                if (Dist(a, b) > Eps) trechos.Add((a, b));
            }

        // Onde cada trecho é partido: as próprias pontas, os cruzamentos com
        // outros trechos e as pontas de outros trechos que encostam nele.
        var cortes = trechos.Select(_ => new List<double> { 0, 1 }).ToList();
        var pontes = new List<(Point3 Ponta, Point3 Pe)>();

        for (var i = 0; i < trechos.Count; i++)
            for (var j = i + 1; j < trechos.Count; j++)
                if (Cruzamento(trechos[i].A, trechos[i].B, trechos[j].A, trechos[j].B) is { } c)
                {
                    cortes[i].Add(c.T);
                    cortes[j].Add(c.U);
                }

        for (var i = 0; i < trechos.Count; i++)
            for (var j = 0; j < trechos.Count; j++)
            {
                if (i == j) continue;
                foreach (var ponta in new[] { trechos[j].A, trechos[j].B })
                {
                    var (t, pe) = Projetar(trechos[i].A, trechos[i].B, ponta);
                    var d = Dist(pe, ponta);
                    if (d > Snap) continue;
                    cortes[i].Add(t);
                    if (d > Eps) pontes.Add((ponta, pe));
                }
            }

        for (var i = 0; i < trechos.Count; i++)
        {
            var ts = cortes[i].Select(t => Math.Clamp(t, 0, 1)).OrderBy(t => t).ToList();
            var anterior = No(Em(trechos[i].A, trechos[i].B, ts[0]));
            foreach (var t in ts.Skip(1))
            {
                var atual = No(Em(trechos[i].A, trechos[i].B, t));
                if (atual != anterior) Ligar(anterior, atual);
                anterior = atual;
            }
        }

        // A ponta que encosta (sem tocar) na outra vala ganha um trechinho até ela.
        foreach (var (ponta, pe) in pontes)
        {
            var a = No(ponta);
            var b = No(pe);
            if (a != b) Ligar(a, b);
        }
    }

    /// <summary>O ponto de vala mais perto de <paramref name="p"/>, se estiver a até <paramref name="maxDistance"/>.</summary>
    public TrenchPoint? Nearest(Point3 p, double maxDistance)
    {
        var q = Plano(p);
        TrenchPoint? melhor = null;

        for (var e = 0; e < _arestas.Count; e++)
        {
            var (_, pe) = Projetar(_nos[_arestas[e].A], _nos[_arestas[e].B], q);
            var d = Dist(pe, q);
            if (d <= maxDistance + Eps && (melhor is null || d < melhor.Value.Distance)) melhor = new TrenchPoint(pe, e, d);
        }

        return melhor;
    }

    /// <summary>
    /// Onde a semirreta que sai de <paramref name="origin"/> na direção
    /// <paramref name="direction"/> bate primeiro numa vala, até
    /// <paramref name="maxDistance"/>. A distância devolvida é a percorrida.
    /// </summary>
    public TrenchPoint? Ray(Point3 origin, Point3 direction, double maxDistance)
    {
        var o = Plano(origin);
        var n = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (n < Eps) return null;

        var fim = new Point3(o.X + direction.X / n * maxDistance, o.Y + direction.Y / n * maxDistance, 0);
        TrenchPoint? melhor = null;

        for (var e = 0; e < _arestas.Count; e++)
        {
            if (Cruzamento(o, fim, _nos[_arestas[e].A], _nos[_arestas[e].B]) is not { } c) continue;
            var d = c.T * maxDistance;
            if (melhor is null || d < melhor.Value.Distance) melhor = new TrenchPoint(Em(o, fim, c.T), e, d);
        }

        return melhor;
    }

    /// <summary>
    /// Os pontos de vala a até <paramref name="maxDistance"/> de <paramref name="p"/>
    /// (o mais perto de cada trecho, sem repetir ponto), do mais perto ao mais
    /// longe, no máximo <paramref name="limit"/>. A vala mais perto pode estar
    /// solta: quem roteia testa todas e fica com o menor caminho que se liga.
    /// </summary>
    public IReadOnlyList<TrenchPoint> Within(Point3 p, double maxDistance, int limit = 16)
    {
        var q = Plano(p);
        var achados = new List<TrenchPoint>();

        for (var e = 0; e < _arestas.Count; e++)
        {
            var (_, pe) = Projetar(_nos[_arestas[e].A], _nos[_arestas[e].B], q);
            var d = Dist(pe, q);
            if (d <= maxDistance + Eps) achados.Add(new TrenchPoint(pe, e, d));
        }

        var unicos = new List<TrenchPoint>();
        foreach (var a in achados.OrderBy(a => a.Distance))
        {
            if (unicos.Any(u => Dist(u.At, a.At) < 1e-6)) continue;
            unicos.Add(a);
            if (unicos.Count == limit) break;
        }

        return unicos;
    }

    /// <summary>
    /// Os nós da rede (pontas, vértices e cruzamentos das valas) como pontos
    /// dela, cada um num trecho que chega nele, com a direção desse trecho.
    /// Na soma das distâncias pela rede a vários pontos, o mínimo cai num nó
    /// ou num desses pontos (entre eles, a soma é côncava no trecho).
    /// </summary>
    public IReadOnlyList<(TrenchPoint Point, Point3 Direction)> Nodes()
    {
        var nos = new List<(TrenchPoint, Point3)>();
        for (var i = 0; i < _nos.Count; i++)
        {
            if (_ligacoes[i].Count == 0) continue;
            nos.Add((new TrenchPoint(_nos[i], _ligacoes[i][0].Aresta, 0), Direction(_ligacoes[i][0].Aresta)));
        }

        return nos;
    }

    /// <summary>A direção (unitária, em planta) do trecho <paramref name="edge"/>.</summary>
    public Point3 Direction(int edge)
    {
        var a = _nos[_arestas[edge].A];
        var b = _nos[_arestas[edge].B];
        var d = Dist(a, b);
        return d < Eps ? new Point3(1, 0, 0) : new Point3((b.X - a.X) / d, (b.Y - a.Y) / d, 0);
    }

    /// <summary>
    /// As valas que ENTRAM no contorno de um equipamento (o rabicho que o
    /// usuário desenha até a porta dele), só em planta: de cada trecho que
    /// tem um pedaço dentro do contorno (ou a até <paramref name="tolerance"/>
    /// dele), o ponto desse pedaço mais perto de <paramref name="anchor"/>.
    /// Do mais perto ao mais longe, sem repetir ponto. Vazio se nenhuma vala
    /// chega ao contorno; aí quem roteia usa o raio (<see cref="Within"/>).
    /// A cota não conta: o rabisco em 2D pode estar a centenas de metros do
    /// terreno no 3D (Renan, 10/10/2026).
    /// </summary>
    public IReadOnlyList<TrenchPoint> Entering(IReadOnlyList<Point3> polygon, Point3 anchor, double tolerance = Snap)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Count < 3) return [];

        var contorno = polygon.Select(Plano).ToList();
        var q = Plano(anchor);
        var achados = new List<TrenchPoint>();

        for (var e = 0; e < _arestas.Count; e++)
        {
            var a = _nos[_arestas[e].A];
            var b = _nos[_arestas[e].B];
            if (PertoDoContorno(a, b, contorno, q, tolerance) is { } p) achados.Add(new TrenchPoint(p, e, Dist(p, q)));
        }

        var unicos = new List<TrenchPoint>();
        foreach (var a in achados.OrderBy(a => a.Distance))
            if (!unicos.Any(u => Dist(u.At, a.At) < 1e-6)) unicos.Add(a);

        return unicos;
    }

    /// <summary>
    /// O ponto do segmento ab dentro do contorno mais perto de q; sem pedaço
    /// dentro, o ponto do segmento mais perto da borda, se estiver a até a
    /// tolerância dela; senão null.
    /// </summary>
    private static Point3? PertoDoContorno(Point3 a, Point3 b, IReadOnlyList<Point3> contorno, Point3 q, double tolerancia)
    {
        // Onde o segmento cruza a borda: os pedaços entre cortes estão todos dentro ou todos fora.
        var cortes = new List<double> { 0, 1 };
        for (var i = 0; i < contorno.Count; i++)
            if (Cruzamento(a, b, contorno[i], contorno[(i + 1) % contorno.Count]) is { } c) cortes.Add(c.T);
        cortes.Sort();

        var (tq, _) = Projetar(a, b, q);
        Point3? melhor = null;
        for (var i = 0; i + 1 < cortes.Count; i++)
        {
            var (t0, t1) = (cortes[i], cortes[i + 1]);
            if (t1 - t0 < Eps || !Dentro(Em(a, b, (t0 + t1) / 2), contorno)) continue;
            var p = Em(a, b, Math.Clamp(tq, t0, t1));
            if (melhor is null || Dist(p, q) < Dist(melhor.Value, q)) melhor = p;
        }

        if (melhor is not null || tolerancia <= 0) return melhor;

        // Encosta sem entrar: o ponto do segmento mais perto da borda.
        // Como os dois não se cruzam, a menor distância é de uma ponta do
        // segmento a um lado do contorno, ou de um canto do contorno ao segmento.
        (Point3 P, double D)? perto = null;
        void Considerar(Point3 noSegmento, double d)
        {
            if (d <= tolerancia + Eps && (perto is null || d < perto.Value.D)) perto = (noSegmento, d);
        }

        for (var i = 0; i < contorno.Count; i++)
        {
            var c = contorno[i];
            var d = contorno[(i + 1) % contorno.Count];
            Considerar(a, Dist(Projetar(c, d, a).Pe, a));
            Considerar(b, Dist(Projetar(c, d, b).Pe, b));
            var pe = Projetar(a, b, c).Pe;
            Considerar(pe, Dist(pe, c));
        }

        return perto?.P;
    }

    /// <summary>Se o ponto está dentro do polígono (paridade dos cruzamentos de um raio horizontal).</summary>
    private static bool Dentro(Point3 p, IReadOnlyList<Point3> poligono)
    {
        var dentro = false;
        for (int i = 0, j = poligono.Count - 1; i < poligono.Count; j = i++)
        {
            var (pi, pj) = (poligono[i], poligono[j]);
            if ((pi.Y > p.Y) != (pj.Y > p.Y) && p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X) dentro = !dentro;
        }

        return dentro;
    }

    /// <summary>
    /// O caminho mais curto pela rede de <paramref name="from"/> a
    /// <paramref name="to"/> (os dois pontos inclusive, só XY); null se as
    /// valas deles não se ligam.
    /// </summary>
    public IReadOnlyList<Point3>? Path(TrenchPoint from, TrenchPoint to) => Tree(from).Path(to);

    /// <summary>Os caminhos mais curtos a partir de um ponto da rede (Dijkstra uma vez, consultado muitas).</summary>
    public TrenchTree Tree(TrenchPoint from)
    {
        var (a1, b1) = _arestas[from.Edge];
        var dist = Enumerable.Repeat(double.PositiveInfinity, _nos.Count).ToArray();
        var veio = Enumerable.Repeat(-1, _nos.Count).ToArray();
        var fila = new PriorityQueue<int, double>();

        foreach (var no in new[] { a1, b1 })
        {
            var d = Dist(from.At, _nos[no]);
            if (d >= dist[no]) continue;
            dist[no] = d;
            fila.Enqueue(no, d);
        }

        while (fila.TryDequeue(out var no, out var d))
        {
            if (d > dist[no]) continue;
            foreach (var (_, vizinho) in _ligacoes[no])
            {
                var nd = d + Dist(_nos[no], _nos[vizinho]);
                if (nd >= dist[vizinho] - Eps) continue;
                dist[vizinho] = nd;
                veio[vizinho] = no;
                fila.Enqueue(vizinho, nd);
            }
        }

        return new TrenchTree(this, from, dist, veio);
    }

    /// <summary>A árvore de caminhos mínimos a partir de um ponto da rede.</summary>
    public sealed class TrenchTree
    {
        private readonly TrenchNetwork _rede;
        private readonly double[] _dist;
        private readonly int[] _veio;

        internal TrenchTree(TrenchNetwork rede, TrenchPoint from, double[] dist, int[] veio)
        {
            _rede = rede;
            From = from;
            _dist = dist;
            _veio = veio;
        }

        public TrenchPoint From { get; }

        /// <summary>O comprimento do caminho até <paramref name="to"/>; infinito se não se liga.</summary>
        public double Length(TrenchPoint to) => Chegada(to).Total;

        /// <summary>O caminho até <paramref name="to"/> (de <see cref="From"/> a ele); null se não se liga.</summary>
        public IReadOnlyList<Point3>? Path(TrenchPoint to)
        {
            if (to.Edge == From.Edge) return [From.At, to.At];

            var (no, total) = Chegada(to);
            if (double.IsInfinity(total)) return null;

            var nos = new List<int>();
            for (var x = no; x >= 0; x = _veio[x]) nos.Add(x);
            nos.Reverse();

            var caminho = new List<Point3> { From.At };
            foreach (var x in nos)
                if (Dist(caminho[^1], _rede._nos[x]) > Eps) caminho.Add(_rede._nos[x]);
            if (Dist(caminho[^1], to.At) > Eps) caminho.Add(to.At);
            return caminho;
        }

        private (int No, double Total) Chegada(TrenchPoint to)
        {
            if (to.Edge == From.Edge) return (-1, Dist(From.At, to.At));

            var (a2, b2) = _rede._arestas[to.Edge];
            var totalA = _dist[a2] + Dist(_rede._nos[a2], to.At);
            var totalB = _dist[b2] + Dist(_rede._nos[b2], to.At);
            return totalA <= totalB ? (a2, totalA) : (b2, totalB);
        }
    }

    /// <summary>O comprimento em planta de uma sequência de pontos.</summary>
    public static double PlanLength(IReadOnlyList<Point3> pontos)
    {
        var total = 0.0;
        for (var i = 0; i + 1 < pontos.Count; i++) total += Dist(pontos[i], pontos[i + 1]);
        return total;
    }

    private int No(Point3 p)
    {
        // ponytail: busca linear; a rede tem centenas de nós, não milhares.
        for (var i = 0; i < _nos.Count; i++)
            if (Dist(_nos[i], p) < 1e-6) return i;

        _nos.Add(p);
        _ligacoes.Add([]);
        return _nos.Count - 1;
    }

    private void Ligar(int a, int b)
    {
        if (_ligacoes[a].Any(l => l.Vizinho == b)) return;
        _arestas.Add((a, b));
        _ligacoes[a].Add((_arestas.Count - 1, b));
        _ligacoes[b].Add((_arestas.Count - 1, a));
    }

    private static Point3 Plano(Point3 p) => new(p.X, p.Y, 0);

    private static double Dist(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point3 Em(Point3 a, Point3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, 0);

    private static (double T, Point3 Pe) Projetar(Point3 a, Point3 b, Point3 p)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var l2 = dx * dx + dy * dy;
        var t = l2 < Eps ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return (t, Em(a, b, t));
    }

    /// <summary>Onde os segmentos ab e cd se tocam (parâmetros em cada um), encostar inclusive; paralelos não contam.</summary>
    private static (double T, double U)? Cruzamento(Point3 a, Point3 b, Point3 c, Point3 d)
    {
        var rx = b.X - a.X;
        var ry = b.Y - a.Y;
        var sx = d.X - c.X;
        var sy = d.Y - c.Y;
        var den = rx * sy - ry * sx;
        if (Math.Abs(den) < Eps) return null;

        var t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den;
        var u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
        const double folga = 1e-9;
        return t >= -folga && t <= 1 + folga && u >= -folga && u <= 1 + folga ? (Math.Clamp(t, 0, 1), Math.Clamp(u, 0, 1)) : null;
    }
}
