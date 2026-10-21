using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Análise › Flechas (26.1): onde, de quanto em quanto, se escreve o valor e
/// as faixas de cor. Solta, como a das sombras: dá para mexer no desenho e
/// gerar de novo sem fechar. Compacta (regra de 02/10/2026): rótulo ao lado,
/// nada de espaço vazio.
/// </summary>
internal sealed class JanelaDeFlechas : Window
{
    private static readonly Dictionary<Document, JanelaDeFlechas> Abertas = [];

    private readonly Document _documento;
    private readonly ComboBox _onde = new() { Width = 200, ToolTip = Tr.T("Dentro das áreas de implantação, num retângulo escolhido no desenho ou no terreno todo.") };
    private readonly TextBox _espacamento = new() { Width = 50, ToolTip = Tr.T("De quantos em quantos metros vai uma flecha (a grade).") };
    private readonly CheckBox _valor = new() { Content = Tr.T("Escrever a declividade em cada flecha"), VerticalAlignment = VerticalAlignment.Center, ToolTip = Tr.T("O valor em % ao lado de cada flecha. Em terreno grande fica carregado; o padrão é sem.") };
    private readonly TextBox[] _limites = Enumerable.Range(0, 4).Select(_ => new TextBox { Width = 40, ToolTip = Tr.T("Limite da faixa, em %.") }).ToArray();
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private JanelaDeFlechas(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Flechas da água");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _onde.Items.Add(Tr.T("Áreas de implantação"));
        _onde.Items.Add(Tr.T("Janela (dois cantos)"));
        _onde.Items.Add(Tr.T("Terreno todo"));
        _onde.SelectedIndex = 0;

        var atual = DrenagemStore.Ler(documento.Database).Arrows;
        _espacamento.Text = atual.Spacing.ToString("0.##", CultureInfo.CurrentCulture);
        _valor.IsChecked = atual.ShowValue;
        for (var i = 0; i < 4; i++) _limites[i].Text = atual.Limits[i].ToString("0.##", CultureInfo.CurrentCulture);

        var pilha = new StackPanel { Margin = new Thickness(12) };

        StackPanel Linha(params UIElement[] itens)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            foreach (var i in itens) linha.Children.Add(i);
            return linha;
        }

        TextBlock R(string texto, double largura = 0) => new()
        {
            Text = texto,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Width = largura > 0 ? largura : double.NaN,
        };

        Border Amostra(RgbColor cor) => new()
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(6, 0, 4, 0),
            Background = new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B)),
            VerticalAlignment = VerticalAlignment.Center,
        };

        pilha.Children.Add(Linha(R(Tr.T("Onde"), 90), _onde));
        pilha.Children.Add(Linha(R(Tr.T("Espaçamento"), 90), _espacamento, R(Tr.T("m")), _valor));

        var cores = DrainageArrows.BandColors;
        var faixas = Linha(R(Tr.T("Faixas (%)"), 90), Amostra(cores[0]), R("0 –"));
        for (var i = 0; i < 4; i++)
        {
            faixas.Children.Add(_limites[i]);
            faixas.Children.Add(Amostra(cores[i + 1]));
        }
        faixas.Children.Add(R(Tr.T("acima")));
        pilha.Children.Add(faixas);

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Cada flecha aponta para onde a água corre, com a cor da faixa de declividade do terreno naquele ponto; ponto plano vira um pontinho cinza. A legenda fica à direita, em cima. Gerar de novo apaga as anteriores."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 6, 0, 0),
        });

        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var gerar = new Button { Content = Tr.T("Gerar flechas"), Width = 110, Height = 26, IsDefault = true, ToolTip = Tr.T("Apaga as flechas anteriores e desenha de novo com o que está nesta janela.") };
        gerar.Click += (_, _) => Gerar();
        var apagar = new Button { Content = Tr.T("Apagar flechas"), Width = 110, Height = 26, Margin = new Thickness(8, 0, 0, 0), ToolTip = Tr.T("Apaga as flechas e a legenda.") };
        apagar.Click += (_, _) => Fazer(() => Tr.F("FLECHAS {0} entidade(s) apagada(s).", FlechasDeDrenagem.Apagar(_documento.Database)));
        var fechar = new Button { Content = Tr.T("Fechar"), Width = 80, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true, ToolTip = Tr.T("Fecha a janela; as flechas desenhadas ficam.") };
        fechar.Click += (_, _) => Close();
        botoes.Children.Add(gerar);
        botoes.Children.Add(apagar);
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        Content = pilha;
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeFlechas(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            if (e.Document == documento) janela.Close();
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) =>
        {
            Abertas.Remove(documento);
            AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
        };

        AcadApp.ShowModelessWindow(janela);
    }

    private void Gerar()
    {
        var onde = _onde.SelectedIndex switch { 1 => OndeFlechas.Janela, 2 => OndeFlechas.Tudo, _ => OndeFlechas.Areas };

        Fazer(() =>
        {
            var configuracao = Ler();

            // A janela (dois cantos) precisa de cliques no desenho: vai como
            // comando, com a configuração já gravada.
            if (onde == OndeFlechas.Janela)
            {
                // Gravar no desenho fora de comando pede o documento travado.
                EscritaForaDeComando.Fazer(_documento, () => DrenagemStore.Mudar(_documento.Database, d => d with { Arrows = configuracao }));
                _documento.SendStringToExecute($"\x03\x03_{PluginInfo.ComandoDrenagemFlechasGerar} _Janela ", true, false, false);
                return Tr.T("Escolha os dois cantos no desenho.");
            }

            return FlechasCommands.GerarPelaJanela(_documento, onde, configuracao);
        }, porComando: onde == OndeFlechas.Janela);
    }

    /// <summary>A configuração dos campos; o que não for número vira o motivo.</summary>
    private ArrowSettings Ler()
    {
        double Numero(TextBox caixa, string nome) =>
            NumberInput.TryParseMeasure(caixa.Text, out var v) ? v : throw new ArgumentException(Tr.F("{0}: \"{1}\" não é número.", nome, caixa.Text));

        var espacamento = Numero(_espacamento, Tr.T("Espaçamento"));
        if (!(espacamento > 0)) throw new ArgumentException(Tr.T("O espaçamento das flechas precisa ser maior que zero."));

        var limites = _limites.Select(l => Numero(l, Tr.T("Faixas"))).ToList();
        _ = DrainageArrows.Bands(limites);

        return new ArrowSettings { Spacing = espacamento, ShowValue = _valor.IsChecked == true, Limits = limites };
    }

    /// <summary>Roda com o documento travado (janela solta) e mostra o relato.</summary>
    private void Fazer(Func<string> operacao, bool porComando = false)
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;

            var frase = porComando
                ? operacao()
                : EscritaForaDeComando.Fazer(_documento, () =>
                {
                    var f = operacao();
                    _documento.Editor.Regen();
                    return f;
                });

            _documento.Editor.WriteMessage($"\n{frase}\n");
            _recado.Foreground = Brushes.ForestGreen;
            _recado.Text = frase;
        }
        catch (Exception erro)
        {
            // Clique de WPF: exceção solta aqui derrubaria o Civil 3D.
            RegistroDeDiagnostico.Registrar("Falha na janela das flechas.", erro);
            _recado.Foreground = Brushes.Firebrick;
            _recado.Text = erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message);
        }
        finally
        {
            Cursor = null;
        }
    }
}
