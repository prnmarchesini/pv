using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A janela do resumo da mesa (42.9): as seções em duas colunas, rótulo ao
/// lado do valor (regra de 02/10/2026: nada de espaço à toa nem rolagem), a
/// orientação em cima à esquerda, que é o que o Renan queria ver. O Copiar
/// leva o resumo em texto para a área de transferência.
/// </summary>
internal sealed class JanelaDoResumoDaMesa : Window
{
    internal JanelaDoResumoDaMesa(TableSummary resumo)
    {
        Title = Tr.F("Clivus Solar — Resumo da mesa {0}", resumo.Label);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var secoes = resumo.Sections();
        var colunas = new Grid { Margin = new Thickness(12, 10, 12, 0) };
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Esquerda: orientação, terreno e a mesa; direita: módulos e pilares.
        var esquerda = new StackPanel();
        var direita = new StackPanel();
        for (var i = 0; i < secoes.Count; i++) (i < 3 ? esquerda : direita).Children.Add(Secao(secoes[i].Title, secoes[i].Lines));

        Grid.SetColumn(esquerda, 0);
        Grid.SetColumn(direita, 2);
        colunas.Children.Add(esquerda);
        colunas.Children.Add(direita);

        var copiar = new Button { Content = Tr.T("Copiar"), Height = 26, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = Tr.T("Copia o resumo em texto.") };
        var recado = new TextBlock { Foreground = Brushes.ForestGreen, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        copiar.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(resumo.Describe());
                recado.Text = Tr.T("Copiado.");
            }
            catch (Exception erro)
            {
                // A área de transferência pode estar presa por outro programa.
                RegistroDeDiagnostico.Registrar("Falha ao copiar o resumo da mesa.", erro);
                recado.Foreground = Brushes.Firebrick;
                recado.Text = Tr.F("Não consegui copiar: {0}", erro.Message);
            }
        };
        var fechar = new Button { Content = Tr.T("Fechar"), Width = 80, Height = 26, IsCancel = true, IsDefault = true };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 10, 12, 12) };
        botoes.Children.Add(recado);
        botoes.Children.Add(copiar);
        botoes.Children.Add(fechar);

        var tudo = new StackPanel();
        tudo.Children.Add(colunas);
        tudo.Children.Add(botoes);
        Content = tudo;
    }

    /// <summary>Uma seção: o título e as linhas, rótulo cinza à esquerda e valor ao lado.</summary>
    private static FrameworkElement Secao(string titulo, IReadOnlyList<(string Label, string Value)> linhas)
    {
        var grade = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cabeca = new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetColumnSpan(cabeca, 2);
        grade.Children.Add(cabeca);

        for (var i = 0; i < linhas.Count; i++)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var rotulo = new TextBlock { Text = linhas[i].Label, Foreground = Brushes.DimGray, Margin = new Thickness(0, 1, 12, 1) };
            var valor = new TextBlock { Text = linhas[i].Value, Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };

            Grid.SetRow(rotulo, i + 1);
            Grid.SetRow(valor, i + 1);
            Grid.SetColumn(valor, 1);
            grade.Children.Add(rotulo);
            grade.Children.Add(valor);
        }

        return grade;
    }
}
