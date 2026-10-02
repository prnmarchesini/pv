using System.Reflection;

namespace UFV.Core.Tests;

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

    [Fact]
    [Trait("Etapa", "8")]
    public void AsAbasDoWord()
    {
        Assert.Equal(["UFV", "UFV Análises", "UFV Tags"], RibbonLayout.Tabs.Select(t => t.Title));
        Assert.Equal(RibbonLayout.Tabs.Count, RibbonLayout.Tabs.Select(t => t.Id).Distinct().Count());

        var analises = RibbonLayout.Tabs[1].Panels.Select(p => p.Title);
        Assert.Equal(["Ponta baixa", "Ponta alta", "Declividade", "Pilares", "Quantidades"], analises);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CadaAnaliseTemOsCincoBotoes()
    {
        foreach (var painel in RibbonLayout.Tabs[1].Panels.Take(4))
            Assert.Equal(["Inserir", "Analisar", "Apagar textos", "Tirar cores", "Quantificar"], painel.Buttons.Select(b => b.Text));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AEdicaoECompacta()
    {
        var edicao = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Edição");

        Assert.Single(edicao.Items.OfType<RibbonMenuSpec>());
        Assert.True(edicao.Items.Count <= 3);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void NenhumBotaoRepetidoNoMesmoPainel()
    {
        foreach (var painel in RibbonLayout.Tabs.SelectMany(t => t.Panels))
            Assert.Equal(painel.Buttons.Count(), painel.Buttons.Select(b => b.Command).Distinct().Count());
    }
}
