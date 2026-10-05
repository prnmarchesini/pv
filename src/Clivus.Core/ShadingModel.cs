using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O que fez a sombra num módulo.</summary>
public enum ShadowCause
{
    /// <summary>Nada: o módulo está no sol.</summary>
    None = 0,

    /// <summary>Um objeto de sombra (árvore).</summary>
    Object = 1,

    /// <summary>Outra mesa (a fileira da frente, a vizinha).</summary>
    Table = 2,

    /// <summary>O relevo: o sol está abaixo do horizonte do terreno visto do módulo.</summary>
    Terrain = 3,
}

/// <summary>Uma face de módulo que recebe e faz sombra: os quatro cantos e a mesa dona (não faz sombra nela mesma).</summary>
public sealed record ShadowQuad(IReadOnlyList<Point3> Corners, int Group);

/// <summary>A sombra de cada face num instante: a fração e o que mais a causou.</summary>
public sealed record ShadingResult(IReadOnlyList<double> Fractions, IReadOnlyList<ShadowCause> Causes);

/// <summary>
/// A sombra com TODOS os elementos do desenho (Renan, 03/10/2026: "cadê o
/// sombreamento das próprias mesas? A análise de sombreamento tem que pegar
/// todos elementos do desenho"): as árvores (cilindros), as faces dos
/// módulos de outras mesas (a fileira da frente faz sombra na de trás) e o
/// relevo (o morro esconde o sol cedo e tarde).
///
/// - Relevo: para cada face, o horizonte do terreno visto do centro dela, em
///   36 direções, calculado uma vez; o sol abaixo dele sombreia a face
///   inteira.
/// - Mesas: o raio de cada ponto amostrado na direção do sol é seguido numa
///   grade em planta, célula a célula, e testado contra as faces das outras
///   mesas (dois triângulos cada). Célula cuja face mais alta fica abaixo do
///   raio é pulada.
/// - Árvores: o cilindro de <see cref="Shading.Blocks"/>.
/// </summary>
public sealed class ShadingModel
{
    private const int Setores = 36;

    private readonly IReadOnlyList<ShadowQuad> _faces;
    private readonly IReadOnlyList<ShadowCylinder> _cilindros;
    private readonly bool _mesas;
    private readonly int _ao;
    private readonly int _atraves;
    private readonly double[][]? _horizontes;
    private readonly (double MinX, double MaxX, double MinY, double MaxY, double MinZ)[] _caixas;

    private readonly double _celula;
    private readonly Dictionary<long, List<int>> _grade = [];
    private readonly Dictionary<long, double> _alturaDaCelula = [];
    private readonly double _maisAlta;
    private readonly int[] _carimbo;
    private int _rodada;

    /// <param name="faces">As faces dos módulos (recebem sombra; fazem também, nas outras mesas).</param>
    /// <param name="cylinders">Os cilindros dos objetos (árvores).</param>
    /// <param name="ground">A cota do terreno, para o horizonte; null deixa o relevo de fora.</param>
    /// <param name="groundMaxZ">A cota mais alta do terreno (para parar a busca do horizonte).</param>
    /// <param name="tablesCastShadow">Se as mesas fazem sombra umas nas outras.</param>
    /// <param name="along">Pontos amostrados ao longo da face.</param>
    /// <param name="across">Pontos amostrados no fundo da face.</param>
    public ShadingModel(
        IReadOnlyList<ShadowQuad> faces, IReadOnlyList<ShadowCylinder> cylinders, Func<double, double, double?>? ground = null,
        double groundMaxZ = double.NaN, bool tablesCastShadow = true, int along = 4, int across = 2)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(cylinders);
        if (faces.Any(f => f.Corners.Count != 4)) throw new ArgumentException("toda face precisa de quatro cantos", nameof(faces));

        _faces = faces;
        _cilindros = cylinders;
        _mesas = tablesCastShadow && faces.Count > 0;
        _ao = Math.Max(1, along);
        _atraves = Math.Max(1, across);
        _carimbo = new int[faces.Count];

        _caixas = faces.Select(f => (f.Corners.Min(p => p.X), f.Corners.Max(p => p.X), f.Corners.Min(p => p.Y), f.Corners.Max(p => p.Y), f.Corners.Min(p => p.Z))).ToArray();

        // A célula da grade: perto do tamanho de um módulo.
        _celula = faces.Count == 0 ? 3 : Math.Clamp(faces.Average(f => Math.Max(Distancia(f.Corners[0], f.Corners[1]), Distancia(f.Corners[1], f.Corners[2]))), 1, 10);
        _maisAlta = faces.Count == 0 ? double.NegativeInfinity : faces.Max(f => f.Corners.Max(p => p.Z));

        for (var k = 0; k < faces.Count; k++)
        {
            var c = _caixas[k];
            var topo = faces[k].Corners.Max(p => p.Z);

            for (var ix = Indice(c.MinX); ix <= Indice(c.MaxX); ix++)
            {
                for (var iy = Indice(c.MinY); iy <= Indice(c.MaxY); iy++)
                {
                    var chave = Chave(ix, iy);
                    if (!_grade.TryGetValue(chave, out var lista)) _grade[chave] = lista = [];
                    lista.Add(k);
                    _alturaDaCelula[chave] = _alturaDaCelula.TryGetValue(chave, out var h) ? Math.Max(h, topo) : topo;
                }
            }
        }

        // O horizonte é um por mesa, do centro dela: numa mesa de 20 m a
        // diferença é desprezível, e por face a conta levava 14 s numa usina
        // de 2 mil módulos.
        if (ground is not null)
        {
            var porMesa = faces
                .Select((f, k) => (f.Group, Centro: Centro(f.Corners)))
                .GroupBy(x => x.Group)
                .ToDictionary(
                    g => g.Key,
                    g => Horizonte(new Point3(g.Average(x => x.Centro.X), g.Average(x => x.Centro.Y), g.Average(x => x.Centro.Z)), ground, groundMaxZ));

            _horizontes = faces.Select(f => porMesa[f.Group]).ToArray();
        }
    }

    /// <summary>A fração sombreada e a causa de cada face, com o sol nesta direção.</summary>
    public ShadingResult At(SunPosition sun)
    {
        ArgumentNullException.ThrowIfNull(sun);

        var fracoes = new double[_faces.Count];
        var causas = new ShadowCause[_faces.Count];
        if (sun.ElevationDegrees < Shading.MinimumElevationDegrees || _faces.Count == 0) return new ShadingResult(fracoes, causas);

        var s = sun.Direction;
        var chao = _caixas.Min(c => c.MinZ);
        var caixasDosCilindros = _cilindros.Select(c => (Cilindro: c, Caixa: CaixaDaSombra(c, s, chao))).ToList();
        var perto = new List<ShadowCylinder>();
        var porCausa = new int[4];

        for (var k = 0; k < _faces.Count; k++)
        {
            // O relevo: abaixo do horizonte, a face inteira.
            if (_horizontes is not null && sun.ElevationDegrees < Horizonte(_horizontes[k], sun.AzimuthDegrees))
            {
                fracoes[k] = 1;
                causas[k] = ShadowCause.Terrain;
                continue;
            }

            var f = _caixas[k];
            perto.Clear();
            foreach (var c in caixasDosCilindros)
                if (c.Caixa.MaxX >= f.MinX && c.Caixa.MinX <= f.MaxX && c.Caixa.MaxY >= f.MinY && c.Caixa.MinY <= f.MaxY) perto.Add(c.Cilindro);

            Array.Clear(porCausa);
            var sombra = 0;

            for (var i = 0; i < _ao; i++)
            {
                for (var j = 0; j < _atraves; j++)
                {
                    var p = Bilinear(_faces[k].Corners, (i + 0.5) / _ao, (j + 0.5) / _atraves);
                    var causa = Causa(p, _faces[k].Group, s, perto);
                    if (causa == ShadowCause.None) continue;

                    sombra++;
                    porCausa[(int)causa]++;
                }
            }

            fracoes[k] = (double)sombra / (_ao * _atraves);
            if (sombra > 0) causas[k] = porCausa[(int)ShadowCause.Object] >= porCausa[(int)ShadowCause.Table] ? ShadowCause.Object : ShadowCause.Table;
        }

        return new ShadingResult(fracoes, causas);
    }

    /// <summary>O pior caso de cada face num período, com a causa nesse pior instante.</summary>
    /// <param name="latitude">Graus.</param>
    /// <param name="longitude">Graus.</param>
    /// <param name="utcOffsetHours">O fuso.</param>
    /// <param name="instants">Os instantes do período.</param>
    /// <param name="onInstant">Chamado a cada instante, antes da conta dele, com o sol (o andamento; quem chama pode juntar mais coisa do mesmo passo).</param>
    /// <param name="cancel">Conferido a cada instante, logo depois de <paramref name="onInstant"/>: cancelado, sai com <see cref="OperationCanceledException"/>.</param>
    public ShadingWorstCase Worst(
        double latitude, double longitude, double utcOffsetHours, IEnumerable<DateTime> instants,
        Action<DateTime, SunPosition>? onInstant = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(instants);

        var pior = new double[_faces.Count];
        var quando = new DateTime?[_faces.Count];
        var causas = new ShadowCause[_faces.Count];
        DateTime? piorInstante = null;
        var piorSoma = 0.0;
        var comSol = 0;
        var total = 0;

        foreach (var instante in instants)
        {
            cancel.ThrowIfCancellationRequested();
            total++;
            var sol = SolarCalculator.Compute(latitude, longitude, instante, utcOffsetHours);
            onInstant?.Invoke(instante, sol);
            cancel.ThrowIfCancellationRequested();
            if (sol.ElevationDegrees < Shading.MinimumElevationDegrees) continue;

            comSol++;
            var r = At(sol);
            var soma = 0.0;

            for (var k = 0; k < pior.Length; k++)
            {
                soma += r.Fractions[k];
                if (r.Fractions[k] <= pior[k]) continue;

                pior[k] = r.Fractions[k];
                quando[k] = instante;
                causas[k] = r.Causes[k];
            }

            if (soma > piorSoma)
            {
                piorSoma = soma;
                piorInstante = instante;
            }
        }

        return new ShadingWorstCase(pior, quando, piorInstante, comSol, total, causas);
    }

    /// <summary>O que bloqueia o raio do ponto: árvore, depois mesa; nada.</summary>
    private ShadowCause Causa(Point3 p, int grupo, (double X, double Y, double Z) s, List<ShadowCylinder> perto)
    {
        foreach (var c in perto)
            if (Shading.Blocks(c, p, s)) return ShadowCause.Object;

        return _mesas && BateNumaMesa(p, grupo, s) ? ShadowCause.Table : ShadowCause.None;
    }

    /// <summary>Segue o raio em planta, célula a célula, até ele passar da face mais alta do desenho.</summary>
    private bool BateNumaMesa(Point3 p, int grupo, (double X, double Y, double Z) s)
    {
        if (p.Z >= _maisAlta) return false;

        _rodada++;
        if (_rodada == int.MaxValue)
        {
            Array.Clear(_carimbo);
            _rodada = 1;
        }

        var horizontal = Math.Sqrt(s.X * s.X + s.Y * s.Y);

        // Sol a pino: só a célula do ponto.
        if (horizontal < 1e-9) return BateNaCelula(Chave(Indice(p.X), Indice(p.Y)), p, grupo, s, p.Z);

        var ux = s.X / horizontal;
        var uy = s.Y / horizontal;
        var subida = s.Z / horizontal;                 // metro de cota por metro em planta
        var alcance = (_maisAlta - p.Z) / subida;      // em planta, até passar da face mais alta

        var ix = Indice(p.X);
        var iy = Indice(p.Y);
        var passoX = ux > 0 ? 1 : -1;
        var passoY = uy > 0 ? 1 : -1;
        var proximaX = Math.Abs(ux) < 1e-12 ? double.PositiveInfinity : ((ix + (ux > 0 ? 1 : 0)) * _celula - p.X) / ux;
        var proximaY = Math.Abs(uy) < 1e-12 ? double.PositiveInfinity : ((iy + (uy > 0 ? 1 : 0)) * _celula - p.Y) / uy;
        var deltaX = Math.Abs(ux) < 1e-12 ? double.PositiveInfinity : _celula / Math.Abs(ux);
        var deltaY = Math.Abs(uy) < 1e-12 ? double.PositiveInfinity : _celula / Math.Abs(uy);
        var entrada = 0.0;

        while (entrada <= alcance)
        {
            if (BateNaCelula(Chave(ix, iy), p, grupo, s, p.Z + entrada * subida)) return true;

            if (proximaX < proximaY)
            {
                entrada = proximaX;
                proximaX += deltaX;
                ix += passoX;
            }
            else
            {
                entrada = proximaY;
                proximaY += deltaY;
                iy += passoY;
            }
        }

        return false;
    }

    private bool BateNaCelula(long chave, Point3 p, int grupo, (double X, double Y, double Z) s, double cotaDoRaio)
    {
        if (!_grade.TryGetValue(chave, out var lista)) return false;

        // A face mais alta da célula fica abaixo de onde o raio entra: nada aqui.
        if (_alturaDaCelula[chave] < cotaDoRaio) return false;

        foreach (var k in lista)
        {
            if (_carimbo[k] == _rodada) continue;
            _carimbo[k] = _rodada;

            if (_faces[k].Group == grupo) continue;

            var c = _faces[k].Corners;
            if (Triangulo(p, s, c[0], c[1], c[2]) || Triangulo(p, s, c[0], c[2], c[3])) return true;
        }

        return false;
    }

    /// <summary>Möller–Trumbore: se o raio p + t·s (t &gt; 0) atravessa o triângulo.</summary>
    private static bool Triangulo(Point3 p, (double X, double Y, double Z) s, Point3 a, Point3 b, Point3 c)
    {
        var e1x = b.X - a.X; var e1y = b.Y - a.Y; var e1z = b.Z - a.Z;
        var e2x = c.X - a.X; var e2y = c.Y - a.Y; var e2z = c.Z - a.Z;

        var hx = s.Y * e2z - s.Z * e2y;
        var hy = s.Z * e2x - s.X * e2z;
        var hz = s.X * e2y - s.Y * e2x;
        var det = e1x * hx + e1y * hy + e1z * hz;
        if (Math.Abs(det) < 1e-12) return false;

        var inv = 1 / det;
        var tx = p.X - a.X; var ty = p.Y - a.Y; var tz = p.Z - a.Z;
        var u = (tx * hx + ty * hy + tz * hz) * inv;
        if (u < 0 || u > 1) return false;

        var qx = ty * e1z - tz * e1y;
        var qy = tz * e1x - tx * e1z;
        var qz = tx * e1y - ty * e1x;
        var v = (s.X * qx + s.Y * qy + s.Z * qz) * inv;
        if (v < 0 || u + v > 1) return false;

        var t = (e2x * qx + e2y * qy + e2z * qz) * inv;
        return t > 1e-4;
    }

    /// <summary>O horizonte do terreno visto de p, em graus, para cada um dos 36 setores de azimute (0° = norte).</summary>
    private static double[] Horizonte(Point3 p, Func<double, double, double?> ground, double groundMaxZ)
    {
        var h = new double[Setores];

        for (var k = 0; k < Setores; k++)
        {
            var az = (k + 0.5) * 360.0 / Setores * Math.PI / 180;
            var dx = Math.Sin(az);
            var dy = Math.Cos(az);
            var maior = double.NegativeInfinity;

            for (var d = 3.0; d <= 1500; d += Math.Max(3, d * 0.06))
            {
                // Nenhum ponto mais longe pode subir acima do que já foi visto.
                if (double.IsFinite(groundMaxZ) && (groundMaxZ - p.Z) / d <= maior) break;

                if (ground(p.X + dx * d, p.Y + dy * d) is not { } z) continue;
                maior = Math.Max(maior, (z - p.Z) / d);
            }

            h[k] = double.IsNegativeInfinity(maior) ? -90 : Math.Atan(maior) * 180 / Math.PI;
        }

        return h;
    }

    /// <summary>O horizonte no azimute dado, interpolado entre os setores.</summary>
    private static double Horizonte(double[] h, double azimute)
    {
        var x = ((azimute % 360) + 360) % 360 / (360.0 / Setores) - 0.5;
        var i0 = (int)Math.Floor(x);
        var f = x - i0;
        var a = h[((i0 % Setores) + Setores) % Setores];
        var b = h[(((i0 + 1) % Setores) + Setores) % Setores];
        return a + (b - a) * f;
    }

    private static (double MinX, double MaxX, double MinY, double MaxY) CaixaDaSombra(ShadowCylinder c, (double X, double Y, double Z) s, double chao)
    {
        var t = Math.Max((c.Top - chao) / s.Z, 0);
        var px = c.X - s.X * t;
        var py = c.Y - s.Y * t;
        return (Math.Min(c.X, px) - c.Radius, Math.Max(c.X, px) + c.Radius, Math.Min(c.Y, py) - c.Radius, Math.Max(c.Y, py) + c.Radius);
    }

    private int Indice(double v) => (int)Math.Floor(v / _celula);

    private static long Chave(int ix, int iy) => ((long)ix << 32) ^ (uint)iy;

    private static Point3 Centro(IReadOnlyList<Point3> c) =>
        new(c.Average(p => p.X), c.Average(p => p.Y), c.Average(p => p.Z));

    private static Point3 Bilinear(IReadOnlyList<Point3> f, double u, double v)
    {
        Point3 L(Point3 a, Point3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        return L(L(f[0], f[1], u), L(f[3], f[2], u), v);
    }

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
