using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Os estilos do projeto (passo 8.13): qual estilo de texto, de cota e de
/// chamada o plugin usa. Também é a aba "Projeto" da janela de
/// Configurações (8.7), pelo <see cref="Conteudo"/>.
/// </summary>
internal sealed class JanelaDeEstilos : Window
{
    internal static string Corrente => Tr.T("(o corrente do desenho)");

    private readonly PainelDeEstilos _painel;

    internal ProjectStyles? Escolhidos { get; private set; }

    internal JanelaDeEstilos(IReadOnlyList<string> textos, IReadOnlyList<string> cotas, IReadOnlyList<string> chamadas, ProjectStyles atuais)
    {
        Title = Tr.T("Clivus Solar — Estilos do projeto");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _painel = new PainelDeEstilos(textos, cotas, chamadas, atuais);

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(_painel);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = Tr.T("Salvar no desenho"), Height = 26, Padding = new Thickness(10, 0, 10, 0), IsDefault = true, ToolTip = Tr.T("Grava a escolha no desenho; vale para os próximos textos do plugin.") };
        ok.Click += (_, _) => { Escolhidos = _painel.Ler(); DialogResult = true; };
        botoes.Children.Add(ok);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        pilha.Children.Add(botoes);

        Content = pilha;
    }
}

/// <summary>Os três campos de estilo, para a janela própria e para a aba do modal de Configurações.</summary>
internal sealed class PainelDeEstilos : StackPanel
{
    private readonly ComboBox _texto;
    private readonly ComboBox _cota;
    private readonly ComboBox _chamada;

    internal PainelDeEstilos(IReadOnlyList<string> textos, IReadOnlyList<string> cotas, IReadOnlyList<string> chamadas, ProjectStyles atuais)
    {
        _texto = Caixa(textos, atuais.TextStyle, Tr.T("O estilo dos textos do plugin: alturas, análises, declividade, tags, grupos. Anotativo, o texto sai na altura de papel do estilo pela escala de anotação corrente."));
        _cota = Caixa(cotas, atuais.DimensionStyle, Tr.T("O estilo das cotas (DIMSTYLE) que o plugin vier a desenhar."));
        _chamada = Caixa(chamadas, atuais.LeaderStyle, Tr.T("O estilo das chamadas (MLEADERSTYLE) que o plugin vier a desenhar."));

        Children.Add(new TextBlock
        {
            Text = Tr.T("Para imprimir com a mesma escala do layout, use estilos anotativos."),
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });

        Linha(Tr.T("Estilo de texto"), _texto);
        Linha(Tr.T("Estilo de cota"), _cota);
        Linha(Tr.T("Estilo de chamada (leader)"), _chamada);
    }

    internal ProjectStyles Ler() => new(Nome(_texto), Nome(_cota), Nome(_chamada));

    private void Linha(string rotulo, ComboBox caixa)
    {
        Children.Add(new TextBlock { Text = rotulo, ToolTip = caixa.ToolTip });
        Children.Add(caixa);
    }

    private static ComboBox Caixa(IReadOnlyList<string> nomes, string? atual, string dica)
    {
        var caixa = new ComboBox { Height = 24, Margin = new Thickness(0, 2, 0, 8), ToolTip = dica };

        caixa.Items.Add(JanelaDeEstilos.Corrente);
        foreach (var nome in nomes.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)) caixa.Items.Add(nome);

        var achado = ProjectStyles.Match(atual, nomes);
        caixa.SelectedItem = achado is null ? JanelaDeEstilos.Corrente : achado;

        return caixa;
    }

    private static string? Nome(ComboBox caixa) =>
        caixa.SelectedItem is string nome && nome != JanelaDeEstilos.Corrente ? nome : null;
}
