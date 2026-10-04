namespace Clivus.Core.Tests;

/// <summary>Passo 8.15: cidade, país e fuso do terreno.</summary>
public class TerrainPlaceTests
{
    [Fact]
    [Trait("Etapa", "8")]
    public void ABaseTemTodosOsMunicipios()
    {
        Assert.InRange(TerrainPlace.Municipalities.Count, 5560, 5580);
        Assert.Contains(TerrainPlace.Municipalities, m => m.Name == "São Paulo" && m.State == "SP");
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(-23.01, -46.85, "Itatiba", "SP")]
    [InlineData(-23.21, -47.52, "Porto Feliz", "SP")]
    [InlineData(-15.78, -47.93, "Brasília", "DF")]
    public void ACidadeMaisPerto(double lat, double lon, string cidade, string uf)
    {
        var achada = TerrainPlace.Nearest(lat, lon);

        Assert.Equal(cidade, achada.City.Name);
        Assert.Equal(uf, achada.City.State);
        Assert.True(achada.DistanceKm < 5);
        Assert.Equal("Brasil", TerrainPlace.Country(achada));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void LongeDoBrasilNaoAfirmaOPais()
    {
        // Santiago do Chile.
        var achada = TerrainPlace.Nearest(-33.45, -70.66);

        Assert.True(achada.DistanceKm > TerrainPlace.BrazilRadiusKm);
        Assert.Null(TerrainPlace.Country(achada));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(-23.0, -46.85, 23, true, 31983)]
    [InlineData(-15.8, -47.9, 23, true, 31983)]
    [InlineData(-3.1, -60.02, 20, true, 31980)]
    [InlineData(-8.05, -34.9, 25, true, 31985)]
    [InlineData(-3.85, -32.42, 25, true, 31985)]
    [InlineData(2.8, -60.7, 20, false, 31974)]
    [InlineData(-33.45, -70.66, 19, true, 31979)]
    public void OFusoUtmComOEpsgDoSirgas(double lat, double lon, int fuso, bool sul, int epsg)
    {
        var utm = TerrainPlace.Utm(lat, lon);

        Assert.Equal(fuso, utm.Zone);
        Assert.Equal(sul, utm.South);
        Assert.Equal(epsg, utm.SirgasEpsg);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ForaDaAmericaDoSulSoOFuso()
    {
        var lisboa = TerrainPlace.Utm(38.72, -9.14);

        Assert.Equal(29, lisboa.Zone);
        Assert.False(lisboa.South);
        Assert.Null(lisboa.SirgasEpsg);
        Assert.Contains("29N", lisboa.Describe());
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADescricaoDoFuso()
    {
        Assert.Equal("SIRGAS 2000 / UTM zone 23S (EPSG:31983)", TerrainPlace.Utm(-23, -46.85).Describe());
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    public void CoordenadaImpossivelERecusada(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPlace.Utm(lat, lon));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainPlace.Nearest(lat, lon));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADistanciaPorHaversine()
    {
        // Um grau de latitude dá cerca de 111,2 km.
        Assert.Equal(111.2, TerrainPlace.DistanceKm(0, 0, 1, 0), 1);
    }
}
