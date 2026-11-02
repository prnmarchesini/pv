using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A rosa dos ventos do 3D (Renan, 02/11/2026: "preciso que os 3Ds gerados
/// tenham a rosa do vento, e conforme vou virando o desenho ela vai girando
/// tambem"; "ela aponta para a seta que eu clicar, tipo, leste, ela vira para
/// o leste (o leste fica pra cima)"; "e se clicar de novo ela solta"). O giro
/// e a trava são do rosa.js, conferidos no navegador; aqui, que a página
/// local leva a rosa com os textos no idioma do plugin e que o laço anda a
/// rosa antes do update dos controles, que é onde a trava do azimute pega.
/// </summary>
public class Viewer3DCompassTests
{
    private static Scene3D Cena() => new(
        "Usina",
        Viewer3DPage.SampleTerrain(314_000, 7_456_000, 314_100, 7_456_050, (_, _) => 700, maxCells: 50),
        [new Scene3DFace([new(314_010, 7_456_010, 702), new(314_012, 7_456_010, 702), new(314_012, 7_456_011, 702.5), new(314_010, 7_456_011, 702.5)], new RgbColor(30, 60, 140))],
        [new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699)],
        [new Scene3DTree(314_050, 7_456_020, 702, TreeSpec.Default)],
        []);

    [Fact]
    [Trait("Etapa", "42")]
    public void APaginaLocalLevaARosaDosVentos()
    {
        using var idioma = Tr.Use(UiLanguage.Portuguese);
        var html = Viewer3DPage.Html(Cena());

        Assert.Contains("<div id=\"rosa\" data-titulo=\"Rosa dos ventos\" data-letras=\"N L S O\" data-rumos=\"norte|leste|sul|oeste\"", html, StringComparison.Ordinal);
        Assert.Contains("data-travado=\"Giro travado com o {rumo} para cima. Para soltar, clique de novo no {letra}.\" data-solto=\"Giro solto.\"", html, StringComparison.Ordinal);

        // O rosa.js embutido e montado nos controles; no laço, antes do update.
        Assert.Contains("raiz.ClivusRosa = api", html, StringComparison.Ordinal);
        Assert.Contains("const rosa = ClivusRosa.montar(controles);", html, StringComparison.Ordinal);
        Assert.Contains("if (rosa) rosa.quadro(); controles.update();", html, StringComparison.Ordinal);

        // As camadas param antes da rosa, no canto de baixo.
        Assert.Contains("max-height: calc(100% - 132px)", html, StringComparison.Ordinal);

        // Continua um arquivo só: os três scripts, nenhuma marca sobrando.
        Assert.Equal(3, html.Split("</script>").Length - 1);
        Assert.DoesNotContain("{{", html.Replace("${", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Etapa", "42")]
    [InlineData(UiLanguage.English, "<div id=\"rosa\" data-titulo=\"Compass rose\" data-letras=\"N E S W\" data-rumos=\"north|east|south|west\" "
        + "data-travar=\"Turn {rumo} to the top and lock the rotation\" data-soltar=\"Unlock the rotation\" "
        + "data-travado=\"Rotation locked with {rumo} at the top. To unlock, click {letra} again.\" data-solto=\"Rotation unlocked.\"></div>")]
    [InlineData(UiLanguage.Spanish, "<div id=\"rosa\" data-titulo=\"Rosa de los vientos\" data-letras=\"N E S O\" data-rumos=\"norte|este|sur|oeste\" "
        + "data-travar=\"Poner el {rumo} arriba y bloquear el giro\" data-soltar=\"Desbloquear el giro\" "
        + "data-travado=\"Giro bloqueado con el {rumo} arriba. Para desbloquear, haga clic de nuevo en la {letra}.\" data-solto=\"Giro desbloqueado.\"></div>")]
    public void ARosaFalaOIdiomaDoPlugin(UiLanguage lingua, string esperado)
    {
        using var idioma = Tr.Use(lingua);

        // A rosa inteira no idioma, com os campos {rumo} e {letra} intactos para o rosa.js.
        Assert.Contains(esperado, Viewer3DPage.Html(Cena()), StringComparison.Ordinal);
    }
}
