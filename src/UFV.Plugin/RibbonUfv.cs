using System.Runtime.CompilerServices;
using Autodesk.Windows;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Monta a aba UFV na ribbon.
///
/// Tudo que toca Autodesk.Windows mora aqui, e nao em UfvExtension, por um
/// motivo concreto: o Core Console (accoreconsole.exe) e o AutoCAD sem
/// interface e nao tem ribbon. Carregar um tipo cujo metodo mencione
/// RibbonItemEventArgs obriga o runtime a resolver AdWindows na hora de
/// carregar a assembly, e o NETLOAD falha inteiro com "Unable to load
/// assembly" - o plugin nao carrega nem para os comandos que funcionariam bem
/// sem interface.
///
/// Com a ribbon isolada aqui, o runtime so procura AdWindows quando um metodo
/// desta classe e de fato chamado, o que no Core Console nunca acontece.
///
/// A ribbon cresce por secoes conforme as etapas; na etapa 0 ela tem so o
/// botao Ola.
/// </summary>
internal static class RibbonUfv
{
    private const string IdDaAba = "UFV_RIBBON_TAB";
    private const string TituloDaAba = "UFV";

    /// <summary>
    /// Monta a aba, ou espera a ribbon existir se o plugin carregou antes dela
    /// (o que acontece quando o bundle carrega no boot do Civil 3D).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (ComponentManager.Ribbon is not null)
        {
            Montar();
            return;
        }

        ComponentManager.ItemInitialized += AoInicializarComponente;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        ComponentManager.ItemInitialized -= AoInicializarComponente;
    }

    private static void AoInicializarComponente(object? remetente, RibbonItemEventArgs e)
    {
        if (ComponentManager.Ribbon is null) return;

        // A ribbon existe: montamos uma vez e desligamos o gancho, que senao
        // dispara a cada item da interface.
        ComponentManager.ItemInitialized -= AoInicializarComponente;
        Montar();
    }

    private static void Montar()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon is null) return;

        // NETLOAD por cima do bundle montaria a aba duas vezes.
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
