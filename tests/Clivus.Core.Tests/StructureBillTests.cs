using System.IO.Compression;
using System.Xml.Linq;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A lista de material da bifila (passo 37.4): pilares por linha, vigas, mãos
/// francesas, terças (com as barras e as emendas) e tirantes, e a aba
/// "Estrutura" do Excel. A monoposte exporta exatamente como antes.
/// </summary>
public class StructureBillTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static SystemConfiguration Config() =>
        SystemConfiguration.Default with { MinEmbedment = 2.2, MaxEmbedment = 2.2, MaxLowEdge = 1.5 };

    /// <summary>Uma mesa GC800 no chão plano, nivelada, com a PB da Arctech (0,800 + 0,033 × cos 10° = 0,8325).</summary>
    private static (BifilaStructureResult Estrutura, TablePillars Pilares, TableProfile Perfil) Mesa()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        var geo = TableGeometry.Local(p.Layout, p.Frame.Pillars(p.Layout), p.Frame);
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };
        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, 8, 0.5,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(p.TiltRadians)), Config().UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[1];
        var orientacao = RowOrientation.Resolve(cell, Config().UpslopeAzimuthRadians);

        Point3 P(double x, double y) => new(x, y, 700);
        var chao = new Tin([new Triangle(P(-100, -100), P(300, -100), P(300, 300)), new Triangle(P(-100, -100), P(300, 300), P(-100, 300))]);

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, p.TiltRadians, new SolvedTable("F2.2", 700.8325, 700.8325, 0, false, null), chao, Config());

        return (BifilaStructure.Build(p, geo, pilares), pilares, p);
    }

    private static IReadOnlyList<QuantityBar> Barras(string mesa = "F1.1")
    {
        var (estrutura, _, perfil) = Mesa();
        return StructureBill.From(mesa, estrutura, perfil.Frame.Bifila!.PurlinMaxBar);
    }

    private static IReadOnlyList<QuantityPillar> Pilares(string mesa = "F1.1")
    {
        var (_, pilares, _) = Mesa();
        return pilares.Pillars.Select((p, i) => new QuantityPillar(mesa, i + 1, p.Embedment, p.FreeHeight, p.Length, p.Line)).ToList();
    }

    private static StructureBillRow Linha(IReadOnlyList<StructureBillRow> linhas, string item) =>
        Assert.Single(linhas, l => l.Item == item);

    // ---------------------------------------------------------------- linhas

    [Fact]
    [Trait("Etapa", "37")]
    public void AListaDaUmaMesa2P14()
    {
        var linhas = StructureBill.Rows(Pilares(), Barras());

        Assert.Equal(6, Linha(linhas, "Pilar dianteiro").Quantity);
        Assert.Equal(6, Linha(linhas, "Pilar traseiro").Quantity);
        Assert.Equal(6 * 3.059, Linha(linhas, "Pilar dianteiro").Total!.Value, 2);
        Assert.Equal(6 * 3.588, Linha(linhas, "Pilar traseiro").Total!.Value, 2);

        var viga = Linha(linhas, "Viga");
        Assert.Equal(6, viga.Quantity);
        Assert.Equal(4.03, viga.Length!.Value, 9);
        Assert.Equal(6 * 4.03, viga.Total!.Value, 6);

        Assert.Equal(6, Linha(linhas, "Mão francesa dianteira").Quantity);
        Assert.Equal(1.244, Linha(linhas, "Mão francesa dianteira").Length!.Value, 9);
        Assert.Equal(6, Linha(linhas, "Mão francesa traseira").Quantity);
        Assert.Equal(1.209, Linha(linhas, "Mão francesa traseira").Length!.Value, 9);

        var terca = Linha(linhas, "Terça");
        Assert.Equal(4, terca.Quantity);
        Assert.Equal(18.602, terca.Length!.Value, 9);

        // 18,602 em barras de até 7 m: 3 barras e 2 emendas por linha, como no desenho da Arctech.
        Assert.Equal(12, Linha(linhas, "Terça: barras de até 7 m").Quantity);
        Assert.Equal(8, Linha(linhas, "Terça: emendas (barras de até 7 m)").Quantity);

        var tirante = Linha(linhas, "Tirante");
        Assert.Equal(4, tirante.Quantity);
        Assert.Equal(3.52, tirante.Length!.Value, 2);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void DuasMesasDobramAsContas()
    {
        var linhas = StructureBill.Rows(
            [.. Pilares("F1.1"), .. Pilares("F1.2")],
            [.. Barras("F1.1"), .. Barras("F1.2")]);

        Assert.Equal(12, Linha(linhas, "Viga").Quantity);
        Assert.Equal(24, Linha(linhas, "Terça: barras de até 7 m").Quantity);
        Assert.Equal(12, Linha(linhas, "Pilar traseiro").Quantity);
    }

    [Theory]
    [Trait("Etapa", "37")]
    [InlineData(18.602, 7.0, 3)]
    [InlineData(14.0, 7.0, 2)]          // exato: duas barras, uma emenda
    [InlineData(14.0000000001, 7.0, 2)] // ruído de ponto flutuante não vira barra
    [InlineData(6.9, 7.0, 1)]
    [InlineData(21.5, 7.0, 4)]
    public void ATercaECortadaEmBarrasDeAteOMaximo(double comprimento, double maximo, int barras)
    {
        Assert.Equal(barras, StructureBill.PurlinBars(comprimento, maximo));
    }

    /// <summary>Mão francesa de 1,24400 e 1,24405: o ruído da conta não vira outra peça.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void RuidoDaContaNaoViraOutraPeca()
    {
        var barras = new List<QuantityBar>
        {
            new("F1.1", StructurePart.Brace, PillarLine.Front, 0, 1.24400),
            new("F1.1", StructurePart.Brace, PillarLine.Front, 1, 1.24405),
        };

        var linha = Linha(StructureBill.Rows([], barras), "Mão francesa dianteira");

        Assert.Equal(2, linha.Quantity);
        Assert.Equal(1.244, linha.Length!.Value, 9);
        Assert.Equal(2.488, linha.Total!.Value, 9);
    }

    /// <summary>
    /// Tirantes de 3,5151 e 3,5249 (a mesa girada muda o comprimento): duas
    /// linhas, e nenhum corte mais curto que a peça (o arredondamento para o
    /// mais próximo cortava o segundo 1 cm curto). Total = corte × quantidade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OCorteNuncaSaiCurto()
    {
        var barras = new List<QuantityBar>
        {
            new("F1.1", StructurePart.Rod, PillarLine.Rear, 2, 3.5151),
            new("F1.1", StructurePart.Rod, PillarLine.Rear, 2, 3.5249),
        };

        var linhas = StructureBill.Rows([], barras).Where(l => l.Item == "Tirante").ToList();

        Assert.Equal(2, linhas.Count);
        Assert.All(linhas, l => Assert.Equal(l.Length!.Value * l.Quantity, l.Total!.Value, 9));
        Assert.Contains(linhas, l => l.Length == 3.525);
        foreach (var peca in barras) Assert.Contains(linhas, l => l.Length >= peca.Length - 0.0001 && l.Length - peca.Length < 0.001);
    }

    /// <summary>Barra máxima que não é número não corta terça e não dá emenda negativa.</summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData(0)]
    [InlineData(-7)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BarraMaximaInvalidaNaoViraEmendaNegativa(double barra)
    {
        var (estrutura, _, _) = Mesa();
        var barras = StructureBill.From("F1.1", estrutura, barra);

        var linhas = StructureBill.Rows([], barras);

        Assert.DoesNotContain(linhas, l => l.Item.StartsWith("Terça: barras", StringComparison.Ordinal));
        Assert.DoesNotContain(linhas, l => l.Quantity < 0);
        Assert.Equal(0, StructureBill.PurlinBars(18.602, barra));
    }

    /// <summary>Dois tipos de bifila com barras diferentes: as emendas de cada uma dizem a barra.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AsEmendasDizemABarra()
    {
        var barras = new List<QuantityBar>
        {
            new("F1.1", StructurePart.Purlin, PillarLine.Single, 0, 18.602, 7.0),
            new("F2.1", StructurePart.Purlin, PillarLine.Single, 0, 18.602, 6.0),
        };

        var linhas = StructureBill.Rows([], barras);

        Assert.Equal(2, Linha(linhas, "Terça: emendas (barras de até 7 m)").Quantity);
        Assert.Equal(3, Linha(linhas, "Terça: emendas (barras de até 6 m)").Quantity);
    }

    /// <summary>A lista mesa a mesa: o plano pede "por mesa e total".</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AListaSaiTambemPorMesa()
    {
        var porMesa = StructureBill.PerTable(
            [.. Pilares("F1.2"), .. Pilares("F1.1")],
            [.. Barras("F1.2"), .. Barras("F1.1")]);

        Assert.Equal(["F1.1", "F1.2"], porMesa.Select(m => m.Table));
        Assert.All(porMesa, m =>
        {
            Assert.Equal(6, m.FrontPillars);
            Assert.Equal(6, m.RearPillars);
            Assert.Equal(6, m.Beams);
            Assert.Equal(12, m.Braces);
            Assert.Equal(4 * 18.602, m.PurlinMeters, 6);
            Assert.Equal(4, m.Rods);
        });
    }

    // ----------------------------------------------------------------- Excel

    private static Dictionary<string, XDocument> Abrir(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var partes = new Dictionary<string, XDocument>();
        foreach (var entrada in zip.Entries)
        {
            using var fluxo = entrada.Open();
            partes[entrada.FullName] = XDocument.Load(fluxo);
        }

        return partes;
    }

    private static List<string?> NomesDasAbas(byte[] bytes) =>
        Abrir(bytes)["xl/workbook.xml"].Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).ToList();

    private static string? Celula(XDocument aba, string referencia)
    {
        var c = aba.Descendants(Ns + "c").FirstOrDefault(x => (string?)x.Attribute("r") == referencia);
        if (c is null) return null;
        return (string?)c.Element(Ns + "v") ?? c.Descendants(Ns + "t").FirstOrDefault()?.Value;
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaGanhaAAbaEstrutura()
    {
        var bytes = QuantityReport.Build(1, 28, 20.16, Pilares(), [], estrutura: Barras()).ToBytes();

        Assert.Contains("Estrutura", NomesDasAbas(bytes));
    }

    /// <summary>A regressão: a monoposte exporta as mesmas abas, na mesma ordem, de antes da bifila.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMonoposteExportaComoAntes()
    {
        var mono = new List<QuantityPillar> { new("F1.1", 1, 0.9, 1.2, 2.1), new("F1.1", 2, 0.9, 1.3, 2.2) };

        var bytes = QuantityReport.Build(1, 28, 20.16, mono, []).ToBytes();

        Assert.Equal(["Resumo", "Análises", "Pilares", "Compra de pilares"], NomesDasAbas(bytes));

        // E a aba Pilares sem a coluna de linha: Mesa, Pilar, Enterrado...
        var partes = Abrir(bytes);
        var pilares = partes["xl/worksheets/sheet3.xml"];
        Assert.Equal("Enterrado (m)", Celula(pilares, "C1"));
    }

    /// <summary>Com bifila, a aba Pilares diz a linha de cada pilar, logo depois do número.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void NaBifilaAAbaPilaresDizALinha()
    {
        var bytes = QuantityReport.Build(1, 28, 20.16, Pilares(), [], estrutura: Barras()).ToBytes();
        var partes = Abrir(bytes);

        var pilares = partes["xl/worksheets/sheet3.xml"];
        Assert.Equal("Linha", Celula(pilares, "C1"));
        Assert.Equal("dianteiro", Celula(pilares, "C2"));
        Assert.Equal("traseiro", Celula(pilares, "C3"));
    }

    /// <summary>Pilar bifila sem nenhuma peça achada: a aba diz isso, em vez de parecer completa.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void SemPecasAAbaAvisa()
    {
        var bytes = QuantityReport.Build(1, 28, 20.16, Pilares(), []).ToBytes();
        var partes = Abrir(bytes);
        var indice = NomesDasAbas(bytes).IndexOf("Estrutura");

        Assert.True(indice >= 0);
        var texto = string.Join(" ", partes[$"xl/worksheets/sheet{indice + 1}.xml"].Descendants(Ns + "t").Select(t => t.Value));
        Assert.Contains("Nenhuma peça da estrutura", texto);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void SemMarcarAAbaEstruturaElaNaoSai()
    {
        var bytes = QuantityReport.Build(1, 28, 20.16, Pilares(), [], ExportSheets.Summary | ExportSheets.Pillars, estrutura: Barras()).ToBytes();

        Assert.DoesNotContain("Estrutura", NomesDasAbas(bytes));
    }
}
