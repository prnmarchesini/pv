namespace Clivus.Core.Tests;

/// <summary>O texto da análise de declividade da mesa (29/09/2026): porcentagem ou graus, à escolha do Renan.</summary>
public class SlopeLabelTests
{
    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(1.0, 20.0, SlopeUnit.Percent, "5,0%")]
    [InlineData(-1.0, 20.0, SlopeUnit.Percent, "5,0%")]
    [InlineData(1.0, 20.0, SlopeUnit.Degrees, "2,9°")]
    [InlineData(3.2475, 18.418, SlopeUnit.Degrees, "10,0°")]
    [InlineData(0.0, 18.7, SlopeUnit.Percent, "0,0%")]
    [InlineData(18.7, 18.7, SlopeUnit.Percent, "100,0%")]
    [InlineData(18.7, 18.7, SlopeUnit.Degrees, "45,0°")]
    public void EscreveNaUnidadeEscolhida(double desnivel, double emPlanta, SlopeUnit unidade, string esperado)
    {
        Assert.Equal(esperado, SlopeLabel.Format(desnivel, emPlanta, unidade));
    }

    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(0.0, true)]
    [InlineData(0.0009, true)]
    [InlineData(-0.0009, true)]
    [InlineData(0.001, false)]
    [InlineData(0.5, false)]
    public void MesaPlanaNaoLevaSeta(double desnivel, bool plana)
    {
        Assert.Equal(plana, SlopeLabel.IsFlat(desnivel));
    }

    [Theory]
    [Trait("Etapa", "7")]
    [InlineData("PORCENTO", SlopeUnit.Percent)]
    [InlineData("graus", SlopeUnit.Degrees)]
    [InlineData(" GRAUS ", SlopeUnit.Degrees)]
    public void AUnidadeGravadaVoltaIgual(string nome, SlopeUnit unidade)
    {
        Assert.Equal(unidade, SlopeLabel.Parse(nome));
        Assert.Equal(unidade, SlopeLabel.Parse(SlopeLabel.Name(unidade)));
    }

    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%")]
    public void UnidadeDesconhecidaENula(string? nome)
    {
        Assert.Null(SlopeLabel.Parse(nome));
    }

    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, -2.0)]
    [InlineData(double.NaN, 10.0)]
    [InlineData(1.0, double.PositiveInfinity)]
    public void MedidaInvalidaERecusada(double desnivel, double emPlanta)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlopeLabel.Format(desnivel, emPlanta, SlopeUnit.Percent));
    }
}
