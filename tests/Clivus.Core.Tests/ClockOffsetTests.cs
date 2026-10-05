namespace Clivus.Core.Tests;

/// <summary>O fuso do relógio pelo lugar do desenho (05/10/2026: o fuso saiu da janela de sombras).</summary>
public class ClockOffsetTests
{
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(-22.992, -46.814, -3)]   // Itatiba (SP)
    [InlineData(-15.78, -47.93, -3)]     // Brasília
    [InlineData(-3.10, -60.02, -4)]      // Manaus (AM)
    [InlineData(-15.60, -56.10, -4)]     // Cuiabá (MT)
    [InlineData(-9.97, -67.81, -5)]      // Rio Branco (AC)
    [InlineData(-3.84, -32.41, -2)]      // Fernando de Noronha
    [InlineData(40.42, -3.70, 0)]        // Madri: fora do Brasil, pela longitude
    [InlineData(19.43, -99.13, -7)]      // Cidade do México: pela longitude
    public void OFusoSaiDoLugar(double latitude, double longitude, double esperado) =>
        Assert.Equal(esperado, TerrainPlace.ClockOffsetHours(latitude, longitude));
}
