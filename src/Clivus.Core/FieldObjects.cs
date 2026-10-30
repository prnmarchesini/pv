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
/// A mesa que um objeto marca: o retângulo (os módulos) em 3D, o eixo da
/// fileira em planta e para que lado fica a ponta baixa.
/// </summary>
/// <param name="Center">O centro do retângulo; o Z é o do objeto, que o Clivus não usa (a mesa senta no terreno).</param>
/// <param name="Axis">O rumo do eixo da fileira em planta, em radianos, de +X no sentido anti-horário, em [0, π).</param>
/// <param name="Length">O lado maior, em 3D (ao longo da fileira).</param>
/// <param name="Width">O lado menor, em 3D (na inclinação).</param>
/// <param name="PlanLength">O lado maior visto de cima.</param>
/// <param name="Tilt">A inclinação em torno do eixo, em radianos; null quando o objeto é plano.</param>
/// <param name="LowSide">+1: a ponta baixa fica à esquerda do eixo (olhando no sentido de <paramref name="Axis"/>); −1: à direita; null: objeto plano.</param>
/// <param name="RowSlope">A rampa do eixo (sobe/anda em planta, no sentido de <paramref name="Axis"/>); null quando plano.</param>
public sealed record FieldFootprint(
    string Id,
    string Layer,
    Point3 Center,
    double Axis,
    double Length,
    double Width,
    double PlanLength,
    double? Tilt,
    int? LowSide,
    double? RowSlope)
{
    /// <summary>O versor do eixo em planta.</summary>
    public (double X, double Y) AxisVector => (Math.Cos(Axis), Math.Sin(Axis));

    /// <summary>Os quatro cantos em planta (Z zero), em ordem, a partir do canto de trás-esquerda.</summary>
    public IReadOnlyList<Point3> PlanCorners(double planWidth)
    {
        var (ux, uy) = AxisVector;
        var (hx, hy) = (-uy, ux);
        var (a, b) = (PlanLength / 2, planWidth / 2);

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

    /// <summary>Inclinação diferente da maioria.</summary>
    Tilt,

    /// <summary>A ponta baixa do outro lado.</summary>
    Facing,

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

    /// <summary>A mesa do Clivus não tem a inclinação dos objetos.</summary>
    ProfileTilt,

    /// <summary>Nenhuma mesa em uso tem o tamanho e a inclinação do objeto: ele não vira mesa (40.2).</summary>
    NoProfile,

    /// <summary>A mesa desenhada ficou longe do objeto (40.2): o motor a sentou no terreno com outra rampa.</summary>
    Deviation,

    /// <summary>A ponta baixa dos objetos não bate com o azimute do projeto (40.2): nada é gerado.</summary>
    ProjectFacing,
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
    double ReferenceAxis,
    double? ReferenceTilt,
    int? ReferenceLowSide)
{
    /// <summary>Quantas mesas saem (os objetos que entraram nas fileiras).</summary>
    public int TableCount => Rows.Sum(r => r.Tables.Count);

    /// <summary>
    /// O rumo para onde a ponta baixa olha, em planta (radianos, de +X no
    /// sentido anti-horário), ou null quando os objetos são planos.
    /// </summary>
    public double? LowSideDirection => ReferenceLowSide is { } lado
        ? Math.Atan2(Math.Sin(ReferenceAxis + lado * Math.PI / 2), Math.Cos(ReferenceAxis + lado * Math.PI / 2))
        : null;
}

/// <summary>
/// Reconhece os objetos do cliente como mesas (40.1, Renan 30/10/2026: "quero
/// ter a opção de escolher os objetos em planta e falar 'esses objetos são
/// mesas' [...] Se tiver divergência ele deve apontar, em teoria todos objetos
/// devem ser iguais").
///
/// O objeto de referência é o do desenho do projeto Aurora: a área dos módulos
/// de uma 2P14 como retângulo 3D (18,502 × 4,788 m, 10° em torno do eixo, o
/// eixo acompanhando a rampa da fileira), cuja projeção em planta é um
/// paralelogramo, e não um retângulo. Por isso o tamanho e o ângulo reto são
/// medidos em 3D; o eixo e o lado da ponta baixa, em planta.
/// </summary>
public static class FieldObjects
{
    /// <summary>Diferença de tamanho que conta como outro objeto: 2 cm.</summary>
    public const double SizeTolerance = 0.02;

    /// <summary>Diferença de rumo e de inclinação que conta: meio grau.</summary>
    public const double AngleTolerance = 0.5 * Math.PI / 180;

    /// <summary>Quanto o retângulo pode fugir do ângulo reto (em 3D): um grau.</summary>
    public const double RightAngleTolerance = 1.0 * Math.PI / 180;

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

    /// <summary>Abaixo disto de diferença de Z o objeto é plano: 1 mm.</summary>
    private const double Flat = 0.001;

    /// <summary>
    /// A pegada do objeto, ou null com o motivo quando ele não é um
    /// retângulo de 4 vértices (o vértice repetido no fim não conta).
    /// </summary>
    public static FieldFootprint? Footprint(FieldObject objeto, out string? whyNot)
    {
        ArgumentNullException.ThrowIfNull(objeto);

        var p = objeto.Vertices.ToList();
        if (p.Count == 5 && Distance(p[0], p[4]) < 1e-6) p.RemoveAt(4);

        if (p.Count != 4)
        {
            whyNot = Tr.F("tem {0} vértices; a mesa é um retângulo de 4", p.Count);
            return null;
        }

        if (p.Any(q => !double.IsFinite(q.X) || !double.IsFinite(q.Y) || !double.IsFinite(q.Z)))
        {
            whyNot = Tr.T("tem coordenada inválida");
            return null;
        }

        // O lado maior é o eixo: p0→p1 ou p1→p2.
        if (Distance(p[0], p[1]) < Distance(p[1], p[2])) p = [p[1], p[2], p[3], p[0]];

        var u = Minus(p[1], p[0]);
        var v = Minus(p[3], p[0]);
        var comprimento = (Distance(p[0], p[1]) + Distance(p[3], p[2])) / 2;
        var largura = (Distance(p[0], p[3]) + Distance(p[1], p[2])) / 2;

        if (largura < 0.05)
        {
            whyNot = Tr.T("é uma linha, não um retângulo");
            return null;
        }

        // Paralelogramo (p2 = p1 + v) e ângulo reto em 3D.
        var p2Esperado = new Point3(p[1].X + v.X, p[1].Y + v.Y, p[1].Z + v.Z);
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

        var planoU = Math.Sqrt(u.X * u.X + u.Y * u.Y);
        if (planoU < 0.05)
        {
            whyNot = Tr.T("está em pé: o lado maior não aparece em planta");
            return null;
        }

        // O eixo em [0, π): o sentido de u acompanha o rumo escolhido.
        var rumo = Math.Atan2(u.Y, u.X);
        if (rumo < 0 || rumo >= Math.PI - 1e-12)
        {
            rumo = Math.Atan2(-u.Y, -u.X);
            u = new Point3(-u.X, -u.Y, -u.Z);
        }

        if (rumo < 0) rumo += Math.PI;
        if (rumo >= Math.PI) rumo -= Math.PI;

        var centro = new Point3(p.Average(q => q.X), p.Average(q => q.Y), p.Average(q => q.Z));
        var planoComprimento = (PlanDistance(p[0], p[1]) + PlanDistance(p[3], p[2])) / 2;

        double? inclinacao = null;
        int? ladoBaixo = null;
        double? rampa = null;

        if (p.Max(q => q.Z) - p.Min(q => q.Z) >= Flat)
        {
            // h: a horizontal perpendicular ao eixo, à esquerda; w completa o
            // terno com o eixo. A inclinação é o giro de v em torno do eixo.
            var un = Scale(u, 1 / Norm(u));
            var h = new Point3(-Math.Sin(rumo), Math.Cos(rumo), 0);
            var w = Cross(un, h);
            inclinacao = Math.Atan2(Math.Abs(Dot(v, w)), Math.Abs(Dot(v, h)));
            rampa = u.Z / planoU;

            // A ponta baixa é o lado maior mais baixo; de que lado do eixo ele fica.
            var zDeTras = (p[0].Z + p[1].Z) / 2;
            var zDaFrente = (p[3].Z + p[2].Z) / 2;

            if (Math.Abs(zDeTras - zDaFrente) >= Flat)
            {
                var (baixoA, baixoB) = zDeTras < zDaFrente ? (p[0], p[1]) : (p[3], p[2]);
                var meio = new Point3((baixoA.X + baixoB.X) / 2 - centro.X, (baixoA.Y + baixoB.Y) / 2 - centro.Y, 0);
                ladoBaixo = Dot(meio, h) >= 0 ? 1 : -1;
            }
            else
            {
                inclinacao = 0;
            }
        }

        whyNot = null;
        return new FieldFootprint(objeto.Id, objeto.Layer, centro, rumo, comprimento, largura, planoComprimento, inclinacao, ladoBaixo, rampa);
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
            return new FieldSurvey(pegadas, [], problemas, 0, 0, 0, null, null);

        // A referência: o que a maioria tem.
        var (refL, refW) = pegadas
            .GroupBy(f => (Math.Round(f.Length, 2), Math.Round(f.Width, 2)))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Item1).First().Key;
        var refRumo = pegadas
            .GroupBy(f => Math.Round(f.Axis * 180 / Math.PI, 1) % 180)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key * Math.PI / 180;
        // O eixo sai em [0, π): a 179,98° e a 0,02° são a mesma fileira em
        // sentidos opostos. Cada pegada passa para o sentido da referência
        // (o lado baixo e a rampa trocam de sinal), senão o lado baixo da
        // maioria, as fileiras e as células saem viradas (revisão do 40.2).
        pegadas = [.. pegadas.Select(f => Math.Cos(f.Axis - refRumo) >= 0 ? f : f with
        {
            Axis = Math.Atan2(Math.Sin(f.Axis + Math.PI), Math.Cos(f.Axis + Math.PI)),
            LowSide = -f.LowSide,
            RowSlope = -f.RowSlope,
        })];

        var inclinadas = pegadas.Where(f => f.Tilt is not null).Select(f => f.Tilt!.Value).OrderBy(t => t).ToList();
        double? refInclinacao = inclinadas.Count == 0 ? null : inclinadas[inclinadas.Count / 2];
        var lados = pegadas.Where(f => f.LowSide is not null).GroupBy(f => f.LowSide!.Value).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
        int? refLado = lados.Count == 0 ? null : lados[0].Key;

        var pulados = new HashSet<string>();

        foreach (var f in pegadas)
        {
            if (Math.Abs(f.Length - refL) > SizeTolerance || Math.Abs(f.Width - refW) > SizeTolerance)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Size, Tr.F("{0} ({1}) tem {2:0.###} × {3:0.###} m; a maioria tem {4:0.###} × {5:0.###} m.", f.Id, f.Layer, f.Length, f.Width, refL, refW)));

            if (AxisDifference(f.Axis, refRumo) > AngleTolerance)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Axis, Tr.F("{0} ({1}) está a {2:0.#}° da direção da maioria das fileiras.", f.Id, f.Layer, AxisDifference(f.Axis, refRumo) * 180 / Math.PI)));

            if (f.Tilt is { } t && refInclinacao is { } rt && Math.Abs(t - rt) > AngleTolerance)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Tilt, Tr.F("{0} ({1}) tem {2:0.#}° de inclinação; a maioria tem {3:0.#}°.", f.Id, f.Layer, t * 180 / Math.PI, rt * 180 / Math.PI)));

            if (f.LowSide is { } lado && refLado is { } rl && SameSide(f, lado, refRumo) != rl)
                problemas.Add(new FieldIssue(f.Id, FieldIssueKind.Facing, Tr.F("{0} ({1}) tem a ponta baixa do outro lado.", f.Id, f.Layer)));

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
        bool DaMaioria(FieldFootprint f) => Math.Abs(f.Length - refL) <= SizeTolerance && Math.Abs(f.Width - refW) <= SizeTolerance;
        var ordem = pegadas.Where(f => !pulados.Contains(f.Id)).OrderBy(f => DaMaioria(f) ? 0 : 1).ToList();
        for (var i = 0; i < ordem.Count; i++)
        {
            if (pulados.Contains(ordem[i].Id)) continue;

            for (var j = i + 1; j < ordem.Count; j++)
            {
                var (a, b) = (ordem[i], ordem[j]);
                if (pulados.Contains(b.Id)) continue;

                var alcance = Math.Max(a.PlanLength, b.PlanLength) + Math.Max(a.Width, b.Width);
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

        if (profile is not null) problemas.AddRange(ProfileIssues(profile, refL, refW, refInclinacao));

        var aceitas = pegadas.Where(f => !pulados.Contains(f.Id)).ToList();
        var fileiras = RowsByAxis(aceitas, rowBreak ?? DefaultRowBreak);

        return new FieldSurvey(pegadas, fileiras, problemas, refL, refW, refRumo, refInclinacao, refLado);
    }

    /// <summary>
    /// A mesa do Clivus e os objetos: o objeto pode ser a área dos módulos ou
    /// a mesa inteira (com as sobras); a inclinação tem que ser a mesma.
    /// </summary>
    public static IReadOnlyList<FieldIssue> ProfileIssues(TableProfile profile, double length, double width, double? tilt)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var problemas = new List<FieldIssue>();
        var layout = profile.Layout;
        var modulos = layout.Length - layout.LeftMargin - layout.RightMargin;

        var bateComprimento = Math.Abs(length - modulos) <= SizeTolerance || Math.Abs(length - layout.Length) <= SizeTolerance;
        var bateLargura = Math.Abs(width - layout.Depth) <= SizeTolerance;

        if (!bateComprimento || !bateLargura)
        {
            problemas.Add(new FieldIssue("*", FieldIssueKind.ProfileSize, Tr.F(
                "a mesa \"{0}\" tem {1:0.###} m de módulos ({2:0.###} m com as sobras) × {3:0.###} m; os objetos têm {4:0.###} × {5:0.###} m.",
                profile.Name, modulos, layout.Length, layout.Depth, length, width)));
        }

        if (tilt is { } t && Math.Abs(t - profile.TiltRadians) > AngleTolerance)
        {
            problemas.Add(new FieldIssue("*", FieldIssueKind.ProfileTilt, Tr.F(
                "a mesa \"{0}\" tem {1:0.#}° de inclinação; os objetos têm {2:0.#}°.",
                profile.Name, profile.TiltDegrees, t * 180 / Math.PI)));
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
                var folga = linha[k].Ao - linha[k - 1].Ao - (linha[k].Pegada.PlanLength + linha[k - 1].Pegada.PlanLength) / 2;
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

    /// <summary>O lado da ponta baixa medido contra o eixo de referência (o eixo da pegada pode estar a 180°).</summary>
    private static int SameSide(FieldFootprint f, int lado, double refRumo) =>
        Math.Cos(f.Axis - refRumo) >= 0 ? lado : -lado;

    /// <summary>A diferença entre dois rumos de eixo (sem sentido), em [0, π/2].</summary>
    private static double AxisDifference(double a, double b)
    {
        var d = Math.Abs(a - b) % Math.PI;
        return Math.Min(d, Math.PI - d);
    }

    /// <summary>Os cantos em planta com a largura vista de cima.</summary>
    private static IReadOnlyList<Point3> Corners(FieldFootprint f) =>
        f.PlanCorners(f.Tilt is { } t ? f.Width * Math.Cos(t) : f.Width);

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

    private static Point3 Scale(Point3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static Point3 Cross(Point3 a, Point3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double Norm(Point3 a) => Math.Sqrt(Dot(a, a));

    private static double Distance(Point3 a, Point3 b) => Norm(Minus(a, b));

    private static double PlanDistance(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
