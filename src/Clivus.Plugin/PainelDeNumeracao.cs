using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A aba Numeração da janela da configuração elétrica (etapa 15). A janela
/// (etapas 12 a 14) só chama <see cref="Criar"/>; CLIVUS_NUMERACAO mostra o
/// mesmo painel numa janela solta. A regra toda está no Core
/// (<see cref="TagScheme"/>); aqui só se mostra, grava e desenha.
/// </summary>
internal sealed class PainelDeNumeracao : DockPanel
{
    private readonly Document _documento;
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    // 15.1: a composição da tag.
    private readonly CheckBox _comTrafo = new() { Content = Tr.T("Trafo"), VerticalAlignment = VerticalAlignment.Center, ToolTip = Tr.T("Com o pedaço do trafo na tag. Inversor sem trafo fica sem esse pedaço.") };
    private readonly TextBox _prefixoTrafo = Caixa(Tr.T("Prefixo do trafo (ex. T ou Trafo; vazio é só o número). O número é a posição do trafo na lista."));
    private readonly TextBox _prefixoInversor = Caixa(Tr.T("Prefixo do inversor (ex. I ou Inv; vazio é só o número). O número é a posição do inversor na lista."));
    private readonly TextBox _prefixoString = Caixa(Tr.T("Prefixo da string (ex. S). O número recomeça do 1 em cada inversor."));
    private readonly ComboBox _separador = new() { Width = 120, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = Tr.T("O que vai entre os pedaços.") };
    private readonly TextBlock _exemplo = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    private PainelDeNumeracao(Document documento)
    {
        _documento = documento;
        Margin = new Thickness(8);
        LastChildFill = true;

        DockPanel.SetDock(_recado, Dock.Bottom);
        Children.Add(_recado);

        var pilha = new StackPanel();
        pilha.Children.Add(SecaoDaTag());
        Children.Add(new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        Carregar();
    }

    /// <summary>O painel do desenho (a aba Numeração).</summary>
    internal static UIElement Criar(Document documento) => new PainelDeNumeracao(documento);

    private static TextBox Caixa(string dica) =>
        new() { Width = 64, Height = 24, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0), ToolTip = dica };

    private static TextBlock Rotulo(string texto) => new() { Text = texto, VerticalAlignment = VerticalAlignment.Center };

    // ------------------------------------------------------------ 15.1 tag

    private UIElement SecaoDaTag()
    {
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Ponto (T1.I1.S1)"), Tag = "." });
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Risquinho (T1-I1-S1)"), Tag = "-" });
        _separador.Items.Add(new ComboBoxItem { Content = Tr.T("Nada (T1I1S1)"), Tag = string.Empty });

        var linha = new WrapPanel { Orientation = Orientation.Horizontal };
        linha.Children.Add(_comTrafo);
        linha.Children.Add(_prefixoTrafo);
        linha.Children.Add(Rotulo(Tr.T("Inversor")));
        linha.Children.Add(_prefixoInversor);
        linha.Children.Add(Rotulo(Tr.T("String")));
        linha.Children.Add(_prefixoString);
        linha.Children.Add(Rotulo(Tr.T("Separador")));
        linha.Children.Add(new Border { Width = 4 });
        linha.Children.Add(_separador);

        var salvar = new Button { Content = Tr.T("Salvar composição"), Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 12, 0), ToolTip = Tr.T("Grava a composição no desenho. As tags já desenhadas só mudam ao gerar de novo.") };
        salvar.Click += (_, _) => SalvarEsquema();

        var embaixo = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(salvar, Dock.Left);
        embaixo.Children.Add(salvar);
        embaixo.Children.Add(_exemplo);

        foreach (var caixa in new[] { _prefixoTrafo, _prefixoInversor, _prefixoString }) caixa.TextChanged += (_, _) => Previa();
        _comTrafo.Checked += (_, _) => Previa();
        _comTrafo.Unchecked += (_, _) => Previa();
        _separador.SelectionChanged += (_, _) => Previa();

        var corpo = new StackPanel { Margin = new Thickness(6) };
        corpo.Children.Add(linha);
        corpo.Children.Add(embaixo);
        return new GroupBox { Header = Tr.T("Composição da tag"), Content = corpo, Margin = new Thickness(0, 0, 0, 8) };
    }

    private TagScheme EsquemaDaTela() => new(
        _comTrafo.IsChecked == true,
        _prefixoTrafo.Text,
        _prefixoInversor.Text,
        _prefixoString.Text,
        (_separador.SelectedItem as ComboBoxItem)?.Tag as string ?? ".");

    private void MostrarEsquema(TagScheme esquema)
    {
        _comTrafo.IsChecked = esquema.IncludeTransformer;
        _prefixoTrafo.Text = esquema.TransformerPrefix;
        _prefixoInversor.Text = esquema.InverterPrefix;
        _prefixoString.Text = esquema.StringPrefix;
        _separador.SelectedItem = _separador.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == esquema.Separator) ?? _separador.Items[0];
        Previa();
    }

    /// <summary>O exemplo ao vivo: três strings de dois inversores e um inversor sem trafo.</summary>
    private void Previa()
    {
        var esquema = EsquemaDaTela();

        if (esquema.Problem() is { } problema)
        {
            _exemplo.Foreground = Brushes.Firebrick;
            _exemplo.Text = problema;
            return;
        }

        _exemplo.Foreground = SystemColors.ControlTextBrush;
        _exemplo.Text = Exemplo(esquema);
    }

    internal static string Exemplo(TagScheme esquema) =>
        Tr.F("Exemplo: {0}, {1}, {2}; inversor sem trafo: {3}", esquema.Compose(1, 1, 1), esquema.Compose(1, 1, 2), esquema.Compose(1, 2, 1), esquema.Compose(null, 3, 1));

    private void SalvarEsquema()
    {
        var esquema = EsquemaDaTela();

        if (esquema.Problem() is { } problema)
        {
            Avisar(Tr.F("Não salvei: {0}.", problema), erro: true);
            return;
        }

        Fazer(() =>
        {
            NumeracaoStore.GravarEsquema(_documento.Database, esquema);
            return Tr.T("Composição salva no desenho.");
        });
    }

    // -------------------------------------------------------------- comum

    private void Carregar()
    {
        try
        {
            var (esquema, problema) = NumeracaoStore.Esquema(_documento.Database);
            MostrarEsquema(esquema);
            if (problema is not null) Avisar(problema, erro: true);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao carregar a aba Numeração.", erro);
            Avisar(Tr.F("Não consegui ler a numeração do desenho: {0}", erro.Message), erro: true);
        }
    }

    private void Avisar(string texto, bool erro = false)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    /// <summary>Escreve no desenho fora de comando (trava e vigia calado), sem derrubar o Civil 3D num clique.</summary>
    private void Fazer(Func<string?> operacao)
    {
        try
        {
            var frase = EscritaForaDeComando.Fazer(_documento, operacao);
            if (frase is not null) Avisar(frase);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na aba Numeração.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }
}
