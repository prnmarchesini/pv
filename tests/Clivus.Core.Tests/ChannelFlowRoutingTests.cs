using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A água dentro da canaleta, andando pelo fundo como ele está (tela do Renan
/// de 21/10/2026: a canaleta 3 faz barriga e a vazão crescia em linha reta
/// até a ponta).
/// </summary>
public class ChannelFlowRoutingTests
{
    /// <summary>1 m² entrando em cada metro, de 0 a <paramref name="comprimento"/>.</summary>
    private static IReadOnlyList<double[]> UmPorMetro(int comprimento) =>
        BasinEntry.Accumulate(Enumerable.Range(0, comprimento + 1).Select(i => (double)i), 1, comprimento);

    [Fact]
    [Trait("Etapa", "32")]
    public void FundoDescendoAcumulaAteASaida()
    {
        var r = ChannelFlowRouting.Route([(0, 10), (100, 5)], 0.5, UmPorMetro(100));

        Assert.Equal(1, r.AreaAt(0), 9);
        Assert.Equal(61, r.AreaAt(60), 9);
        Assert.Equal(101, r.AreaAt(100), 9);
        Assert.Equal(1, r.DirectionAt(30));

        var saida = Assert.Single(r.Outlets);
        Assert.Equal(ChannelOutletKind.End, saida.Kind);
        Assert.Equal(100, saida.Station, 9);
        Assert.Equal((100.0, 101.0), r.Peak);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void NaBarrigaAAguaDosDoisLadosJuntaNoFundoETransborda()
    {
        // Desce de 10 a 5 até a estaca 50 e sobe a 8 até a 100: barriga de 3 m, parede de 0,5 m.
        var r = ChannelFlowRouting.Route([(0, 10), (50, 5), (100, 8)], 0.5, UmPorMetro(100));

        // Perto da ponta de cima da direita, só a água dos últimos metros passa.
        Assert.Equal(11, r.AreaAt(90), 9);
        Assert.Equal(-1, r.DirectionAt(90));
        Assert.Equal(1, r.DirectionAt(10));

        // O máximo é na barriga: tudo chega ali.
        Assert.Equal((50.0, 101.0), r.Peak);
        var saida = Assert.Single(r.Outlets);
        Assert.Equal(ChannelOutletKind.Overflow, saida.Kind);
        Assert.Equal(50, saida.Station, 9);
        Assert.Equal(3, saida.Depth, 6);
        Assert.Equal(50, r.OutletFrom(90)!.Station, 9);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void BarrigaRasaEncheEAAguaSegue()
    {
        // Descendo 10 → 5 com um buraco de 0,1 m na estaca 50: menos que a parede, enche e passa.
        var r = ChannelFlowRouting.Route([(0, 10), (49, 7.55), (50, 7.40), (51, 7.45), (100, 5)], 0.5, UmPorMetro(100));

        Assert.Equal(61, r.AreaAt(60), 9);
        Assert.Equal(1, r.DirectionAt(50));
        var saida = Assert.Single(r.Outlets);
        Assert.Equal(ChannelOutletKind.End, saida.Kind);
        Assert.Equal(100, saida.Station, 9);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void FundoSubindoNoMeioSaiPelasDuasPontas()
    {
        // Lombada: sobe de 5 a 8 até a 50 e desce a 4.
        var r = ChannelFlowRouting.Route([(0, 5), (50, 8), (100, 4)], 0.5, UmPorMetro(100));

        Assert.Equal(2, r.Outlets.Count);
        Assert.All(r.Outlets, o => Assert.Equal(ChannelOutletKind.End, o.Kind));
        Assert.Equal(101, r.Outlets.Sum(o => o.Area), 9);
        Assert.Equal(r.Peak.Area, r.Outlets.Max(o => o.Area), 9);
        // Na estaca 10, a água corre para o começo: passa a que entrou de 10 a 49 (do topo, a 50, desce para o lado mais baixo).
        Assert.Equal(40, r.AreaAt(10), 9);
        Assert.Equal(-1, r.DirectionAt(10));
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void AMemoriaUsaAEstacaComMaisAguaEAvisaOTransbordo()
    {
        var secao = ChannelSection.Defaults[0];
        // O terreno desce 5 m até a estaca 50 e sobe 3 m até a 100: a canaleta segue o terreno.
        var pontos = Enumerable.Range(0, 21).Select(k => k * 5.0)
            .Select(d => new ProfilePoint(d, d, 0, d <= 50 ? 10.5 - 0.1 * d : 5.5 + 0.06 * (d - 50), true)).ToList();
        var perfil = ChannelProfile.Build(new TerrainProfile(pontos, false, 0), secao.UsefulHeight, ChannelBottomMode.FollowTerrain, 0.005, 20);

        var bacia = new BasinEntry(Guid.NewGuid(), "C", 101, 100, 5, false) { Accumulation = UmPorMetro(100) };
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", secao, perfil, bacia));

        Assert.NotNull(c.Routing);
        Assert.Equal(50, c.Routing!.Peak.Station, 0);
        Assert.Contains(c.Alerts, a => a.Contains("transbordam"));

        var p = new HydrologyParameters();
        var (_, naBarriga) = DrainageMemory.FlowAt(p, c, 50);
        var (_, pertoDaPonta) = DrainageMemory.FlowAt(p, c, 95);
        var (_, seDescesse) = DrainageMemory.FlowAtIfDrained(p, c, 95);

        Assert.Equal(c.FlowLitersPerSecond, naBarriga, 6);
        Assert.True(pertoDaPonta < naBarriga / 5, $"perto da ponta {pertoDaPonta} l/s, na barriga {naBarriga}");
        Assert.True(seDescesse > pertoDaPonta * 5);
    }
}
