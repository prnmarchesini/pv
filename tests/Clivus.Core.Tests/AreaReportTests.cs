using System.IO.Compression;
using System.Xml.Linq;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O Excel do resumo por área (etapa 41): a aba Resumo com uma linha por
/// área e o total, e uma aba por área com o detalhe. Os números são os do
/// <see cref="AreaSummary"/>.
/// </summary>
public class AreaReportTests
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

    private static string? Celula(XDocument aba, string referencia)
    {
        var c = aba.Descendants(Ns + "c").FirstOrDefault(x => (string?)x.Attribute("r") == referencia);
        if (c is null) return null;
        return (string?)c.Element(Ns + "v") ?? c.Descendants(Ns + "t").FirstOrDefault()?.Value;
    }

    private static IReadOnlyList<Point3> Retangulo(double x0, double y0, double x1, double y1) =>
        [new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0)];

    private static AreaTableInput Mesa(double cx, double cy, string tipo = "Mesa 28", int modulos = 28)
    {
        var identidade = new TableIdentity(Guid.NewGuid(), "F1.1", 100, 100, 0.35, false, null, ModulePowerWatts: 600, ProfileName: tipo);
        return new AreaTableInput(new CountedTable(identidade, 1, modulos, [3.0, 3.0], 0), Retangulo(cx - 5, cy - 2, cx + 5, cy + 2), 2.5);
    }

    private static AreaSummary Resumo(params string[] nomes)
    {
        var areas = nomes.Select((n, i) => new AreaInput(Guid.NewGuid(), n, Retangulo(i * 200, 0, i * 200 + 100, 100))).ToList();
        var mesas = new List<AreaTableInput> { Mesa(20, 20), Mesa(20, 40, "Mesa 14", 14), Mesa(220, 20), Mesa(5000, 5000) };
        return AreaSummary.Build(areas, mesas, 550);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void OExcelTemOResumoEUmaAbaPorArea()
    {
        var resumo = Resumo("Norte", "Sul/Leste");
        var partes = Abrir(AreaReport.Build(resumo, "Aurora", new DateTime(2026, 10, 30, 21, 0, 0)).ToBytes());

        var nomes = partes["xl/workbook.xml"].Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).ToList();
        Assert.Equal(["Resumo", "Norte", "SulLeste", "Fora das áreas"], nomes);

        var aba = partes["xl/worksheets/sheet1.xml"];
        Assert.Equal("Clivus Solar — Aurora: resumo por área", Celula(aba, "A1"));
        Assert.Equal("Área", Celula(aba, "A4"));
        Assert.Equal("Mesas", Celula(aba, "C4"));

        // Uma linha por área, a de fora das áreas e o total, com as mesas de cada uma.
        Assert.Equal(("Norte", "2"), (Celula(aba, "A5"), Celula(aba, "C5")));
        Assert.Equal(("Sul/Leste", "1"), (Celula(aba, "A6"), Celula(aba, "C6")));
        Assert.Equal(("Fora das áreas", "1"), (Celula(aba, "A7"), Celula(aba, "C7")));
        Assert.Equal(("Total", "4"), (Celula(aba, "A8"), Celula(aba, "C8")));
        Assert.Equal("1", Celula(aba, "B5"));
        Assert.Equal("42", Celula(aba, "D5"));
        Assert.Equal("3 × Mesa 28, 1 × Mesa 14", Celula(aba, "M8"));
        Assert.Equal("não", Celula(aba, "N5"));

        // A aba da área: os tipos, o total e os itens.
        var norte = partes["xl/worksheets/sheet2.xml"];
        Assert.Equal("Norte", Celula(norte, "A1"));
        Assert.Equal("Tipo de mesa", Celula(norte, "A4"));
        Assert.Equal(("Mesa 28", "1", "28"), (Celula(norte, "A5"), Celula(norte, "B5"), Celula(norte, "C5")));
        Assert.Equal(("Mesa 14", "1", "14"), (Celula(norte, "A6"), Celula(norte, "B6"), Celula(norte, "C6")));
        Assert.Equal(("Total", "2", "42"), (Celula(norte, "A7"), Celula(norte, "B7"), Celula(norte, "C7")));
        Assert.Equal("Item", Celula(norte, "A9"));
        Assert.Contains(norte.Descendants(Ns + "t"), t => t.Value == "Ocupação (área dos módulos ÷ área em planta)");
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AreasComONomeRepetidoGanhamNumeroNaAba()
    {
        var partes = Abrir(AreaReport.Build(Resumo("Norte", "Norte", "Resumo"), "x", DateTime.Now).ToBytes());

        var nomes = partes["xl/workbook.xml"].Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).ToList();
        Assert.Equal(["Resumo", "Norte", "Norte (2)", "Resumo (2)", "Fora das áreas"], nomes);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void ONomeDeAbaSaiSemCaracterProibidoECurto()
    {
        var planilha = new XlsxWriter();
        planilha.Sheet("Área");

        Assert.Equal("ab", QuantityReport.SheetName(planilha, "a:/b?*[]"));
        Assert.Equal(31, QuantityReport.SheetName(planilha, new string('x', 40)).Length);
        Assert.Equal("Área (2)", QuantityReport.SheetName(planilha, "Área"));
        Assert.Equal("Tabela", QuantityReport.SheetName(planilha, " / "));
    }
}
