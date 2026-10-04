using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clivus.Plugin;

/// <summary>A janela do resumo do terreno (passo 8.15), com os botões de trocar o terreno e a localização.</summary>
internal sealed class JanelaDeResumoDoTerreno : Window
{
    internal enum Acao
    {
        Nada,
        TrocarTerreno,
        Localizacao,
    }

    internal Acao Escolha { get; private set; }

    internal JanelaDeResumoDoTerreno(IReadOnlyList<string> linhas)
    {
        Title = "Clivus Solar — Terreno";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pilha = new StackPanel { Margin = new Thickness(14) };

        for (var i = 0; i < linhas.Count; i++)
        {
            pilha.Children.Add(new TextBlock
            {
                Text = linhas[i],
                TextWrapping = TextWrapping.Wrap,
                FontSize = i == 0 ? 14 : 12.5,
                FontWeight = i == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, 0, 0, 5),
            });
        }

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };

        Button Botao(string texto, string dica, Acao acao, bool cancela = false)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(8, 0, 0, 0), ToolTip = dica, IsCancel = cancela };
            b.Click += (_, _) => { Escolha = acao; DialogResult = acao != Acao.Nada; };
            return b;
        }

        botoes.Children.Add(Botao("Localização...", "Informar ou corrigir a latitude e a longitude, quando o desenho não tem sistema de coordenadas.", Acao.Localizacao));
        botoes.Children.Add(Botao("Fechar", "Fecha o resumo.", Acao.Nada, cancela: true));

        pilha.Children.Add(new TextBlock
        {
            Text = "A cidade é o município do IBGE com a sede mais perto; o fuso sai da longitude.",
            Foreground = Brushes.Gray,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        });
        pilha.Children.Add(botoes);

        Content = pilha;
    }
}
