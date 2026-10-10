using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A janela da mesa: todos os campos, o comprimento que sai deles e a planta
/// baixa com as sobras.
///
/// A janela recalcula a cada tecla de propósito. O erro que ela existe para
/// pegar não é de conta — é de digitação, e ele não se denuncia sozinho num
/// campo: 28 módulos em 1V dão uma mesa de 37 m, e o número 37,224 parece tão
/// razoável quanto 18,702. O que denuncia é a planta mudando de forma na hora.
///
/// Recalcular a cada tecla tem um preço, e ele é alto: o cálculo roda dentro
/// de um manipulador de evento do WPF, e exceção não tratada ali não fecha a
/// janela — fecha o Civil 3D. Por isso <see cref="Recalcular"/> é inteiro
/// dentro de um try, e não só o trecho que parece perigoso. Bastava digitar 5
/// no espaçamento, com contagem alta, para a mesa passar de mil vãos e o
/// AutoCAD do Renan fechar sem salvar nada.
///
/// Montada em código, como a janela do terreno. É mais janela que aquela, mas
/// ainda são caixas de texto numa grade; um arquivo de marcação traria o
/// aparato de XAML sem trazer clareza.
/// </summary>
internal sealed class JanelaDeMesa : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly TableProfileStore _perfis;

    /// <summary>
    /// Os vãos escritos par a par na janela de vãos (8.2), ou null para a
    /// tabela sair do vão-alvo.
    /// </summary>
    private IReadOnlyList<double>? _vaosEscritos;

    /// <summary>Os módulos da lista e de onde vieram (serviço ou embutida, passo 8.3).</summary>
    private IReadOnlyList<SolarModule> _modulos;
    private readonly Button _cadastrarModulo = new()
    {
        Content = Tr.T("Cadastrar módulo..."),
        Height = 24,
        Margin = new Thickness(0, 2, 0, 6),
        ToolTip = Tr.T("Grava um módulo novo no serviço local (biblioteca de módulos). Precisa do serviço no ar: tools\\servico-local.ps1."),
    };
    private readonly TextBlock _origemDosModulos = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };

    private readonly ComboBox _salvos = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _nome = Campo();
    private readonly ComboBox _modelo = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _altura = Campo();
    private readonly TextBox _largura = Campo();
    private readonly TextBox _espessura = Campo();
    private readonly TextBox _potencia = Campo();

    // Os dados elétricos do módulo (item 15 de 10/10/2026): do .PAN ou à mão.
    private readonly TextBox _voc = Campo();
    private readonly TextBox _isc = Campo();
    private readonly TextBox _vmp = Campo();
    private readonly TextBox _imp = Campo();
    private readonly TextBox _betaVoc = Campo();
    private readonly TextBox _gamaPmax = Campo();
    private readonly TextBox _alfaIsc = Campo();
    private readonly TextBlock _origemEletrica = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
    private readonly Button _carregarPan = new()
    {
        Content = Tr.T("Carregar .PAN..."),
        Height = 24,
        Margin = new Thickness(0, 2, 6, 2),
        ToolTip = Tr.T("Lê o arquivo PAN do PVsyst: fabricante, modelo, Pmáx, Voc, Isc, Vmp, Imp e os coeficientes de temperatura. A potência do módulo passa a ser a do PAN; as medidas não mudam. É a fonte única do módulo para a parte elétrica."),
    };
    private readonly Button _limparEletrica = new()
    {
        Content = Tr.T("Limpar"),
        Height = 24,
        Margin = new Thickness(0, 2, 0, 2),
        ToolTip = Tr.T("Tira os dados elétricos do módulo desta estrutura: as tensões e correntes da rota ficam em branco."),
    };

    /// <summary>O PAN lido (ou o que veio do perfil) e os textos com que ele encheu os campos: mudou um campo, os valores passam a ser à mão.</summary>
    private PanModule? _panLido;
    private string[] _textosDoPan = [];
    private readonly TextBox _quantidade = Campo();
    private readonly ComboBox _arranjo = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _espacamentoH = Campo();
    private readonly TextBox _espacamentoV = Campo();
    private readonly TextBox _sobraEsquerda = Campo();
    private readonly TextBox _sobraDireita = Campo();
    private readonly TextBox _inclinacao = Campo();
    private readonly TextBox _tesoura = Campo();
    private readonly TextBox _pilarNaTesoura = Campo();
    private readonly TextBox _pilarLargura = Campo();
    private readonly TextBox _pilarProfundidade = Campo();
    private readonly TextBox _vaoAlvo = Campo();
    private readonly TextBox _balanco = Campo();
    private readonly TextBox _enterro = Campo();
    private readonly Button _botaoDosVaos = new()
    {
        Content = Tr.T("Vãos entre pilares..."),
        Height = 24,
        Margin = new Thickness(0, 2, 0, 2),
        ToolTip = Tr.T("Abre a lista P1-P2, P2-P3... para escrever cada vão; mostra a soma e se ela fecha com a mesa."),
    };
    private readonly TextBlock _resumoDosVaos = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };

    private readonly PlantaDaMesa _planta = new();
    private readonly CorteDaMesa _corte = new();
    private readonly TextBlock _resumo = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0),
        FontSize = 12.5,
    };

    private readonly Button _salvar;
    private readonly Button _usar;

    /// <summary>
    /// Marca e modelo do módulo como vieram do perfil, para o caso de ele não
    /// estar na biblioteca.
    ///
    /// Sem guardar, abrir o perfil de um colega com um modelo que este plugin
    /// não conhece e salvar apagava o nome do modelo dele, trocando por
    /// "(à mão)".
    /// </summary>
    private string _marcaDoPerfil = string.Empty;
    private string _modeloDoPerfil = string.Empty;

    /// <summary>
    /// Se a janela está enchendo os campos por conta própria.
    ///
    /// Serve para dois motivos. Antes de a janela existir por inteiro, um
    /// recálculo tocaria em controle ainda nulo. E durante o preenchimento,
    /// escolher o modelo na lista dispararia a troca automática de medidas —
    /// jogando fora exatamente os ajustes à mão que o perfil guardava.
    /// </summary>
    private bool _preenchendo = true;

    /// <summary>A mesa que o usuário confirmou, ou null se ele desistiu.</summary>
    internal TableProfile? Escolhida { get; private set; }

    /// <summary>O último perfil gravado em "Salvar perfil" (como foi lido de volta do disco), ou null.</summary>
    internal TableProfile? Salvo { get; private set; }

    /// <summary>O nome da mesa com que a janela abriu.</summary>
    private readonly string _nomeAberto;

    /// <summary>Se a janela foi aberta para uma mesa do desenho (Configurações).</summary>
    private readonly bool _doDesenho;

    /// <summary>
    /// O que volta para a mesa do desenho: a confirmada, ou a gravada em
    /// "Salvar perfil" com o mesmo nome da aberta (ver <see cref="DrawingTables.AfterEdit"/>).
    /// </summary>
    internal TableProfile? Resultado => DrawingTables.AfterEdit(_nomeAberto, Escolhida, Salvo);

    /// <param name="perfis">A biblioteca de perfis (pasta do usuário).</param>
    /// <param name="inicial">A mesa com que a janela abre.</param>
    /// <param name="doDesenho">
    /// Aberta para uma mesa do desenho (Configurações): o recado de "Salvar
    /// perfil" diz que a mesa do desenho acompanha e que falta gravar no desenho.
    /// </param>
    internal JanelaDeMesa(TableProfileStore perfis, TableProfile inicial, bool doDesenho = false)
    {
        _perfis = perfis ?? throw new ArgumentNullException(nameof(perfis));
        _nomeAberto = (inicial ?? throw new ArgumentNullException(nameof(inicial))).Name;
        _doDesenho = doDesenho;

        Title = Tr.T("Clivus Solar — Mesa");
        Width = 1320;
        Height = 800;
        MinWidth = 1100;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        (_modulos, _origemDosModulos.Text) = FonteDeModulos.Carregar();
        ListarModulos();

        _arranjo.Items.Add(new ComboBoxItem
        {
            Content = Tr.T("1V — uma fileira de módulos em pé"),
            Tag = TableArrangement.SingleRow,
        });
        _arranjo.Items.Add(new ComboBoxItem
        {
            Content = Tr.T("2V — duas fileiras, uma acima da outra"),
            Tag = TableArrangement.DoubleRow,
        });

        _salvar = new Button { Content = Tr.T("Salvar perfil"), Width = 110, Height = 26 };
        _salvar.Click += (_, _) => Salvar();

        _usar = new Button
        {
            Content = Tr.T("Usar esta mesa"),
            Width = 130,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
        };
        _usar.Click += (_, _) => Confirmar();

        Content = Montar();

        ListarSalvos();
        Preencher(inicial);

        _preenchendo = false;
        Recalcular();
    }

    // ------------------------------------------------------------- montagem

    private static TextBox Campo() => new() { Margin = new Thickness(0, 2, 0, 6), Height = 24 };

    private UIElement Montar()
    {
        var grade = new Grid { Margin = new Thickness(12) };

        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grade.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Seções lado a lado, rótulo ao lado do campo (02/10/2026: "otimizar
        // espaço e melhorar UX de tudo"): enchem a altura e passam para a
        // coluna seguinte, em vez de uma coluna estreita com barra de rolagem.
        var campos = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = LayoutCompacto.Secoes((Panel)Formulario()),
        };

        Grid.SetColumn(campos, 0);
        Grid.SetRow(campos, 0);
        grade.Children.Add(campos);

        var direita = new DockPanel();

        DockPanel.SetDock(_resumo, Dock.Bottom);
        direita.Children.Add(_resumo);

        // Os dois desenhos empilhados: a planta conta o comprimento e os
        // pilares, o corte conta a inclinação, a tesoura e onde o pilar
        // encosta. Um não substitui o outro — e é justamente o que a planta
        // não mostra que o Renan pediu para ver.
        var desenhos = new Grid();

        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.15, GridUnitType.Star) });
        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var emCima = Emoldurar(Tr.T("PLANTA BAIXA"), _planta);
        var embaixo = Emoldurar(Tr.T("VISTA LATERAL — na direção da inclinação"), _corte);

        Grid.SetRow(emCima, 0);
        Grid.SetRow(embaixo, 2);

        desenhos.Children.Add(emCima);
        desenhos.Children.Add(embaixo);

        direita.Children.Add(desenhos);

        Grid.SetColumn(direita, 2);
        Grid.SetRow(direita, 0);
        grade.Children.Add(direita);

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };

        botoes.Children.Add(_salvar);
        botoes.Children.Add(_usar);
        botoes.Children.Add(new Button
        {
            Content = Tr.T("Fechar"),
            Width = 90,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
        });

        Grid.SetColumn(botoes, 0);
        Grid.SetColumnSpan(botoes, 3);
        Grid.SetRow(botoes, 1);
        grade.Children.Add(botoes);

        return grade;
    }

    /// <summary>Um desenho com o seu rótulo em cima.</summary>
    private static UIElement Emoldurar(string rotulo, UIElement desenho)
    {
        var painel = new DockPanel();

        var texto = new TextBlock
        {
            Text = rotulo,
            FontSize = 10.5,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 4),
        };

        DockPanel.SetDock(texto, Dock.Top);
        painel.Children.Add(texto);

        painel.Children.Add(new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = desenho,
        });

        return painel;
    }

    private UIElement Formulario()
    {
        var pilha = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

        void Secao(string titulo) => pilha.Children.Add(new TextBlock
        {
            Text = titulo.ToUpperInvariant(),
            FontSize = 10.5,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 10, 0, 2),
        });

        void Linha(string rotulo, Control campo, string? dica = null)
        {
            pilha.Children.Add(new TextBlock { Text = rotulo, FontSize = 12, ToolTip = dica });
            if (dica is not null) campo.ToolTip = dica;
            pilha.Children.Add(campo);
        }

        Secao(Tr.T("Perfil"));
        Linha(Tr.T("Perfil salvo"), _salvos);
        Linha(Tr.T("Nome"), _nome);

        Secao(Tr.T("Módulo"));
        Linha(Tr.T("Modelo"), _modelo);
        pilha.Children.Add(_origemDosModulos);
        pilha.Children.Add(_cadastrarModulo);
        Linha(Tr.T("Altura (m)"), _altura);
        Linha(Tr.T("Largura (m)"), _largura);
        Linha(Tr.T("Espessura (m)"), _espessura);
        Linha(Tr.T("Potência (Wp)"), _potencia);

        // Item 15 (10/10/2026): "adicione lá na estrutura a opção de carregar
        // o arquivo PAN ... uma fonte única, na estrutura"; sem PAN, os
        // valores à mão.
        Secao(Tr.T("Módulo: dados elétricos"));
        var botoesDoPan = new WrapPanel();
        botoesDoPan.Children.Add(_carregarPan);
        botoesDoPan.Children.Add(_limparEletrica);
        pilha.Children.Add(botoesDoPan);
        pilha.Children.Add(_origemEletrica);
        Linha(Tr.T("Voc (V)"), _voc, Tr.T("Tensão de circuito aberto (STC)."));
        Linha(Tr.T("Isc (A)"), _isc, Tr.T("Corrente de curto-circuito (STC)."));
        Linha(Tr.T("Vmp (V)"), _vmp, Tr.T("Tensão de máxima potência (STC)."));
        Linha(Tr.T("Imp (A)"), _imp, Tr.T("Corrente de máxima potência (STC)."));
        Linha(Tr.T("β Voc (%/°C)"), _betaVoc, Tr.T("Coeficiente de temperatura da Voc, como no datasheet (ex.: -0,25)."));
        Linha(Tr.T("γ Pmáx (%/°C, opcional)"), _gamaPmax, Tr.T("Coeficiente de temperatura da potência. Com ele (e o da Isc), a Vmp no calor sai mais exata; em branco, usa o da Voc."));
        Linha(Tr.T("α Isc (%/°C, opcional)"), _alfaIsc, Tr.T("Coeficiente de temperatura da Isc. Em branco, zero."));

        Secao(Tr.T("Mesa"));
        Linha(Tr.T("Módulos"), _quantidade);
        Linha(Tr.T("Arranjo"), _arranjo);
        Linha(Tr.T("Espaçamento entre módulos (m)"), _espacamentoH);
        Linha(Tr.T("Espaçamento entre fileiras (m)"), _espacamentoV);
        Linha(Tr.T("Sobra da esquerda (m)"), _sobraEsquerda);
        Linha(Tr.T("Sobra da direita (m)"), _sobraDireita);
        Linha(Tr.T("Inclinação (graus)"), _inclinacao);

        Secao(Tr.T("Estrutura"));
        Linha(Tr.T("Tesoura T1 (m)"), _tesoura, Tr.T("Comprimento da tesoura, ao longo da inclinação."));
        Linha(Tr.T("Pilar na tesoura T2 (m)"), _pilarNaTesoura, Tr.T("Onde o pilar encosta na tesoura, medido da ponta baixa dela."));
        Linha(Tr.T("Pilar: largura ao longo da fileira (m)"), _pilarLargura, Tr.T("Lado da seção do pilar no sentido da fileira (comprimento da mesa)."));
        Linha(Tr.T("Pilar: largura na inclinação (m)"), _pilarProfundidade, Tr.T("Lado da seção do pilar no sentido da inclinação (tesoura)."));
        Linha(Tr.T("Enterro mínimo T3 (m)"), _enterro, Tr.T("O mínimo que o pilar fica dentro do solo. Em branco, vale o enterro mínimo da configuração do projeto."));
        Linha(Tr.T("Vão-alvo entre pilares (m)"), _vaoAlvo, Tr.T("Vão pretendido; a tabela divide a mesa em vãos iguais perto dele. Não vale quando há vãos escritos."));
        pilha.Children.Add(_botaoDosVaos);
        pilha.Children.Add(_resumoDosVaos);
        Linha(Tr.T("Balanço nas pontas (m)"), _balanco, Tr.T("Quanto de estrutura sobra para fora do primeiro e do último pilar."));

        foreach (var (campo, _) in Todos()) campo.TextChanged += (_, _) => Recalcular();

        _arranjo.SelectionChanged += (_, _) => Recalcular();
        _modelo.SelectionChanged += (_, _) => TrocarModulo();
        _salvos.SelectionChanged += (_, _) => CarregarSalvo();
        _enterro.TextChanged += (_, _) => Recalcular();
        _botaoDosVaos.Click += (_, _) => EscreverVaos();
        _cadastrarModulo.Click += (_, _) => CadastrarModulo();
        _carregarPan.Click += (_, _) => CarregarPan();
        _limparEletrica.Click += (_, _) => LimparEletrica();

        foreach (var campo in CamposEletricos()) campo.TextChanged += (_, _) => Recalcular();

        return pilha;
    }

    /// <summary>
    /// Os campos de texto e o nome de cada um, para a mensagem de erro poder
    /// dizer qual está errado.
    ///
    /// "Há campo em branco" manda o projetista procurar entre quinze caixas.
    /// O resto do Core se dá ao trabalho de nomear o campo em toda mensagem;
    /// jogar isso fora justamente no erro mais comum seria desperdício.
    /// </summary>
    private IEnumerable<(TextBox Campo, string Nome)> Todos()
    {
        yield return (_nome, Tr.T("Nome"));
        yield return (_altura, Tr.T("Altura"));
        yield return (_largura, Tr.T("Largura"));
        yield return (_espessura, Tr.T("Espessura"));
        yield return (_potencia, Tr.T("Potência"));
        yield return (_quantidade, Tr.T("Módulos"));
        yield return (_espacamentoH, Tr.T("Espaçamento entre módulos"));
        yield return (_espacamentoV, Tr.T("Espaçamento entre fileiras"));
        yield return (_sobraEsquerda, Tr.T("Sobra da esquerda"));
        yield return (_sobraDireita, Tr.T("Sobra da direita"));
        yield return (_inclinacao, Tr.T("Inclinação"));
        yield return (_tesoura, Tr.T("Tesoura T1"));
        yield return (_pilarNaTesoura, Tr.T("Pilar na tesoura T2"));
        yield return (_pilarLargura, Tr.T("Pilar: largura ao longo da fileira"));
        yield return (_pilarProfundidade, Tr.T("Pilar: largura na inclinação"));
        yield return (_vaoAlvo, Tr.T("Vão-alvo entre pilares"));
        yield return (_balanco, Tr.T("Balanço nas pontas"));
    }

    // --------------------------------------------------------- ida e volta

    /// <param name="escolher">O perfil que fica escolhido na lista, ou null para "(nenhum — mesa atual)".</param>
    private void ListarSalvos(string? escolher = null)
    {
        _salvos.Items.Clear();
        _salvos.Items.Add(new ComboBoxItem { Content = Tr.T("(nenhum — mesa atual)"), Tag = null });

        try
        {
            foreach (var nome in _perfis.List())
                _salvos.Items.Add(new ComboBoxItem { Content = nome, Tag = nome });
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui listar os perfis de mesa.", erro);
        }

        _salvos.SelectedIndex = 0;

        if (escolher is null) return;

        foreach (ComboBoxItem item in _salvos.Items)
        {
            if (item.Tag is not string nome || !string.Equals(nome, escolher, StringComparison.Ordinal)) continue;

            _salvos.SelectedItem = item;
            return;
        }
    }

    private void CarregarSalvo()
    {
        if (_preenchendo) return;
        if ((_salvos.SelectedItem as ComboBoxItem)?.Tag is not string nome) return;

        try
        {
            var perfil = _perfis.Load(nome);

            _preenchendo = true;
            Preencher(perfil);
            _preenchendo = false;

            Recalcular();
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar($"Não consegui abrir o perfil \"{nome}\".", erro);
            Avisar(Tr.F("Não consegui abrir o perfil \"{0}\": {1}", nome, erro.Message));
        }
    }

    private void Preencher(TableProfile perfil)
    {
        _marcaDoPerfil = perfil.Layout.Module.Brand;
        _modeloDoPerfil = perfil.Layout.Module.Model;

        _nome.Text = perfil.Name;

        _altura.Text = Numero(perfil.Layout.Module.Height);
        _largura.Text = Numero(perfil.Layout.Module.Width);
        _espessura.Text = Numero(perfil.Layout.Module.Thickness);
        _potencia.Text = NumeroGrande(perfil.Layout.Module.PowerWatts);

        _quantidade.Text = perfil.Layout.ModuleCount.ToString(Tr.Culture);
        _espacamentoH.Text = Numero(perfil.Layout.HorizontalGap);
        _espacamentoV.Text = Numero(perfil.Layout.VerticalGap);
        _sobraEsquerda.Text = Numero(perfil.Layout.LeftMargin);
        _sobraDireita.Text = Numero(perfil.Layout.RightMargin);
        _inclinacao.Text = Numero(perfil.TiltDegrees);

        _tesoura.Text = Numero(perfil.Frame.RafterLength);
        _pilarNaTesoura.Text = Numero(perfil.Frame.PillarAlongRafter);
        _pilarLargura.Text = Numero(perfil.Frame.PillarWidth);
        _pilarProfundidade.Text = Numero(perfil.Frame.PillarDepth);
        _vaoAlvo.Text = Numero(perfil.Frame.PillarSpanTarget);
        _balanco.Text = Numero(perfil.Frame.PillarCantilever);
        _enterro.Text = perfil.Frame.MinEmbedment is { } t3 ? Numero(t3) : string.Empty;
        _vaosEscritos = perfil.Frame.PillarSpans;

        Selecionar(_arranjo, perfil.Layout.Arrangement);
        PreencherEletrica(perfil.ModuleElectrical);

        var daBiblioteca = ModuleLibrary.Find(_modulos, perfil.Layout.Module.Model);

        _modelo.SelectedIndex = daBiblioteca is null
            ? _modelo.Items.Count - 1
            : _modulos.ToList().IndexOf(daBiblioteca);
    }

    /// <summary>
    /// O perfil que os campos descrevem, ou null com o motivo se eles ainda
    /// não descrevem mesa nenhuma.
    /// </summary>
    private TableProfile? Ler(out string motivo)
    {
        foreach (var (campo, nome) in Todos())
        {
            if (campo == _nome) continue;

            var certo = campo == _potencia || campo == _quantidade
                ? NumberInput.TryParseLarge(campo.Text, out _)
                : NumberInput.TryParseMeasure(campo.Text, out _);

            if (certo) continue;

            motivo = string.IsNullOrWhiteSpace(campo.Text)
                ? Tr.F("o campo \"{0}\" está em branco.", nome)
                : Tr.F("não consigo ler o número do campo \"{0}\".", nome);

            return null;
        }

        NumberInput.TryParseMeasure(_altura.Text, out var altura);
        NumberInput.TryParseMeasure(_largura.Text, out var largura);
        NumberInput.TryParseMeasure(_espessura.Text, out var espessura);
        NumberInput.TryParseLarge(_potencia.Text, out var potencia);
        NumberInput.TryParseMeasure(_espacamentoH.Text, out var gapH);
        NumberInput.TryParseMeasure(_espacamentoV.Text, out var gapV);
        NumberInput.TryParseMeasure(_sobraEsquerda.Text, out var esquerda);
        NumberInput.TryParseMeasure(_sobraDireita.Text, out var direita);
        NumberInput.TryParseMeasure(_inclinacao.Text, out var graus);
        NumberInput.TryParseMeasure(_tesoura.Text, out var tesoura);
        NumberInput.TryParseMeasure(_pilarNaTesoura.Text, out var t2);
        NumberInput.TryParseMeasure(_pilarLargura.Text, out var pilarL);
        NumberInput.TryParseMeasure(_pilarProfundidade.Text, out var pilarP);
        NumberInput.TryParseMeasure(_vaoAlvo.Text, out var vao);
        NumberInput.TryParseMeasure(_balanco.Text, out var balanco);

        // T3 é opcional: em branco, vale o da configuração do projeto.
        double? enterro = null;

        if (!string.IsNullOrWhiteSpace(_enterro.Text))
        {
            if (!NumberInput.TryParseMeasure(_enterro.Text, out var t3))
            {
                motivo = Tr.F("não consigo ler o número do campo \"{0}\".", Tr.T("Enterro mínimo T3"));
                return null;
            }

            enterro = t3;
        }

        if (!NumberInput.TryParseCount(_quantidade.Text, out var quantidade))
        {
            motivo = Tr.T("o número de módulos precisa ser inteiro.");
            return null;
        }

        var (marca, modelo) = Modulo();

        if (!LerEletrica(marca, modelo, potencia, out var eletrica, out var porQueEletrica))
        {
            motivo = porQueEletrica;
            return null;
        }

        var perfil = new TableProfile(
            _nome.Text,
            new TableLayout(
                new SolarModule(marca, modelo, potencia, altura, largura, espessura),
                quantidade,
                Arranjo(),
                gapH, gapV, esquerda, direita),
            new TableFrame(tesoura, t2, pilarL, pilarP, vao, balanco)
            {
                PillarSpans = _vaosEscritos,
                MinEmbedment = enterro,
            },
            graus * Math.PI / 180)
        {
            ModuleElectrical = eletrica,
        };

        if (perfil.WhyInvalid is { } porQue)
        {
            motivo = porQue + ".";
            return null;
        }

        motivo = string.Empty;
        return perfil;
    }

    /// <summary>
    /// Marca e modelo do módulo escolhido.
    ///
    /// Quando o usuário está no item "à mão", vale o que veio do perfil — não
    /// se apaga o nome do modelo de um colega só porque este plugin não o tem
    /// na biblioteca.
    /// </summary>
    private (string Marca, string Modelo) Modulo()
    {
        if ((_modelo.SelectedItem as ComboBoxItem)?.Tag is SolarModule daLista)
            return (daLista.Brand, daLista.Model);

        return string.IsNullOrWhiteSpace(_modeloDoPerfil)
            ? (string.Empty, "(à mão)")
            : (_marcaDoPerfil, _modeloDoPerfil);
    }

    private TableArrangement Arranjo() =>
        (_arranjo.SelectedItem as ComboBoxItem)?.Tag as TableArrangement? ?? TableArrangement.SingleRow;

    private void TrocarModulo()
    {
        // Durante o preenchimento a troca jogaria fora as medidas ajustadas à
        // mão que o perfil guardava.
        if (_preenchendo) return;
        if ((_modelo.SelectedItem as ComboBoxItem)?.Tag is not SolarModule modulo) return;

        _altura.Text = Numero(modulo.Height);
        _largura.Text = Numero(modulo.Width);
        _espessura.Text = Numero(modulo.Thickness);
        _potencia.Text = NumeroGrande(modulo.PowerWatts);
    }

    // ------------------------------------------------------------- o cálculo

    /// <summary>
    /// Refaz a conta e a planta.
    ///
    /// O try é de tudo, e não de um trecho: este método roda dentro de um
    /// manipulador de evento do WPF, e exceção não tratada ali derruba o Civil
    /// 3D inteiro. Já havia um caminho real para isso — espaçamento de 5 m com
    /// contagem alta passa na validação do perfil e faz a tabela de pilares
    /// passar dos mil vãos, que é recusa por exceção.
    /// </summary>
    private void Recalcular()
    {
        if (_preenchendo) return;

        MostrarOrigemDosVaos();
        MostrarOrigemEletrica();

        try
        {
            var perfil = Ler(out var motivo);

            if (perfil is null)
            {
                NaoDeu(motivo);
                return;
            }

            var pilares = perfil.Frame.Pillars(perfil.Layout);
            var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

            _planta.Mostrar(geometria);
            _corte.Mostrar(geometria, perfil.TiltRadians, perfil.Frame.MinEmbedment);

            var potencia = perfil.Layout.ModuleCount * perfil.Layout.Module.PowerWatts / 1000;

            // A subida do pilar é o número que o projetista não consegue
            // estimar de cabeça, e é o que decide se o pilar dele vai caber.
            var subida = PillarSizing.FreeHeight(0, geometria.PillarRow, perfil.TiltRadians);

            _resumo.Text =
                Tr.F("Comprimento {0} m · {1} m na inclinação · {2} colunas · {3:0.#} kWp",
                    Numero(perfil.Layout.Length), Numero(perfil.Layout.Depth), perfil.Layout.Columns, potencia)
                + "\n"
                + (_vaosEscritos is null
                    ? Tr.F("{0} pilares em {1} vãos de {2} m", pilares.PillarCount, pilares.Spans.Count, Numero(pilares.Spans[0]))
                    : Tr.F("{0} pilares em {1} vãos escritos ({2} m)", pilares.PillarCount, pilares.Spans.Count, string.Join(" + ", pilares.Spans.Select(Numero))))
                + (pilares.Cantilever > 0
                    ? Tr.F(", com balanço de {0} m em cada ponta", Numero(pilares.Cantilever))
                    : Tr.T(", com o pilar na ponta da estrutura"))
                + "\n"
                + Tr.F("O pilar encosta na mesa a {0} m da ponta baixa do módulo (sobra da tesoura {1} m), e sobe {2} m acima dela.",
                    Numero(geometria.PillarRow), Numero(geometria.RafterOffset), Numero(subida))
                + (ModuleSource.ProfileDivergence(perfil) is { } divergencia ? "\n" + Tr.F("ATENÇÃO: {0}", divergencia) : string.Empty);

            _usar.IsEnabled = true;
            _salvar.IsEnabled = true;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular a mesa.", erro);
            NaoDeu(erro.Message);
        }
    }

    private void ListarModulos()
    {
        _modelo.Items.Clear();

        foreach (var modulo in _modulos)
            _modelo.Items.Add(new ComboBoxItem { Content = modulo.Describe(), Tag = modulo });

        _modelo.Items.Add(new ComboBoxItem { Content = Tr.T("(outro módulo, medidas à mão)"), Tag = null });
    }

    /// <summary>
    /// Passo 8.4: cadastra um módulo no serviço, recarrega a lista e já o
    /// escolhe. Parte das medidas que estão nos campos.
    /// </summary>
    private void CadastrarModulo()
    {
        try
        {
            NumberInput.TryParseLarge(_potencia.Text, out var potencia);
            NumberInput.TryParseMeasure(_altura.Text, out var altura);
            NumberInput.TryParseMeasure(_largura.Text, out var largura);
            NumberInput.TryParseMeasure(_espessura.Text, out var espessura);

            var janela = new JanelaDeCadastroDeModulo(new SolarModule(string.Empty, string.Empty, potencia, altura, largura, espessura))
            {
                Owner = this,
            };

            if (janela.ShowDialog() != true || janela.Cadastrado is not { } novo) return;

            _preenchendo = true;
            (_modulos, _origemDosModulos.Text) = FonteDeModulos.Carregar();
            ListarModulos();

            var achado = ModuleLibrary.Find(_modulos, novo.Model);
            _modelo.SelectedIndex = achado is null ? _modelo.Items.Count - 1 : _modulos.ToList().IndexOf(achado);
            _preenchendo = false;

            if (achado is null)
            {
                // Gravou, mas a lista não recarregou do serviço: as medidas
                // cadastradas vão para os campos à mão, e o usuário fica sabendo.
                _marcaDoPerfil = novo.Brand;
                _modeloDoPerfil = novo.Model;
                _altura.Text = Numero(novo.Height);
                _largura.Text = Numero(novo.Width);
                _espessura.Text = Numero(novo.Thickness);
                _potencia.Text = NumeroGrande(novo.PowerWatts);
                Avisar(Tr.F("O módulo {0} foi cadastrado, mas a lista não recarregou do serviço; as medidas dele ficaram nos campos.", novo.DisplayName));
            }
            else
            {
                TrocarModulo();
            }

            Recalcular();
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar("Falha ao cadastrar módulo pela janela de Mesa.", erro);
            Avisar(Tr.F("Não consegui cadastrar o módulo: {0}", erro.Message));
        }
    }

    // ------------------------------------------------- dados elétricos (item 15)

    private IEnumerable<TextBox> CamposEletricos() => [_voc, _isc, _vmp, _imp, _betaVoc, _gamaPmax, _alfaIsc];

    private string[] TextosEletricos() => CamposEletricos().Select(c => c.Text.Trim()).ToArray();

    /// <summary>Enche os campos elétricos (null: em branco) e guarda de onde vieram.</summary>
    private void PreencherEletrica(PanModule? eletrica)
    {
        var antes = _preenchendo;
        _preenchendo = true;

        _voc.Text = eletrica is null ? string.Empty : eletrica.Voc.ToString("0.###", Tr.Culture);
        _isc.Text = eletrica is null ? string.Empty : eletrica.Isc.ToString("0.###", Tr.Culture);
        _vmp.Text = eletrica is null ? string.Empty : eletrica.Vmp.ToString("0.###", Tr.Culture);
        _imp.Text = eletrica is null ? string.Empty : eletrica.Imp.ToString("0.###", Tr.Culture);
        _betaVoc.Text = eletrica is null ? string.Empty : eletrica.VocCoefficientPercent.ToString("0.####", Tr.Culture);
        _gamaPmax.Text = eletrica?.PowerCoefficient is { } g ? g.ToString("0.####", Tr.Culture) : string.Empty;
        _alfaIsc.Text = eletrica is null || eletrica.IscCoefficient == 0 ? string.Empty : eletrica.IscCoefficientPercent.ToString("0.####", Tr.Culture);

        _panLido = eletrica;
        _textosDoPan = TextosEletricos();
        _preenchendo = antes;
        MostrarOrigemEletrica();
    }

    private void MostrarOrigemEletrica()
    {
        if (_panLido is not null && TextosEletricos().SequenceEqual(_textosDoPan))
            _origemEletrica.Text = Tr.F("Gravado na estrutura: {0}", _panLido.Describe());
        else if (TextosEletricos().All(t => t.Length == 0))
            _origemEletrica.Text = Tr.T("Sem dados elétricos: carregue o .PAN ou digite Voc, Isc, Vmp, Imp e β Voc. Sem eles, a rota não calcula tensões e correntes.");
        else
            _origemEletrica.Text = Tr.T("Valores à mão (a Pmáx é a potência do módulo).");
    }

    /// <summary>
    /// Os dados elétricos que os campos descrevem: o PAN como foi lido, se os
    /// campos não mudaram; os valores à mão, se mudaram (a Pmáx é a potência
    /// do módulo); null, se estão todos em branco. False, com o motivo, se
    /// estão pela metade ou não se leem.
    /// </summary>
    private bool LerEletrica(string marca, string modelo, double potencia, out PanModule? eletrica, out string motivo)
    {
        eletrica = null;
        motivo = string.Empty;
        var textos = TextosEletricos();

        if (textos.All(t => t.Length == 0)) return true;

        if (_panLido is not null && textos.SequenceEqual(_textosDoPan))
        {
            eletrica = _panLido;
            return true;
        }

        var obrigatorios = new (TextBox Campo, string Nome)[] { (_voc, "Voc"), (_isc, "Isc"), (_vmp, "Vmp"), (_imp, "Imp"), (_betaVoc, "β Voc") };
        var valores = new double[obrigatorios.Length];

        for (var i = 0; i < obrigatorios.Length; i++)
        {
            if (NumberInput.TryParseMeasure(obrigatorios[i].Campo.Text, out valores[i])) continue;

            motivo = string.IsNullOrWhiteSpace(obrigatorios[i].Campo.Text)
                ? Tr.F("dados elétricos pela metade: falta {0}. Preencha Voc, Isc, Vmp, Imp e β Voc, ou deixe todos em branco.", obrigatorios[i].Nome)
                : Tr.F("não consigo ler o número do campo \"{0}\".", obrigatorios[i].Nome);
            return false;
        }

        double? gama = null;
        if (!string.IsNullOrWhiteSpace(_gamaPmax.Text))
        {
            if (!NumberInput.TryParseMeasure(_gamaPmax.Text, out var g))
            {
                motivo = Tr.F("não consigo ler o número do campo \"{0}\".", "γ Pmáx");
                return false;
            }

            gama = g;
        }

        var alfa = 0.0;
        if (!string.IsNullOrWhiteSpace(_alfaIsc.Text) && !NumberInput.TryParseMeasure(_alfaIsc.Text, out alfa))
        {
            motivo = Tr.F("não consigo ler o número do campo \"{0}\".", "α Isc");
            return false;
        }

        var (voc, isc, vmp, imp, beta) = (valores[0], valores[1], valores[2], valores[3], valores[4]);
        var manual = new PanModule(marca, modelo, potencia, voc, isc, vmp, imp, beta / 100 * voc, alfa / 100 * isc, null, gama);

        if (manual.WhyInvalid() is { } porque)
        {
            motivo = Tr.F("os dados elétricos do módulo não servem: {0}.", porque);
            return false;
        }

        eletrica = manual;
        return true;
    }

    /// <summary>
    /// "Carregar .PAN..." (item 15): lê o PAN e põe na estrutura os dados
    /// elétricos e a potência dele (<see cref="TableProfile.WithPan"/>, o
    /// mesmo do CLIVUS_ESTRUTURA_PAN). As medidas não mudam.
    /// </summary>
    private void CarregarPan()
    {
        try
        {
            var caminho = DialogoDeArquivo.Abrir(Tr.T("Arquivo PAN do módulo"), Tr.T("Módulo do PVsyst (*.pan)|*.pan|Todos os arquivos (*.*)|*.*"), null);
            if (caminho is null) return;

            if (PotenciaCommands.LerPan(caminho, out var porque) is not { } pan)
            {
                Avisar(porque);
                return;
            }

            NumberInput.TryParseLarge(_potencia.Text, out var antes);
            var atual = Ler(out _);

            _preenchendo = true;
            if (atual is not null)
            {
                Preencher(atual.WithPan(pan));
            }
            else
            {
                // A mesa ainda não fecha: só os campos do módulo mudam.
                PreencherEletrica(pan);
                _potencia.Text = NumeroGrande(pan.Pmax);
            }

            _preenchendo = false;
            Recalcular();

            var recado = Tr.F("PAN lido: {0}.", pan.Describe());
            if (Math.Abs(antes - pan.Pmax) > ModuleSource.Tolerance)
                recado += "\n\n" + Tr.F("A potência do módulo passou de {0:0.#} para {1:0.#} Wp. As mesas já desenhadas continuam como estavam: depois de gravar a estrutura no desenho, use \"Atualizar a potência das mesas desenhadas\" nas Configurações.", antes, pan.Pmax);

            Avisar(recado);
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar("Falha ao carregar o PAN na janela de Mesa.", erro);
            Avisar(Tr.F("Não consegui carregar o PAN: {0}", erro.Message));
        }
    }

    private void LimparEletrica()
    {
        PreencherEletrica(null);
        Recalcular();
    }

    /// <summary>A linha embaixo do botão: de onde sai a tabela de pilares.</summary>
    private void MostrarOrigemDosVaos() =>
        _resumoDosVaos.Text = _vaosEscritos is null
            ? Tr.T("Vãos iguais pelo vão-alvo.")
            : Tr.F("{0} vãos escritos à mão; o vão-alvo não vale.", _vaosEscritos.Count);

    private void EscreverVaos()
    {
        // Manipulador de clique do WPF: exceção solta aqui fecha o Civil 3D
        // (a distribuição pelo vão-alvo lança acima de mil vãos).
        try
        {
            AbrirVaos();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir os vãos entre pilares.", erro);
            Avisar(Tr.F("Não consegui abrir os vãos: {0}", erro.Message));
        }
    }

    private void AbrirVaos()
    {
        var perfil = Ler(out var motivo);

        // Com vãos escritos que deixaram de fechar (mudou o número de
        // módulos), o perfil é recusado; a janela de vãos abre mesmo assim,
        // com a mesa lida sem os vãos, para o usuário consertar.
        if (perfil is null && _vaosEscritos is not null)
        {
            var guardados = _vaosEscritos;
            _vaosEscritos = null;
            perfil = Ler(out motivo);
            _vaosEscritos = guardados;

            if (perfil is not null)
                perfil = perfil with { Frame = perfil.Frame with { PillarSpans = guardados } };
        }

        if (perfil is null)
        {
            Avisar(Tr.F("Antes dos vãos, a mesa precisa fechar: {0}", motivo));
            return;
        }

        var janela = new JanelaDeVaos(perfil.Layout, perfil.Frame) { Owner = this };

        if (janela.ShowDialog() != true || !janela.Confirmou) return;

        _vaosEscritos = janela.Vaos;
        Recalcular();
    }

    private void NaoDeu(string motivo)
    {
        _planta.Dizer(Tr.T("A mesa ainda não fecha."));
        _corte.Dizer(Tr.T("A mesa ainda não fecha."));
        _resumo.Text = motivo;
        _usar.IsEnabled = false;
        _salvar.IsEnabled = false;
    }

    // ------------------------------------------------------------- ações

    private void Salvar()
    {
        var perfil = Ler(out var motivo);

        if (perfil is null)
        {
            Avisar(motivo);
            return;
        }

        try
        {
            // Perguntar antes de substituir: o nome é o que o projetista
            // reconhece, e dois perfis parecidos com nomes parecidos é o
            // normal. Sobrescrever calado apaga trabalho.
            if (_perfis.Exists(perfil.Name))
            {
                var resposta = MessageBox.Show(
                    this,
                    Tr.F("Já existe um perfil chamado \"{0}\". Substituir?", perfil.Name),
                    Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resposta != MessageBoxResult.Yes) return;
            }

            _perfis.Save(perfil);

            // O recado diz o que ESTÁ no disco, lido de volta, e não o que
            // se mandou gravar (05/10/2026: "aparece a mensagem se quero
            // substituir, eu falo que sim, mas não salva").
            var gravado = _perfis.Load(perfil.Name);
            Salvo = gravado;

            // A lista passa a mostrar o perfil gravado, em vez de voltar para
            // "(nenhum — mesa atual)" como se nada tivesse acontecido.
            _preenchendo = true;
            ListarSalvos(gravado.Name);
            _preenchendo = false;

            var recado = Tr.F("Perfil \"{0}\" gravado: {1}, {2:0.#} kWp.\nArquivo na pasta {3}.",
                gravado.Name,
                gravado.Layout.Describe(),
                gravado.Layout.ModuleCount * gravado.Layout.Module.PowerWatts / 1000,
                _perfis.Folder);

            if (_doDesenho && Resultado is not null)
                recado += "\n\n" + Tr.T("A mesa do desenho com este nome fica com estes números ao fechar esta janela. Para gravá-la no desenho, clique em \"Salvar no desenho\" nas Configurações.");

            Avisar(recado);
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar("Falha ao salvar o perfil de mesa.", erro);
            Avisar(Tr.F("Não consegui salvar: {0}", erro.Message));
        }
    }

    private void Confirmar()
    {
        var perfil = Ler(out var motivo);

        if (perfil is null)
        {
            Avisar(motivo);
            return;
        }

        Escolhida = perfil;
        DialogResult = true;
    }

    private void Avisar(string recado) =>
        MessageBox.Show(this, recado, Title, MessageBoxButton.OK, MessageBoxImage.Information);

    // ------------------------------------------------------------ números

    private static string Numero(double valor) => valor.ToString("0.####", Tr.Culture);

    /// <summary>
    /// Potência no campo: fica no formato brasileiro, porque o campo é lido
    /// por <see cref="NumberInput.TryParseLarge"/>, onde o ponto é milhar.
    /// </summary>
    private static string NumeroGrande(double valor) => valor.ToString("0.####", Brasil);

    private static void Selecionar(ComboBox caixa, object valor)
    {
        foreach (ComboBoxItem item in caixa.Items)
        {
            if (!Equals(item.Tag, valor)) continue;

            caixa.SelectedItem = item;
            return;
        }

        caixa.SelectedIndex = 0;
    }
}
