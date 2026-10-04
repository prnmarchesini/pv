using System.Windows;
using System.Windows.Controls;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Trocar mesa (9.2): o tipo novo, quantas no lugar, o lado que fica preso e
/// se a fileira é reespaçada depois. Tudo numa linha por campo, rótulo ao
/// lado (regra de 02/10/2026: nada de espaço à toa).
/// </summary>
internal sealed class JanelaDeTroca : Window
{
    private readonly ComboBox _tipo = new() { Width = 300, ToolTip = "A mesa que entra no lugar: as mesas cadastradas neste desenho (Configurações > Estruturas)." };
    private readonly ComboBox _quantas = new() { Width = 60, ToolTip = "Quantas mesas novas no lugar da antiga, uma depois da outra, com o espaçamento entre mesas da configuração." };
    private readonly RadioButton _inicio = new() { Content = "Início", IsChecked = true, Margin = new Thickness(0, 0, 14, 0), ToolTip = "A primeira mesa nova começa onde a antiga começava (o lado do primeiro pilar)." };
    private readonly RadioButton _fim = new() { Content = "Fim", ToolTip = "A última mesa nova termina onde a antiga terminava (o lado do último pilar)." };
    private readonly CheckBox _reespacar = new()
    {
        Content = "Reespaçar a fileira depois (mantém as mesas, acerta o espaçamento)",
        ToolTip = "Depois da troca, as mesas da fileira são postas de novo com o espaçamento da configuração, cada uma com o tipo dela. Sem isso, as vizinhas não se mexem e a troca pode passar delas.",
        Margin = new Thickness(0, 8, 0, 0),
    };

    internal TrocarMesaCommands.Escolha? Escolhida { get; private set; }

    internal JanelaDeTroca(IReadOnlyList<DrawingTable> mesas, string letreiro)
    {
        Title = $"Clivus Solar — Trocar a mesa {letreiro}";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var m in mesas) _tipo.Items.Add($"{m.Name} — {m.Profile.Layout.ModuleCount} módulos, {m.Profile.Layout.Length:0.##} m");
        _tipo.SelectedIndex = 0;

        for (var i = 1; i <= 5; i++) _quantas.Items.Add(i);
        _quantas.SelectedIndex = 0;

        var grade = new Grid { Margin = new Thickness(12) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition());

        void Linha(int linha, string rotulo, UIElement campo)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var texto = new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 10, 4) };
            Grid.SetRow(texto, linha);
            grade.Children.Add(texto);

            if (campo is FrameworkElement f) f.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(campo, linha);
            Grid.SetColumn(campo, 1);
            grade.Children.Add(campo);
        }

        var lados = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        lados.Children.Add(_inicio);
        lados.Children.Add(_fim);

        Linha(0, "Mesa nova", _tipo);
        Linha(1, "Quantas", _quantas);
        Linha(2, "Lado travado", lados);

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_reespacar, 3);
        Grid.SetColumnSpan(_reespacar, 2);
        grade.Children.Add(_reespacar);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Trocar", Width = 90, Height = 26, IsDefault = true, ToolTip = "Apaga a mesa clicada e desenha as novas no lugar, assentadas no terreno." };
        ok.Click += (_, _) =>
        {
            Escolhida = new TrocarMesaCommands.Escolha(
                _tipo.SelectedIndex, (int)_quantas.SelectedItem!, _fim.IsChecked == true ? SwapAnchor.End : SwapAnchor.Start, _reespacar.IsChecked == true);
            DialogResult = true;
        };
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = "Cancelar", Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(botoes, 4);
        Grid.SetColumnSpan(botoes, 2);
        grade.Children.Add(botoes);

        Content = grade;
    }
}
