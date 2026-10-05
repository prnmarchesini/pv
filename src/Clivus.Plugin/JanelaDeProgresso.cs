using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// "Carregando..." com a porcentagem (05/10/2026, Renan: "deveria aparecer um
/// modal de carregando... com a %"). O trabalho roda na linha do AutoCAD (o
/// desenho só aceita ela), então cada <see cref="Avancar"/> deixa a janela se
/// redesenhar antes de seguir. A janela de quem chamou fica travada enquanto
/// isso, como num modal.
/// </summary>
internal sealed class JanelaDeProgresso : Window
{
    private readonly ProgressBar _barra = new() { Height = 18, Minimum = 0, Maximum = 100 };
    private readonly TextBlock _texto = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _porcento = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DimGray };
    private readonly Window? _dono;

    private JanelaDeProgresso(Window? dono, string titulo)
    {
        _dono = dono;
        Owner = dono;
        Title = titulo;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        WindowStartupLocation = dono is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;

        _texto.Text = Tr.T("Carregando...");
        var pilha = new StackPanel { Margin = new Thickness(14) };
        pilha.Children.Add(_texto);
        pilha.Children.Add(_barra);
        pilha.Children.Add(_porcento);
        Content = pilha;
    }

    /// <summary>Abre a janela por cima de <paramref name="dono"/> (que fica travado até <see cref="Dispose"/>).</summary>
    internal static JanelaDeProgresso Abrir(Window? dono, string titulo)
    {
        var janela = new JanelaDeProgresso(dono, titulo);
        if (dono is not null) dono.IsEnabled = false;
        janela.Show();
        janela.Avancar(0, Tr.T("Carregando..."));
        return janela;
    }

    /// <summary>A porcentagem (0 a 100) e o que está sendo feito; a janela se redesenha.</summary>
    internal void Avancar(double porcento, string? texto = null)
    {
        var p = Math.Clamp(porcento, 0, 100);
        _barra.Value = p;
        _porcento.Text = Tr.F("{0:0}%", p);
        if (texto is not null) _texto.Text = texto;

        // Deixa a janela se pintar antes de o trabalho continuar.
        Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    /// <summary>Fecha e destrava a janela de quem chamou.</summary>
    internal void Fechar()
    {
        if (_dono is not null) _dono.IsEnabled = true;
        Close();
        _dono?.Activate();
    }
}
