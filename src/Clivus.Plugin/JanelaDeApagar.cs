using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A janela do Apagar da Edição (05/10/2026): uma caixa de marcar por opção,
/// com o que ela leva ao lado (as contagens lidas do desenho), e Apagar /
/// Cancelar. Apagar pede a confirmação com as contagens das marcadas; a
/// opção sem nada a fazer fica cinza. Compacta (regra de UX do Renan:
/// rótulo ao lado, nada de espaço à toa). Montada em código, como as outras.
/// </summary>
internal sealed class JanelaDeApagar : Window
{
    private readonly CleanupCount _contagem;
    private readonly List<(CleanupOptions Opcao, CheckBox Caixa)> _caixas = [];
    private readonly Button _apagar;

    /// <summary>As opções confirmadas (None se cancelou).</summary>
    internal CleanupOptions Opcoes { get; private set; }

    internal JanelaDeApagar(CleanupCount contagem)
    {
        _contagem = contagem;

        Title = Tr.T("Apagar");
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 320;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var painel = new StackPanel { Margin = new Thickness(12) };
        painel.Children.Add(new TextBlock
        {
            Text = Tr.T("No desenho todo, só o que o Clivus Solar criou:"),
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 6),
        });

        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dicas = new Dictionary<CleanupOptions, string>
        {
            [CleanupOptions.Colors] = Tr.T("Mesas, strings e textos de análise voltam à cor original (a do tipo de mesa; magenta ou roxo na que não cabe). Tira as pinturas das análises, da sombra, da pendência e dos inversores."),
            [CleanupOptions.Texts] = Tr.T("Cotas, setas e textos das análises, tags, numeração das strings, sinais + e − e etiquetas de sombra. A marca dos grupos fica."),
            [CleanupOptions.Shadows] = Tr.T("Contornos e etiquetas de sombra e o registro do \"Por que essa sombra?\"; os módulos marcados voltam à cor de antes. As árvores ficam."),
            [CleanupOptions.Strings] = Tr.T("O traçado de todas as strings, com os sinais e as tags. Os tipos de string ficam."),
            [CleanupOptions.Electrical] = Tr.T("Inversores, trafos, subestações e skids (o cadastro e os blocos em campo). As strings ficam soltas, sem tag. Os modelos de inversor ficam."),
        };

        var linha = 0;
        foreach (var opcao in Cleanup.Each)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var vazio = _contagem.Of(opcao) == 0;
            var caixa = new CheckBox
            {
                Content = $"{linha + 1}  {Cleanup.Name(opcao)}",
                IsEnabled = !vazio,
                ToolTip = dicas[opcao],
                Margin = new Thickness(0, 3, 14, 3),
                VerticalAlignment = VerticalAlignment.Center,
            };
            caixa.Checked += (_, _) => Conferir();
            caixa.Unchecked += (_, _) => Conferir();
            Grid.SetRow(caixa, linha);
            grade.Children.Add(caixa);
            _caixas.Add((opcao, caixa));

            var quanto = new TextBlock
            {
                Text = vazio ? Tr.T("nada") : _contagem.Describe(opcao),
                Foreground = Brushes.DimGray,
                ToolTip = dicas[opcao],
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(quanto, linha);
            Grid.SetColumn(quanto, 1);
            grade.Children.Add(quanto);

            linha++;
        }

        painel.Children.Add(grade);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };

        _apagar = new Button { Content = Tr.T("Apagar"), MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };
        _apagar.Click += (_, _) => Confirmar();
        botoes.Children.Add(_apagar);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), MinWidth = 80, IsCancel = true });

        painel.Children.Add(botoes);
        Content = painel;
    }

    private CleanupOptions Marcadas() =>
        _caixas.Where(c => c.Caixa.IsChecked == true).Aggregate(CleanupOptions.None, (a, c) => a | c.Opcao);

    private void Conferir()
    {
        try
        {
            var marcadas = Marcadas();
            _apagar.IsEnabled = marcadas != CleanupOptions.None && !_contagem.IsEmpty(marcadas);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir as caixas do Apagar.", erro);
        }
    }

    private void Confirmar()
    {
        try
        {
            var marcadas = Marcadas();
            if (marcadas == CleanupOptions.None) return;

            var texto = Tr.T("Vai apagar, no desenho todo:") + "\n\n"
                + string.Join("\n", _contagem.Lines(marcadas).Select(l => "•  " + l))
                + "\n\n" + Tr.T("Só o que o Clivus Solar criou; o resto do desenho fica. U desfaz. Continuar?");

            if (MessageBox.Show(this, texto, Tr.T("Apagar"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

            Opcoes = marcadas;
            DialogResult = true;
            Close();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao confirmar o Apagar.", erro);
        }
    }
}
