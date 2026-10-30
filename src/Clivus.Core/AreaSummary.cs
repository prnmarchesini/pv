using Clipper2Lib;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma área de implantação, como o resumo por área a recebe (41.1).</summary>
/// <param name="Id">O GUID da área (o do XData).</param>
/// <param name="Name">O nome que o Clivus mostra.</param>
/// <param name="Outline">O contorno em planta; o Z não entra na conta.</param>
/// <param name="FollowsObjects">Se a área segue os objetos do cliente (40.2).</param>
public sealed record AreaInput(Guid Id, string Name, IReadOnlyList<Point3> Outline, bool FollowsObjects = false);

/// <summary>Uma mesa do desenho, como o resumo por área a recebe.</summary>
/// <param name="Table">A mesa contada como o Recontar conta (<see cref="CountedTable"/>): módulos, pilares, potência, estado.</param>
/// <param name="Outline">
/// Os cantos do contorno da mesa, em 3D (a planta decide a área; o Z, a
/// cota da hachura). Na mesa sem contorno (peças órfãs), o ponto de uma peça.
/// </param>
/// <param name="ModuleFaceArea">
/// A área de um módulo da mesa, em m²: a face de cima dele, em verdadeira
/// grandeza (todos os módulos de uma mesa são iguais). Null quando a mesa
/// não tem face no desenho.
/// </param>
public sealed record AreaTableInput(CountedTable Table, IReadOnlyList<Point3> Outline, double? ModuleFaceArea = null);

/// <summary>Um tipo de mesa numa área: o nome e a contagem dele ali.</summary>
public sealed record AreaTypeCount(string Name, int Tables, int Modules, double PowerKwp);

/// <summary>O que é a linha do resumo.</summary>
public enum AreaRowKind
{
    /// <summary>Uma área de implantação.</summary>
    Area,

    /// <summary>As mesas cujo centro não está em área nenhuma.</summary>
    Outside,

    /// <summary>O desenho inteiro (bate com o Recontar).</summary>
    Total,
}

/// <summary>O resumo de uma área, das mesas de fora das áreas ou do desenho inteiro.</summary>
/// <param name="Kind">O que é a linha.</param>
/// <param name="Id">O GUID da área; null fora das áreas e no total.</param>
/// <param name="Name">O nome da área, "Fora das áreas" ou "Total".</param>
/// <param name="PlanArea">A área em planta, em m²; null fora das áreas (e no total sem área nenhuma).</param>
/// <param name="Census">A contagem das mesas, pela mesma conta do Recontar (<see cref="LayoutCensus.Count"/>).</param>
/// <param name="Types">A contagem por tipo de mesa, do que tem mais mesas para o que tem menos.</param>
/// <param name="ModuleArea">A área dos módulos (verdadeira grandeza), em m².</param>
/// <param name="TablesWithoutFace">Mesas com módulo e sem face no desenho: ficam fora da área dos módulos.</param>
/// <param name="OnBorder">Mesas contadas aqui com algum canto fora da área (na divisa).</param>
/// <param name="FollowsObjects">Se a área segue os objetos do cliente (40.2).</param>
public sealed record AreaSummaryRow(
    AreaRowKind Kind,
    Guid? Id,
    string Name,
    double? PlanArea,
    LayoutCensus Census,
    IReadOnlyList<AreaTypeCount> Types,
    double ModuleArea,
    int TablesWithoutFace,
    int OnBorder,
    bool FollowsObjects)
{
    /// <summary>A área em planta, em hectares.</summary>
    public double? Hectares => PlanArea / 10_000.0;

    /// <summary>A soma dos comprimentos gravados dos pilares, em metro.</summary>
    public double PillarMeters => Census.PillarLengths.Sum();

    /// <summary>A ocupação: a área dos módulos sobre a área em planta (0,38 = 38 %); null sem área.</summary>
    public double? Occupation => PlanArea is > 0 ? ModuleArea / PlanArea.Value : null;

    /// <summary>"78 × GC800, 12 × GC1000": os tipos que têm mesa, na ordem da lista.</summary>
    public string TypesText =>
        string.Join(", ", Types.Where(t => t.Tables > 0).Select(t => string.Create(Tr.Culture, $"{t.Tables} × {t.Name}")));
}

/// <summary>
/// O resumo por área (41.1, Renan, 30/10/2026: "nesse caso real vou ter 7
/// áreas [...] quero ter a opção do sistema listar o resumo de cada área [...]
/// do resumo sair por área").
/// <para>
/// A conta é a do Recontar: cada mesa vira <see cref="CountedTable"/> pelo
/// XData e cada grupo de mesas passa por <see cref="LayoutCensus.Count"/>.
/// Aqui só se decide de que área é cada mesa e se soma por área e por tipo.
/// </para>
/// <para>
/// A mesa que atravessa duas áreas (decisão de 30/10/2026): conta uma vez só,
/// na área onde está o CENTRO dela, a mesma regra do inversor
/// (<see cref="InverterSites.AreaOf"/>): dentro de duas (uma dentro da outra),
/// na menor. Com algum canto fora da área dela, ela conta como "na divisa"
/// (<see cref="AreaSummaryRow.OnBorder"/>). Assim a soma das áreas, mais a
/// linha "Fora das áreas" (centro em área nenhuma), é sempre o total do
/// desenho, que bate com o Recontar. Atenção ao que isso NÃO muda: o Regerar
/// área e o Apagar tudo levam a mesa que tem qualquer canto dentro da área,
/// então a mesa na divisa sai com o Regerar de qualquer das duas.
/// </para>
/// </summary>
/// <param name="Areas">Uma linha por área, na ordem recebida.</param>
/// <param name="Outside">As mesas com o centro fora de toda área, ou null se não há nenhuma.</param>
/// <param name="Total">O desenho inteiro.</param>
/// <param name="Assignment">Para cada mesa recebida, a posição da área dela na lista, ou null (fora das áreas).</param>
public sealed record AreaSummary(IReadOnlyList<AreaSummaryRow> Areas, AreaSummaryRow? Outside, AreaSummaryRow Total, IReadOnlyList<int?> Assignment)
{
    /// <summary>As áreas, a linha de fora das áreas (se houver) e o total, nessa ordem.</summary>
    public IEnumerable<AreaSummaryRow> Rows => Outside is null ? [.. Areas, Total] : [.. Areas, Outside, Total];

    /// <summary>Monta o resumo.</summary>
    /// <param name="areas">As áreas, na ordem em que aparecem.</param>
    /// <param name="tables">Todas as mesas do desenho (as de fora das áreas também: o total é o desenho).</param>
    /// <param name="fallbackPowerWatts">A potência do perfil atual, para mesa sem potência gravada (como no Recontar).</param>
    /// <param name="simulatedPowerWatts">A potência do módulo trocada pela área (item 14 de 10/10/2026), ou null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Potência de reserva que não é um número positivo.</exception>
    public static AreaSummary Build(IReadOnlyList<AreaInput> areas, IReadOnlyList<AreaTableInput> tables, double fallbackPowerWatts, double? simulatedPowerWatts = null)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(tables);

        var dono = tables.Select(t => AreaOf(t.Outline, areas)).ToList();

        AreaSummaryRow Linha(AreaRowKind tipo, Guid? id, string nome, double? planta, IReadOnlyList<int> quais, bool objetos, IReadOnlyList<Point3>? contorno)
        {
            var mesas = quais.Select(i => tables[i]).ToList();
            var divisa = contorno is { Count: >= 3 }
                ? mesas.Count(m => m.Outline.Count > 1 && m.Outline.Any(p => p.IsFinite && !Polygons.Contains(contorno, p.X, p.Y)))
                : 0;

            return new AreaSummaryRow(
                tipo,
                id,
                nome,
                planta,
                LayoutCensus.Count(mesas.Select(m => m.Table).ToList(), fallbackPowerWatts, simulatedPowerWatts),
                Tipos(mesas, fallbackPowerWatts, simulatedPowerWatts),
                mesas.Sum(AreaDosModulos),
                mesas.Count(SemFace),
                divisa,
                objetos);
        }

        var linhas = areas
            .Select((a, k) => Linha(
                AreaRowKind.Area, a.Id, a.Name, a.Outline.Count >= 3 ? InverterSites.PlanArea(a.Outline) : 0,
                Enumerable.Range(0, tables.Count).Where(i => dono[i] == k).ToList(), a.FollowsObjects, a.Outline))
            .ToList();

        var fora = Enumerable.Range(0, tables.Count).Where(i => dono[i] is null).ToList();
        var foraDasAreas = fora.Count == 0 ? null : Linha(AreaRowKind.Outside, null, Tr.T("Fora das áreas"), null, fora, false, null);

        var planta = areas.Any(a => a.Outline.Count >= 3) ? UnionArea(areas.Select(a => a.Outline)) : (double?)null;
        var total = Linha(AreaRowKind.Total, null, Tr.T("Total"), planta, Enumerable.Range(0, tables.Count).ToList(), false, null)
            with { OnBorder = linhas.Sum(l => l.OnBorder) };

        return new AreaSummary(linhas, foraDasAreas, total, dono);
    }

    /// <summary>
    /// A posição (na lista) da área da mesa, pelo centro dela: a regra do
    /// inversor (<see cref="InverterSites.AreaOf"/>), dentro de duas a menor;
    /// null se o centro não está em área nenhuma. A chave passada à regra é a
    /// posição, e não o GUID: duas polilinhas podem ter o mesmo GUID (cópia
    /// da área, que o Validar aponta), e cada uma conta as suas mesas.
    /// </summary>
    public static int? AreaOf(IReadOnlyList<Point3> tableOutline, IReadOnlyList<AreaInput> areas)
    {
        ArgumentNullException.ThrowIfNull(tableOutline);
        ArgumentNullException.ThrowIfNull(areas);

        if (Center(tableOutline) is not { } centro) return null;

        var chave = InverterSites.AreaOf(centro.X, centro.Y, areas.Select((a, i) => (Chave(i), a.Outline)));
        return chave is { } k ? Posicao(k) : null;
    }

    /// <summary>
    /// O centro, em planta, do contorno da mesa: o meio do retângulo que o
    /// envolve. O contorno da mesa é um paralelogramo em planta, e nele o meio
    /// do envelope é o centro de verdade, com ou sem o ponto de fechamento
    /// repetido. Null sem ponto finito.
    /// </summary>
    public static Point3? Center(IReadOnlyList<Point3> outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var pontos = outline.Where(p => p.IsFinite).ToList();
        if (pontos.Count == 0) return null;

        return new Point3((pontos.Min(p => p.X) + pontos.Max(p => p.X)) / 2, (pontos.Min(p => p.Y) + pontos.Max(p => p.Y)) / 2, 0);
    }

    /// <summary>A área de um quadrilátero plano em 3D (a face do módulo), em verdadeira grandeza: metade do produto vetorial das diagonais.</summary>
    public static double QuadArea(Point3 a, Point3 b, Point3 c, Point3 d)
    {
        var (ux, uy, uz) = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var (vx, vy, vz) = (d.X - b.X, d.Y - b.Y, d.Z - b.Z);
        var (cx, cy, cz) = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
        return Math.Sqrt(cx * cx + cy * cy + cz * cz) / 2;
    }

    /// <summary>
    /// A área em planta da união dos polígonos, em m²: área sobreposta conta
    /// uma vez (o total da usina não soma duas vezes o chão de duas áreas que
    /// se cruzam). União da Clipper2, em milímetros, como a das sombras.
    /// </summary>
    public static double UnionArea(IEnumerable<IReadOnlyList<Point3>> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);

        var caminhos = new Paths64();
        foreach (var p in polygons)
        {
            if (p.Count < 3 || p.Any(q => !double.IsFinite(q.X) || !double.IsFinite(q.Y))) continue;

            var caminho = new Path64(p.Count);
            foreach (var q in p) caminho.Add(new Point64(Math.Round(q.X * 1000), Math.Round(q.Y * 1000)));
            caminhos.Add(caminho);
        }

        if (caminhos.Count == 0) return 0;

        // Os anéis de fora e os buracos têm sentidos opostos: a soma com sinal é a área de verdade.
        return Math.Abs(Clipper.Union(caminhos, FillRule.NonZero).Sum(c => Clipper.Area(c))) / 1e6;
    }

    /// <summary>
    /// A contagem por tipo de mesa: o nome do perfil gravado na mesa (desde o
    /// 8.6); "(sem tipo)" na mesa de antes disso, "(peças órfãs)" nas peças
    /// sem contorno. Cada tipo passa pela conta do Recontar.
    /// </summary>
    private static List<AreaTypeCount> Tipos(IReadOnlyList<AreaTableInput> mesas, double reserva, double? simulada) =>
        mesas.GroupBy(m => NomeDoTipo(m.Table), StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var censo = LayoutCensus.Count(g.Select(m => m.Table).ToList(), reserva, simulada);
                return new AreaTypeCount(g.Key, censo.Tables, censo.Modules, censo.PowerKwp);
            })
            .OrderByDescending(t => t.Tables)
            .ThenByDescending(t => t.Modules)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static string NomeDoTipo(CountedTable mesa) =>
        mesa.Identity is null ? Tr.T("(peças órfãs)")
        : string.IsNullOrWhiteSpace(mesa.Identity.ProfileName) ? Tr.T("(sem tipo)")
        : mesa.Identity.ProfileName.Trim();

    private static double AreaDosModulos(AreaTableInput mesa) =>
        mesa.ModuleFaceArea is { } face && double.IsFinite(face) && face > 0 ? mesa.Table.Modules * face : 0;

    private static bool SemFace(AreaTableInput mesa) => mesa.Table.Modules > 0 && AreaDosModulos(mesa) == 0;

    private static Guid Chave(int posicao) => new(posicao + 1, 0, 0, new byte[8]);

    private static int Posicao(Guid chave) => BitConverter.ToInt32(chave.ToByteArray(), 0) - 1;
}
