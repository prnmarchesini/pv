namespace UFV.Core.Tests;

/// <summary>Passo 8.15: o resumo do terreno.</summary>
public class TerrainReportTests
{
    private static TerrainSummary Itatiba() => new("Terreno Itatiba", 12000, 0, 700.5, 742.25, 352_000, 355_100);

    [Fact]
    [Trait("Etapa", "8")]
    public void OResumoComLocalizacao()
    {
        var linhas = TerrainReport.Lines(Itatiba(), "Atual", new GeoLocation(-23.01, -46.85, GeoLocationSource.Desenho));

        Assert.Equal("Terreno: Terreno Itatiba (Atual)", linhas[0]);
        Assert.Contains("35,20 ha em planta", linhas[1]);
        Assert.Contains("700,50 a 742,25 m", linhas[2]);
        Assert.Contains(linhas, l => l.StartsWith("Cidade: Itatiba - SP"));
        Assert.Contains("País: Brasil", linhas);
        Assert.Contains("Fuso: SIRGAS 2000 / UTM zone 23S (EPSG:31983)", linhas);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void SemLocalizacaoDizComoDefinir()
    {
        var linhas = TerrainReport.Lines(Itatiba(), null, null);

        Assert.Equal("Terreno: Terreno Itatiba", linhas[0]);
        Assert.Contains(linhas, l => l.StartsWith("Localização: não definida"));
        Assert.DoesNotContain(linhas, l => l.StartsWith("Fuso"));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void LocalizacaoZeroZeroContaComoNaoDefinida()
    {
        var linhas = TerrainReport.Lines(Itatiba(), null, new GeoLocation(0, 0, GeoLocationSource.Desenho));

        Assert.Contains(linhas, l => l.StartsWith("Localização: não definida"));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ForaDoBrasil()
    {
        var linhas = TerrainReport.Lines(Itatiba(), null, new GeoLocation(-33.45, -70.66, GeoLocationSource.Usuario));

        Assert.Contains(linhas, l => l.StartsWith("Cidade: fora do Brasil"));
        Assert.Contains("País: não identificado", linhas);
        Assert.Contains(linhas, l => l.Contains("EPSG:31979"));
    }
}
