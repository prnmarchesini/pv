using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clivus.Core;

namespace Clivus.Instalador;

/// <summary>
/// ClivusSolar-Setup.exe. Sem argumentos, a janela. Argumentos:
/// <c>/desinstalar</c>, <c>/silencioso</c> (sem janela, o resultado no
/// <c>/log=arquivo</c> e no código de saída), <c>/teste=pasta</c> (tudo dentro
/// da pasta, sem tocar a instalação de verdade), <c>/ignorar-civil3d</c> (só
/// com /teste). Códigos de saída: 0 ok, 2/3/4/6 os da regra de versão, 5
/// AutoCAD aberto, 1 falha.
/// </summary>
internal static class Programa
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? Valor(string nome) => args.FirstOrDefault(a => a.StartsWith(nome + "=", StringComparison.OrdinalIgnoreCase))?[(nome.Length + 1)..].Trim('"');
        bool Tem(string nome) => args.Any(a => string.Equals(a, nome, StringComparison.OrdinalIgnoreCase));

        var teste = Valor("/teste");
        var instalacao = new Instalacao(teste);
        var desinstalar = Tem("/desinstalar");
        var ignorarCivil = teste is not null && Tem("/ignorar-civil3d");

        if (!Tem("/silencioso"))
        {
            var app = new Application();
            return app.Run(new JanelaDoInstalador(instalacao, desinstalar, ignorarCivil));
        }

        var (codigo, frase) = Executar(instalacao, desinstalar, ignorarCivil, null);
        if (Valor("/log") is { } log) File.WriteAllText(log, $"{codigo} {frase}");
        return codigo;
    }

    /// <summary>Instala ou desinstala; o código de saída e a frase.</summary>
    internal static (int Codigo, string Frase) Executar(Instalacao instalacao, bool desinstalar, bool ignorarCivil, Action<string>? progresso)
    {
        try
        {
            if (Instalacao.AutoCadAberto())
                return (5, Tr.T("Feche o Civil 3D (e o AutoCAD) antes de continuar: ele segura os arquivos do plugin."));

            if (desinstalar) return (0, instalacao.Desinstalar());

            if (!ignorarCivil)
            {
                var (min, max) = Instalacao.SerieSuportada();
                var (resultado, _, frase) = InstallRules.Check(Instalacao.AutoCads(), min, max);
                if (resultado != InstallCheck.Ok) return (InstallRules.ExitCode(resultado), frase);
            }

            return (0, instalacao.Instalar(progresso));
        }
        catch (Exception erro)
        {
            return (1, desinstalar ? Tr.F("Não consegui desinstalar: {0}", erro.Message) : Tr.F("Não consegui instalar: {0}", erro.Message));
        }
    }
}

/// <summary>A janela: o logo, o que vai acontecer, um botão.</summary>
internal sealed class JanelaDoInstalador : Window
{
    private static readonly Brush Petroleo = new SolidColorBrush(Color.FromRgb(0x0F, 0x25, 0x33));
    private static readonly Brush Ambar = new SolidColorBrush(Color.FromRgb(0xF4, 0xA5, 0x1C));

    internal JanelaDoInstalador(Instalacao instalacao, bool desinstalar, bool ignorarCivil)
    {
        Title = desinstalar ? Tr.T("Desinstalar o Clivus Solar") : Tr.F("Instalar o Clivus Solar {0}", Instalacao.Versao);
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        try
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/clivus.ico", UriKind.Absolute));
        }
        catch (Exception)
        {
            // Sem ícone a janela funciona igual.
        }

        var pilha = new StackPanel { Margin = new Thickness(24) };

        try
        {
            pilha.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Resources/clivus-logo-480.png", UriKind.Absolute)),
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16),
            });
        }
        catch (Exception)
        {
            pilha.Children.Add(new TextBlock { Text = "Clivus Solar", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Petroleo });
        }

        TextBlock Texto(string t) => new() { Text = t, TextWrapping = TextWrapping.Wrap, Foreground = Petroleo, Margin = new Thickness(0, 0, 0, 8), FontSize = 13 };

        var explicacao = Texto(desinstalar
            ? Tr.T("Tira o Clivus Solar deste computador. Sua licença e seus perfis de mesa ficam guardados, para uma próxima instalação.")
            : Tr.T("Instala o plugin de layout de usinas fotovoltaicas para o Civil 3D 2026, só para o seu usuário (não precisa de administrador). Depois de instalar, abra o Civil 3D e ative com o código gerado no portal do app."));
        pilha.Children.Add(explicacao);

        var situacao = Texto(string.Empty);
        situacao.FontWeight = FontWeights.SemiBold;
        pilha.Children.Add(situacao);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var acao = new Button
        {
            Content = desinstalar ? Tr.T("Desinstalar") : Tr.T("Instalar"),
            Width = 120,
            Height = 32,
            Background = Ambar,
            Foreground = Petroleo,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            IsDefault = true,
        };
        var fechar = new Button { Content = Tr.T("Cancelar"), Width = 100, Height = 32, Margin = new Thickness(10, 0, 0, 0), IsCancel = true };
        fechar.Click += (_, _) => Close();
        botoes.Children.Add(acao);
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        acao.Click += (_, _) =>
        {
            acao.IsEnabled = false;
            Cursor = System.Windows.Input.Cursors.Wait;

            var (codigo, frase) = Programa.Executar(instalacao, desinstalar, ignorarCivil, p => { situacao.Text = p; });

            Cursor = null;
            situacao.Text = frase;
            situacao.Foreground = codigo == 0 ? Brushes.ForestGreen : Brushes.Firebrick;

            fechar.Content = Tr.T("Fechar");
            acao.Visibility = codigo == 0 ? Visibility.Collapsed : Visibility.Visible;
            acao.IsEnabled = codigo != 0;
        };

        Content = pilha;
    }
}
