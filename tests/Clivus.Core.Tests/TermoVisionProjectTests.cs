using System.Text.Json;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O projeto do TermoVision sem envio (contrato v2, 02/11/2026): o desenho
/// que não veio do TermoVision vira uma usina pública de lá, com as mesas, o
/// terreno em grade e o sistema de coordenadas.
/// </summary>
public class TermoVisionProjectTests
{
    private static readonly Guid Desenho = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static TermoVisionTable Mesa(string nome, string? subtipo = TermoVisionTable.SubtipoBiposte) =>
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), nome,
            [(612_000.12346, 940_000.5, 50), (612_016.1, 940_000.5, 50), (612_016.1, 940_004.5, 51.6), (612_000.12346, 940_004.5, 51.6)],
            14, 2, new TermoVisionModule(1.134, 2.384, 0.02)) { Subtype = subtipo };

    private static JsonElement Ler(byte[] corpo) => JsonDocument.Parse(corpo).RootElement;

    [Fact]
    [Trait("Etapa", "33")]
    public void OCorpoTemONomeOGuidOSistemaOTerrenoEAsMesasComOTipoDaEstrutura()
    {
        var terreno = new Scene3DTerrain(611_900, 939_900, 2, 3, 2, [50.123, null, 51, 52, 53.456, double.NaN]);
        var crs = TermoVisionProjectCrs.Of(8.5, -80.0, drawingEpsg: 32617, drawingName: "UTM84-17N");

        var corpo = Ler(TermoVisionProject.Build("0.1.14", "PLAT A_Perfiles.dwg", "Usina Aurora", Desenho, crs, terreno, [Mesa("F1.1")], out var recusadas));

        Assert.Empty(recusadas);
        Assert.Equal(1, corpo.GetProperty("versao").GetInt32());
        Assert.Equal("Usina Aurora", corpo.GetProperty("nome").GetString());
        Assert.Equal(Desenho.ToString("D"), corpo.GetProperty("guid_do_desenho").GetString());

        var c = corpo.GetProperty("crs");
        Assert.Equal(32617, c.GetProperty("epsg").GetInt32());
        Assert.Equal("UTM84-17N", c.GetProperty("nome").GetString());
        Assert.Equal(17, c.GetProperty("zona").GetInt32());
        Assert.Equal("N", c.GetProperty("hemisferio").GetString());

        // A grade: linha 0 ao sul, z com duas casas, null sem superfície (e o NaN vira null).
        var t = corpo.GetProperty("terreno");
        Assert.Equal(2, t.GetProperty("passo_m").GetDouble());
        Assert.Equal(611_900, t.GetProperty("e0").GetDouble());
        Assert.Equal(939_900, t.GetProperty("n0").GetDouble());
        Assert.Equal(3, t.GetProperty("colunas").GetInt32());
        Assert.Equal(2, t.GetProperty("linhas").GetInt32());
        Assert.Equal("[50.12,null,51,52,53.46,null]", t.GetProperty("z").GetRawText());

        // A mesa como no retorno, com o tipo da estrutura.
        var mesa = corpo.GetProperty("mesas")[0];
        Assert.Equal("F1.1", mesa.GetProperty("nome").GetString());
        Assert.Equal("fixa", mesa.GetProperty("tipo").GetString());
        Assert.Equal("biposte", mesa.GetProperty("estrutura").GetProperty("tipo").GetString());
        Assert.Equal(612_000.1235, mesa.GetProperty("cantos")[0][0].GetDouble());
        Assert.Equal(14, mesa.GetProperty("colunas").GetInt32());
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void SemOSistemaDoDesenhoVaiAZonaEOEpsgDoSirgasOuNull()
    {
        // Itatiba: SIRGAS 2000 / UTM 23S.
        var itatiba = TermoVisionProjectCrs.Of(-22.992, -46.814);
        Assert.Equal(new TermoVisionProjectCrs(31983, null, 23, true), itatiba);

        // Fora da América do Sul, sem EPSG: o TermoVision aceita null com a zona.
        var portugal = TermoVisionProjectCrs.Of(38.7, -9.1);
        Assert.Null(portugal.Epsg);
        Assert.Equal(29, portugal.Zone);

        var corpo = Ler(TermoVisionProject.Build("0.1.14", "x.dwg", "x", Desenho, portugal, new Scene3DTerrain(0, 0, 2, 2, 2, [1, 2, 3, 4]), [Mesa("F1")], out _));
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("crs").GetProperty("epsg").ValueKind);
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("crs").GetProperty("nome").ValueKind);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void ODesenhoEmCoordenadaLocalOuEmOutraZonaNaoPassa()
    {
        // O desenho do nível 2 (Curvas Itatiba): o centro em UTM 23S e a lat/long dele.
        var crs = TermoVisionProjectCrs.Of(-22.992, -46.814);
        Assert.True(crs.LooksLikeUtm(314_068.8, 7_456_213.5, -22.992, -46.814));

        // Em coordenada local (perto de 0,0) ou deslocado de uma zona: não.
        Assert.False(crs.LooksLikeUtm(1_250, 830, -22.992, -46.814));
        Assert.False(crs.LooksLikeUtm(314_068.8 + 640_000, 7_456_213.5, -22.992, -46.814));
        Assert.False(crs.LooksLikeUtm(double.NaN, 7_456_213.5, -22.992, -46.814));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void AGradeTem2MAte2KmDeLadoEDepoisMilCelulas()
    {
        Assert.Equal(250, TermoVisionProject.GridCells(500));
        Assert.Equal(1000, TermoVisionProject.GridCells(2000));
        Assert.Equal(1000, TermoVisionProject.GridCells(4000));
        Assert.Equal(2, TermoVisionProject.GridCells(0));
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void AMesaQueNaoServeFicaDeForaComOMotivo()
    {
        var torta = new TermoVisionTable(Guid.NewGuid(), "F9", [(1, 2, 3)], 14, 2, new TermoVisionModule(1.134, 2.384, 0.02));

        var corpo = Ler(TermoVisionProject.Build("0.1.14", "x.dwg", "x", Desenho, TermoVisionProjectCrs.Of(-23, -46.8), new Scene3DTerrain(0, 0, 2, 2, 2, [1, 2, 3, 4]), [torta, Mesa("F1")], out var recusadas));

        Assert.Single(recusadas);
        Assert.StartsWith("F9: ", recusadas[0]);
        Assert.Equal(1, corpo.GetProperty("mesas").GetArrayLength());
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void ARespostaTrazAUsinaEOLink()
    {
        var r = TermoVisionProjectResult.TryParse(
            "{\"gravado\":true,\"usina\":{\"codigo\":\"U-0042\",\"nome\":\"Usina Aurora\"},\"link\":{\"url\":\"https://app.termovision.tech/v/abc\",\"expira_em\":\"2027-01-31T12:00:00Z\"}}");

        Assert.NotNull(r);
        Assert.True(r!.Saved);
        Assert.Equal("U-0042", r.PlantCode);
        Assert.Equal("https://app.termovision.tech/v/abc", r.Url);
        Assert.Equal(new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero), r.ExpiresAt);

        // Sem o link não é a resposta do contrato.
        Assert.Null(TermoVisionProjectResult.TryParse("{\"gravado\":true}"));
        Assert.Null(TermoVisionProjectResult.TryParse("isto não é json"));
    }
}
