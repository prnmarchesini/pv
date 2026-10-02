using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A tela única de configuração: os limites do sistema de um lado, as regras
/// de análise do outro, e o que se grava vai para o desenho.
///
/// A janela não sabe o que é centímetro nem grau. Cada caixa de texto está
/// ligada a um campo de <see cref="ProjectSettingsForm"/>, no Core, que é
/// quem converte para metro e radiano e diz qual campo está errado. A
/// conversão de unidade é a coisa mais fácil de errar sem ninguém notar
/// (/100 no lugar de /10 não se denuncia sozinho), e por isso ela mora onde
/// há teste de nível 1, e não aqui.
///
/// A janela confere a cada tecla, como a da mesa, e pelo mesmo motivo: o erro
/// que ela pega é de digitação, e a mensagem que nomeia o campo precisa
/// aparecer enquanto o campo ainda está na frente do projetista. Todo método
/// chamado por evento do WPF (<see cref="Conferir"/>, <see cref="Confirmar"/>,
/// <see cref="RestaurarPadrao"/>) é inteiro dentro de um try, porque exceção
/// num evento do WPF fecha o Civil 3D, não a janela.
///
/// Montada em código, como as outras duas janelas do plugin.
/// </summary>
internal sealed class JanelaDeConfiguracao : Window
{
    /// <summary>
    /// A paleta de seleção de cor. O padrão do Renan (vermelho abaixo, azul
    /// acima) está nela; o resto são cores que se distinguem umas das outras
    /// em cima de um terreno cinza.
    /// </summary>
    private static readonly (string Nome, RgbColor Cor)[] Paleta =
    [
        ("Vermelho", RgbColor.Red),
        ("Azul", RgbColor.Blue),
        ("Magenta", RgbColor.Magenta),
        ("Amarelo", new RgbColor(255, 255, 0)),
        ("Laranja", new RgbColor(255, 128, 0)),
        ("Verde", new RgbColor(0, 160, 0)),
        ("Ciano", new RgbColor(0, 200, 200)),
        ("Roxo", new RgbColor(128, 0, 200)),
        ("Rosa", new RgbColor(255, 105, 180)),
        ("Marrom", new RgbColor(150, 90, 40)),
        ("Branco", new RgbColor(255, 255, 255)),
        ("Preto", new RgbColor(0, 0, 0)),
    ];

    // ---- sistema
    private readonly TextBox _azimute = Campo();
    private readonly TextBox _pitch = Campo();
    private readonly TextBox _pontaBaixaMin = Campo();
    private readonly TextBox _pontaBaixaMax = Campo();
    private readonly TextBox _enterroMin = Campo();
    private readonly TextBox _enterroMax = Campo();
    private readonly TextBox _degrauMin = Campo();
    private readonly TextBox _degrauMax = Campo();
    private readonly TextBox _lombo = Campo();
    private readonly TextBox _espacamentoMesas = Campo();
    private readonly CheckBox _temDeclividade = new()
    {
        Content = "Limitar a declividade longitudinal",
        Margin = new Thickness(0, 6, 0, 2),
    };
    private readonly TextBox _declividadeMax = Campo();
    private readonly TextBox _espacamento = Campo();

    // ---- análises
    private sealed class LinhaDeAnalise
    {
        public required CheckBox Ligada { get; init; }
        public required TextBox Camada { get; init; }
        public required ComboBox? CorAbaixo { get; init; }
        public required ComboBox CorAcima { get; init; }
    }

    private readonly Dictionary<AnalysisKind, LinhaDeAnalise> _analises = new();
    private readonly CheckBox _pintarPilar = new()
    {
        Content = "Pintar pilar mais comprido que (m)",
        Margin = new Thickness(0, 6, 0, 2),
    };
    private readonly TextBox _pilarAcimaDe = Campo();

    private readonly TextBlock _resumo = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0),
        FontSize = 12.5,
        MinHeight = 40,
    };

    private readonly Button _salvar;

    /// <summary>
    /// O formulário com que a janela abriu (ou o padrão, depois de restaurar).
    /// As cores "abaixo" das análises sem mínimo não aparecem na tela e são
    /// devolvidas daqui, para a tela não trocá-las em silêncio ao salvar.
    /// </summary>
    private ProjectSettingsForm _base;

    private bool _preenchendo = true;

    /// <summary>A configuração que o usuário mandou gravar, ou null se fechou sem salvar.</summary>
    internal ProjectSettings? Escolhida { get; private set; }

    /// <summary>
    /// Só os parâmetros que as análises e o motor usam para decidir o que
    /// estoura (botão Parâmetros da seção Análises, 27/09/2026): faixa da
    /// ponta baixa, lombo, degraus, declividade e as cores. O resto
    /// (azimute, pitch, espaçamentos, enterro) fica como está no desenho.
    /// </summary>
    private readonly bool _soAnalises;

    /// <summary>
    /// Embutida na aba Parâmetros da janela de Configurações (8.7): sem os
    /// botões próprios; quem salva é a janela de fora, por <see cref="Validar"/>.
    /// </summary>
    private readonly bool _embutida;

    internal JanelaDeConfiguracao(ProjectSettings inicial, string? aviso, bool soAnalises = false, bool embutida = false)
    {
        ArgumentNullException.ThrowIfNull(inicial);

        _base = ProjectSettingsForm.From(inicial);
        _soAnalises = soAnalises;
        _embutida = embutida;

        Title = soAnalises ? "UFV — Parâmetros das análises" : "UFV — Configuração do projeto";
        Width = 980;
        Height = 700;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _salvar = new Button
        {
            Content = "Salvar no desenho",
            Width = 150,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
        };
        _salvar.Click += (_, _) => Confirmar();

        Content = Montar();

        Preencher(_base);

        _preenchendo = false;
        Conferir();

        if (aviso is not null) _resumo.Text = aviso;
    }

    // ------------------------------------------------------------- montagem

    private static TextBox Campo() => new() { Margin = new Thickness(0, 2, 0, 6), Height = 24 };

    private UIElement Montar()
    {
        var grade = new Grid { Margin = new Thickness(12) };

        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grade.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Embutida na aba Parâmetros (02/10/2026: "para que tanto espaço em
        // branco e uma barra de rolagem?"): as seções lado a lado, rótulo ao
        // lado do campo, ocupando a largura toda.
        var sistema = _embutida
            ? new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = LayoutCompacto.Secoes((Panel)FormularioDoSistema(), 400),
            }
            : new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = FormularioDoSistema(),
            };

        Grid.SetColumn(sistema, 0);
        Grid.SetRow(sistema, 0);
        if (_embutida) Grid.SetColumnSpan(sistema, 3);
        grade.Children.Add(sistema);

        var analises = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = FormularioDasAnalises(),
        };

        Grid.SetColumn(analises, 2);
        Grid.SetRow(analises, 0);

        // Embutida na aba Parâmetros (8.7), a grade de cores das análises
        // sai: desde o 8.9 cada análise tem a regra dela, na janela dela.
        if (!_embutida) grade.Children.Add(analises);

        Grid.SetColumn(_resumo, 0);
        Grid.SetColumnSpan(_resumo, 3);
        Grid.SetRow(_resumo, 1);
        grade.Children.Add(_resumo);

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };

        var padrao = new Button { Content = "Restaurar padrão", Width = 130, Height = 26 };
        padrao.Click += (_, _) => RestaurarPadrao();

        botoes.Children.Add(padrao);
        botoes.Children.Add(_salvar);
        botoes.Children.Add(new Button
        {
            Content = "Fechar",
            Width = 90,
            Height = 26,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
        });

        Grid.SetColumn(botoes, 0);
        Grid.SetColumnSpan(botoes, 3);
        Grid.SetRow(botoes, 2);
        if (!_embutida) grade.Children.Add(botoes);

        return grade;
    }

    private static TextBlock Secao(string titulo) => new()
    {
        Text = titulo.ToUpperInvariant(),
        FontSize = 10.5,
        Foreground = Brushes.Gray,
        Margin = new Thickness(0, 10, 0, 2),
    };

    private static TextBlock Nota(string texto) => new()
    {
        Text = texto,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 11.5,
        Foreground = Brushes.Gray,
        Margin = new Thickness(0, 0, 0, 8),
    };

    private UIElement FormularioDoSistema()
    {
        var pilha = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };

        void Linha(string rotulo, Control campo)
        {
            pilha.Children.Add(new TextBlock { Text = rotulo, FontSize = 12 });
            pilha.Children.Add(campo);
        }

        if (!_soAnalises)
        {
            pilha.Children.Add(Secao("Orientação"));
            Linha("Azimute para onde a mesa olha (graus, 0 = norte)", _azimute);
        }

        pilha.Children.Add(Secao(_soAnalises ? "Degrau entre mesas vizinhas" : "Mesas"));
        if (!_soAnalises) Linha("Pitch entre mesas (m)", _pitch);
        Linha("Degrau mínimo entre mesas vizinhas (cm)", _degrauMin);
        Linha("Degrau máximo entre mesas vizinhas (cm)", _degrauMax);

        if (!_soAnalises)
        {
            Linha("Espaçamento entre mesas da fileira (cm)", _espacamentoMesas);
            Linha("Espaçamento que quebra a fileira (cm)", _espacamento);
        }

        pilha.Children.Add(Secao("Ponta baixa do módulo"));
        Linha("Altura livre mínima (cm)", _pontaBaixaMin);
        Linha("Altura livre máxima (cm)", _pontaBaixaMax);
        Linha("Módulos por mesa que podem estourar (lombo)", _lombo);

        if (!_soAnalises)
        {
            pilha.Children.Add(Secao("Pilar"));
            Linha("Enterro mínimo (cm)", _enterroMin);
            Linha("Enterro máximo (cm)", _enterroMax);
        }

        pilha.Children.Add(Secao("Declividade longitudinal"));
        pilha.Children.Add(_temDeclividade);
        Linha("Declividade máxima da mesa (graus)", _declividadeMax);

        foreach (var campo in CamposDeTexto()) campo.TextChanged += (_, _) => Conferir();

        _temDeclividade.Checked += (_, _) => Conferir();
        _temDeclividade.Unchecked += (_, _) => Conferir();

        return pilha;
    }

    private UIElement FormularioDasAnalises()
    {
        var pilha = new StackPanel();

        pilha.Children.Add(Secao("Análises: o que se pinta, de que cor, em que camada"));
        pilha.Children.Add(Nota(
            "Dentro do limite não pinta nada. Fora, pinta com a cor escolhida para aquele lado. "
            + "Os limites são os da esquerda; cada análise vai na sua camada, para ligar e desligar."));

        var grade = new Grid();

        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

        void Cabecalho(int coluna, string texto)
        {
            var bloco = new TextBlock
            {
                Text = texto,
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(2, 0, 2, 4),
            };
            Grid.SetColumn(bloco, coluna);
            Grid.SetRow(bloco, 0);
            grade.Children.Add(bloco);
        }

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Cabecalho(0, "Análise");
        Cabecalho(1, "Camada");
        Cabecalho(2, "Abaixo do mínimo");
        Cabecalho(3, "Acima do máximo");

        var linha = 1;

        foreach (var kind in AnalysisRules.RangedKinds)
            AcrescentarLinha(grade, linha++, kind, Nome(kind), AnalysisRules.HasMinimum(kind));

        AcrescentarLinha(grade, linha, AnalysisKind.EdgeTable, Nome(AnalysisKind.EdgeTable), temMinimo: false);

        pilha.Children.Add(grade);

        pilha.Children.Add(Secao("Comprimento de pilar"));
        pilha.Children.Add(Nota(
            "Não é teto: o plugin sempre calcula o pilar ideal. Isto só pinta os que passarem do valor."));
        pilha.Children.Add(_pintarPilar);
        pilha.Children.Add(_pilarAcimaDe);

        _pintarPilar.Checked += (_, _) => Conferir();
        _pintarPilar.Unchecked += (_, _) => Conferir();

        return pilha;
    }

    private void AcrescentarLinha(Grid grade, int linha, AnalysisKind kind, string nome, bool temMinimo)
    {
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var ligada = new CheckBox
        {
            Content = nome,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 4, 2, 4),
        };
        var camada = new TextBox
        {
            Height = 24,
            Margin = new Thickness(2, 2, 2, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var corAbaixo = temMinimo ? CaixaDeCor() : null;
        var corAcima = CaixaDeCor();

        Grid.SetRow(ligada, linha);
        Grid.SetColumn(ligada, 0);
        grade.Children.Add(ligada);

        Grid.SetRow(camada, linha);
        Grid.SetColumn(camada, 1);
        grade.Children.Add(camada);

        if (corAbaixo is not null)
        {
            Grid.SetRow(corAbaixo, linha);
            Grid.SetColumn(corAbaixo, 2);
            grade.Children.Add(corAbaixo);
        }
        else
        {
            // Sem mínimo não há "abaixo": o lugar fica vazio de propósito, para
            // o projetista não escolher uma cor que nunca seria usada.
            var vazio = new TextBlock
            {
                Text = kind == AnalysisKind.EdgeTable ? "" : "(sem mínimo)",
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetRow(vazio, linha);
            Grid.SetColumn(vazio, 2);
            grade.Children.Add(vazio);
        }

        Grid.SetRow(corAcima, linha);
        Grid.SetColumn(corAcima, 3);
        grade.Children.Add(corAcima);

        if (kind == AnalysisKind.EdgeTable) corAcima.ToolTip = "A cor da mesa que cai fora da área.";

        ligada.Checked += (_, _) => Conferir();
        ligada.Unchecked += (_, _) => Conferir();
        camada.TextChanged += (_, _) => Conferir();
        corAcima.SelectionChanged += (_, _) => Conferir();
        if (corAbaixo is not null) corAbaixo.SelectionChanged += (_, _) => Conferir();

        _analises[kind] = new LinhaDeAnalise
        {
            Ligada = ligada,
            Camada = camada,
            CorAbaixo = corAbaixo,
            CorAcima = corAcima,
        };
    }

    /// <summary>A paleta: um quadrado da cor e o nome ao lado.</summary>
    private static ComboBox CaixaDeCor()
    {
        var caixa = new ComboBox
        {
            Height = 24,
            Margin = new Thickness(2, 2, 2, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };

        foreach (var (nome, cor) in Paleta) caixa.Items.Add(ItemDeCor(nome, cor, daPaleta: true));

        return caixa;
    }

    private static ComboBoxItem ItemDeCor(string nome, RgbColor cor, bool daPaleta)
    {
        var painel = new StackPanel { Orientation = Orientation.Horizontal };

        painel.Children.Add(new Border
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 6, 0),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B)),
        });
        painel.Children.Add(new TextBlock { Text = nome, VerticalAlignment = VerticalAlignment.Center });

        return new ComboBoxItem { Content = painel, Tag = new ItemDePaleta(cor, daPaleta) };
    }

    private sealed record ItemDePaleta(RgbColor Cor, bool DaPaleta);

    private static string Nome(AnalysisKind kind) => kind switch
    {
        AnalysisKind.LowEdge => "Ponta baixa",
        AnalysisKind.PillarLength => "Comprimento de pilar",
        AnalysisKind.Embedment => "Enterro",
        AnalysisKind.LongitudinalSlope => "Declividade",
        AnalysisKind.EdgeTable => "Mesa na borda",
        _ => kind.ToString(),
    };

    private IEnumerable<TextBox> CamposDeTexto()
    {
        yield return _azimute;
        yield return _pitch;
        yield return _degrauMin;
        yield return _degrauMax;
        yield return _espacamentoMesas;
        yield return _espacamento;
        yield return _pontaBaixaMin;
        yield return _pontaBaixaMax;
        yield return _lombo;
        yield return _enterroMin;
        yield return _enterroMax;
        yield return _declividadeMax;
        yield return _pilarAcimaDe;
    }

    // --------------------------------------------------------- ida e volta

    private void Preencher(ProjectSettingsForm form)
    {
        _azimute.Text = form.AzimuthDegrees;
        _pitch.Text = form.Pitch;
        _degrauMin.Text = form.MinStepCm;
        _degrauMax.Text = form.MaxStepCm;
        _espacamentoMesas.Text = form.TableGapCm;
        _espacamento.Text = form.BreakGapCm;
        _pontaBaixaMin.Text = form.MinLowEdgeCm;
        _pontaBaixaMax.Text = form.MaxLowEdgeCm;
        _lombo.Text = form.BumpModules;
        _enterroMin.Text = form.MinEmbedmentCm;
        _enterroMax.Text = form.MaxEmbedmentCm;
        _temDeclividade.IsChecked = form.LimitSlope;
        _declividadeMax.Text = form.MaxSlopeDegrees;
        _pintarPilar.IsChecked = form.PaintPillars;
        _pilarAcimaDe.Text = form.PillarLongerThan;

        foreach (var kind in AnalysisRules.RangedKinds)
        {
            var regra = form.Rules[kind];
            var linha = _analises[kind];

            linha.Ligada.IsChecked = regra.Enabled;
            linha.Camada.Text = regra.Layer;
            if (linha.CorAbaixo is not null) Selecionar(linha.CorAbaixo, regra.BelowColor);
            Selecionar(linha.CorAcima, regra.AboveColor);
        }

        var borda = _analises[AnalysisKind.EdgeTable];

        borda.Ligada.IsChecked = form.Edge.Enabled;
        borda.Camada.Text = form.Edge.Layer;
        Selecionar(borda.CorAcima, form.Edge.Color);
    }

    /// <summary>O formulário como está na tela. Quem lê números é o Core.</summary>
    private ProjectSettingsForm Ler()
    {
        var regras = new Dictionary<AnalysisKind, AnalysisRule>();

        foreach (var kind in AnalysisRules.RangedKinds)
        {
            var linha = _analises[kind];

            regras[kind] = new AnalysisRule(
                Enabled: linha.Ligada.IsChecked == true,
                Layer: linha.Camada.Text,
                // Sem caixa "abaixo" na tela, a cor é a que veio: não se troca
                // em silêncio o que o projetista não pôde ver.
                BelowColor: linha.CorAbaixo is { } abaixo ? CorEscolhida(abaixo) : _base.Rules[kind].BelowColor,
                AboveColor: CorEscolhida(linha.CorAcima));
        }

        var borda = _analises[AnalysisKind.EdgeTable];

        return _base with
        {
            AzimuthDegrees = _azimute.Text,
            Pitch = _pitch.Text,
            MinStepCm = _degrauMin.Text,
            MaxStepCm = _degrauMax.Text,
            TableGapCm = _espacamentoMesas.Text,
            BreakGapCm = _espacamento.Text,
            MinLowEdgeCm = _pontaBaixaMin.Text,
            MaxLowEdgeCm = _pontaBaixaMax.Text,
            BumpModules = _lombo.Text,
            MinEmbedmentCm = _enterroMin.Text,
            MaxEmbedmentCm = _enterroMax.Text,
            LimitSlope = _temDeclividade.IsChecked == true,
            MaxSlopeDegrees = _declividadeMax.Text,
            PaintPillars = _pintarPilar.IsChecked == true,
            PillarLongerThan = _pilarAcimaDe.Text,
            Rules = regras,
            Edge = new EdgeRule(
                Enabled: borda.Ligada.IsChecked == true,
                Layer: borda.Camada.Text,
                Color: CorEscolhida(borda.CorAcima)),
        };
    }

    /// <summary>
    /// Confere os campos e mostra o resultado. O try é de tudo: exceção num
    /// evento do WPF derruba o Civil 3D.
    /// </summary>
    private void Conferir()
    {
        if (_preenchendo) return;

        try
        {
            var settings = Ler().TryParse(out var motivo);

            if (settings is null)
            {
                _resumo.Text = motivo;
                _salvar.IsEnabled = false;
                return;
            }

            _resumo.Text = settings.Describe();
            _salvar.IsEnabled = true;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir a configuração.", erro);
            _resumo.Text = erro.Message;
            _salvar.IsEnabled = false;
        }
    }

    private void RestaurarPadrao()
    {
        try
        {
            _preenchendo = true;
            _base = ProjectSettingsForm.From(ProjectSettings.Default);
            Preencher(_base);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao restaurar o padrão.", erro);
        }
        finally
        {
            _preenchendo = false;
        }

        Conferir();
    }

    /// <summary>O formulário, tirado desta janela para ir numa aba (8.7).</summary>
    internal UIElement Formulario()
    {
        var formulario = (UIElement)Content;
        Content = null;
        return formulario;
    }

    /// <summary>A configuração dos campos, ou null com o motivo (8.7).</summary>
    internal ProjectSettings? Validar(out string motivo) => Ler().TryParse(out motivo);

    private void Confirmar()
    {
        try
        {
            var settings = Ler().TryParse(out var motivo);

            if (settings is null)
            {
                MessageBox.Show(this, motivo, Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Escolhida = settings;
            DialogResult = true;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao confirmar a configuração.", erro);
            MessageBox.Show(this, $"Não consegui ler a configuração: {erro.Message}", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------------------------------------ cores

    private static RgbColor CorEscolhida(ComboBox caixa) =>
        (caixa.SelectedItem as ComboBoxItem)?.Tag is ItemDePaleta item ? item.Cor : RgbColor.Red;

    /// <summary>
    /// Seleciona a cor na paleta. Uma cor que não está nela (gravada por outra
    /// versão, ou editada no registro) entra como item próprio, para não ser
    /// trocada em silêncio pela primeira da lista. Há no máximo um item desses
    /// por caixa: o anterior sai antes de o novo entrar.
    /// </summary>
    private static void Selecionar(ComboBox caixa, RgbColor cor)
    {
        ComboBoxItem? forasteiro = null;

        foreach (ComboBoxItem item in caixa.Items)
        {
            if (item.Tag is not ItemDePaleta candidata) continue;

            if (candidata.Cor == cor)
            {
                caixa.SelectedItem = item;
                return;
            }

            if (!candidata.DaPaleta) forasteiro = item;
        }

        if (forasteiro is not null) caixa.Items.Remove(forasteiro);

        var proprio = ItemDeCor($"Outra ({cor.ToHex()})", cor, daPaleta: false);

        caixa.Items.Add(proprio);
        caixa.SelectedItem = proprio;
    }
}
