namespace Clivus.Geo;

/// <summary>Por que o caminho da água parou.</summary>
public enum FlowStop
{
    /// <summary>Chegou numa canaleta (um dos alvos).</summary>
    Target,

    /// <summary>Caiu num fundo: ponto plano ou de onde não há descida (bacia fechada, empoçamento).</summary>
    Sink,

    /// <summary>Saiu do terreno (ou nem começou dentro dele).</summary>
    OutsideTerrain,

    /// <summary>Andou o comprimento máximo sem chegar a lugar nenhum. Guarda contra travar.</summary>
    LengthLimit,

    /// <summary>Entrou num lugar cujo destino já se sabe (o cálculo das bacias aproveita o caminho já feito).</summary>
    Joined,
}

/// <summary>Os parâmetros do caminho da água.</summary>
public sealed class FlowOptions
{
    /// <summary>O passo do caminho, em metros.</summary>
    public double Step { get; init; } = 0.5;

    /// <summary>O comprimento máximo em planta, em metros.</summary>
    public double MaxLength { get; init; } = 5000;

    /// <summary>Abaixo desta declividade (m/m) o triângulo é plano: a água só sai sondando em volta.</summary>
    public double FlatRatio { get; init; } = 0.0005;

    /// <summary>
    /// Até que distância (m) a água sonda em volta quando o gradiente não acha
    /// saída (triângulo plano, talvegue torto). Nada mais baixo dentro dele: é
    /// fundo. 50 m: em Itatiba (curvas de metro em metro) há patamares planos
    /// de mais de 20 m entre duas curvas, e a água ainda desce depois deles.
    /// </summary>
    public double ProbeRadius { get; init; } = 50;
}

/// <summary>
/// O caminho que a água fez.
/// </summary>
/// <param name="Points">Os pontos, do início ao fim, com a cota do terreno. Vazio se o início está fora do terreno.</param>
/// <param name="Stop">Por que parou.</param>
/// <param name="TargetIndex">Em qual alvo parou (posição na lista de <see cref="FlowTargets"/>); −1 se não foi num alvo.</param>
/// <param name="Length2D">Comprimento em planta, em metros.</param>
/// <param name="Length3D">Comprimento no espaço, em metros.</param>
public sealed record FlowPath(IReadOnlyList<Point3> Points, FlowStop Stop, int TargetIndex, double Length2D, double Length3D)
{
    /// <summary>Quanto a água desceu do início ao fim, em metros.</summary>
    public double Drop => Points.Count < 2 ? 0 : Points[0].Z - Points[^1].Z;
}

/// <summary>
/// As linhas onde a água para (as canaletas), com um índice espacial para o
/// caminho perguntar "cruzei alguma?" a cada passo sem varrer todas.
/// </summary>
public sealed class FlowTargets
{
    private readonly List<(Point3 A, Point3 B, int Index)> _segmentos = [];
    private readonly Dictionary<(long, long), List<int>> _celulas = [];
    private readonly double _celula;

    /// <summary>
    /// As polilinhas dos alvos, em planta (o Z é ignorado). <paramref name="celula"/>
    /// é o lado da célula do índice, em metros.
    /// </summary>
    public FlowTargets(IReadOnlyList<IReadOnlyList<Point3>> polylines, double celula = 10)
    {
        ArgumentNullException.ThrowIfNull(polylines);

        if (!(celula > 0) || !double.IsFinite(celula)) throw new ArgumentOutOfRangeException(nameof(celula));

        _celula = celula;
        Count = polylines.Count;

        for (var p = 0; p < polylines.Count; p++)
        {
            var linha = polylines[p];

            for (var i = 1; i < linha.Count; i++)
            {
                var s = _segmentos.Count;
                _segmentos.Add((linha[i - 1], linha[i], p));

                foreach (var chave in CelulasAoLongo(linha[i - 1], linha[i]))
                {
                    if (!_celulas.TryGetValue(chave, out var lista)) _celulas[chave] = lista = [];
                    lista.Add(s);
                }
            }
        }
    }

    /// <summary>Quantas polilinhas.</summary>
    public int Count { get; }

    private IEnumerable<(long, long)> Celulas(double x0, double y0, double x1, double y1)
    {
        var i0 = (long)Math.Floor(Math.Min(x0, x1) / _celula);
        var i1 = (long)Math.Floor(Math.Max(x0, x1) / _celula);
        var j0 = (long)Math.Floor(Math.Min(y0, y1) / _celula);
        var j1 = (long)Math.Floor(Math.Max(y0, y1) / _celula);

        for (var i = i0; i <= i1; i++)
        for (var j = j0; j <= j1; j++)
            yield return (i, j);
    }

    /// <summary>
    /// As células por onde o segmento passa, com as vizinhas (folga para o
    /// segmento que só raspa um canto). Não o retângulo envolvente: uma
    /// canaleta diagonal de 2 km ocuparia dezenas de milhares de células.
    /// </summary>
    private HashSet<(long, long)> CelulasAoLongo(Point3 a, Point3 b)
    {
        var celulas = new HashSet<(long, long)>();
        var comprimento = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        var passos = Math.Max(1, (int)Math.Ceiling(comprimento / (_celula / 4)));

        for (var k = 0; k <= passos; k++)
        {
            var t = (double)k / passos;
            var i = (long)Math.Floor((a.X + (b.X - a.X) * t) / _celula);
            var j = (long)Math.Floor((a.Y + (b.Y - a.Y) * t) / _celula);

            for (var di = -1; di <= 1; di++)
            for (var dj = -1; dj <= 1; dj++)
                celulas.Add((i + di, j + dj));
        }

        return celulas;
    }

    private readonly HashSet<int> _vistos = [];

    /// <summary>
    /// O primeiro alvo que o segmento (x0, y0) → (x1, y1) toca, em planta: em
    /// qual polilinha e em que fração <paramref name="t"/> do segmento.
    /// </summary>
    public bool TryHit(double x0, double y0, double x1, double y1, out int index, out double t)
    {
        index = -1;
        t = double.MaxValue;

        // Reaproveitado: um HashSet novo por passo pesava no cálculo das bacias.
        // Por isso a consulta não é segura entre threads.
        var vistos = _vistos;
        vistos.Clear();

        foreach (var chave in Celulas(x0, y0, x1, y1))
        {
            if (!_celulas.TryGetValue(chave, out var lista)) continue;

            foreach (var s in lista)
            {
                if (!vistos.Add(s)) continue;

                var (a, b, dono) = _segmentos[s];

                if (Cruza(x0, y0, x1, y1, a.X, a.Y, b.X, b.Y, out var ts) && ts < t)
                {
                    t = ts;
                    index = dono;
                }
            }
        }

        return index >= 0;
    }

    /// <summary>
    /// Se os dois segmentos se tocam, e em que fração do primeiro. As pontas
    /// contam: passo que termina em cima da canaleta chegou nela.
    /// </summary>
    private static bool Cruza(double px, double py, double qx, double qy, double ax, double ay, double bx, double by, out double t)
    {
        t = 0;

        var (rx, ry) = (qx - px, qy - py);
        var (sx, sy) = (bx - ax, by - ay);

        var denominador = rx * sy - ry * sx;
        if (Math.Abs(denominador) < 1e-12) return false;

        var (wx, wy) = (ax - px, ay - py);

        t = (wx * sy - wy * sx) / denominador;
        var u = (wx * ry - wy * rx) / denominador;

        const double folga = 1e-9;

        return t >= -folga && t <= 1 + folga && u >= -folga && u <= 1 + folga;
    }
}

/// <summary>
/// O caminho da água sobre o terreno (passo 25.4): de um ponto, segue a
/// descida mais íngreme até chegar numa canaleta, cair num fundo ou sair do
/// terreno.
///
/// O cuidado está no talvegue. Num vale, a face de um lado manda a água para
/// o outro, e a face do outro manda de volta: andando só pelo gradiente, o
/// caminho ziguezagueia de uma face à outra e o comprimento sai inchado. Quando
/// o passo cai numa face que aponta de volta, a direção vira a soma das duas,
/// que é a direção do próprio talvegue.
/// </summary>
public static class FlowTracer
{
    /// <summary>O caminho da água a partir de (x, y).</summary>
    /// <param name="joined">
    /// Se o passo terminar num ponto para o qual isto diz sim, o caminho para
    /// com <see cref="FlowStop.Joined"/>: o resto dele já é conhecido.
    /// </param>
    public static FlowPath Trace(Tin tin, double x, double y, FlowTargets? targets = null, FlowOptions? options = null, Func<double, double, bool>? joined = null)
    {
        ArgumentNullException.ThrowIfNull(tin);

        options ??= new FlowOptions();

        // Passo zero prendia o laço das tentativas para sempre.
        if (!(options.Step > 0) || !double.IsFinite(options.Step)) throw new ArgumentOutOfRangeException(nameof(options), "Step");
        if (!(options.MaxLength > 0)) throw new ArgumentOutOfRangeException(nameof(options), "MaxLength");

        if (!tin.TryGetZ(x, y, out var z0)) return new FlowPath([], FlowStop.OutsideTerrain, -1, 0, 0);

        var pontos = new List<Point3> { new(x, y, z0) };
        double em2d = 0, em3d = 0;

        FlowPath Fim(FlowStop motivo, int alvo = -1) => new(pontos, motivo, alvo, em2d, em3d);

        while (true)
        {
            var p = pontos[^1];

            if (!tin.TryGetSlope(p.X, p.Y, out var aqui)) return Fim(FlowStop.OutsideTerrain);

            var restante = options.MaxLength - em2d;
            if (restante <= 1e-9) return Fim(FlowStop.LengthLimit);

            var passo = Math.Min(options.Step, restante);
            Point3? proximo = null;
            var foraDoTerreno = false;

            if (!aqui.IsFlat(options.FlatRatio))
            {
                var (dx, dy) = (aqui.DownhillX, aqui.DownhillY);

                // Olha a face de onde o passo cai: se ela manda de volta, é
                // talvegue, e a água segue a soma das duas direções.
                if (tin.TryGetSlope(p.X + dx * passo, p.Y + dy * passo, out var la)
                    && !la.IsFlat(options.FlatRatio)
                    && dx * la.DownhillX + dy * la.DownhillY < 0)
                {
                    var (sx, sy) = (dx + la.DownhillX, dy + la.DownhillY);
                    var norma = Math.Sqrt(sx * sx + sy * sy);
                    if (norma >= 1e-6) (dx, dy) = (sx / norma, sy / norma);
                }

                // Anda o passo; se não desce, tenta passos menores.
                for (var tentativa = passo; tentativa >= passo / 16; tentativa /= 2)
                {
                    var (qx, qy) = (p.X + dx * tentativa, p.Y + dy * tentativa);

                    if (!tin.TryGetZ(qx, qy, out var qz))
                    {
                        foraDoTerreno = true;
                        continue;
                    }

                    if (qz < p.Z - 1e-9)
                    {
                        proximo = new Point3(qx, qy, qz);
                        break;
                    }
                }
            }

            // Triângulo plano (comum em TIN feito de curvas de nível: os três
            // vértices na mesma curva) ou talvegue torto da triangulação: o
            // gradiente não acha a saída, mas o terreno em volta desce. Sonda
            // em volta, em raios crescentes, antes de dizer que a água empoça.
            proximo ??= Sondar(tin, p, Math.Min(passo, restante), Math.Min(options.ProbeRadius, restante), ref foraDoTerreno);

            if (proximo is not { } q) return Fim(foraDoTerreno ? FlowStop.OutsideTerrain : FlowStop.Sink);

            if (targets is not null && targets.TryHit(p.X, p.Y, q.X, q.Y, out var alvo, out var t))
            {
                t = Math.Clamp(t, 0, 1);
                var (hx, hy) = (p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);
                var hz = tin.TryGetZ(hx, hy, out var zz) ? zz : p.Z + (q.Z - p.Z) * t;

                Andar(new Point3(hx, hy, hz));
                return Fim(FlowStop.Target, alvo);
            }

            Andar(q);

            if (joined is not null && joined(q.X, q.Y)) return Fim(FlowStop.Joined);
        }

        static Point3? Sondar(Tin tin, Point3 p, double passo, double raioMaximo, ref bool foraDoTerreno)
        {
            for (var raio = passo; raio <= raioMaximo + 1e-9; raio = raio * 2 >= raioMaximo && raio < raioMaximo ? raioMaximo : raio * 2)
            {
                Point3? melhor = null;

                for (var k = 0; k < 16; k++)
                {
                    var a = k * Math.PI / 8;
                    var (qx, qy) = (p.X + raio * Math.Cos(a), p.Y + raio * Math.Sin(a));

                    if (!tin.TryGetZ(qx, qy, out var qz))
                    {
                        foraDoTerreno = true;
                        continue;
                    }

                    if (qz < p.Z - 1e-6 && (melhor is null || qz < melhor.Value.Z)) melhor = new Point3(qx, qy, qz);
                }

                if (melhor is not null) return melhor;
                if (raio >= raioMaximo) break;
            }

            return null;
        }

        void Andar(Point3 q)
        {
            var p = pontos[^1];
            var (ex, ey, ez) = (q.X - p.X, q.Y - p.Y, q.Z - p.Z);

            em2d += Math.Sqrt(ex * ex + ey * ey);
            em3d += Math.Sqrt(ex * ex + ey * ey + ez * ez);

            if (ex * ex + ey * ey > 1e-18) pontos.Add(q);
        }
    }
}
