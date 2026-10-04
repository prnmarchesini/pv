using System.Windows;
using System.Windows.Controls;

namespace Clivus.Plugin;

/// <summary>
/// A janela que pede um nome (área, alinhamento). Pedido do Renan em
/// 26/09/2026: "quando o sistema pede o nome de algo, poderia aparecer uma
/// janela, é ruim ver no terminal". O botão OK só habilita com um nome que
/// serve (não vazio, até <see cref="Perguntas.MaiorNome"/> caracteres).
/// Montada em código, como as outras janelas do plugin.
/// </summary>
internal sealed class JanelaDeNome : Window
{
    private readonly TextBox _caixa;
    private readonly Button _ok;
    private readonly TextBlock _aviso;

    internal string? Nome { get; private set; }

    internal JanelaDeNome(string titulo, string pergunta)
    {
        Title = titulo;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var painel = new StackPanel { Margin = new Thickness(14) };

        painel.Children.Add(new TextBlock { Text = pergunta, Margin = new Thickness(0, 0, 0, 6) });

        _caixa = new TextBox { MaxLength = Perguntas.MaiorNome, Margin = new Thickness(0, 0, 0, 6) };
        _caixa.TextChanged += (_, _) => Conferir();
        painel.Children.Add(_caixa);

        _aviso = new TextBlock { Foreground = System.Windows.Media.Brushes.DarkRed, Margin = new Thickness(0, 0, 0, 8) };
        painel.Children.Add(_aviso);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        _ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true, IsEnabled = false };
        _ok.Click += (_, _) => Confirmar();
        botoes.Children.Add(_ok);

        var cancelar = new Button { Content = Clivus.Core.Tr.T("Cancelar"), Width = 90, IsCancel = true };
        botoes.Children.Add(cancelar);

        painel.Children.Add(botoes);
        Content = painel;

        Loaded += (_, _) =>
        {
            try { _caixa.Focus(); }
            catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao focar a caixa do nome.", erro); }
        };
    }

    private void Conferir()
    {
        try
        {
            var nome = _caixa.Text.Trim();

            _aviso.Text = nome.Length == 0 ? Clivus.Core.Tr.T("Digite um nome.") : string.Empty;
            _ok.IsEnabled = nome.Length > 0 && nome.Length <= Perguntas.MaiorNome;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir o nome na janela.", erro);
        }
    }

    private void Confirmar()
    {
        try
        {
            var nome = _caixa.Text.Trim();
            if (nome.Length == 0 || nome.Length > Perguntas.MaiorNome) return;

            Nome = nome;
            DialogResult = true;
            Close();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao confirmar o nome na janela.", erro);
        }
    }
}
