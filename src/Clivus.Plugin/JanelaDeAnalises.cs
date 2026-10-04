using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A janela das análises (Renan, 02/10/2026: "menu análises, e aí o modal
/// com as abas, algo muito estruturado"). Uma aba por análise (Ponta baixa,
/// Ponta alta, Declividade, Pilares), cada uma em três seções numeradas:
/// 1. Textos (inserir, apagar), 2. Cores (a regra: abaixo de X, acima de Y,
/// só textos ou também as peças; analisar, tirar cores), 3. Quantidades
/// (quantificar, com os números na própria janela). A última aba junta as
/// quantificações feitas e exporta o Excel.
///
/// Cada botão faz uma operação no desenho (as mesmas dos comandos CLIVUS_AN_*)
/// e o desenho atualiza atrás da janela.
/// </summary>
internal sealed class JanelaDeAnalises : Window
{
    private readonly Database _database;
    private readonly Autodesk.AutoCAD.EditorInput.Editor _editor;
    private readonly Action _atualizarTela;
    private readonly StackPanel _quantidades = new();

    internal JanelaDeAnalises(Database database, Autodesk.AutoCAD.EditorInput.Editor editor, Action atualizarTela)
    {
        _database = database;
        _editor = editor;
        _atualizarTela = atualizarTela;

        Title = Tr.T("Clivus Solar — Análises");
        Width = 760;
        Height = 640;
        MinWidth = 560;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var abas = new TabControl { Margin = new Thickness(10) };

        foreach (var tipo in Analises)
            abas.Items.Add(new TabItem { Header = Titulo(tipo), Content = new PainelDeAnalise(this, tipo), ToolTip = Tr.F("Análise de {0}.", IndependentAnalysis.Name(tipo)) });

        abas.Items.Add(new TabItem { Header = Tr.T("Quantidades"), Content = AbaQuantidades(), ToolTip = Tr.T("As quantificações feitas, como vão para o Excel.") });
        abas.SelectionChanged += (_, e) => { if (e.Source == abas) AtualizarQuantidades(); };

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 90, Height = 26, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, ToolTip = Tr.T("Fecha a janela; o que foi feito já está no desenho.") };

        // A janela é solta (03/10/2026: "quero poder mexer na tela análises
        // e no CAD ao mesmo tempo"): IsCancel só fecha diálogo, aqui fecha à mão.
        fechar.Click += (_, _) => Close();

        var raiz = new DockPanel();
        DockPanel.SetDock(fechar, Dock.Bottom);
        raiz.Children.Add(fechar);
        raiz.Children.Add(abas);
        Content = raiz;
    }

    /// <summary>As análises, na ordem das abas.</summary>
    private static readonly IndependentKind[] Analises =
    [
        IndependentKind.LowEdge, IndependentKind.HighEdge, IndependentKind.Slope,
        IndependentKind.PillarAbove, IndependentKind.PillarBuried, IndependentKind.PillarLength,
    ];

    internal static string Titulo(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => Tr.T("Ponta baixa"),
        IndependentKind.HighEdge => Tr.T("Ponta alta"),
        IndependentKind.Slope => Tr.T("Declividade"),
        IndependentKind.PillarBuried => Tr.T("Pilar enterrado"),
        IndependentKind.PillarLength => Tr.T("Pilar total"),
        _ => Tr.T("Pilar livre"),
    };

    internal Database Database => _database;

    /// <summary>
    /// Roda a operação, atualiza o desenho e devolve a frase (ou o erro) para a seção mostrar.
    /// <paramref name="oQue"/> vem marcado com Tr.N: vai em português para o registro e traduzido para a tela.
    /// </summary>
    internal (bool Ok, string Frase) Fazer(string oQue, Func<string> operacao)
    {
        try
        {
            // Janela solta: o clique chega fora de um comando, e escrever no
            // desenho pede a trava do documento. Desenho fechado, nada a fazer.
            var documento = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.GetDocument(_database)
                ?? throw new InvalidOperationException(Tr.T("o desenho desta janela foi fechado"));

            // Trava e cala o vigia: a pintura das análises não é edição do
            // usuário (sem isso, as mesas pintadas viravam pendentes).
            var frase = EscritaForaDeComando.Fazer(documento, () =>
            {
                var f = operacao();
                _atualizarTela();
                return f;
            });

            _editor.WriteMessage(Tr.F("\nANÁLISES {0}\n", frase));
            return (true, frase);
        }
        catch (Exception erro)
        {
            // Clique de WPF: exceção solta aqui fecharia o Civil 3D.
            RegistroDeDiagnostico.Registrar($"Falha na janela de análises ({oQue}).", erro);
            return (false, Tr.F("Não consegui {0}: {1}", Tr.T(oQue), erro.Message));
        }
    }

    // --------------------------------------------------------- quantidades

    private UIElement AbaQuantidades()
    {
        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("A última quantificação de cada análise (o botão Quantificar de cada aba). É o que vai para o Excel, junto com o quantitativo de mesas, módulos e pilares."),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });
        pilha.Children.Add(_quantidades);

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Para a planilha, use o botão Excel da ribbon (painel Saída)."),
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 14, 0, 0),
        });

        AtualizarQuantidades();
        return new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    internal void AtualizarQuantidades()
    {
        _quantidades.Children.Clear();

        foreach (var tipo in Analises)
        {
            var q = QuantificacaoGravada.Ler(_database, tipo);

            _quantidades.Children.Add(new TextBlock { Text = Titulo(tipo), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
            _quantidades.Children.Add(new TextBlock
            {
                Text = q is null
                    ? Tr.T("ainda não quantificada")
                    : IndependentAnalysis.Describe(tipo, q.Rule, q.Points, q.Unit)
                      + (q.Modules is { } m ? Tr.F("\nmódulos — {0}", IndependentAnalysis.Describe(tipo, q.Rule, m, q.Unit)) : "")
                      + Tr.F("\n(em {0:dd/MM/yyyy HH:mm})", q.When),
                Foreground = q is null ? Brushes.Gray : Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    internal static void Dizer(TextBlock alvo, bool ok, string frase)
    {
        alvo.Foreground = ok ? Brushes.ForestGreen : Brushes.Firebrick;
        alvo.Text = frase;
    }
}

/// <summary>Uma aba da janela de análises: as três seções de uma análise.</summary>
internal sealed class PainelDeAnalise : ScrollViewer
{
    private readonly JanelaDeAnalises _janela;
    private readonly IndependentKind _tipo;

    private readonly CheckBox _usarAbaixo = new() { Content = Tr.T("Menor que"), VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = Tr.T("Desmarcado: nada é pintado por ser menor.") };
    private readonly TextBox _abaixo = new() { Width = 70, Height = 24, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _corAbaixo;
    private readonly CheckBox _usarAcima = new() { Content = Tr.T("Maior que"), VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = Tr.T("Desmarcado: nada é pintado por ser maior.") };
    private readonly TextBox _acima = new() { Width = 70, Height = 24, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _corAcima;
    private readonly CheckBox _pecas = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly ComboBox _unidade = new() { Width = 150, Height = 24 };

    private readonly TextBlock _recadoTextos = Recado();
    private readonly TextBlock _recadoCores = Recado();
    private readonly TextBlock _recadoQuantidade = Recado();

    internal PainelDeAnalise(JanelaDeAnalises janela, IndependentKind tipo)
    {
        _janela = janela;
        _tipo = tipo;

        var regra = AnalisesIndependentesCommands.Regra(janela.Database, tipo);
        var unidade = AnalisesIndependentesCommands.Unidade(janela.Database);
        var nome = IndependentAnalysis.Name(tipo);
        var emMetro = tipo != IndependentKind.Slope;

        _corAbaixo = PaletaDeCores.Caixa(regra.BelowColor, Tr.T("Cor de quem fica abaixo do limite."));
        _corAcima = PaletaDeCores.Caixa(regra.AboveColor, Tr.T("Cor de quem fica acima do limite."));
        _abaixo.ToolTip = Tr.F("Limite de baixo{0}. Igual ao limite conta como dentro.", emMetro ? " (m)" : "");
        _acima.ToolTip = Tr.F("Limite de cima{0}. Igual ao limite conta como dentro.", emMetro ? " (m)" : "");
        _usarAbaixo.IsChecked = regra.Below is not null;
        _abaixo.Text = regra.Below is { } b ? b.ToString("0.###", Tr.Culture) : "";
        _usarAcima.IsChecked = regra.Above is not null;
        _acima.Text = regra.Above is { } a ? a.ToString("0.###", Tr.Culture) : "";
        _pecas.IsChecked = regra.PaintPieces;
        _pecas.Content = tipo switch
        {
            IndependentKind.Slope => Tr.T("Pintar também o contorno da mesa"),
            IndependentKind.PillarAbove or IndependentKind.PillarBuried or IndependentKind.PillarLength => Tr.T("Pintar também os pilares"),
            _ => Tr.T("Pintar também os módulos (não só os textos)"),
        };
        _pecas.ToolTip = Tr.T("Desmarcado, só os textos desta análise mudam de cor.");

        _unidade.Items.Add(new ComboBoxItem { Content = Tr.T("Porcentagem (%)"), Tag = SlopeUnit.Percent });
        _unidade.Items.Add(new ComboBoxItem { Content = Tr.T("Graus (°)"), Tag = SlopeUnit.Degrees });
        _unidade.SelectedIndex = unidade == SlopeUnit.Degrees ? 1 : 0;
        _unidade.ToolTip = Tr.T("A unidade dos textos e dos limites da declividade.");

        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(new TextBlock
        {
            Text = Explicacao(tipo),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 8),
        });

        // 1. Textos
        var textos = new StackPanel();
        textos.Children.Add(Botoes(
            (Tr.T("Inserir textos"), Tr.F("Escreve {0} em todas as mesas; os textos desta análise que já existiam saem antes.", nome), Inserir),
            (Tr.T("Apagar textos"), Tr.F("Apaga os textos de {0}. As outras análises ficam.", nome), Apagar)));
        textos.Children.Add(_recadoTextos);
        pilha.Children.Add(Secao(Tr.T("1. Textos"), textos));

        // 2. Cores
        var cores = new StackPanel();
        if (tipo == IndependentKind.Slope)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            linha.Children.Add(new TextBlock { Text = Tr.T("Unidade"), Width = 94, VerticalAlignment = VerticalAlignment.Center });
            linha.Children.Add(_unidade);
            cores.Children.Add(linha);
        }

        cores.Children.Add(Faixa(_usarAbaixo, _abaixo, _corAbaixo, emMetro, tipo));
        cores.Children.Add(Faixa(_usarAcima, _acima, _corAcima, emMetro, tipo));
        cores.Children.Add(_pecas);
        cores.Children.Add(Botoes(
            (Tr.T("Analisar"), Tr.T("Grava a regra no desenho e pinta quem sai da faixa (refaz a pintura anterior desta análise)."), Analisar),
            (Tr.T("Tirar cores"), Tr.T("Volta à cor de antes (da camada, do tipo de mesa ou magenta) o que esta análise pintou; as cores das outras análises ficam."), TirarCores)));
        cores.Children.Add(_recadoCores);
        pilha.Children.Add(Secao(Tr.T("2. Cores"), cores));

        // 3. Quantidades
        var quantidades = new StackPanel();
        quantidades.Children.Add(Botoes((Tr.T("Quantificar"), Tr.T("Conta quantos ficaram abaixo, dentro e acima da faixa (com a regra acima); fica gravado para o Excel."), Quantificar)));
        quantidades.Children.Add(_recadoQuantidade);
        pilha.Children.Add(Secao(Tr.T("3. Quantidades"), quantidades));

        Content = pilha;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private static string Explicacao(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => Tr.T("A altura livre da ponta baixa do módulo em cada pilar (PB), escrita fora da borda baixa da mesa."),
        IndependentKind.HighEdge => Tr.T("A altura livre da ponta alta do módulo em cada pilar (PA), escrita fora da borda alta da mesa."),
        IndependentKind.Slope => Tr.T("A declividade de cada mesa ao longo da fileira, com a seta descendo."),
        IndependentKind.PillarBuried => Tr.T("A parte ENTERRADA de cada pilar (E): do terreno até a ponta de baixo. Escrita junto ao pilar, um pouco para a borda baixa."),
        IndependentKind.PillarLength => Tr.T("O comprimento TOTAL de cada pilar (PT): parte livre mais parte enterrada. Escrito junto ao pilar, um pouco para a borda alta."),
        _ => Tr.T("A parte LIVRE de cada pilar (P): do terreno até o topo, fora da terra. Escrita no topo dele."),
    };

    private static TextBlock Recado() => new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    private static GroupBox Secao(string titulo, UIElement conteudo) => new()
    {
        Header = new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold },
        Content = conteudo,
        Padding = new Thickness(8),
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static StackPanel Faixa(CheckBox usar, TextBox valor, ComboBox cor, bool emMetro, IndependentKind tipo)
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        linha.Children.Add(usar);
        linha.Children.Add(valor);
        linha.Children.Add(new TextBlock { Text = emMetro ? Tr.T("m, pintar de") : Tr.T("pintar de"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        linha.Children.Add(cor);
        return linha;
    }

    private static StackPanel Botoes(params (string Texto, string Dica, Action Acao)[] botoes)
    {
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

        foreach (var (texto, dica, acao) in botoes)
        {
            var botao = new Button { Content = texto, Height = 28, MinWidth = 120, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 8, 0), ToolTip = dica };
            botao.Click += (_, _) => acao();
            linha.Children.Add(botao);
        }

        return linha;
    }

    private SlopeUnit Unidade() =>
        _tipo == IndependentKind.Slope && (_unidade.SelectedItem as ComboBoxItem)?.Tag is SlopeUnit u
            ? u
            : AnalisesIndependentesCommands.Unidade(_janela.Database);

    /// <summary>A regra dos campos, ou null com o motivo no recado das cores.</summary>
    private ThresholdRule? LerRegra()
    {
        double? abaixo = null, acima = null;

        if (_usarAbaixo.IsChecked == true)
        {
            if (!NumberInput.TryParseMeasure(_abaixo.Text, out var v)) { JanelaDeAnalises.Dizer(_recadoCores, false, Tr.T("Não consigo ler o limite de baixo.")); return null; }
            abaixo = v;
        }

        if (_usarAcima.IsChecked == true)
        {
            if (!NumberInput.TryParseMeasure(_acima.Text, out var v)) { JanelaDeAnalises.Dizer(_recadoCores, false, Tr.T("Não consigo ler o limite de cima.")); return null; }
            acima = v;
        }

        var regra = new ThresholdRule(abaixo, PaletaDeCores.Cor(_corAbaixo), acima, PaletaDeCores.Cor(_corAcima), _pecas.IsChecked == true);

        if (regra.WhyInvalid is { } motivo)
        {
            JanelaDeAnalises.Dizer(_recadoCores, false, char.ToUpper(motivo[0], Tr.Culture) + motivo[1..] + ".");
            return null;
        }

        return regra;
    }

    /// <summary>Se a unidade da declividade mudou, grava e reinsere os textos nela.</summary>
    private void AplicarUnidade()
    {
        if (_tipo != IndependentKind.Slope) return;

        var nova = Unidade();
        var atual = AnalisesIndependentesCommands.Unidade(_janela.Database);
        if (nova == atual) return;

        SetaDeDeclividade.Gravar(_janela.Database, SetaDeDeclividade.Ler(_janela.Database).Ligada, nova);
        if (AnalisesIndependentes.Apagar(_janela.Database, _tipo) > 0) AnalisesIndependentes.Inserir(_janela.Database, _tipo, nova);
    }

    private void Inserir()
    {
        var (ok, frase) = _janela.Fazer(Tr.N("inserir os textos"), () =>
        {
            AplicarUnidade();
            var (criados, mesas, ignoradas) = AnalisesIndependentes.Inserir(_janela.Database, _tipo, Unidade());
            return mesas == 0
                ? Tr.T("O desenho não tem mesa gerada pelo plugin; nada inserido.")
                : Tr.F("{0} texto(s) em {1} mesa(s).", criados, mesas) + (ignoradas > 0 ? Tr.F(" {0} mesa(s) com contorno deformado ficaram de fora.", ignoradas) : "");
        });

        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Apagar()
    {
        var (ok, frase) = _janela.Fazer(Tr.N("apagar os textos"), () => Tr.F("{0} texto(s) apagado(s).", AnalisesIndependentes.Apagar(_janela.Database, _tipo)));
        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Analisar()
    {
        if (LerRegra() is not { } regra) return;

        var (ok, frase) = _janela.Fazer(Tr.N("analisar"), () =>
        {
            AplicarUnidade();
            AnalisesIndependentesCommands.GravarRegra(_janela.Database, _tipo, regra);
            var (textos, pecas) = AnalisesIndependentes.Analisar(_janela.Database, _tipo, regra, Unidade());
            return (regra.PaintPieces ? Tr.F("{0} texto(s) pintado(s), {1} peça(s) pintada(s).", textos, pecas) : Tr.F("{0} texto(s) pintado(s).", textos))
                + (textos == 0 && !regra.PaintPieces ? Tr.T(" Nenhum texto desta análise no desenho: insira os textos primeiro.") : "");
        });

        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void TirarCores()
    {
        var (ok, frase) = _janela.Fazer(Tr.N("tirar as cores"), () => Tr.F("{0} entidade(s) de volta à cor de antes.", AnalisesIndependentes.TirarCores(_janela.Database, _tipo)));
        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void Quantificar()
    {
        if (LerRegra() is not { } regra) return;

        var (ok, frase) = _janela.Fazer(Tr.N("quantificar"), () =>
        {
            var unidade = Unidade();
            var (pontos, modulos) = AnalisesIndependentes.Quantificar(_janela.Database, _tipo, regra, unidade);
            QuantificacaoGravada.Gravar(_janela.Database, _tipo, regra, unidade, pontos, modulos);

            var descricao = IndependentAnalysis.Describe(_tipo, regra, pontos, unidade);
            return (_tipo == IndependentKind.Slope ? Tr.F("Em mesas: {0}", descricao) : Tr.F("Em pilares: {0}", descricao))
                + (modulos is not null ? Tr.F("\nEm módulos: {0}", IndependentAnalysis.Describe(_tipo, regra, modulos, unidade)) : "");
        });

        JanelaDeAnalises.Dizer(_recadoQuantidade, ok, frase);
        _janela.AtualizarQuantidades();
    }
}
