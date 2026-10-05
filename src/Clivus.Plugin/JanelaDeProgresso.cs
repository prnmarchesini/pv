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
///
/// Com o botão Cancelar (05/10/2026, Renan, sombras do ano: "um modal que
/// mostra o carregamento e % ... com um botão de cancelar a qualquer momento,
/// porque não pode prender o CAD"): cada <see cref="Avancar"/> também deixa
/// o clique passar, e quem chama confere <see cref="Cancelado"/>. Fechar no
/// X conta como cancelar.
/// </summary>
internal sealed class JanelaDeProgresso : Window
{
    private readonly ProgressBar _barra = new() { Height = 18, Minimum = 0, Maximum = 100 };
    private readonly TextBlock _texto = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _porcento = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DimGray };
    private readonly Button? _cancelar;
    private readonly Window? _dono;
    private readonly System.Diagnostics.Stopwatch _desdeAPintura = System.Diagnostics.Stopwatch.StartNew();
    private bool _terminando;
    private bool _pintou;

    /// <summary>Se o usuário pediu para cancelar (botão ou X da janela).</summary>
    internal bool Cancelado { get; private set; }

    private JanelaDeProgresso(Window? dono, string titulo, bool podeCancelar)
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

        if (podeCancelar)
        {
            _cancelar = new Button
            {
                Content = Tr.T("Cancelar"),
                Width = 90,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0),
                IsCancel = true,
                ToolTip = Tr.T("Para o cálculo; nada é desenhado."),
            };
            _cancelar.Click += (_, _) => PedirCancelar();
            pilha.Children.Add(_cancelar);
        }

        // O X da janela: com Cancelar, vale como cancelar; a janela só some
        // quando o trabalho para (Fechar). Sem Cancelar, o X não faz nada.
        Closing += (_, e) =>
        {
            if (_terminando) return;
            e.Cancel = true;
            if (podeCancelar) PedirCancelar();
        };

        Content = pilha;
    }

    private void PedirCancelar()
    {
        // Clique de WPF: nada pode escapar daqui.
        try
        {
            if (_cancelar is null || !_cancelar.IsEnabled) return;
            Cancelado = true;
            _cancelar.IsEnabled = false;
            _texto.Text = Tr.T("Cancelando...");
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao cancelar o andamento.", erro);
        }
    }

    /// <summary>
    /// Liga ou desliga o Cancelar (desligado na hora de gravar no desenho, que
    /// não para pela metade).
    /// </summary>
    internal void PodeCancelar(bool pode)
    {
        if (_cancelar is null) return;
        _cancelar.IsEnabled = pode && !Cancelado;
    }

    /// <summary>Abre a janela por cima de <paramref name="dono"/> (que fica travado até <see cref="Fechar"/>).</summary>
    /// <param name="dono">A janela de quem chamou, ou null (comando).</param>
    /// <param name="titulo">O título.</param>
    /// <param name="podeCancelar">Se mostra o botão Cancelar.</param>
    internal static JanelaDeProgresso Abrir(Window? dono, string titulo, bool podeCancelar = false)
    {
        var janela = new JanelaDeProgresso(dono, titulo, podeCancelar);
        if (dono is not null) dono.IsEnabled = false;

        // Sem dono (comando): por cima da janela do AutoCAD, não atrás dela.
        if (dono is null)
        {
            try
            {
                new System.Windows.Interop.WindowInteropHelper(janela).Owner = Autodesk.AutoCAD.ApplicationServices.Core.Application.MainWindow.Handle;
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui pôr o andamento sobre o AutoCAD.", erro);
            }
        }

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
        if (texto is not null && !Cancelado) _texto.Text = texto;

        // Deixa a janela se pintar e o clique em Cancelar passar antes de o
        // trabalho continuar; no máximo umas 20 vezes por segundo (chamada a
        // cada passo do cálculo, a pintura a cada vez atrasaria a conta).
        if (_pintou && _desdeAPintura.ElapsedMilliseconds < 50 && p < 100) return;
        Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        _desdeAPintura.Restart();
        _pintou = true;
    }

    /// <summary>Fecha e destrava a janela de quem chamou.</summary>
    internal void Fechar()
    {
        _terminando = true;
        if (_dono is not null) _dono.IsEnabled = true;
        Close();
        _dono?.Activate();
    }
}
