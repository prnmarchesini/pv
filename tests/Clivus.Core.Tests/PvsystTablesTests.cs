using System.Globalization;
using System.Xml.Linq;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O PVsyst por mesa (6.5, Renan, 31/10/2026: "é preciso exportar como mesa"):
/// as faces dos módulos de cada mesa viram uma fiada só, o retângulo exato
/// dos módulos com a medida do módulo e os vãos, como o PVcase exporta; e o
/// arquivo tem a forma do dele.
/// </summary>
public class PvsystTablesTests
{
    private static readonly XNamespace Ns = PvColladaWriter.Ns;

    /// <summary>
    /// As faces de uma mesa como o desenho as grava: o começo da borda baixa
    /// em <paramref name="inicio"/>, colunas pelo eixo da mesa (com o giro
    /// longitudinal), fileiras subindo a inclinação, cada face na ordem
    /// começo e fim da borda baixa, fim e começo da borda alta.
    /// </summary>
    private static List<PvModuleFace> Mesa(
        int colunas, int fileiras, double largura = 1.134, double altura = 2.456, double vaoX = 0.02, double vaoY = 0.02,
        double inclinacao = 15, double azimute = 20, double giro = 3, Point3? inicio = null)
    {
        var p0 = inicio ?? new Point3(314018.25, 7456163.5, 718.4);
        var az = azimute * Math.PI / 180;
        var g = giro * Math.PI / 180;
        var t = inclinacao * Math.PI / 180;

        // u ao longo da mesa (com o giro), v subindo a inclinação, perpendicular a u.
        var u = (X: Math.Cos(az) * Math.Cos(g), Y: Math.Sin(az) * Math.Cos(g), Z: Math.Sin(g));
        var horizontal = (X: -Math.Sin(az), Y: Math.Cos(az), Z: 0.0);
        var v = (X: horizontal.X * Math.Cos(t), Y: horizontal.Y * Math.Cos(t), Z: Math.Sin(t));
        var vu = v.X * u.X + v.Y * u.Y + v.Z * u.Z;
        v = (v.X - vu * u.X, v.Y - vu * u.Y, v.Z - vu * u.Z);
        var tam = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        v = (v.X / tam, v.Y / tam, v.Z / tam);

        Point3 Em(double s, double r) => new(p0.X + s * u.X + r * v.X, p0.Y + s * u.Y + r * v.Y, p0.Z + s * u.Z + r * v.Z);

        var faces = new List<PvModuleFace>();
        for (var c = 0; c < colunas; c++)
            for (var f = 0; f < fileiras; f++)
            {
                var s0 = c * (largura + vaoX);
                var r0 = f * (altura + vaoY);
                faces.Add(new PvModuleFace(c, f, [Em(s0, r0), Em(s0 + largura, r0), Em(s0 + largura, r0 + altura), Em(s0, r0 + altura)]));
            }

        return faces;
    }

    private static PvModuleFace Face(List<PvModuleFace> faces, int coluna, int fileira) =>
        faces.Single(f => f.Column == coluna && f.Row == fileira);

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static void Perto(Point3 esperado, Point3 obtido, double folga = 1e-6) =>
        Assert.True(Distancia(esperado, obtido) <= folga, $"esperava {esperado}, veio {obtido}");

    /// <summary>A mesa 2P14 inteira é uma fiada: 14 × 2, a medida do módulo, os vãos, e os cantos são os dos módulos das pontas.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void AMesaInteiraEUmaFiadaComOsModulosDentro()
    {
        var faces = Mesa(14, 2);
        var r = PvFrames.FromFaces(Guid.NewGuid(), "A-12", faces, 0.03);

        Assert.Null(r.Problem);
        Assert.False(r.OffGrid);
        var fiada = Assert.Single(r.Frames);
        Assert.Equal("A-12", fiada.Name);
        Assert.Equal((14, 2, 28), (fiada.Columns, fiada.Rows, fiada.ModuleCount));
        Assert.Equal((1.134, 2.456, 0.02, 0.02, 0.03), (fiada.ModuleWidth, fiada.ModuleHeight, fiada.SpacingX, fiada.SpacingY, fiada.Thickness));

        // A ordem do PVcase: começo e fim da borda baixa, fim e começo da borda alta.
        Perto(Face(faces, 0, 0).Corners[0], fiada.Corners[0]);
        Perto(Face(faces, 13, 0).Corners[1], fiada.Corners[1]);
        Perto(Face(faces, 13, 1).Corners[2], fiada.Corners[2]);
        Perto(Face(faces, 0, 1).Corners[3], fiada.Corners[3]);
    }

    /// <summary>
    /// O lado da fiada é colunas × largura + vãos e fileiras × altura + vão,
    /// como nas mesas do PVcase do Renan: Porto Feliz 12 × 2 (15,856 × 4,788),
    /// Mairinque 26 × 2 com vão de 0,021 (34,403) e 39 × 2 com 0,018 (51,501).
    /// </summary>
    [Theory]
    [Trait("Etapa", "6")]
    [InlineData(12, 0.020, 15.856)]
    [InlineData(26, 0.021, 34.403)]
    [InlineData(39, 0.018, 51.501)]
    public void OLadoFechaComOsModulosComoNoPVcase(int colunas, double vao, double lado)
    {
        var faces = Mesa(colunas, 2, largura: 1.303, altura: 2.384, vaoX: vao, vaoY: 0.02);
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "M", faces, 0.035).Frames);

        Assert.Equal((1.303, 2.384, vao, 0.02), (fiada.ModuleWidth, fiada.ModuleHeight, fiada.SpacingX, fiada.SpacingY));
        Assert.Equal(lado, Distancia(fiada.Corners[0], fiada.Corners[1]), 6);
        Assert.Equal(4.788, Distancia(fiada.Corners[0], fiada.Corners[3]), 6);
        Assert.Equal(colunas * 2, fiada.ModuleCount);
    }

    /// <summary>Sem o módulo da ponta de cima, a mesa vai em dois retângulos: 13 × 2 e o 1 × 1 que sobrou; nenhum módulo a mais.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void SemOModuloDaPontaVaiEmDoisPedacos()
    {
        var faces = Mesa(14, 2);
        faces.Remove(Face(faces, 13, 1));

        var r = PvFrames.FromFaces(Guid.NewGuid(), "B-3", faces, 0.03);

        Assert.False(r.OffGrid);
        Assert.Equal(2, r.Frames.Count);
        Assert.Equal((13, 2), (r.Frames[0].Columns, r.Frames[0].Rows));
        Assert.Equal((1, 1), (r.Frames[1].Columns, r.Frames[1].Rows));
        Assert.Equal(27, r.Frames.Sum(f => f.ModuleCount));
        Assert.Equal(["B-3 #1", "B-3 #2"], r.Frames.Select(f => f.Name));
        Perto(Face(faces, 13, 0).Corners[0], r.Frames[1].Corners[0]);
        Perto(Face(faces, 13, 0).Corners[2], r.Frames[1].Corners[2]);
    }

    /// <summary>Sem um módulo do meio, três pedaços: as colunas de antes, a coluna dele (só a outra fileira) e as de depois.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void SemUmModuloDoMeioVaiEmTresPedacos()
    {
        var faces = Mesa(14, 2);
        faces.Remove(Face(faces, 5, 0));

        var r = PvFrames.FromFaces(Guid.NewGuid(), "C-1", faces, 0.03);

        Assert.Equal([(5, 2), (1, 1), (8, 2)], r.Frames.Select(f => (f.Columns, f.Rows)));
        Assert.Equal(27, r.Frames.Sum(f => f.ModuleCount));
        Perto(Face(faces, 5, 1).Corners[0], r.Frames[1].Corners[0]);
    }

    /// <summary>Um módulo arrastado 5 cm tira a mesa da grade: ela vai módulo a módulo, cada um no lugar dele.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void ModuloForaDoLugarLevaAMesaModuloAModulo()
    {
        var faces = Mesa(14, 2);
        var arrastada = Face(faces, 7, 1);
        faces[faces.IndexOf(arrastada)] = arrastada with { Corners = arrastada.Corners.Select(p => new Point3(p.X + 0.05, p.Y, p.Z)).ToList() };

        var r = PvFrames.FromFaces(Guid.NewGuid(), "D-2", faces, 0.03);

        Assert.True(r.OffGrid);
        Assert.Equal(28, r.Frames.Count);
        Assert.All(r.Frames, f => Assert.Equal((1, 1), (f.Columns, f.Rows)));

        // A arrastada vai onde ficou, inclusive fora do plano da mesa.
        var movida = Face(faces, 7, 1);
        Assert.Contains(r.Frames, f => Distancia(f.Corners[0], movida.Corners[0]) < 1e-6 && Distancia(f.Corners[2], movida.Corners[2]) < 1e-6);
    }

    /// <summary>
    /// Medidas com décimo de milímetro vão ao milímetro, e o retângulo é
    /// refeito com elas: colunas × largura + vãos fecha com o lado, e o
    /// PVsyst conta os mesmos 14 módulos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void MedidaQuebradaVaiAoMilimetroEOLadoFecha()
    {
        var faces = Mesa(14, 2, largura: 1.1344, altura: 2.4561, vaoX: 0.0206, vaoY: 0.0198);
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "E", faces, 0.03).Frames);

        Assert.Equal((1.134, 2.456, 0.021, 0.02), (fiada.ModuleWidth, fiada.ModuleHeight, fiada.SpacingX, fiada.SpacingY));
        Assert.Equal(14 * 1.134 + 13 * 0.021, Distancia(fiada.Corners[0], fiada.Corners[1]), 9);
        Assert.Equal(2 * 2.456 + 0.02, Distancia(fiada.Corners[0], fiada.Corners[3]), 9);
    }

    /// <summary>A mesa 1V (uma fileira) leva o vão das colunas também no outro sentido, como o PVcase escreve os dois.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void UmaFileiraSoUsaOVaoDasColunasNosDois()
    {
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "F", Mesa(28, 1, vaoX: 0.025), 0.03).Frames);

        Assert.Equal((28, 1, 0.025, 0.025), (fiada.Columns, fiada.Rows, fiada.SpacingX, fiada.SpacingY));
    }

    /// <summary>Uma mesa de um módulo só vai com o vão padrão do PVsyst.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void UmModuloSoVaiComOVaoPadrao()
    {
        var faces = Mesa(1, 1);
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "G", faces, double.NaN).Frames);

        Assert.Equal((1, 1, PvFrames.DefaultSpacing, PvFrames.DefaultSpacing, PvFrames.DefaultThickness),
            (fiada.Columns, fiada.Rows, fiada.SpacingX, fiada.SpacingY, fiada.Thickness));
        Perto(faces[0].Corners[0], fiada.Corners[0]);
        Perto(faces[0].Corners[2], fiada.Corners[2]);
    }

    /// <summary>Módulo deitado (paisagem): a largura é o lado menor e a altura o maior, como no exemplo do PVsyst.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void PaisagemLevaOLadoMenorNaLargura()
    {
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "H", Mesa(5, 3, largura: 2.278, altura: 1.134), 0.03).Frames);

        Assert.Equal((5, 3, 1.134, 2.278), (fiada.Columns, fiada.Rows, fiada.ModuleWidth, fiada.ModuleHeight));
        Assert.Equal(5 * 2.278 + 4 * 0.02, Distancia(fiada.Corners[0], fiada.Corners[1]), 6);
    }

    /// <summary>Os eixos vêm das colunas e das fileiras: a ordem dos cantos de cada face não muda a fiada.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void AOrdemDosCantosDaFaceNaoMudaAFiada()
    {
        var faces = Mesa(14, 2);
        var giradas = faces.Select(f => f with { Corners = [f.Corners[2], f.Corners[1], f.Corners[0], f.Corners[3]] }).ToList();

        var a = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "I", faces, 0.03).Frames);
        var b = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "I", giradas, 0.03).Frames);

        for (var k = 0; k < 4; k++) Perto(a.Corners[k], b.Corners[k]);
    }

    /// <summary>Mesa sem face, face sem área e mesa de pé ficam de fora, com o motivo.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void MesaSemFaceSemAreaOuDePeFicaDeFora()
    {
        Assert.NotNull(PvFrames.FromFaces(Guid.NewGuid(), "J", [], 0.03).Problem);

        var p = new Point3(10, 20, 30);
        Assert.NotNull(PvFrames.FromFaces(Guid.NewGuid(), "J", [new PvModuleFace(0, 0, [p, p, p, p])], 0.03).Problem);

        var dePe = new PvModuleFace(0, 0, [new Point3(0, 0, 0), new Point3(1, 0, 0), new Point3(1, 0, 2), new Point3(0, 0, 2)]);
        var r = PvFrames.FromFaces(Guid.NewGuid(), "J", [dePe], 0.03);
        Assert.NotNull(r.Problem);
        Assert.Empty(r.Frames);
    }

    private static XDocument Escrever(IReadOnlyList<PvFrame> fiadas, Point3? origem = null) =>
        PvColladaWriter.Write(fiadas, new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc), "Renan", origem ?? PvColladaWriter.LocalOrigin(fiadas));

    /// <summary>
    /// O arquivo tem a forma do do PVcase: material "Frames", oito vértices e
    /// os triângulos "3 1 2 1 3 0" em cada mesa, o frame_parameters com as
    /// seis medidas na ordem dele e o "generic", sem up_axis, e o nó raiz com
    /// a matriz de Y para cima.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OArquivoTemAFormaDoPVcase()
    {
        var mesa = Guid.NewGuid();
        var fiadas = PvFrames.FromFaces(mesa, "A-1", Mesa(14, 2), 0.035).Frames;
        var lido = XDocument.Parse(Escrever(fiadas).ToString());

        Assert.Equal("1.4.1", lido.Root!.Attribute("version")!.Value);
        Assert.Null(lido.Root.Element(Ns + "asset")!.Element(Ns + "up_axis"));
        Assert.Equal("1", lido.Root.Element(Ns + "asset")!.Element(Ns + "unit")!.Attribute("meter")!.Value);
        Assert.Equal(PvColladaWriter.FramesMaterial, Assert.Single(lido.Descendants(Ns + "material")).Attribute("name")!.Value);

        var geometria = Assert.Single(lido.Descendants(Ns + "geometry"));
        Assert.Equal(PvColladaWriter.GeometryId(mesa, 0), geometria.Attribute("id")!.Value);
        Assert.Equal("A-1", geometria.Attribute("name")!.Value);
        Assert.Equal("24", geometria.Descendants(Ns + "float_array").Single().Attribute("count")!.Value);
        Assert.Equal(24, geometria.Descendants(Ns + "float_array").Single().Value.Split(' ').Length);
        Assert.Equal(PvColladaWriter.Triangles, geometria.Descendants(Ns + "p").Single().Value);

        var parametros = geometria.Descendants(Ns + "frame_parameters").Single().Elements().Select(e => (e.Name.LocalName, e.Value)).ToList();
        Assert.Equal(
            [("module_width", "1.134"), ("module_height", "2.456"), ("module_x_spacing", "0.02"), ("module_y_spacing", "0.02"),
             ("module_manufacturer", "generic"), ("module_name", "generic")],
            parametros);

        // O frame_parameters vem depois dos triângulos, dentro da malha, como no PVcase.
        Assert.Equal(["source", "vertices", "triangles", "frame_parameters"], geometria.Element(Ns + "mesh")!.Elements().Select(e => e.Name.LocalName));

        var raiz = lido.Descendants(Ns + "visual_scene").Single().Element(Ns + "node")!;
        Assert.Equal(PvColladaWriter.RootMatrix, raiz.Element(Ns + "matrix")!.Value);
        Assert.Equal("#" + PvColladaWriter.GeometryId(mesa, 0), raiz.Descendants(Ns + "instance_geometry").Single().Attribute("url")!.Value);
    }

    /// <summary>Os cantos voltam do arquivo pela matriz do nó raiz e pela origem do cabeçalho, ao micrômetro.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OsCantosVoltamPelaMatrizEPelaOrigem()
    {
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "A-1", Mesa(14, 2), 0.035).Frames);
        var doc = Escrever([fiada]);

        var origem = ColladaWriter.ParseOriginComment(doc.Descendants(Ns + "comments").Single().Value)!.Value;
        Assert.Equal(PvColladaWriter.LocalOrigin([fiada]), origem);

        var n = doc.Descendants(Ns + "float_array").Single().Value.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

        for (var k = 0; k < 4; k++)
        {
            // A matriz do PVcase: x = x, y = −z, z = y.
            var cena = new Point3(n[3 * k] + origem.X, -n[3 * k + 2] + origem.Y, n[3 * k + 1] + origem.Z);
            Perto(fiada.Corners[k], cena, 2e-6);
        }
    }

    /// <summary>O fundo da caixa fica a espessura do módulo abaixo da face de cima, pela normal que sobe.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OFundoFicaAEspessuraAbaixo()
    {
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "A-1", Mesa(14, 2), 0.035).Frames);
        var n = Escrever([fiada]).Descendants(Ns + "float_array").Single().Value.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

        for (var k = 0; k < 4; k++)
        {
            var cima = new Point3(n[3 * k], n[3 * k + 1], n[3 * k + 2]);
            var fundo = new Point3(n[3 * (k + 4)], n[3 * (k + 4) + 1], n[3 * (k + 4) + 2]);
            Assert.Equal(0.035, Distancia(cima, fundo), 5);

            // Y é o "cima" no arquivo.
            Assert.True(fundo.Y < cima.Y);
        }
    }

    /// <summary>Uma geometria por fiada; os pedaços da mesma mesa levam o GUID dela com "-2", "-3".</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void UmaGeometriaPorFiadaComOGuidDaMesa()
    {
        var mesa = Guid.NewGuid();
        var faces = Mesa(14, 2);
        faces.Remove(Face(faces, 5, 0));
        var outra = Guid.NewGuid();
        var fiadas = PvFrames.FromFaces(mesa, "K", faces, 0.03).Frames.Concat(PvFrames.FromFaces(outra, "L", Mesa(14, 2, inicio: new Point3(314040, 7456163.5, 719)), 0.03).Frames).ToList();

        var ids = Escrever(fiadas).Descendants(Ns + "geometry").Select(g => g.Attribute("id")!.Value).ToList();

        Assert.Equal([$"mesa-{mesa:N}", $"mesa-{mesa:N}-2", $"mesa-{mesa:N}-3", $"mesa-{outra:N}"], ids);
    }

    /// <summary>Os números vão com ponto mesmo com o Windows em português.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void NumerosComPontoEmQualquerCultura()
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "M", Mesa(14, 2), 0.035).Frames);
            var texto = Escrever([fiada]).ToString();

            Assert.Contains("<module_width>1.134</module_width>", texto);
            Assert.DoesNotContain(",", Escrever([fiada]).Descendants(Ns + "float_array").Single().Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    /// <summary>Sem fiada, com medida impossível ou origem não finita, o escritor recusa.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void RecusaSemFiadaOuComMedidaImpossivel()
    {
        var fiada = Assert.Single(PvFrames.FromFaces(Guid.NewGuid(), "N", Mesa(14, 2), 0.035).Frames);

        Assert.Throws<ArgumentException>(() => Escrever([], new Point3(0, 0, 0)));
        Assert.Throws<ArgumentException>(() => Escrever([fiada with { ModuleWidth = 0 }]));
        Assert.Throws<ArgumentException>(() => Escrever([fiada with { SpacingX = -0.01 }]));
        Assert.Throws<ArgumentException>(() => Escrever([fiada with { Columns = 0 }]));
        Assert.Throws<ArgumentException>(() => Escrever([fiada], new Point3(double.NaN, 0, 0)));
        Assert.Throws<ArgumentException>(() => Escrever([fiada with { Corners = [fiada.Corners[0], fiada.Corners[0], fiada.Corners[0], fiada.Corners[0]] }]));
    }
}
