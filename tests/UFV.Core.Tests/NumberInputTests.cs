namespace UFV.Core.Tests;

/// <summary>
/// O pedaço mais sujeito a erro silencioso da janela: texto virando número.
///
/// Estava preso dentro do controle de interface, sem teste nenhum, e a revisão
/// do 3.7 mostrou por que isso é caro: "1.500" no campo de potência virava
/// 1,5 Wp, passava na validação, e a usina saía com a potência dividida por
/// mil sem uma linha de aviso.
/// </summary>
public class NumberInputTests
{
    // -------------------------------------------------- medida em metro

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("2,384", 2.384)]
    [InlineData("2.384", 2.384)]
    [InlineData("0,02", 0.02)]
    [InlineData("0.02", 0.02)]
    [InlineData(",5", 0.5)]
    [InlineData(".5", 0.5)]
    [InlineData("3", 3)]
    [InlineData("  3  ", 3)]
    [InlineData("0", 0)]
    [InlineData("-0,5", -0.5)]
    public void AMedidaAceitaVirgulaEPonto(string texto, double esperado)
    {
        Assert.True(NumberInput.TryParseMeasure(texto, out var valor));
        Assert.Equal(esperado, valor, 9);
    }

    /// <summary>
    /// Numa medida de mesa o ponto é decimal, sempre: nada que a mesa mede
    /// chega a mil metros, então "1.500" só pode ser um e meio.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void NaMedidaOPontoEDecimalMesmoComTresCasas()
    {
        Assert.True(NumberInput.TryParseMeasure("1.500", out var valor));
        Assert.Equal(1.5, valor, 9);
    }

    /// <summary>
    /// Com vírgula presente, o ponto volta a ser milhar — é assim que um
    /// número colado de planilha entra inteiro.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("1.303,5", 1303.5)]
    [InlineData("1.500,25", 1500.25)]
    public void ComVirgulaOPontoEMilhar(string texto, double esperado)
    {
        Assert.True(NumberInput.TryParseMeasure(texto, out var valor));
        Assert.Equal(esperado, valor, 9);
    }

    // ------------------------------------------- grandeza da casa do milhar

    /// <summary>
    /// O caso que a revisão achou: no campo de potência, "1.500" é mil e
    /// quinhentos, e não um e meio.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("720", 720)]
    [InlineData("1.500", 1500)]
    [InlineData("1500", 1500)]
    [InlineData("1.500,5", 1500.5)]
    [InlineData("720,5", 720.5)]
    public void NaPotenciaOPontoEMilhar(string texto, double esperado)
    {
        Assert.True(NumberInput.TryParseLarge(texto, out var valor));
        Assert.Equal(esperado, valor, 9);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("28", 28)]
    [InlineData("1.000", 1000)]
    [InlineData("  28  ", 28)]
    public void AContagemLeInteiro(string texto, int esperado)
    {
        Assert.True(NumberInput.TryParseCount(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    /// <summary>
    /// Contagem quebrada não é contagem. Truncar em silêncio faria "28,5
    /// módulos" virar 28 sem o projetista saber que digitou errado.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("28,5")]
    [InlineData("28.5")]
    [InlineData("1e30")]
    public void ContagemQuebradaOuEnormeNaoELida(string texto)
    {
        Assert.False(NumberInput.TryParseCount(texto, out _));
    }

    // ------------------------------------------------------- o que recusar

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    [InlineData("--3")]
    [InlineData("3 m")]
    public void TextoQueNaoENumeroNaoELido(string? texto)
    {
        Assert.False(NumberInput.TryParseMeasure(texto, out _));
        Assert.False(NumberInput.TryParseLarge(texto, out _));
    }

    /// <summary>
    /// NaN e infinito são lidos como número pelo .NET. Quem os recusa é a
    /// validação de cada campo, e este teste existe para deixar registrado que
    /// não é aqui — se um dia alguém confiar neste método para barrá-los, vai
    /// descobrir tarde.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("NaN")]
    [InlineData("∞")]
    public void NaoFinitoPassaAquiEEBarradoAdiante(string texto)
    {
        Assert.True(NumberInput.TryParseMeasure(texto, out var valor));
        Assert.False(double.IsFinite(valor));
    }

    /// <summary>
    /// A leitura não depende da cultura da máquina: o campo é sempre lido como
    /// português, porque é isso que o projetista digita.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ALeituraNaoDependeDaCulturaDaMaquina()
    {
        var antes = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("en-US");

            Assert.True(NumberInput.TryParseMeasure("2,384", out var valor));
            Assert.Equal(2.384, valor, 9);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = antes;
        }
    }
}
