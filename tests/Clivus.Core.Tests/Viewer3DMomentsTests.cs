using System.Text.Json;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O 3D do 42.3 (tela do Renan, 31/10/2026): o terreno de verdade em
/// triângulos ("comeu módulo, é mentira, não comeu no cad"), os momentos da
/// análise de sombras do CAD com a pior sombra de cada módulo ("deveria no
/// mínimo ter um botão 'ver momento que causa sombras'") e a hachura de cada
/// anel de sombra, para o navegador preencher com os furos.
/// </summary>
public class Viewer3DMomentsTests
{
    private static readonly IReadOnlyList<Point3> Cantos =
        [new(314_010, 7_456_010, 702), new(314_012, 7_456_010, 702), new(314_012, 7_456_011, 702.5), new(314_010, 7_456_011, 702.5)];

    private static Scene3D Cena() => new(
        "Usina",
        Viewer3DPage.SampleTerrain(314_000, 7_456_000, 314_100, 7_456_050, (_, _) => 700, maxCells: 50),
        [new Scene3DFace(Cantos, new RgbColor(30, 60, 140))],
        [new Scene3DPillar(314_011, 7_456_010.5, 701.5, 699)],
        [],
        []);

    private static JsonElement Ler(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    [Trait("Etapa", "42")]
    public void ACenaSemTinNemMomentosContinuaADeAntes()
    {
        var cena = Ler(Viewer3DPage.Json(Cena()));

        Assert.False(cena.GetProperty("terreno").TryGetProperty("tin", out _));
        Assert.False(cena.TryGetProperty("momentos", out _));
        Assert.False(cena.TryGetProperty("sombrasGrupo", out _));
        Assert.Equal(13, cena.GetProperty("faces")[0].GetArrayLength());
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OTinVaiComOsVerticesJuntadosEOsTriangulos()
    {
        // Dois triângulos com um lado em comum: quatro vértices, seis posições.
        Point3 a = new(314_000, 7_456_000, 700), b = new(314_100, 7_456_000, 701), c = new(314_100, 7_456_050, 702), d = new(314_000, 7_456_050, 703);
        var tin = Scene3DTin.From([new Triangle(a, b, c), new Triangle(a, c, d)]);

        Assert.NotNull(tin);
        Assert.Equal([a, b, c, d], tin.Vertices);
        Assert.Equal([0, 1, 2, 0, 2, 3], tin.Triangles);

        // Acima do teto, só a grade; sem triângulo, nada.
        Assert.Null(Scene3DTin.From([new Triangle(a, b, c), new Triangle(a, c, d)], maxTriangles: 1));
        Assert.Null(Scene3DTin.From([]));

        var cena = Cena();
        var json = Ler(Viewer3DPage.Json(cena with { Terrain = cena.Terrain! with { Tin = tin } }));
        var origem = json.GetProperty("origem");
        var v = json.GetProperty("terreno").GetProperty("tin").GetProperty("v").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var t = json.GetProperty("terreno").GetProperty("tin").GetProperty("t").EnumerateArray().Select(e => e.GetInt32()).ToList();

        // Relativos à origem, como as faces.
        Assert.Equal(12, v.Count);
        Assert.Equal(a.X - origem[0].GetDouble(), v[0], 3);
        Assert.Equal(d.Z - origem[2].GetDouble(), v[11], 3);
        Assert.Equal([0, 1, 2, 0, 2, 3], t);

        // A grade continua indo (o visualizador antigo e a caixa da cena).
        Assert.True(json.GetProperty("terreno").GetProperty("z").GetArrayLength() > 0);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AFaceComSombraLevaOMomentoEAFracao()
    {
        var momentos = new List<Scene3DMoment> { new(new DateTime(2026, 6, 21, 7, 30, 0), 12), new(new DateTime(2026, 6, 21, 8, 0, 0), 3) };
        var cena = Cena() with
        {
            Faces =
            [
                new Scene3DFace(Cantos, new RgbColor(30, 60, 140), new Scene3DFaceShadow(1, 0.45678)),
                new Scene3DFace(Cantos, new RgbColor(30, 60, 140), new Scene3DFaceShadow(5, 0.5)),     // fora da lista: não vai
                new Scene3DFace(Cantos, new RgbColor(30, 60, 140), new Scene3DFaceShadow(0, 1.2)),     // passa de 1: vai 1
                new Scene3DFace(Cantos, new RgbColor(30, 60, 140)),
            ],
            Moments = momentos,
        };

        var json = Ler(Viewer3DPage.Json(cena));
        var faces = json.GetProperty("faces");

        Assert.Equal(15, faces[0].GetArrayLength());
        Assert.Equal(1, faces[0][13].GetInt32());
        Assert.Equal(0.457, faces[0][14].GetDouble(), 9);
        Assert.Equal(13, faces[1].GetArrayLength());
        Assert.Equal(1, faces[2][14].GetDouble(), 9);
        Assert.Equal(13, faces[3].GetArrayLength());

        Assert.Equal("[{\"quando\":\"2026-06-21T07:30\",\"modulos\":12},{\"quando\":\"2026-06-21T08:00\",\"modulos\":3}]", json.GetProperty("momentos").GetRawText());

        // No envio ao servidor vai igual (o momento é a hora do relógio da usina, sem o lugar).
        var publicada = Ler(Viewer3DPage.PublishBody(cena, "0.1.2", "x")).GetProperty("cena");
        Assert.Equal(2, publicada.GetProperty("momentos").GetArrayLength());
        Assert.Equal(15, publicada.GetProperty("faces")[0].GetArrayLength());
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OsAneisDasSombrasVaoComAHachura()
    {
        IReadOnlyList<Point3> Anel(double x) => [new(314_000 + x, 7_456_000, 700), new(314_010 + x, 7_456_000, 700), new(314_010 + x, 7_456_010, 700)];

        var cena = Cena() with { Shadows = [Anel(0), Anel(2), Anel(20)], ShadowGroups = [0, 0, 1] };
        Assert.Equal("[0,0,1]", Ler(Viewer3DPage.Json(cena)).GetProperty("sombrasGrupo").GetRawText());

        // Grupo que não casa com os anéis não vai: o servidor recusaria a cena.
        Assert.False(Ler(Viewer3DPage.Json(cena with { ShadowGroups = [0, 1] })).TryGetProperty("sombrasGrupo", out _));
        Assert.False(Ler(Viewer3DPage.Json(cena with { ShadowGroups = [0, -1, 1] })).TryGetProperty("sombrasGrupo", out _));
    }
}
