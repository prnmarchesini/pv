using Clipper2Lib;

namespace Clivus.Core;

/// <summary>
/// A área do trafo (pedido do Renan em 05/10/2026: "na aba do trafo, ter um
/// botão, para criar um hatch em torno da área que compreende as strings dos
/// inversores que compõem aquele trafo"): o contorno, em planta, que envolve
/// as pegadas dos módulos das strings do trafo.
/// <para>
/// A conta (Clipper2, em milímetros, como a <see cref="ShadowUnion"/>):
/// une as pegadas; fecha os vãos entre módulos, mesas e fileiras crescendo
/// <see cref="DefaultBridge"/> e encolhendo o mesmo tanto (o "fechamento":
/// o corredor entre fileiras some e o contorno externo volta ao lugar); dá
/// uma folga de <see cref="DefaultMargin"/> em volta; tira as pegadas dos
/// módulos dos outros trafos (com a mesma folga), para que o fechamento não
/// avance sobre o vizinho; joga fora os buracos menores que
/// <see cref="MinHoleArea"/> e as ilhas que não têm módulo do trafo (sobras
/// do recorte). Área separada dá várias ilhas.
/// </para>
/// </summary>
public static class TransformerArea
{
    private const double Escala = 1000;

    /// <summary>Metade do maior vão fechado, em metro: corredor de até 8 m entre fileiras vira área.</summary>
    public const double DefaultBridge = 4.0;

    /// <summary>A folga em volta dos módulos, em metro.</summary>
    public const double DefaultMargin = 0.5;

    /// <summary>Buraco menor que isto (m²) é fechado: só sobra o buraco que é área de verdade.</summary>
    public const double MinHoleArea = 1.0;

    /// <summary>Tolerância da simplificação do contorno, em metro.</summary>
    private const double Simplificacao = 0.02;

    /// <summary>Tolerância dos arcos do fechamento, em metro.</summary>
    private const double ToleranciaDoArco = 0.05;

    /// <summary>
    /// Uma ilha da área: o anel de fora, os buracos e os índices das pegadas
    /// do trafo que caem nela (pelo centro). Anéis fechados implicitamente.
    /// </summary>
    public sealed record Island(
        IReadOnlyList<(double X, double Y)> Outer,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> Holes,
        IReadOnlyList<int> Members)
    {
        /// <summary>A área da ilha (m²): a de fora menos os buracos.</summary>
        public double Area => ShadowUnion.Area(Outer) - Holes.Sum(ShadowUnion.Area);
    }

    /// <summary>
    /// As ilhas da área das pegadas <paramref name="own"/> (as do trafo), sem
    /// invadir as <paramref name="others"/> (as dos módulos de outros trafos).
    /// Vazio se nenhuma pegada tem área. Ilhas da maior para a menor.
    /// </summary>
    public static IReadOnlyList<Island> Outline(
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> own,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>>? others = null,
        double bridge = DefaultBridge,
        double margin = DefaultMargin)
    {
        ArgumentNullException.ThrowIfNull(own);
        if (!(bridge >= 0) || !(margin >= 0)) throw new ArgumentOutOfRangeException(nameof(bridge), "Vão e folga não podem ser negativos.");

        var proprias = Caminhos(own);
        if (proprias.Count == 0) return [];

        var tolerancia = ToleranciaDoArco * Escala;
        var uniao = Clipper.Union(proprias, FillRule.NonZero);

        // O fechamento: cresce e encolhe o mesmo tanto. Os vãos menores que o
        // dobro somem; o contorno de fora volta ao lugar. Em quina viva
        // (miter), não arredondada: com arco, o lado do corredor entre duas
        // fileiras ficava com um dente para dentro e a fresta de 2 cm entre
        // módulos deixava farpas de 5 cm na borda.
        var fechada = bridge > 0
            ? Clipper.InflatePaths(Clipper.InflatePaths(uniao, bridge * Escala, JoinType.Miter, EndType.Polygon, 4, tolerancia), -bridge * Escala, JoinType.Miter, EndType.Polygon, 4, tolerancia)
            : uniao;

        var comFolga = margin > 0 ? Clipper.InflatePaths(fechada, margin * Escala, JoinType.Miter, EndType.Polygon, 2, tolerancia) : fechada;

        var recorte = new Clipper64();
        recorte.AddSubject(comFolga);

        var alheias = Caminhos(others ?? []);
        if (alheias.Count > 0)
        {
            var vizinhas = Clipper.Union(alheias, FillRule.NonZero);
            recorte.AddClip(margin > 0 ? Clipper.InflatePaths(vizinhas, margin * Escala, JoinType.Miter, EndType.Polygon, 2, tolerancia) : vizinhas);
        }

        var arvore = new PolyTree64();
        recorte.Execute(alheias.Count > 0 ? ClipType.Difference : ClipType.Union, FillRule.NonZero, arvore);

        // O centro de cada pegada do trafo diz a que ilha ela pertence.
        var centros = own.Select(Centro).ToList();
        var ilhas = new List<Island>();
        var usadas = new HashSet<int>();

        void Visitar(PolyPath64 no)
        {
            for (var i = 0; i < no.Count; i++)
            {
                var externo = no.Child(i);
                if (externo.IsHole || externo.Polygon is not { } poligono) continue;

                var fora = Simplificar(poligono);
                var buracos = new List<Path64>();

                for (var j = 0; j < externo.Count; j++)
                {
                    var buraco = externo.Child(j);
                    if (buraco.Polygon is { } furo && Math.Abs(Clipper.Area(furo)) >= MinHoleArea * Escala * Escala) buracos.Add(Simplificar(furo));

                    // Ilha dentro do buraco: é ilha também.
                    Visitar(buraco);
                }

                if (fora.Count < 3) continue;

                var membros = new List<int>();
                for (var k = 0; k < centros.Count; k++)
                {
                    if (centros[k] is not { } c || usadas.Contains(k)) continue;
                    if (Clipper.PointInPolygon(c, fora) == PointInPolygonResult.IsOutside) continue;
                    if (buracos.Any(b => Clipper.PointInPolygon(c, b) == PointInPolygonResult.IsInside)) continue;
                    membros.Add(k);
                }

                // Sobra do recorte, sem módulo do trafo: não é área dele.
                if (membros.Count == 0) continue;

                foreach (var k in membros) usadas.Add(k);
                ilhas.Add(new Island(Metros(fora), buracos.Where(b => b.Count >= 3).Select(Metros).ToList(), membros));
            }
        }

        Visitar(arvore);
        return ilhas.OrderByDescending(i => i.Area).ToList();
    }

    private static Paths64 Caminhos(IEnumerable<IReadOnlyList<(double X, double Y)>> poligonos)
    {
        var caminhos = new Paths64();
        foreach (var p in poligonos)
        {
            if (p is null || p.Count < 3 || p.Any(v => !double.IsFinite(v.X) || !double.IsFinite(v.Y))) continue;
            var caminho = new Path64(p.Count);
            foreach (var (x, y) in p) caminho.Add(new Point64(Math.Round(x * Escala), Math.Round(y * Escala)));
            if (Math.Abs(Clipper.Area(caminho)) > 0) caminhos.Add(caminho);
        }

        return caminhos;
    }

    private static Point64? Centro(IReadOnlyList<(double X, double Y)> p)
    {
        if (p is null || p.Count < 3 || p.Any(v => !double.IsFinite(v.X) || !double.IsFinite(v.Y))) return null;
        return new Point64(Math.Round(p.Average(v => v.X) * Escala), Math.Round(p.Average(v => v.Y) * Escala));
    }

    private static Path64 Simplificar(Path64 anel)
    {
        var simples = Clipper.SimplifyPaths([anel], Simplificacao * Escala, true);
        return simples.Count > 0 && simples[0].Count >= 3 ? simples[0] : anel;
    }

    private static IReadOnlyList<(double X, double Y)> Metros(Path64 anel) =>
        anel.Select(p => (p.X / Escala, p.Y / Escala)).ToList();
}

/// <summary>
/// A marca do hatch da área do trafo no desenho (XData, tipo
/// <see cref="Tipo"/>): o GUID do trafo. É por ela que gerar de novo acha e
/// substitui o hatch antigo daquele trafo, e apagar o trafo o leva junto. O
/// hatch é representação: nenhum vínculo é lido dele.
/// </summary>
public sealed record TransformerAreaMark(Guid Transformer)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "AreaDoTrafo";

    public const int FieldCount = 1;

    public bool IsValid => Transformer != Guid.Empty;
}
