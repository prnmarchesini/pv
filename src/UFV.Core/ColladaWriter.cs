using System.Globalization;
using System.Xml.Linq;
using UFV.Geo;

namespace UFV.Core;

/// <summary>A face superior de um módulo, como vai para o PVsyst.</summary>
/// <param name="Id">O GUID da face (<see cref="FaceIdentity"/>).</param>
/// <param name="Corners">Os quatro cantos, em metro, em ordem anti-horária vistos
/// de cima. Se vierem horários, o escritor inverte a ordem: a normal sai
/// para cima de qualquer jeito. Os quatro são tomados como coplanares (a mesa
/// é rígida, regra sagrada 2); não há conferência disso.</param>
public sealed record ModuleFace(Guid Id, IReadOnlyList<Point3> Corners)
{
    /// <summary>Se a face tem quatro cantos finitos.</summary>
    public bool IsValid => Corners is { Count: 4 } && Corners.All(c => c.IsFinite) && Id != Guid.Empty;
}

/// <summary>
/// Escreve as faces superiores dos módulos em Collada 1.4.1 (DAE), o
/// formato de cena 3D que o PVsyst importa.
///
/// É o passo 6.1, puro: recebe faces e devolve XML. Nada de CAD. Cada face é
/// uma geometria própria (quatro vértices, dois triângulos, normal para
/// cima) e um nó na cena. Todas as faces usam um único MATERIAL, com o nome
/// da camada do módulo: é pelo material que o PVsyst reconhece as
/// superfícies PV na importação ("pick up one or more materials used in the
/// imported scene and convert the faces which use them to PV fields", help
/// do PVsyst 7 e 8), e é esse nome que o Renan escolhe lá (6.3). O nome da
/// camada vai também nos nós, para quem abrir o arquivo noutro programa. Um
/// nó por face, e não uma malha única, porque o PVsyst conta objetos, e
/// cada objeto é um módulo.
///
/// Unidade metro, eixo Z para cima, como o desenho. Os números vão com
/// ponto e em formato redondo: o arquivo é lido noutra máquina, noutra
/// cultura, e "0,5" seria dois números.
/// </summary>
public static class ColladaWriter
{
    /// <summary>O namespace do Collada 1.4.1.</summary>
    public static readonly XNamespace Ns = "http://www.collada.org/2005/11/COLLADASchema";

    /// <summary>O símbolo pelo qual cada geometria pede o material do módulo.</summary>
    public const string MaterialSymbol = "modulo";

    /// <summary>
    /// Escreve o documento.
    /// </summary>
    /// <param name="faces">As faces, uma por módulo.</param>
    /// <param name="layerName">O nome da camada do módulo, que vira o nome do material e dos nós.</param>
    /// <param name="createdAt">A data do arquivo, gravada no cabeçalho.</param>
    /// <param name="author">Quem gerou, gravado no cabeçalho.</param>
    /// <exception cref="ArgumentException">Nenhuma face, face sem quatro cantos finitos, GUID repetido, ou camada sem nome.</exception>
    public static XDocument Write(IReadOnlyList<ModuleFace> faces, string layerName, DateTime createdAt, string author)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        // Sem face o documento seria inválido pelo esquema (biblioteca e cena
        // sem filhos) e inútil no PVsyst: quem chama diz "nenhum módulo".
        if (faces.Count == 0)
            throw new ArgumentException("Não há face para exportar.", nameof(faces));

        var vistos = new HashSet<Guid>();

        foreach (var face in faces)
        {
            if (face is null || !face.IsValid)
                throw new ArgumentException("Há face sem quatro cantos finitos ou sem identidade.", nameof(faces));

            if (!vistos.Add(face.Id))
                throw new ArgumentException($"A face {face.Id:D} aparece duas vezes.", nameof(faces));
        }

        var materialId = MaterialId(layerName);
        var geometrias = new XElement(Ns + "library_geometries");
        var cena = new XElement(Ns + "visual_scene", new XAttribute("id", "cena"), new XAttribute("name", "UFV"));

        foreach (var face in faces)
        {
            var id = "face-" + face.Id.ToString("N");

            geometrias.Add(Geometria(id, face));
            cena.Add(new XElement(Ns + "node",
                new XAttribute("id", "no-" + face.Id.ToString("N")),
                new XAttribute("name", layerName),
                new XElement(Ns + "instance_geometry",
                    new XAttribute("url", "#" + id),
                    new XElement(Ns + "bind_material",
                        new XElement(Ns + "technique_common",
                            new XElement(Ns + "instance_material",
                                new XAttribute("symbol", MaterialSymbol),
                                new XAttribute("target", "#" + materialId)))))));
        }

        // Um efeito (a cor, azul escuro de módulo) e um material com o nome da
        // camada. É o material que o PVsyst lista na importação.
        var efeitos = new XElement(Ns + "library_effects",
            new XElement(Ns + "effect",
                new XAttribute("id", materialId + "-efeito"),
                new XElement(Ns + "profile_COMMON",
                    new XElement(Ns + "technique",
                        new XAttribute("sid", "common"),
                        new XElement(Ns + "lambert",
                            new XElement(Ns + "diffuse",
                                new XElement(Ns + "color", "0.1 0.2 0.5 1")))))));

        var materiais = new XElement(Ns + "library_materials",
            new XElement(Ns + "material",
                new XAttribute("id", materialId),
                new XAttribute("name", layerName),
                new XElement(Ns + "instance_effect", new XAttribute("url", "#" + materialId + "-efeito"))));

        var quando = createdAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var raiz = new XElement(Ns + "COLLADA",
            new XAttribute("version", "1.4.1"),
            new XElement(Ns + "asset",
                new XElement(Ns + "contributor",
                    new XElement(Ns + "author", author),
                    new XElement(Ns + "authoring_tool", PluginInfo.Nome)),
                new XElement(Ns + "created", quando),
                new XElement(Ns + "modified", quando),
                new XElement(Ns + "unit", new XAttribute("name", "meter"), new XAttribute("meter", "1")),
                new XElement(Ns + "up_axis", "Z_UP")),
            efeitos,
            materiais,
            geometrias,
            new XElement(Ns + "library_visual_scenes", cena),
            new XElement(Ns + "scene",
                new XElement(Ns + "instance_visual_scene", new XAttribute("url", "#cena"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), raiz);
    }

    /// <summary>
    /// Uma face como geometria: os quatro vértices, a normal (para cima, a
    /// mesma para os quatro), e dois triângulos (0 1 2) e (0 2 3) que cobrem
    /// o quadrilátero mantendo a orientação anti-horária.
    /// </summary>
    private static XElement Geometria(string id, ModuleFace face)
    {
        var c = face.Corners;

        // A normal do plano: (c1 − c0) × (c3 − c0), unitária. Para cima quando
        // os cantos são anti-horários vistos de cima; se vierem horários, a
        // ordem é invertida e a normal recalculada. Vertical não é módulo.
        var (nx, ny, nz) = Normal(c);

        if (double.IsNaN(nz)) throw new ArgumentException($"A face {face.Id:D} não tem área.", nameof(face));

        if (nz < 0)
        {
            c = [c[0], c[3], c[2], c[1]];
            (nx, ny, nz) = Normal(c);
        }

        if (nz <= 1e-9) throw new ArgumentException($"A face {face.Id:D} é vertical.", nameof(face));

        var posicoes = string.Join(" ", c.Select(p => $"{N(p.X)} {N(p.Y)} {N(p.Z)}"));

        return new XElement(Ns + "geometry",
            new XAttribute("id", id),
            new XAttribute("name", id),
            new XElement(Ns + "mesh",
                new XElement(Ns + "source",
                    new XAttribute("id", id + "-pos"),
                    new XElement(Ns + "float_array", new XAttribute("id", id + "-pos-array"), new XAttribute("count", "12"), posicoes),
                    new XElement(Ns + "technique_common",
                        new XElement(Ns + "accessor",
                            new XAttribute("source", "#" + id + "-pos-array"), new XAttribute("count", "4"), new XAttribute("stride", "3"),
                            Param("X"), Param("Y"), Param("Z")))),
                new XElement(Ns + "source",
                    new XAttribute("id", id + "-nrm"),
                    new XElement(Ns + "float_array", new XAttribute("id", id + "-nrm-array"), new XAttribute("count", "3"), $"{N(nx)} {N(ny)} {N(nz)}"),
                    new XElement(Ns + "technique_common",
                        new XElement(Ns + "accessor",
                            new XAttribute("source", "#" + id + "-nrm-array"), new XAttribute("count", "1"), new XAttribute("stride", "3"),
                            Param("X"), Param("Y"), Param("Z")))),
                new XElement(Ns + "vertices",
                    new XAttribute("id", id + "-vtx"),
                    new XElement(Ns + "input", new XAttribute("semantic", "POSITION"), new XAttribute("source", "#" + id + "-pos"))),
                new XElement(Ns + "triangles",
                    new XAttribute("count", "2"),
                    new XAttribute("material", MaterialSymbol),
                    new XElement(Ns + "input", new XAttribute("semantic", "VERTEX"), new XAttribute("source", "#" + id + "-vtx"), new XAttribute("offset", "0")),
                    new XElement(Ns + "input", new XAttribute("semantic", "NORMAL"), new XAttribute("source", "#" + id + "-nrm"), new XAttribute("offset", "1")),
                    new XElement(Ns + "p", "0 0 1 0 2 0 0 0 2 0 3 0"))));
    }

    /// <summary>A normal unitária de (c1 − c0) × (c3 − c0), ou NaN se não há área.</summary>
    private static (double X, double Y, double Z) Normal(IReadOnlyList<Point3> c)
    {
        var ux = c[1].X - c[0].X; var uy = c[1].Y - c[0].Y; var uz = c[1].Z - c[0].Z;
        var vx = c[3].X - c[0].X; var vy = c[3].Y - c[0].Y; var vz = c[3].Z - c[0].Z;
        var nx = uy * vz - uz * vy; var ny = uz * vx - ux * vz; var nz = ux * vy - uy * vx;
        var tamanho = Math.Sqrt(nx * nx + ny * ny + nz * nz);

        return tamanho <= 1e-12 ? (double.NaN, double.NaN, double.NaN) : (nx / tamanho, ny / tamanho, nz / tamanho);
    }

    /// <summary>
    /// O id XML do material: "material-" e o nome da camada com o que não é
    /// letra, dígito, ponto, hífen ou sublinhado trocado por sublinhado. O
    /// prefixo garante que não colide com os ids fixos ("cena") nem com os
    /// das faces. O NOME do material fica com a camada tal como é.
    /// </summary>
    private static string MaterialId(string layerName)
    {
        var chars = layerName.Trim().Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray();

        return "material-" + new string(chars);
    }

    private static XElement Param(string nome) =>
        new(Ns + "param", new XAttribute("name", nome), new XAttribute("type", "float"));

    private static string N(double valor) => valor.ToString("R", CultureInfo.InvariantCulture);
}
