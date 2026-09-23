namespace UFV.Core.Tests;

/// <summary>
/// Latitude e longitude são pré-requisito da posição do sol, e portanto do
/// azimute e de qualquer conta de sombreamento. Um sinal trocado põe a usina
/// no hemisfério errado e inverte a orientação das mesas — e não dá erro
/// nenhum, só um resultado que parece plausível.
/// </summary>
public class GeoLocationTests
{
    [Fact]
    [Trait("Etapa", "1")]
    public void UmLugarNoBrasilEValido()
    {
        // Itatiba, São Paulo.
        var lugar = new GeoLocation(-23.0059, -46.8386, GeoLocationSource.Desenho);

        Assert.True(lugar.IsValid);
        Assert.False(lugar.LooksUnset);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [Trait("Etapa", "1")]
    public void ForaDoPlanetaNaoEValido(double latitude, double longitude)
    {
        Assert.False(new GeoLocation(latitude, longitude, GeoLocationSource.Usuario).IsValid);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [Trait("Etapa", "1")]
    public void OsExtremosSaoValidos(double latitude, double longitude)
    {
        Assert.True(new GeoLocation(latitude, longitude, GeoLocationSource.Desenho).IsValid);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void ZeroZeroPareceDesenhoSemGeolocalizacao()
    {
        // É um ponto legítimo do planeta, no golfo da Guiné, mas na prática
        // significa desenho sem geolocalização com os campos por preencher.
        var zerado = new GeoLocation(0, 0, GeoLocationSource.Desenho);

        Assert.True(zerado.IsValid);
        Assert.True(zerado.LooksUnset);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void UmLugarDeVerdadeNaoPareceVazio()
    {
        Assert.False(new GeoLocation(-0.0001, 0.0001, GeoLocationSource.Desenho).LooksUnset);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OTextoDizOHemisferioPorExtenso()
    {
        // Sul e oeste, que é onde fica o Brasil. Um sinal trocado num número
        // solto passa despercebido; um "N" no lugar de "S" não.
        var itatiba = new GeoLocation(-23.0059, -46.8386, GeoLocationSource.Desenho);

        Assert.Equal("23,005900° S, 46,838600° O", itatiba.Describe());
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void NorteELesteTambemSaoDitos()
    {
        var berlim = new GeoLocation(52.52, 13.405, GeoLocationSource.Usuario);

        Assert.Contains("N", berlim.Describe());
        Assert.Contains("L", berlim.Describe());
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AOrigemDoValorFicaRegistrada()
    {
        // Importa saber se o número veio do desenho ou foi digitado: um
        // digitado errado é mais provável, e o usuário precisa poder rever.
        var doDesenho = new GeoLocation(-23, -46, GeoLocationSource.Desenho);
        var digitado = new GeoLocation(-23, -46, GeoLocationSource.Usuario);

        Assert.NotEqual(doDesenho, digitado);
        Assert.Equal(GeoLocationSource.Usuario, digitado.Source);
    }
}
