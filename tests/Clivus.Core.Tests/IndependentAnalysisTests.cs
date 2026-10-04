namespace Clivus.Core.Tests;

/// <summary>
/// Passos 8.9 a 8.11: as análises independentes (Melhorias.docx,
/// 01/10/2026). Cada uma insere os seus textos, pinta abaixo de X de uma cor
/// e acima de X de outra, apaga, tira as cores e quantifica.
/// </summary>
public class IndependentAnalysisTests
{
    private static readonly RgbColor Vermelho = new(255, 0, 0);
    private static readonly RgbColor Azul = new(0, 0, 255);

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(0.20, Band.Below)]
    [InlineData(0.30, Band.Inside)]
    [InlineData(0.45, Band.Inside)]
    [InlineData(1.20, Band.Inside)]
    [InlineData(1.21, Band.Above)]
    public void ClassificaPelaFaixa(double valor, Band esperada)
    {
        var regra = new ThresholdRule(0.30, Vermelho, 1.20, Azul, PaintPieces: false);

        Assert.Equal(esperada, regra.Classify(valor));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void SoUmLadoLigado()
    {
        var soAbaixo = new ThresholdRule(0.30, Vermelho, null, Azul, false);
        var soAcima = new ThresholdRule(null, Vermelho, 1.20, Azul, false);

        Assert.Equal(Band.Inside, soAbaixo.Classify(9));
        Assert.Equal(Band.Below, soAbaixo.Classify(0.1));
        Assert.Equal(Band.Inside, soAcima.Classify(-5));
        Assert.Equal(Band.Above, soAcima.Classify(1.5));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ACorSaiDaFaixa()
    {
        var regra = new ThresholdRule(0.30, Vermelho, 1.20, Azul, false);

        Assert.Equal(Vermelho, regra.ColorOf(0.1));
        Assert.Equal(Azul, regra.ColorOf(2));
        Assert.Null(regra.ColorOf(0.5));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(1.0, 0.5, "maior")]
    [InlineData(double.NaN, 1.0, "número")]
    [InlineData(null, null, "pelo menos um")]
    public void RegraImpossivelERecusada(double? abaixo, double? acima, string trecho)
    {
        var regra = new ThresholdRule(abaixo, Vermelho, acima, Azul, false);

        Assert.False(regra.IsValid);
        Assert.Contains(trecho, regra.WhyInvalid);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ContaCadaFaixa()
    {
        var regra = new ThresholdRule(0.30, Vermelho, 1.20, Azul, false);

        var conta = BandCount.Of(regra, [0.1, 0.2, 0.5, 0.9, 1.5, double.NaN]);

        Assert.Equal(2, conta.Below);
        Assert.Equal(2, conta.Inside);
        Assert.Equal(1, conta.Above);
        Assert.Equal(1, conta.Missing);
        Assert.Equal(6, conta.Total);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(IndependentKind.LowEdge, 0.452, "PB 0,45")]
    [InlineData(IndependentKind.HighEdge, 2.1, "PA 2,10")]
    [InlineData(IndependentKind.PillarAbove, 1.857, "P 1,86")]
    [InlineData(IndependentKind.PillarBuried, 1.5, "E 1,50")]
    [InlineData(IndependentKind.PillarLength, 3.364, "PT 3,36")]
    public void ORotuloDoTexto(IndependentKind tipo, double valor, string esperado)
    {
        Assert.Equal(esperado, IndependentAnalysis.Label(tipo, valor, SlopeUnit.Percent));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ORotuloDaDeclividadeSegueAUnidade()
    {
        Assert.Equal("5,0%", IndependentAnalysis.Label(IndependentKind.Slope, 5.0, SlopeUnit.Percent));
        Assert.Equal("2,9°", IndependentAnalysis.Label(IndependentKind.Slope, 2.86, SlopeUnit.Degrees));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CadaTipoTemCamadaPropria()
    {
        var camadas = Enum.GetValues<IndependentKind>().Select(IndependentAnalysis.LayerName).ToList();

        Assert.Equal(camadas.Count, camadas.Distinct().Count());
        Assert.All(camadas, c => Assert.StartsWith("CLIVUS_TXT_", c));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ARegraGravadaVoltaIgual()
    {
        var regra = new ThresholdRule(0.30, Vermelho, null, Azul, PaintPieces: true);

        var campos = IndependentAnalysis.Encode(regra);
        var volta = IndependentAnalysis.Decode(campos, IndependentKind.LowEdge);

        Assert.Equal(regra, volta);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void RegraIlegivelVoltaOPadrao()
    {
        Assert.Equal(IndependentAnalysis.Default(IndependentKind.PillarAbove), IndependentAnalysis.Decode(["lixo"], IndependentKind.PillarAbove));
        Assert.Equal(IndependentAnalysis.Default(IndependentKind.Slope), IndependentAnalysis.Decode([], IndependentKind.Slope));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CadaAnaliseDePilarLeOSeuValor()
    {
        // Renan, 02/10/2026: parte livre, parte enterrada e comprimento total são três análises.
        var pilar = new PillarIdentity(Guid.NewGuid(), Guid.NewGuid(), 1, 0, Length: 3.6, Embedment: 1.5, FreeHeight: 2.1, Problem: null, GroundZ: 700);

        Assert.Equal(2.1, IndependentAnalysis.PillarValue(IndependentKind.PillarAbove, pilar));
        Assert.Equal(1.5, IndependentAnalysis.PillarValue(IndependentKind.PillarBuried, pilar));
        Assert.Equal(3.6, IndependentAnalysis.PillarValue(IndependentKind.PillarLength, pilar));

        var semTerreno = pilar with { Length = null, FreeHeight = null, GroundZ = null };
        Assert.All(
            new[] { IndependentKind.PillarAbove, IndependentKind.PillarBuried, IndependentKind.PillarLength },
            t => Assert.True(double.IsNaN(IndependentAnalysis.PillarValue(t, semTerreno))));

        Assert.True(IndependentAnalysis.IsPillar(IndependentKind.PillarBuried));
        Assert.False(IndependentAnalysis.IsPillar(IndependentKind.Slope));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OsPadroesSaoValidos()
    {
        foreach (var tipo in Enum.GetValues<IndependentKind>())
            Assert.True(IndependentAnalysis.Default(tipo).IsValid, tipo.ToString());
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADeclividadeEmCadaUnidade()
    {
        // 1 m de desnível em 20 m: 5% ou 2,86°.
        Assert.Equal(5.0, IndependentAnalysis.SlopeValue(1, 20, SlopeUnit.Percent), 9);
        Assert.Equal(2.8624052, IndependentAnalysis.SlopeValue(-1, 20, SlopeUnit.Degrees), 5);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADescricaoDaContagem()
    {
        var regra = new ThresholdRule(0.30, Vermelho, 1.20, Azul, false);
        var texto = IndependentAnalysis.Describe(IndependentKind.LowEdge, regra, new BandCount(3, 10, 2, 1), SlopeUnit.Percent);

        Assert.Contains("ponta baixa", texto);
        Assert.Contains("3 abaixo de 0,30 m", texto);
        Assert.Contains("2 acima de 1,20 m", texto);
        Assert.Contains("10 dentro", texto);
    }
}

/// <summary>Passo 8.12: a quantificação gravada; 8.13: os estilos do projeto.</summary>
public class AnalysisTallyAndStylesTests
{
    [Fact]
    [Trait("Etapa", "8")]
    public void AQuantificacaoGravadaVoltaIgual()
    {
        var regra = new ThresholdRule(0.30, RgbColor.Red, 1.20, RgbColor.Blue, true);
        var registro = new AnalysisTally(IndependentKind.LowEdge, regra, SlopeUnit.Degrees,
            new BandCount(3, 30, 2, 1), new BandCount(10, 100, 6, 0), new DateTime(2026, 10, 1, 14, 30, 0, DateTimeKind.Local));

        var volta = AnalysisTally.Decode(IndependentKind.LowEdge, registro.Encode());

        Assert.Equal(registro, volta);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void QuantificacaoSemModulos()
    {
        var registro = new AnalysisTally(IndependentKind.Slope, IndependentAnalysis.Default(IndependentKind.Slope), SlopeUnit.Percent,
            new BandCount(0, 70, 10, 0), null, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(registro, AnalysisTally.Decode(IndependentKind.Slope, registro.Encode()));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void QuantificacaoIlegivelENula()
    {
        Assert.Null(AnalysisTally.Decode(IndependentKind.Slope, null));
        Assert.Null(AnalysisTally.Decode(IndependentKind.Slope, ["9"]));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OsEstilosGravadosVoltamIguais()
    {
        Assert.Equal(ProjectStyles.Marcheng, ProjectStyles.Decode(ProjectStyles.Marcheng.Encode()));
        Assert.Equal(ProjectStyles.None, ProjectStyles.Decode(ProjectStyles.None.Encode()));
        Assert.Equal(ProjectStyles.None, ProjectStyles.Decode(["lixo"]));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData("Marchengg Anotativa - Detalhe", "Marchengg Anotativa – Detalhe")]
    [InlineData(" marcheng_anotativa ", "Marcheng_anotativa")]
    public void OEstiloEAchadoMesmoComTravessaoOuMaiuscula(string pedido, string existente)
    {
        Assert.Equal(existente, ProjectStyles.Match(pedido, ["Standard", existente]));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void EstiloQueNaoExisteENulo()
    {
        Assert.Null(ProjectStyles.Match("Outro", ["Standard"]));
        Assert.Null(ProjectStyles.Match(null, ["Standard"]));
    }
}
