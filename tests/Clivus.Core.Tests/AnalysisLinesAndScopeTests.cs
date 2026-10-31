using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Clivus.Core.Tests;

/// <summary>
/// 42.6 (31/10/2026, Renan na tela do Aurora): as análises por escopo ("Em
/// analise eu tbm quero poder escolher o que analisar, uma area ou todas"),
/// o fundo e a borda dos textos ("texto ruim de ver") e, nos pilares do
/// biposte, uma coluna de limites por linha de pilar ("cada um tem uma
/// altura [...] a cor pode ser o mesmo para ambos").
/// </summary>
public class AnalysisLinesAndScopeTests
{
    private static readonly RgbColor Vermelho = new(255, 0, 0);
    private static readonly RgbColor Azul = new(0, 0, 255);

    /// <summary>A regra do Aurora: o pilar da frente acima de 1,50 m, o de trás acima de 2,50 m; a monoposte acima de 3 m.</summary>
    private static ThresholdRule DoBiposte() => new(null, Vermelho, 3.0, Azul, PaintPieces: true)
    {
        Front = new LineLimits(null, 1.5),
        Rear = new LineLimits(1.0, 2.5),
    };

    [Theory]
    [Trait("Etapa", "42")]
    [InlineData(PillarLine.Front, 1.4, Band.Inside)]
    [InlineData(PillarLine.Front, 1.6, Band.Above)]
    [InlineData(PillarLine.Rear, 1.6, Band.Inside)]
    [InlineData(PillarLine.Rear, 0.9, Band.Below)]
    [InlineData(PillarLine.Rear, 2.6, Band.Above)]
    [InlineData(PillarLine.Single, 2.6, Band.Inside)]
    [InlineData(PillarLine.Single, 3.1, Band.Above)]
    public void CadaLinhaDePilarUsaOsLimitesDela(PillarLine linha, double valor, Band esperada)
    {
        Assert.Equal(esperada, DoBiposte().ForLine(linha).Classify(valor));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AsCoresEAPinturaSaoAsDaRegraEmTodasAsLinhas()
    {
        var frente = DoBiposte().ForLine(PillarLine.Front);

        Assert.Equal(Azul, frente.ColorOf(1.6));
        Assert.True(frente.PaintPieces);
        Assert.Equal(Vermelho, DoBiposte().ForLine(PillarLine.Rear).ColorOf(0.5));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void LinhaSemLimitesPropriosUsaOsDaRegra()
    {
        var regra = new ThresholdRule(null, Vermelho, 2.0, Azul, PaintPieces: false) { Rear = new LineLimits(null, 3.0) };

        Assert.Equal(Band.Above, regra.ForLine(PillarLine.Front).Classify(2.5));
        Assert.Equal(Band.Inside, regra.ForLine(PillarLine.Rear).Classify(2.5));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void BastaUmLimiteEmUmaLinha()
    {
        var soATras = new ThresholdRule(null, Vermelho, null, Azul, PaintPieces: false) { Front = new LineLimits(null, null), Rear = new LineLimits(null, 2.5) };
        var nenhum = new ThresholdRule(null, Vermelho, null, Azul, PaintPieces: false) { Front = new LineLimits(null, null), Rear = new LineLimits(null, null) };

        Assert.True(soATras.IsValid);
        Assert.Equal(Band.Inside, soATras.ForLine(PillarLine.Front).Classify(99));
        Assert.False(nenhum.IsValid);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void LimiteTrocadoNumaLinhaDizQualLinha()
    {
        var regra = new ThresholdRule(null, Vermelho, 3.0, Azul, PaintPieces: false) { Rear = new LineLimits(2.0, 1.0) };

        Assert.False(regra.IsValid);
        Assert.Contains(IndependentAnalysis.LineName(PillarLine.Rear), regra.WhyInvalid, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ARegraComAsLinhasVoltaIgualEASemLinhasContinuaNaVersaoUm()
    {
        var comLinhas = IndependentAnalysis.Encode(DoBiposte());
        Assert.Equal(8, comLinhas.Count);
        Assert.Equal(DoBiposte(), IndependentAnalysis.Decode(comLinhas, IndependentKind.PillarAbove));

        // Os limites de uma linha sem número são diferentes de não ter limites próprios.
        var vazia = new ThresholdRule(0.3, Vermelho, null, Azul, PaintPieces: false) { Front = new LineLimits(null, null) };
        Assert.Equal(vazia, IndependentAnalysis.Decode(IndependentAnalysis.Encode(vazia), IndependentKind.PillarBuried));

        var semLinhas = IndependentAnalysis.Encode(new ThresholdRule(0.3, Vermelho, 1.2, Azul, PaintPieces: false));
        Assert.Equal(6, semLinhas.Count);
        Assert.Equal("1", semLinhas[0]);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void LimiteDeLinhaIlegivelVoltaAPadrao()
    {
        var campos = IndependentAnalysis.Encode(DoBiposte()).ToList();
        campos[7] = "1,5;x";

        Assert.Equal(IndependentAnalysis.Default(IndependentKind.PillarAbove), IndependentAnalysis.Decode(campos, IndependentKind.PillarAbove));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AContagemSomaAsLinhas()
    {
        Assert.Equal(new BandCount(3, 7, 4, 1), new BandCount(1, 5, 0, 1).Plus(new BandCount(2, 2, 4, 0)));
        Assert.Equal(BandCount.None, new BandCount(0, 0, 0, 0));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ADescricaoDaLinhaUsaOsLimitesDela()
    {
        var texto = IndependentAnalysis.DescribeLine(IndependentKind.PillarAbove, DoBiposte(), PillarLine.Rear, new BandCount(2, 30, 1, 0), SlopeUnit.Percent);

        Assert.StartsWith("pilares de trás:", texto, StringComparison.Ordinal);
        Assert.Contains("2 abaixo de 1,00 m", texto, StringComparison.Ordinal);
        Assert.Contains("1 acima de 2,50 m", texto, StringComparison.Ordinal);
        Assert.Contains("(total 33)", texto, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AQuantificacaoComLugarELinhasVoltaIgual()
    {
        var linhas = new[] { new LineCount(PillarLine.Front, new BandCount(0, 40, 2, 0)), new LineCount(PillarLine.Rear, new BandCount(1, 38, 3, 0)) };
        var registro = new AnalysisTally(IndependentKind.PillarAbove, DoBiposte(), SlopeUnit.Percent,
            new BandCount(1, 78, 5, 0), null, new DateTime(2026, 10, 31, 14, 0, 0, DateTimeKind.Local), "Área 1, Área 2", linhas);

        var volta = AnalysisTally.Decode(IndependentKind.PillarAbove, registro.Encode());

        Assert.NotNull(volta);
        Assert.Equal(registro.Rule, volta.Rule);
        Assert.Equal(registro.Points, volta.Points);
        Assert.Equal(registro.When, volta.When);
        Assert.Equal("Área 1, Área 2", volta.Where);
        Assert.Equal(linhas, volta.Lines);
    }

    /// <summary>A quantificação gravada antes de 31/10/2026 (versão 1) continua legível, e era do desenho inteiro.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void AQuantificacaoDaVersaoUmContinuaLegivel()
    {
        var regra = new ThresholdRule(0.30, Vermelho, 1.20, Azul, PaintPieces: false);
        var quando = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Local);
        var campos = new List<string> { "1", SlopeLabel.Name(SlopeUnit.Percent), quando.ToString("O", CultureInfo.InvariantCulture), "1", "2", "3", "0", "", "", "", "" };
        campos.AddRange(IndependentAnalysis.Encode(regra));

        var antiga = AnalysisTally.Decode(IndependentKind.LowEdge, campos);

        Assert.NotNull(antiga);
        Assert.Equal(new BandCount(1, 2, 3, 0), antiga.Points);
        Assert.Equal(regra, antiga.Rule);
        Assert.Null(antiga.Scope);
        Assert.Null(antiga.Lines);
        Assert.Equal("desenho inteiro", antiga.Where);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AQuantificacaoComLinhaIlegivelENula()
    {
        var registro = new AnalysisTally(IndependentKind.PillarAbove, DoBiposte(), SlopeUnit.Percent,
            new BandCount(1, 78, 5, 0), null, new DateTime(2026, 10, 31, 14, 0, 0, DateTimeKind.Local), "Área 1", [new LineCount(PillarLine.Front, new BandCount(1, 2, 3, 0))]);
        var campos = registro.Encode().ToList();
        campos[12] = "Lateral=1,2,3,0";

        Assert.Null(AnalysisTally.Decode(IndependentKind.PillarAbove, campos));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OFundoEABordaVoltamIguaisEOPadraoTemFundo()
    {
        Assert.True(AnalysisTextLook.Default.Background);
        Assert.False(AnalysisTextLook.Default.Border);

        foreach (var aparencia in new[] { new AnalysisTextLook(true, true), new AnalysisTextLook(false, true), new AnalysisTextLook(false, false) })
            Assert.Equal(aparencia, AnalysisTextLook.Decode(aparencia.Encode()));

        Assert.Null(AnalysisTextLook.Decode(null));
        Assert.Null(AnalysisTextLook.Decode(["1", "2", "0"]));
        Assert.NotEqual(AnalysisTextLook.StorageKey(IndependentKind.PillarAbove), AnalysisTextLook.StorageKey(IndependentKind.PillarBuried));
    }

    /// <summary>No Excel, cada linha de pilar sai numa linha com os limites dela, e a coluna Onde diz o lugar.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void NoExcelCadaLinhaDePilarTemALinhaDelaEOLugar()
    {
        var pilares = new[]
        {
            new QuantityPillar("F1.1", 1, 2.2, 1.2, 3.4, PillarLine.Front),
            new QuantityPillar("F1.1", 2, 2.2, 2.6, 4.8, PillarLine.Rear),
        };
        var livre = new AnalysisTally(IndependentKind.PillarAbove, DoBiposte(), SlopeUnit.Percent,
            new BandCount(0, 1, 1, 0), null, new DateTime(2026, 10, 31, 14, 0, 0), "Área 1",
            [new LineCount(PillarLine.Front, new BandCount(0, 1, 0, 0)), new LineCount(PillarLine.Rear, new BandCount(0, 0, 1, 0))]);
        var pb = new AnalysisTally(IndependentKind.LowEdge, IndependentAnalysis.Default(IndependentKind.LowEdge), SlopeUnit.Percent,
            new BandCount(0, 1, 0, 0), null, new DateTime(2026, 10, 1, 9, 0, 0));

        var analises = Planilha(QuantityReport.Build(1, 28, 20.16, pilares, [pb, livre]).ToBytes(), "xl/worksheets/sheet2.xml");

        Assert.Equal("Onde", Celula(analises, "K1"));
        Assert.Equal("desenho inteiro", Celula(analises, "K2"));
        Assert.Equal("pilares da frente", Celula(analises, "B3"));
        Assert.Equal("1,5 m", Celula(analises, "F3"));
        Assert.Equal("Área 1", Celula(analises, "K3"));
        Assert.Equal("pilares de trás", Celula(analises, "B4"));
        Assert.Equal("1 m", Celula(analises, "C4"));
        Assert.Equal("2,5 m", Celula(analises, "F4"));
        Assert.Equal("1", Celula(analises, "G4"));
    }

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static XDocument Planilha(byte[] bytes, string parte)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var leitor = zip.GetEntry(parte)!.Open();
        return XDocument.Load(leitor);
    }

    /// <summary>O texto (ou número) da célula, ou null (como em <see cref="QuantityReportTests"/>).</summary>
    private static string? Celula(XDocument aba, string referencia)
    {
        var c = aba.Descendants(Ns + "c").FirstOrDefault(x => (string?)x.Attribute("r") == referencia);
        if (c is null) return null;
        return (string?)c.Element(Ns + "v") ?? c.Descendants(Ns + "t").FirstOrDefault()?.Value;
    }
}
