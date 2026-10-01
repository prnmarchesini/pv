using System.IO.Compression;
using System.Xml.Linq;

namespace UFV.Core.Tests;

/// <summary>Passo 8.12: o Excel das quantidades.</summary>
public class QuantityReportTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

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

    /// <summary>O texto (ou número) da célula, ou null.</summary>
    private static string? Celula(XDocument aba, string referencia)
    {
        var c = aba.Descendants(Ns + "c").FirstOrDefault(x => (string?)x.Attribute("r") == referencia);
        if (c is null) return null;
        return (string?)c.Element(Ns + "v") ?? c.Descendants(Ns + "t").FirstOrDefault()?.Value;
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ALetraDaColuna(int indice, string esperada)
    {
        Assert.Equal(esperada, XlsxWriter.Column(indice));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void APlanilhaEUmZipDeXmlValido()
    {
        var planilha = new XlsxWriter();
        var aba = planilha.Sheet("Teste");
        aba.Add(["Nome", "Valor"]);
        aba.Add(["a < b & c", 1.5]);
        aba.Add(["inteiro", 7]);
        aba.Add(["sem valor", double.NaN]);

        var partes = Abrir(planilha.ToBytes());

        Assert.Contains("[Content_Types].xml", partes.Keys);
        Assert.Contains("xl/workbook.xml", partes.Keys);

        var folha = partes["xl/worksheets/sheet1.xml"];
        Assert.Equal("a < b & c", Celula(folha, "A2"));
        Assert.Equal("1.5", Celula(folha, "B2"));
        Assert.Equal("7", Celula(folha, "B3"));
        Assert.Null(Celula(folha, "B4"));

        var nomes = partes["xl/workbook.xml"].Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name"));
        Assert.Equal(["Teste"], nomes);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("um nome comprido demais para o excel aceitar")]
    public void AbaComNomeRuimERecusada(string nome)
    {
        Assert.ThrowsAny<ArgumentException>(() => new XlsxWriter().Sheet(nome));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AbaRepetidaERecusada()
    {
        var planilha = new XlsxWriter();
        planilha.Sheet("Resumo");

        Assert.Throws<ArgumentException>(() => planilha.Sheet("resumo"));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void PlanilhaSemAbaERecusada()
    {
        Assert.Throws<InvalidOperationException>(() => new XlsxWriter().ToBytes());
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ORelatorioTemOResumoAsAnalisesEOsPilares()
    {
        var pilares = new[]
        {
            new QuantityPillar("F1.1", 1, 1.10, 1.50, 2.60),
            new QuantityPillar("F1.1", 2, 1.10, 1.90, 3.00),
            new QuantityPillar("F1.2", 1, 1.10, null, null),
        };

        var quantificacao = new AnalysisTally(
            IndependentKind.LowEdge, IndependentAnalysis.Default(IndependentKind.LowEdge), SlopeUnit.Percent,
            new BandCount(1, 2, 0, 0), new BandCount(14, 26, 0, 0), new DateTime(2026, 10, 1, 9, 0, 0));

        var partes = Abrir(QuantityReport.Build(2, 56, 40.32, pilares, [quantificacao]).ToBytes());

        var nomes = partes["xl/workbook.xml"].Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).ToList();
        Assert.Equal(["Resumo", "Análises", "Pilares", "Compra de pilares"], nomes);

        var resumo = partes["xl/worksheets/sheet1.xml"];
        Assert.Equal("2", Celula(resumo, "B2"));
        Assert.Equal("56", Celula(resumo, "B3"));
        Assert.Equal("40.32", Celula(resumo, "B4"));
        Assert.Equal("3", Celula(resumo, "B5"));
        Assert.Equal("1", Celula(resumo, "B6"));
        Assert.Equal("5.6", Celula(resumo, "B7"));

        var analises = partes["xl/worksheets/sheet2.xml"];
        Assert.Equal("ponta baixa", Celula(analises, "A2"));
        Assert.Equal("pilares", Celula(analises, "B2"));
        Assert.Equal("0,3 m", Celula(analises, "C2"));
        Assert.Equal("1", Celula(analises, "D2"));
        Assert.Equal("módulos", Celula(analises, "B3"));
        Assert.Equal("14", Celula(analises, "D3"));

        var lista = partes["xl/worksheets/sheet3.xml"];
        Assert.Equal("F1.1", Celula(lista, "A2"));
        Assert.Equal("2.6", Celula(lista, "E2"));
        Assert.Null(Celula(lista, "E4"));

        var compra = partes["xl/worksheets/sheet4.xml"];
        Assert.Equal("2.6", Celula(compra, "A2"));
        Assert.Equal("1", Celula(compra, "B2"));
        Assert.Equal("3", Celula(compra, "A3"));
    }
}
