using System.Globalization;

namespace Clivus.Core.Tests;

/// <summary>O mecanismo de tradução (10.1).</summary>
public class TrTests
{
    private static readonly Dictionary<string, string> Ingles = new()
    {
        ["Olá"] = "Hello",
        ["{0} mesa(s) com {1:0.0} kWp"] = "{0} table(s) with {1:0.0} kWp",
    };

    [Fact]
    [Trait("Etapa", "10")]
    public void EmPortuguesAFraseVoltaComoEsta()
    {
        using var _ = Tr.Use(UiLanguage.Portuguese, Ingles);

        Assert.Equal("\nOlá\n", Tr.T("\nOlá\n"));
        Assert.Equal("3 mesa(s) com 12,5 kWp", Tr.F("{0} mesa(s) com {1:0.0} kWp", 3, 12.5));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void EmInglesTraduzMantendoAsPontasEUsaPontoDecimal()
    {
        using var _ = Tr.Use(UiLanguage.English, Ingles);

        Assert.Equal("\n  Hello\n", Tr.T("\n  Olá\n"));
        Assert.Equal("3 table(s) with 12.5 kWp", Tr.F("{0} mesa(s) com {1:0.0} kWp", 3, 12.5));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void SemTraducaoVoltaEmPortuguesEFicaAnotada()
    {
        using var _ = Tr.Use(UiLanguage.Spanish, new Dictionary<string, string>());

        Assert.Equal("Frase que ninguém traduziu", Tr.T("Frase que ninguém traduziu"));
        Assert.Contains("Frase que ninguém traduziu", Tr.Missing.Keys);

        // Espanhol: vírgula decimal, mesmo sem tradução.
        Assert.Equal("1,5", Tr.F("{0:0.0}", 1.5));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void OIdiomaDoFluxoNaoVazaEVoltaNoFim()
    {
        var antes = Tr.Current;

        using (Tr.Use(UiLanguage.English, Ingles))
        {
            Assert.Equal(UiLanguage.English, Tr.Current);
            Assert.Equal(".", Tr.Culture.NumberFormat.NumberDecimalSeparator);
        }

        Assert.Equal(antes, Tr.Current);
    }

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData("en", "pt-BR", UiLanguage.English)]
    [InlineData("es", "en-US", UiLanguage.Spanish)]
    [InlineData("pt", "en-US", UiLanguage.Portuguese)]
    [InlineData("auto", "en-US", UiLanguage.English)]
    [InlineData("auto", "es-MX", UiLanguage.Spanish)]
    [InlineData("auto", "pt-BR", UiLanguage.Portuguese)]
    [InlineData(null, "de-DE", UiLanguage.English)]
    [InlineData("", "es-ES", UiLanguage.Spanish)]
    public void OIdiomaVemDaEscolhaOuDoCivil3D(string? escolha, string cultura, UiLanguage esperado) =>
        Assert.Equal(esperado, Tr.Resolve(escolha, CultureInfo.GetCultureInfo(cultura)));

    [Fact]
    [Trait("Etapa", "10")]
    public void OsCatalogosEmbutidosCarregam()
    {
        Assert.NotNull(Tr.CatalogOf(UiLanguage.English));
        Assert.NotNull(Tr.CatalogOf(UiLanguage.Spanish));
        Assert.Empty(Tr.CatalogOf(UiLanguage.Portuguese));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void SemCulturaDoProdutoFicaEmPortugues() =>
        Assert.Equal(UiLanguage.Portuguese, Tr.Resolve("auto", null));

    [Fact]
    [Trait("Etapa", "10")]
    public void AsPreferenciasGuardamOIdiomaEToleramArquivoEstragado()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clivus-pref-" + Guid.NewGuid().ToString("N"));
        var arquivo = Path.Combine(pasta, "preferencias.json");

        try
        {
            Assert.Equal("auto", UserPreferences.Load(arquivo).Language);

            new UserPreferences("es").Save(arquivo);
            Assert.Equal("es", UserPreferences.Load(arquivo).Language);

            File.WriteAllText(arquivo, "{ isto não é json");
            Assert.Equal("auto", UserPreferences.Load(arquivo).Language);

            File.WriteAllText(arquivo, "{\"idioma\": \"klingon\"}");
            Assert.Equal("auto", UserPreferences.Load(arquivo).Language);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData("PTB", UiLanguage.Portuguese)]
    [InlineData("ENU", UiLanguage.English)]
    [InlineData("esp", UiLanguage.Spanish)]
    [InlineData("DEU", UiLanguage.English)]
    [InlineData("XYZ", UiLanguage.Portuguese)]
    public void OLocaleDoCivil3DEscolheOIdiomaNoAutomatico(string locale, UiLanguage esperado) =>
        Assert.Equal(esperado, Tr.Resolve("auto", UserPreferences.CultureOfAutoCadLocale(locale)));

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData(UiLanguage.Portuguese, "pt-BR")]
    [InlineData(UiLanguage.English, "en")]
    [InlineData(UiLanguage.Spanish, "es")]
    public void OPedidoAoServidorLevaOIdiomaDaTela(UiLanguage idioma, string esperado)
    {
        using var _ = Tr.Use(idioma);
        Assert.Equal(esperado, Tr.AcceptLanguage);
    }
}
