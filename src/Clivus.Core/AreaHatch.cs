using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// A marca da hachura de uma área no desenho (XData, tipo <see cref="Tipo"/>),
/// no hatch e no letreiro: o GUID e o handle da polilinha da área. É por ela
/// que hachurar de novo acha e troca a hachura antiga daquela área, e que a
/// janela diz se a área está hachurada. A hachura é representação: nada se lê
/// dela.
/// </summary>
/// <param name="Area">O GUID da área.</param>
/// <param name="AreaHandle">O handle da polilinha da área quando a hachura foi feita.</param>
public sealed record AreaHatchMark(Guid Area, string AreaHandle)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "AreaHachura";

    public const int FieldCount = 2;

    public bool IsValid => Area != Guid.Empty;

    /// <summary>
    /// Se a marca é desta área. Pela polilinha (o GUID e o handle juntos):
    /// a polilinha copiada no mesmo desenho leva o GUID (o Validar aponta), e
    /// cada cópia tem a sua hachura. Sem polilinha nenhuma com o par (o
    /// desenho foi copiado para outro e os handles mudaram), vale o GUID.
    /// </summary>
    /// <param name="areaId">O GUID da área perguntada.</param>
    /// <param name="areaHandle">O handle da polilinha dela.</param>
    /// <param name="areas">Todas as áreas do desenho (GUID e handle).</param>
    public bool Belongs(Guid areaId, string areaHandle, IEnumerable<(Guid Id, string Handle)> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        if (Area != areaId) return false;
        if (string.Equals(AreaHandle, areaHandle, StringComparison.OrdinalIgnoreCase)) return true;

        return !areas.Any(a => a.Id == Area && string.Equals(a.Handle, AreaHandle, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// As regras da hachura das áreas (41.2): a cor de cada área, a cota do
/// hatch e onde vai o nome. O plugin só desenha.
/// <para>
/// A cota (regra sagrada 5): o hatch é plano, e fica na cota mais alta do
/// TERRENO dentro da área (os vértices do TIN de dentro e o máximo de verdade
/// ao longo de cada lado do contorno) e das mesas que têm canto nela, mais a
/// folga da marca do grupo: em vista 3D ele nunca afunda no morro nem nos
/// módulos, e acompanha o terreno. É a escolha do hatch das bacias (a cota
/// mais alta da mancha) somada à da marca do grupo e da área do trafo (acima
/// do canto mais alto das mesas). Nunca a cota de um clique. Sem terreno, os
/// vértices da própria área, que vieram do terreno quando ela foi assentada.
/// </para>
/// </summary>
public static class AreaHatch
{
    /// <summary>70 % transparente (o alfa do AutoCAD vai de 0, invisível, a 255, opaco): as mesas aparecem por baixo.</summary>
    public const byte Alpha = 77;

    /// <summary>
    /// A cor da área pela posição dela na lista: a paleta dos inversores, a
    /// mesma das bacias (vizinhas bem diferentes, longe das cores de aviso e
    /// da sombra). Dez cores; da décima primeira em diante, recomeça.
    /// </summary>
    public static RgbColor ColorOf(int index)
    {
        var paleta = InverterColors.Palette;
        return paleta[((index % paleta.Count) + paleta.Count) % paleta.Count].Color;
    }

    /// <summary>
    /// A cota do hatch da área: a mais alta do terreno dentro dela e das
    /// mesas que têm canto nela, mais a folga. Null se não há cota nenhuma
    /// (área sem três vértices).
    /// </summary>
    /// <param name="terrain">O terreno processado, ou null (sem terreno: a cota dos vértices da área).</param>
    /// <param name="outline">O contorno da área (em planta; o Z só vale sem terreno).</param>
    /// <param name="tables">Os cantos de cada mesa do desenho, em 3D.</param>
    /// <param name="clearance">A folga acima de tudo, em metro.</param>
    public static double? Elevation(Tin? terrain, IReadOnlyList<Point3> outline, IEnumerable<IReadOnlyList<Point3>> tables, double clearance)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(tables);

        if (outline.Count < 3 || outline.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return null;

        double? alto = null;
        void Somar(double z)
        {
            if (double.IsFinite(z)) alto = alto is { } a ? Math.Max(a, z) : z;
        }

        if (terrain is not null)
        {
            var (x0, y0) = (outline.Min(p => p.X), outline.Min(p => p.Y));
            var (x1, y1) = (outline.Max(p => p.X), outline.Max(p => p.Y));

            // Os vértices do TIN de dentro: o morro no meio da área.
            void Vertice(Point3 v)
            {
                if (v.X < x0 || v.X > x1 || v.Y < y0 || v.Y > y1 || !v.IsFinite) return;
                if (Polygons.Contains(outline, v.X, v.Y)) Somar(v.Z);
            }

            foreach (var t in terrain.Triangles)
            {
                Vertice(t.A);
                Vertice(t.B);
                Vertice(t.C);
            }

            // A borda: o máximo de verdade ao longo de cada lado (a crista que
            // cruza o lado entre dois vértices do TIN); o lado que sai do
            // terreno vale pelas pontas que estão nele.
            for (var i = 0; i < outline.Count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
                if (terrain.TryGetMaxZAlong(a.X, a.Y, b.X, b.Y, out var z)) Somar(z);
                else if (terrain.TryGetZ(a.X, a.Y, out var za)) Somar(za);
            }
        }

        if (alto is null)
            foreach (var p in outline) Somar(p.Z);

        foreach (var mesa in tables)
        {
            if (!mesa.Any(p => p.IsFinite && Polygons.Contains(outline, p.X, p.Y))) continue;
            foreach (var p in mesa) Somar(p.Z);
        }

        return alto + clearance;
    }

    /// <summary>
    /// Onde vai o nome da área: o centroide, se ele cai dentro; senão (área
    /// em U, em L), o meio do trecho mais largo de dentro na horizontal que
    /// passa pelo centroide. Sempre dentro da área (o Z é zero; a cota é a do hatch).
    /// </summary>
    public static Point3 LabelPoint(IReadOnlyList<Point3> outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var pontos = outline.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToList();
        if (pontos.Count == 0) throw new ArgumentException("O contorno não tem ponto finito.", nameof(outline));
        if (pontos.Count < 3) return new Point3(pontos.Average(p => p.X), pontos.Average(p => p.Y), 0);

        var centro = Centroide(pontos);
        if (Polygons.Contains(pontos, centro.X, centro.Y)) return centro;

        var esquerda = pontos.Min(p => p.X) - 1;
        foreach (var y in new[] { centro.Y, (pontos.Min(p => p.Y) + pontos.Max(p => p.Y)) / 2 })
        {
            var cortes = Polygons.Crossings(pontos, new Point3(esquerda, y, 0), new Point3(1, 0, 0));
            var melhor = (Largura: 0.0, X: double.NaN);

            for (var i = 0; i + 1 < cortes.Count; i += 2)
            {
                var largura = cortes[i + 1] - cortes[i];
                if (largura > melhor.Largura) melhor = (largura, esquerda + (cortes[i] + cortes[i + 1]) / 2);
            }

            if (double.IsFinite(melhor.X)) return new Point3(melhor.X, y, 0);
        }

        return new Point3(pontos[0].X, pontos[0].Y, 0);
    }

    /// <summary>A altura do nome, em metro, pelo tamanho da área: lado médio ÷ 30, entre 2 e 15 m.</summary>
    public static double LabelHeight(double planArea) =>
        double.IsFinite(planArea) && planArea > 0 ? Math.Clamp(Math.Sqrt(planArea) / 30, 2, 15) : 2;

    /// <summary>O nome como conteúdo de MTEXT: barra invertida e chaves escapadas (senão viram código de formatação).</summary>
    public static string LabelText(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("{", "\\{", StringComparison.Ordinal).Replace("}", "\\}", StringComparison.Ordinal);
    }

    /// <summary>O centroide do polígono (pela área, contado a partir do primeiro vértice: em UTM a conta direta perde casas).</summary>
    private static Point3 Centroide(IReadOnlyList<Point3> p)
    {
        var (ox, oy) = (p[0].X, p[0].Y);
        double area = 0, cx = 0, cy = 0;

        for (var i = 0; i < p.Count; i++)
        {
            var (ax, ay) = (p[i].X - ox, p[i].Y - oy);
            var (bx, by) = (p[(i + 1) % p.Count].X - ox, p[(i + 1) % p.Count].Y - oy);
            var cruz = ax * by - bx * ay;
            area += cruz;
            cx += (ax + bx) * cruz;
            cy += (ay + by) * cruz;
        }

        if (Math.Abs(area) < 1e-12) return new Point3(p.Average(q => q.X), p.Average(q => q.Y), 0);

        return new Point3(ox + cx / (3 * area), oy + cy / (3 * area), 0);
    }
}
