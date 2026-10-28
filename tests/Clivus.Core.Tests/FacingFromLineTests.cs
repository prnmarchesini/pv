namespace Clivus.Core.Tests;

/// <summary>O azimute pela linha de apoio (Renan, 28/10/2026): os dois desenhos dele.</summary>
public class FacingFromLineTests
{
    private static double Graus(double? radianos) => radianos!.Value * 180 / Math.PI;

    [Fact]
    [Trait("Etapa", "8")]
    public void LinhaDeitadaDaZero()
    {
        Assert.Equal(0, Graus(FacingFromLine.Azimuth(0, 0, 10, 0)), 6);
        Assert.Equal(0, Graus(FacingFromLine.Azimuth(10, 0, 0, 0)), 6);       // o sentido do traço não importa
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void LinhaSubindoVinteGrausDaVinteParaOeste()
    {
        var a = 20 * Math.PI / 180;
        var az = FacingFromLine.Azimuth(0, 0, Math.Cos(a), Math.Sin(a));

        Assert.Equal(340, Graus(az), 6);
        Assert.Equal("a mesa olha 20° para oeste do norte", FacingFromLine.Describe(340));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void LinhaDescendoVinteGrausDaVinteParaLeste()
    {
        var a = -20 * Math.PI / 180;

        Assert.Equal(20, Graus(FacingFromLine.Azimuth(0, 0, Math.Cos(a), Math.Sin(a))), 6);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void FicaALadoMaisPertoDoAzimuteAtual()
    {
        // Com a mesa olhando para o sul (hemisfério norte), a linha deitada dá 180.
        Assert.Equal(180, Graus(FacingFromLine.Azimuth(0, 0, 10, 0, Math.PI)), 6);

        // Linha em pé: leste ou oeste, conforme o atual.
        Assert.Equal(90, Graus(FacingFromLine.Azimuth(0, 0, 0, 10, 80 * Math.PI / 180)), 6);
        Assert.Equal(270, Graus(FacingFromLine.Azimuth(0, 0, 0, 10, 300 * Math.PI / 180)), 6);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void PontosIguaisNaoDaoAzimute()
    {
        Assert.Null(FacingFromLine.Azimuth(5, 5, 5, 5));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OResultadoFicaDeZeroAte360()
    {
        for (var k = 0; k < 72; k++)
        {
            var a = k * 5 * Math.PI / 180;
            var az = FacingFromLine.Azimuth(0, 0, Math.Cos(a), Math.Sin(a))!.Value;
            Assert.InRange(az, 0, 2 * Math.PI - 1e-12);
        }
    }
}
