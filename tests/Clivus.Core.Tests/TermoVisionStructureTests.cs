using System.Text.Json;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O suporte das mesas fixas no 3D do TermoVision (33.10, 02/11/2026; Renan:
/// "leve a estrutura tbm, fica mais profissional"): os pilares do terreno até
/// o topo e as barras do biposte, em triângulos na cor do aço, sem os módulos.
/// </summary>
public class TermoVisionStructureTests
{
    private static readonly Guid MesaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static StructureIdentity Barra(StructurePart parte, Point3 inicio, Point3 fim) =>
        new(Guid.NewGuid(), MesaId, parte, PillarLine.Front, 0, 1, null, inicio, fim, 0.1, 0.05, new Point3(0, 0, 1));

    // Um pórtico do biposte: os dois pilares e uma barra de cada tipo.
    private static readonly TermoVisionStructure.Pillar[] Pilares =
    [
        new(new Point3(1000, 2000, 51.2), 50.0, 0.15, 0.10, 0),
        new(new Point3(1000, 2003, 52.4), 50.5, 0.15, 0.10, 0),
    ];

    private static readonly StructureIdentity[] Barras =
    [
        Barra(StructurePart.Beam, new Point3(1000, 1999.5, 51.1), new Point3(1000, 2003.5, 52.5)),
        Barra(StructurePart.Brace, new Point3(1000, 2000, 50.8), new Point3(1000, 2001, 51.4)),
        Barra(StructurePart.Purlin, new Point3(995, 2001, 51.8), new Point3(1005, 2001, 51.8)),
        Barra(StructurePart.Rod, new Point3(995, 2000, 50.6), new Point3(1005, 2000, 51.0)),
    ];

    private static IEnumerable<Point3> Vertices(IEnumerable<Scene3DFace> faces) => faces.SelectMany(f => f.Corners);

    [Fact]
    [Trait("Etapa", "33")]
    public void OPilarVaiDoTerrenoAteOTopoComASecaoDoBloco()
    {
        var faces = TermoVisionStructure.Table([Pilares[0]], []);

        // Um prisma sem as tampas: 8 triângulos, todos na cor do aço.
        Assert.Equal(8, faces.Count);
        Assert.All(faces, f => Assert.Equal(CarportMesh.CorDoAco, f.Color));

        // Do pé um pouco abaixo do terreno (a grade de 2 m pode passar abaixo do TIN entre os nós) até o topo do bloco.
        var v = Vertices(faces).ToList();
        Assert.Equal(50.0 - TermoVisionStructure.BelowGround, v.Min(p => p.Z), 9);
        Assert.Equal(51.2, v.Max(p => p.Z), 9);

        // Giro 0: a largura (0,15) ao longo de X, a profundidade (0,10) ao longo de Y, centradas no bloco.
        Assert.Equal(1000 - 0.075, v.Min(p => p.X), 9);
        Assert.Equal(1000 + 0.075, v.Max(p => p.X), 9);
        Assert.Equal(2000 - 0.05, v.Min(p => p.Y), 9);
        Assert.Equal(2000 + 0.05, v.Max(p => p.Y), 9);

        // Girado 90°: a largura passa para Y.
        var girado = Vertices(TermoVisionStructure.Table([Pilares[0] with { Rotation = Math.PI / 2 }], [])).ToList();
        Assert.Equal(0.15, girado.Max(p => p.Y) - girado.Min(p => p.Y), 9);
        Assert.Equal(0.10, girado.Max(p => p.X) - girado.Min(p => p.X), 9);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void OPeDoPilarVaiAbaixoDoNoMaisBaixoDaCelulaDaGrade()
    {
        // Grade 3 × 2 de 2 m: na célula de baixo à esquerda os nós são 50, 51 (sul) e 49, 52 (norte).
        var grade = new Scene3DTerrain(0, 0, 2, 3, 2, [50, 51, 53, 49, 52, null]);

        Assert.Equal(49, TermoVisionStructure.LowestNodeAround(grade, 1, 1));
        Assert.Equal(51, TermoVisionStructure.LowestNodeAround(grade, 3, 1));   // a célula da direita: 51, 53, 52 e um nó sem cota
        Assert.Equal(53, TermoVisionStructure.LowestNodeAround(grade, 4, -0.5)); // na borda de baixo à direita só o nó (2, 0)
        Assert.Null(TermoVisionStructure.LowestNodeAround(grade, -10, -10));
        Assert.Null(TermoVisionStructure.LowestNodeAround(grade, double.NaN, 1));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void ODetalheTiraOsTirantesDepoisAsTercasDepoisAsBarras()
    {
        // 2 pilares e 4 barras, 8 triângulos cada.
        Assert.Equal(48, TermoVisionStructure.Table(Pilares, Barras, TermoVisionStructure.Detail.Full).Count);
        Assert.Equal(40, TermoVisionStructure.Table(Pilares, Barras, TermoVisionStructure.Detail.NoRods).Count);
        Assert.Equal(32, TermoVisionStructure.Table(Pilares, Barras, TermoVisionStructure.Detail.NoPurlins).Count);
        Assert.Equal(16, TermoVisionStructure.Table(Pilares, Barras, TermoVisionStructure.Detail.PillarsOnly).Count);

        // A monoposte: só os pilares, em qualquer detalhe.
        Assert.Equal(16, TermoVisionStructure.Table(Pilares, [], TermoVisionStructure.Detail.Full).Count);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void AMesaQuePassaDoLimiteDesceDeDetalheSozinha()
    {
        // 700 tirantes (5600 triângulos) passam dos 5000 da mesa: ela vai sem os tirantes.
        var tirantes = Enumerable.Range(0, 700).Select(i => Barra(StructurePart.Rod, new Point3(i, 0, 1), new Point3(i, 1, 1))).ToList();
        var faces = TermoVisionStructure.Table(Pilares, [.. Barras, .. tirantes]);
        Assert.Equal(40, faces.Count);

        // Pilares demais: só eles, cortados no limite.
        var pilares = Enumerable.Range(0, 700).Select(i => Pilares[0] with { Top = new Point3(i, 0, 51) }).ToList();
        Assert.Equal(TermoVisionStructure.MaxTrianglesPerTable, TermoVisionStructure.Table(pilares, []).Count);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void OEnvioFicaComOPrimeiroDetalheQueCabe()
    {
        var medidos = new List<TermoVisionStructure.Detail>();
        (int, long) Medir(TermoVisionStructure.Detail d)
        {
            medidos.Add(d);
            return d switch
            {
                TermoVisionStructure.Detail.Full => (450_000, 9_000_000),         // passa dos triângulos
                TermoVisionStructure.Detail.NoRods => (300_000, 19_000_000),      // passa dos bytes
                _ => (200_000, 12_000_000),
            };
        }

        Assert.Equal(TermoVisionStructure.Detail.NoPurlins, TermoVisionStructure.Fit(Medir));
        Assert.Equal([TermoVisionStructure.Detail.Full, TermoVisionStructure.Detail.NoRods, TermoVisionStructure.Detail.NoPurlins], medidos);

        // Nem só os pilares cabem: vai sem a estrutura.
        Assert.Null(TermoVisionStructure.Fit(_ => (500_000, 1)));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void PilarEBarraQueNaoServemFicamDeFora()
    {
        TermoVisionStructure.Pillar[] ruins =
        [
            Pilares[0] with { GroundZ = double.NaN },
            Pilares[0] with { Width = 0 },
            Pilares[0] with { GroundZ = 60 },   // o topo abaixo do terreno
        ];
        StructureIdentity[] tortas =
        [
            Barra(StructurePart.Beam, new Point3(0, 0, 1), new Point3(0, 0, 1)),        // sem comprimento
            Barra(StructurePart.Beam, new Point3(double.NaN, 0, 1), new Point3(1, 0, 1)),
        ];

        Assert.Empty(TermoVisionStructure.Table(ruins, tortas));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void AMesaComEstruturaLevaOsTriangulosComACorNoCorpo()
    {
        var mesa = new TermoVisionTable(MesaId, "F1.1",
            [(1000, 1999.5, 51.1), (1010, 1999.5, 51.1), (1010, 2003.5, 52.5), (1000, 2003.5, 52.5)],
            14, 2, new TermoVisionModule(1.134, 2.384, 0.02))
        {
            Subtype = TermoVisionTable.SubtipoBiposte,
            Structure = TermoVisionStructure.Table(Pilares, Barras),
        };

        var corpo = JsonDocument.Parse(TermoVisionProject.Build("0.1.14", "x.dwg", "x", Guid.NewGuid(), TermoVisionProjectCrs.Of(8.5, -80.0),
            new Scene3DTerrain(990, 1990, 20, 2, 2, [50, 50, 51, 51]), [mesa], out _)).RootElement;

        var estrutura = corpo.GetProperty("mesas")[0].GetProperty("estrutura");
        Assert.Equal("biposte", estrutura.GetProperty("tipo").GetString());
        var triangulos = estrutura.GetProperty("triangulos");
        Assert.Equal(48, triangulos.GetArrayLength());

        // [e1, n1, z1, e2, n2, z2, e3, n3, z3, cor], a cor em decimal (0xRRGGBB).
        var primeiro = triangulos[0];
        Assert.Equal(10, primeiro.GetArrayLength());
        var aco = CarportMesh.CorDoAco;
        Assert.Equal((aco.R << 16) | (aco.G << 8) | aco.B, primeiro[9].GetInt32());
    }
}
