namespace Clivus.Core.Tests;

/// <summary>
/// As mesas assentadas num terreno diferente do de hoje (30/10/2026, o 3D do
/// Aurora mostrava o chão por cima das mesas numa região onde o terreno tinha
/// mudado depois que elas foram geradas).
/// </summary>
public class TerrainDriftTests
{
    [Fact]
    [Trait("Etapa", "39")]
    public void SoAMesaComPilarForaDoTerrenoDeHoje()
    {
        var mudadas = TerrainDrift.Tables([
            ("F1.1", 100.00, 100.05),                   // o mesmo terreno (arredondamento)
            ("F1.1", 101.00, 100.90),
            ("F1.2", 100.00, 105.17), ("F1.2", 100.00, 100.10), // subiu 5,17 m num pilar
            ("F1.3", 52.34, 49.75),                     // desceu 2,59 m
            ("F1.4", null, 120.00),                     // pilar sem cota gravada
            ("F1.5", 80.00, null),                      // sem terreno embaixo hoje
        ]);

        Assert.Equal(["F1.2", "F1.3"], mudadas.Select(m => m.Label));
        Assert.Equal(5.17, mudadas[0].MaxRise, 6);
        Assert.Equal(0, mudadas[0].MaxDrop, 6);
        Assert.Equal(0, mudadas[1].MaxRise, 6);
        Assert.Equal(2.59, mudadas[1].MaxDrop, 6);
    }

    [Fact]
    [Trait("Etapa", "39")]
    public void ATolerancia()
    {
        Assert.Empty(TerrainDrift.Tables([("F1.1", 100.0, 100.0 + TerrainDrift.Tolerance)]));
        Assert.Single(TerrainDrift.Tables([("F1.1", 100.0, 100.0 + TerrainDrift.Tolerance + 0.01)]));
    }
}
