using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// As mesas do cliente (40.1): o objeto de 4 vértices vira pegada, as pegadas
/// viram fileiras, e o que diverge sai com o motivo. Tudo em planta, só com X
/// e Y (revisão de tela do 40.2, Renan 30/10/2026: "o eixo Z deve estar
/// cagado, então vc vai gerar a mesa respeitando apenas a posição X e Y").
/// </summary>
public class FieldObjectsTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>
    /// O objeto D6D4 do desenho do projeto Aurora (PLAT A - CELDAS), com a
    /// origem no primeiro vértice: a área dos módulos de uma 2P14 a 10°, o
    /// eixo descendo 1,4 m na fileira. Em planta é um paralelogramo.
    /// </summary>
    private static FieldObject DoCliente(string id = "D6D4") => new(id, "_PLAT A - CELDAS",
    [
        new(0, 0, 0),
        new(18.4486, 0, -1.4056),
        new(18.3854, -4.7152, -2.2347),
        new(-0.0631, -4.7152, -0.8290),
    ]);

    /// <summary>
    /// Um retângulo 3D com o lado de trás em (x0, y0), o eixo no rumo dado e
    /// a ponta baixa à direita do eixo, inclinado <paramref name="graus"/>.
    /// </summary>
    private static FieldObject Mesa(string id, double x0, double y0, double comprimento = 18.502, double largura = 4.788, double graus = 10, double rumo = 0)
    {
        var (ux, uy) = (Math.Cos(rumo * Grau), Math.Sin(rumo * Grau));
        var (rx, ry) = (uy, -ux);
        var (dh, dz) = (largura * Math.Cos(graus * Grau), -largura * Math.Sin(graus * Grau));

        return new FieldObject(id, "MESAS",
        [
            new(x0, y0, 0),
            new(x0 + ux * comprimento, y0 + uy * comprimento, 0),
            new(x0 + ux * comprimento + rx * dh, y0 + uy * comprimento + ry * dh, dz),
            new(x0 + rx * dh, y0 + ry * dh, dz),
        ]);
    }

    private static TableProfile Gc800 => BifilaReference.Profile(BifilaVariant.Gc800);

    /// <summary>
    /// O D6D4 visto de cima: o comprimento é o de planta (a rampa de 7,6 %
    /// encurta os 18,502 em 5 cm) e a largura é a distância entre os lados
    /// maiores (4,788·cos 10°), num paralelogramo que passa por retângulo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void OObjetoDoClienteEmPlanta()
    {
        var f = FieldObjects.Footprint(DoCliente(), out var porQue);

        Assert.Null(porQue);
        Assert.NotNull(f);
        Assert.Equal(18.4486, f.Length, 3);
        Assert.Equal(4.7152, f.Width, 4);
        Assert.Equal(0, f.Axis, 6);
        Assert.Equal(0, f.Center.Z);
    }

    /// <summary>
    /// O Z do objeto não conta (Renan, 30/10/2026: "o objeto que vou
    /// selecionar do cliente, ele está em 2D, o eixo Z deve estar cagado"): o
    /// mesmo objeto achatado, a 9999 m ou com cada vértice numa cota
    /// qualquer dá a mesma pegada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void OZDoObjetoNaoMudaNada()
    {
        var o = DoCliente();
        var referencia = FieldObjects.Footprint(o, out _)!;
        var cotas = new[] { 0.0, 9999.0, -37.25, 1e6 };

        var variantes = new[]
        {
            o with { Vertices = [.. o.Vertices.Select(p => new Point3(p.X, p.Y, 0))] },
            o with { Vertices = [.. o.Vertices.Select(p => new Point3(p.X, p.Y, 9999))] },
            o with { Vertices = [.. o.Vertices.Select((p, i) => new Point3(p.X, p.Y, cotas[i]))] },
            o with { Vertices = [.. o.Vertices.Select(p => new Point3(p.X, p.Y, double.NaN))] },
        };

        foreach (var variante in variantes)
        {
            var f = FieldObjects.Footprint(variante, out var porQue);

            Assert.Null(porQue);
            Assert.Equal(referencia, f);
        }
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void OsVerticesDeTrasParaFrenteDaoAMesmaPegada()
    {
        var o = DoCliente();
        var invertido = o with { Vertices = [o.Vertices[2], o.Vertices[3], o.Vertices[0], o.Vertices[1]] };
        var ladoMenor = o with { Vertices = [o.Vertices[1], o.Vertices[2], o.Vertices[3], o.Vertices[0]] };

        foreach (var variante in new[] { invertido, ladoMenor })
        {
            var f = FieldObjects.Footprint(variante, out _)!;

            Assert.Equal(0, f.Axis, 6);
            Assert.Equal(18.4486, f.Length, 3);
            Assert.Equal(4.7152, f.Width, 3);
        }
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void OVerticeRepetidoNoFimNaoConta()
    {
        var o = DoCliente();
        var fechado = o with { Vertices = [.. o.Vertices, o.Vertices[0]] };

        Assert.NotNull(FieldObjects.Footprint(fechado, out _));
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void OEixoTortoSaiNoRumoDele()
    {
        var f = FieldObjects.Footprint(Mesa("A", 10, 20, rumo: 30), out _)!;

        Assert.Equal(30, f.Axis / Grau, 6);
        Assert.Equal(18.502, f.Length, 6);
        Assert.Equal(4.788 * Math.Cos(10 * Grau), f.Width, 6);
    }

    /// <summary>O retângulo 3D inclinado e o plano com a largura de planta são a mesma mesa.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void ORetanguloPlanoEOInclinadoDaoAMesmaPegada()
    {
        var largura = 4.788 * Math.Cos(10 * Grau);
        var plano = new FieldObject("P", "L", [new(0, 0, 5), new(18.502, 0, 5), new(18.502, -largura, 5), new(0, -largura, 5)]);

        var doPlano = FieldObjects.Footprint(plano, out _)!;
        var doInclinado = FieldObjects.Footprint(Mesa("I", 0, 0), out _)!;

        Assert.Equal(doInclinado.Length, doPlano.Length, 9);
        Assert.Equal(doInclinado.Width, doPlano.Width, 9);
        Assert.Equal(doInclinado.Center, doPlano.Center);
    }

    [Theory]
    [Trait("Etapa", "40")]
    [InlineData(3)]
    [InlineData(5)]
    public void SoRetanguloDeQuatroVerticesViraMesa(int vertices)
    {
        var pontos = Enumerable.Range(0, vertices).Select(i => new Point3(Math.Cos(i * 2 * Math.PI / vertices) * 5, Math.Sin(i * 2 * Math.PI / vertices) * 5, 0)).ToList();
        var levantamento = FieldObjects.Survey([new FieldObject("X", "L", pontos)]);

        Assert.Null(FieldObjects.Footprint(new FieldObject("X", "L", pontos), out var porQue));
        Assert.Contains("vértices", porQue);
        Assert.Equal(FieldIssueKind.NotRectangle, Assert.Single(levantamento.Issues).Kind);
        Assert.Equal(0, levantamento.TableCount);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void LosangoNaoViraMesa()
    {
        var losango = new FieldObject("L", "L", [new(0, 0, 0), new(18, 0, 0), new(20, 4, 0), new(2, 4, 0)]);

        Assert.Null(FieldObjects.Footprint(losango, out var porQue));
        Assert.Contains("90°", porQue);
    }

    /// <summary>Duas fileiras de três com 0,35 m entre as mesas, e uma quarta 6 m depois (o motor parte a corrente acima de 5 m): ela é outra fileira.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void AsFileirasSeguemAsLinhasEPartemNaFolgaGrande()
    {
        var passo = 18.502 + 0.35;
        List<FieldObject> objetos =
        [
            Mesa("A1", 0, 0), Mesa("A2", passo, 0), Mesa("A3", 2 * passo, 0), Mesa("A4", 3 * passo + 6, 0),
            Mesa("B1", 0, -6.8), Mesa("B2", passo, -6.8), Mesa("B3", 2 * passo, -6.8),
        ];

        var levantamento = FieldObjects.Survey(objetos);

        Assert.Empty(levantamento.Issues);
        Assert.Equal(7, levantamento.TableCount);
        Assert.Equal([3, 1, 3], levantamento.Rows.Select(r => r.Tables.Count));
        Assert.Equal(["A1", "A2", "A3"], levantamento.Rows[0].Tables.Select(t => t.Id));
        Assert.Equal(["B1", "B2", "B3"], levantamento.Rows[2].Tables.Select(t => t.Id));
    }

    /// <summary>Mesas da mesma fileira com alguns centímetros de diferença de linha continuam na fileira.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void AlgunsCentimetrosForaDaLinhaContinuamNaFileira()
    {
        var levantamento = FieldObjects.Survey([Mesa("A1", 0, 0), Mesa("A2", 18.852, 0.21), Mesa("A3", 37.704, -0.04)]);

        Assert.Single(levantamento.Rows);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ObjetoRepetidoViraUmaMesaSo()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 0, 0), Mesa("C", 18.852, 0)]);

        var repetido = Assert.Single(levantamento.Issues);
        Assert.Equal(FieldIssueKind.Duplicate, repetido.Kind);
        Assert.Equal("B", repetido.Id);
        Assert.True(repetido.Skips);
        Assert.Contains("A", repetido.Message);
        Assert.Equal(2, levantamento.TableCount);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ObjetoEmCimaDeParteDeOutroNaoViraMesa()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 5, 0)]);

        var problema = Assert.Single(levantamento.Issues);
        Assert.Equal(FieldIssueKind.Overlap, problema.Kind);
        Assert.Equal("B", problema.Id);
        Assert.Equal(1, levantamento.TableCount);
    }

    /// <summary>Mesas encostadas (folga zero) não estão uma em cima da outra.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void MesasEncostadasNaoSeSobrepoem()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 18.502, 0)]);

        Assert.Empty(levantamento.Issues);
        Assert.Equal(2, levantamento.TableCount);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void TamanhoDiferenteEApontadoMasViraMesa()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 18.852, 0), Mesa("C", 37.704, 0, comprimento: 9.3)]);

        var problema = Assert.Single(levantamento.Issues);
        Assert.Equal(FieldIssueKind.Size, problema.Kind);
        Assert.Equal("C", problema.Id);
        Assert.False(problema.Skips);
        Assert.Equal(18.5, levantamento.ReferenceLength, 2);
        Assert.Equal(3, levantamento.TableCount);
    }

    /// <summary>
    /// O rumo diferente é apontado; a inclinação diferente aparece só como a
    /// largura em planta que ela dá (o Z não conta).
    /// </summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void RumoDiferenteEApontadoEAInclinacaoSoPelaLargura()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 0, -7), Mesa("C", 0, -14, graus: 15), Mesa("D", 40, 0, rumo: 3)]);

        Assert.Contains(levantamento.Issues, i => i.Id == "C" && i.Kind == FieldIssueKind.Size);
        Assert.Contains(levantamento.Issues, i => i.Id == "D" && i.Kind == FieldIssueKind.Axis);
    }

    /// <summary>O mesmo retângulo desenhado no sentido contrário (eixo a 180°) é a mesma mesa: em planta não há lado de ponta baixa.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void OEixoDesenhadoAoContrarioNaoEDivergencia()
    {
        var b = Mesa("B", 18.502, -20, rumo: 180);
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("C", 0, -7), b]);

        Assert.Empty(levantamento.Issues);
        Assert.Equal(3, levantamento.TableCount);
    }

    /// <summary>A rampa da fileira encurta o objeto 3D em planta: até 1,5 % não é "tamanho diferente".</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void ARampaQueEncurtaAPlantaNaoEDivergencia()
    {
        var naRampa = DoCliente("RAMPA");
        var curto = Mesa("CURTO", 0, 20, comprimento: 18.0);

        var levantamento = FieldObjects.Survey([Mesa("PLANO1", 0, 10), Mesa("PLANO2", 0, 30), naRampa, curto]);

        Assert.DoesNotContain(levantamento.Issues, i => i.Id == "RAMPA");
        Assert.Contains(levantamento.Issues, i => i.Id == "CURTO" && i.Kind == FieldIssueKind.Size);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ForaDaAreaNaoViraMesaEAtravessarViraComAviso()
    {
        IReadOnlyList<Point3> area = [new(-1, 1, 0), new(30, 1, 0), new(30, -6, 0), new(-1, -6, 0)];
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 18.852, 0), Mesa("C", 100, 0)], area);

        Assert.Contains(levantamento.Issues, i => i.Id == "C" && i.Kind == FieldIssueKind.OutsideArea && i.Skips);
        Assert.Contains(levantamento.Issues, i => i.Id == "B" && i.Kind == FieldIssueKind.CrossesArea && !i.Skips);
        Assert.Equal(2, levantamento.TableCount);
    }

    /// <summary>A Arctech GC800 tem a área de módulos dos objetos (18,502 m sem as sobras) e os 10°.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void AGc800BateComOsObjetosDoCliente()
    {
        var levantamento = FieldObjects.Survey([DoCliente()], profile: Gc800);

        Assert.Empty(levantamento.Issues);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void MesaDeOutroTamanhoEApontada()
    {
        var modulo = new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);
        var outra = new TableProfile(
            "2V28",
            new TableLayout(modulo, 56, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
            20 * Grau);

        var levantamento = FieldObjects.Survey([DoCliente()], profile: outra);

        Assert.Contains(levantamento.Issues, i => i.Id == "*" && i.Kind == FieldIssueKind.ProfileSize);
    }

    /// <summary>O objeto pode ser a mesa inteira, com as sobras: também bate.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void OObjetoComAsSobrasTambemBate()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0, comprimento: 18.602)], profile: Gc800);

        Assert.Empty(levantamento.Issues);
    }
}
