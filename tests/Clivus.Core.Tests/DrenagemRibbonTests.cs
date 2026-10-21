namespace Clivus.Core.Tests;

/// <summary>Passo 25.1: o painel Drenagem na ribbon, com Análise, Canaleta e Memória.</summary>
public class DrenagemRibbonTests
{
    private static RibbonPanelSpec Painel() =>
        Assert.Single(RibbonLayout.Tabs.SelectMany(t => t.Panels), p => p.Title == "Drenagem");

    [Fact]
    [Trait("Etapa", "25")]
    public void OPainelTemAnaliseCanaletaEMemoriaNessaOrdem()
    {
        var itens = Painel().Items;

        Assert.Equal(3, itens.Count);

        var analise = Assert.IsType<RibbonMenuSpec>(itens[0]);
        Assert.Equal("Análise", analise.Text);
        Assert.Equal([PluginInfo.ComandoDrenagemFlechas, PluginInfo.ComandoDrenagemLinha], analise.Items.Select(b => b.Command));

        var canaleta = Assert.IsType<RibbonButtonSpec>(itens[1]);
        Assert.Equal(PluginInfo.ComandoDrenagemCanaleta, canaleta.Command);
        Assert.True(canaleta.Large);

        var memoria = Assert.IsType<RibbonMenuSpec>(itens[2]);
        Assert.Equal("Memória", memoria.Text);
        Assert.Equal([PluginInfo.ComandoDrenagemBacias, PluginInfo.ComandoDrenagemMemoria], memoria.Items.Select(b => b.Command));
    }

    [Fact]
    [Trait("Etapa", "25")]
    public void ODrenagemFicaAntesDaSaida()
    {
        var titulos = RibbonLayout.Tabs.Single().Panels.Select(p => p.Title).ToList();

        Assert.Equal(titulos.IndexOf("Saída") - 1, titulos.IndexOf("Drenagem"));
    }
}
