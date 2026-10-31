using Clipper2Lib;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>O que calcular (42.1, Renan, 31/10/2026: "Seleção, tudo de uma area, tudo de uma area mais seleção, varias areas, desenho inteiro").</summary>
public enum ShadowScopeKind
{
    /// <summary>As mesas escolhidas no desenho.</summary>
    Selection = 0,

    /// <summary>Uma área de implantação.</summary>
    Area = 1,

    /// <summary>Uma área e mais as mesas escolhidas.</summary>
    AreaAndSelection = 2,

    /// <summary>Várias áreas.</summary>
    Areas = 3,

    /// <summary>O desenho inteiro.</summary>
    Drawing = 4,
}

/// <summary>Onde está uma entidade de sombra.</summary>
public enum ShadowPlace
{
    /// <summary>No chão (hachura e borda).</summary>
    Ground,

    /// <summary>Sobre uma mesa (hachura e borda no plano dela).</summary>
    Table,

    /// <summary>A etiqueta (texto).</summary>
    Label,
}

/// <summary>O escopo pedido: o tipo, as áreas escolhidas e as mesas escolhidas (GUIDs).</summary>
public sealed record ShadowScope(ShadowScopeKind Kind, IReadOnlyList<Guid> Areas, IReadOnlyList<Guid> Tables)
{
    /// <summary>O desenho inteiro.</summary>
    public static ShadowScope WholeDrawing { get; } = new(ShadowScopeKind.Drawing, [], []);

    /// <summary>Os tipos na ordem da janela.</summary>
    public static IReadOnlyList<ShadowScopeKind> Kinds { get; } =
        [ShadowScopeKind.Selection, ShadowScopeKind.Area, ShadowScopeKind.AreaAndSelection, ShadowScopeKind.Areas, ShadowScopeKind.Drawing];

    /// <summary>Se o escopo usa áreas escolhidas.</summary>
    public bool UsesAreas => Kind is ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection or ShadowScopeKind.Areas;

    /// <summary>Se o escopo usa as mesas escolhidas.</summary>
    public bool UsesSelection => Kind is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection;

    /// <summary>O que falta para o escopo valer, ou null se vale.</summary>
    public string? WhyInvalid() => Kind switch
    {
        ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection when Areas.Count != 1 => Tr.T("Escolha uma área."),
        ShadowScopeKind.Areas when Areas.Count == 0 => Tr.T("Escolha pelo menos uma área."),
        ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection when Tables.Count == 0 => Tr.T("Selecione as mesas no desenho (clique nelas) e use Pegar seleção."),
        _ => null,
    };

    /// <summary>O nome do tipo, para a janela.</summary>
    public static string Name(ShadowScopeKind kind) => kind switch
    {
        ShadowScopeKind.Selection => Tr.T("Seleção"),
        ShadowScopeKind.Area => Tr.T("Uma área"),
        ShadowScopeKind.AreaAndSelection => Tr.T("Área + seleção"),
        ShadowScopeKind.Areas => Tr.T("Várias áreas"),
        _ => Tr.T("Desenho inteiro"),
    };

    /// <summary>O tipo pela palavra da linha de comando (SELECAO, AREA, AREA+SELECAO, AREAS, TUDO), ou null.</summary>
    public static ShadowScopeKind? Parse(string? text) => text?.Trim().ToUpperInvariant() switch
    {
        "SELECAO" or "SELEÇÃO" or "SELECTION" => ShadowScopeKind.Selection,
        "AREA" or "ÁREA" => ShadowScopeKind.Area,
        "AREA+SELECAO" or "ÁREA+SELEÇÃO" or "AREA+SELECTION" => ShadowScopeKind.AreaAndSelection,
        "AREAS" or "ÁREAS" => ShadowScopeKind.Areas,
        "TUDO" or "DESENHO" or "ALL" or "DRAWING" => ShadowScopeKind.Drawing,
        _ => null,
    };
}

/// <summary>Uma área de implantação para o escopo: o GUID, o nome e o contorno.</summary>
public sealed record ShadowArea(Guid Id, string Name, IReadOnlyList<Point3> Outline);

/// <summary>Uma mesa do desenho para o escopo: o GUID e o contorno.</summary>
public sealed record ShadowTable(Guid Id, IReadOnlyList<Point3> Outline);

/// <summary>Um pedaço do chão do escopo e a área dele (null: fora das áreas).</summary>
public sealed record ShadowRegionPart(Guid? Area, IReadOnlyList<IReadOnlyList<(double X, double Y)>> Rings);

/// <summary>
/// A marca de uma entidade de sombra no desenho, como o XData dela diz (ou,
/// nas de antes de 31/10/2026, como a posição dela diz): onde está, a área,
/// a mesa (sobre a mesa) e o ladrilho de 8 m (no chão e na etiqueta).
/// </summary>
public sealed record ShadowTag(ShadowPlace Place, Guid? Area, Guid? Table, (int X, int Y)? Tile);

/// <summary>
/// O escopo resolvido no desenho (42.1): quem recebe sombra, o chão em que
/// ela é desenhada (por área), quem faz sombra e o que um novo cálculo ou o
/// Apagar levam.
/// <list type="bullet">
/// <item>Recebem as mesas do escopo: na área, pelo centro do contorno (dentro
/// de duas, a menor, a regra do resumo por área); na seleção, as escolhidas;
/// no desenho inteiro, todas.</item>
/// <item>O chão de cada área é o território dela: a área menos as menores que
/// estão dentro dela (o ponto do chão é da menor área que o contém, como a
/// mesa). Na seleção, os ladrilhos de 8 m em volta das mesas escolhidas
/// (<see cref="GroundPatches.TileSize"/>), repartidos pelas áreas.</item>
/// <item>Fazem sombra as mesas e as árvores até a margem em volta de quem
/// recebe (<see cref="Reaches"/>), e o terreno.</item>
/// <item>Gerar ou Apagar num escopo troca só o dele: as entidades marcadas
/// com uma área dele, as das mesas escolhidas e as dos ladrilhos da
/// seleção (<see cref="Erases"/>).</item>
/// </list>
/// </summary>
public sealed class ShadowScopePlan
{
    private readonly HashSet<Guid> _areas;
    private readonly HashSet<Guid> _mesasEscolhidas;

    private ShadowScopePlan(
        ShadowScope scope, IReadOnlyList<int?> areaOfTable, IReadOnlyList<bool> receives, IReadOnlyList<ShadowRegionPart> ground,
        IReadOnlySet<(int X, int Y)> tiles, (double MinX, double MinY, double MaxX, double MaxY)? bounds, HashSet<Guid> areas, HashSet<Guid> mesas)
    {
        Scope = scope;
        AreaOfTable = areaOfTable;
        Receives = receives;
        Ground = ground;
        Tiles = tiles;
        Bounds = bounds;
        _areas = areas;
        _mesasEscolhidas = mesas;
    }

    /// <summary>O escopo pedido.</summary>
    public ShadowScope Scope { get; }

    /// <summary>Para cada mesa recebida, a posição da área dela (pelo centro), ou null fora das áreas.</summary>
    public IReadOnlyList<int?> AreaOfTable { get; }

    /// <summary>Para cada mesa recebida, se ela recebe sombra neste escopo.</summary>
    public IReadOnlyList<bool> Receives { get; }

    /// <summary>O chão do escopo, em pedaços com a área de cada um.</summary>
    public IReadOnlyList<ShadowRegionPart> Ground { get; }

    /// <summary>Os ladrilhos de 8 m em volta das mesas escolhidas (na seleção) e das de fora das áreas (no desenho inteiro).</summary>
    public IReadOnlySet<(int X, int Y)> Tiles { get; }

    /// <summary>A caixa de quem recebe (as mesas e o chão), ou null se ninguém recebe.</summary>
    public (double MinX, double MinY, double MaxX, double MaxY)? Bounds { get; }

    /// <summary>Quantas mesas recebem.</summary>
    public int ReceiverCount => Receives.Count(r => r);

    /// <summary>
    /// Resolve o escopo.
    /// </summary>
    /// <param name="scope">O escopo pedido.</param>
    /// <param name="areas">As áreas do desenho, na ordem da janela.</param>
    /// <param name="tables">As mesas do desenho.</param>
    /// <param name="selectionBuffer">Quanto em volta das mesas escolhidas o chão entra (m), antes de arredondar para os ladrilhos.</param>
    public static ShadowScopePlan Build(ShadowScope scope, IReadOnlyList<ShadowArea> areas, IReadOnlyList<ShadowTable> tables, double selectionBuffer)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(tables);

        var entradas = areas.Select(a => new AreaInput(a.Id, a.Name, a.Outline, false)).ToList();
        var dono = tables.Select(t => AreaSummary.AreaOf(t.Outline, entradas)).ToList();
        var escolhidas = scope.UsesAreas ? scope.Areas.ToHashSet() : new HashSet<Guid>();
        var mesasEscolhidas = scope.UsesSelection ? scope.Tables.ToHashSet() : new HashSet<Guid>();

        bool Valida(ShadowTable t) => t.Outline.Count(p => p.IsFinite) >= 3;

        var recebe = tables.Select((t, k) => Valida(t) && (scope.Kind switch
        {
            ShadowScopeKind.Drawing => true,
            ShadowScopeKind.Selection => mesasEscolhidas.Contains(t.Id),
            ShadowScopeKind.AreaAndSelection => mesasEscolhidas.Contains(t.Id) || (dono[k] is { } a && escolhidas.Contains(areas[a].Id)),
            _ => dono[k] is { } a2 && escolhidas.Contains(areas[a2].Id),
        })).ToList();

        // O território de cada área: ela menos as menores (a menor que contém manda).
        var contornos = areas.Select(a => a.Outline.Count(p => p.IsFinite) >= 3 ? Caminho(a.Outline) : []).ToList();
        var tamanhos = contornos.Select(c => c.Count >= 3 ? Math.Abs(Clipper.Area(c)) : double.PositiveInfinity).ToList();
        var territorios = new List<Paths64>();
        for (var i = 0; i < areas.Count; i++)
        {
            if (contornos[i].Count < 3)
            {
                territorios.Add([]);
                continue;
            }

            var menores = new Paths64();
            for (var j = 0; j < areas.Count; j++)
                if (j != i && contornos[j].Count >= 3 && (tamanhos[j] < tamanhos[i] || (tamanhos[j] == tamanhos[i] && j < i))) menores.Add(contornos[j]);

            territorios.Add(menores.Count == 0 ? [contornos[i]] : Clipper.Difference([contornos[i]], menores, FillRule.NonZero));
        }

        var todas = new Paths64(contornos.Where(c => c.Count >= 3));
        var partes = new Dictionary<Guid, Paths64>();
        var semArea = new Paths64();

        void Juntar(Guid? area, Paths64 caminhos)
        {
            if (caminhos.Count == 0) return;
            if (area is null)
            {
                semArea.AddRange(caminhos);
                return;
            }

            if (!partes.TryGetValue(area.Value, out var lista)) partes[area.Value] = lista = [];
            lista.AddRange(caminhos);
        }

        // As áreas escolhidas (ou todas, no desenho inteiro), inteiras.
        for (var i = 0; i < areas.Count; i++)
            if (scope.Kind == ShadowScopeKind.Drawing || escolhidas.Contains(areas[i].Id)) Juntar(areas[i].Id, territorios[i]);

        // Os ladrilhos: em volta das escolhidas, ou, no desenho inteiro, das de fora das áreas.
        var ladrilhos = new HashSet<(int X, int Y)>();
        for (var k = 0; k < tables.Count; k++)
        {
            if (!recebe[k]) continue;

            var emVolta = scope.Kind switch
            {
                ShadowScopeKind.Drawing => dono[k] is null,
                ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection => mesasEscolhidas.Contains(tables[k].Id),
                _ => false,
            };
            if (!emVolta) continue;

            var pontos = tables[k].Outline.Where(p => p.IsFinite).ToList();
            var (x0, y0) = GroundPatches.TileOf(pontos.Min(p => p.X) - selectionBuffer, pontos.Min(p => p.Y) - selectionBuffer);
            var (x1, y1) = GroundPatches.TileOf(pontos.Max(p => p.X) + selectionBuffer, pontos.Max(p => p.Y) + selectionBuffer);
            for (var ty = y0; ty <= y1; ty++)
                for (var tx = x0; tx <= x1; tx++)
                    ladrilhos.Add((tx, ty));
        }

        if (ladrilhos.Count > 0)
        {
            var quadrados = new Paths64(ladrilhos.Select(t => ShadowPaths.Rect(
                t.X * GroundPatches.TileSize, t.Y * GroundPatches.TileSize, (t.X + 1) * GroundPatches.TileSize, (t.Y + 1) * GroundPatches.TileSize)));
            var regiao = Clipper.Union(quadrados, FillRule.NonZero);

            // O que já entrou pelas áreas escolhidas não entra de novo.
            var jaEntrou = new Paths64(partes.Values.SelectMany(p => p));
            if (jaEntrou.Count > 0) regiao = Clipper.Difference(regiao, jaEntrou, FillRule.NonZero);

            for (var i = 0; i < areas.Count; i++)
                if (territorios[i].Count > 0) Juntar(areas[i].Id, Clipper.Intersect(regiao, territorios[i], FillRule.NonZero));

            Juntar(null, todas.Count == 0 ? regiao : Clipper.Difference(regiao, todas, FillRule.NonZero));
        }

        var chao = partes
            .Select(p => new ShadowRegionPart(p.Key, ShadowPaths.Rings(Clipper.Union(p.Value, FillRule.NonZero), 2500)))
            .Concat(semArea.Count > 0 ? [new ShadowRegionPart(null, ShadowPaths.Rings(Clipper.Union(semArea, FillRule.NonZero), 2500))] : [])
            .Where(p => p.Rings.Count > 0)
            .ToList();

        // A caixa de quem recebe.
        var xs = new List<double>();
        var ys = new List<double>();
        for (var k = 0; k < tables.Count; k++)
        {
            if (!recebe[k]) continue;
            foreach (var p in tables[k].Outline.Where(p => p.IsFinite))
            {
                xs.Add(p.X);
                ys.Add(p.Y);
            }
        }

        foreach (var p in chao.SelectMany(c => c.Rings).SelectMany(r => r))
        {
            xs.Add(p.X);
            ys.Add(p.Y);
        }

        (double, double, double, double)? caixa = xs.Count == 0 ? null : (xs.Min(), ys.Min(), xs.Max(), ys.Max());

        return new ShadowScopePlan(scope, dono, recebe, chao, ladrilhos, caixa, escolhidas, mesasEscolhidas);
    }

    /// <summary>
    /// Se um novo cálculo (ou o Apagar) neste escopo leva a entidade de
    /// sombra com esta marca: no desenho inteiro, todas; senão, as de uma
    /// área do escopo, e, na seleção, as das mesas escolhidas e as dos
    /// ladrilhos em volta delas.
    /// </summary>
    public bool Erases(ShadowTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (Scope.Kind == ShadowScopeKind.Drawing) return true;
        if (tag.Area is { } area && _areas.Contains(area)) return true;
        if (!Scope.UsesSelection) return false;

        return tag.Place == ShadowPlace.Table
            ? tag.Table is { } mesa && _mesasEscolhidas.Contains(mesa)
            : tag.Tile is { } ladrilho && Tiles.Contains(ladrilho);
    }

    /// <summary>Se o objeto (a caixa dele) está até <paramref name="margin"/> de quem recebe: só esses fazem sombra no cálculo.</summary>
    public bool Reaches(double minX, double minY, double maxX, double maxY, double margin)
    {
        if (Bounds is not { } b) return false;

        var dx = Math.Max(0, Math.Max(minX - b.MaxX, b.MinX - maxX));
        var dy = Math.Max(0, Math.Max(minY - b.MaxY, b.MinY - maxY));
        return dx * dx + dy * dy <= margin * margin;
    }

    /// <summary>A área (GUID) da mesa na posição <paramref name="table"/>, ou null fora das áreas.</summary>
    public Guid? AreaIdOf(int table, IReadOnlyList<ShadowArea> areas) =>
        AreaOfTable[table] is { } a && a >= 0 && a < areas.Count ? areas[a].Id : null;

    private static Path64 Caminho(IReadOnlyList<Point3> contorno) =>
        ShadowPaths.Path(contorno.Where(p => p.IsFinite).Select(p => (p.X, p.Y)).ToList());
}
