using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>O tipo de estrutura nova (35.1).</summary>
internal enum TipoDeEstrutura
{
    Fixa,
    Carport,

    /// <summary>Fixa com dois pilares por pórtico (38.1).</summary>
    Bifila,
}

/// <summary>
/// Configurações › Estruturas › Nova estrutura... (35.1): a pergunta do tipo.
/// Renan, 28/10/2026: <i>"ele entra em configurações, em nova estrutura, ai
/// coloca que tem fixo e carport"</i>. Fixa segue para a janela de Mesa de
/// sempre; Carport, para a janela do carport. Desde 30/10/2026 (38.1) há a
/// Fixa bifila: a mesma janela de Mesa, com a seção da bifila.
/// </summary>
internal sealed class JanelaNovaEstrutura : Window
{
    /// <summary>O tipo escolhido, ou null se fechou.</summary>
    internal TipoDeEstrutura? Escolhido { get; private set; }

    internal JanelaNovaEstrutura()
    {
        Title = Tr.T("Nova estrutura");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var painel = new StackPanel { Margin = new Thickness(14) };
        painel.Children.Add(new TextBlock { Text = Tr.T("Que tipo de estrutura?"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

        var linha = new StackPanel { Orientation = Orientation.Horizontal };
        linha.Children.Add(Opcao(Tr.T("Fixa"), Tr.T("Mesa fixa no terreno, com tesoura e pilares cravados. A janela de Mesa de sempre."), TipoDeEstrutura.Fixa));
        linha.Children.Add(Opcao(Tr.T("Fixa bifila"), Tr.T("Mesa fixa com dois pilares por pórtico (dianteiro e traseiro), mãos francesas, terças e tirantes. Abre com a última bifila do desenho, ou com a Arctech 2P14 GC800."), TipoDeEstrutura.Bifila));
        linha.Children.Add(Opcao(Tr.T("Carport"), Tr.T("Cobertura de estacionamento: pórticos biposte em fila, módulos por cima, altura livre para os carros."), TipoDeEstrutura.Carport));
        painel.Children.Add(linha);

        var cancelar = new Button { Content = Tr.T("Cancelar"), Width = 90, Height = 26, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        painel.Children.Add(cancelar);

        Content = painel;
    }

    private Button Opcao(string titulo, string texto, TipoDeEstrutura tipo)
    {
        var conteudo = new StackPanel { Width = 210, Margin = new Thickness(6) };
        conteudo.Children.Add(new TextBlock { Text = titulo, FontSize = 15, FontWeight = FontWeights.SemiBold });
        conteudo.Children.Add(new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) });

        var botao = new Button { Content = conteudo, Margin = new Thickness(0, 0, 10, 0), HorizontalContentAlignment = HorizontalAlignment.Left };
        botao.Click += (_, _) =>
        {
            Escolhido = tipo;
            DialogResult = true;
        };
        return botao;
    }
}
