using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// O pior caso de um período (9.8): para cada face, a maior fração
/// sombreada e o instante em que ela aconteceu.
/// </summary>
/// <param name="Fractions">A maior fração sombreada de cada face (0 a 1), na ordem das faces.</param>
/// <param name="When">O instante dessa maior fração, ou null se a face nunca pegou sombra.</param>
/// <param name="WorstInstant">O instante com mais área sombreada somada, ou null se nada pegou sombra.</param>
/// <param name="InstantsWithSun">Quantos instantes do período tinham sol acima do mínimo.</param>
/// <param name="Instants">Quantos instantes o período tinha.</param>
/// <param name="Causes">O que fez a sombra no pior instante de cada face (null nas contas só com árvores).</param>
public sealed record ShadingWorstCase(
    IReadOnlyList<double> Fractions, IReadOnlyList<DateTime?> When, DateTime? WorstInstant, int InstantsWithSun, int Instants,
    IReadOnlyList<ShadowCause>? Causes = null)
{
    /// <summary>Quantas faces pegaram alguma sombra.</summary>
    public int ShadedCount => Fractions.Count(f => f > 0);
}

/// <summary>
/// A sombra dos objetos (9.6, 9.8). Cada objeto é um ou mais cilindros
/// verticais (a árvore é tronco e copa). Uma face de módulo é amostrada numa
/// grade; um ponto está na sombra se o raio dele na direção do sol bate num
/// cilindro. A fração da face é a dos pontos na sombra. O contorno da sombra
/// no chão é a envoltória das projeções dos círculos da base e do topo,
/// assentada no terreno (regra 5: tudo que desenha acompanha o terreno).
/// </summary>
public static class Shading
{
    /// <summary>Abaixo disto (graus), o sol é tratado como posto: sombra infinita não é desenhada nem contada.</summary>
    public const double MinimumElevationDegrees = 2.0;

    /// <summary>Se o cilindro bloqueia o raio que sai de <paramref name="p"/> na direção do sol.</summary>
    public static bool Blocks(ShadowCylinder c, Point3 p, (double X, double Y, double Z) sun)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (sun.Z <= 0) return false;

        var dx = p.X - c.X;
        var dy = p.Y - c.Y;
        var a = sun.X * sun.X + sun.Y * sun.Y;
        var cc = dx * dx + dy * dy - c.Radius * c.Radius;

        // Sol a pino: o raio é vertical.
        if (a < 1e-12) return cc <= 0 && c.Top > p.Z;

        var b = 2 * (dx * sun.X + dy * sun.Y);
        var disc = b * b - 4 * a * cc;
        if (disc < 0) return false;

        var raiz = Math.Sqrt(disc);
        var t1 = Math.Max((-b - raiz) / (2 * a), 0);
        var t2 = (-b + raiz) / (2 * a);
        if (t2 <= 0) return false;

        // Ao longo do raio a cota só sobe: [z1, z2] é o trecho dentro do
        // cilindro infinito; bloqueia se cruza a altura dele.
        var z1 = p.Z + t1 * sun.Z;
        var z2 = p.Z + t2 * sun.Z;

        return z2 >= c.Bottom && z1 <= c.Top;
    }

    /// <summary>
    /// A fração de uma face (quatro cantos, em ordem) na sombra dos
    /// cilindros, amostrada numa grade de <paramref name="along"/> por
    /// <paramref name="across"/> pontos (o centro de cada célula).
    /// </summary>
    public static double FaceFraction(IReadOnlyList<Point3> face, IReadOnlyList<ShadowCylinder> cylinders, (double X, double Y, double Z) sun, int along = 6, int across = 3)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(cylinders);
        if (face.Count != 4) throw new ArgumentException("a face precisa de quatro cantos", nameof(face));
        if (sun.Z <= 0 || cylinders.Count == 0) return 0;

        var sombra = 0;

        for (var i = 0; i < along; i++)
        {
            var u = (i + 0.5) / along;

            for (var j = 0; j < across; j++)
            {
                var v = (j + 0.5) / across;
                var p = Bilinear(face, u, v);

                foreach (var c in cylinders)
                {
                    if (!Blocks(c, p, sun)) continue;

                    sombra++;
                    break;
                }
            }
        }

        return (double)sombra / (along * across);
    }

    /// <summary>
    /// A fração sombreada de cada face num instante. Só as faces que caem na
    /// caixa da sombra de algum cilindro são amostradas: numa usina de
    /// milhares de módulos, a árvore alcança poucos.
    /// </summary>
    public static double[] Fractions(IReadOnlyList<IReadOnlyList<Point3>> faces, IReadOnlyList<ShadowCylinder> cylinders, (double X, double Y, double Z) sun)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(cylinders);

        return Fractions(faces, Caixas(faces), cylinders, sun);
    }

    /// <summary>As caixas em planta das faces e a cota mais baixa delas, calculadas uma vez para o período inteiro.</summary>
    private static (double MinX, double MaxX, double MinY, double MaxY, double Chao)[] Caixas(IReadOnlyList<IReadOnlyList<Point3>> faces) =>
        faces.Select(f => (f.Min(p => p.X), f.Max(p => p.X), f.Min(p => p.Y), f.Max(p => p.Y), f.Min(p => p.Z))).ToArray();

    private static double[] Fractions(
        IReadOnlyList<IReadOnlyList<Point3>> faces, (double MinX, double MaxX, double MinY, double MaxY, double Chao)[] caixasDasFaces,
        IReadOnlyList<ShadowCylinder> cylinders, (double X, double Y, double Z) sun)
    {
        var resultado = new double[faces.Count];
        if (sun.Z <= 0 || cylinders.Count == 0 || faces.Count == 0) return resultado;

        var chao = caixasDasFaces.Min(c => c.Chao);
        var caixas = cylinders.Select(c => (Cilindro: c, Caixa: Caixa(c, sun, chao))).ToList();
        var perto = new List<ShadowCylinder>(cylinders.Count);

        for (var k = 0; k < faces.Count; k++)
        {
            var f = caixasDasFaces[k];
            perto.Clear();

            foreach (var c in caixas)
                if (c.Caixa.MaxX >= f.MinX && c.Caixa.MinX <= f.MaxX && c.Caixa.MaxY >= f.MinY && c.Caixa.MinY <= f.MaxY) perto.Add(c.Cilindro);

            if (perto.Count > 0) resultado[k] = FaceFraction(faces[k], perto, sun);
        }

        return resultado;
    }

    /// <summary>
    /// O pior caso de cada face num período: a maior fração e quando foi.
    /// </summary>
    public static ShadingWorstCase Worst(
        IReadOnlyList<IReadOnlyList<Point3>> faces, IReadOnlyList<ShadowCylinder> cylinders,
        double latitude, double longitude, double utcOffsetHours, IEnumerable<DateTime> instants)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(instants);

        var caixasDasFaces = Caixas(faces);
        var pior = new double[faces.Count];
        var quando = new DateTime?[faces.Count];
        DateTime? piorInstante = null;
        var piorSoma = 0.0;
        var comSol = 0;
        var total = 0;

        foreach (var instante in instants)
        {
            total++;
            var sol = SolarCalculator.Compute(latitude, longitude, instante, utcOffsetHours);
            if (sol.ElevationDegrees < MinimumElevationDegrees) continue;

            comSol++;
            var fracoes = Fractions(faces, caixasDasFaces, cylinders, sol.Direction);
            var soma = 0.0;

            for (var k = 0; k < fracoes.Length; k++)
            {
                soma += fracoes[k];
                if (fracoes[k] <= pior[k]) continue;

                pior[k] = fracoes[k];
                quando[k] = instante;
            }

            if (soma > piorSoma)
            {
                piorSoma = soma;
                piorInstante = instante;
            }
        }

        return new ShadingWorstCase(pior, quando, piorInstante, comSol, total);
    }

    /// <summary>
    /// Os instantes de um período: cada dia de <paramref name="firstDay"/> a
    /// <paramref name="lastDay"/>, das <paramref name="from"/> às
    /// <paramref name="to"/>, de <paramref name="step"/> em
    /// <paramref name="step"/>. Um instante só: o mesmo dia e a mesma hora.
    /// Um horário fixo num período: <paramref name="from"/> igual a
    /// <paramref name="to"/>.
    /// </summary>
    public static IEnumerable<DateTime> Instants(DateOnly firstDay, DateOnly lastDay, TimeOnly from, TimeOnly to, TimeSpan step, int dayStep = 1)
    {
        if (dayStep < 1) throw new ArgumentOutOfRangeException(nameof(dayStep), "o passo de dias precisa ser pelo menos 1");
        if (lastDay < firstDay) throw new ArgumentException("o último dia vem antes do primeiro", nameof(lastDay));
        if (to < from) throw new ArgumentException("a hora final vem antes da inicial", nameof(to));
        if (step <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step), "o passo precisa ser positivo");

        return Gerar();

        IEnumerable<DateTime> Gerar()
        {
            for (var dia = firstDay; dia <= lastDay; dia = dia.AddDays(dayStep))
            {
                var inicio = dia.ToDateTime(from);
                var fim = dia.ToDateTime(to);

                for (var t = inicio; t <= fim; t += step) yield return t;
            }
        }
    }

    /// <summary>
    /// Quantos instantes o período tem: (dias) × (passos por dia). Acima de
    /// <see cref="MaxInstants"/> a conta travaria o CAD por minutos.
    /// </summary>
    public static long CountInstants(DateOnly firstDay, DateOnly lastDay, TimeOnly from, TimeOnly to, TimeSpan step, int dayStep = 1)
    {
        if (lastDay < firstDay || to < from || step <= TimeSpan.Zero || dayStep < 1) return 0;
        var porDia = (long)Math.Floor((to - from).TotalMinutes / step.TotalMinutes) + 1;
        var dias = (lastDay.DayNumber - firstDay.DayNumber) / dayStep + 1L;
        return dias * porDia;
    }

    /// <summary>O máximo de instantes de um período: um ano de hora em hora das 6h às 18h cabe com folga.</summary>
    public const int MaxInstants = 20_000;

    /// <summary>
    /// O contorno da sombra de um cilindro no chão: a envoltória (em planta)
    /// dos círculos da base e do topo projetados na direção do sol até o
    /// terreno, cada vértice com a cota do terreno. Vazio com o sol baixo.
    /// </summary>
    /// <param name="c">O cilindro.</param>
    /// <param name="sun">O vetor que aponta para o sol.</param>
    /// <param name="ground">A cota do terreno em (x, y), ou null fora dele.</param>
    /// <param name="segments">Quantos pontos em cada círculo.</param>
    public static IReadOnlyList<Point3> ShadowOutline(ShadowCylinder c, (double X, double Y, double Z) sun, Func<double, double, double?> ground, int segments = 32)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(ground);

        var elevacao = Math.Asin(Math.Clamp(sun.Z, -1, 1)) * 180 / Math.PI;
        if (elevacao < MinimumElevationDegrees) return [];

        var pontos = new List<(double X, double Y)>(4 * segments);

        for (var i = 0; i < segments; i++)
        {
            var a = 2 * Math.PI * i / segments;
            var x = c.X + c.Radius * Math.Cos(a);
            var y = c.Y + c.Radius * Math.Sin(a);

            foreach (var z in new[] { c.Bottom, c.Top })
            {
                var q = NoChao(new Point3(x, y, z), sun, ground, c.Bottom);
                pontos.Add((q.X, q.Y));
            }
        }

        return Envoltoria(pontos).Select(p => new Point3(p.X, p.Y, ground(p.X, p.Y) ?? c.Bottom)).ToList();
    }

    /// <summary>
    /// A sombra de um cilindro sobre uma mesa (04/10/2026, Renan: "a sombra é
    /// projetada somente na superfície TIN e não considera que os módulos irão
    /// receber as sombras"): os círculos da base e do topo da parte do
    /// cilindro acima do plano, projetados na direção do sol até o plano da
    /// mesa, a envoltória deles recortada pelo contorno da mesa. Cada vértice
    /// fica no plano. Vazio se a sombra não cai na mesa ou o sol está baixo.
    /// </summary>
    /// <param name="c">O cilindro.</param>
    /// <param name="sun">O vetor que aponta para o sol.</param>
    /// <param name="table">O contorno da mesa (plano e convexo), em ordem.</param>
    /// <param name="segments">Quantos pontos em cada círculo.</param>
    public static IReadOnlyList<Point3> ShadowOnPlane(ShadowCylinder c, (double X, double Y, double Z) sun, IReadOnlyList<Point3> table, int segments = 32)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count < 3) return [];

        var elevacao = Math.Asin(Math.Clamp(sun.Z, -1, 1)) * 180 / Math.PI;
        if (elevacao < MinimumElevationDegrees) return [];

        // O plano da mesa: a normal pelo produto vetorial de dois lados.
        var p0 = table[0];
        var (ux, uy, uz) = (table[1].X - p0.X, table[1].Y - p0.Y, table[1].Z - p0.Z);
        var (vx, vy, vz) = (table[2].X - p0.X, table[2].Y - p0.Y, table[2].Z - p0.Z);
        var (nx, ny, nz) = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
        if (Math.Abs(nz) < 1e-9) return [];

        double CotaDoPlano(double x, double y) => p0.Z - (nx * (x - p0.X) + ny * (y - p0.Y)) / nz;

        var ns = nx * sun.X + ny * sun.Y + nz * sun.Z;
        if (Math.Abs(ns) < 1e-9) return [];

        // Só a parte do cilindro acima do plano faz sombra nele.
        var baseNoPlano = CotaDoPlano(c.X, c.Y);
        var baixo = Math.Max(c.Bottom, baseNoPlano);
        if (c.Top <= baixo) return [];

        var pontos = new List<(double X, double Y)>(2 * segments);

        for (var i = 0; i < segments; i++)
        {
            var a = 2 * Math.PI * i / segments;
            var x = c.X + c.Radius * Math.Cos(a);
            var y = c.Y + c.Radius * Math.Sin(a);

            foreach (var z in new[] { baixo, c.Top })
            {
                // q - s·t no plano: n·(q - s·t - p0) = 0.
                var t = (nx * (x - p0.X) + ny * (y - p0.Y) + nz * (z - p0.Z)) / ns;
                if (t < 0) continue;
                pontos.Add((x - sun.X * t, y - sun.Y * t));
            }
        }

        if (pontos.Count < 3) return [];

        var recorte = Recortar(Envoltoria(pontos), table.Select(p => (p.X, p.Y)).ToList());
        return recorte.Count < 3 ? [] : recorte.Select(p => new Point3(p.X, p.Y, CotaDoPlano(p.X, p.Y))).ToList();
    }

    /// <summary>
    /// A cota do plano da mesa (o contorno, plano) no ponto (x, y); null se o
    /// contorno não define um plano que se lê em planta.
    /// </summary>
    public static double? PlaneHeight(IReadOnlyList<Point3> table, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count < 3) return null;

        var p0 = table[0];
        var (ux, uy, uz) = (table[1].X - p0.X, table[1].Y - p0.Y, table[1].Z - p0.Z);
        var (vx, vy, vz) = (table[2].X - p0.X, table[2].Y - p0.Y, table[2].Z - p0.Z);
        var (nx, ny, nz) = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
        if (Math.Abs(nz) < 1e-9) return null;

        return p0.Z - (nx * (x - p0.X) + ny * (y - p0.Y)) / nz;
    }

    /// <summary>Sutherland–Hodgman: o polígono recortado por um contorno convexo (os dois em planta).</summary>
    private static List<(double X, double Y)> Recortar(IReadOnlyList<(double X, double Y)> poligono, List<(double X, double Y)> contorno)
    {
        // O contorno no sentido anti-horário.
        var area = 0.0;
        for (var i = 0; i < contorno.Count; i++)
        {
            var (a, b) = (contorno[i], contorno[(i + 1) % contorno.Count]);
            area += a.X * b.Y - b.X * a.Y;
        }
        if (area < 0) contorno.Reverse();

        var saida = poligono.ToList();

        for (var i = 0; i < contorno.Count && saida.Count > 0; i++)
        {
            var (a, b) = (contorno[i], contorno[(i + 1) % contorno.Count]);
            double Lado((double X, double Y) p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

            var entrada = saida;
            saida = [];

            for (var j = 0; j < entrada.Count; j++)
            {
                var atual = entrada[j];
                var anterior = entrada[(j + entrada.Count - 1) % entrada.Count];
                var (la, lp) = (Lado(atual), Lado(anterior));

                if (la >= 0)
                {
                    if (lp < 0) saida.Add(Cruzamento(anterior, atual, lp, la));
                    saida.Add(atual);
                }
                else if (lp >= 0)
                {
                    saida.Add(Cruzamento(anterior, atual, lp, la));
                }
            }
        }

        return saida;

        static (double X, double Y) Cruzamento((double X, double Y) p, (double X, double Y) q, double lp, double lq)
        {
            var t = lp / (lp - lq);
            return (p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);
        }
    }

    /// <summary>
    /// O ponto projetado na direção oposta ao sol até o terreno: a cota do
    /// chão muda com o lugar, então a conta é refeita algumas vezes com a
    /// cota do ponto achado.
    /// </summary>
    private static Point3 NoChao(Point3 p, (double X, double Y, double Z) sun, Func<double, double, double?> ground, double cotaReserva)
    {
        var z = ground(p.X, p.Y) ?? cotaReserva;
        var q = p;

        for (var i = 0; i < 6; i++)
        {
            var t = Math.Max((p.Z - z) / sun.Z, 0);
            q = new Point3(p.X - sun.X * t, p.Y - sun.Y * t, z);

            var novo = ground(q.X, q.Y) ?? z;
            if (Math.Abs(novo - z) < 1e-3) break;
            z = novo;
        }

        return q with { Z = z };
    }

    /// <summary>A caixa em planta onde o cilindro pode fazer sombra em pontos acima de <paramref name="chao"/>.</summary>
    private static (double MinX, double MaxX, double MinY, double MaxY) Caixa(ShadowCylinder c, (double X, double Y, double Z) sun, double chao)
    {
        var t = Math.Max((c.Top - chao) / sun.Z, 0);
        var px = c.X - sun.X * t;
        var py = c.Y - sun.Y * t;
        var r = c.Radius;

        return (Math.Min(c.X, px) - r, Math.Max(c.X, px) + r, Math.Min(c.Y, py) - r, Math.Max(c.Y, py) + r);
    }

    private static Point3 Bilinear(IReadOnlyList<Point3> f, double u, double v)
    {
        // f[0]→f[1] é a borda de baixo, f[3]→f[2] a de cima.
        Point3 L(Point3 a, Point3 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        return L(L(f[0], f[1], u), L(f[3], f[2], u), v);
    }

    /// <summary>A envoltória convexa (monotone chain), anti-horária.</summary>
    internal static List<(double X, double Y)> Envoltoria(List<(double X, double Y)> pontos)
    {
        var p = pontos.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;

        static double Cruz((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
            (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var h = new List<(double X, double Y)>(2 * p.Count);

        foreach (var q in p)
        {
            while (h.Count >= 2 && Cruz(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1);
            h.Add(q);
        }

        var baixo = h.Count + 1;

        for (var i = p.Count - 2; i >= 0; i--)
        {
            var q = p[i];
            while (h.Count >= baixo && Cruz(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1);
            h.Add(q);
        }

        h.RemoveAt(h.Count - 1);
        return h;
    }
}
