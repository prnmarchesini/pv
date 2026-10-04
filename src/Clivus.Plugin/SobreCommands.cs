using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.SobreCommands))]

namespace Clivus.Plugin;

/// <summary>CLIVUS_SOBRE: a janela Sobre, com o logo do Clivus Solar e a versão.</summary>
public static class SobreCommands
{
    [CommandMethod(PluginInfo.ComandoSobre)]
    public static void Sobre()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        var versao = PluginInfo.VersaoLegivel(ClivusCommands.VersaoDoPlugin());

        if (!ClivusExtension.TemInterface())
        {
            documento?.Editor.WriteMessage($"\n{PluginInfo.MensagemDeApresentacao(versao)}\n");
            return;
        }

        try
        {
            AcadApp.ShowModalWindow(new JanelaSobre(versao));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela Sobre.", erro);
            documento?.Editor.WriteMessage(Tr.F("\nNão consegui abrir a janela Sobre: {0}\n", erro.Message));
        }
    }
}

/// <summary>O logo, a versão, o que o plugin faz e onde fica o registro de diagnóstico.</summary>
internal sealed class JanelaSobre : Window
{
    private static readonly Brush Petroleo = new SolidColorBrush(Color.FromRgb(0x0F, 0x25, 0x33));

    internal JanelaSobre(string versao)
    {
        Title = Tr.T("Sobre o Clivus Solar");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(20) };

        // O logo horizontal na largura de 480 px (o de 960 é a versão @2x).
        var logo = IconesClivus.Marca("clivus-logo-960.png");
        if (logo is not null)
            pilha.Children.Add(new Image { Source = logo, Width = 360, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14) });

        TextBlock Linha(string texto, bool forte = false) => new()
        {
            Text = texto,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Petroleo,
            FontWeight = forte ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 2),
        };

        pilha.Children.Add(Linha($"Clivus Solar {versao}", forte: true));
        pilha.Children.Add(Linha(Tr.T("Layout de usinas fotovoltaicas em terreno inclinado, no Civil 3D.")));
        pilha.Children.Add(Linha(Tr.F("Registro de diagnóstico: {0}", RegistroDeDiagnostico.Caminho)));

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 90, Height = 26, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0), IsCancel = true, IsDefault = true };
        pilha.Children.Add(fechar);

        Content = pilha;
    }
}

/// <summary>
/// O ícone do Clivus Solar em toda janela do plugin, sem mexer em cada uma:
/// quando uma janela nossa carrega e não tem ícone, recebe o clivus.ico.
/// </summary>
internal static class AparenciaDasJanelas
{
    private static bool _instalado;

    internal static void Instalar()
    {
        if (_instalado) return;

        var icone = IconesClivus.Marca("clivus.ico");
        if (icone is null) return;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((remetente, _) =>
        {
            if (remetente is Window janela && janela.Icon is null && janela.GetType().Assembly == typeof(AparenciaDasJanelas).Assembly)
                janela.Icon = icone;
        }));

        _instalado = true;
    }

    /// <summary>O clivus.ico como ícone do Windows Forms (o PaletteSet do AutoCAD pede esse), ou null.</summary>
    internal static System.Drawing.Icon? IconeDoWindows()
    {
        try
        {
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
            var recurso = Application.GetResourceStream(new Uri("pack://application:,,,/Clivus.Plugin;component/Resources/Branding/clivus.ico", UriKind.Absolute));
            return recurso is null ? null : new System.Drawing.Icon(recurso.Stream, 16, 16);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não achei o clivus.ico.", erro);
            return null;
        }
    }
}
