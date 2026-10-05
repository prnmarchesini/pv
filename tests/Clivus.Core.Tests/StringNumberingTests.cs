namespace Clivus.Core.Tests;

/// <summary>Numeração das strings (elétrica, etapa 15): a composição da tag.</summary>
public class StringNumberingTests
{
    // ------------------------------------------------------------ 15.1 tag

    [Fact]
    [Trait("Etapa", "15")]
    public void ATagPadraoEhTrafoInversorStringComPonto()
    {
        Assert.Equal("T1.I1.S1", TagScheme.Default.Compose(1, 1, 1));
        Assert.Equal("T2.I13.S7", TagScheme.Default.Compose(2, 13, 7));
        Assert.Null(TagScheme.Default.Problem());
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void CadaPedacoTemOSeuPrefixoEOSeparadorEhOMesmo()
    {
        var risco = new TagScheme(true, "Trafo", "Inv", "S", "-");
        Assert.Equal("Trafo3-Inv2-S10", risco.Compose(3, 2, 10));

        // 1S1: sem o pedaço do trafo, inversor sem prefixo, colado.
        var colado = new TagScheme(false, "T", "", "S", "");
        Assert.Null(colado.Problem());
        Assert.Equal("1S1", colado.Compose(4, 1, 1));
        Assert.Equal("12S3", colado.Compose(null, 12, 3));

        var coladoComTrafo = new TagScheme(true, "T", "I", "S", "");
        Assert.Equal("T1I2S3", coladoComTrafo.Compose(1, 2, 3));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void InversorSemTrafoFicaSemOPedacoDoTrafo()
    {
        // Decisão de 04/10/2026: o pedaço do trafo some (com o separador),
        // em vez de um "T0" que parece um trafo de verdade.
        Assert.Equal("I4.S2", TagScheme.Default.Compose(null, 4, 2));
    }

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData(true, "T", "", "S", "")]      // T1 + 2 colados: T12S3 é T1.I2 ou T12?
    [InlineData(false, "", "I", "", "")]      // I2 + 3 colados
    [InlineData(true, "T", "I", "S", "/")]    // separador fora da lista
    [InlineData(true, "T1", "I", "S", ".")]   // prefixo terminando em algarismo
    [InlineData(true, "T", "I{", "S", ".")]   // caractere de formatação do texto
    [InlineData(true, "T", "I", "S\\", ".")]
    [InlineData(true, "T", "I", "Stringcomprida", ".")] // mais de 12 caracteres
    public void ComposicaoAmbiguaOuInvalidaEhRecusada(bool trafo, string t, string i, string s, string separador)
    {
        var esquema = new TagScheme(trafo, t, i, s, separador);
        Assert.NotNull(esquema.Problem());
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void NumeroForaDaFaixaNaoViraTag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(1, 1, 0));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OEsquemaVaiEVoltaDosCampos()
    {
        var esquema = new TagScheme(false, "Trafo", "", "S", "");
        Assert.Equal(TagScheme.FieldCount, esquema.ToFields().Count);
        Assert.Equal(esquema, TagScheme.Parse(esquema.ToFields()));
        Assert.Equal(TagScheme.Default, TagScheme.Parse(TagScheme.Default.ToFields()));

        // Gravado estragado (separador que não existe) não volta.
        var estragado = TagScheme.Default.ToFields().ToArray();
        estragado[4] = "#";
        Assert.Null(TagScheme.Parse(estragado));
    }
}
