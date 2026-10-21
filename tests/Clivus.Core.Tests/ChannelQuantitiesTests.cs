using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Passos 29.2 e 29.3: quantitativos da canaleta e as canaletas no Ver 3D.</summary>
public class ChannelQuantitiesTests
{
    private static readonly ChannelSection Trap = ChannelSection.Defaults[0];

    private static TerrainProfile Perfil(params (double D, double Z)[] pontos) =>
        new(pontos.Select(p => new ProfilePoint(p.D, p.D, 0, p.Z, true)).ToList(), false, 0);

    [Fact]
    [Trait("Etapa", "29")]
    public void BordaNoTerrenoEAreaVezesComprimento()
    {
        var c = ChannelProfile.Build(Perfil((0, 100), (100, 99)), 0.35, ChannelBottomMode.FollowTerrain, 0.005, 20);
        var q = ChannelQuantities.Of(c, Trap);

        Assert.Equal(100, q.Length2D, 9);
        Assert.Equal(Math.Sqrt(100 * 100 + 1), q.Length3D, 9);
        Assert.Equal(Trap.WallArea * 100, q.Concrete, 9);
        Assert.Equal(Trap.ExternalArea * 100, q.Excavation, 9);
        Assert.Equal(0, q.ExtraExcavation, 9);
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void OAprofundamentoEntraNaEscavacao()
    {
        // Plano: no modo mínimo o fundo desce 0,5 % e a canaleta aprofunda até 0,5 m a mais no fim.
        var c = ChannelProfile.Build(Perfil((0, 100), (100, 100)), 0.35, ChannelBottomMode.MinimumSlope, 0.005, 20);
        var q = ChannelQuantities.Of(c, Trap);

        var larguraExterna = 2 * Trap.OuterOutline().Max(p => p.X);
        Assert.Equal(0.5 / 2 * 100 * larguraExterna, q.ExtraExcavation, 6);
        Assert.Equal(Trap.ExternalArea * 100 + q.ExtraExcavation, q.Excavation, 9);
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void AsCanaletasVaoParaOVer3DNumaCamadaPropria()
    {
        var p0 = new Point3(314000, 7456000, 700);
        var cena = new Scene3D("t", null, [], [], [], [],
            [new Scene3DFace([p0, p0 with { X = p0.X + 1 }, p0 with { Y = p0.Y + 1 }], new RgbColor(175, 175, 170))]);

        var json = Viewer3DPage.Json(cena);
        Assert.Contains("\"canaletas\":[[", json, StringComparison.Ordinal);

        var html = Viewer3DPage.Html(cena);
        Assert.Contains("id=\"vCanaletas\"", html, StringComparison.Ordinal);
        Assert.Contains("D.canaletas", html, StringComparison.Ordinal);

        // Cena sem canaleta (desenho sem drenagem) continua válida.
        Assert.Contains("\"canaletas\":[]", Viewer3DPage.Json(cena with { Channels = null }), StringComparison.Ordinal);
    }
}
