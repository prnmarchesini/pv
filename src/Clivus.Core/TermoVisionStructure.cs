using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// O suporte das mesas fixas no 3D do TermoVision (33.10, 02/11/2026; o
/// Renan, olhando o 3D do PLAT A, só com as placas amarelas sobre o terreno:
/// "leve a estrutura tbm, fica mais profissional"). Vão em triângulos, na cor
/// do aço galvanizado:
/// <list type="bullet">
/// <item><description>os pilares, do terreno até o topo (o bloco do pilar);</description></item>
/// <item><description>as barras do biposte que o desenho tem: a viga, a mão
/// francesa, a terça e o tirante.</description></item>
/// </list>
/// Os módulos não vão: a face o TermoVision desenha pelos cantos da mesa. A
/// monoposte leva só os pilares (o plugin não desenha a viga dela), como no 3D
/// do Clivus. O pilar desce um pouco abaixo do terreno: a grade de 2 m que vai
/// junto passa pelo TIN nos nós, mas entre eles pode ficar uns centímetros
/// abaixo, e o pilar não pode aparecer flutuando.
/// </summary>
public static class TermoVisionStructure
{
    /// <summary>Até quantos triângulos uma mesa leva (contrato v2).</summary>
    public const int MaxTrianglesPerTable = 5_000;

    /// <summary>Até quantos triângulos o envio inteiro leva (contrato v2).</summary>
    public const int MaxTriangles = 400_000;

    /// <summary>O corpo descomprimido que cabe, com folga sob os 20 MB do contrato e do servidor do Clivus.</summary>
    public const int MaxBodyBytes = 18_000_000;

    /// <summary>Quanto o pilar desce abaixo do terreno, em metro.</summary>
    public const double BelowGround = 0.3;

    /// <summary>
    /// Um pilar da mesa: o topo (a origem do bloco), a cota do terreno no pé e
    /// a seção (a largura ao longo da mesa, a profundidade e o giro do bloco, em radiano).
    /// </summary>
    public sealed record Pillar(Point3 Top, double GroundZ, double Width, double Depth, double Rotation);

    /// <summary>
    /// Quanto do suporte vai, do mais completo ao mais simples: o envio que
    /// passa do limite desce um degrau (o contrato: "se passar, simplifique as terças").
    /// </summary>
    public enum Detail
    {
        /// <summary>Tudo: os pilares e todas as barras.</summary>
        Full,

        /// <summary>Sem os tirantes.</summary>
        NoRods,

        /// <summary>Sem os tirantes e as terças: os pilares, as vigas e as mãos francesas.</summary>
        NoPurlins,

        /// <summary>Só os pilares.</summary>
        PillarsOnly,
    }

    /// <summary>
    /// O suporte de uma mesa em triângulos, no <paramref name="detail"/> pedido
    /// ou, se a mesa passar dos <see cref="MaxTrianglesPerTable"/>, no primeiro
    /// mais simples que cabe (só os pilares cortados no limite, em último caso).
    /// </summary>
    public static IReadOnlyList<Scene3DFace> Table(IEnumerable<Pillar> pillars, IEnumerable<StructureIdentity> bars, Detail detail = Detail.Full)
    {
        ArgumentNullException.ThrowIfNull(pillars);
        ArgumentNullException.ThrowIfNull(bars);

        var pilares = pillars.ToList();
        var barras = bars.ToList();
        for (var d = detail; ; d++)
        {
            var faces = Faces(pilares, barras, d);
            if (faces.Count <= MaxTrianglesPerTable) return faces;
            if (d == Detail.PillarsOnly) return faces.Take(MaxTrianglesPerTable).ToList();
        }
    }

    /// <summary>
    /// A cota mais baixa dos nós da célula da grade em (x, y): qualquer
    /// superfície que passe pelos nós (a bilinear, os dois triângulos da
    /// célula, como o TermoVision desenhar) fica acima dela. O pé do pilar vai
    /// abaixo dela e do TIN: no talude, a grade de 2 m passa até meio metro
    /// abaixo do TIN, e o pilar com o pé no TIN aparecia flutuando (13 no PLAT
    /// A). Null fora da grade ou sem nó com cota na célula.
    /// </summary>
    public static double? LowestNodeAround(Scene3DTerrain grid, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!(grid.Step > 0) || !double.IsFinite(x) || !double.IsFinite(y)) return null;

        var i = (int)Math.Floor((x - grid.X0) / grid.Step);
        var j = (int)Math.Floor((y - grid.Y0) / grid.Step);
        double? menor = null;
        for (var l = j; l <= j + 1; l++)
        {
            for (var c = i; c <= i + 1; c++)
            {
                if (c < 0 || l < 0 || c >= grid.Columns || l >= grid.Rows) continue;
                if (grid.Z[l * grid.Columns + c] is { } z && double.IsFinite(z) && (menor is null || z < menor)) menor = z;
            }
        }

        return menor;
    }

    /// <summary>
    /// O detalhe do envio: o primeiro, do <see cref="Detail.Full"/> para baixo,
    /// em que o corpo cabe (os triângulos e os bytes que <paramref name="measure"/>
    /// conta montando o corpo com ele); null se nem só os pilares cabem (o
    /// envio vai sem a estrutura).
    /// </summary>
    public static Detail? Fit(Func<Detail, (int Triangles, long Bytes)> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);

        foreach (var d in Enum.GetValues<Detail>())
        {
            var (triangulos, bytes) = measure(d);
            if (triangulos <= MaxTriangles && bytes <= MaxBodyBytes) return d;
        }

        return null;
    }

    private static List<Scene3DFace> Faces(List<Pillar> pilares, List<StructureIdentity> barras, Detail detalhe)
    {
        var faces = new List<Scene3DFace>();
        var aco = CarportMesh.CorDoAco;

        foreach (var p in pilares)
        {
            if (!Finito(p.Top) || !double.IsFinite(p.GroundZ) || !(p.Width > 0) || !(p.Depth > 0)) continue;

            var pe = new Point3(p.Top.X, p.Top.Y, p.GroundZ - BelowGround);
            if (!(p.Top.Z > pe.Z)) continue;

            // A profundidade fica atravessada à mesa; a largura, ao longo dela (a seção do bloco).
            faces.AddRange(BarMesh.Box(pe, p.Top, p.Width, p.Depth, new Point3(-Math.Sin(p.Rotation), Math.Cos(p.Rotation), 0), aco, caps: false));
        }

        if (detalhe == Detail.PillarsOnly) return faces;

        foreach (var b in barras)
        {
            if (!Vai(b.Part, detalhe) || !Finito(b.Start) || !Finito(b.End) || !(b.Width > 0) || !(b.Depth > 0)) continue;
            if (Math.Sqrt(Math.Pow(b.End.X - b.Start.X, 2) + Math.Pow(b.End.Y - b.Start.Y, 2) + Math.Pow(b.End.Z - b.Start.Z, 2)) < 1e-6) continue;
            faces.AddRange(BarMesh.Box(b.Start, b.End, b.Width, b.Depth, b.DepthDirection, aco, caps: false));
        }

        return faces;
    }

    private static bool Vai(StructurePart parte, Detail detalhe) => parte switch
    {
        StructurePart.Rod => detalhe == Detail.Full,
        StructurePart.Purlin => detalhe <= Detail.NoRods,
        _ => detalhe <= Detail.NoPurlins,
    };

    private static bool Finito(Point3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
}
