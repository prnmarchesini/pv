namespace Clivus.Core.Tests;

/// <summary>Passo 26: as faixas, a grade e a forma das flechas do sentido da água.</summary>
public class DrainageArrowsTests
{
    [Fact]
    [Trait("Etapa", "26")]
    public void AsFaixasPadraoSaoAsDaFigura3DoMemorial()
    {
        var faixas = DrainageArrows.Bands(DrainageArrows.DefaultLimits);

        Assert.Equal(5, faixas.Count);
        Assert.Equal(["0 – 5%", "5 – 10%", "10 – 15%", "15 – 20%", "> 20%"], faixas.Select(f => f.Label));
        Assert.Equal(new RgbColor(0, 200, 0), faixas[0].Color);
        Assert.Equal(double.PositiveInfinity, faixas[^1].UpToPercent);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(4.99, 0)]
    [InlineData(5.0, 0)]
    [InlineData(5.01, 1)]
    [InlineData(12, 2)]
    [InlineData(20, 3)]
    [InlineData(37, 4)]
    [Trait("Etapa", "26")]
    public void ODeclivePegaAFaixaCerta(double porcento, int faixa)
    {
        Assert.Equal(faixa, DrainageArrows.BandOf(porcento, DrainageArrows.Bands(DrainageArrows.DefaultLimits)));
    }

    [Theory]
    [InlineData(new double[] { 5, 5, 15, 20 })]
    [InlineData(new double[] { 5, 10, 8, 20 })]
    [InlineData(new double[] { 0, 10, 15, 20 })]
    [InlineData(new double[] { 5, 10, 15 })]
    [Trait("Etapa", "26")]
    public void LimitesQueNaoSobemSaoRecusados(double[] limites)
    {
        Assert.Throws<ArgumentException>(() => DrainageArrows.Bands(limites));
    }

    [Fact]
    [Trait("Etapa", "26")]
    public void AGradeEAlinhadaNoEspacamentoERespeitaODentro()
    {
        var pontos = DrainageArrows.GridPoints(0, 0, 20, 10, 5, (x, _) => x < 15).ToList();

        // Centros de célula: x = 2,5; 7,5; 12,5 (17,5 fica fora pelo dentro), y = 2,5; 7,5.
        Assert.Equal(6, pontos.Count);
        Assert.Contains((2.5, 2.5), pontos);
        Assert.Contains((12.5, 7.5), pontos);

        // Começar a janela fora do múltiplo não muda os pontos de dentro.
        var deslocada = DrainageArrows.GridPoints(1, 1, 20, 10, 5, (x, _) => x < 15).ToList();
        Assert.Equal(pontos, deslocada);
    }

    [Fact]
    [Trait("Etapa", "26")]
    public void AFlechaApontaParaODirecaoComAbasSimetricas()
    {
        var f = DrainageArrows.Shape(10, 10, 1, 0, 5);

        Assert.Equal((8.25, 10.0), f.Tail);
        Assert.Equal((11.75, 10.0), f.Tip);

        // As abas ficam atrás da ponta e espelhadas no eixo.
        Assert.True(f.WingLeft.X < f.Tip.X && f.WingRight.X < f.Tip.X);
        Assert.Equal(f.WingLeft.X, f.WingRight.X, 9);
        Assert.Equal(10 - f.WingLeft.Y, f.WingRight.Y - 10, 9);
    }
}
