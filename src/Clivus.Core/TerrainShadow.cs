using Clipper2Lib;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// A sombra do relevo (42.1, Renan, 31/10/2026: "Entenda tudo gera sombra,
/// as mesas geram sombras e eu nao estou vendo, o terreno gera sombra se
/// tiver barranco"): um ponto está na sombra do terreno se o raio dele até o
/// sol bate no chão.
/// <para>
/// O terreno vira, uma vez, uma grade de cotas (1 a 2 m) em volta de quem
/// recebe. A cada passo do cálculo, linhas paralelas ao sol varrem a grade
/// a partir do lado do sol guardando o maior <c>z + s·tan(elevação)</c> já
/// visto (<c>s</c> cresce para longe do sol). Um ponto está na sombra do
/// relevo se esse máximo, até meio passo antes dele na mesma linha, passa da
/// cota dele mais o mesmo <c>s·tan</c>: é o raio dele até o sol passando
/// por baixo de algum chão. Uma varredura por passo, proporcional à área, e
/// cada pergunta depois sai numa conta.
/// </para>
/// <para>
/// Recebem sombra as células do chão dentro da região do escopo (1 a 2 m) e
/// os pontos de cada mesa no plano dela (0,5 m). O que pegou sombra em algum
/// passo fica marcado e, no fim, vira polígono (<see cref="Ground"/>,
/// <see cref="OnTable"/>). Os módulos perguntam ponto a ponto
/// (<see cref="Occludes"/>), na mesma varredura do passo.
/// </para>
/// <para>
/// Saída cedo: se o sol está mais alto que o chão mais íngreme da grade, na
/// direção dele, nenhum ponto pode ficar na sombra do relevo, e o passo não
/// varre nada. Num terreno sem barranco, quase todo passo sai por aqui.
/// </para>
/// </summary>
public sealed class TerrainShadow
{
    /// <summary>
    /// A folga da comparação, em metro: a grade e o TIN não são o mesmo chão
    /// (a grade interpola entre os nós), e um talude quase no ângulo do sol
    /// não pode piscar sombra de um passo para o outro.
    /// </summary>
    public const double Tolerance = 0.05;

    /// <summary>O passo dos pontos de cada mesa, no plano dela.</summary>
    public const double TableStep = 0.5;

    /// <summary>O maior número de amostras de uma varredura (linhas × amostras); acima disso, o passo da varredura cresce.</summary>
    private const long MaximoDaVarredura = 16_000_000;

    // A origem local (no mundo): as contas são feitas perto de zero.
    private readonly double _ox, _oy;

    // A grade de cotas, em coordenadas locais: os nós de (_gx0, _gy0) de _h em _h.
    private readonly double _h, _gx0, _gy0;
    private readonly int _nx, _ny;
    private readonly float[] _z;
    private readonly double _rampaX, _rampaY, _zMaximo;

    // O chão: células de _c, de (_cx0, _cy0), as que estão na região.
    private readonly double _c, _cx0, _cy0;
    private readonly int _cnx, _cny;
    private readonly bool[] _recebe;
    private readonly float[] _zCelula;
    private readonly bool[] _naSombra;
    private readonly Paths64 _regiao;

    private readonly Mesa[] _mesas;

    // A caixa de quem recebe (local) e a cota mais baixa dela.
    private readonly bool _temQuem;
    private readonly double _bx0, _by0, _bx1, _by1, _zMinimoQueRecebe;

    // O passo preparado.
    private double _azimute = double.NaN, _elevacao = double.NaN;
    private bool _pode;
    private double _ax, _ay, _px, _py, _tan, _u0, _s0, _hs;
    private int _linhas, _amostras;
    private double[] _maximo = [];

    /// <param name="ground">A cota do terreno em (x, y), ou null fora dele.</param>
    /// <param name="region">Os anéis (em planta) do chão que recebe sombra; vazio: só as mesas.</param>
    /// <param name="tables">Os contornos das mesas que recebem sombra (planos), em ordem.</param>
    /// <param name="margin">Quanto o terreno é lido além de quem recebe, em volta (o morro longe também faz sombra).</param>
    /// <param name="groundCell">A célula do chão, em metro (1 a 2 m).</param>
    /// <param name="gridStep">O passo da grade de cotas; null: o da célula, crescendo se a grade passar de <paramref name="maxGridNodes"/> nós.</param>
    /// <param name="maxGridNodes">O tamanho máximo da grade de cotas.</param>
    public TerrainShadow(
        Func<double, double, double?> ground,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> region,
        IReadOnlyList<IReadOnlyList<Point3>> tables,
        double margin,
        double groundCell = 1,
        double? gridStep = null,
        int maxGridNodes = 4_000_000)
    {
        ArgumentNullException.ThrowIfNull(ground);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(tables);
        if (!(groundCell > 0)) throw new ArgumentOutOfRangeException(nameof(groundCell));
        if (gridStep is { } g && !(g > 0)) throw new ArgumentOutOfRangeException(nameof(gridStep));

        var aneis = region.Where(a => a.Count >= 3 && a.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y))).ToList();
        var contornos = tables.Select(t => t.Where(p => p.IsFinite).ToList()).ToList();

        // A região recorta no fim; a conta vai em toda célula que toca nela
        // (o centro até ¾ de célula para fora), senão a borda perdia meia célula.
        _regiao = ShadowPaths.Paths(aneis);
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> infladas = _regiao.Count == 0
            ? []
            : ShadowPaths.Rings(Clipper.InflatePaths(_regiao, 0.75 * groundCell * ShadowPaths.Scale, JoinType.Square, EndType.Polygon));

        var xs = infladas.SelectMany(a => a.Select(p => p.X)).Concat(contornos.Where(c => c.Count >= 3).SelectMany(c => c.Select(p => p.X))).ToList();
        var ys = infladas.SelectMany(a => a.Select(p => p.Y)).Concat(contornos.Where(c => c.Count >= 3).SelectMany(c => c.Select(p => p.Y))).ToList();

        _temQuem = xs.Count > 0;

        if (!_temQuem)
        {
            _z = [];
            _recebe = [];
            _zCelula = [];
            _naSombra = [];
            _mesas = contornos.Select(_ => Mesa.Vazia).ToArray();
            _h = _c = 1;
            return;
        }

        _ox = Math.Floor(xs.Min());
        _oy = Math.Floor(ys.Min());
        (_bx0, _by0, _bx1, _by1) = (xs.Min() - _ox, ys.Min() - _oy, xs.Max() - _ox, ys.Max() - _oy);

        // A grade de cotas: a caixa de quem recebe com a margem em volta.
        margin = double.IsFinite(margin) ? Math.Max(0, margin) : 0;
        var largura = _bx1 - _bx0 + 2 * margin;
        var altura = _by1 - _by0 + 2 * margin;
        _h = gridStep ?? groundCell;
        while ((largura / _h + 2) * (altura / _h + 2) > Math.Max(4, maxGridNodes)) _h *= 1.25;

        _gx0 = _bx0 - margin;
        _gy0 = _by0 - margin;
        _nx = Math.Max(2, (int)Math.Ceiling(largura / _h) + 1);
        _ny = Math.Max(2, (int)Math.Ceiling(altura / _h) + 1);
        _z = new float[_nx * _ny];

        for (var j = 0; j < _ny; j++)
        {
            var y = _oy + _gy0 + j * _h;
            for (var i = 0; i < _nx; i++)
                _z[j * _nx + i] = ground(_ox + _gx0 + i * _h, y) is { } z && double.IsFinite(z) ? (float)z : float.NaN;
        }

        // A rampa mais íngreme entre nós vizinhos, em x e em y: o limite de
        // quanto o chão da grade sobe por metro em qualquer direção.
        _zMaximo = double.NegativeInfinity;
        for (var j = 0; j < _ny; j++)
        {
            for (var i = 0; i < _nx; i++)
            {
                var z = _z[j * _nx + i];
                if (float.IsNaN(z)) continue;

                _zMaximo = Math.Max(_zMaximo, z);
                if (i + 1 < _nx && !float.IsNaN(_z[j * _nx + i + 1])) _rampaX = Math.Max(_rampaX, Math.Abs(_z[j * _nx + i + 1] - z) / _h);
                if (j + 1 < _ny && !float.IsNaN(_z[(j + 1) * _nx + i])) _rampaY = Math.Max(_rampaY, Math.Abs(_z[(j + 1) * _nx + i] - z) / _h);
            }
        }

        // O chão: células de groundCell, deslocadas um quarto de célula para
        // as bordas delas nunca caírem nas dos pedaços de 8 m do desenho.
        _c = groundCell;
        _cx0 = Math.Floor(_bx0 / _c) * _c - 0.75 * _c;
        _cy0 = Math.Floor(_by0 / _c) * _c - 0.75 * _c;
        _cnx = infladas.Count == 0 ? 0 : (int)Math.Ceiling((_bx1 - _cx0) / _c) + 1;
        _cny = infladas.Count == 0 ? 0 : (int)Math.Ceiling((_by1 - _cy0) / _c) + 1;
        _recebe = new bool[_cnx * _cny];
        _zCelula = new float[_cnx * _cny];
        _naSombra = new bool[_cnx * _cny];

        var zMinimo = double.PositiveInfinity;
        for (var j = 0; j < _cny; j++)
        {
            var y = _cy0 + (j + 0.5) * _c;
            var faixas = Cruzamentos(infladas, y + _oy);

            for (var k = 0; k + 1 < faixas.Count; k += 2)
            {
                var i0 = Math.Max(0, (int)Math.Ceiling((faixas[k] - _ox - _cx0) / _c - 0.5));
                var i1 = Math.Min(_cnx - 1, (int)Math.Floor((faixas[k + 1] - _ox - _cx0) / _c - 0.5));

                for (var i = i0; i <= i1; i++)
                {
                    var z = Cota(_cx0 + (i + 0.5) * _c, y);
                    if (double.IsNaN(z)) continue;

                    _recebe[j * _cnx + i] = true;
                    _zCelula[j * _cnx + i] = (float)z;
                    zMinimo = Math.Min(zMinimo, z);
                }
            }
        }

        _mesas = contornos.Select(c => Mesa.Montar(c, _ox, _oy)).ToArray();
        foreach (var m in _mesas.Where(m => m.Pontos > 0)) zMinimo = Math.Min(zMinimo, m.ZMinimo);

        _zMinimoQueRecebe = double.IsFinite(zMinimo) ? zMinimo : 0;
    }

    /// <summary>Quantos passos com sol entraram (acima do mínimo do cálculo).</summary>
    public int Steps { get; private set; }

    /// <summary>Em quantos desses passos o relevo podia fazer sombra (a varredura rodou).</summary>
    public int SweptSteps { get; private set; }

    /// <summary>Se alguma célula do chão ou ponto de mesa pegou sombra do relevo.</summary>
    public bool AnyShadow => _naSombra.Any(s => s) || _mesas.Any(m => m.NaSombra.Any(s => s));

    /// <summary>As mesas (posição na lista do construtor) que pegaram sombra do relevo.</summary>
    public IEnumerable<int> ShadedTables => Enumerable.Range(0, _mesas.Length).Where(k => _mesas[k].NaSombra.Any(s => s));

    /// <summary>
    /// Prepara o passo (a varredura do sol nesta posição); a mesma posição
    /// de novo não varre outra vez. Falso quando o relevo não tem como fazer
    /// sombra com este sol (sol baixo demais para contar, alto demais para o
    /// chão mais íngreme, ou ninguém recebendo).
    /// </summary>
    public bool Prepare(SunPosition sun)
    {
        ArgumentNullException.ThrowIfNull(sun);

        if (sun.AzimuthDegrees.Equals(_azimute) && sun.ElevationDegrees.Equals(_elevacao)) return _pode;

        _azimute = sun.AzimuthDegrees;
        _elevacao = sun.ElevationDegrees;
        _pode = Varrer(sun);
        if (_pode) SweptSteps++;
        return _pode;
    }

    /// <summary>
    /// Se o ponto (no mundo) está na sombra do relevo com o sol preparado
    /// (<see cref="Prepare"/>). Fora da varredura, falso.
    /// </summary>
    public bool Occludes(double x, double y, double z) => _pode && Bloqueado(x - _ox, y - _oy, z);

    /// <summary>Junta a sombra do relevo com o sol nesta posição (abaixo do mínimo do cálculo, nada).</summary>
    public void Add(SunPosition sun)
    {
        ArgumentNullException.ThrowIfNull(sun);
        if (sun.ElevationDegrees < Shading.MinimumElevationDegrees) return;

        Steps++;
        if (!Prepare(sun)) return;

        for (var j = 0; j < _cny; j++)
        {
            var y = _cy0 + (j + 0.5) * _c;
            for (var i = 0; i < _cnx; i++)
            {
                var k = j * _cnx + i;
                if (!_recebe[k] || _naSombra[k]) continue;
                if (Bloqueado(_cx0 + (i + 0.5) * _c, y, _zCelula[k])) _naSombra[k] = true;
            }
        }

        foreach (var m in _mesas)
        {
            if (m.Pontos == 0 || !PodePegar(m)) continue;

            for (var b = 0; b < m.Nv; b++)
            {
                for (var a = 0; a < m.Nu; a++)
                {
                    var k = b * m.Nu + a;
                    if (!m.Dentro[k] || m.NaSombra[k]) continue;

                    var (lx, ly) = m.Centro(a, b);
                    if (Bloqueado(lx, ly, m.Z[k])) m.NaSombra[k] = true;
                }
            }
        }
    }

    /// <summary>A sombra do relevo no chão (em planta), recortada pela região: as células, juntas.</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Ground()
    {
        var caminhos = new Paths64();

        for (var j = 0; j < _cny; j++)
        {
            var i = 0;
            while (i < _cnx)
            {
                if (!_naSombra[j * _cnx + i])
                {
                    i++;
                    continue;
                }

                var inicio = i;
                while (i < _cnx && _naSombra[j * _cnx + i]) i++;

                caminhos.Add(ShadowPaths.Rect(
                    _ox + _cx0 + inicio * _c, _oy + _cy0 + j * _c,
                    _ox + _cx0 + i * _c, _oy + _cy0 + (j + 1) * _c));
            }
        }

        if (caminhos.Count == 0) return [];

        var uniao = Clipper.Union(caminhos, FillRule.NonZero);
        return ShadowPaths.Rings(Clipper.SimplifyPaths(Clipper.Intersect(uniao, _regiao, FillRule.NonZero), 2));
    }

    /// <summary>A sombra do relevo sobre a mesa (em planta; a cota vem do plano dela), recortada pelo contorno.</summary>
    public IReadOnlyList<IReadOnlyList<(double X, double Y)>> OnTable(int table)
    {
        if (table < 0 || table >= _mesas.Length) return [];

        var m = _mesas[table];
        if (m.Pontos == 0) return [];

        var caminhos = new Paths64();
        for (var b = 0; b < m.Nv; b++)
        {
            var a = 0;
            while (a < m.Nu)
            {
                if (!m.NaSombra[b * m.Nu + a])
                {
                    a++;
                    continue;
                }

                var inicio = a;
                while (a < m.Nu && m.NaSombra[b * m.Nu + a]) a++;

                caminhos.Add(ShadowPaths.Path(
                [
                    m.Canto(inicio, b, _ox, _oy), m.Canto(a, b, _ox, _oy), m.Canto(a, b + 1, _ox, _oy), m.Canto(inicio, b + 1, _ox, _oy),
                ]));
            }
        }

        if (caminhos.Count == 0) return [];

        var uniao = Clipper.Union(caminhos, FillRule.NonZero);
        return ShadowPaths.Rings(Clipper.SimplifyPaths(Clipper.Intersect(uniao, [m.Contorno], FillRule.NonZero), 2));
    }

    /// <summary>
    /// O chão mais íngreme (a maior declividade dos triângulos, em m/m) do
    /// terreno dentro da caixa: com o sol mais alto que isso, o relevo não
    /// faz sombra em lugar nenhum dela (a saída cedo de quem monta o cálculo).
    /// </summary>
    public static double MaxSlope(Tin terrain, double minX, double minY, double maxX, double maxY)
    {
        ArgumentNullException.ThrowIfNull(terrain);

        var maior = 0.0;
        foreach (var t in terrain.Triangles)
        {
            if (Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)) < minX || Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)) > maxX) continue;
            if (Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) < minY || Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > maxY) continue;

            var (ux, uy, uz) = (t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z);
            var (vx, vy, vz) = (t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z);
            var (nx, ny, nz) = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
            if (Math.Abs(nz) < 1e-12) continue;

            maior = Math.Max(maior, Math.Sqrt(nx * nx + ny * ny) / Math.Abs(nz));
        }

        return maior;
    }

    /// <summary>
    /// Até onde, em planta, um chão (ou objeto) de cota <paramref name="highest"/>
    /// alcança com sombra um ponto de cota <paramref name="lowest"/>, com o
    /// sol na menor elevação do período; no máximo <paramref name="cap"/>.
    /// </summary>
    public static double Reach(double highest, double lowest, double minElevationDegrees, double cap)
    {
        if (!double.IsFinite(highest) || !double.IsFinite(lowest) || highest <= lowest) return 0;

        var elevacao = Math.Max(minElevationDegrees, Shading.MinimumElevationDegrees);
        return Math.Min(cap, (highest - lowest) / Math.Tan(elevacao * Math.PI / 180));
    }

    // ------------------------------------------------------------ a conta

    private bool Varrer(SunPosition sol)
    {
        if (!_temQuem || sol.ElevationDegrees < Shading.MinimumElevationDegrees || _z.Length == 0) return false;

        var az = sol.AzimuthDegrees * Math.PI / 180;
        var el = sol.ElevationDegrees * Math.PI / 180;

        // a: para longe do sol, em planta; p: perpendicular.
        _ax = -Math.Sin(az);
        _ay = -Math.Cos(az);
        _px = -_ay;
        _py = _ax;
        _tan = Math.Tan(el);

        // A saída cedo: nenhum chão da grade sobe tanto quanto o raio.
        if (_tan >= _rampaX * Math.Abs(_ax) + _rampaY * Math.Abs(_ay)) return false;

        var (uMin, uMax, sMin, sMax) = Faixa(_bx0, _by0, _bx1, _by1);
        var (_, _, sGrade, _) = Faixa(_gx0, _gy0, _gx0 + (_nx - 1) * _h, _gy0 + (_ny - 1) * _h);

        // A montante, só até onde o chão mais alto ainda alcança quem recebe.
        var alcance = Math.Max(0, (_zMaximo - _zMinimoQueRecebe) / _tan);
        var inicio = Math.Max(sMin - alcance, sGrade);

        _hs = _h;
        long linhas, amostras;
        while (true)
        {
            linhas = (long)Math.Ceiling((uMax - uMin) / _hs) + 3;
            amostras = (long)Math.Ceiling((sMax - inicio) / _hs) + 3;
            if (linhas * amostras <= MaximoDaVarredura) break;
            _hs *= 1.25;
        }

        _u0 = uMin - _hs;
        _s0 = inicio - _hs;
        _linhas = (int)linhas;
        _amostras = (int)amostras;

        var n = linhas * amostras;
        if (_maximo.Length < n) _maximo = new double[n];

        var (dx, dy) = (_ax * _hs, _ay * _hs);
        var subida = _tan * _hs;

        for (var i = 0; i < _linhas; i++)
        {
            var u = _u0 + i * _hs;
            var x = _s0 * _ax + u * _px;
            var y = _s0 * _ay + u * _py;
            var w0 = _s0 * _tan;
            var corrida = double.NegativeInfinity;
            var linha = i * _amostras;

            for (var k = 0; k < _amostras; k++)
            {
                var z = Cota(x, y);
                if (!double.IsNaN(z))
                {
                    var w = z + w0 + k * subida;
                    if (w > corrida) corrida = w;
                }

                _maximo[linha + k] = corrida;
                x += dx;
                y += dy;
            }
        }

        return true;
    }

    /// <summary>O ponto local está na sombra do relevo: o maior "z + s·tan" da linha dele, até meio passo antes, passa do dele.</summary>
    private bool Bloqueado(double lx, double ly, double z)
    {
        var u = lx * _px + ly * _py;
        var s = lx * _ax + ly * _ay;

        var i = (int)Math.Round((u - _u0) / _hs);
        if (i < 0 || i >= _linhas) return false;

        var k = (int)Math.Floor((s - _s0) / _hs - 0.5);
        if (k < 0) return false;
        if (k >= _amostras) k = _amostras - 1;

        return _maximo[i * _amostras + k] > z + s * _tan + Tolerance;
    }

    /// <summary>Se algum ponto da mesa pode estar na sombra neste passo (o máximo da linha só cresce ao longo dela).</summary>
    private bool PodePegar(Mesa m)
    {
        var (uMin, uMax, sMin, sMax) = Faixa(m.X0, m.Y0, m.X1, m.Y1);

        var i0 = Math.Max(0, (int)Math.Round((uMin - _u0) / _hs));
        var i1 = Math.Min(_linhas - 1, (int)Math.Round((uMax - _u0) / _hs));
        var k = Math.Min(_amostras - 1, (int)Math.Floor((sMax - _s0) / _hs - 0.5));
        if (i0 > i1 || k < 0) return false;

        var maior = double.NegativeInfinity;
        for (var i = i0; i <= i1; i++) maior = Math.Max(maior, _maximo[i * _amostras + k]);

        return maior > m.ZMinimo + sMin * _tan + Tolerance;
    }

    /// <summary>As faixas de u e s dos quatro cantos da caixa (local).</summary>
    private (double UMin, double UMax, double SMin, double SMax) Faixa(double x0, double y0, double x1, double y1)
    {
        var (uMin, uMax, sMin, sMax) = (double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity);

        foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) })
        {
            var u = x * _px + y * _py;
            var s = x * _ax + y * _ay;
            (uMin, uMax, sMin, sMax) = (Math.Min(uMin, u), Math.Max(uMax, u), Math.Min(sMin, s), Math.Max(sMax, s));
        }

        return (uMin, uMax, sMin, sMax);
    }

    /// <summary>A cota da grade no ponto local: bilinear; perto de um buraco, a média dos nós que existem; fora, NaN.</summary>
    private double Cota(double x, double y)
    {
        var fx = (x - _gx0) / _h;
        var fy = (y - _gy0) / _h;
        if (!(fx >= 0) || !(fy >= 0)) return double.NaN;

        var i = (int)fx;
        var j = (int)fy;
        if (i >= _nx - 1 || j >= _ny - 1) return double.NaN;

        var tx = fx - i;
        var ty = fy - j;
        var k = j * _nx + i;
        var (a, b, c, d) = (_z[k], _z[k + 1], _z[k + _nx], _z[k + _nx + 1]);

        if (!float.IsNaN(a) && !float.IsNaN(b) && !float.IsNaN(c) && !float.IsNaN(d))
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;

        var soma = 0.0;
        var n = 0;
        foreach (var v in new[] { a, b, c, d })
        {
            if (float.IsNaN(v)) continue;
            soma += v;
            n++;
        }

        return n == 0 ? double.NaN : soma / n;
    }

    /// <summary>Onde a horizontal y (mundo) cruza os anéis, em ordem: os pares são os trechos de dentro (par-ímpar).</summary>
    private static List<double> Cruzamentos(IReadOnlyList<IReadOnlyList<(double X, double Y)>> aneis, double y)
    {
        var xs = new List<double>();

        foreach (var anel in aneis)
        {
            for (var i = 0; i < anel.Count; i++)
            {
                var (a, b) = (anel[i], anel[(i + 1) % anel.Count]);
                if ((a.Y > y) == (b.Y > y)) continue;
                xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
        }

        xs.Sort();
        return xs;
    }

    /// <summary>Uma mesa que recebe: os pontos do plano dela, de 0,5 em 0,5 m, numa grade ao longo do maior lado.</summary>
    private sealed class Mesa
    {
        internal static Mesa Vazia { get; } = new();

        private double _ox, _oy, _ux, _uy, _vx, _vy;

        internal int Nu { get; private set; }

        internal int Nv { get; private set; }

        internal int Pontos { get; private set; }

        internal bool[] Dentro { get; private set; } = [];

        internal float[] Z { get; private set; } = [];

        internal bool[] NaSombra { get; private set; } = [];

        internal Path64 Contorno { get; private set; } = [];

        internal double X0 { get; private set; }

        internal double Y0 { get; private set; }

        internal double X1 { get; private set; }

        internal double Y1 { get; private set; }

        internal double ZMinimo { get; private set; }

        /// <summary>O centro do ponto (a, b), local.</summary>
        internal (double X, double Y) Centro(int a, int b) =>
            (_ox + (a + 0.5) * TableStep * _ux + (b + 0.5) * TableStep * _vx, _oy + (a + 0.5) * TableStep * _uy + (b + 0.5) * TableStep * _vy);

        /// <summary>O canto (a, b) da grade, no mundo.</summary>
        internal (double X, double Y) Canto(int a, int b, double ox, double oy) =>
            (ox + _ox + a * TableStep * _ux + b * TableStep * _vx, oy + _oy + a * TableStep * _uy + b * TableStep * _vy);

        internal static Mesa Montar(IReadOnlyList<Point3> contorno, double ox, double oy)
        {
            if (contorno.Count < 3) return Vazia;

            // O eixo: o maior lado em planta.
            var (ux, uy, maior) = (1.0, 0.0, 0.0);
            for (var i = 0; i < contorno.Count; i++)
            {
                var (a, b) = (contorno[i], contorno[(i + 1) % contorno.Count]);
                var d = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                if (d <= maior) continue;
                (ux, uy, maior) = ((b.X - a.X) / d, (b.Y - a.Y) / d, d);
            }

            if (maior < 1e-6) return Vazia;

            var (vx, vy) = (-uy, ux);
            var p0 = contorno[0];
            var (aMin, aMax, bMin, bMax) = (double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity);
            foreach (var p in contorno)
            {
                var a = (p.X - p0.X) * ux + (p.Y - p0.Y) * uy;
                var b = (p.X - p0.X) * vx + (p.Y - p0.Y) * vy;
                (aMin, aMax, bMin, bMax) = (Math.Min(aMin, a), Math.Max(aMax, a), Math.Min(bMin, b), Math.Max(bMax, b));
            }

            var m = new Mesa
            {
                _ox = p0.X + aMin * ux + bMin * vx - ox,
                _oy = p0.Y + aMin * uy + bMin * vy - oy,
                _ux = ux,
                _uy = uy,
                _vx = vx,
                _vy = vy,
                Nu = Math.Max(1, (int)Math.Ceiling((aMax - aMin) / TableStep)),
                Nv = Math.Max(1, (int)Math.Ceiling((bMax - bMin) / TableStep)),
                Contorno = ShadowPaths.Path(contorno.Select(p => (p.X, p.Y)).ToList()),
                X0 = contorno.Min(p => p.X) - ox,
                Y0 = contorno.Min(p => p.Y) - oy,
                X1 = contorno.Max(p => p.X) - ox,
                Y1 = contorno.Max(p => p.Y) - oy,
            };

            m.Dentro = new bool[m.Nu * m.Nv];
            m.Z = new float[m.Nu * m.Nv];
            m.NaSombra = new bool[m.Nu * m.Nv];
            var zMinimo = double.PositiveInfinity;

            for (var b = 0; b < m.Nv; b++)
            {
                for (var a = 0; a < m.Nu; a++)
                {
                    var (lx, ly) = m.Centro(a, b);
                    var (x, y) = (lx + ox, ly + oy);
                    if (!Polygons.Contains(contorno, x, y) || Shading.PlaneHeight(contorno, x, y) is not { } z) continue;

                    m.Dentro[b * m.Nu + a] = true;
                    m.Z[b * m.Nu + a] = (float)z;
                    m.Pontos++;
                    zMinimo = Math.Min(zMinimo, z);
                }
            }

            m.ZMinimo = double.IsFinite(zMinimo) ? zMinimo : 0;
            return m;
        }
    }
}
