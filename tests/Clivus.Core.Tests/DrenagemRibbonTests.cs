namespace Clivus.Core.Tests;

/// <summary>Passos 25.1 e 32.4: o painel Drenagem na ribbon, com Análise, Desenhos e Memória.</summary>
public class DrenagemRibbonTests
{
    private static RibbonPanelSpec Painel() =>
        Assert.Single(RibbonLayout.Tabs.SelectMany(t => t.Panels), p => p.Title == "Drenagem");

    [Fact]
    [Trait("Etapa", "32")]
    public void OPainelTemAnaliseDesenhosEMemoriaNessaOrdem()
    {
        var itens = Painel().Items;

        Assert.Equal(3, itens.Count);

        var analise = Assert.IsType<RibbonMenuSpec>(itens[0]);
        Assert.Equal("Análise", analise.Text);
        Assert.Equal([PluginInfo.ComandoDrenagemFlechas, PluginInfo.ComandoDrenagemLinha, PluginInfo.ComandoDrenagemGraficoRecriar, PluginInfo.ComandoDrenagemGraficoApagar, PluginInfo.ComandoDrenagemRastreio], analise.Items.Select(b => b.Command));

        // Renan, 21/10/2026: o botão Canaleta virou o suspenso Desenhos.
        var desenhos = Assert.IsType<RibbonMenuSpec>(itens[1]);
        Assert.Equal("Desenhos", desenhos.Text);
        Assert.Equal([PluginInfo.ComandoDrenagemCanaleta, PluginInfo.ComandoDrenagemCaixa, PluginInfo.ComandoDrenagemDissipador, PluginInfo.ComandoDrenagemExportar],
            desenhos.Items.Select(b => b.Command));

        var memoria = Assert.IsType<RibbonMenuSpec>(itens[2]);
        Assert.Equal("Memória", memoria.Text);
        Assert.Equal([PluginInfo.ComandoDrenagemBacias, PluginInfo.ComandoDrenagemMemoria, PluginInfo.ComandoDrenagemVazao], memoria.Items.Select(b => b.Command));
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void ODrenagemFicaAntesDaSaida()
    {
        var titulos = RibbonLayout.Tabs.Single().Panels.Select(p => p.Title).ToList();

        Assert.Equal(titulos.IndexOf("Saída") - 1, titulos.IndexOf("Drenagem"));
    }
}
