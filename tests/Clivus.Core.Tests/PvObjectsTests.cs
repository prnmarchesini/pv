using System.Globalization;
using System.Xml.Linq;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Os objetos que vão para o PVsyst junto com as mesas (42.8, Renan,
/// 31/10/2026: "quero exportar todos objetos"): a árvore como os dois
/// cilindros da sombra, o equipamento como a caixa do PVcase, as mesas que se
/// cruzam (a F16.4 e a F16.5 do Aurora) e o terreno no CSV.
/// </summary>
public class PvObjectsTests
{
    private static readonly XNamespace Ns = PvColladaWriter.Ns;

    private static PvFrame Fiada(Guid mesa, string nome, double x0, double comprimento = 18.502, double fundo = 4.788, double z = 100) => new(
        mesa, nome,
        [new Point3(x0, 0, z), new Point3(x0 + comprimento, 0, z), new Point3(x0 + comprimento, fundo, z + 1), new Point3(x0, fundo, z + 1)],
        14, 2, 1.303, 2.384, 0.02, 0.02, 0.035);

    /// <summary>A árvore são dois cilindros de 12 lados: o tronco do chão até a altura dele, a copa de lá até o topo, cada um na largura dele.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void AArvoreEOTroncoEACopaComoNaSombra()
    {
        var id = Guid.NewGuid();
        var spec = new TreeSpec(3, 0.4, 5, 6);
        var partes = PvObjects.Tree(id, "Árvore", 1000, 2000, 50, spec);

        Assert.Equal([PvObjectKind.TreeTrunk, PvObjectKind.TreeCrown], partes.Select(p => p.Kind));
        Assert.Equal([$"arvore-{id:N}-tronco", $"arvore-{id:N}-copa"], partes.Select(p => p.Id));

        void Confere(PvObject o, double raio, double baixo, double alto)
        {
            Assert.Equal(2 * PvObjects.CylinderSides, o.Vertices.Count);
            Assert.All(o.Vertices, v => Assert.Equal(raio, Math.Sqrt((v.X - 1000) * (v.X - 1000) + (v.Y - 2000) * (v.Y - 2000)), 9));
            Assert.Equal(baixo, o.Vertices.Min(v => v.Z), 9);
            Assert.Equal(alto, o.Vertices.Max(v => v.Z), 9);
            Assert.Equal(3 * (2 * PvObjects.CylinderSides + 2 * (PvObjects.CylinderSides - 2)), o.Triangles.Count);
            Assert.All(o.Triangles, i => Assert.InRange(i, 0, o.Vertices.Count - 1));
        }

        Confere(partes[0], 0.2, 50, 53);
        Confere(partes[1], 3, 53, 58);

        // A segunda árvore de mesmo GUID (copiada com o Clivus fechado) tem o id dela.
        Assert.Equal([$"arvore-{id:N}-2-tronco", $"arvore-{id:N}-2-copa"], PvObjects.Tree(id, "Árvore", 1010, 2000, 50, spec, copy: 2).Select(p => p.Id));
    }

    /// <summary>A caixa do equipamento tem os oito cantos e os doze triângulos na ordem das caixas do PVcase.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void AEstacaoEAcaixaDoPVcase()
    {
        Point3[] baixo = [new(0, 0, 10), new(2, 0, 10), new(2, 3, 10), new(0, 3, 10)];
        Point3[] alto = [.. baixo.Select(p => p with { Z = 12.5 })];

        var caixa = PvObjects.Box("equipamento-1", "INV 1", PvObjectKind.Station, baixo, alto);

        Assert.Equal([.. baixo, .. alto], caixa.Vertices);
        Assert.Equal("0 1 2 0 2 3 0 1 5 0 4 5 1 2 6 1 5 6 0 3 7 0 4 7 2 3 7 2 6 7 4 5 6 4 6 7", string.Join(" ", caixa.Triangles));
        Assert.Throws<ArgumentException>(() => PvObjects.Box("x", "x", PvObjectKind.Station, baixo[..3], alto));
    }

    /// <summary>
    /// Duas mesas da mesma fileira, a segunda começando 21 cm antes de a
    /// primeira acabar (a F16.4 e a F16.5 do Aurora): cruzam, com a área do
    /// pedaço em comum; com 2 cm de vão, não; pedaços da mesma mesa, nunca.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void AsMesasQueSeCruzamSaoAchadas()
    {
        var a = Fiada(Guid.NewGuid(), "F16.4", 434117.09);
        var b = Fiada(Guid.NewGuid(), "F16.5", 434117.09 + 18.502 - 0.21);
        var c = Fiada(Guid.NewGuid(), "F16.6", 434117.09 + 2 * 18.502 - 0.21 + 0.02);

        var cruzadas = PvObjects.Crossings([a, b, c]);

        var cruzada = Assert.Single(cruzadas);
        Assert.Equal(("F16.4", "F16.5"), (cruzada.A.Name, cruzada.B.Name));
        Assert.Equal(0.21 * 4.788, cruzada.Area, 3);

        var mesa = Guid.NewGuid();
        Assert.Empty(PvObjects.Crossings([Fiada(mesa, "G #1", 0), Fiada(mesa, "G #2", 10)]));
    }

    /// <summary>
    /// Duas fiadas giradas 33°, uma anti-horária e a outra horária (a mesa
    /// espelhada), a segunda entrando 0,5 m na primeira: a área é a do
    /// retângulo em comum, qualquer que seja o sentido dos cantos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void MesasGiradasEEmSentidosOpostosSeCruzamPelaArea()
    {
        var a = Math.PI * 33 / 180;
        Point3 Giro(double s, double t) => new(434000 + s * Math.Cos(a) - t * Math.Sin(a), 907000 + s * Math.Sin(a) + t * Math.Cos(a), 100);
        PvFrame Retangulo(Guid id, string nome, double s0, bool horario)
        {
            Point3[] c = [Giro(s0, 0), Giro(s0 + 18.502, 0), Giro(s0 + 18.502, 4.788), Giro(s0, 4.788)];
            return new PvFrame(id, nome, horario ? [c[3], c[2], c[1], c[0]] : c, 14, 2, 1.303, 2.384, 0.02, 0.02, 0.035);
        }

        var cruzada = Assert.Single(PvObjects.Crossings([Retangulo(Guid.NewGuid(), "A", 0, false), Retangulo(Guid.NewGuid(), "B", 18.002, true)]));
        Assert.Equal(0.5 * 4.788, cruzada.Area, 6);
    }

    /// <summary>
    /// O terreno vai numa grade triangular de 5 m, como o "_terrain.csv" do
    /// PVcase: 5 m numa linha, as linhas a 5·√3/2, cada uma deslocada meio
    /// passo, presa aos múltiplos do passo; só onde há terreno, até 100 m das
    /// mesas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OTerrenoVaiNumaGradeTriangularDe5m()
    {
        var caixa = (MinX: 1000.0, MinY: 2000.0, MaxX: 1050.0, MaxY: 2020.0);
        double? Cota(double x, double y) => x < 1060 ? 100 + 0.01 * x : null;

        var (pontos, passo) = PvObjects.TerrainGrid(Cota, caixa);

        Assert.Equal(PvObjects.TerrainSpacing, passo);
        Assert.All(pontos, p => Assert.InRange(p.X, 900, 1060));
        Assert.All(pontos, p => Assert.InRange(p.Y, 1900, 2120));
        Assert.All(pontos, p => Assert.Equal(100 + 0.01 * p.X, p.Z, 9));

        var linhas = pontos.GroupBy(p => Math.Round(p.Y / (5 * Math.Sqrt(3) / 2))).OrderBy(g => g.Key).ToList();
        Assert.Equal(5 * Math.Sqrt(3) / 2, linhas[1].First().Y - linhas[0].First().Y, 9);
        foreach (var linha in linhas)
        {
            var xs = linha.Select(p => p.X).OrderBy(x => x).ToList();
            Assert.All(xs.Zip(xs.Skip(1)), par => Assert.Equal(5, par.Second - par.First, 9));
            var meio = (long)linha.Key % 2 == 0 ? 0 : 2.5;
            Assert.All(xs, x => Assert.Equal(0, Math.Abs((x - meio) % 5), 9));
        }

        // Uma caixa enorme abre a grade para caber no teto.
        var (muitos, largo) = PvObjects.TerrainGrid((_, _) => 0, (0, 0, 20000, 20000), 0);
        Assert.True(largo > 5 && muitos.Count <= PvObjects.MaxTerrainPoints * 1.05, $"passo {largo}, {muitos.Count} pontos");
    }

    /// <summary>O CSV do terreno: uma linha "X Y Z" por ponto, em metro, relativa à origem do PVC, com ponto mesmo em pt-BR.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OCsvDoTerrenoEXYZRelativoAOrigem()
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            var csv = PvObjects.TerrainCsv([new Point3(434030.5, 907540.25, 96.125), new Point3(434027, 907539, 95)], new Point3(434026, 907539, 95));

            Assert.Equal("4.5 1.25 1.125\r\n1 0 0\r\n", csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    /// <summary>
    /// O arquivo com objetos: as mesas no Frames e cada objeto no material do
    /// PVcase (Material1 Tree_trunk, Material2 Tree_crown, Material4 Station),
    /// só os que aparecem; os triângulos e os vértices de cada um; a origem
    /// pega os objetos também.
    /// </summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void OArquivoLevaOsObjetosNosMateriaisDoPVcase()
    {
        var fiada = Fiada(Guid.NewGuid(), "A-1", 434117.09);
        var objetos = PvObjects.Tree(Guid.NewGuid(), "Árvore", 434100, -30, 95.5, TreeSpec.Default).ToList();
        objetos.Add(PvObjects.Box("equipamento-Inverter-1-1", "INV 1", PvObjectKind.Station,
            [new(434140, -5, 99), new(434142, -5, 99), new(434142, -2, 99), new(434140, -2, 99)],
            [new(434140, -5, 101), new(434142, -5, 101), new(434142, -2, 101), new(434140, -2, 101)]));

        var origem = PvColladaWriter.LocalOrigin([fiada], objetos);
        Assert.Equal(new Point3(434100 - 3, -33, 95), origem);

        var doc = PvColladaWriter.Write([fiada], objetos, new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc), "Renan", origem);

        Assert.Equal([("Material0", "Frames"), ("Material1", "Tree_trunk"), ("Material2", "Tree_crown"), ("Material4", "Station")],
            doc.Descendants(Ns + "material").Select(m => (m.Attribute("id")!.Value, m.Attribute("name")!.Value)));
        Assert.Equal(4, doc.Descendants(Ns + "geometry").Count());
        Assert.Equal(4, doc.Descendants(Ns + "instance_geometry").Count());

        var estacao = doc.Descendants(Ns + "geometry").Single(g => g.Attribute("id")!.Value == "equipamento-Inverter-1-1");
        Assert.Equal("Material4", estacao.Descendants(Ns + "triangles").Single().Attribute("material")!.Value);
        Assert.Equal("12", estacao.Descendants(Ns + "triangles").Single().Attribute("count")!.Value);
        Assert.Equal(string.Join(" ", PvObjects.BoxTriangles), estacao.Descendants(Ns + "p").Single().Value);
        Assert.Null(estacao.Descendants(Ns + "frame_parameters").SingleOrDefault());

        // O primeiro canto da estação de volta ao desenho pela matriz e pela origem.
        var n = estacao.Descendants(Ns + "float_array").Single().Value.Split(' ').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(24, n.Length);
        Assert.Equal((434140.0, -5.0, 99.0), (n[0] + origem.X, -n[2] + origem.Y, n[1] + origem.Z));

        var instancia = doc.Descendants(Ns + "instance_geometry").Single(i => i.Attribute("url")!.Value == "#equipamento-Inverter-1-1");
        Assert.Equal("Material4", instancia.Descendants(Ns + "instance_material").Single().Attribute("symbol")!.Value);
    }

    /// <summary>Objeto com índice fora, sem triângulo ou com id repetido é recusado.</summary>
    [Fact]
    [Trait("Etapa", "6")]
    public void ObjetoMalFeitoERecusado()
    {
        var fiada = Fiada(Guid.NewGuid(), "A-1", 0);
        var ruim = new PvObject("x", "x", PvObjectKind.Station, [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [0, 1, 3]);
        var vazio = ruim with { Triangles = [] };
        var bom = ruim with { Triangles = [0, 1, 2] };

        XDocument Escrever(IReadOnlyList<PvObject> o) => PvColladaWriter.Write([fiada], o, DateTime.UtcNow, "Renan", new Point3(0, 0, 0));

        Assert.Throws<ArgumentException>(() => Escrever([ruim]));
        Assert.Throws<ArgumentException>(() => Escrever([vazio]));
        Assert.Throws<ArgumentException>(() => Escrever([bom, bom]));
        Assert.Single(Escrever([bom]).Descendants(Ns + "geometry"), g => g.Attribute("id")!.Value == "x");
    }
}
