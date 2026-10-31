using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
///
/// Desde 30/10/2026 (38.5) são duas abas: "Perfil e módulo", onde também se
/// escolhe monoposte ou biposte, e a aba do tipo escolhido, com a mesa, a
/// estrutura e os desenhos (Renan: "uma guia ... para configuração do perfil,
/// módulo, e aí eu escolho se é mono ou biposte, e aí abre a aba certa para a
/// opção escolhida, e aí sim faço a configuração").
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
        ToolTip = Tr.T("Manda um módulo novo para a biblioteca de módulos, com o .PAN. No servidor do Clivus ele espera a equipe conferir antes de entrar na lista de todos; no serviço local (CLIVUS_SERVICO), entra na hora."),
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
        ToolTip = Tr.T("Lê o arquivo PAN do PVsyst: marca, modelo, medidas (Height, Width, Depth), Pmáx, Voc, Isc, Vmp, Imp e os coeficientes de temperatura. O módulo passa a ser o do PAN. É a fonte única do módulo para a parte elétrica."),
    };
    private readonly Button _limparEletrica = new()
    {
        Content = Tr.T("Limpar"),
        Height = 24,
        Margin = new Thickness(0, 2, 0, 2),
        ToolTip = Tr.T("Tira os dados elétricos do módulo desta estrutura: as tensões e correntes da rota ficam em branco."),
    };

    /// <summary>O texto e o nome do .PAN carregado nesta janela (38.6): vão junto no "Cadastrar módulo...". O perfil não guarda o arquivo.</summary>
    private (string Texto, string Nome)? _arquivoDoPan;

    /// <summary>
    /// O módulo do último PAN lido nesta janela, com a parte elétrica e o
    /// arquivo dele: escolher esse modelo na lista traz os dois de volta
    /// (revisão do 38.6).
    /// </summary>
    private (SolarModule Modulo, PanModule Eletrica, string Texto, string Nome)? _doPan;

    /// <summary>A entrada que o PAN pôs na lista de modelos, só nesta janela (a biblioteca não tinha o modelo).</summary>
    private SolarModule? _provisorio;

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

    // O enterro (38.12, Renan, 31/10/2026: "enterro nominal, para usar no
    // desenho, tendo o enterro mínimo e máximo"; "no biposte, o enterro mínimo
    // e máximo é POR PILAR"): o _enterro é o mínimo; no biposte, os três da
    // frente ficam na Estrutura e os de trás na seção do pórtico.
    private readonly TextBox _enterroNominal = Campo();
    private readonly TextBox _enterroMaximo = Campo();
    private readonly TextBox _enterroTrasMinimo = Campo();
    private readonly TextBox _enterroTrasNominal = Campo();
    private readonly TextBox _enterroTrasMaximo = Campo();
    private readonly TextBlock _rotuloEnterro = new();

    // Biposte (a bifila, 38.1): as seções dele só aparecem na aba do biposte.
    private bool _bifila;
    private readonly List<GroupBox> _secoesDoBiposte = [];

    // As abas (38.5): a primeira é do perfil e do módulo; a segunda muda de
    // título e de seções com o tipo escolhido nos cartões.
    private readonly TabControl _abas = new() { Margin = new Thickness(12, 12, 12, 0) };
    private readonly TabItem _abaDoModulo = new();
    private readonly TabItem _abaDaEstrutura = new();
    private readonly ToggleButton _cartaoMonoposte;
    private readonly ToggleButton _cartaoBiposte;
    private readonly TextBlock _rotuloT1 = new();
    private readonly TextBlock _rotuloT2 = new();

    /// <summary>Por que a mesa ainda não fecha, embaixo e fora das abas: o erro pode estar na aba que não está à vista.</summary>
    private readonly TextBlock _recado = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Firebrick,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 12, 0),
    };

    /// <summary>
    /// O que ficou na aba do outro tipo, por tipo (true: biposte): trocar de
    /// monoposte para biposte e voltar não perde nada do que foi digitado.
    /// </summary>
    private readonly Dictionary<bool, Rascunho> _rascunhos = new();

    /// <summary>Os textos da mesa e da estrutura de um tipo, como estavam nos campos.</summary>
    private sealed record Rascunho(IReadOnlyList<string> Textos, int Arranjo, IReadOnlyList<double>? Vaos);
    private readonly TextBox _bfEntrePilares = Campo();
    private readonly TextBox _bfTopo = Campo();
    private readonly TextBox _bfMaoFrente = Campo();
    private readonly TextBox _bfMaoFrenteAbaixo = Campo();
    private readonly TextBox _bfMaoTras = Campo();
    private readonly TextBox _bfMaoTrasAbaixo = Campo();
    private readonly TextBox _bfTercaBorda = Campo();
    private readonly TextBox _bfTercaBarra = Campo();
    private readonly ComboBox _bfTiranteLinha = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _bfTiranteVaos = Campo();
    private readonly TextBox _bfTiranteCima = Campo();
    private readonly TextBox _bfTiranteBaixo = Campo();
    private readonly TextBox _bfViga = Campo();
    private readonly TextBox _bfTerca = Campo();
    private readonly TextBox _bfMao = Campo();
    private readonly TextBox _bfTiranteDiametro = Campo();

    // P1 e P2 (38.9; Renan, 30/10/2026: "porque raios eu não consigo alterar
    // as alturas? A altura está no print, 3059 P1 e 3888 P2"): a altura é da
    // estrutura, digitada. Em branco os dois, ela vem da faixa do projeto.
    private readonly TextBox _bfP1 = Campo();
    private readonly TextBox _bfP2 = Campo();
    private readonly TextBlock _bfAlturaBasica = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };

    // A altura livre sob o módulo, na ponta baixa (38.9; Renan, 30/10/2026:
    // "não consigo chegar nesses 80cm"): o mesmo grau de liberdade do P1, dito
    // como o fabricante (a GC800 é a de 0,800). Digitar nela muda o P1; mudar
    // o P1 muda ela. O P2 fica como está.
    private readonly TextBox _bfLivre = Campo();

    /// <summary>O último perfil que fechou, para a conta da altura livre enquanto o usuário digita.</summary>
    private TableProfile? _ultimoPerfil;

    // Apagar o perfil escolhido da biblioteca (Renan, 30/10/2026: "quero
    // apagar as mesas da biblioteca, tem duas inúteis lá e não consigo
    // apagar"). O arquivo vai para a subpasta dos removidos, não some.
    private readonly Button _apagarSalvo = new()
    {
        Content = Tr.T("Apagar da biblioteca"),
        Height = 24,
        Padding = new Thickness(8, 0, 8, 0),
        HorizontalAlignment = HorizontalAlignment.Left,
        ToolTip = Tr.T("Tira o perfil escolhido em \"Perfil salvo\" da biblioteca da pasta do usuário: ele some desta lista e da lista das Configurações. O arquivo vai para a subpasta \"removidos\", de onde pode voltar à mão."),
    };

    /// <summary>A configuração do projeto: a faixa da ponta baixa, o enterro e para onde as mesas olham.</summary>
    private readonly SystemConfiguration _projeto;

    // O nome também na aba do tipo (38.9: "eu escolho um modelo e edito ela,
    // onde posso dar outro nome"), o mesmo da primeira aba.
    private readonly TextBox _nomeNaAba = Campo();

    // Para onde os módulos olham, embaixo da inclinação: é do projeto (38.9).
    private readonly TextBlock _olhaPara = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };

    // A aba Modelos (38.7; o nome de 30/10/2026): as estruturas prontas, que vão para a aba do biposte.
    private readonly TabItem _abaDosModelos = new();
    private readonly TextBlock _recadoDosModelos = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

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
    /// <param name="outroTipo">
    /// A mesa com que a aba do outro tipo começa (38.5): a última desse tipo
    /// no desenho. Null (ou do mesmo tipo da inicial): o biposte genérico, ou
    /// o exemplo na monoposte.
    /// </param>
    /// <param name="projeto">
    /// A configuração do projeto, para o P1 e o P2 na altura básica (38.7).
    /// Null: a padrão.
    /// </param>
    internal JanelaDeMesa(TableProfileStore perfis, TableProfile inicial, bool doDesenho = false, TableProfile? outroTipo = null, SystemConfiguration? projeto = null)
    {
        _perfis = perfis ?? throw new ArgumentNullException(nameof(perfis));
        _nomeAberto = (inicial ?? throw new ArgumentNullException(nameof(inicial))).Name;
        _doDesenho = doDesenho;
        _projeto = projeto ?? ProjectSettings.Default.Configuration;

        Title = Tr.T("Clivus Solar — Mesa");

        // Mais larga e mais alta (38.9): o biposte cabe em duas colunas e a
        // vista lateral, com as peças cotadas, fica legível.
        Width = 1400;
        Height = 900;
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

        foreach (var (linha, texto) in new[]
        {
            (BifilaRodLine.Rear, Tr.T("Na linha dos pilares traseiros")),
            (BifilaRodLine.Front, Tr.T("Na linha dos pilares dianteiros")),
            (BifilaRodLine.Both, Tr.T("Nas duas linhas")),
            (BifilaRodLine.None, Tr.T("Sem tirante")),
        })
        {
            _bfTiranteLinha.Items.Add(new ComboBoxItem { Content = texto, Tag = linha });
        }

        _salvar = new Button { Content = Tr.T("Salvar perfil"), Width = 110, Height = 26 };
        _salvar.Click += (_, _) => Protegido(Salvar, Tr.T("salvar o perfil"));

        _usar = new Button
        {
            Content = Tr.T("Usar esta mesa"),
            Width = 130,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
        };
        _usar.Click += (_, _) => Protegido(Confirmar, Tr.T("usar a mesa"));

        _cartaoMonoposte = Cartao(
            Tr.T("Monoposte"),
            Tr.T("Um pilar por pórtico, sob a tesoura: a mesa fixa de sempre."),
            bifila: false);
        _cartaoBiposte = Cartao(
            Tr.T("Biposte"),
            Tr.T("Dois pilares por pórtico (dianteiro e traseiro), viga e terças; mãos francesas e tirantes, se o seu tiver. Os prontos de fabricante estão na aba Modelos."),
            bifila: true);

        Content = Montar();

        // As seções do biposte viraram GroupBox no layout compacto: são elas
        // que aparecem e somem com o tipo da mesa.
        foreach (var campo in new Control[] { _bfP1, _bfTercaBorda, _bfTiranteVaos, _bfViga })
        {
            if (Caixa(campo) is { } caixa && !_secoesDoBiposte.Contains(caixa)) _secoesDoBiposte.Add(caixa);
        }

        _olhaPara.Text = Tr.F("{0}: é do projeto, em Configurações › Parâmetros (botões Norte e Sul).",
            Maiuscula(FacingFromLine.Describe(_projeto.FacingAzimuthDegrees)));

        ListarSalvos();

        // A aba do outro tipo começa na semente e fica guardada; a do tipo da
        // mesa aberta, na própria mesa.
        // O biposte genérico (38.7) vai pelo vão-alvo: abre com qualquer módulo
        // na primeira aba sem sair reprovado.
        var semente = outroTipo is { } outro && outro.IsBifila != inicial.IsBifila
            ? outro
            : inicial.IsBifila ? MesaCommands.MesaDeExemplo() : BifilaReference.Generic();
        PreencherEstrutura(semente);
        _rascunhos[semente.IsBifila] = Capturar();

        Preencher(inicial);

        _preenchendo = false;
        Recalcular();
    }

    /// <summary>O GroupBox do layout compacto onde o controle foi parar, ou null.</summary>
    private static GroupBox? Caixa(DependencyObject controle)
    {
        for (DependencyObject? d = controle; d is not null; d = LogicalTreeHelper.GetParent(d))
        {
            if (d is GroupBox caixa) return caixa;
        }

        return null;
    }

    // ------------------------------------------------------------- montagem

    private static TextBox Campo() => new() { Margin = new Thickness(0, 2, 0, 6), Height = 24 };

    /// <summary>Três campos lado a lado numa linha só (o mínimo, o nominal e o máximo do enterro, 38.12).</summary>
    private static ContentControl TresCampos(TextBox a, TextBox b, TextBox c)
    {
        var grade = new Grid();
        for (var i = 0; i < 3; i++) grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        foreach (var (campo, i) in new[] { (a, 0), (b, 1), (c, 2) })
        {
            campo.Margin = new Thickness(i == 0 ? 0 : 3, 2, i == 2 ? 0 : 3, 6);
            Grid.SetColumn(campo, i);
            grade.Children.Add(campo);
        }

        a.ToolTip = Tr.T("O mínimo que a estaca fica dentro do solo. Em branco, o mínimo do projeto.");
        b.ToolTip = Tr.T("O enterro nominal: o do desenho da estrutura no chão plano. Em branco, o mínimo.");
        c.ToolTip = Tr.T("O máximo que a estaca pode afundar. Em branco, o do projeto.");

        return new ContentControl { Content = grade, IsTabStop = false, Focusable = false };
    }

    /// <summary>A dica da linha do enterro.</summary>
    private static string DicaDoEnterro() =>
        Tr.T("O enterro da estaca: o mínimo, o nominal e o máximo. O nominal é o do desenho da estrutura no chão plano (no biposte, é com ele que o P1 dá a altura). Na usina, a estaca do catálogo (P1, P2) afunda o que o terreno pedir entre o mínimo e o máximo; se nem com o mínimo ela alcança, cresce com o mínimo enterrado, e o que fica acima da terra é consequência. Sem catálogo (a monoposte), a estaca sai com o nominal. Em branco: o mínimo e o máximo do projeto, e o nominal igual ao mínimo.");

    /// <summary>Um campo de número opcional: em branco é null; escrito e ilegível, o motivo.</summary>
    private static bool Opcional(TextBox campo, string nome, out double? valor, out string motivo)
    {
        valor = null;
        motivo = string.Empty;
        if (string.IsNullOrWhiteSpace(campo.Text)) return true;

        if (!NumberInput.TryParseMeasure(campo.Text, out var lido))
        {
            motivo = Tr.F("não consigo ler o número do campo \"{0}\".", nome);
            return false;
        }

        valor = lido;
        return true;
    }

    /// <summary>A frase com a primeira letra maiúscula.</summary>
    private static string Maiuscula(string frase) =>
        string.IsNullOrEmpty(frase) ? frase : char.ToUpper(frase[0], Tr.Culture) + frase[1..];

    private UIElement Montar()
    {
        // Seções lado a lado, rótulo ao lado do campo (02/10/2026: "otimizar
        // espaço e melhorar UX de tudo"): enchem a altura e passam para a
        // coluna seguinte, em vez de uma coluna estreita com barra de rolagem.
        _abaDoModulo.Header = Tr.T("Perfil e módulo");
        _abaDoModulo.ToolTip = Tr.T("O perfil, o módulo (medidas e dados elétricos) e o tipo de estrutura: monoposte ou biposte.");
        _abaDoModulo.Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(8),
            Content = LayoutCompacto.Secoes((Panel)FormularioDoModulo()),
        };

        _abaDaEstrutura.Content = AbaDaEstrutura();

        _abaDosModelos.Header = Tr.T("Modelos");
        _abaDosModelos.ToolTip = Tr.T("O biposte genérico e as estruturas prontas, uma seção por fabricante, com as medidas dos desenhos deles: a escolhida vai para a aba do biposte.");
        _abaDosModelos.Content = AbaDosModelos();

        _abas.Items.Add(_abaDoModulo);
        _abas.Items.Add(_abaDaEstrutura);
        _abas.Items.Add(_abaDosModelos);

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
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

        var rodape = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(botoes, Dock.Right);
        rodape.Children.Add(botoes);
        rodape.Children.Add(_recado);

        var raiz = new DockPanel();
        DockPanel.SetDock(rodape, Dock.Bottom);
        raiz.Children.Add(rodape);
        raiz.Children.Add(_abas);

        LigarEventos();

        return raiz;
    }

    /// <summary>A aba do tipo: os campos da mesa e da estrutura à esquerda, os desenhos à direita.</summary>
    private UIElement AbaDaEstrutura()
    {
        var grade = new Grid { Margin = new Thickness(8) };

        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var campos = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = LayoutCompacto.Secoes((Panel)FormularioDaEstrutura()),
        };

        Grid.SetColumn(campos, 0);
        grade.Children.Add(campos);

        var direita = new DockPanel();

        DockPanel.SetDock(_resumo, Dock.Bottom);
        direita.Children.Add(_resumo);

        // Os dois desenhos empilhados: a planta conta o comprimento e os
        // pilares, o corte conta a inclinação, a tesoura e onde o pilar
        // encosta. Um não substitui o outro — e é justamente o que a planta
        // não mostra que o Renan pediu para ver.
        var desenhos = new Grid();

        // A planta é comprida e baixa: cabe em menos altura. A vista lateral do
        // biposte, com os pilares e as cotas, precisa de mais (38.9).
        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.8, GridUnitType.Star) });
        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        desenhos.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.25, GridUnitType.Star) });

        var emCima = Emoldurar(Tr.T("PLANTA BAIXA"), _planta);
        var embaixo = Emoldurar(Tr.T("VISTA LATERAL — na direção da inclinação"), _corte);

        Grid.SetRow(emCima, 0);
        Grid.SetRow(embaixo, 2);

        desenhos.Children.Add(emCima);
        desenhos.Children.Add(embaixo);

        direita.Children.Add(desenhos);

        Grid.SetColumn(direita, 2);
        grade.Children.Add(direita);

        return grade;
    }

    /// <summary>
    /// A aba Modelos (38.7, Fabricantes até 30/10/2026): o biposte genérico,
    /// para montar o seu, e uma seção por fabricante com um cartão por
    /// estrutura pronta (as peças, a altura livre e os pilares de catálogo).
    /// "Usar" enche a aba do biposte, onde tudo se muda, o nome também (38.9:
    /// "eu escolho um modelo e edito ela, onde posso dar outro nome").
    /// "Apagar" tira o cartão da aba (Renan, 30/10/2026: "eu falei que quero
    /// APAGAR os fabricantes, cadê? E não chama fabricante, chama modelos a
    /// aba, e aí, quero seções POR fabricantes"); os prontos vêm com o
    /// plugin, então apagar é esconder, e "Mostrar os apagados" traz de volta.
    /// </summary>
    private UIElement AbaDosModelos()
    {
        var apagados = UserPreferences.Load(IdiomaDoPlugin.Arquivo).HiddenModelIds;
        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("O biposte genérico, para montar o seu, e as estruturas prontas, com as medidas dos desenhos do fabricante. \"Usar\" enche a aba Biposte (o pórtico com P1, MF1, P2 e MF2, a tesoura, as terças e os tirantes), e lá tudo se muda, o nome também. O módulo continua o da primeira aba."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6),
        });

        if (!apagados.Contains(VendorStructures.GenericModelId))
        {
            pilha.Children.Add(TituloDaSecao(Tr.T("Genérico")));
            pilha.Children.Add(new WrapPanel { Children = { CartaoDoGenerico() } });
        }

        foreach (var (fabricante, modelos) in VendorStructures.ByManufacturer(apagados))
        {
            pilha.Children.Add(TituloDaSecao(fabricante));

            var cartoes = new WrapPanel();
            foreach (var estrutura in modelos) cartoes.Children.Add(CartaoDoFabricante(estrutura));
            pilha.Children.Add(cartoes);
        }

        var quantosApagados = VendorStructures.HiddenCount(apagados);
        if (quantosApagados > 0)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            linha.Children.Add(new TextBlock
            {
                Text = Tr.F("{0} modelo(s) apagado(s) desta aba.", quantosApagados),
                FontSize = 11.5,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var mostrar = new Button
            {
                Content = Tr.T("Mostrar os apagados"),
                Height = 24,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = Tr.T("Traz de volta para esta aba os modelos que você apagou."),
            };
            mostrar.Click += (_, _) => Protegido(() => MudarApagados(_ => [], Tr.T("Os modelos apagados voltaram para a aba.")), Tr.T("mostrar os modelos apagados"));
            linha.Children.Add(mostrar);

            pilha.Children.Add(linha);
        }

        // O recado é um só: sai da aba antiga antes de entrar na nova (a aba é remontada ao apagar).
        (_recadoDosModelos.Parent as Panel)?.Children.Remove(_recadoDosModelos);
        pilha.Children.Add(_recadoDosModelos);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = pilha,
        };
    }

    /// <summary>O título de uma seção da aba Modelos: o fabricante, ou "Genérico".</summary>
    private static TextBlock TituloDaSecao(string titulo) => new()
    {
        Text = titulo,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 10, 0, 6),
    };

    /// <summary>
    /// Muda a lista dos modelos apagados nas preferências e remonta a aba.
    /// </summary>
    private void MudarApagados(Func<IReadOnlyList<string>, IReadOnlyList<string>> mudar, string recado)
    {
        var preferencias = UserPreferences.Load(IdiomaDoPlugin.Arquivo);
        (preferencias with { HiddenModels = mudar(preferencias.HiddenModelIds) }).Save(IdiomaDoPlugin.Arquivo);

        _abaDosModelos.Content = AbaDosModelos();
        _recadoDosModelos.Text = recado;
    }

    /// <summary>Apaga o modelo da aba (esconde; "Mostrar os apagados" traz de volta).</summary>
    private void ApagarModelo(string id, string nome) =>
        MudarApagados(
            apagados => [.. apagados.Where(a => a != id), id],
            Tr.F("{0} saiu da aba. \"Mostrar os apagados\", no fim da aba, traz de volta.", nome));

    /// <summary>O cartão de uma estrutura de fabricante: o desenho do pórtico, o nome, as peças, o catálogo e os botões.</summary>
    private Border CartaoDoFabricante(VendorStructure estrutura) => CartaoDeEstrutura(
        string.Empty,
        estrutura.Model,
        [estrutura.Describe(), estrutura.DescribeCatalog()],
        estrutura.DescribeSource(),
        Tr.T("Usar esta estrutura"),
        () => UsarDoFabricante(estrutura),
        estrutura.Name,
        () => ApagarModelo(estrutura.Name, estrutura.Name));

    /// <summary>
    /// O cartão do biposte genérico (38.9), o primeiro da aba: as peças do
    /// pórtico de referência, para mudar na aba Biposte.
    /// </summary>
    private Border CartaoDoGenerico()
    {
        var generico = BifilaReference.Generic();
        var b = generico.Frame.Bifila!;

        return CartaoDeEstrutura(
            Tr.T("Para montar o seu"),
            Tr.T("Biposte genérico"),
            [
                Tr.F("P1 de {0} m, MF1 de {1} m, P2 de {2} m, MF2 de {3} m, tesoura de {4} m e {5} m enterrados; terças nas bordas dos módulos, sem tirante.",
                    Numero(b.FrontPillar ?? 0), Numero(b.FrontBraceLength), Numero(b.RearPillar ?? 0), Numero(b.RearBraceLength),
                    Numero(generico.Frame.RafterLength), Numero(generico.Frame.MinEmbedment ?? 0)),
                Tr.T("Tudo se muda na aba Biposte: as peças, as medidas e o nome."),
            ],
            null,
            Tr.T("Usar este"),
            UsarGenerico,
            Tr.T("o biposte genérico"),
            () => ApagarModelo(VendorStructures.GenericModelId, Tr.T("O biposte genérico")));
    }

    /// <summary>Um cartão da aba Modelos: o desenho do pórtico, as linhas de texto e os botões Usar e Apagar.</summary>
    private Border CartaoDeEstrutura(string emCima, string titulo, IReadOnlyList<string> linhas, string? fonte, string botao, Action usar, string nomeParaOErro, Action apagar)
    {
        var figura = FiguraDoTipo(bifila: true);
        DockPanel.SetDock(figura, Dock.Left);

        var textos = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        // O fabricante já é o título da seção; a linha de cima fica para o genérico.
        if (emCima.Length > 0) textos.Children.Add(new TextBlock { Text = emCima, FontSize = 11, Foreground = Brushes.Gray });
        textos.Children.Add(new TextBlock { Text = titulo, FontSize = 13.5, FontWeight = FontWeights.SemiBold });
        foreach (var linha in linhas)
            textos.Children.Add(new TextBlock { Text = linha, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        if (fonte is not null)
            textos.Children.Add(new TextBlock { Text = fonte, FontSize = 10.5, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) });

        var botaoUsar = new Button
        {
            Content = botao,
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            ToolTip = Tr.T("Enche a aba do biposte com esta estrutura e abre a aba. O que estava nela é substituído; a monoposte fica guardada."),
        };

        botaoUsar.Click += (_, _) =>
        {
            // Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.
            try
            {
                usar();
            }
            catch (Exception erro)
            {
                _preenchendo = false;
                RegistroDeDiagnostico.Registrar("Falha ao usar a estrutura da aba Modelos.", erro);
                Avisar(Tr.F("Não consegui usar a estrutura {0}: {1}", nomeParaOErro, erro.Message));
            }
        };

        var botaoApagar = new Button
        {
            Content = Tr.T("Apagar"),
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = Tr.T("Tira este modelo da aba. A mesa que já usa as medidas dele não muda; \"Mostrar os apagados\", no fim da aba, traz de volta."),
        };
        botaoApagar.Click += (_, _) => Protegido(apagar, Tr.F("apagar {0}", nomeParaOErro));

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        botoes.Children.Add(botaoUsar);
        botoes.Children.Add(botaoApagar);
        textos.Children.Add(botoes);

        var conteudo = new DockPanel { Margin = new Thickness(10) };
        conteudo.Children.Add(figura);
        conteudo.Children.Add(textos);

        return new Border
        {
            Child = conteudo,
            Width = 470,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 12, 12),
        };
    }

    /// <summary>
    /// Uma estrutura de fabricante (38.7) na aba do biposte, como se o cartão
    /// do biposte tivesse sido clicado: a monoposte fica guardada. O nome não
    /// muda sozinho (a mesa do desenho é achada pelo nome; revisão do 38.9):
    /// muda-se no campo Nome da aba. O módulo também não, e os vãos escritos do
    /// fabricante só ficam com a largura do módulo dele, senão a mesa vai pelo
    /// vão-alvo, como a semente do 38.5.
    /// </summary>
    private void UsarDoFabricante(VendorStructure estrutura)
    {
        var perfil = estrutura.Profile;
        var mesmaLargura = NumberInput.TryParseMeasure(_largura.Text, out var largura)
            && Math.Abs(largura - perfil.Layout.Module.Width) < 1e-6;

        if (!mesmaLargura) perfil = perfil with { Frame = perfil.Frame with { PillarSpans = null } };

        UsarNoBiposte(perfil, nome: null);

        _recadoDosModelos.Text = mesmaLargura
            ? Tr.F("A {0} está na aba Biposte; o nome continua \"{1}\" (mude no campo Nome, no alto da aba).", estrutura.Name, _nome.Text)
            : Tr.F("A {0} está na aba Biposte, com o vão-alvo: os vãos dela são para o módulo de {1} m de largura. O nome continua \"{2}\" (mude no campo Nome).", estrutura.Name, Numero(estrutura.Profile.Layout.Module.Width), _nome.Text);
    }

    /// <summary>O biposte genérico (38.9) na aba do biposte; o nome fica o que estava.</summary>
    private void UsarGenerico()
    {
        UsarNoBiposte(BifilaReference.Generic(), nome: null);
        _recadoDosModelos.Text = Tr.T("O biposte genérico está na aba Biposte: mude lá as peças, as medidas e o nome.");
    }

    /// <summary>A estrutura na aba do biposte, que abre; a monoposte fica guardada.</summary>
    /// <param name="nome">O nome que a mesa passa a ter, ou null para ficar com o atual.</param>
    private void UsarNoBiposte(TableProfile perfil, string? nome)
    {
        if (!_bifila) _rascunhos[false] = Capturar();

        _preenchendo = true;
        try
        {
            PreencherEstrutura(perfil);
            if (nome is not null) _nome.Text = nome;
        }
        finally
        {
            _preenchendo = false;
        }

        Recalcular();
        _abas.SelectedItem = _abaDaEstrutura;
    }

    /// <summary>
    /// O cartão de um tipo na primeira aba (38.5): o nome, uma frase e o
    /// desenho do pórtico. Clicar escolhe o tipo e abre a aba dele.
    /// </summary>
    private ToggleButton Cartao(string titulo, string texto, bool bifila)
    {
        var figura = FiguraDoTipo(bifila);
        DockPanel.SetDock(figura, Dock.Left);

        var textos = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        textos.Children.Add(new TextBlock { Text = titulo, FontSize = 13.5, FontWeight = FontWeights.SemiBold });
        textos.Children.Add(new TextBlock { Text = texto, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });

        var conteudo = new DockPanel { Margin = new Thickness(4) };
        conteudo.Children.Add(figura);
        conteudo.Children.Add(textos);

        var cartao = new ToggleButton
        {
            Content = conteudo,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 3, 0, 3),
            ToolTip = Tr.T("Escolhe o tipo e abre a aba dele, com a mesa e a estrutura."),
        };

        cartao.Click += (_, _) =>
        {
            // Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.
            try
            {
                Escolher(bifila);
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao trocar o tipo da mesa.", erro);
                Avisar(Tr.F("Não consegui trocar o tipo da mesa: {0}", erro.Message));
            }
        };

        return cartao;
    }

    /// <summary>
    /// O pórtico de cada tipo em traço simples: chão, pilares, a viga (ou a
    /// tesoura) e os módulos por cima; no biposte, também as mãos francesas.
    /// </summary>
    private static Canvas FiguraDoTipo(bool bifila)
    {
        var figura = new Canvas { Width = 92, Height = 62 };

        void Traco(double x1, double y1, double x2, double y2, Brush cor, double espessura) =>
            figura.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = cor,
                StrokeThickness = espessura,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });

        // A viga sobe da esquerda (ponta baixa) para a direita; os módulos vão
        // 4 px acima dela, paralelos.
        const double chao = 58;
        double Viga(double x) => 36 - (x - 10) * 0.24;

        Traco(2, chao, 90, chao, Brushes.SaddleBrown, 1.5);
        Traco(8, Viga(8) - 4, 86, Viga(86) - 4, Brushes.SteelBlue, 4.5);

        if (bifila)
        {
            Traco(26, chao, 26, Viga(26), Brushes.DimGray, 3);
            Traco(68, chao, 68, Viga(68), Brushes.DimGray, 3);
            Traco(12, Viga(12), 82, Viga(82), Brushes.DimGray, 2);
            Traco(26, Viga(26) + 13, 38, Viga(38), Brushes.Gray, 1.5);
            Traco(68, Viga(68) + 13, 56, Viga(56), Brushes.Gray, 1.5);
        }
        else
        {
            Traco(48, chao, 48, Viga(48), Brushes.DimGray, 3);
            Traco(22, Viga(22), 76, Viga(76), Brushes.DimGray, 2);
        }

        return figura;
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

    /// <summary>O título de seção que o <see cref="LayoutCompacto"/> reconhece.</summary>
    private static void Secao(Panel pilha, string titulo) => pilha.Children.Add(new TextBlock
    {
        Text = titulo.ToUpperInvariant(),
        FontSize = 10.5,
        Foreground = Brushes.Gray,
        Margin = new Thickness(0, 10, 0, 2),
    });

    /// <param name="texto">O rótulo já criado, quando a janela muda o texto dele depois (T1 e T2 do biposte).</param>
    private static void Linha(Panel pilha, string rotulo, Control campo, string? dica = null, TextBlock? texto = null)
    {
        texto ??= new TextBlock();
        texto.Text = rotulo;
        texto.FontSize = 12;
        texto.ToolTip = dica;
        pilha.Children.Add(texto);
        if (dica is not null) campo.ToolTip = dica;
        pilha.Children.Add(campo);
    }

    /// <summary>A primeira aba: perfil, módulo, dados elétricos e o tipo de estrutura.</summary>
    private UIElement FormularioDoModulo()
    {
        var pilha = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

        void Secao(string titulo) => JanelaDeMesa.Secao(pilha, titulo);
        void Linha(string rotulo, Control campo, string? dica = null) => JanelaDeMesa.Linha(pilha, rotulo, campo, dica);

        Secao(Tr.T("Perfil"));
        Linha(Tr.T("Perfil salvo"), _salvos);
        pilha.Children.Add(_apagarSalvo);
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

        // 38.5: o tipo é escolhido aqui, e a escolha abre a aba dele.
        Secao(Tr.T("Tipo de estrutura"));
        pilha.Children.Add(_cartaoMonoposte);
        pilha.Children.Add(_cartaoBiposte);
        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("A escolha abre a aba do tipo, com a mesa e a estrutura dele. O resto do Clivus é o mesmo para os dois: strings, rotas, valas, cabos e quantitativos."),
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
        });

        return pilha;
    }

    /// <summary>
    /// A aba do tipo: a mesa e a estrutura; no biposte, também o pórtico, as
    /// mãos francesas, as terças, os tirantes e as seções das peças.
    /// </summary>
    private UIElement FormularioDaEstrutura()
    {
        var pilha = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

        void Secao(string titulo) => JanelaDeMesa.Secao(pilha, titulo);
        void Linha(string rotulo, Control campo, string? dica = null, TextBlock? texto = null) => JanelaDeMesa.Linha(pilha, rotulo, campo, dica, texto);

        Secao(Tr.T("Mesa"));
        Linha(Tr.T("Nome"), _nomeNaAba, Tr.T("O nome da estrutura, o mesmo da primeira aba: é com ele que ela aparece na lista das Configurações e na Escolha das estruturas."));
        Linha(Tr.T("Módulos"), _quantidade);
        Linha(Tr.T("Arranjo"), _arranjo);
        Linha(Tr.T("Espaçamento entre módulos (m)"), _espacamentoH);
        Linha(Tr.T("Espaçamento entre fileiras (m)"), _espacamentoV);
        Linha(Tr.T("Sobra da esquerda (m)"), _sobraEsquerda);
        Linha(Tr.T("Sobra da direita (m)"), _sobraDireita);
        Linha(Tr.T("Inclinação (graus)"), _inclinacao);
        pilha.Children.Add(_olhaPara);

        // T1 e T2 mudam de nome no biposte (MostrarTipo): viga, e pilar dianteiro.
        Secao(Tr.T("Estrutura"));
        Linha(Tr.T("Tesoura T1 (m)"), _tesoura, Tr.T("Comprimento da tesoura, ao longo da inclinação."), _rotuloT1);
        Linha(Tr.T("Pilar na tesoura T2 (m)"), _pilarNaTesoura, Tr.T("Onde o pilar encosta na tesoura, medido da ponta baixa dela."), _rotuloT2);
        Linha(Tr.T("Pilar: largura ao longo da fileira (m)"), _pilarLargura, Tr.T("Lado da seção do pilar no sentido da fileira (comprimento da mesa)."));
        Linha(Tr.T("Pilar: largura na inclinação (m)"), _pilarProfundidade, Tr.T("Lado da seção do pilar no sentido da inclinação (tesoura)."));
        Linha(Tr.T("Enterro mín. · nominal · máx. (m)"), TresCampos(_enterro, _enterroNominal, _enterroMaximo), DicaDoEnterro(), _rotuloEnterro);
        Linha(Tr.T("Vão-alvo entre pilares (m)"), _vaoAlvo, Tr.T("Vão pretendido; a tabela divide a mesa em vãos iguais perto dele. Não vale quando há vãos escritos."));
        pilha.Children.Add(_botaoDosVaos);
        pilha.Children.Add(_resumoDosVaos);
        Linha(Tr.T("Balanço nas pontas (m)"), _balanco, Tr.T("Quanto de estrutura sobra para fora do primeiro e do último pilar."));

        // O biposte (38.1): dois pilares por pórtico. O T2 de cima é o do
        // dianteiro; o traseiro sai da distância entre os dois na horizontal,
        // que é como o fornecedor cota. Cada parte tem a sua seção (38.5).
        // O pórtico peça por peça (38.9; Renan, 30/10/2026: "é PILAR P1, Mão
        // francesa MF1, pilar P2, Mão francesa MF2, aí tem as terças, as
        // tesouras"), com o P1 e o P2 digitados: a altura é da estrutura.
        Secao(Tr.T("Pórtico: pilares e mãos francesas"));
        Linha(Tr.T("P1: pilar dianteiro, o menor (m)"), _bfP1, Tr.T("O pilar dianteiro inteiro, com o enterro T3, como o fabricante cota no chão plano (ex.: 3,059). É a altura da estrutura: na usina, a faixa da ponta baixa começa na que ele dá, e cada pilar sai com o comprimento que o terreno pede. Em branco, com o P2: a altura vem da faixa do projeto."));
        Linha(Tr.T("Livre sob o módulo (m)"), _bfLivre, Tr.T("A altura livre do chão plano ao fundo do módulo, na ponta baixa, como o fabricante dá o nome à estrutura (a GC800 é a de 0,800). É o mesmo que o P1: digitar aqui muda o P1, e mudar o P1 muda aqui. O P2 fica como está."));
        Linha(Tr.T("MF1: mão francesa do P1 (m)"), _bfMaoFrente, Tr.T("O comprimento da mão francesa do pilar dianteiro (ex.: 1,244). Zero: sem mão francesa."));
        Linha(Tr.T("MF1: abaixo do topo do P1 (m)"), _bfMaoFrenteAbaixo, Tr.T("Onde a MF1 prende no P1, medido do topo do pilar para baixo (ex.: 0,447). A outra ponta vai na tesoura, onde o comprimento alcança. A vista lateral mostra a cota."));
        Linha(Tr.T("P2: pilar traseiro, o maior (m)"), _bfP2, Tr.T("O pilar traseiro inteiro, como o fabricante cota (ex.: 3,888). No chão plano ele precisa do P1 mais a subida da tesoura entre os dois; o que passar disso é a folga para o chão caindo para trás."));
        Linha(Tr.T("P2: enterro mín. · nominal · máx. (m)"), TresCampos(_enterroTrasMinimo, _enterroTrasNominal, _enterroTrasMaximo),
            Tr.T("O enterro do pilar de trás, que pode ser outro que o da frente. Na usina a estaca é a do P2 e o enterro é o que o terreno der, entre o mínimo e o máximo; se nem com o mínimo ela alcança, ela cresce com o mínimo enterrado; se afundaria além do máximo, é cortada. O nominal é o do chão plano. Em branco, os da frente."));
        Linha(Tr.T("MF2: mão francesa do P2 (m)"), _bfMaoTras, Tr.T("O comprimento da mão francesa do pilar traseiro (ex.: 1,209). Zero: sem mão francesa."));
        Linha(Tr.T("MF2: abaixo do topo do P2 (m)"), _bfMaoTrasAbaixo, Tr.T("Onde a MF2 prende no P2, medido do topo do pilar para baixo (ex.: 0,773)."));
        Linha(Tr.T("Entre P1 e P2, na horizontal (m)"), _bfEntrePilares, Tr.T("Distância entre os eixos do P1 e do P2, em planta (ex.: 3,00). O T2 da seção Estrutura é onde o P1 encosta na tesoura."));
        Linha(Tr.T("Topo dos pilares sob os módulos (m)"), _bfTopo, Tr.T("Na vertical, da face de cima dos módulos ao topo dos pilares: módulo, terça e tesoura (ex.: 0,126)."));
        pilha.Children.Add(_bfAlturaBasica);

        Secao(Tr.T("Terças"));
        Linha(Tr.T("A partir da borda do módulo (m)"), _bfTercaBorda, Tr.T("Duas terças por fileira de módulos, cada uma a esta distância de uma borda (ex.: 0,492)."));
        Linha(Tr.T("Barra máxima (m)"), _bfTercaBarra, Tr.T("A terça da mesa é cortada em barras de até este comprimento; cada corte é uma emenda na lista."));

        Secao(Tr.T("Tirantes"));
        Linha(Tr.T("Em que linha de pilares"), _bfTiranteLinha);
        Linha(Tr.T("Vãos com tirante"), _bfTiranteVaos, Tr.T("Os vãos travados com o X, contados de 1 a partir do começo da mesa, separados por ponto e vírgula (ex.: 2; 4)."));
        Linha(Tr.T("Ponto de cima, abaixo do topo (m)"), _bfTiranteCima);
        Linha(Tr.T("Ponto de baixo, abaixo do topo (m)"), _bfTiranteBaixo);
        Linha(Tr.T("Diâmetro (m)"), _bfTiranteDiametro);

        Secao(Tr.T("Seções das peças"));
        Linha(Tr.T("Viga: largura × altura (m)"), _bfViga, Tr.T("Seção da viga, para o 3D. Ex.: 0,05 × 0,08."));
        Linha(Tr.T("Terça: largura × altura (m)"), _bfTerca, Tr.T("Seção da terça, para o 3D. Ex.: 0,05 × 0,10."));
        Linha(Tr.T("Mão francesa: largura × altura (m)"), _bfMao, Tr.T("Seção da mão francesa, para o 3D. Ex.: 0,04 × 0,06."));

        return pilha;
    }

    /// <summary>Os eventos dos campos das duas abas, ligados uma vez, depois de as duas existirem.</summary>
    private void LigarEventos()
    {
        foreach (var (campo, _) in Todos()) campo.TextChanged += (_, _) => Recalcular();

        _arranjo.SelectionChanged += (_, _) => Recalcular();
        _modelo.SelectionChanged += (_, _) => Protegido(TrocarModulo, Tr.T("trocar o módulo"));
        _salvos.SelectionChanged += (_, _) => CarregarSalvo();
        foreach (var campo in new[] { _enterro, _enterroNominal, _enterroMaximo, _enterroTrasMinimo, _enterroTrasNominal, _enterroTrasMaximo })
            campo.TextChanged += (_, _) => Recalcular();
        _botaoDosVaos.Click += (_, _) => EscreverVaos();
        _cadastrarModulo.Click += (_, _) => CadastrarModulo();
        _carregarPan.Click += (_, _) => CarregarPan();
        _limparEletrica.Click += (_, _) => Protegido(LimparEletrica, Tr.T("limpar os dados elétricos"));

        foreach (var campo in CamposEletricos()) campo.TextChanged += (_, _) => Recalcular();
        foreach (var (campo, _) in CamposDaBifila()) campo.TextChanged += (_, _) => Recalcular();
        _bfP1.TextChanged += (_, _) => Recalcular();
        _bfP2.TextChanged += (_, _) => Recalcular();
        _bfLivre.TextChanged += (_, _) => Protegido(LivreMudou, Tr.T("mudar a altura livre"));
        _apagarSalvo.Click += (_, _) => Protegido(ApagarSalvo, Tr.T("apagar o perfil da biblioteca"));
        _bfTiranteVaos.TextChanged += (_, _) => Recalcular();

        // O nome é um só, nas duas abas (38.9).
        _nome.TextChanged += (_, _) =>
        {
            if (_nomeNaAba.Text != _nome.Text) _nomeNaAba.Text = _nome.Text;
        };
        _nomeNaAba.TextChanged += (_, _) =>
        {
            if (_nome.Text != _nomeNaAba.Text) _nome.Text = _nomeNaAba.Text;
        };
        foreach (var secao in new[] { _bfViga, _bfTerca, _bfMao }) secao.TextChanged += (_, _) => Recalcular();
        _bfTiranteLinha.SelectionChanged += (_, _) => Recalcular();
    }

    /// <summary>
    /// Um manipulador de evento que não deixa exceção sair: solta num evento
    /// do WPF, ela fecha o Civil 3D (revisão do 38.7, para os que ainda não
    /// tinham o try dentro).
    /// </summary>
    /// <param name="oQue">O que o usuário tentou fazer, para o recado ("salvar o perfil").</param>
    private void Protegido(Action acao, string oQue)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha na janela de Mesa ao {oQue}.", erro);
            Avisar(Tr.F("Não consegui {0}: {1}", oQue, erro.Message));
        }
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
        yield return (_pilarNaTesoura, _bifila ? Tr.T("P1 na tesoura T2") : Tr.T("Pilar na tesoura T2"));
        yield return (_pilarLargura, Tr.T("Pilar: largura ao longo da fileira"));
        yield return (_pilarProfundidade, Tr.T("Pilar: largura na inclinação"));
        yield return (_vaoAlvo, Tr.T("Vão-alvo entre pilares"));
        yield return (_balanco, Tr.T("Balanço nas pontas"));
    }

    /// <summary>Os campos de número da bifila e o nome de cada um, para a mensagem de erro (o P1 e o P2, que podem ficar em branco, à parte).</summary>
    private IEnumerable<(TextBox Campo, string Nome)> CamposDaBifila()
    {
        yield return (_bfEntrePilares, Tr.T("Entre P1 e P2, na horizontal"));
        yield return (_bfTopo, Tr.T("Topo dos pilares sob os módulos"));
        yield return (_bfMaoFrente, Tr.T("MF1: mão francesa do P1"));
        yield return (_bfMaoFrenteAbaixo, Tr.T("MF1: abaixo do topo do P1"));
        yield return (_bfMaoTras, Tr.T("MF2: mão francesa do P2"));
        yield return (_bfMaoTrasAbaixo, Tr.T("MF2: abaixo do topo do P2"));
        yield return (_bfTercaBorda, Tr.T("Terça a partir da borda do módulo"));
        yield return (_bfTercaBarra, Tr.T("Barra máxima da terça"));
        yield return (_bfTiranteCima, Tr.T("Tirante: ponto de cima"));
        yield return (_bfTiranteBaixo, Tr.T("Tirante: ponto de baixo"));
        yield return (_bfTiranteDiametro, Tr.T("Tirante: diâmetro"));
    }

    /// <summary>
    /// Enche as seções do biposte e deixa a aba com a cara do tipo do perfil.
    /// Na monoposte, os campos do biposte ficam como estavam: são a aba do
    /// outro tipo, guardada.
    /// </summary>
    private void PreencherBifila(TableProfile perfil)
    {
        _bifila = perfil.Frame.Bifila is not null;
        MostrarTipo();

        if (perfil.Frame.Bifila is not { } b) return;

        // Ao milímetro: a volta pela tesoura dava 2,9997 no lugar dos 3,000.
        _bfEntrePilares.Text = Numero(Math.Round(perfil.BifilaHorizontalSpacing ?? 0, 3));
        _bfTopo.Text = Numero(b.PillarTopDrop);
        _bfP1.Text = b.FrontPillar is { } p1 ? Numero(p1) : string.Empty;
        _bfP2.Text = b.RearPillar is { } p2 ? Numero(p2) : string.Empty;
        _enterroTrasMinimo.Text = b.RearMinEmbedment is { } minimoTras ? Numero(minimoTras) : string.Empty;
        _enterroTrasNominal.Text = b.RearNominalEmbedment is { } nominalTras ? Numero(nominalTras) : string.Empty;
        _enterroTrasMaximo.Text = b.RearMaxEmbedment is { } maximoTras ? Numero(maximoTras) : string.Empty;
        PreencherMaos(b);
        _bfTercaBorda.Text = Numero(b.PurlinFromModuleEdge);
        _bfTercaBarra.Text = Numero(b.PurlinMaxBar);
        Selecionar(_bfTiranteLinha, b.RodLine);

        // "Sem tirante" vem com a lista vazia; os vãos que o usuário deixou
        // escritos no campo ficam lá (LerBifila só não os manda para a mesa).
        if (b.RodLine != BifilaRodLine.None || b.RodSpans.Count > 0)
            _bfTiranteVaos.Text = string.Join("; ", b.RodSpans.Select(v => v.ToString(Tr.Culture)));
        _bfTiranteCima.Text = Numero(b.RodUpperBelowTop);
        _bfTiranteBaixo.Text = Numero(b.RodLowerBelowTop);
        _bfViga.Text = Secao(b.Beam);
        _bfTerca.Text = Secao(b.Purlin);
        _bfMao.Text = Secao(b.Brace);
        _bfTiranteDiametro.Text = Numero(b.RodDiameter);
    }

    /// <summary>As mãos francesas de um biposte.</summary>
    private void PreencherMaos(BifilaFrame b)
    {
        _bfMaoFrente.Text = Numero(b.FrontBraceLength);
        _bfMaoFrenteAbaixo.Text = Numero(b.FrontBraceBelowTop);
        _bfMaoTras.Text = Numero(b.RearBraceLength);
        _bfMaoTrasAbaixo.Text = Numero(b.RearBraceBelowTop);
    }

    private static string Secao(BarSection secao) => $"{Numero(secao.Width)} × {Numero(secao.Depth)}";

    /// <summary>"0,05 × 0,08" (ou com x, X ou *) em largura e altura.</summary>
    private static bool LerSecao(string texto, out BarSection secao)
    {
        secao = new BarSection(0, 0);
        var partes = texto.Split(['×', 'x', 'X', '*'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length != 2
            || !NumberInput.TryParseMeasure(partes[0], out var largura)
            || !NumberInput.TryParseMeasure(partes[1], out var altura))
        {
            return false;
        }

        secao = new BarSection(largura, altura);
        return true;
    }

    /// <summary>
    /// A parte bifila que os campos descrevem, ou false com o motivo. O
    /// pilar traseiro na tesoura sai do T2 do dianteiro e da distância na
    /// horizontal, com a inclinação da mesa.
    /// </summary>
    private bool LerBifila(double t2, double tiltRadians, out BifilaFrame? bifila, out string motivo)
    {
        bifila = null;

        foreach (var (campo, nome) in CamposDaBifila())
        {
            if (NumberInput.TryParseMeasure(campo.Text, out _)) continue;

            motivo = string.IsNullOrWhiteSpace(campo.Text)
                ? Tr.F("o campo \"{0}\" está em branco.", nome)
                : Tr.F("não consigo ler o número do campo \"{0}\".", nome);
            return false;
        }

        double Valor(TextBox campo)
        {
            NumberInput.TryParseMeasure(campo.Text, out var v);
            return v;
        }

        var vaos = new List<int>();
        foreach (var parte in _bfTiranteVaos.Text.Split([';', ',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!NumberInput.TryParseCount(parte, out var vao))
            {
                motivo = Tr.F("não consigo ler o vão \"{0}\" do campo \"{1}\": escreva os números separados por ponto e vírgula.", parte, Tr.T("Vãos com tirante"));
                return false;
            }

            vaos.Add(vao);
        }

        foreach (var (campo, nome) in new[]
        {
            (_bfViga, Tr.T("Viga: largura × altura")),
            (_bfTerca, Tr.T("Terça: largura × altura")),
            (_bfMao, Tr.T("Mão francesa: largura × altura")),
        })
        {
            if (LerSecao(campo.Text, out _)) continue;

            motivo = Tr.F("o campo \"{0}\" precisa de duas medidas, como 0,05 × 0,08.", nome);
            return false;
        }

        LerSecao(_bfViga.Text, out var viga);
        LerSecao(_bfTerca.Text, out var terca);
        LerSecao(_bfMao.Text, out var mao);

        // "Sem tirante" é linha nenhuma e lista vazia: os vãos escritos no
        // campo ficam lá, mas não vão para a mesa.
        var linhaDoTirante = (_bfTiranteLinha.SelectedItem as ComboBoxItem)?.Tag as BifilaRodLine? ?? BifilaRodLine.None;

        if (!(tiltRadians >= 0 && tiltRadians < Math.PI / 2))
        {
            motivo = Tr.T("o biposte precisa de inclinação entre 0 e 90°.");
            return false;
        }

        // O P1 e o P2 (38.9): os dois em branco é a altura da faixa do
        // projeto; um só escrito não fecha.
        double? p1 = null;
        double? p2 = null;

        if (!string.IsNullOrWhiteSpace(_bfP1.Text) || !string.IsNullOrWhiteSpace(_bfP2.Text))
        {
            foreach (var (campo, nome) in new[] { (_bfP1, Tr.T("P1: pilar dianteiro")), (_bfP2, Tr.T("P2: pilar traseiro")) })
            {
                if (NumberInput.TryParseMeasure(campo.Text, out _)) continue;

                motivo = string.IsNullOrWhiteSpace(campo.Text)
                    ? Tr.F("o campo \"{0}\" está em branco: o P1 e o P2 andam juntos (os dois em branco, a altura vem da faixa do projeto).", nome)
                    : Tr.F("não consigo ler o número do campo \"{0}\".", nome);
                return false;
            }

            p1 = Valor(_bfP1);
            p2 = Valor(_bfP2);
        }

        bifila = new BifilaFrame(
            BifilaFrame.RearAlongRafter(t2, Valor(_bfEntrePilares), tiltRadians),
            Valor(_bfTopo),
            Valor(_bfMaoFrente),
            Valor(_bfMaoFrenteAbaixo),
            Valor(_bfMaoTras),
            Valor(_bfMaoTrasAbaixo),
            Valor(_bfTercaBorda),
            Valor(_bfTercaBarra),
            linhaDoTirante,
            linhaDoTirante == BifilaRodLine.None ? [] : vaos,
            Valor(_bfTiranteCima),
            Valor(_bfTiranteBaixo),
            viga,
            terca,
            mao,
            Valor(_bfTiranteDiametro))
        {
            FrontPillar = p1,
            RearPillar = p2,
        };

        motivo = string.Empty;
        return true;
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

            // Perfil do outro tipo: o que estava na aba atual fica guardado,
            // como se o usuário tivesse trocado de cartão.
            if (perfil.IsBifila != _bifila) _rascunhos[_bifila] = Capturar();

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

    /// <summary>
    /// O perfil escolhido em "Perfil salvo" sai da biblioteca (Renan,
    /// 30/10/2026: "quero apagar as mesas da biblioteca"): o arquivo vai para a
    /// subpasta dos removidos. A mesa que está nos campos fica como está.
    /// </summary>
    private void ApagarSalvo()
    {
        if ((_salvos.SelectedItem as ComboBoxItem)?.Tag is not string nome)
        {
            Avisar(Tr.T("Escolha em \"Perfil salvo\" o perfil que sai da biblioteca."));
            return;
        }

        if (!_perfis.MoveToRemoved(nome))
        {
            Avisar(Tr.F("O perfil \"{0}\" já não está na biblioteca.", nome));
            ListarSalvos();
            return;
        }

        _preenchendo = true;
        try
        {
            ListarSalvos();
        }
        finally
        {
            _preenchendo = false;
        }

        _recado.Text = Tr.F("O perfil \"{0}\" saiu da biblioteca (o arquivo foi para a subpasta \"{1}\"). A mesa nos campos continua como está.", nome, TableProfileStore.PastaDosRemovidos);
    }

    /// <summary>
    /// A altura livre digitada vira o P1 (38.9): o P1 que dá essa altura no
    /// chão plano, com o T3, a inclinação e o módulo da mesa. Só quando é o
    /// usuário que digita nela; quando a janela a enche pelo P1, nada.
    /// </summary>
    private void LivreMudou()
    {
        if (_preenchendo || !_bfLivre.IsKeyboardFocusWithin) return;
        if (_ultimoPerfil is not { Frame.Bifila: not null } perfil || (perfil.Frame.NominalEmbedment ?? perfil.Frame.MinEmbedment) is not { } t3) return;
        if (!NumberInput.TryParseMeasure(_bfLivre.Text, out var livre) || livre <= 0) return;

        var geometria = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);
        var pontaBaixa = livre + perfil.Layout.Module.Thickness * Math.Cos(perfil.TiltRadians);
        var p1 = StructureHeight.FrontPillarFor(geometria, perfil.TiltRadians, pontaBaixa, t3);

        // Sem P2 ainda (o biposte de antes do 38.9), o do chão plano: o P1
        // mais a subida da tesoura entre os dois pilares.
        if (string.IsNullOrWhiteSpace(_bfP2.Text) && geometria.RearPillarRow is { } tras)
            _bfP2.Text = Numero(Math.Round(p1 + (tras - geometria.PillarRow) * Math.Sin(perfil.TiltRadians), 3));

        _bfP1.Text = Numero(Math.Round(p1, 3));
    }

    private void Preencher(TableProfile perfil)
    {
        _marcaDoPerfil = perfil.Layout.Module.Brand;
        _modeloDoPerfil = perfil.Layout.Module.Model;

        _nome.Text = perfil.Name;
        _arquivoDoPan = null;

        // Um biposte carregado substitui a aba do biposte, e com ela o recado
        // do fabricante; uma monoposte deixa a aba do biposte como estava.
        if (perfil.IsBifila) _recadoDosModelos.Text = string.Empty;

        _altura.Text = Numero(perfil.Layout.Module.Height);
        _largura.Text = Numero(perfil.Layout.Module.Width);
        _espessura.Text = Numero(perfil.Layout.Module.Thickness);
        _potencia.Text = NumeroGrande(perfil.Layout.Module.PowerWatts);

        PreencherEstrutura(perfil);
        PreencherEletrica(perfil.ModuleElectrical);

        var daBiblioteca = ModuleLibrary.Find(_modulos, perfil.Layout.Module.Model);

        _modelo.SelectedIndex = daBiblioteca is null
            ? _modelo.Items.Count - 1
            : _modulos.ToList().IndexOf(daBiblioteca);
    }

    /// <summary>A mesa e a estrutura do perfil, que é a aba do tipo dele; o módulo fica como está.</summary>
    private void PreencherEstrutura(TableProfile perfil)
    {
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
        _enterroNominal.Text = perfil.Frame.NominalEmbedment is { } nominal ? Numero(nominal) : string.Empty;
        _enterroMaximo.Text = perfil.Frame.MaxEmbedment is { } maximo ? Numero(maximo) : string.Empty;
        _vaosEscritos = perfil.Frame.PillarSpans;

        Selecionar(_arranjo, perfil.Layout.Arrangement);
        PreencherBifila(perfil);
    }

    // ------------------------------------------------- o tipo da mesa (38.5)

    /// <summary>
    /// Os campos que as duas abas de tipo dividem: a mesa e a estrutura comum.
    /// Os do biposte só existem na aba dele e não entram aqui.
    /// </summary>
    private IEnumerable<TextBox> CamposDaAba() =>
    [
        _quantidade, _espacamentoH, _espacamentoV, _sobraEsquerda, _sobraDireita, _inclinacao,
        _tesoura, _pilarNaTesoura, _pilarLargura, _pilarProfundidade, _enterro, _enterroNominal, _enterroMaximo, _vaoAlvo, _balanco,
    ];

    private Rascunho Capturar() => new([.. CamposDaAba().Select(c => c.Text)], _arranjo.SelectedIndex, _vaosEscritos);

    private void Restaurar(Rascunho rascunho)
    {
        foreach (var (campo, texto) in CamposDaAba().Zip(rascunho.Textos)) campo.Text = texto;

        _arranjo.SelectedIndex = rascunho.Arranjo;
        _vaosEscritos = rascunho.Vaos;
    }

    /// <summary>
    /// O cartão de um tipo: guarda a aba do tipo atual, põe nos campos a do
    /// tipo escolhido (como ficou da última vez, ou a semente) e abre a aba.
    /// </summary>
    private void Escolher(bool bifila)
    {
        if (bifila != _bifila)
        {
            var guardado = Capturar();

            _preenchendo = true;
            try
            {
                if (_rascunhos.TryGetValue(bifila, out var rascunho)) Restaurar(rascunho);
                _rascunhos[_bifila] = guardado;
                _bifila = bifila;
            }
            finally
            {
                _preenchendo = false;
            }
        }

        MostrarTipo();
        Recalcular();
        _abas.SelectedItem = _abaDaEstrutura;
    }

    /// <summary>A aba da estrutura com a cara do tipo: título, T1 e T2, as seções do biposte e o cartão marcado.</summary>
    private void MostrarTipo()
    {
        _abaDaEstrutura.Header = _bifila ? Tr.T("Biposte") : Tr.T("Monoposte");
        _abaDaEstrutura.ToolTip = _bifila
            ? Tr.T("A mesa e a estrutura do biposte: dois pilares por pórtico, viga, mãos francesas, terças e tirantes.")
            : Tr.T("A mesa e a estrutura da monoposte: um pilar por pórtico, sob a tesoura.");

        foreach (var secao in _secoesDoBiposte) secao.Visibility = _bifila ? Visibility.Visible : Visibility.Collapsed;

        // A tesoura é a viga do pórtico; o Renan chama de tesoura (38.9).
        _rotuloT1.Text = Tr.T("Tesoura T1 (m)");
        _rotuloT1.ToolTip = _tesoura.ToolTip = _bifila
            ? Tr.T("Comprimento da tesoura (a viga do pórtico), ao longo da inclinação (ex.: 4,030).")
            : Tr.T("Comprimento da tesoura, ao longo da inclinação.");

        _rotuloT2.Text = _bifila ? Tr.T("P1 na tesoura T2 (m)") : Tr.T("Pilar na tesoura T2 (m)");
        _rotuloT2.ToolTip = _pilarNaTesoura.ToolTip = _bifila
            ? Tr.T("Onde o pilar dianteiro encosta na viga, medido da ponta baixa dela (ex.: 0,498). O traseiro sai da distância entre os pilares, na seção Pórtico.")
            : Tr.T("Onde o pilar encosta na tesoura, medido da ponta baixa dela.");

        // No biposte, a linha do enterro da Estrutura é a do P1; a do P2 fica no pórtico (38.12).
        _rotuloEnterro.Text = _bifila ? Tr.T("P1: enterro mín. · nominal · máx. (m)") : Tr.T("Enterro mín. · nominal · máx. (m)");

        _cartaoMonoposte.IsChecked = !_bifila;
        _cartaoBiposte.IsChecked = _bifila;
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

        // O enterro é opcional, campo a campo (38.12): em branco, o mínimo é o
        // do projeto, o nominal é o mínimo e o máximo é o do projeto.
        if (!Opcional(_enterro, Tr.T("Enterro mínimo"), out var enterro, out motivo)) return null;
        if (!Opcional(_enterroNominal, Tr.T("Enterro nominal"), out var enterroNominal, out motivo)) return null;
        if (!Opcional(_enterroMaximo, Tr.T("Enterro máximo"), out var enterroMaximo, out motivo)) return null;

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

        BifilaFrame? bifila = null;

        if (_bifila && !LerBifila(t2, graus * Math.PI / 180, out bifila, out var porQueBifila))
        {
            motivo = porQueBifila;
            return null;
        }

        if (bifila is not null)
        {
            if (!Opcional(_enterroTrasMinimo, Tr.T("P2: enterro mínimo"), out var minimoTras, out motivo)) return null;
            if (!Opcional(_enterroTrasNominal, Tr.T("P2: enterro nominal"), out var nominalTras, out motivo)) return null;
            if (!Opcional(_enterroTrasMaximo, Tr.T("P2: enterro máximo"), out var maximoTras, out motivo)) return null;

            bifila = bifila with { RearMinEmbedment = minimoTras, RearNominalEmbedment = nominalTras, RearMaxEmbedment = maximoTras };
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
                NominalEmbedment = enterroNominal,
                MaxEmbedment = enterroMaximo,
                Bifila = bifila,
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

        // A parte elétrica acompanha o módulo escolhido (38.6, e a revisão):
        // a do PAN lido nesta janela, a que o serviço tem, ou nenhuma; nunca
        // a de outro módulo. Valores à mão, sem PAN, ficam.
        if (_doPan is { } doPan && MesmoModelo(doPan.Modulo.Model, modulo.Model))
        {
            PreencherEletrica(doPan.Eletrica);
            _arquivoDoPan = (doPan.Texto, doPan.Nome);
        }
        else if (FonteDeModulos.Eletrica.TryGetValue(modulo.Model, out var eletrica))
        {
            PreencherEletrica(eletrica);
            _arquivoDoPan = null;
        }
        else if (_panLido is { } lido && !MesmoModelo(lido.Model, modulo.Model))
        {
            PreencherEletrica(null);
            _arquivoDoPan = null;
        }

        Recalcular();
    }

    private static bool MesmoModelo(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

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

            _ultimoPerfil = perfil;
            var basica = AlturaBasica(perfil);

            _planta.Mostrar(geometria);
            _corte.Mostrar(geometria, perfil.TiltRadians, perfil.Frame.NominalEmbedment ?? perfil.Frame.MinEmbedment, perfil.Frame.Bifila, basica);

            var potencia = perfil.Layout.ModuleCount * perfil.Layout.Module.PowerWatts / 1000;

            // A subida do pilar é o número que o projetista não consegue
            // estimar de cabeça, e é o que decide se o pilar dele vai caber.
            var subida = PillarSizing.FreeHeight(0, geometria.PillarRow, perfil.TiltRadians);

            _resumo.Text =
                Tr.F("Comprimento {0} m · {1} m na inclinação · {2} colunas · {3:0.#} kWp",
                    Numero(perfil.Layout.Length), Numero(perfil.Layout.Depth), perfil.Layout.Columns, potencia)
                + "\n"
                + (perfil.IsBifila
                    ? (_vaosEscritos is null
                        ? Tr.F("{0} pórticos de dois pilares em {1} vãos de {2} m", pilares.PillarCount, pilares.Spans.Count, Numero(pilares.Spans[0]))
                        : Tr.F("{0} pórticos de dois pilares em {1} vãos escritos ({2} m)", pilares.PillarCount, pilares.Spans.Count, string.Join(" + ", pilares.Spans.Select(Numero))))
                    : _vaosEscritos is null
                        ? Tr.F("{0} pilares em {1} vãos de {2} m", pilares.PillarCount, pilares.Spans.Count, Numero(pilares.Spans[0]))
                        : Tr.F("{0} pilares em {1} vãos escritos ({2} m)", pilares.PillarCount, pilares.Spans.Count, string.Join(" + ", pilares.Spans.Select(Numero))))
                + (pilares.Cantilever > 0
                    ? Tr.F(", com balanço de {0} m em cada ponta", Numero(pilares.Cantilever))
                    : Tr.T(", com o pilar na ponta da estrutura"))
                + "\n"
                + (geometria.RearPillarRow is { } tras && basica?.P2 is { } p2
                    ? Tr.F("P1 a {0} m da ponta baixa do módulo e P2 a {1} m ({2} m entre eles na horizontal). P1 de {3} m e P2 de {4} m, com {5} m enterrados.",
                        Numero(geometria.PillarRow), Numero(tras), Numero(perfil.BifilaHorizontalSpacing ?? 0),
                        Milimetro(basica.P1), Milimetro(perfil.Frame.Bifila?.RearPillar ?? p2), Numero(basica.Embedment))
                    : geometria.RearPillarRow is { } traseiro
                    ? Tr.F("Pilar dianteiro a {0} m da ponta baixa do módulo e traseiro a {1} m ({2} m entre eles na horizontal).",
                        Numero(geometria.PillarRow), Numero(traseiro), Numero(perfil.BifilaHorizontalSpacing ?? 0))
                    : Tr.F("O pilar encosta na mesa a {0} m da ponta baixa do módulo (sobra da tesoura {1} m), e sobe {2} m acima dela.",
                        Numero(geometria.PillarRow), Numero(geometria.RafterOffset), Numero(subida)))
                + (ModuleSource.ProfileDivergence(perfil) is { } divergencia ? "\n" + Tr.F("ATENÇÃO: {0}", divergencia) : string.Empty);

            _usar.IsEnabled = true;
            _salvar.IsEnabled = true;
            _recado.Text = string.Empty;
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
        {
            // O mandado ao servidor do Clivus nesta sessão espera a gestão (38.8).
            var texto = FonteDeModulos.EhPendente(modulo.Model)
                ? Tr.F("{0} (esperando a validação)", modulo.Describe())
                : modulo.Describe();

            _modelo.Items.Add(new ComboBoxItem { Content = texto, Tag = modulo });
        }

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
            // Sem endereço, sem HTTPS ou sem a chave (nem a licença, etapa 43),
            // o cadastro não iria: a janela diz antes de o usuário encher o
            // diálogo (revisão do 38.8).
            if (FonteDeModulos.PorQueNaoCadastra is { } porQue)
            {
                Avisar(porQue);
                return;
            }

            NumberInput.TryParseLarge(_potencia.Text, out var potencia);
            NumberInput.TryParseMeasure(_altura.Text, out var altura);
            NumberInput.TryParseMeasure(_largura.Text, out var largura);
            NumberInput.TryParseMeasure(_espessura.Text, out var espessura);

            // Com o .PAN carregado nesta janela, o cadastro parte dele, e o arquivo vai junto (38.6).
            var (marca, modelo) = _panLido is not null && _arquivoDoPan is not null ? Modulo() : (string.Empty, string.Empty);
            var pan = _panLido is { } lido && _arquivoDoPan is { } arquivo ? (lido, arquivo.Texto, arquivo.Nome) : ((PanModule, string, string)?)null;

            var janela = new JanelaDeCadastroDeModulo(new SolarModule(marca, modelo == "(à mão)" ? string.Empty : modelo, potencia, altura, largura, espessura), pan)
            {
                Owner = this,
            };

            if (janela.ShowDialog() != true || janela.Cadastrado is not { } novo) return;

            // A lista volta (a guardada, mais os pendentes desta sessão: o
            // mandado ao servidor do Clivus está nela, esperando a gestão),
            // com o módulo novo escolhido. A entrada de um .PAN que não foi
            // mandado continua na janela (revisão do 38.8).
            _preenchendo = true;
            (_modulos, _origemDosModulos.Text) = FonteDeModulos.Carregar();

            if (_provisorio is { } provisorio && ModuleLibrary.Find(_modulos, provisorio.Model) is null)
                _modulos = [.. _modulos, provisorio];
            else
                _provisorio = null;

            if (janela.PanEnviado is { } enviado) _doPan = (novo, enviado.Eletrica, enviado.Texto, enviado.Nome);
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

                // O que o servidor disse: o pendente, o "já está lá", ou o
                // serviço antigo que não guardou o .PAN (revisão do 38.8).
                Avisar(janela.Mensagem);
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
    /// "Carregar .PAN..." (item 15; 38.6): lê o PAN e põe na seção Módulo o
    /// módulo inteiro que ele traz — marca, modelo, medidas e potência — e a
    /// parte elétrica (Renan, 30/10/2026: "na hora de puxar o pan, já poderia
    /// configurar o modelo, marca, altura, tudo"). O módulo que a biblioteca
    /// não tem entra na lista desta janela, e o Clivus oferece cadastrá-lo no
    /// serviço, com o arquivo.
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

            var texto = PanModule.ReadText(caminho);
            var medidas = PanModule.DimensionsOf(texto);
            var modulo = medidas?.ModuleOf(pan);
            if (modulo is { IsValid: false }) modulo = null;

            NumberInput.TryParseLarge(_potencia.Text, out var antes);

            _preenchendo = true;
            try
            {
                // A entrada que o PAN anterior pôs na lista sai: um PAN do
                // mesmo modelo, lido de novo, a troca (revisão do 38.6).
                if (_provisorio is { } velho)
                {
                    _modulos = [.. _modulos.Where(m => !ReferenceEquals(m, velho))];
                    _provisorio = null;
                    ListarModulos();
                }
            }
            finally
            {
                _preenchendo = false;
            }

            var daBiblioteca = modulo is null ? null : ModuleLibrary.Find(_modulos, modulo.Model);

            _preenchendo = true;
            try
            {
                if (modulo is not null)
                {
                    // O módulo do PAN na lista: o da biblioteca com o mesmo
                    // modelo, ou ele mesmo, só nesta janela.
                    if (daBiblioteca is null)
                    {
                        _modulos = [.. _modulos, modulo];
                        _provisorio = modulo;
                        ListarModulos();
                    }

                    _modelo.SelectedIndex = _modulos.ToList().IndexOf(daBiblioteca ?? modulo);
                    _marcaDoPerfil = modulo.Brand;
                    _modeloDoPerfil = modulo.Model;
                    _altura.Text = Numero(modulo.Height);
                    _largura.Text = Numero(modulo.Width);
                    _espessura.Text = Numero(modulo.Thickness);
                }

                _potencia.Text = NumeroGrande(pan.Pmax);
                PreencherEletrica(pan);
                _arquivoDoPan = (texto, System.IO.Path.GetFileName(caminho));
                _doPan = modulo is null ? null : (modulo, pan, texto, System.IO.Path.GetFileName(caminho));
            }
            finally
            {
                _preenchendo = false;
            }

            Recalcular();

            var recado = Tr.F("PAN lido: {0}.", pan.Describe())
                + "\n\n"
                + (modulo is not null
                    ? Tr.F("Módulo: {0}, {1} × {2} × {3} m.", modulo.DisplayName, Numero(modulo.Height), Numero(modulo.Width), Numero(modulo.Thickness))
                    : medidas is null
                        ? Tr.T("O PAN não traz as medidas do módulo (Height, Width, Depth): altura, largura e espessura ficaram como estavam.")
                        : Tr.F("As medidas do PAN não servem ({0} × {1} × {2}; o PVsyst grava em metro): altura, largura e espessura ficaram como estavam.", Numero(medidas.Height), Numero(medidas.Width), Numero(medidas.Thickness)));
            if (Math.Abs(antes - pan.Pmax) > ModuleSource.Tolerance)
                recado += "\n\n" + Tr.F("A potência do módulo passou de {0:0.#} para {1:0.#} Wp. As mesas já desenhadas continuam como estavam: depois de gravar a estrutura no desenho, use \"Atualizar a potência das mesas desenhadas\" nas Configurações.", antes, pan.Pmax);

            // Sem a chave nem a licença (ou sem servidor), o módulo novo não
            // pode ir para a biblioteca de todos: o recado diz que ele vale só
            // nesta janela, e a janela nem pergunta (revisão do 38.8).
            if (modulo is not null && daBiblioteca is null && FonteDeModulos.PorQueNaoCadastra is { } porQue)
                recado += "\n\n" + Tr.F("O módulo vale só nesta janela. {0}", porQue);

            Avisar(recado);

            // O usuário subindo o PAN para a biblioteca de todos (38.6).
            if (modulo is not null && daBiblioteca is null)
                OferecerCadastro(modulo, pan, texto, System.IO.Path.GetFileName(caminho));
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar("Falha ao carregar o PAN na janela de Mesa.", erro);
            Avisar(Tr.F("Não consegui carregar o PAN: {0}", erro.Message));
        }
    }

    /// <summary>
    /// O módulo do PAN que a biblioteca não tem: com o serviço no ar, pergunta
    /// se vai para lá, com o arquivo; fora do ar, diz como fazer depois.
    /// </summary>
    private void OferecerCadastro(SolarModule modulo, PanModule pan, string texto, string nome)
    {
        // Sem a chave nem a licença, o servidor do Clivus recusaria (38.8):
        // nem pergunta; o recado do .PAN já disse que o módulo vale só nesta janela.
        if (FonteDeModulos.PorQueNaoCadastra is not null) return;

        if (!FonteDeModulos.NoAr)
        {
            Avisar(Tr.F("O módulo {0} não está na biblioteca. Com o serviço de módulos no ar, \"Cadastrar módulo...\" o leva para lá, com o PAN.", modulo.DisplayName));
            return;
        }

        var resposta = MessageBox.Show(
            this,
            Tr.F("O módulo {0} não está na biblioteca do serviço. Cadastrar agora, com o PAN, para ele aparecer na lista de todos?", modulo.DisplayName),
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (resposta != MessageBoxResult.Yes) return;

        // O envio pode levar uns segundos (o .PAN subindo): o cursor diz.
        (bool Cadastrou, bool Pendente, string Mensagem) resultado;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            resultado = FonteDeModulos.Cadastrar(modulo, pan, texto, nome);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        Avisar(resultado.Mensagem);
        if (!resultado.Cadastrou) return;

        // A lista volta, com o módulo novo escolhido: do serviço local, ou,
        // pendente no servidor do Clivus, dos pendentes desta sessão (38.8).
        _preenchendo = true;
        try
        {
            (_modulos, _origemDosModulos.Text) = FonteDeModulos.Carregar();
            var achado = ModuleLibrary.Find(_modulos, modulo.Model);

            // A lista não recarregou do serviço (ou ainda não tem o módulo):
            // ele volta a ser a entrada desta janela, escolhida, e o usuário sabe.
            if (achado is null)
            {
                _modulos = [.. _modulos, modulo];
                _provisorio = modulo;
            }
            else
            {
                _provisorio = null;
            }

            ListarModulos();
            _modelo.SelectedIndex = _modulos.ToList().IndexOf(achado ?? modulo);

            if (achado is null)
                Avisar(Tr.F("O módulo {0} foi cadastrado, mas a lista não recarregou do serviço; ele fica na lista desta janela.", modulo.DisplayName));
        }
        finally
        {
            _preenchendo = false;
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

    /// <summary>
    /// Os pilares no chão plano e o recado embaixo do pórtico. Com o P1 e o
    /// P2 (38.9): a ponta baixa que o P1 dá, a faixa da usina e a folga do P2.
    /// Sem eles: o P1 e o P2 da faixa do projeto (38.7). Null na monoposte, ou
    /// se a conta não fechar: aí o recado diz por quê e o resto da janela segue.
    /// </summary>
    private BasicPillars? AlturaBasica(TableProfile perfil)
    {
        if (perfil.Frame.Bifila is not { } b) return null;

        try
        {
            var basica = perfil.PillarsAtBasicHeight(_projeto);

            // A altura livre acompanha o P1, menos quando é ela que está sendo
            // digitada (o P1 é que acompanha ela).
            if (!_bfLivre.IsKeyboardFocusWithin)
                _bfLivre.Text = Milimetro(basica.LowEdge - perfil.Layout.Module.Thickness * Math.Cos(perfil.TiltRadians));

            if (b.FrontPillar is { } p1 && b.RearPillar is { } p2 && basica.P2 is { } p2NoPlano)
            {
                var geometria = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);
                var naUsina = _projeto.ForStructure(geometria, perfil.TiltRadians);
                var livre = basica.LowEdge - perfil.Layout.Module.Thickness * Math.Cos(perfil.TiltRadians);

                // O recado curto embaixo do pórtico (o formulário cabe em duas
                // colunas e a vista lateral fica larga); a conta inteira na dica.
                // O P1 é o pilar do chão plano: no terreno, o motor segura a
                // ponta baixa na borda do módulo, e o dianteiro sai menor que
                // o P1 com o chão subindo para trás (revisão do 38.9).
                var texto = Tr.F(
                    "No chão plano, o P1 de {0} m com {1} m enterrados põe a ponta baixa a {2} m do chão ({3} m livres sob o módulo). Na usina, a ponta baixa vai de {4} a {5} m, e cada pilar sai com o comprimento que o terreno pede: com o chão subindo para trás, o dianteiro sai menor que o P1; caindo, maior.",
                    Milimetro(p1), Numero(basica.Embedment), Milimetro(basica.LowEdge), Milimetro(livre), Milimetro(naUsina.MinLowEdge), Milimetro(naUsina.MaxLowEdge));

                var folga = p2 - p2NoPlano > 0.0005 && StructureHeight.RearSlopeAllowance(geometria, perfil.TiltRadians) is { } declive
                    ? (Tr.F("No chão plano o P2 precisa de {0} m; os {1} m a mais são a folga do catálogo para o chão caindo {2} % para trás, medido do P1 ao P2. Na usina, cada pilar sai com o comprimento que o terreno pede.", Milimetro(p2NoPlano), Milimetro(p2 - p2NoPlano), Numero(Math.Round(declive * 100, 1))),
                        Tr.F("P2: folga do catálogo, {0} % de chão caindo do P1 ao P2.", Numero(Math.Round(declive * 100, 1))))
                    : (Tr.T("O P2 é o do chão plano, sem folga para o chão caindo para trás."), Tr.T("P2: o do chão plano, sem folga."));

                // Uma linha só: o formulário cabe em duas colunas e a vista
                // lateral fica larga; a folga do P2 está na dica e no desenho.
                _bfAlturaBasica.Text = Tr.F("Na usina, a ponta baixa vai de {0} a {1} m.", Milimetro(naUsina.MinLowEdge), Milimetro(naUsina.MaxLowEdge));
                _bfAlturaBasica.ToolTip = texto + " " + folga.Item1 + " " + folga.Item2;
            }
            else
            {
                var enterro = perfil.Frame.MinEmbedment is null && perfil.Frame.NominalEmbedment is null
                    ? Tr.F("{0} m enterrados, o mínimo do projeto", Numero(basica.Embedment))
                    : Tr.F("{0} m enterrados, o nominal", Numero(basica.Embedment));

                _bfAlturaBasica.Text = Tr.F(
                    "P1 e P2 em branco: a altura vem da faixa do projeto. No chão plano, com a ponta baixa em {0} m (a mínima do projeto, Configurações › Parâmetros) e {1}, o P1 dá {2} m e o P2 {3} m.",
                    Numero(basica.LowEdge), enterro, Milimetro(basica.P1), Milimetro(basica.P2 ?? 0));
                _bfAlturaBasica.ToolTip = null;
            }

            return basica;
        }
        catch (Exception erro) when (erro is ArgumentException or InvalidOperationException)
        {
            RegistroDeDiagnostico.Registrar("Não consegui a altura dos pilares.", erro);
            _bfAlturaBasica.Text = Tr.F("Sem a altura dos pilares: {0}", erro.Message);
            return null;
        }
    }

    private void NaoDeu(string motivo)
    {
        _bfAlturaBasica.Text = string.Empty;
        _planta.Dizer(Tr.T("A mesa ainda não fecha."));
        _corte.Dizer(Tr.T("A mesa ainda não fecha."));
        _resumo.Text = motivo;
        _recado.Text = Tr.F("A mesa ainda não fecha: {0}", motivo);
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

    /// <summary>Ao milímetro, sempre com as três casas: é como o fabricante cota o pilar (3,059).</summary>
    private static string Milimetro(double valor) => valor.ToString("0.000", Tr.Culture);

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
