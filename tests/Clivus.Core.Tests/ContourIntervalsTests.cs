namespace Clivus.Core.Tests;

public class ContourIntervalsTests
{
    [Theory]
    [Trait("Etapa", "33")]
    [InlineData(3.736, 0.2, 1.0)]       // a Área 2 da Paradise
    [InlineData(1.5, 0.1, 0.5)]
    [InlineData(4.5, 0.25, 1.0)]
    [InlineData(30, 2.0, 10.0)]
    [InlineData(5000, 10.0, 50.0)]
    [InlineData(0, 0.25, 1.0)]
    [InlineData(double.NaN, 0.25, 1.0)]
    public void OIntervaloSegueODesnivel(double desnivel, double secundaria, double mestra)
    {
        var (minor, major) = ContourIntervals.For(desnivel);

        Assert.Equal(secundaria, minor, 9);
        Assert.Equal(mestra, major, 9);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void AMestraEMultiploDaSecundaria()
    {
        foreach (var d in new[] { 0.5, 2, 4, 8, 15, 40, 90, 300, 800 })
        {
            var (minor, major) = ContourIntervals.For(d);
            var vezes = major / minor;
            Assert.Equal(Math.Round(vezes), vezes, 9);
        }
    }
}
