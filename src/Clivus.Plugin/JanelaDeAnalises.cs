using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
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
///
/// 31/10/2026, três pedidos do Renan na tela do Aurora:
/// <list type="bullet">
/// <item><description>em cima, o que analisar, para todas as abas ("Em analise
/// eu tbm quero poder escolher o que analisar, uma area ou todas, isso para
/// TODAS analises"): o seletor das sombras, que abre no desenho inteiro, como
/// era;</description></item>
/// <item><description>nos textos, o fundo e a borda ("texto ruim de ver, falta
/// opção do texto de colocar o fundo e borda, isso para TODAS
/// analises");</description></item>
/// <item><description>nas análises de pilar com biposte, uma coluna de limites
/// por linha de pilar ("cada um tem uma altura [...] uma coluna para cada
/// pilar o da frente e o de traz, a cor pode ser o mesmo para
/// ambos").</description></item>
/// </list>
/// </summary>
internal sealed class JanelaDeAnalises : Window
{
    private readonly Database _database;
    private readonly Autodesk.AutoCAD.EditorInput.Editor _editor;
    private readonly Action _atualizarTela;
    private readonly StackPanel _quantidades = new();
    private readonly SeletorDeEscopo _seletor;
    private readonly TextBlock _recadoDoEscopo = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0), Visibility = System.Windows.Visibility.Collapsed };

    internal JanelaDeAnalises(Document documento, Action atualizarTela)
    {
        _database = documento.Database;
        _editor = documento.Editor;
        _atualizarTela = atualizarTela;

        Title = Tr.T("Clivus Solar — Análises");
        Width = 760;
        Height = 660;
        MinWidth = 560;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        // O que analisar: vale para todas as abas. Abre no desenho inteiro,
        // como era antes da escolha.
        _seletor = new SeletorDeEscopo(documento, Tr.T("Analisar"), RecadoDoEscopo, Tentar, desenhoInteiro: true);
        var escopo = new StackPanel();
        escopo.Children.Add(_seletor.Painel);
        escopo.Children.Add(_recadoDoEscopo);
        var oQue = new GroupBox
        {
            Header = new TextBlock { Text = Tr.T("O que analisar"), FontWeight = FontWeights.SemiBold },
            Content = escopo,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(10, 10, 10, 0),
            ToolTip = Tr.T("Vale para todas as abas: inserir, apagar, analisar, tirar cores e quantificar mexem só nas mesas escolhidas aqui."),
        };

        // As linhas de pilar do desenho: com biposte, as análises de pilar
        // têm uma coluna de limites para cada linha.
        IReadOnlySet<PillarLine> linhas = new HashSet<PillarLine>();
        Tentar(() => linhas = AnalisesIndependentes.Linhas(_database));
        Tentar(() => DarFundoAosTextosAntigos(documento));
        Tentar(() => DeitarOsTextosAntigos(documento));

        var abas = new TabControl { Margin = new Thickness(10) };

        foreach (var tipo in Analises)
            abas.Items.Add(new TabItem { Header = Titulo(tipo), Content = new PainelDeAnalise(this, tipo, linhas), ToolTip = Tr.F("Análise de {0}.", IndependentAnalysis.Name(tipo)) });

        abas.Items.Add(new TabItem { Header = Tr.T("Quantidades"), Content = AbaQuantidades(), ToolTip = Tr.T("As quantificações feitas, como vão para o Excel.") });
        abas.SelectionChanged += (_, e) => { if (e.Source == abas) AtualizarQuantidades(); };

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 90, Height = 26, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, ToolTip = Tr.T("Fecha a janela; o que foi feito já está no desenho.") };

        // A janela é solta (03/10/2026: "quero poder mexer na tela análises
        // e no CAD ao mesmo tempo"): IsCancel só fecha diálogo, aqui fecha à mão.
        fechar.Click += (_, _) => Close();
        Closed += (_, _) => _seletor.Soltar();

        var raiz = new DockPanel();
        DockPanel.SetDock(oQue, Dock.Top);
        raiz.Children.Add(oQue);
        DockPanel.SetDock(fechar, Dock.Bottom);
        raiz.Children.Add(fechar);
        raiz.Children.Add(abas);
        Content = raiz;
    }

    /// <summary>As análises, na ordem das abas.</summary>
    internal static readonly IndependentKind[] Analises =
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

    /// <summary>A janela já aberta, chamada de novo pela ribbon, lê de novo as áreas do desenho (as marcadas ficam).</summary>
    internal void LerAreas() => Tentar(() => _seletor.LerAreas());

    /// <summary>O escopo marcado em cima, ou null com o que falta no recado da seção.</summary>
    internal ShadowScope? Escopo(TextBlock recado)
    {
        try
        {
            return _seletor.Escopo();
        }
        catch (ArgumentException falta)
        {
            Dizer(recado, false, falta.Message);
            return null;
        }
    }

    /// <summary>
    /// Os textos inseridos antes do fundo e da borda (31/10/2026: "texto ruim
    /// de ver") ganham o fundo na primeira vez que a janela abre: a caixa
    /// Fundo nasce marcada e o desenho fica igual a ela. Depois disso vale o
    /// que o usuário marcar em cada aba.
    /// </summary>
    private void DarFundoAosTextosAntigos(Document documento)
    {
        var semEscolha = Analises.Where(t => AnalisesIndependentes.AparenciaGravada(_database, t) is null).ToList();
        if (semEscolha.Count == 0) return;

        EscritaForaDeComando.Fazer(documento, () =>
        {
            // Uma varredura do desenho para as seis; grava só as que tinham
            // texto, para o desenho sem análise não ficar modificado à toa.
            foreach (var tipo in AnalisesIndependentes.AplicarAparencia(_database, semEscolha, out _))
                AnalisesIndependentes.GravarAparencia(_database, tipo, AnalysisTextLook.Default);
        });
    }

    /// <summary>A marca no desenho de que os textos antigos já foram deitados no plano dos módulos.</summary>
    private const string ChaveDosTextosDeitados = "ANALISE_TEXTOS_NO_PLANO";

    /// <summary>
    /// Os textos de ponta e de pilar de antes de 31/10/2026 ("o texto fica
    /// por traz") vão para o plano dos módulos na primeira vez que a janela
    /// abre; o desenho guarda que já foram.
    /// </summary>
    private void DeitarOsTextosAntigos(Document documento)
    {
        using (var marca = PluginDictionary.Load(_database, ChaveDosTextosDeitados))
        {
            if (marca is not null) return;
        }

        EscritaForaDeComando.Fazer(documento, () =>
        {
            var (deitados, textos, ficaram) = AnalisesIndependentes.DeitarTextosAntigos(_database);

            // Sem texto de ponta ou de pilar, nada a gravar: abrir a janela
            // não deixa o desenho modificado à toa.
            if (textos == 0) return;

            PluginDictionary.Save(_database, ChaveDosTextosDeitados, new ResultBuffer(new TypedValue((int)DxfCode.Text, "1")));
            if (deitados > 0)
            {
                RegistroDeDiagnostico.Registrar($"Análises: {deitados} texto(s) antigos deitados no plano dos módulos.");
                _atualizarTela();
            }

            if (ficaram > 0) RegistroDeDiagnostico.Registrar($"Análises: {ficaram} texto(s) antigos ficaram como estavam (longe da mesa ou em camada travada).");
        });
    }

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

    /// <summary>O recado do seletor (a área marcada, ou o que não deu).</summary>
    private void RecadoDoEscopo(string texto, bool erro)
    {
        _recadoDoEscopo.Visibility = System.Windows.Visibility.Visible;
        Dizer(_recadoDoEscopo, !erro, texto);
    }

    /// <summary>Roda o manipulador protegido: exceção solta num clique de WPF fecharia o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de análises.", erro);
            RecadoDoEscopo(Tr.F("Não consegui: {0}", erro.Message), true);
        }
    }

    // --------------------------------------------------------- quantidades

    private UIElement AbaQuantidades()
    {
        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("A última quantificação de cada análise (o botão Quantificar de cada aba), com o lugar onde foi feita. É o que vai para o Excel, junto com o quantitativo de mesas, módulos e pilares."),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });
        pilha.Children.Add(_quantidades);

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Para a planilha, use o botão Exportar da ribbon (painel Saída)."),
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
                    : (q.Lines is { Count: > 0 } linhas
                        ? string.Join("\n", linhas.Select(l => IndependentAnalysis.DescribeLine(tipo, q.Rule, l.Line, l.Count, q.Unit)))
                        : IndependentAnalysis.Describe(tipo, q.Rule, q.Points, q.Unit))
                      + (q.Modules is { } m ? Tr.F("\nmódulos — {0}", IndependentAnalysis.Describe(tipo, q.Rule, m, q.Unit)) : "")
                      + Tr.F("\n({0}, em {1:dd/MM/yyyy HH:mm})", q.Where, q.When),
                Foreground = q is null ? Brushes.Gray : Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    internal static void Dizer(TextBlock alvo, bool ok, string frase)
    {
        alvo.Foreground = ok ? Brushes.ForestGreen : Brushes.Firebrick;
        alvo.Text = frase;
        alvo.Visibility = System.Windows.Visibility.Visible;
    }
}

/// <summary>Uma aba da janela de análises: as três seções de uma análise.</summary>
internal sealed class PainelDeAnalise : ScrollViewer
{
    private readonly JanelaDeAnalises _janela;
    private readonly IndependentKind _tipo;

    /// <summary>
    /// As colunas dos limites: uma só, ou, nas análises de pilar de um
    /// desenho com biposte, uma por linha de pilar que o desenho tem (a
    /// monoposte, se houver, a da frente e a de trás).
    /// </summary>
    private readonly IReadOnlyList<PillarLine> _colunas;

    /// <summary>A regra gravada quando a janela abriu: os limites da linha que não tem coluna ficam como estavam.</summary>
    private readonly ThresholdRule _gravada;

    private readonly Dictionary<PillarLine, (TextBox Abaixo, TextBox Acima)> _limites = [];

    private readonly CheckBox _usarAbaixo = new() { Content = Tr.T("Menor que"), VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = Tr.T("Desmarcado: nada é pintado por ser menor.") };
    private readonly ComboBox _corAbaixo;
    private readonly CheckBox _usarAcima = new() { Content = Tr.T("Maior que"), VerticalAlignment = VerticalAlignment.Center, Width = 90, ToolTip = Tr.T("Desmarcado: nada é pintado por ser maior.") };
    private readonly ComboBox _corAcima;
    private readonly CheckBox _pecas = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly ComboBox _unidade = new() { Width = 150, Height = 24 };

    private readonly CheckBox _fundo = new()
    {
        Content = Tr.T("Fundo"),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 12, 0),
        ToolTip = Tr.T("Uma máscara na cor do fundo da tela atrás do texto: o módulo e as linhas de trás somem e o texto fica legível. Vale já para os textos desta análise no desenho e para os próximos."),
    };

    private readonly CheckBox _borda = new()
    {
        Content = Tr.T("Borda"),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = Tr.T("Um quadro em volta do texto, na cor dele (a da regra, depois de Analisar). Vale já para os textos desta análise no desenho e para os próximos."),
    };

    private readonly TextBlock _recadoTextos = Recado();
    private readonly TextBlock _recadoCores = Recado();
    private readonly TextBlock _recadoQuantidade = Recado();

    internal PainelDeAnalise(JanelaDeAnalises janela, IndependentKind tipo, IReadOnlySet<PillarLine> linhas)
    {
        _janela = janela;
        _tipo = tipo;

        var regra = AnalisesIndependentesCommands.Regra(janela.Database, tipo);
        var unidade = AnalisesIndependentesCommands.Unidade(janela.Database);
        var aparencia = AnalisesIndependentes.Aparencia(janela.Database, tipo);
        var nome = IndependentAnalysis.Name(tipo);
        var emMetro = tipo != IndependentKind.Slope;

        _gravada = regra;
        _colunas = ThresholdForm.Columns(tipo, linhas);

        _corAbaixo = PaletaDeCores.Caixa(regra.BelowColor, Tr.T("Cor de quem fica abaixo do limite."));
        _corAcima = PaletaDeCores.Caixa(regra.AboveColor, Tr.T("Cor de quem fica acima do limite."));

        foreach (var linha in _colunas)
        {
            var daLinha = regra.ForLine(linha);
            var naLinha = _colunas.Count == 1 ? "" : linha switch
            {
                PillarLine.Front => Tr.T(" do pilar da frente"),
                PillarLine.Rear => Tr.T(" do pilar de trás"),
                _ => Tr.T(" da monoposte"),
            };
            var vazio = _colunas.Count > 1 ? Tr.T(" Vazio: sem limite nesta linha.") : "";

            _limites[linha] = (
                Caixa(daLinha.Below, Tr.F("Limite de baixo{0}{1}. Igual ao limite conta como dentro.{2}", naLinha, emMetro ? " (m)" : "", vazio)),
                Caixa(daLinha.Above, Tr.F("Limite de cima{0}{1}. Igual ao limite conta como dentro.{2}", naLinha, emMetro ? " (m)" : "", vazio)));
        }

        _usarAbaixo.IsChecked = _colunas.Any(l => regra.ForLine(l).Below is not null);
        _usarAcima.IsChecked = _colunas.Any(l => regra.ForLine(l).Above is not null);
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

        // O fundo e a borda mudam já os textos que estão no desenho: os
        // eventos entram depois do valor inicial, senão abrir a janela mexia.
        _fundo.IsChecked = aparencia.Background;
        _borda.IsChecked = aparencia.Border;
        _fundo.Checked += (_, _) => MudarAparencia();
        _fundo.Unchecked += (_, _) => MudarAparencia();
        _borda.Checked += (_, _) => MudarAparencia();
        _borda.Unchecked += (_, _) => MudarAparencia();

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
        var linhaDosTextos = new WrapPanel { Orientation = Orientation.Horizontal };
        linhaDosTextos.Children.Add(Botoes(
            (Tr.T("Inserir textos"), Tr.F("Escreve {0} nas mesas marcadas em cima (O que analisar); os textos desta análise que já existiam nelas saem antes.", nome), Inserir),
            (Tr.T("Apagar textos"), Tr.F("Apaga os textos de {0} nas mesas marcadas em cima. As outras análises ficam.", nome), Apagar)));
        var aparenciaDoTexto = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        aparenciaDoTexto.Children.Add(new TextBlock { Text = Tr.T("Texto com"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        aparenciaDoTexto.Children.Add(_fundo);
        aparenciaDoTexto.Children.Add(_borda);
        linhaDosTextos.Children.Add(aparenciaDoTexto);
        textos.Children.Add(linhaDosTextos);
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

        cores.Children.Add(Faixas(emMetro));
        cores.Children.Add(_pecas);
        cores.Children.Add(Botoes(
            (Tr.T("Analisar"), Tr.T("Grava a regra no desenho e pinta quem sai da faixa nas mesas marcadas em cima (refaz a pintura anterior desta análise nelas)."), Analisar),
            (Tr.T("Tirar cores"), Tr.T("Volta à cor de antes (da camada, do tipo de mesa ou magenta) o que esta análise pintou nas mesas marcadas em cima; as cores das outras análises ficam."), TirarCores)));
        cores.Children.Add(_recadoCores);
        pilha.Children.Add(Secao(Tr.T("2. Cores"), cores));

        // 3. Quantidades
        var quantidades = new StackPanel();
        quantidades.Children.Add(Botoes((Tr.T("Quantificar"), Tr.T("Conta, nas mesas marcadas em cima, quantos ficaram abaixo, dentro e acima da faixa (com a regra acima); fica gravado para o Excel, com o lugar."), Quantificar)));
        quantidades.Children.Add(_recadoQuantidade);
        pilha.Children.Add(Secao(Tr.T("3. Quantidades"), quantidades));

        Content = pilha;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private static string Explicacao(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => Tr.T("A altura livre da ponta baixa do módulo em cada pilar (PB; no biposte, uma por pórtico), escrita fora da borda baixa da mesa."),
        IndependentKind.HighEdge => Tr.T("A altura livre da ponta alta do módulo em cada pilar (PA; no biposte, uma por pórtico), escrita fora da borda alta da mesa."),
        IndependentKind.Slope => Tr.T("A declividade de cada mesa ao longo da fileira, com a seta descendo."),
        IndependentKind.PillarBuried => Tr.T("A parte ENTERRADA de cada pilar (E): do terreno até a ponta de baixo. Escrita junto ao pilar, um pouco para a borda baixa, deitada sobre os módulos."),
        IndependentKind.PillarLength => Tr.T("O comprimento TOTAL de cada pilar (PT): parte livre mais parte enterrada. Escrito junto ao pilar, um pouco para a borda alta, deitado sobre os módulos."),
        _ => Tr.T("A parte LIVRE de cada pilar (P): do terreno até o topo, fora da terra. Escrita sobre o pilar, deitada sobre os módulos."),
    };

    // O recado só ocupa lugar quando tem o que dizer (02/10/2026: "nada de espaço em branco").
    private static TextBlock Recado() => new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = System.Windows.Visibility.Collapsed };

    private static GroupBox Secao(string titulo, UIElement conteudo) => new()
    {
        Header = new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold },
        Content = conteudo,
        Padding = new Thickness(8),
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static TextBox Caixa(double? valor, string dica) => new()
    {
        Width = 70,
        Height = 24,
        Margin = new Thickness(4, 0, 4, 0),
        Text = valor is { } v ? v.ToString("0.###", Tr.Culture) : "",
        ToolTip = dica,
    };

    /// <summary>
    /// As duas faixas (menor que, maior que). Uma coluna: a linha de sempre.
    /// Com biposte nos pilares, uma grade: uma coluna de limites para cada
    /// linha de pilar, e a cor no fim, a mesma para todas.
    /// </summary>
    private UIElement Faixas(bool emMetro)
    {
        if (_colunas.Count == 1)
        {
            var (abaixo, acima) = _limites[_colunas[0]];
            var faixas = new StackPanel();
            faixas.Children.Add(Faixa(_usarAbaixo, abaixo, _corAbaixo, emMetro));
            faixas.Children.Add(Faixa(_usarAcima, acima, _corAcima, emMetro));
            return faixas;
        }

        var grade = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        for (var c = 0; c < _colunas.Count + 3; c++) grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var r = 0; r < 3; r++) grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void Por(UIElement e, int linha, int coluna)
        {
            Grid.SetRow(e, linha);
            Grid.SetColumn(e, coluna);
            grade.Children.Add(e);
        }

        for (var i = 0; i < _colunas.Count; i++)
        {
            Por(new TextBlock { Text = IndependentAnalysis.LineName(_colunas[i]), FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 12, 2) }, 0, 1 + i);

            var (abaixo, acima) = _limites[_colunas[i]];
            Por(ComUnidade(abaixo, emMetro), 1, 1 + i);
            Por(ComUnidade(acima, emMetro), 2, 1 + i);
        }

        Por(_usarAbaixo, 1, 0);
        Por(_usarAcima, 2, 0);

        var pintarAbaixo = new TextBlock { Text = Tr.T("pintar de"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var pintarAcima = new TextBlock { Text = Tr.T("pintar de"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Por(pintarAbaixo, 1, _colunas.Count + 1);
        Por(pintarAcima, 2, _colunas.Count + 1);
        Por(_corAbaixo, 1, _colunas.Count + 2);
        Por(_corAcima, 2, _colunas.Count + 2);

        // A margem de cada linha, como na faixa de uma coluna.
        foreach (UIElement e in grade.Children)
        {
            if (e is FrameworkElement f && Grid.GetRow(f) > 0) f.Margin = new Thickness(f.Margin.Left, 3, f.Margin.Right, 3);
        }

        return grade;
    }

    private static StackPanel ComUnidade(TextBox caixa, bool emMetro)
    {
        var celula = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
        celula.Children.Add(caixa);
        celula.Children.Add(new TextBlock { Text = emMetro ? "m" : "", VerticalAlignment = VerticalAlignment.Center, Width = 14 });
        return celula;
    }

    private static StackPanel Faixa(CheckBox usar, TextBox valor, ComboBox cor, bool emMetro)
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

    /// <summary>A regra das caixas (<see cref="ThresholdForm"/>), ou null com o motivo no recado da seção que pediu.</summary>
    private ThresholdRule? LerRegra(TextBlock recado)
    {
        var regra = ThresholdForm.Build(
            _colunas,
            _limites.ToDictionary(l => l.Key, l => (l.Value.Abaixo.Text, l.Value.Acima.Text)),
            _usarAbaixo.IsChecked == true,
            _usarAcima.IsChecked == true,
            PaletaDeCores.Cor(_corAbaixo),
            PaletaDeCores.Cor(_corAcima),
            _pecas.IsChecked == true,
            _gravada,
            out var erro);

        if (regra is null) JanelaDeAnalises.Dizer(recado, false, erro ?? Tr.T("A regra não serve."));
        return regra;
    }

    /// <summary>Se a unidade da declividade mudou, grava e reinsere nela os textos que existiam.</summary>
    private void AplicarUnidade()
    {
        if (_tipo != IndependentKind.Slope) return;

        var nova = Unidade();
        var atual = AnalisesIndependentesCommands.Unidade(_janela.Database);
        if (nova == atual) return;

        SetaDeDeclividade.Gravar(_janela.Database, SetaDeDeclividade.Ler(_janela.Database).Ligada, nova);
        AnalisesIndependentes.Reinserir(_janela.Database, _tipo, nova);
    }

    private void Inserir()
    {
        if (_janela.Escopo(_recadoTextos) is not { } escopo) return;

        var (ok, frase) = _janela.Fazer(Tr.N("inserir os textos"), () =>
        {
            AplicarUnidade();
            var (mesas, onde) = OperacoesDeAnalise.Resolver(_janela.Database, escopo);
            return OperacoesDeAnalise.Inserir(_janela.Database, _tipo, Unidade(), mesas, onde);
        });

        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Apagar()
    {
        if (_janela.Escopo(_recadoTextos) is not { } escopo) return;

        var (ok, frase) = _janela.Fazer(Tr.N("apagar os textos"), () =>
        {
            var (mesas, onde) = OperacoesDeAnalise.Resolver(_janela.Database, escopo);
            return OperacoesDeAnalise.Apagar(_janela.Database, _tipo, mesas, onde);
        });

        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void MudarAparencia()
    {
        var aparencia = new AnalysisTextLook(_fundo.IsChecked == true, _borda.IsChecked == true);
        var (ok, frase) = _janela.Fazer(Tr.N("mudar o fundo e a borda dos textos"), () => OperacoesDeAnalise.Aparencia(_janela.Database, _tipo, aparencia));
        JanelaDeAnalises.Dizer(_recadoTextos, ok, frase);
    }

    private void Analisar()
    {
        if (_janela.Escopo(_recadoCores) is not { } escopo) return;
        if (LerRegra(_recadoCores) is not { } regra) return;

        var (ok, frase) = _janela.Fazer(Tr.N("analisar"), () =>
        {
            AplicarUnidade();
            var (mesas, onde) = OperacoesDeAnalise.Resolver(_janela.Database, escopo);
            return OperacoesDeAnalise.Analisar(_janela.Database, _tipo, regra, Unidade(), mesas, onde);
        });

        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void TirarCores()
    {
        if (_janela.Escopo(_recadoCores) is not { } escopo) return;

        var (ok, frase) = _janela.Fazer(Tr.N("tirar as cores"), () =>
        {
            var (mesas, onde) = OperacoesDeAnalise.Resolver(_janela.Database, escopo);
            return OperacoesDeAnalise.TirarCores(_janela.Database, _tipo, mesas, onde);
        });

        JanelaDeAnalises.Dizer(_recadoCores, ok, frase);
    }

    private void Quantificar()
    {
        if (_janela.Escopo(_recadoQuantidade) is not { } escopo) return;
        if (LerRegra(_recadoQuantidade) is not { } regra) return;

        var (ok, frase) = _janela.Fazer(Tr.N("quantificar"), () =>
        {
            var (mesas, onde) = OperacoesDeAnalise.Resolver(_janela.Database, escopo);
            return OperacoesDeAnalise.Quantificar(_janela.Database, _tipo, regra, Unidade(), mesas, onde);
        });

        JanelaDeAnalises.Dizer(_recadoQuantidade, ok, frase);
        _janela.AtualizarQuantidades();
    }
}

/// <summary>
/// O que cada botão da janela de análises faz, com o escopo já resolvido
/// nas mesas: a janela e o gancho de teste do nível 2 passam pelo mesmo
/// caminho. Cada operação devolve a frase da seção.
/// </summary>
internal static class OperacoesDeAnalise
{
    /// <summary>
    /// As mesas do escopo (null no desenho inteiro: o caminho de sempre, que
    /// leva também o texto que ficou sem mesa) e o nome do lugar.
    /// </summary>
    internal static (IReadOnlySet<Guid>? Mesas, string Onde) Resolver(Database database, ShadowScope escopo)
    {
        if (escopo.Kind == ShadowScopeKind.Drawing) return (null, Tr.T("desenho inteiro"));

        var (mesas, _) = SombrasCommands.MesasDoEscopo(database, escopo);
        return (mesas, Onde(escopo, AreasDoDesenho.Varrer(database)));
    }

    /// <summary>O lugar para a frase e para o Excel: "Área 1", "Área 1, Área 2", "12 mesa(s) escolhida(s)".</summary>
    internal static string Onde(ShadowScope escopo, IReadOnlyList<AreaNoDesenho> areas)
    {
        var nomes = string.Join(", ", escopo.Areas.Select(id => areas.FirstOrDefault(a => a.Identity.Id == id)?.Identity.DisplayName ?? "?"));

        return escopo.Kind switch
        {
            ShadowScopeKind.Selection => Tr.F("{0} mesa(s) escolhida(s)", escopo.Tables.Count),
            ShadowScopeKind.Area or ShadowScopeKind.Areas => nomes,
            ShadowScopeKind.AreaAndSelection => Tr.F("{0} e {1} mesa(s) escolhida(s)", nomes, escopo.Tables.Count),
            _ => Tr.T("desenho inteiro"),
        };
    }

    private static string Lugar(string onde) => onde.Length == 0 ? onde : char.ToUpper(onde[0], Tr.Culture) + onde[1..];

    internal static string Inserir(Database database, IndependentKind tipo, SlopeUnit unidade, IReadOnlySet<Guid>? mesas, string onde)
    {
        var (criados, contadas, ignoradas) = AnalisesIndependentes.Inserir(database, tipo, unidade, mesas);

        if (contadas == 0)
            return mesas is null ? Tr.T("O desenho não tem mesa gerada pelo plugin; nada inserido.") : Tr.F("{0}: nenhuma mesa; nada inserido.", Lugar(onde));

        return Tr.F("{0}: {1} texto(s) em {2} mesa(s).", Lugar(onde), criados, contadas)
            + (ignoradas > 0 ? Tr.F(" {0} mesa(s) com contorno deformado ficaram de fora.", ignoradas) : "");
    }

    internal static string Apagar(Database database, IndependentKind tipo, IReadOnlySet<Guid>? mesas, string onde) =>
        Tr.F("{0}: {1} texto(s) apagado(s).", Lugar(onde), AnalisesIndependentes.Apagar(database, tipo, mesas));

    /// <summary>Grava a regra (vale para as próximas vezes) e pinta as mesas do escopo.</summary>
    internal static string Analisar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade, IReadOnlySet<Guid>? mesas, string onde)
    {
        AnalisesIndependentesCommands.GravarRegra(database, tipo, regra);
        var (textos, pecas) = AnalisesIndependentes.Analisar(database, tipo, regra, unidade, mesas);

        return Tr.F("{0}: ", Lugar(onde))
            + (regra.PaintPieces ? Tr.F("{0} texto(s) pintado(s), {1} peça(s) pintada(s).", textos, pecas) : Tr.F("{0} texto(s) pintado(s).", textos))
            + (textos == 0 && !regra.PaintPieces ? Tr.T(" Nenhum texto desta análise aqui: insira os textos primeiro.") : "");
    }

    internal static string TirarCores(Database database, IndependentKind tipo, IReadOnlySet<Guid>? mesas, string onde) =>
        Tr.F("{0}: {1} entidade(s) de volta à cor de antes.", Lugar(onde), AnalisesIndependentes.TirarCores(database, tipo, mesas));

    /// <summary>Conta, grava para o Excel (com o lugar e, no biposte, a conta de cada linha de pilar) e descreve.</summary>
    internal static string Quantificar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade, IReadOnlySet<Guid>? mesas, string onde)
    {
        var (pontos, modulos, biposte, linhas) = AnalisesIndependentes.Quantificar(database, tipo, regra, unidade, mesas);

        // O desenho inteiro vai sem nome: a aba e o Excel o dizem no idioma da tela.
        QuantificacaoGravada.Gravar(database, tipo, regra, unidade, pontos, modulos, mesas is null ? null : onde, linhas);

        if (linhas is { Count: > 0 })
            return Tr.F("{0}, {1}:", Lugar(onde), IndependentAnalysis.Name(tipo))
                + string.Concat(linhas.Select(l => "\n" + IndependentAnalysis.DescribeLine(tipo, regra, l.Line, l.Count, unidade)));

        return Tr.F("{0}, em {1}: {2}", Lugar(onde), IndependentAnalysis.CountedIn(tipo, biposte), IndependentAnalysis.Describe(tipo, regra, pontos, unidade))
            + (modulos is not null ? Tr.F("\nEm módulos: {0}", IndependentAnalysis.Describe(tipo, regra, modulos, unidade)) : "");
    }

    /// <summary>Grava o fundo e a borda da análise e põe nos textos dela que já estão no desenho.</summary>
    internal static string Aparencia(Database database, IndependentKind tipo, AnalysisTextLook aparencia)
    {
        AnalisesIndependentes.GravarAparencia(database, tipo, aparencia);
        var textos = AnalisesIndependentes.AplicarAparencia(database, tipo);

        return textos == 0
            ? Tr.T("Fundo e borda gravados: valem para os próximos textos.")
            : Tr.F("Fundo e borda postos em {0} texto(s) desta análise.", textos);
    }
}
