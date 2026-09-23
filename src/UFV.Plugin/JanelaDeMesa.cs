using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UFV.Core;

namespace UFV.Plugin;

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

    private readonly ComboBox _salvos = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _nome = Campo();
    private readonly ComboBox _modelo = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _altura = Campo();
    private readonly TextBox _largura = Campo();
    private readonly TextBox _espessura = Campo();
    private readonly TextBox _potencia = Campo();
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

    internal JanelaDeMesa(TableProfileStore perfis, TableProfile inicial)
    {
        _perfis = perfis ?? throw new ArgumentNullException(nameof(perfis));

        Title = "UFV — Mesa";
        Width = 1000;
        Height = 760;
        MinWidth = 840;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var modulo in ModuleLibrary.Default())
            _modelo.Items.Add(new ComboBoxItem { Content = modulo.Describe(), Tag = modulo });

        _modelo.Items.Add(new ComboBoxItem { Content = "(outro módulo, medidas à mão)", Tag = null });

        _arranjo.Items.Add(new ComboBoxItem
        {
            Content = "1V — uma fileira de módulos em pé",
            Tag = TableArrangement.SingleRow,
        });
        _arranjo.Items.Add(new ComboBoxItem
        {
            Content = "2V — duas fileiras, uma acima da outra",
            Tag = TableArrangement.DoubleRow,
        });

        _salvar = new Button { Content = "Salvar perfil", Width = 110, Height = 26 };
        _salvar.Click += (_, _) => Salvar();

        _usar = new Button
        {
            Content = "Usar esta mesa",
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

        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grade.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var campos = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = Formulario(),
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

        var emCima = Emoldurar("PLANTA BAIXA", _planta);
        var embaixo = Emoldurar("VISTA LATERAL — na direção da inclinação", _corte);

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
            Content = "Fechar",
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

        void Linha(string rotulo, Control campo)
        {
            pilha.Children.Add(new TextBlock { Text = rotulo, FontSize = 12 });
            pilha.Children.Add(campo);
        }

        Secao("Perfil");
        Linha("Perfil salvo", _salvos);
        Linha("Nome", _nome);

        Secao("Módulo");
        Linha("Modelo", _modelo);
        Linha("Altura (m)", _altura);
        Linha("Largura (m)", _largura);
        Linha("Espessura (m)", _espessura);
        Linha("Potência (Wp)", _potencia);

        Secao("Mesa");
        Linha("Módulos", _quantidade);
        Linha("Arranjo", _arranjo);
        Linha("Espaçamento entre módulos (m)", _espacamentoH);
        Linha("Espaçamento entre fileiras (m)", _espacamentoV);
        Linha("Sobra da esquerda (m)", _sobraEsquerda);
        Linha("Sobra da direita (m)", _sobraDireita);
        Linha("Inclinação (graus)", _inclinacao);

        Secao("Estrutura");
        Linha("Tesoura T1 (m)", _tesoura);
        Linha("Pilar na tesoura T2 (m)", _pilarNaTesoura);
        Linha("Pilar: largura (m)", _pilarLargura);
        Linha("Pilar: profundidade (m)", _pilarProfundidade);
        Linha("Vão entre pilares (m)", _vaoAlvo);
        Linha("Balanço nas pontas (m)", _balanco);

        foreach (var (campo, _) in Todos()) campo.TextChanged += (_, _) => Recalcular();

        _arranjo.SelectionChanged += (_, _) => Recalcular();
        _modelo.SelectionChanged += (_, _) => TrocarModulo();
        _salvos.SelectionChanged += (_, _) => CarregarSalvo();

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
        yield return (_nome, "Nome");
        yield return (_altura, "Altura");
        yield return (_largura, "Largura");
        yield return (_espessura, "Espessura");
        yield return (_potencia, "Potência");
        yield return (_quantidade, "Módulos");
        yield return (_espacamentoH, "Espaçamento entre módulos");
        yield return (_espacamentoV, "Espaçamento entre fileiras");
        yield return (_sobraEsquerda, "Sobra da esquerda");
        yield return (_sobraDireita, "Sobra da direita");
        yield return (_inclinacao, "Inclinação");
        yield return (_tesoura, "Tesoura T1");
        yield return (_pilarNaTesoura, "Pilar na tesoura T2");
        yield return (_pilarLargura, "Pilar: largura");
        yield return (_pilarProfundidade, "Pilar: profundidade");
        yield return (_vaoAlvo, "Vão entre pilares");
        yield return (_balanco, "Balanço nas pontas");
    }

    // --------------------------------------------------------- ida e volta

    private void ListarSalvos()
    {
        _salvos.Items.Clear();
        _salvos.Items.Add(new ComboBoxItem { Content = "(nenhum — mesa atual)", Tag = null });

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
            Avisar($"Não consegui abrir o perfil \"{nome}\": {erro.Message}");
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
        _potencia.Text = Numero(perfil.Layout.Module.PowerWatts);

        _quantidade.Text = perfil.Layout.ModuleCount.ToString(Brasil);
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

        Selecionar(_arranjo, perfil.Layout.Arrangement);

        var daBiblioteca = ModuleLibrary.Find(perfil.Layout.Module.Model);

        _modelo.SelectedIndex = daBiblioteca is null
            ? _modelo.Items.Count - 1
            : ModuleLibrary.Default().ToList().FindIndex(m => m.Model == daBiblioteca.Model);
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
                ? $"o campo \"{nome}\" está em branco."
                : $"não consigo ler o número do campo \"{nome}\".";

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

        if (!NumberInput.TryParseCount(_quantidade.Text, out var quantidade))
        {
            motivo = "o número de módulos precisa ser inteiro.";
            return null;
        }

        var (marca, modelo) = Modulo();

        var perfil = new TableProfile(
            _nome.Text,
            new TableLayout(
                new SolarModule(marca, modelo, potencia, altura, largura, espessura),
                quantidade,
                Arranjo(),
                gapH, gapV, esquerda, direita),
            new TableFrame(tesoura, t2, pilarL, pilarP, vao, balanco),
            graus * Math.PI / 180);

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
        _potencia.Text = Numero(modulo.PowerWatts);
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

        try
        {
            var perfil = Ler(out var motivo);

            if (perfil is null)
            {
                NaoDeu(motivo);
                return;
            }

            var pilares = PillarTable.Distribute(
                perfil.Layout.Length,
                perfil.Frame.PillarSpanTarget,
                perfil.Frame.PillarCantilever);
            var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

            _planta.Mostrar(geometria);
            _corte.Mostrar(geometria, perfil.TiltRadians);

            var potencia = perfil.Layout.ModuleCount * perfil.Layout.Module.PowerWatts / 1000;

            // A subida do pilar é o número que o projetista não consegue
            // estimar de cabeça, e é o que decide se o pilar dele vai caber.
            var subida = PillarSizing.FreeHeight(0, geometria.PillarRow, perfil.TiltRadians);

            _resumo.Text =
                $"Comprimento {Numero(perfil.Layout.Length)} m · {Numero(perfil.Layout.Depth)} m na "
                + $"inclinação · {perfil.Layout.Columns} colunas · "
                + $"{potencia.ToString("0.#", Brasil)} kWp\n"
                + $"{pilares.PillarCount} pilares em {pilares.Spans.Count} vãos de "
                + $"{Numero(pilares.Spans[0])} m"
                + (pilares.Cantilever > 0
                    ? $", com balanço de {Numero(pilares.Cantilever)} m em cada ponta\n"
                    : ", com o pilar na ponta da estrutura\n")
                + $"O pilar encosta na mesa a {Numero(geometria.PillarRow)} m da ponta baixa do "
                + $"módulo (sobra da tesoura {Numero(geometria.RafterOffset)} m), e sobe "
                + $"{Numero(subida)} m acima dela.";

            _usar.IsEnabled = true;
            _salvar.IsEnabled = true;
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular a mesa.", erro);
            NaoDeu(erro.Message);
        }
    }

    private void NaoDeu(string motivo)
    {
        _planta.Dizer("A mesa ainda não fecha.");
        _corte.Dizer("A mesa ainda não fecha.");
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
                    $"Já existe um perfil chamado \"{perfil.Name}\". Substituir?",
                    Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resposta != MessageBoxResult.Yes) return;
            }

            _perfis.Save(perfil);

            _preenchendo = true;
            ListarSalvos();
            _preenchendo = false;

            Avisar($"Perfil \"{perfil.Name}\" salvo em {_perfis.Folder}.");
        }
        catch (Exception erro)
        {
            _preenchendo = false;
            RegistroDeDiagnostico.Registrar("Falha ao salvar o perfil de mesa.", erro);
            Avisar($"Não consegui salvar: {erro.Message}");
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

    private static string Numero(double valor) => valor.ToString("0.####", Brasil);

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
