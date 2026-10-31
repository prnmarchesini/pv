using System.Reflection;

namespace Clivus.Core.Tests;

/// <summary>Passo 8.16: a ribbon nova, conferida pelo que o Word pede.</summary>
public class RibbonLayoutTests
{
    private static readonly HashSet<string> Comandos = typeof(PluginInfo)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.Name.StartsWith("Comando", StringComparison.Ordinal) && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();

    [Fact]
    [Trait("Etapa", "8")]
    public void TodoBotaoTemTextoExplicativo()
    {
        Assert.All(RibbonLayout.AllButtons, b => Assert.False(string.IsNullOrWhiteSpace(b.Tooltip), b.Text));

        var menus = RibbonLayout.Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Items).OfType<RibbonMenuSpec>();
        Assert.All(menus, m => Assert.False(string.IsNullOrWhiteSpace(m.Tooltip), m.Text));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void TodoBotaoChamaUmComandoQueExiste()
    {
        Assert.All(RibbonLayout.AllButtons, b => Assert.Contains(b.Command, Comandos));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OlaMesaConfiguracaoEParametrosSairam()
    {
        var comandos = RibbonLayout.AllButtons.Select(b => b.Command).ToHashSet();

        Assert.DoesNotContain(PluginInfo.ComandoOla, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoMesa, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoConfig, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoAnalisesParametros, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoPintar, comandos);
        Assert.Contains(PluginInfo.ComandoConfiguracoes, comandos);
        Assert.Contains(PluginInfo.ComandoRegerar, comandos);
    }

    /// <summary>
    /// Renan, 02/10/2026, na tela: "eu quero apenas o menu UFV, e aí dentro
    /// dele você coloca análises e tags"; "menu análises, e aí o modal com as
    /// abas". Uma aba; Análises e Tags são botões que abrem janelas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void UmaAbaSoComOsBotoesDeAnalisesETags()
    {
        var aba = Assert.Single(RibbonLayout.Tabs);
        Assert.Equal("Clivus Solar", aba.Title);

        var comandos = aba.Panels.SelectMany(p => p.Buttons).Select(b => b.Command).ToList();
        Assert.Contains(PluginInfo.ComandoAnalises, comandos);
        Assert.Contains(PluginInfo.ComandoTags, comandos);
    }

    /// <summary>
    /// Renan, 02/10/2026: "tem numerar e tag, deixa somente tag, tem que ser
    /// somente UM, não pode ter redundância". Cada comando aparece uma vez,
    /// e o que já mora numa janela não repete na ribbon.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void NenhumComandoRepetidoNaRibbon()
    {
        var comandos = RibbonLayout.AllButtons.Select(b => b.Command).ToList();

        Assert.Equal(comandos.Count, comandos.Distinct().Count());
        Assert.DoesNotContain(PluginInfo.ComandoNumerar, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoEstilos, comandos);
        Assert.DoesNotContain(PluginInfo.ComandoLocalizacao, comandos);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AEdicaoECompacta()
    {
        var edicao = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Edição");

        Assert.Single(edicao.Items.OfType<RibbonMenuSpec>());
        Assert.True(edicao.Items.Count <= 2);
    }

    /// <summary>
    /// Item 13 de 10/10/2026 ("o submenu explodiu"): todo suspenso (Edição,
    /// Objetos e os que vierem) abre como menu comum, com ícone pequeno e o
    /// texto numa linha, e nenhum item dele é botão grande.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void OsSuspensosAbremComItensPequenos()
    {
        var menus = RibbonLayout.Tabs.SelectMany(t => t.Panels).SelectMany(p => p.Items).OfType<RibbonMenuSpec>().ToList();

        Assert.Contains(menus, m => m.Text == "Edição");
        Assert.Contains(menus, m => m.Text == "Objetos");

        foreach (var menu in menus)
        {
            Assert.True(menu.SmallList, $"o suspenso {menu.Text} abre com itens grandes");
            Assert.DoesNotContain(menu.Items, b => b.Large);
        }
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void NenhumBotaoRepetidoNoMesmoPainel()
    {
        foreach (var painel in RibbonLayout.Tabs.SelectMany(t => t.Panels))
            Assert.Equal(painel.Buttons.Count(), painel.Buttons.Select(b => b.Command).Distinct().Count());
    }

    /// <summary>
    /// Etapa 41 (Renan, 30/10/2026: "em implantação, eu quero ter um botão,
    /// pequeno, de lista de áreas"): o Áreas, pequeno, no painel Implantação,
    /// com ícone próprio, abrindo a janela pelo CLIVUS_AREAS.
    /// </summary>
    [Fact]
    [Trait("Etapa", "41")]
    public void AreasEUmBotaoPequenoNaImplantacao()
    {
        var implantacao = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Implantação");
        var botao = Assert.Single(implantacao.Buttons, b => b.Command == PluginInfo.ComandoAreas);

        Assert.Equal("Áreas", botao.Text);
        Assert.False(botao.Large);
        Assert.Equal("areas", botao.Icon);
        Assert.Equal(PluginInfo.ComandoArea, implantacao.Buttons.First().Command);
    }

    /// <summary>
    /// Etapa 43 (Renan, 31/10/2026: "quero que o cliente receba atualizações
    /// também"): o Atualizar, pequeno, no painel Configurações, embaixo do
    /// Ativar e do Sobre (a coluna de três pequenos), com ícone próprio.
    /// </summary>
    [Fact]
    [Trait("Etapa", "43")]
    public void AtualizarEUmBotaoPequenoNasConfiguracoes()
    {
        var configuracoes = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Configurações");
        var botao = Assert.Single(configuracoes.Buttons, b => b.Command == PluginInfo.ComandoAtualizar);

        Assert.Equal("Atualizar", botao.Text);
        Assert.False(botao.Large);
        Assert.Equal("atualizar", botao.Icon);
        Assert.Equal(
            [PluginInfo.ComandoAtivar, PluginInfo.ComandoSobre, PluginInfo.ComandoAtualizar],
            configuracoes.Buttons.Where(b => !b.Large).Select(b => b.Command).ToArray());
    }

    /// <summary>
    /// Etapa 33, desde 28/10/2026 um botão só no painel Saída: o TermoVision
    /// abre ou manda conforme o desenho (pedido do Renan).
    /// </summary>
    [Fact]
    [Trait("Etapa", "33")]
    public void TermoVisionNoPainelSaida()
    {
        var saida = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Saída");
        var botao = Assert.Single(saida.Buttons, b => b.Text == "TermoVision");
        Assert.Equal(PluginInfo.ComandoTermoVision, botao.Command);
        Assert.DoesNotContain(saida.Items.OfType<RibbonMenuSpec>(), m => m.Text == "TermoVision");
    }
}
