using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A lista de cores das janelas (escolher a cor de uma análise, de uma
/// mesa): um quadradinho da cor e o nome. A mesma paleta da tela de
/// configuração do 4.4, mais a cor que veio do desenho quando ela não está
/// na paleta.
/// </summary>
internal static class PaletaDeCores
{
    internal static readonly (string Nome, RgbColor Cor)[] Cores =
    [
        ("Vermelho", RgbColor.Red),
        ("Azul", RgbColor.Blue),
        ("Magenta", RgbColor.Magenta),
        ("Amarelo", new RgbColor(255, 255, 0)),
        ("Laranja", new RgbColor(255, 128, 0)),
        ("Verde", new RgbColor(0, 160, 0)),
        ("Ciano", new RgbColor(0, 200, 200)),
        ("Roxo", new RgbColor(128, 0, 200)),
        ("Rosa", new RgbColor(255, 105, 180)),
        ("Marrom", new RgbColor(150, 90, 40)),
        ("Branco", new RgbColor(255, 255, 255)),
        ("Preto", new RgbColor(0, 0, 0)),
    ];

    /// <summary>Uma caixa de escolha de cor, já com a cor dada escolhida.</summary>
    internal static ComboBox Caixa(RgbColor escolhida, string dica)
    {
        var caixa = new ComboBox { Height = 24, MinWidth = 130, ToolTip = dica };

        foreach (var (nome, cor) in Cores) caixa.Items.Add(Item(nome, cor));

        var indice = Array.FindIndex(Cores, c => c.Cor == escolhida);

        if (indice < 0)
        {
            caixa.Items.Add(Item(escolhida.ToHex(), escolhida));
            indice = caixa.Items.Count - 1;
        }

        caixa.SelectedIndex = indice;
        PermitirMaisCores(caixa, c => Item(c.ToHex(), c), i => i.Tag as RgbColor?);
        return caixa;
    }

    /// <summary>A cor escolhida na caixa.</summary>
    internal static RgbColor Cor(ComboBox caixa) =>
        (caixa.SelectedItem as ComboBoxItem)?.Tag is RgbColor cor ? cor : RgbColor.Red;

    /// <summary>A marca do item "Mais cores..." (o Tag dele).</summary>
    private sealed class MaisCores;

    /// <summary>
    /// Põe no fim da caixa o item "Mais cores...", que abre a janela de cor
    /// do Windows (RGB, cores personalizadas). A cor escolhida entra na lista
    /// e fica escolhida; cancelar volta à de antes. (Renan, 02/10/2026: "em
    /// toda seleção de cor, não quero ficar limitado à lista".)
    /// </summary>
    /// <param name="caixa">A caixa de cor.</param>
    /// <param name="itemDe">Como a caixa monta o item de uma cor.</param>
    /// <param name="corDoItem">A cor de um item da caixa, ou null.</param>
    internal static void PermitirMaisCores(ComboBox caixa, Func<RgbColor, ComboBoxItem> itemDe, Func<ComboBoxItem, RgbColor?> corDoItem)
    {
        var mais = new ComboBoxItem
        {
            Content = new TextBlock { Text = "Mais cores...", FontStyle = FontStyles.Italic },
            Tag = new MaisCores(),
            ToolTip = "Abre a janela de cor do Windows: qualquer cor, por RGB ou pela paleta.",
        };

        caixa.Items.Add(mais);

        var anterior = caixa.SelectedItem;

        caixa.SelectionChanged += (_, _) =>
        {
            if (!ReferenceEquals(caixa.SelectedItem, mais))
            {
                anterior = caixa.SelectedItem;
                return;
            }

            try
            {
                var atual = anterior is ComboBoxItem i && corDoItem(i) is { } c ? c : RgbColor.Red;

                using var dialogo = new System.Windows.Forms.ColorDialog
                {
                    FullOpen = true,
                    AnyColor = true,
                    Color = System.Drawing.Color.FromArgb(atual.R, atual.G, atual.B),
                };

                if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var escolhida = new RgbColor(dialogo.Color.R, dialogo.Color.G, dialogo.Color.B);
                    var item = itemDe(escolhida);

                    caixa.Items.Insert(caixa.Items.IndexOf(mais), item);
                    caixa.SelectedItem = item;
                }
                else
                {
                    caixa.SelectedItem = anterior;
                }
            }
            catch (Exception erro)
            {
                // Manipulador de evento do WPF: exceção solta fecharia o Civil 3D.
                RegistroDeDiagnostico.Registrar("Falha na janela de cor.", erro);
                caixa.SelectedItem = anterior;
            }
        };
    }

    private static ComboBoxItem Item(string nome, RgbColor cor)
    {
        var painel = new StackPanel { Orientation = Orientation.Horizontal };

        painel.Children.Add(new Rectangle
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 6, 0),
            Fill = new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B)),
            Stroke = Brushes.Gray,
        });
        painel.Children.Add(new TextBlock { Text = nome });

        return new ComboBoxItem { Content = painel, Tag = cor };
    }
}
