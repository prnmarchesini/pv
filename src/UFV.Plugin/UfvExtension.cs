using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using UFV.Core;

[assembly: ExtensionApplication(typeof(UFV.Plugin.UfvExtension))]

namespace UFV.Plugin;

/// <summary>
/// Ponto de entrada do plugin. O AutoCAD chama Initialize quando carrega a
/// assembly (pelo bundle em ApplicationPlugins ou por NETLOAD) e Terminate
/// quando fecha.
///
/// A unica coisa que acontece aqui e montar a aba da ribbon. A ribbon cresce
/// por secoes conforme as etapas: na etapa 0 ela tem so o botao Ola.
/// </summary>
public sealed class UfvExtension : IExtensionApplication
{
    private const string IdDaAba = "UFV_RIBBON_TAB";
    private const string TituloDaAba = "UFV";

    public void Initialize()
    {
        // Quando o plugin carrega no boot do Civil 3D, a ribbon ainda nao
        // existe. Nesse caso esperamos o componente ficar pronto.
        if (ComponentManager.Ribbon is not null)
        {
            MontarAba();
            return;
        }

        ComponentManager.ItemInitialized += AoInicializarComponente;
    }

    public void Terminate()
    {
        ComponentManager.ItemInitialized -= AoInicializarComponente;
    }

    private void AoInicializarComponente(object? remetente, RibbonItemEventArgs e)
    {
        if (ComponentManager.Ribbon is null) return;

        // A partir daqui a ribbon existe: montamos a aba uma vez so e
        // desligamos o gancho, que senao dispara a cada item da interface.
        ComponentManager.ItemInitialized -= AoInicializarComponente;
        MontarAba();
    }

    private static void MontarAba()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null) return;

        // NETLOAD depois do bundle carregaria a aba duas vezes.
        if (ribbon.Tabs.Any(t => t.Id == IdDaAba)) return;

        var aba = new RibbonTab
        {
            Id = IdDaAba,
            Title = TituloDaAba,
            Name = TituloDaAba,
        };

        aba.Panels.Add(MontarPainelInicio());
        ribbon.Tabs.Add(aba);
    }

    private static RibbonPanel MontarPainelInicio()
    {
        var origem = new RibbonPanelSource { Title = "Início" };

        origem.Items.Add(new RibbonButton
        {
            Text = "Olá",
            ShowText = true,
            ShowImage = false,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            // O espaco no fim envia o comando; sem ele o texto so fica digitado.
            CommandParameter = "UFV_OLA ",
            CommandHandler = new ComandoDaRibbon(),
            ToolTip = $"{PluginInfo.Nome}: confirma que o plugin está carregado.",
        });

        return new RibbonPanel { Source = origem };
    }
}
