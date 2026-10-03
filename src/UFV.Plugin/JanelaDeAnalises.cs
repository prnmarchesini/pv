using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A janela das análises (Renan, 02/10/2026: "menu análises, e aí o modal
/// com as abas, algo muito estruturado"). Uma aba por análise (Ponta baixa,
/// Ponta alta, Declividade, Pilares), cada uma em três seções numeradas:
/// 1. Textos (inserir, apagar), 2. Cores (a regra: abaixo de X, acima de Y,
/// só textos ou também as peças; analisar, tirar cores), 3. Quantidades
/// (quantificar, com os números na própria janela). A última aba junta as
/// quantificações feitas e exporta o Excel.
///
/// Cada botão faz uma operação no desenho (as mesmas dos comandos UFV_AN_*)
/// e o desenho atualiza atrás da janela.
/// </summary>
internal sealed class JanelaDeAnalises : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly Database _database;
    private readonly Autodesk.AutoCAD.EditorInput.Editor _editor;
    private readonly Action _atualizarTela;
    private readonly StackPanel _quantidades = new();

    internal JanelaDeAnalises(Database database, Autodesk.AutoCAD.EditorInput.Editor editor, Action atualizarTela)
    {
        _database = database;
        _editor = editor;
        _atualizarTela = atualizarTela;

        Title = "UFV — Análises";
        Width = 760;
        Height = 640;
        MinWidth = 560;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var abas = new TabControl { Margin = new Thickness(10) };

        foreach (var tipo in Analises)
            abas.Items.Add(new TabItem { Header = Titulo(tipo), Content = new PainelDeAnalise(this, tipo), ToolTip = $"Análise de {IndependentAnalysis.Name(tipo)}." });

        abas.Items.Add(new TabItem { Header = "Quantidades", Content = AbaQuantidades(), ToolTip = "As quantificações feitas, como vão para o Excel." });
        abas.SelectionChanged += (_, e) => { if (e.Source == abas) AtualizarQuantidades(); };

        var fechar = new Button { Content = "Fechar", Width = 90, Height = 26, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, ToolTip = "Fecha a janela; o que foi feito já está no desenho." };

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
        IndependentKind.LowEdge => "Ponta baixa",
        IndependentKind.HighEdge => "Ponta alta",
        IndependentKind.Slope => "Declividade",
        IndependentKind.PillarBuried => "Pilar enterrado",
        IndependentKind.PillarLength => "Pilar total",
        _ => "Pilar livre",
    };

    internal Database Database => _database;

    /// <summary>Roda a operação, atualiza o desenho e devolve a frase (ou o erro) para a seção mostrar.</summary>
    internal (bool Ok, string Frase) Fazer(string oQue, Func<string> operacao)
    {
        try
        {
            // Janela solta: o clique chega fora de um comando, e escrever no
            // desenho pede a trava do documento. Desenho fechado, nada a fazer.
            var documento = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.GetDocument(_database)
                ?? throw new InvalidOperationException("o desenho desta janela foi fechado");

            string frase;
            using (documento.LockDocument())
            {
                frase = operacao();
                _atualizarTela();
            }

            _editor.WriteMessage($"\nANÁLISES {frase}\n");
            return (true, frase);
        }
        catch (Exception erro)
        {
            // Clique de WPF: exceção solta aqui fecharia o Civil 3D.
            RegistroDeDiagnostico.Registrar($"Falha na janela de análises ({oQue}).", erro);
            return (false, $"Não consegui {oQue}: {erro.Message}");
        }
    }

    // --------------------------------------------------------- quantidades

    private UIElement AbaQuantidades()
    {
        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(new TextBlock
        {
            Text = "A última quantificação de cada análise (o botão Quantificar de cada aba). É o que vai para o Excel, junto com o quantitativo de mesas, módulos e pilares.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });
        pilha.Children.Add(_quantidades);

        pilha.Children.Add(new TextBlock
        {
            Text = "Para a planilha, use o botão Excel da ribbon (painel Saída).",
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
                    ? "ainda não quantificada"
                    : IndependentAnalysis.Describe(tipo, q.Rule, q.Points, q.Unit)
                      + (q.Modules is { } m ? $"\nmódulos — {IndependentAnalysis.Describe(tipo, q.Rule, m, q.Unit)}" : "")
                      + $"\n(em {q.When.ToString("dd/MM/yyyy HH:mm", Brasil)})",
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
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly JanelaDeAnalises _janela;
    private readonly IndependentKind _tipo;

    private readonly CheckBox _usarAbaixo = new() { Content = "Menor que", VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = "Desmarcado: nada é pintado por ser menor." };
    private readonly TextBox _abaixo = new() { Width = 70, Height = 24, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ComboBox _corAbaixo;
    private readonly CheckBox _usarAcima = new() { Content = "Maior que", VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = "Desmarcado: nada é pintado por ser maior." };
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

        _corAbaixo = PaletaDeCores.Caixa(regra.BelowColor, "Cor de quem fica abaixo do limite.");
        _corAcima = PaletaDeCores.Caixa(regra.AboveColor, "Cor de quem fica acima do limite.");
        _abaixo.ToolTip = $"Limite de baixo{(emMetro ? " (m)" : "")}. Igual ao limite conta como dentro.";
        _acima.ToolTip = $"Limite de cima{(emMetro ? " (m)" : "")}. Igual ao limite conta como dentro.";
        _usarAbaixo.IsChecked = regra.Below is not null;
        _abaixo.Text = regra.Below is { } b ? b.ToString("0.###", Brasil) : "";
        _usarAcima.IsChecked = regra.Above is not null;
        _acima.Text = regra.Above is { } a ? a.ToString("0.###", Brasil) : "";
        _pecas.IsChecked = regra.PaintPieces;
        _pecas.Content = tipo switch
        {
            IndependentKind.Slope => "Pintar também o contorno da mesa",
            IndependentKind.PillarAbove or IndependentKind.PillarBuried or IndependentKind.PillarLength => "Pintar também os pilares",
            _ => "Pintar também os módulos (não só os textos)",
        };
        _pecas.ToolTip = "Desmarcado, só os textos desta análise mudam de cor.";

        _unidade.Items.Add(new ComboBoxItem { Content = "Porcentagem (%)", Tag = SlopeUnit.Percent });
        _unidade.Items.Add(new ComboBoxItem { Content = "Graus (°)", Tag = SlopeUnit.Degrees });
        _unidade.SelectedIndex = unidade == SlopeUnit.Degrees ? 1 : 0;
        _unidade.ToolTip = "A unidade dos textos e dos limites da declividade.";

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
            ("Inserir textos", $"Escreve {nome} em todas as mesas; os textos desta análise que já existiam saem antes.", Inserir),
            ("Apagar textos", $"Apaga os textos de {nome}. As outras análises ficam.", Apagar)));
        textos.Children.Add(_recadoTextos);
        pilha.Children.Add(Secao("1. Textos", textos));

        // 2. Cores
        var cores = new StackPanel();
        if (tipo == IndependentKind.Slope)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            linha.Children.Add(new TextBlock { Text = "Unidade", Width = 94, VerticalAlignment = VerticalAlignment.Center });
            linha.Children.Add(_unidade);
            cores.Children.Add(linha);
        }

        cores.Children.Add(Faixa(_usarAbaixo, _abaixo, _corAbaixo, emMetro, tipo));
        cores.Children.Add(Faixa(_usarAcima, _acima, _corAcima, emMetro, tipo));
        cores.Children.Add(_pecas);
        cores.Children.Add(Botoes(
            ("Analisar", "Grava a regra no desenho e pinta quem sai da faixa (refaz a pintura anterior desta análise).", Analisar),
            ("Tirar cores", "Volta à cor de antes (da camada, do tipo de mesa ou magenta) o que esta análise pintou; as cores das outras análises ficam.", TirarCores)));
        cores.Children.Add(_recadoCores);
        pilha.Children.Add(Secao("2. Cores", cores));

        // 3. Quantidades
        var quantidades = new StackPanel();
        quantidades.Children.Add(Botoes(("Quantificar", "Conta quantos ficaram abaixo, dentro e acima da faixa (com a regra acima); fica gravado para o Excel.", Quantificar)));
        quantidades.Children.Add(_recadoQuantidade);
        pilha.Children.Add(Secao("3. Quantidades", quantidades));

        Content = pilha;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private static string Explicacao(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => "A altura livre da ponta baixa do módulo em cada pilar (PB), escrita fora da borda baixa da mesa.",
        IndependentKind.HighEdge => "A altura livre da ponta alta do módulo em cada pilar (PA), escrita fora da borda alta da mesa.",
        IndependentKind.Slope => "A declividade de cada mesa ao longo da fileira, com a seta descendo.",
        IndependentKind.PillarBuried => "A parte ENTERRADA de cada pilar (E): do terreno até a ponta de baixo. Escrita junto ao pilar, um pouco para a borda baixa.",
        IndependentKind.PillarLength => "O comprimento TOTAL de cada pilar (PT): parte livre mais parte enterrada. Escrito junto ao pilar, um pouco para a borda alta.",
        _ => "A parte LIVRE de cada pilar (P): do terreno até o topo, fora da terra. Escrita no topo dele.",
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
        linha.Children.Add(new TextBlock { Text = emMetro ? "m, pintar de" : "pintar de", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
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
            if (!NumberInput.TryParseMeasure(_abaixo.Text, out var v)) { JanelaDeAnalises.Dizer(_recadoCores, false, "Não consigo ler o limite de baixo."); return null; }
            abaixo = v;
        }

        if (_usarAcima.IsChecked == true)
        {
            if (!NumberInput.TryParseMeasure(_acima.Text, out var v)) { JanelaDeAnalises.Dizer(_recadoCores, false, "Não consigo ler o limite de cima."); return null; }
            acima = v;
        }

        var regra = new ThresholdRule(abaixo, PaletaDeCores.Cor(_corAbaixo), acima, PaletaDeCores.Cor(_corAcima), _pecas.IsChecked == true);

        if (regra.WhyInvalid is { } motivo)
        {
            JanelaDeAnalises.Dizer(_recadoCores, false, char.ToUpper(motivo[0], Brasil) + motivo[1..] + ".");
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
        var (ok, frase) = _janela.Fazer("inserir os textos", () =>
        {
            AplicarUnidade();
            var (criados, mesas, ignoradas) = AnalisesIndependentes.Inserir(_janela.Database, _tipo, Unidade());
            return mesas == 0
                ? "O desenho não tem mesa gerada pelo plugin; nada inserido."
                : $"{criados} texto(s) em {mesas} mesa(s)." + (ignoradas > 0 ? $" {ignoradas} mesa(s) com contorno deformado ficaram de fora." : "");
        });

        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Apagar()
    {
        var (ok, frase) = _janela.Fazer("apagar os textos", () => $"{AnalisesIndependentes.Apagar(_janela.Database, _tipo)} texto(s) apagado(s).");
        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Analisar()
    {
        if (LerRegra() is not { } regra) return;

        var (ok, frase) = _janela.Fazer("analisar", () =>
        {
            AplicarUnidade();
            AnalisesIndependentesCommands.GravarRegra(_janela.Database, _tipo, regra);
            var (textos, pecas) = AnalisesIndependentes.Analisar(_janela.Database, _tipo, regra, Unidade());
            return $"{textos} texto(s) pintado(s)" + (regra.PaintPieces ? $", {pecas} peça(s) pintada(s)." : ".")
                + (textos == 0 && !regra.PaintPieces ? " Nenhum texto desta análise no desenho: insira os textos primeiro." : "");
        });

        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void TirarCores()
    {
        var (ok, frase) = _janela.Fazer("tirar as cores", () => $"{AnalisesIndependentes.TirarCores(_janela.Database, _tipo)} entidade(s) de volta à cor de antes.");
        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void Quantificar()
    {
        if (LerRegra() is not { } regra) return;

        var (ok, frase) = _janela.Fazer("quantificar", () =>
        {
            var unidade = Unidade();
            var (pontos, modulos) = AnalisesIndependentes.Quantificar(_janela.Database, _tipo, regra, unidade);
            QuantificacaoGravada.Gravar(_janela.Database, _tipo, regra, unidade, pontos, modulos);

            var oQue = _tipo == IndependentKind.Slope ? "mesas" : "pilares";
            return $"Em {oQue}: {IndependentAnalysis.Describe(_tipo, regra, pontos, unidade)}"
                + (modulos is not null ? $"\nEm módulos: {IndependentAnalysis.Describe(_tipo, regra, modulos, unidade)}" : "");
        });

        JanelaDeAnalises.Dizer(_recadoQuantidade, ok, frase);
        _janela.AtualizarQuantidades();
    }
}
