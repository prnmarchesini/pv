using System.Globalization;
using System.Xml.Linq;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Escreve as mesas como o PVcase as manda para o PVsyst: o PV Collada, o
/// Collada 1.4.1 com um <c>frame_parameters</c> em cada malha, que o PVsyst
/// importa desde a 7.0 ("Adding one of these tags to the mesh will define it
/// as a PV mesh", help do PVsyst 8.0.16, "PV Collada file format").
///
/// É o arquivo do PVcase, peça por peça (as quatro exportações do Renan, de
/// Porto Feliz e de Mairinque, 31/10/2026):
/// <list type="bullet">
/// <item><description>cada mesa é uma geometria de oito vértices, a face de
/// cima dos módulos e o fundo deles (a espessura do módulo abaixo), e dois
/// triângulos da face de cima, "3 1 2 1 3 0", girando como os do PVcase (a
/// normal deles para baixo, do lado do fundo);</description></item>
/// <item><description>o material se chama "Frames";</description></item>
/// <item><description><c>frame_parameters</c>: a largura e a altura do módulo
/// (o lado menor e o maior), o vão ao longo da mesa e o vão entre as
/// fileiras, em metro ao milímetro, e o fabricante e o modelo "generic" (o
/// módulo de verdade é o do projeto no PVsyst);</description></item>
/// <item><description>os vértices com Y para cima e o nó raiz girando para Z
/// para cima, como o PVcase grava e o PVsyst lê.</description></item>
/// </list>
///
/// As coordenadas vão relativas a uma origem local, como no DAE do 6.2 (UTM
/// em float32 perde o milímetro), registrada no cabeçalho.
/// </summary>
public static class PvColladaWriter
{
    /// <summary>O namespace do Collada 1.4.1, o mesmo do DAE.</summary>
    public static readonly XNamespace Ns = ColladaWriter.Ns;

    /// <summary>O nome do material das mesas, o do PVcase.</summary>
    public const string FramesMaterial = "Frames";

    /// <summary>O fabricante e o modelo do módulo no arquivo, como o PVcase escreve.</summary>
    public const string GenericModule = "generic";

    /// <summary>Os dois triângulos da face de cima, os do PVcase.</summary>
    public const string Triangles = "3 1 2 1 3 0";

    /// <summary>A matriz do nó raiz, a do PVcase: Y para cima no arquivo, Z para cima na cena.</summary>
    public const string RootMatrix = "1 0 0 0 0 0 -1 0 0 1 0 0 0 0 0 1";

    private const string MaterialId = "Material0";

    /// <summary>
    /// Escreve o documento.
    /// </summary>
    /// <param name="frames">As fiadas, uma por mesa (ou por pedaço de mesa).</param>
    /// <param name="createdAt">A data do arquivo, no cabeçalho.</param>
    /// <param name="author">Quem gerou, no cabeçalho.</param>
    /// <param name="origin">A origem local, em metro, no sistema do desenho; subtraída de todos os cantos.</param>
    /// <exception cref="ArgumentException">Nenhuma fiada, fiada sem quatro cantos finitos, sem área ou de pé, medida impossível, origem não finita.</exception>
    public static XDocument Write(IReadOnlyList<PvFrame> frames, DateTime createdAt, string author, Point3 origin)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        if (!origin.IsFinite) throw new ArgumentException("A origem local não é finita.", nameof(origin));

        // Sem mesa o documento seria inválido e inútil no PVsyst: quem chama diz "nenhuma mesa".
        if (frames.Count == 0) throw new ArgumentException("Não há mesa para exportar.", nameof(frames));

        foreach (var fiada in frames)
        {
            if (fiada is null || fiada.Table == Guid.Empty || fiada.Corners is not { Count: 4 } || !fiada.Corners.All(c => c.IsFinite))
                throw new ArgumentException("Há fiada sem quatro cantos finitos ou sem mesa.", nameof(frames));

            if (fiada.Columns < 1 || fiada.Rows < 1
                || !Positivo(fiada.ModuleWidth) || !Positivo(fiada.ModuleHeight)
                || !NaoNegativo(fiada.SpacingX) || !NaoNegativo(fiada.SpacingY) || !NaoNegativo(fiada.Thickness))
                throw new ArgumentException($"A fiada {fiada.Name} tem medida impossível.", nameof(frames));
        }

        var geometrias = new XElement(Ns + "library_geometries");
        var mesas = new XElement(Ns + "node",
            new XAttribute("id", "mesas"), new XAttribute("name", "Mesas"), new XAttribute("sid", "mesas"),
            new XElement(Ns + "matrix", new XAttribute("sid", "matrix"), "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1"));
        var pedacos = new Dictionary<Guid, int>();

        foreach (var fiada in frames)
        {
            var pedaco = pedacos.TryGetValue(fiada.Table, out var ja) ? ja : 0;
            pedacos[fiada.Table] = pedaco + 1;

            var id = GeometryId(fiada.Table, pedaco);
            geometrias.Add(Geometria(id, fiada, origin));
            mesas.Add(new XElement(Ns + "instance_geometry",
                new XAttribute("url", "#" + id),
                new XElement(Ns + "bind_material",
                    new XElement(Ns + "technique_common",
                        new XElement(Ns + "instance_material", new XAttribute("symbol", MaterialId), new XAttribute("target", "#" + MaterialId))))));
        }

        var quando = createdAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var raiz = new XElement(Ns + "COLLADA",
            new XAttribute("version", "1.4.1"),
            new XElement(Ns + "asset",
                new XElement(Ns + "contributor",
                    new XElement(Ns + "author", author),
                    new XElement(Ns + "authoring_tool", PluginInfo.Nome),
                    new XElement(Ns + "comments", ColladaWriter.OriginComment(origin))),
                new XElement(Ns + "created", quando),
                new XElement(Ns + "modified", quando),
                new XElement(Ns + "unit", new XAttribute("name", "meter"), new XAttribute("meter", "1"))),
            new XElement(Ns + "library_materials",
                new XElement(Ns + "material",
                    new XAttribute("id", MaterialId), new XAttribute("name", FramesMaterial),
                    new XElement(Ns + "instance_effect", new XAttribute("url", "#" + MaterialId + "-fx")))),
            new XElement(Ns + "library_effects",
                new XElement(Ns + "effect",
                    new XAttribute("id", MaterialId + "-fx"), new XAttribute("name", MaterialId),
                    new XElement(Ns + "profile_COMMON",
                        new XElement(Ns + "technique",
                            new XAttribute("sid", "standard"),
                            new XElement(Ns + "lambert",
                                new XElement(Ns + "diffuse",
                                    new XElement(Ns + "color", new XAttribute("sid", "diffuse"), "0.25 0.5 0 1"))))))),
            geometrias,
            new XElement(Ns + "library_visual_scenes",
                new XElement(Ns + "visual_scene",
                    new XAttribute("id", "cena"), new XAttribute("name", PluginInfo.Nome),
                    new XElement(Ns + "node",
                        new XAttribute("id", "raiz"), new XAttribute("name", PluginInfo.Nome), new XAttribute("sid", "raiz"),
                        new XElement(Ns + "matrix", new XAttribute("sid", "matrix"), RootMatrix),
                        mesas))),
            new XElement(Ns + "scene",
                new XElement(Ns + "instance_visual_scene", new XAttribute("url", "#cena"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), raiz);
    }

    /// <summary>
    /// Uma origem local para as fiadas: o menor X, Y e Z entre os cantos,
    /// arredondados para baixo ao metro inteiro, como a do DAE.
    /// </summary>
    public static Point3 LocalOrigin(IReadOnlyList<PvFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var cantos = frames.Where(f => f?.Corners is not null).SelectMany(f => f.Corners).Where(c => c.IsFinite).ToList();
        if (cantos.Count == 0) return new Point3(0, 0, 0);

        return new Point3(Math.Floor(cantos.Min(c => c.X)), Math.Floor(cantos.Min(c => c.Y)), Math.Floor(cantos.Min(c => c.Z)));
    }

    /// <summary>
    /// Um ponto do desenho como vai no arquivo: relativo à origem e com Y para
    /// cima (x = leste, y = cima, z = −norte). A matriz do nó raiz o devolve.
    /// </summary>
    public static (double X, double Y, double Z) ToFile(Point3 point, Point3 origin) =>
        (point.X - origin.X, point.Z - origin.Z, -(point.Y - origin.Y));

    /// <summary>
    /// O id da geometria de uma fiada: "mesa-" e o GUID da mesa (regra sagrada
    /// 3 atravessando para o PVsyst), com "-2", "-3" nos pedaços seguintes.
    /// </summary>
    public static string GeometryId(Guid table, int piece) =>
        piece == 0 ? "mesa-" + table.ToString("N") : $"mesa-{table:N}-{piece + 1}";

    /// <summary>
    /// A fiada como geometria: a face de cima e o fundo, a espessura do módulo
    /// abaixo. Os cantos vão no sentido do PVcase: a borda baixa primeiro (v0
    /// v1), percorrida de modo que, vista de cima, a volta seja horária, e a
    /// normal dos triângulos "3 1 2 1 3 0" aponte para baixo, para o lado do
    /// fundo. É assim em todas as mesas dos quatro arquivos do PVcase e dos dois
    /// exemplos do PVsyst, sem exceção (revisão do 42.7); a fiada anti-horária
    /// vai com a borda baixa ao contrário (c1 c0 c3 c2).
    /// </summary>
    private static XElement Geometria(string id, PvFrame fiada, Point3 origin)
    {
        var c = fiada.Corners;
        var ux = c[1].X - c[0].X; var uy = c[1].Y - c[0].Y; var uz = c[1].Z - c[0].Z;
        var vx = c[3].X - c[0].X; var vy = c[3].Y - c[0].Y; var vz = c[3].Z - c[0].Z;
        var nx = uy * vz - uz * vy; var ny = uz * vx - ux * vz; var nz = ux * vy - uy * vx;
        var tamanho = Math.Sqrt(nx * nx + ny * ny + nz * nz);

        if (tamanho <= 1e-12) throw new ArgumentException($"A fiada {fiada.Name} não tem área.", nameof(fiada));
        if (Math.Abs(nz) / tamanho <= 1e-9) throw new ArgumentException($"A fiada {fiada.Name} está de pé.", nameof(fiada));

        // Horária vista de cima, como o PVcase.
        IReadOnlyList<Point3> cima = nz > 0 ? [c[1], c[0], c[3], c[2]] : c;

        // A normal que sobe, para o fundo ficar abaixo.
        var sinal = nz > 0 ? 1.0 : -1.0;
        nx = nx / tamanho * sinal; ny = ny / tamanho * sinal; nz = nz / tamanho * sinal;

        var vertices = cima.Concat(cima.Select(p => new Point3(p.X - nx * fiada.Thickness, p.Y - ny * fiada.Thickness, p.Z - nz * fiada.Thickness)));
        var posicoes = string.Join(" ", vertices.Select(p =>
        {
            var (x, y, z) = ToFile(p, origin);
            return $"{Coordenada(x)} {Coordenada(y)} {Coordenada(z)}";
        }));

        return new XElement(Ns + "geometry",
            new XAttribute("id", id),
            new XAttribute("name", fiada.Name),
            new XElement(Ns + "mesh",
                new XElement(Ns + "source",
                    new XAttribute("id", id + "-pos"),
                    new XElement(Ns + "float_array", new XAttribute("id", id + "-pos-array"), new XAttribute("count", "24"), posicoes),
                    new XElement(Ns + "technique_common",
                        new XElement(Ns + "accessor",
                            new XAttribute("source", "#" + id + "-pos-array"), new XAttribute("count", "8"), new XAttribute("stride", "3"),
                            Param("X"), Param("Y"), Param("Z")))),
                new XElement(Ns + "vertices",
                    new XAttribute("id", id + "-vtx"),
                    new XElement(Ns + "input", new XAttribute("semantic", "POSITION"), new XAttribute("source", "#" + id + "-pos"))),
                new XElement(Ns + "triangles",
                    new XAttribute("count", "2"),
                    new XAttribute("material", MaterialId),
                    new XElement(Ns + "input", new XAttribute("offset", "0"), new XAttribute("semantic", "VERTEX"), new XAttribute("source", "#" + id + "-vtx")),
                    new XElement(Ns + "p", Triangles)),
                new XElement(Ns + "frame_parameters",
                    new XElement(Ns + "module_width", Medida(fiada.ModuleWidth)),
                    new XElement(Ns + "module_height", Medida(fiada.ModuleHeight)),
                    new XElement(Ns + "module_x_spacing", Medida(fiada.SpacingX)),
                    new XElement(Ns + "module_y_spacing", Medida(fiada.SpacingY)),
                    new XElement(Ns + "module_manufacturer", GenericModule),
                    new XElement(Ns + "module_name", GenericModule))));
    }

    private static XElement Param(string nome) =>
        new(Ns + "param", new XAttribute("name", nome), new XAttribute("type", "float"));

    /// <summary>Coordenada ao micrômetro, como o PVcase (seis casas), sem "-0".</summary>
    private static string Coordenada(double valor) =>
        Math.Abs(valor) < 5e-7 ? "0" : valor.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>
    /// Medida do módulo e vão em metro ao milímetro, como o PVcase ("1.303",
    /// "0.02"), sempre com o ponto: as referências leem número inteiro como
    /// milímetro (992 no exemplo do PVsyst), e um módulo de 2 m sairia "2".
    /// </summary>
    private static string Medida(double valor) =>
        Math.Abs(valor) < 5e-4 ? "0.0" : valor.ToString("0.0##", CultureInfo.InvariantCulture);

    private static bool Positivo(double valor) => double.IsFinite(valor) && valor > 0;

    private static bool NaoNegativo(double valor) => double.IsFinite(valor) && valor >= 0;
}
