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

    [Fact]
    [Trait("Etapa", "8")]
    public void NenhumBotaoRepetidoNoMesmoPainel()
    {
        foreach (var painel in RibbonLayout.Tabs.SelectMany(t => t.Panels))
            Assert.Equal(painel.Buttons.Count(), painel.Buttons.Select(b => b.Command).Distinct().Count());
    }
}
