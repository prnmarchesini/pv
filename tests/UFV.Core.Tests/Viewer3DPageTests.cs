using UFV.Geo;

namespace UFV.Core.Tests;

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
}
