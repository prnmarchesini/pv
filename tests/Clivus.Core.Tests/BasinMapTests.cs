using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Passo 30.2: as manchas das bacias para o hatch.</summary>
public class BasinMapTests
{
    /// <summary>Uma grade 4 × 2 de células de 2 m: as duas colunas da esquerda vão para a canaleta 0, a terceira para a 1, a quarta empoça.</summary>
    private static WatershedGrid Grade()
    {
        var dono = new[] { 0, 0, 1, Watershed.NoDestination, 0, 0, 1, Watershed.NoDestination };
        var motivo = dono.Select(d => d == Watershed.NoDestination ? FlowStop.Sink : FlowStop.Target).ToArray();
        var z = new double[] { 10, 11, 12, 13, 14, 15, 16, 17 };
        return new WatershedGrid(100, 200, 2, 4, 2, dono, motivo, new double[8], z, new double[8], new bool[8]);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void UmaManchaPorCanaletaEUmaPorMotivo()
    {
        var manchas = BasinMap.Shapes(Grade());

        Assert.Equal(3, manchas.Count);
        Assert.Equal([0, 1, Watershed.NoDestination], manchas.Select(m => m.Owner));

        // A bacia da canaleta 0: um retângulo de 4 × 4 m, sem os vértices do meio.
        var b0 = manchas[0];
        Assert.Equal(16, b0.Area, 9);
        var anel = Assert.Single(b0.Rings);
        Assert.Equal(4, anel.Count);
        Assert.Equal(16, ShadowUnion.Area(anel), 6);

        // O rótulo dentro da mancha; o hatch na cota mais alta dela.
        Assert.InRange(b0.LabelAt.X, 100, 104);
        Assert.Equal(15, b0.MaxZ);

        Assert.Equal(FlowStop.Sink, manchas[2].Reason);
        Assert.Contains("empoça", BasinMap.Why(manchas[2].Reason), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "30")]
    public void ASomaDasManchasEAAreaAnalisada()
    {
        var g = Grade();
        Assert.Equal(g.AnalyzedCount * 4, BasinMap.Shapes(g).Sum(m => m.Area), 9);
    }
}
