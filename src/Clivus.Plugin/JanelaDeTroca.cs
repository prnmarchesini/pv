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
    private readonly ComboBox _tipo = new() { Width = 300, ToolTip = Tr.T("A mesa que entra no lugar: as mesas cadastradas neste desenho (Configurações > Estruturas).") };
    private readonly ComboBox _quantas = new() { Width = 60, ToolTip = Tr.T("Quantas mesas novas no lugar da antiga, uma depois da outra, com o espaçamento entre mesas da configuração.") };
    private readonly RadioButton _esquerda = new() { Content = Tr.T("Esquerda"), IsChecked = true, Margin = new Thickness(0, 0, 14, 0), ToolTip = Tr.T("A ponta esquerda fica parada, olhando o desenho com o norte para cima (a ponta de menor X; numa fileira norte-sul, a ponta sul). Com \"Refazer a fileira inteira\", vale para a ponta esquerda da fileira.") };
    private readonly RadioButton _direita = new() { Content = Tr.T("Direita"), ToolTip = Tr.T("A ponta direita fica parada, olhando o desenho com o norte para cima (a ponta de maior X; numa fileira norte-sul, a ponta norte). Com \"Refazer a fileira inteira\", vale para a ponta direita da fileira.") };
    private readonly CheckBox _reespacar = new()
    {
        Content = Tr.T("Refazer a fileira inteira (acerta o espaçamento)"),
        ToolTip = Tr.T("Depois da troca, as mesas da fileira são postas de novo com o espaçamento da configuração, cada uma com o tipo dela, encostadas na ponta do lado travado, que fica parada; as strings da fileira são apagadas. Sem isso, as vizinhas não se mexem e a troca pode passar delas."),
        Margin = new Thickness(0, 8, 0, 0),
    };

    internal TrocarMesaCommands.Escolha? Escolhida { get; private set; }

    internal JanelaDeTroca(IReadOnlyList<DrawingTable> mesas, string letreiro)
    {
        Title = Tr.F("Clivus Solar — Trocar a mesa {0}", letreiro);
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var m in mesas) _tipo.Items.Add(Tr.F("{0} — {1} módulos, {2:0.##} m", m.Name, m.Profile.Layout.ModuleCount, m.Profile.Layout.Length));
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
        lados.Children.Add(_esquerda);
        lados.Children.Add(_direita);

        Linha(0, Tr.T("Mesa nova"), _tipo);
        Linha(1, Tr.T("Quantas"), _quantas);
        Linha(2, Tr.T("Lado travado"), lados);

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_reespacar, 3);
        Grid.SetColumnSpan(_reespacar, 2);
        grade.Children.Add(_reespacar);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = Tr.T("Trocar"), Width = 90, Height = 26, IsDefault = true, ToolTip = Tr.T("Apaga a mesa clicada e desenha as novas no lugar, assentadas no terreno.") };
        ok.Click += (_, _) =>
        {
            Escolhida = new TrocarMesaCommands.Escolha(
                _tipo.SelectedIndex, (int)_quantas.SelectedItem!, _direita.IsChecked == true ? RowSide.Right : RowSide.Left, _reespacar.IsChecked == true);
            DialogResult = true;
        };
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(botoes, 4);
        Grid.SetColumnSpan(botoes, 2);
        grade.Children.Add(botoes);

        Content = grade;
    }
}
