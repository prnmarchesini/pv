using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UFV.Plugin;

/// <summary>
/// Reorganiza os formulários feitos em pilha (título de seção, rótulo em
/// cima, campo embaixo) em seções lado a lado, rótulo à esquerda do campo
/// (Renan, 02/10/2026: "para que tanto espaço em branco e uma barra de
/// rolagem? Otimizar espaço e melhorar UX de tudo").
///
/// As seções vão num WrapPanel vertical: enchem uma coluna e passam para a
/// próxima, ocupando a largura da janela em vez de rolar.
/// </summary>
internal static class LayoutCompacto
{
    /// <summary>A largura de cada seção.</summary>
    internal const double LarguraDaSecao = 320;

    /// <summary>O título de seção dos formulários é um TextBlock pequeno, cinza, em maiúsculas.</summary>
    private static bool ETitulo(UIElement e) =>
        e is TextBlock t && Math.Abs(t.FontSize - 10.5) < 0.01 && t.Text == t.Text.ToUpperInvariant();

    /// <summary>
    /// As seções da pilha, cada uma num GroupBox. A pilha é esvaziada (os
    /// controles mudam de dono; os eventos ligados neles continuam valendo).
    /// </summary>
    internal static WrapPanel Secoes(Panel pilha, double largura = LarguraDaSecao)
    {
        var itens = pilha.Children.Cast<UIElement>().ToList();
        pilha.Children.Clear();

        var secoes = new WrapPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0) };
        Grid? grade = null;

        Grid NovaSecao(string titulo)
        {
            var g = new Grid { Margin = new Thickness(2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            secoes.Children.Add(new GroupBox
            {
                Header = new TextBlock { Text = Capitalizar(titulo), FontWeight = FontWeights.SemiBold },
                Content = g,
                Width = largura,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(4, 2, 4, 4),
            });

            return g;
        }

        void Linha(UIElement? rotulo, UIElement campo, bool larguraToda)
        {
            grade ??= NovaSecao("Geral");
            var linha = grade.RowDefinitions.Count;
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            if (rotulo is not null)
            {
                Grid.SetRow(rotulo, linha);
                Grid.SetColumn(rotulo, 0);
                Grid.SetColumnSpan(rotulo, larguraToda ? 2 : 1);
                grade.Children.Add(rotulo);

                if (larguraToda)
                {
                    // Rótulo em cima só quando o campo é largo (listas).
                    linha = grade.RowDefinitions.Count;
                    grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
            }

            Grid.SetRow(campo, linha);
            Grid.SetColumn(campo, larguraToda || rotulo is null ? 0 : 1);
            Grid.SetColumnSpan(campo, larguraToda || rotulo is null ? 2 : 1);
            grade.Children.Add(campo);
        }

        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];

            if (ETitulo(item))
            {
                grade = NovaSecao(((TextBlock)item).Text);
                continue;
            }

            // Rótulo seguido do campo: lado a lado, campo estreito à direita;
            // lista (ComboBox) fica embaixo, com a largura toda.
            if (item is TextBlock rotulo && i + 1 < itens.Count && itens[i + 1] is TextBox or ComboBox)
            {
                var campo = (Control)itens[i + 1];
                // Lista e texto livre (o nome) ocupam a largura toda; número, a coluna estreita.
                var lista = campo is ComboBox || rotulo.Text.StartsWith("Nome", StringComparison.Ordinal);

                rotulo.VerticalAlignment = VerticalAlignment.Center;
                rotulo.TextWrapping = TextWrapping.Wrap;
                rotulo.Margin = new Thickness(0, 2, 6, 2);

                campo.Margin = new Thickness(0, 2, 0, 2);
                if (!lista) campo.Width = 90;

                Linha(rotulo, campo, lista);
                i++;
                continue;
            }

            if (item is FrameworkElement elemento) elemento.Margin = new Thickness(0, 2, 0, 2);
            Linha(null, item, true);
        }

        return secoes;
    }

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0]) + texto[1..].ToLowerInvariant();
}
