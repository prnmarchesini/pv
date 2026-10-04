using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>A página 3D no navegador (9.9).</summary>
public class Viewer3DPageTests
{
    private static Scene3D Cena()
    {
        double? Rampa(double x, double y) => x < 314_000 + 95 ? 700 + 0.1 * (y - 7_456_000) : null;

        var terreno = Viewer3DPage.SampleTerrain(314_000, 7_456_000, 314_100, 7_456_050, Rampa, maxCells: 50);

        return new Scene3D(
            "Usina \"Teste\" </script>",
            terreno,
            [new Scene3DFace([new(314_010, 7_456_010, 702), new(314_012, 7_456_010, 702), new(314_012, 7_456_011, 702.5), new(314_010, 7_456_011, 702.5)], new RgbColor(255, 128, 0))],
            [new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699)],
            [new Scene3DTree(314_050, 7_456_020, 702, TreeSpec.Default)],
            [[new(314_050, 7_456_020, 702), new(314_055, 7_456_025, 702.5), new(314_045, 7_456_025, 702.5)]]);
    }

    /// <summary>
    /// Módulo e pilar sem terreno embaixo e fora da faixa de cotas (no desenho,
    /// planos na cota 0) ficam fora da cena; o da beira, na altura das
    /// vizinhas, fica; a origem volta à cota da usina.
    /// </summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void ModuloEPilarSemTerrenoEmbaixoFicamForaDaCena()
    {
        double? Chao(double x, double y) => x < 314_000 + 95 ? 700 : null;

        var cena = Cena();
        var solto = new Scene3DFace([new(314_097, 7_456_010, 0), new(314_099, 7_456_010, 0), new(314_099, 7_456_011, 0.815), new(314_097, 7_456_011, 0.815)], new RgbColor(255, 0, 255));
        var meioFora = new Scene3DFace([new(314_093, 7_456_012, 0), new(314_098, 7_456_012, 0), new(314_098, 7_456_013, 0.5), new(314_093, 7_456_013, 0.5)], new RgbColor(255, 0, 255));
        // Na beira: sem terreno embaixo, mas na altura das vizinhas. Fica.
        var naBeira = new Scene3DFace([new(314_096, 7_456_020, 702), new(314_098, 7_456_020, 702), new(314_098, 7_456_021, 702.5), new(314_096, 7_456_021, 702.5)], new RgbColor(0, 160, 0));
        var comSoltos = cena with
        {
            Faces = [.. cena.Faces, solto, meioFora, naBeira],
            Pillars = [.. cena.Pillars, new Scene3DPillar(314_098, 7_456_010.5, 0.5, 0)],
        };

        Assert.Equal(0, Viewer3DPage.Origin(comSoltos).Z, 9);

        var (limpa, modulos, pilares) = Viewer3DPage.OnTerrain(comSoltos, Chao);

        Assert.Equal(2, modulos);
        Assert.Equal(1, pilares);
        Assert.Equal([.. cena.Faces, naBeira], limpa.Faces);
        Assert.Equal(cena.Pillars, limpa.Pillars);
        Assert.Equal(cena.Trees, limpa.Trees);
        Assert.Equal(cena.Shadows, limpa.Shadows);
        Assert.Same(cena.Terrain, limpa.Terrain);
        Assert.Equal(Viewer3DPage.Origin(cena), Viewer3DPage.Origin(limpa));
        Assert.True(Viewer3DPage.Origin(limpa).Z > 690);

        // Tudo com chão: nada sai.
        var (igual, nenhum, nenhumPilar) = Viewer3DPage.OnTerrain(cena, Chao);
        Assert.Equal((0, 0), (nenhum, nenhumPilar));
        Assert.Equal(cena.Faces, igual.Faces);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OTerrenoViraUmaGradeDeNoMaximoNCelulas()
    {
        var t = Cena().Terrain!;

        Assert.Equal(2.0, t.Step);
        Assert.Equal(51, t.Columns);
        Assert.Equal(26, t.Rows);
        Assert.Equal(t.Columns * t.Rows, t.Z.Count);
        Assert.Equal(700, t.Z[0]!.Value, 9);
        Assert.Null(t.Z[50]);                                 // x = 100 m: fora do terreno
        Assert.Equal(705, t.Z[25 * 51]!.Value, 9);           // y = 50 m

        // Terreno pequeno: nunca menos de 1 m entre pontos.
        Assert.Equal(1.0, Viewer3DPage.SampleTerrain(0, 0, 10, 10, (_, _) => 0, maxCells: 200).Step);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AsCoordenadasVaoRelativasAoCentro()
    {
        var cena = Cena();
        var o = Viewer3DPage.Origin(cena);

        Assert.Equal(314_050, o.X, 9);
        Assert.Equal(7_456_025, o.Y, 9);
        Assert.Equal(700, o.Z, 9);

        var json = Viewer3DPage.Json(cena);
        Assert.Contains("\"origem\":[314050,7456025,700]", json, StringComparison.Ordinal);
        Assert.Contains("\"faces\":[[-40,-15,2,-38,-15,2,-38,-14,2.5,-40,-14,2.5,16744448]]", json, StringComparison.Ordinal);
        Assert.Contains("\"pilares\":[[-39,-14.5,1.5,-1]]", json, StringComparison.Ordinal);
        Assert.Contains("\"arvores\":[[0,-5,2,3,0.4,5,6]]", json, StringComparison.Ordinal);
        Assert.Contains("\"titulo\":\"Usina \\\"Teste\\\" \\u003c/script>\"", json, StringComparison.Ordinal);
    }

    /// <summary>Um arquivo só, sem nada de fora: a three.js embutida e nenhum script buscado na internet.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void APaginaEUmArquivoSoQueAbreSemInternet()
    {
        var html = Viewer3DPage.Html(Cena());

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.Contains("Copyright 2010-2021 Three.js Authors", html, StringComparison.Ordinal);
        Assert.Contains("THREE.OrbitControls = OrbitControls", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", html.Replace("${", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        // O título no HTML vai escapado, e o "</script>" do título não fecha o script da cena.
        Assert.Contains("<title>Usina &quot;Teste&quot; &lt;/script&gt; — 3D</title>", html, StringComparison.Ordinal);
        Assert.Equal(3, html.Split("</script>").Length - 1);
    }

    /// <summary>O corpo do envio é o do contrato: versão, plugin, desenho e a cena da página local com a origem zerada.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void OCorpoDoEnvioSegueOContrato()
    {
        var cena = Cena();
        var corpo = Viewer3DPage.PublishBody(cena, "0.1.0", "Itatiba \"A\"");

        using var json = System.Text.Json.JsonDocument.Parse(corpo);
        var raiz = json.RootElement;

        Assert.Equal(1, raiz.GetProperty("versao").GetInt32());
        Assert.Equal("0.1.0", raiz.GetProperty("plugin").GetString());
        Assert.Equal("Itatiba \"A\"", raiz.GetProperty("desenho").GetString());
        Assert.Equal(Viewer3DPage.Json(cena, comOrigem: false), raiz.GetProperty("cena").GetRawText());
        Assert.Equal(1, raiz.GetProperty("cena").GetProperty("faces").GetArrayLength());

        // A coordenada real da usina não sai do computador: origem zerada e
        // as mesmas coordenadas relativas da página local.
        Assert.Equal("[0,0,0]", raiz.GetProperty("cena").GetProperty("origem").GetRawText());
        using var local = System.Text.Json.JsonDocument.Parse(Viewer3DPage.Json(cena));
        Assert.NotEqual("[0,0,0]", local.RootElement.GetProperty("origem").GetRawText());
        Assert.Equal(local.RootElement.GetProperty("faces").GetRawText(), raiz.GetProperty("cena").GetProperty("faces").GetRawText());
        Assert.DoesNotContain("314050", corpo, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ARespostaDoServidorViraLinkOuErro()
    {
        var (ok, erro) = Viewer3DPage.ParseResponse(201, "{\"id\":\"k3f9x2\",\"url\":\"https://x.sslip.io/3d/k3f9x2\",\"expira_em\":\"2026-11-03T12:00:00Z\"}");
        Assert.Null(erro);
        Assert.Equal("k3f9x2", ok!.Id);
        Assert.Equal("https://x.sslip.io/3d/k3f9x2", ok.Url);
        Assert.Equal(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc), ok.ExpiresAt);

        Assert.Equal("a cena não tem faces", Viewer3DPage.ParseResponse(400, "{\"erro\":\"a cena não tem faces\"}").Erro);
        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", Viewer3DPage.ParseResponse(401, "").Erro, StringComparison.Ordinal);
        Assert.Contains("grande demais", Viewer3DPage.ParseResponse(413, "<html>").Erro, StringComparison.Ordinal);
        Assert.Equal("o servidor respondeu 502", Viewer3DPage.ParseResponse(502, "Bad Gateway").Erro);
        Assert.Equal("o servidor respondeu sem o link da página 3D", Viewer3DPage.ParseResponse(200, "{\"id\":\"a\",\"url\":\"javascript:alert(1)\"}").Erro);
        Assert.Null(Viewer3DPage.ParseResponse(201, "{\"id\":\"a\",\"url\":\"https://x/3d/a\"}").Publicada!.ExpiresAt);
    }

    /// <summary>Segurança (plano/seguranca.md): só HTTPS (http em localhost), e só abre link do próprio servidor.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void SoHttpsESoLinkDoProprioServidor()
    {
        Assert.Null(Viewer3DPage.WhyServerUnsafe("https://clivus.exemplo.com.br"));
        Assert.Null(Viewer3DPage.WhyServerUnsafe("http://127.0.0.1:18765"));
        Assert.Null(Viewer3DPage.WhyServerUnsafe("http://localhost:8000"));
        Assert.NotNull(Viewer3DPage.WhyServerUnsafe("http://clivus.exemplo.com.br"));
        Assert.NotNull(Viewer3DPage.WhyServerUnsafe("ftp://x"));
        Assert.NotNull(Viewer3DPage.WhyServerUnsafe(null));

        const string servidor = "https://clivus.exemplo.com.br";
        Assert.True(Viewer3DPage.IsLinkFromServer("https://clivus.exemplo.com.br/3d/abc", servidor));
        Assert.False(Viewer3DPage.IsLinkFromServer("https://clivus.exemplo.com.br.golpe.com/3d/abc", servidor));
        Assert.False(Viewer3DPage.IsLinkFromServer("http://clivus.exemplo.com.br/3d/abc", servidor));
        Assert.False(Viewer3DPage.IsLinkFromServer("https://outro.com/3d/abc", servidor));
        Assert.False(Viewer3DPage.IsLinkFromServer("https://x.com/3d/abc", "http://x.com"));
    }

    /// <summary>Um desenho chamado "x{{ORBITA}}" não faz o script entrar no título.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void OTituloNaoViraLugarDeScript()
    {
        var html = Viewer3DPage.Html(Cena() with { Title = "x{{ORBITA}}{{THREE}}" });

        Assert.Contains("<title>x{{ORBITA}}{{THREE}} — 3D</title>", html, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "THREE.OrbitControls = OrbitControls"));
    }
}
