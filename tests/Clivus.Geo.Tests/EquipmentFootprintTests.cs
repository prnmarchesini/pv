using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>O retângulo do equipamento elétrico em campo (12.3, 13.2, 14.6).</summary>
public class EquipmentFootprintTests
{
    [Fact]
    [Trait("Etapa", "12")]
    public void ABaseFlutuaOitentaCentimetrosAcimaDoTerreno()
    {
        Assert.Equal(712.30, EquipmentFootprint.BaseElevation(711.50), 9);
        Assert.Equal(0.80, EquipmentFootprint.BaseElevation(0), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentFootprint.BaseElevation(double.NaN));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void OsCantosSaoOCentroMaisMeiaMedidaLarguraEmXComprimentoEmY()
    {
        var c = EquipmentFootprint.Corners(100, 200, 4, 3);

        Assert.Equal([(98.0, 198.5), (102.0, 198.5), (102.0, 201.5), (98.0, 201.5)], c);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentFootprint.Corners(0, 0, 0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentFootprint.Corners(0, 0, 4, double.NaN));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void CantoComTerrenoAcimaDaBaseEContadoEForaDoTerrenoNao()
    {
        Assert.Equal(0, EquipmentFootprint.BuriedCorners(100.8, [100, 100.5, 100.79, double.NaN]));
        Assert.Equal(2, EquipmentFootprint.BuriedCorners(100.8, [100.81, 101, 99, 100]));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ATagCabeNoTopo()
    {
        // "Subestação C1" (13 letras) num bloquinho de 4 x 3 m.
        var h = EquipmentFootprint.TagHeight(13, 4, 3);
        Assert.True(h * 0.9 * 13 <= 4 * 0.9 + 1e-9);
        Assert.True(h <= 1);

        // "T1" num trafo de 3 x 2,5: a letra limitada pelo comprimento.
        Assert.Equal(2.5 / 3, EquipmentFootprint.TagHeight(2, 3, 2.5), 9);

        // Nome enorme num inversor pequeno: nunca abaixo do mínimo.
        Assert.Equal(EquipmentFootprint.MinTagHeight, EquipmentFootprint.TagHeight(400, 1.1, 0.7), 9);
        Assert.True(EquipmentFootprint.TagHeight(0, 1, 1) > 0);
    }
}
