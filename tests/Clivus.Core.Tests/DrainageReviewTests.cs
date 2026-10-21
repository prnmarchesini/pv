using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Correções da revisão das etapas 30 e 31.</summary>
public class DrainageReviewTests
{
    [Fact]
    [Trait("Etapa", "31")]
    public void CruzamentoNoVerticeContaUmaVez()
    {
        // A canaleta tem um vértice exatamente em cima da via (y = 10).
        IReadOnlyList<Point3> canaleta = [new(0, 0, 0), new(5, 10, 0), new(10, 20, 0)];
        IReadOnlyList<Point3> via = [new(-10, 10, 0), new(20, 10, 0)];

        var cruzamentos = RoadCrossings.Find(canaleta, [via]);

        var c = Assert.Single(cruzamentos);
        Assert.Equal(5, c.X, 9);
        Assert.Equal(10, c.Y, 9);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void CruzamentoComumEPontaDeViaNoMeioDoTrecho()
    {
        IReadOnlyList<Point3> canaleta = [new(0, 0, 0), new(10, 0, 0)];

        Assert.Single(RoadCrossings.Find(canaleta, [[new Point3(4, -5, 0), new Point3(4, 5, 0)]]));
        // A via começa em cima da canaleta (toque): conta.
        Assert.Single(RoadCrossings.Find(canaleta, [[new Point3(6, 0, 0), new Point3(6, 5, 0)]]));
        // Via paralela ou longe: nada.
        Assert.Empty(RoadCrossings.Find(canaleta, [[new Point3(0, 1, 0), new Point3(10, 1, 0)]]));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void ContrapendenteCurtaApareceNosAlertasDaMemoria()
    {
        // Desce 2 % na média, mas sobe 3 cm entre 8 e 12 m.
        var pontos = new List<ProfilePoint> { new(0, 0, 0, 100, true), new(8, 8, 0, 99.84, true), new(12, 12, 0, 99.87, true), new(20, 20, 0, 99.6, true) };
        var perfil = ChannelProfile.Build(new TerrainProfile(pontos, false, 0), 0.35, ChannelBottomMode.FollowTerrain, 0.005, 20);
        var bacia = new BasinEntry(Guid.NewGuid(), "C", 1000, 50, 2, false);

        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", ChannelSection.Defaults[0], perfil, bacia));

        Assert.Contains(c.Alerts, a => a.Contains("plano ou sobe de 8,0 a 12,0 m", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void CanaletaForaDaMemoriaViraAlerta()
    {
        var memoria = DrainageMemory.Build(new HydrologyParameters(), [], ["Canaleta 3: sem perfil"]);

        Assert.Contains("Canaleta 3: sem perfil", memoria.Alerts);
        Assert.Contains(DrainageMemory.Tables(memoria).Single(t => t.Title == "Alertas").Rows, r => (string?)r[0] == "Canaleta 3: sem perfil");
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void MeiaCanaPodeSugerirRaioMenor()
    {
        // Vazão pequena numa meia-cana de 50 cm: a sugestão desce.
        var grande = ChannelSection.Defaults[1] with { Radius = 0.5 };
        var r = Hydraulics.SuggestedHeight(grande, 0.005, 0.02, 0.05);

        Assert.True(r < 0.5, $"sugeriu {r}");
        var h = Hydraulics.NormalDepth(grande with { Radius = r }, 0.005, 0.02);
        Assert.True(h + Hydraulics.Freeboard(0.005) <= r + 1e-9);
    }
}
