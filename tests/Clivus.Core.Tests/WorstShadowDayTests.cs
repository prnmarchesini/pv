namespace Clivus.Core.Tests;

/// <summary>
/// O pior dia do ano (42.3, Renan, 31/10/2026: "Solsticio de inverno para
/// quem? Estou no brasil, e quem esta no panama que é o projeto"): o dia de
/// sombra mais longa no lugar do desenho, o do sol mais baixo ao meio-dia.
/// </summary>
public class WorstShadowDayTests
{
    [Theory]
    [Trait("Etapa", "42")]
    [InlineData(8.212, -81.598, -5, 12)]      // o Aurora, no Panamá: dezembro
    [InlineData(-23.01, -46.84, -3, 6)]       // Itatiba: junho
    [InlineData(-3.73, -38.52, -3, 6)]        // Fortaleza, perto do equador mas ao sul: junho
    [InlineData(52.37, 4.9, 1, 12)]           // Amsterdã: dezembro
    public void OPiorDiaEOSolsticioDoLadoDeLaDoEquador(double latitude, double longitude, double fuso, int mes)
    {
        var dia = SolarCalculator.WorstShadowDay(latitude, longitude, 2026, fuso);

        // Os solstícios de 2026: 21/06 e 21/12; um dia para lá ou para cá, pela hora do meio-dia.
        Assert.Equal(mes, dia.Month);
        Assert.InRange(dia.Day, 20, 22);
        Assert.Equal(2026, dia.Year);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void NoPiorDiaOSolDoMeioDiaEOMaisBaixoDoAno()
    {
        // No Panamá, o meio-dia de dezembro fica bem abaixo do de junho (uns 58° contra 75°).
        double Meio(DateOnly d) => Enumerable.Range(0, 145)
            .Max(k => SolarCalculator.Compute(8.212, -81.598, d.ToDateTime(new TimeOnly(10, 0)).AddMinutes(2 * k), -5).ElevationDegrees);

        var pior = SolarCalculator.WorstShadowDay(8.212, -81.598, 2026, -5);
        Assert.InRange(Meio(pior), 57, 60);
        Assert.InRange(Meio(new DateOnly(2026, 6, 21)), 74, 76);
        Assert.True(Meio(pior) < Meio(new DateOnly(2026, 3, 20)));
    }
}
