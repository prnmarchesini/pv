using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Um objeto do desenho do cliente que marca uma mesa (40.1): uma polilinha
/// fechada de 4 vértices, 2D ou 3D, como veio do desenho.
/// </summary>
/// <param name="Id">O handle do objeto, para a divergência apontar qual é.</param>
/// <param name="Layer">A camada, só para o relatório.</param>
public sealed record FieldObject(string Id, string Layer, IReadOnlyList<Point3> Vertices);

/// <summary>
/// A mesa que um objeto marca, em planta: o retângulo visto de cima e o eixo
/// da fileira. Só X e Y (40.2, Renan 30/10/2026: "o objeto ... está em 2D, o
/// eixo Z deve estar cagado, então vc vai gerar a mesa respeitando apenas a
/// posição X e Y e encaixar a Z no terreno conforme topografia"): a cota, a
/// inclinação e o lado da ponta baixa não saem do objeto. A inclinação é a da
/// mesa do Clivus, e o lado da ponta baixa, o do azimute do projeto.
/// </summary>
/// <param name="Center">O centro do retângulo em planta (Z zero; a mesa senta no terreno).</param>
/// <param name="Axis">O rumo do eixo da fileira em planta, em radianos, de +X no sentido anti-horário, em [0, π).</param>
/// <param name="Length">O lado maior, em planta (ao longo da fileira).</param>
/// <param name="Width">A largura em planta: a distância entre os dois lados maiores.</param>
public sealed record FieldFootprint(
    string Id,
    string Layer,
    Point3 Center,
    double Axis,
    double Length,
    double Width)
{
    /// <summary>O versor do eixo em planta.</summary>
    public (double X, double Y) AxisVector => (Math.Cos(Axis), Math.Sin(Axis));

    /// <summary>Os quatro cantos em planta (Z zero), em ordem, a partir do canto de trás-esquerda.</summary>
    public IReadOnlyList<Point3> PlanCorners()
    {
        var (ux, uy) = AxisVector;
        var (hx, hy) = (-uy, ux);
        var (a, b) = (Length / 2, Width / 2);

        return
        [
            new(Center.X - ux * a + hx * b, Center.Y - uy * a + hy * b, 0),
            new(Center.X + ux * a + hx * b, Center.Y + uy * a + hy * b, 0),
            new(Center.X + ux * a - hx * b, Center.Y + uy * a - hy * b, 0),
            new(Center.X - ux * a - hx * b, Center.Y - uy * a - hy * b, 0),
        ];
    }
}

/// <summary>O tipo de divergência de um objeto (40.1).</summary>
public enum FieldIssueKind
{
    /// <summary>Não é um retângulo de 4 vértices.</summary>
    NotRectangle,

    /// <summary>Tamanho diferente da maioria.</summary>
    Size,

    /// <summary>Rumo diferente da maioria.</summary>
    Axis,

    /// <summary>Outro objeto no mesmo lugar: só um vira mesa.</summary>
    Duplicate,

    /// <summary>Em cima de parte de outro objeto: o segundo não vira mesa.</summary>
    Overlap,

    /// <summary>O centro fora da área: é de outra área, não vira mesa aqui.</summary>
    OutsideArea,

    /// <summary>Atravessa o contorno da área: vira mesa, com aviso.</summary>
    CrossesArea,

    /// <summary>A mesa do Clivus não tem o tamanho dos objetos.</summary>
    ProfileSize,

    /// <summary>Nenhuma mesa em uso tem perto do tamanho do objeto: ele não vira mesa (40.2).</summary>
    NoProfile,

    /// <summary>A mesa desenhada ficou longe do lugar no objeto (40.2): o motor a sentou no terreno com outra rampa.</summary>
    Deviation,
}

/// <summary>Uma divergência: o objeto (ou "*" quando é de todos), o tipo e a frase.</summary>
public sealed record FieldIssue(string Id, FieldIssueKind Kind, string Message)
{
    /// <summary>Se o objeto fica sem mesa por causa desta divergência.</summary>
    public bool Skips => Kind is FieldIssueKind.NotRectangle or FieldIssueKind.Duplicate or FieldIssueKind.Overlap
        or FieldIssueKind.OutsideArea or FieldIssueKind.NoProfile;
}

/// <summary>Uma fileira de objetos: as mesas uma depois da outra, no sentido do eixo.</summary>
public sealed record FieldRow(IReadOnlyList<FieldFootprint> Tables);

/// <summary>
/// O levantamento dos objetos de uma área (40.1): as pegadas, as fileiras com
/// as que viram mesa e as divergências. A referência é o que a maioria tem.
/// </summary>
public sealed record FieldSurvey(
    IReadOnlyList<FieldFootprint> Footprints,
    IReadOnlyList<FieldRow> Rows,
    IReadOnlyList<FieldIssue> Issues,
    double ReferenceLength,
    double ReferenceWidth,
    double ReferenceAxis)
{
    /// <summary>Quantas mesas saem (os objetos que entraram nas fileiras).</summary>
    public int TableCount => Rows.Sum(r => r.Tables.Count);
}

/// <summary>
/// Reconhece os objetos do cliente como mesas (40.1, Renan 30/10/2026: "quero
/// ter a opção de escolher os objetos em planta e falar 'esses objetos são
/// mesas' [...] Se tiver divergência ele deve apontar, em teoria todos objetos
/// devem ser iguais").
///
/// Tudo em planta, só com X e Y (revisão de tela do 40.2: "o eixo Z deve
/// estar cagado"). O objeto do Aurora é a área dos módulos de uma 2P14 como
/// retângulo 3D (18,502 × 4,788 m, 10° em torno do eixo, o eixo acompanhando
/// a rampa da fileira): visto de cima, ele tem 4,715 m de largura e é um
/// paralelogramo, e não um retângulo, quando a fileira tem rampa. Por isso a
/// largura é a distância entre os lados maiores, e o ângulo reto tem folga
/// de <see cref="RightAngleTolerance"/>.
/// </summary>
public static class FieldObjects
{
    /// <summary>Diferença de tamanho que conta como outro objeto: 2 cm.</summary>
    public const double SizeTolerance = 0.02;

    /// <summary>
    /// No comprimento em planta, a folga é 1,5 % dele: a rampa da fileira
    /// encurta o objeto 3D visto de cima (5 cm no D6D4 do Aurora, 1,1 % a 15 %
    /// de rampa), e 2 cm apontariam quase todo objeto como de outro tamanho.
    /// </summary>
    public const double PlanLengthTolerance = 0.015;

    /// <summary>Se dois comprimentos em planta são o mesmo, com a folga da rampa.</summary>
    public static bool SameLength(double a, double b) =>
        Math.Abs(a - b) <= Math.Max(SizeTolerance, PlanLengthTolerance * Math.Max(a, b));

    /// <summary>Diferença de rumo que conta: meio grau.</summary>
    public const double AngleTolerance = 0.5 * Math.PI / 180;

    /// <summary>
    /// Quanto o retângulo pode fugir do ângulo reto em planta: três graus. O
    /// retângulo 3D inclinado na rampa da fileira aparece de cima como
    /// paralelogramo, e o desvio cresce com a rampa (um grau a 10 %, com a
    /// mesa a 10°).
    /// </summary>
    public const double RightAngleTolerance = 3.0 * Math.PI / 180;

    /// <summary>Dois objetos com o centro a menos disto são o mesmo lugar: 5 cm.</summary>
    public const double SamePlace = 0.05;

    /// <summary>Quanto um canto pode passar do contorno da área sem contar como atravessar: 10 cm.</summary>
    public const double AreaTolerance = 0.10;

    /// <summary>Quanto o centro de uma mesa pode sair da linha da fileira: meio metro.</summary>
    public const double RowLineTolerance = 0.5;

    /// <summary>
    /// A folga entre mesas acima da qual a fileira se parte: a mesma do motor
    /// (<see cref="SystemConfiguration.MaxGapBeforeBreak"/>, 5 m no padrão), para a
    /// fileira do Clivus ser a corrente que o motor resolve.
    /// </summary>
    public static double DefaultRowBreak => SystemConfiguration.Default.MaxGapBeforeBreak;

    /// <summary>
    /// A pegada do objeto em planta, ou null com o motivo quando ele não é um
    /// retângulo de 4 vértices (o vértice repetido no fim não conta). O Z dos
    /// vértices não entra em nada: pode vir 0, 9999 ou torto.
    /// </summary>
    public static FieldFootprint? Footprint(FieldObject objeto, out string? whyNot)
    {
        ArgumentNullException.ThrowIfNull(objeto);

        if (objeto.Vertices.Any(q => !double.IsFinite(q.X) || !double.IsFinite(q.Y)))
        {
            whyNot = Tr.T("tem coordenada inválida");
            return null;
        }

        var p = objeto.Vertices.Select(q => new Point3(q.X, q.Y, 0)).ToList();
        if (p.Count == 5 && Distance(p[0], p[4]) < 1e-6) p.RemoveAt(4);

        if (p.Count != 4)
        {
            whyNot = Tr.F("tem {0} vértices; a mesa é um retângulo de 4", p.Count);
            return null;
        }

        // O lado maior é o eixo: p0→p1 ou p1→p2.
        if (Distance(p[0], p[1]) < Distance(p[1], p[2])) p = [p[1], p[2], p[3], p[0]];

        var u = Minus(p[1], p[0]);
        var v = Minus(p[3], p[0]);
        var comprimento = (Distance(p[0], p[1]) + Distance(p[3], p[2])) / 2;

        // A largura é a distância entre os lados maiores: no paralelogramo do
        // retângulo 3D visto de cima, a altura dele, e não o lado.
        var largura = comprimento < 0.05 ? 0 : Math.Abs(u.X * v.Y - u.Y * v.X) / Norm(u);

        if (largura < 0.05)
        {
            whyNot = Tr.T("é uma linha, não um retângulo");
            return null;
        }

        // Paralelogramo (p2 = p1 + v) e ângulo reto, com a folga do inclinado.
        var p2Esperado = new Point3(p[1].X + v.X, p[1].Y + v.Y, 0);
        if (Distance(p2Esperado, p[2]) > SizeTolerance)
        {
            whyNot = Tr.T("os lados opostos não são paralelos e iguais");
            return null;
        }

        var cosseno = Math.Abs(Dot(u, v)) / (Norm(u) * Norm(v));
        if (cosseno > Math.Sin(RightAngleTolerance))
        {
            whyNot = Tr.F("os cantos têm {0:0.#}°, e não 90°", Math.Acos(Math.Min(1, cosseno)) * 180 / Math.PI);
            return null;
        }

        // O eixo em [0, π).
        var rumo = Math.Atan2(u.Y, u.X);
        if (rumo < 0 || rumo >= Math.PI - 1e-12) rumo = Math.Atan2(-u.Y, -u.X);
        if (rumo < 0) rumo += Math.PI;
        if (rumo >= Math.PI) rumo -= Math.PI;

        var centro = new Point3(p.Average(q => q.X), p.Average(q => q.Y), 0);

        whyNot = null;
        return new FieldFootprint(objeto.Id, objeto.Layer, centro, rumo, comprimento, largura);
    }

    /// <summary>
    /// O levantamento dos objetos: pegadas, divergências e fileiras.
    /// </summary>
    /// <param name="area">O contorno da área em planta, ou null para não conferir.</param>
    /// <param name="profile">A mesa do Clivus que vai nos objetos, ou null para não conferir.</param>
    /// <param name="rowBreak">A folga entre duas mesas acima da qual a fileira se parte; null, a do motor.</param>
    public static FieldSurvey Survey(
        IReadOnlyList<FieldObject> objects,
        IReadOnlyList<Point3>? area = null,
        TableProfile? profile = null,
        double? rowBreak = null)
    {
        ArgumentNullException.ThrowIfNull(objects);

        var problemas = new List<FieldIssue>();
        var pegadas = new List<FieldFootprint>();

        foreach (var objeto in objects)
        {
            if (Footprint(objeto, out var porQue) is { } pegada)
                pegadas.Add(pegada);
            else
                problemas.Add(new FieldIssue(objeto.Id, FieldIssueKind.NotRectangle, Tr.F("{0} ({1}) não vira mesa: {2}.", objeto.Id, objeto.Layer, porQue!)));
        }

        if (pegadas.Count == 0)
            return new FieldSurvey(pegadas, [], problemas, 0, 0, 0);

        // A referência: o que a maioria tem.
        var (refL, refW) = pegadas
            .GroupBy(f => (Math.Round(f.Length, 2), Math.Round(f.Width, 2)))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Item1).First().Key;
        var refRumo = pegadas
            .GroupBy(f => Math.Round(f.Axis * 180 / Math.PI, 1) % 180)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key * Math.PI / 180;
        // O eixo sai em [0, π): a 179,98° e a 0,02° são a mesma fileira em
        // sentidos opostos. Cada pegada passa para o sentido da referência,
        // senão as fileiras e as células saem viradas (revisão do 40.2).
        pegadas = [.. pegadas.Select(f => Math.Cos(f.Axis - refRumo) >= 0 ? f : f with
        {
            Axis = Math.Atan2(Math.Sin(f.Axis + Math.PI), Math.Cos(f.Axis + Math.PI)),
        })];

        var pulados = new HashSet<string>();

        foreach (var f in pegadas)
        {
            if (!SameLength(f.Length, refL) || Math.Abs(f.Width - refW) > SizeTolerance)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Size,Tr.F("{0} ({1}) tem {2:0.###} × {3:0.###} m; a maioria tem {4:0.###} × {5:0.###} m.", f.Id, f.Layer, f.Length, f.Width, refL, refW)));

            if (AxisDifference(f.Axis, refRumo) > AngleTolerance)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Axis, Tr.F("{0} ({1}) está a {2:0.#}° da direção da maioria das fileiras.", f.Id, f.Layer, AxisDifference(f.Axis, refRumo) * 180 / Math.PI)));

            if (area is { Count: >= 3 })
            {
                if (!Polygons.Contains(area, f.Center.X, f.Center.Y))
                {
                    problemas.Add(new FieldIssue(f.Id, FieldIssueKind.OutsideArea, Tr.F("{0} ({1}) está fora da área.", f.Id, f.Layer)));
                    pulados.Add(f.Id);
                }
                else if (Corners(f).Any(c => !Polygons.Contains(area, c.X, c.Y) && DistanceToContour(area, c) > AreaTolerance))
                {
                    problemas.Add(new FieldIssue(f.Id, FieldIssueKind.CrossesArea, Tr.F("{0} ({1}) atravessa o contorno da área: a mesa sai, mas passa da área.", f.Id, f.Layer)));
                }
            }
        }

        // Dois objetos no mesmo lugar, ou um em cima do outro: fica o primeiro.
        // Os do tamanho da maioria primeiro: um retângulo que não é mesa
        // (cerca, lote) não tira o lugar das mesas que cobre (revisão do 40.2).
        bool DaMaioria(FieldFootprint f) => SameLength(f.Length, refL) && Math.Abs(f.Width - refW) <= SizeTolerance;
        var ordem = pegadas.Where(f => !pulados.Contains(f.Id)).OrderBy(f => DaMaioria(f) ? 0 : 1).ToList();
        for (var i = 0; i < ordem.Count; i++)
        {
            if (pulados.Contains(ordem[i].Id)) continue;

            for (var j = i + 1; j < ordem.Count; j++)
            {
                var (a, b) = (ordem[i], ordem[j]);
                if (pulados.Contains(b.Id)) continue;

                var alcance = Math.Max(a.Length, b.Length) + Math.Max(a.Width, b.Width);
                if (Math.Abs(a.Center.X - b.Center.X) > alcance || Math.Abs(a.Center.Y - b.Center.Y) > alcance) continue;

                if (PlanDistance(a.Center, b.Center) < SamePlace && Math.Abs(a.Length - b.Length) <= SizeTolerance && Math.Abs(a.Width - b.Width) <= SizeTolerance)
                {
                    problemas.Add(new FieldIssue(b.Id, FieldIssueKind.Duplicate, Tr.F("{0} ({1}) está no mesmo lugar que {2} ({3}): só uma mesa sai.", b.Id, b.Layer, a.Id, a.Layer)));
                    pulados.Add(b.Id);
                }
                else if (Intersect(Corners(a), Corners(b)))
                {
                    problemas.Add(new FieldIssue(b.Id, FieldIssueKind.Overlap, Tr.F("{0} ({1}) está em cima de parte de {2} ({3}): só {2} vira mesa. Apague ou mova o que sobra e gere de novo.", b.Id, b.Layer, a.Id, a.Layer)));
                    pulados.Add(b.Id);
                }
            }
        }

        if (profile is not null) problemas.AddRange(ProfileIssues(profile, refL, refW));

        var aceitas = pegadas.Where(f => !pulados.Contains(f.Id)).ToList();
        var fileiras = RowsByAxis(aceitas, rowBreak ?? DefaultRowBreak);

        return new FieldSurvey(pegadas, fileiras, problemas, refL, refW, refRumo);
    }

    /// <summary>
    /// A mesa do Clivus e os objetos, em planta: o objeto pode ser a área dos
    /// módulos ou a mesa inteira (com as sobras), e a largura pode ser a de
    /// planta ou a da inclinação (o retângulo plano desenhado com a largura
    /// dos módulos).
    /// </summary>
    public static IReadOnlyList<FieldIssue> ProfileIssues(TableProfile profile, double length, double width)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var problemas = new List<FieldIssue>();
        var layout = profile.Layout;
        var modulos = layout.Length - layout.LeftMargin - layout.RightMargin;
        var emPlanta = layout.Depth * Math.Cos(profile.TiltRadians);

        var bateComprimento = SameLength(length, modulos) || SameLength(length, layout.Length);
        var bateLargura = Math.Abs(width - emPlanta) <= SizeTolerance || Math.Abs(width - layout.Depth) <= SizeTolerance;

        if (!bateComprimento || !bateLargura)
        {
            problemas.Add(new FieldIssue("*", FieldIssueKind.ProfileSize, Tr.F(
                "a mesa \"{0}\" tem {1:0.###} m de módulos ({2:0.###} m com as sobras) × {3:0.###} m em planta; os objetos têm {4:0.###} × {5:0.###} m.",
                profile.Name, modulos, layout.Length, emPlanta, length, width)));
        }

        return problemas;
    }

    /// <summary>
    /// As fileiras: as pegadas na mesma linha do eixo (o centro a menos de
    /// <see cref="RowLineTolerance"/> dela), em ordem ao longo do eixo; a
    /// fileira se parte onde a folga entre duas mesas passa de
    /// <paramref name="rowBreak"/>. As fileiras saem de trás para a frente
    /// (do lado esquerdo do eixo para o direito) e, na mesma linha, no sentido
    /// do eixo.
    /// </summary>
    /// <summary>
    /// As fileiras, cada grupo de eixos pelo eixo dele: uma fileira a 3° da
    /// maioria continua uma fileira, e não mesas soltas (revisão do 40.2).
    /// Os grupos são de eixos a até 1° um do outro; as pegadas já estão no
    /// sentido da referência (<see cref="Survey"/>).
    /// </summary>
    public static IReadOnlyList<FieldRow> RowsByAxis(IReadOnlyList<FieldFootprint> footprints, double? rowBreak = null)
    {
        ArgumentNullException.ThrowIfNull(footprints);

        var grupos = new List<List<FieldFootprint>>();

        foreach (var f in footprints.OrderBy(f => f.Axis))
        {
            var grupo = grupos.FirstOrDefault(g => AxisDifference(g[0].Axis, f.Axis) <= 1.0 * Math.PI / 180 && Math.Cos(g[0].Axis - f.Axis) > 0);
            if (grupo is null) grupos.Add([f]);
            else grupo.Add(f);
        }

        return [.. grupos.OrderByDescending(g => g.Count).SelectMany(g => Rows(g, Math.Atan2(g.Average(f => Math.Sin(f.Axis)), g.Average(f => Math.Cos(f.Axis))), rowBreak))];
    }

    public static IReadOnlyList<FieldRow> Rows(IReadOnlyList<FieldFootprint> footprints, double axis, double? rowBreak = null)
    {
        var quebra = rowBreak ?? DefaultRowBreak;
        ArgumentNullException.ThrowIfNull(footprints);

        var (ux, uy) = (Math.Cos(axis), Math.Sin(axis));
        var (hx, hy) = (-uy, ux);

        var porLinha = footprints
            .Select(f => (Pegada: f, Ao: f.Center.X * ux + f.Center.Y * uy, Lado: f.Center.X * hx + f.Center.Y * hy))
            .OrderByDescending(t => t.Lado)
            .ToList();

        var fileiras = new List<FieldRow>();
        var i = 0;

        while (i < porLinha.Count)
        {
            // A linha: tudo a menos da tolerância do primeiro dela.
            var linha = new List<(FieldFootprint Pegada, double Ao, double Lado)> { porLinha[i] };
            var j = i + 1;
            while (j < porLinha.Count && porLinha[i].Lado - porLinha[j].Lado <= RowLineTolerance) linha.Add(porLinha[j++]);
            i = j;

            linha.Sort((a, b) => a.Ao.CompareTo(b.Ao));

            var atual = new List<FieldFootprint> { linha[0].Pegada };
            for (var k = 1; k < linha.Count; k++)
            {
                var folga = linha[k].Ao - linha[k - 1].Ao - (linha[k].Pegada.Length + linha[k - 1].Pegada.Length) / 2;
                if (folga > quebra)
                {
                    fileiras.Add(new FieldRow(atual));
                    atual = [];
                }

                atual.Add(linha[k].Pegada);
            }

            fileiras.Add(new FieldRow(atual));
        }

        return fileiras;
    }

    /// <summary>A diferença entre dois rumos de eixo (sem sentido), em [0, π/2].</summary>
    private static double AxisDifference(double a, double b)
    {
        var d = Math.Abs(a - b) % Math.PI;
        return Math.Min(d, Math.PI - d);
    }

    /// <summary>Os cantos em planta.</summary>
    private static IReadOnlyList<Point3> Corners(FieldFootprint f) => f.PlanCorners();

    private static double DistanceToContour(IReadOnlyList<Point3> area, Point3 p)
    {
        var menor = double.PositiveInfinity;

        for (var i = 0; i < area.Count; i++)
        {
            var a = area[i];
            var b = area[(i + 1) % area.Count];
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var l2 = dx * dx + dy * dy;
            var t = l2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
            var (qx, qy) = (a.X + t * dx - p.X, a.Y + t * dy - p.Y);
            menor = Math.Min(menor, Math.Sqrt(qx * qx + qy * qy));
        }

        return menor;
    }

    /// <summary>
    /// Se dois quadriláteros convexos em planta se cruzam (eixos separadores),
    /// com 1 cm de folga: encostar não conta.
    /// </summary>
    private static bool Intersect(IReadOnlyList<Point3> a, IReadOnlyList<Point3> b)
    {
        const double folga = 0.01;

        foreach (var poligono in new[] { a, b })
        {
            for (var i = 0; i < poligono.Count; i++)
            {
                var p = poligono[i];
                var q = poligono[(i + 1) % poligono.Count];
                var (nx, ny) = (-(q.Y - p.Y), q.X - p.X);
                var n = Math.Sqrt(nx * nx + ny * ny);
                if (n < 1e-12) continue;
                (nx, ny) = (nx / n, ny / n);

                var (minA, maxA) = Projetar(a, nx, ny);
                var (minB, maxB) = Projetar(b, nx, ny);
                if (maxA - folga <= minB || maxB - folga <= minA) return false;
            }
        }

        return true;

        static (double Min, double Max) Projetar(IReadOnlyList<Point3> poligono, double nx, double ny)
        {
            var valores = poligono.Select(p => p.X * nx + p.Y * ny).ToList();
            return (valores.Min(), valores.Max());
        }
    }

    private static Point3 Minus(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static double Norm(Point3 a) => Math.Sqrt(Dot(a, a));

    private static double Distance(Point3 a, Point3 b) => Norm(Minus(a, b));

    private static double PlanDistance(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
