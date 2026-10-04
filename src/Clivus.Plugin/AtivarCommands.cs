using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AtivarCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ATIVAR: a janela da licença. O cliente gera o código no portal do
/// app (Renan, 04/10/2026: "o usuário vai logar, vai colocar o código dele e
/// vai ativar") e cola aqui.
/// </summary>
public static class AtivarCommands
{
    [CommandMethod(PluginInfo.ComandoAtivar)]
    public static void Ativar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.F("\nA janela de ativação precisa da interface; use {0}.\n", PluginInfo.ComandoAtivarAutomatico));
            return;
        }

        try
        {
            AcadApp.ShowModalWindow(new JanelaDeAtivacao());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de ativação.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a ativação: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_ATIVAR_AUTO: o código pela linha de comando. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoAtivarAutomatico)]
    public static void AtivarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var codigo = editor.GetString(new PromptStringOptions(Tr.T("\nCódigo de ativação: ")) { AllowSpaces = false });
            if (codigo.Status != PromptStatus.OK) return;

            var (_, frase) = Licenciamento.Ativar(codigo.StringResult);
            editor.WriteMessage(Tr.F("\nATIVAR {0}\n", frase));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ativar (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ativar: {0}\n", erro.Message));
        }
    }

    /// <summary>A situação da licença, em uma frase.</summary>
    internal static string Situacao()
    {
        if (!Licenciamento.Ligado) return Tr.T("Esta versão do Clivus Solar não pede ativação.");

        var (estado, conteudo, porque) = Licenciamento.Estado();

        return estado switch
        {
            LicenseState.Valid => Tr.F("Ativado para {0} (plano {1}), até {2:dd/MM/yyyy}.", conteudo!.Account, conteudo.Plan, conteudo.ExpiresAt.ToLocalTime()),
            LicenseState.Revalidate => Tr.F("Ativado para {0}; a licença renova na próxima vez com internet (vale até {1:dd/MM/yyyy}).", conteudo!.Account, conteudo.ExpiresAt.ToLocalTime()),
            _ => Tr.F("Não ativado ({0}).", porque),
        };
    }
}

/// <summary>A janela de ativação: a situação, o campo do código e o link do portal.</summary>
internal sealed class JanelaDeAtivacao : Window
{
    private static readonly Brush Petroleo = new SolidColorBrush(Color.FromRgb(0x0F, 0x25, 0x33));

    private readonly TextBlock _situacao = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBox _codigo = new() { Width = 260, ToolTip = Tr.T("O código gerado no portal do app (Clivus Solar), como CLV-XXXX-XXXX-XXXX.") };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    internal JanelaDeAtivacao()
    {
        Title = Tr.T("Clivus Solar — Ativação");
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _situacao.Foreground = Petroleo;
        _situacao.Text = AtivarCommands.Situacao();

        var linha = new StackPanel { Orientation = Orientation.Horizontal };
        linha.Children.Add(new TextBlock { Text = Tr.T("Código"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        linha.Children.Add(_codigo);

        var ativar = new Button { Content = Tr.T("Ativar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsDefault = true, ToolTip = Tr.T("Manda o código ao servidor e guarda a licença neste PC.") };
        ativar.Click += (_, _) =>
        {
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                var (ok, frase) = Licenciamento.Ativar(_codigo.Text);
                _recado.Foreground = ok ? Brushes.ForestGreen : Brushes.Firebrick;
                _recado.Text = frase;
                _situacao.Text = AtivarCommands.Situacao();
            }
            catch (System.Exception erro)
            {
                // Clique de WPF: exceção solta derrubaria o Civil 3D.
                RegistroDeDiagnostico.Registrar("Falha ao ativar.", erro);
                _recado.Foreground = Brushes.Firebrick;
                _recado.Text = Tr.F("Não consegui ativar: {0}", erro.Message);
            }
            finally
            {
                Cursor = null;
            }
        };
        linha.Children.Add(ativar);

        var portal = new Button
        {
            Content = Tr.T("Gerar meu código no portal"),
            Padding = new Thickness(10, 0, 10, 0),
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            ToolTip = Tr.T("Abre o portal do app no navegador: entre na sua conta e gere o código desta máquina."),
        };
        portal.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(PluginInfo.PortalDoApp) { UseShellExecute = true }); }
            catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao abrir o portal.", erro); }
        };

        var pilha = new StackPanel { Margin = new Thickness(16) };
        var logo = IconesClivus.Marca("clivus-logo-480.png");
        if (logo is not null) pilha.Children.Add(new Image { Source = logo, Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) });
        pilha.Children.Add(_situacao);
        pilha.Children.Add(linha);
        pilha.Children.Add(_recado);
        pilha.Children.Add(portal);

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 90, Height = 26, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), IsCancel = true };
        pilha.Children.Add(fechar);

        Content = pilha;
    }
}
