using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// As medidas da árvore (9.4): tronco e copa, altura e largura de cada um,
/// lado a lado, com o desenho do pirulito para lembrar o que é cada medida.
/// </summary>
internal sealed class JanelaDeArvore : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly TextBox _alturaTronco;
    private readonly TextBox _larguraTronco;
    private readonly TextBox _alturaCopa;
    private readonly TextBox _larguraCopa;
    private readonly TextBlock _recado = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    internal TreeSpec? Medidas { get; private set; }

    internal JanelaDeArvore(TreeSpec atual)
    {
        Title = "UFV — Árvore";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        TextBox Campo(double valor, string dica) =>
            new() { Text = valor.ToString("0.##", Brasil), Width = 70, Margin = new Thickness(0, 3, 0, 3), ToolTip = dica };

        _alturaTronco = Campo(atual.TrunkHeight, "Do chão até onde a copa começa, em metro.");
        _larguraTronco = Campo(atual.TrunkWidth, "O diâmetro do tronco, em metro.");
        _alturaCopa = Campo(atual.CrownHeight, "Da base da copa até o topo da árvore, em metro.");
        _larguraCopa = Campo(atual.CrownWidth, "O diâmetro da copa, em metro.");

        var grade = new Grid();
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        void Linha(int linha, string rotulo, TextBox altura, TextBox largura)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var texto = new TextBlock { Text = rotulo, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var a = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };
            a.Children.Add(new TextBlock { Text = "altura ", VerticalAlignment = VerticalAlignment.Center });
            a.Children.Add(altura);
            var l = new StackPanel { Orientation = Orientation.Horizontal };
            l.Children.Add(new TextBlock { Text = "largura ", VerticalAlignment = VerticalAlignment.Center });
            l.Children.Add(largura);

            Grid.SetRow(texto, linha);
            Grid.SetRow(a, linha);
            Grid.SetColumn(a, 1);
            Grid.SetRow(l, linha);
            Grid.SetColumn(l, 2);
            grade.Children.Add(texto);
            grade.Children.Add(a);
            grade.Children.Add(l);
        }

        Linha(0, "Copa", _alturaCopa, _larguraCopa);
        Linha(1, "Tronco", _alturaTronco, _larguraTronco);

        // O pirulito, para lembrar o que é cada medida.
        var desenho = new Canvas { Width = 60, Height = 70, Margin = new Thickness(0, 0, 14, 0) };
        desenho.Children.Add(new System.Windows.Shapes.Rectangle { Width = 44, Height = 34, Fill = new SolidColorBrush(Color.FromRgb(40, 140, 60)) });
        Canvas.SetLeft(desenho.Children[0], 8);
        desenho.Children.Add(new System.Windows.Shapes.Rectangle { Width = 8, Height = 32, Fill = new SolidColorBrush(Color.FromRgb(120, 80, 40)) });
        Canvas.SetLeft(desenho.Children[1], 26);
        Canvas.SetTop(desenho.Children[1], 34);

        var corpo = new StackPanel { Orientation = Orientation.Horizontal };
        corpo.Children.Add(desenho);
        corpo.Children.Add(grade);

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(corpo);
        pilha.Children.Add(new TextBlock
        {
            Text = "Depois clique onde pôr cada árvore (Enter termina). O pé fica no terreno, e a árvore arrastada volta ao chão do lugar novo.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });
        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "Pôr árvores", Width = 100, Height = 26, IsDefault = true, ToolTip = "Fecha a janela e começa os cliques no desenho." };
        ok.Click += (_, _) => Confirmar();
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = "Cancelar", Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        pilha.Children.Add(botoes);

        Content = pilha;
    }

    private void Confirmar()
    {
        bool Ler(TextBox caixa, out double v) => NumberInput.TryParseMeasure(caixa.Text, out v);

        if (!Ler(_alturaTronco, out var ht) || !Ler(_larguraTronco, out var lt) || !Ler(_alturaCopa, out var hc) || !Ler(_larguraCopa, out var lc))
        {
            _recado.Text = "Todas as medidas precisam ser números, em metro.";
            return;
        }

        var medidas = new TreeSpec(ht, lt, hc, lc);

        if (medidas.WhyInvalid() is { } porque)
        {
            _recado.Text = char.ToUpper(porque[0], Brasil) + porque[1..] + ".";
            return;
        }

        Medidas = medidas;
        DialogResult = true;
    }
}
