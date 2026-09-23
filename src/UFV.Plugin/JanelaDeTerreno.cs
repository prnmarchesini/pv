using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A janela de escolha da superfície do terreno.
///
/// Montada em código, sem XAML: são um rótulo, uma lista e dois botões, e um
/// arquivo de marcação a mais custaria mais do que economiza. Quando a
/// interface crescer (etapa 3 em diante tem modais de verdade), vale mudar.
///
/// Quem abre esta janela é <see cref="EscolhaDeTerreno"/>, e só quando há
/// interface. O tipo não pode ser nomeado por nenhum caminho que rode no Core
/// Console.
/// </summary>
internal sealed class JanelaDeTerreno : Window
{
    private readonly ListBox _lista;
    private readonly Button _confirmar;

    internal JanelaDeTerreno(IReadOnlyList<SurfaceEntry> superficies)
    {
        Title = "UFV — Terreno";
        Width = 460;
        Height = 360;
        // CenterOwner, e não CenterScreen: com dois monitores, a janela tem
        // que nascer sobre o Civil 3D, não no meio da tela principal.
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = false;

        _lista = new ListBox { Margin = new Thickness(0, 8, 0, 8) };

        foreach (var superficie in superficies)
        {
            var item = new ListBoxItem
            {
                Content = superficie.Summary.Describe(),
                Tag = superficie,
                // Superfície sem ponto nenhum aparece, para o usuário entender
                // por que não pode escolhê-la, mas não dá para confirmar.
                IsEnabled = superficie.Summary.CanBeTerrain,
            };

            // O duplo clique fica no item, não na lista. Na lista, um duplo
            // clique na área vazia abaixo das linhas confirmaria a seleção
            // anterior — entregando um terreno que o usuário não escolheu,
            // que é exatamente o erro que esta janela existe para evitar.
            item.MouseDoubleClick += (_, e) =>
            {
                e.Handled = true;
                Confirmar();
            };

            _lista.Items.Add(item);
        }

        _lista.SelectionChanged += (_, _) => AtualizarBotao();

        _confirmar = new Button
        {
            Content = "Usar como terreno",
            Width = 150,
            Height = 26,
            IsDefault = true,
            IsEnabled = false,
        };
        _confirmar.Click += (_, _) => Confirmar();

        var cancelar = new Button
        {
            Content = "Cancelar",
            Width = 90,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
        };

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        botoes.Children.Add(_confirmar);
        botoes.Children.Add(cancelar);

        var conteudo = new DockPanel { Margin = new Thickness(12) };

        var titulo = new TextBlock
        {
            Text = "Qual destas superfícies é o terreno?",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        DockPanel.SetDock(titulo, Dock.Top);
        conteudo.Children.Add(titulo);

        DockPanel.SetDock(botoes, Dock.Bottom);
        conteudo.Children.Add(botoes);

        conteudo.Children.Add(_lista);
        Content = conteudo;

        // A primeira aproveitável já vem marcada: no caso comum há uma só.
        var primeira = _lista.Items
            .OfType<ListBoxItem>()
            .FirstOrDefault(i => i.IsEnabled);

        if (primeira is not null)
        {
            _lista.SelectedItem = primeira;
            primeira.Focus();
        }
    }

    /// <summary>A superfície escolhida, ou null se o usuário desistiu.</summary>
    internal SurfaceEntry? Escolhida { get; private set; }

    private void AtualizarBotao() =>
        _confirmar.IsEnabled = Selecionada() is not null;

    private SurfaceEntry? Selecionada() =>
        (_lista.SelectedItem as ListBoxItem)?.Tag is SurfaceEntry entrada
        && entrada.Summary.CanBeTerrain
            ? entrada
            : null;

    private void Confirmar()
    {
        var escolhida = Selecionada();
        if (escolhida is null) return;

        Escolhida = escolhida;
        DialogResult = true;
    }
}
