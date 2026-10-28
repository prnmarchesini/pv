using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A janela do carport (35.2, <c>plano/carport</c>): o perfil inteiro em
/// seções lado a lado, rótulo ao lado do campo, e o corte do pórtico
/// redesenhado a cada tecla. Como na janela de Mesa, o recálculo inteiro fica
/// num try: exceção solta num manipulador do WPF fecha o Civil 3D.
/// Abre a partir de Configurações › Estruturas › Nova estrutura... › Carport.
/// </summary>
internal sealed class JanelaDeCarport : Window
{
    private readonly IReadOnlyList<SolarModule> _modulosDoDesenho;

    private readonly TextBox _nome = Campo(220);
    private readonly ComboBox _doDesenho = new() { Height = 24, Width = 220 };
    private readonly TextBox _marca = Campo();
    private readonly TextBox _modelo = Campo();
    private readonly TextBox _potencia = Campo();
    private readonly TextBox _altura = Campo();
    private readonly TextBox _largura = Campo();
    private readonly TextBox _espessura = Campo();

    private readonly ComboBox _posicao = new() { Height = 24, Width = 90 };
    private readonly TextBox _naDescida = Campo();
    private readonly TextBox _porVao = Campo();
    private readonly TextBox _folgaColunas = Campo();
    private readonly TextBox _folgaDescida = Campo();
    private readonly TextBox _inclinacao = Campo();
    private readonly TextBox _alturaLivre = Campo();
    private readonly TextBox _corredor = Campo();

    private readonly TextBox _bases = Campo();
    private readonly TextBox _abertura = Campo();
    private readonly TextBox _deslocamento = Campo();
    private readonly TextBox _recuoBaixo = Campo();
    private readonly TextBox _recuoAlto = Campo();
    private readonly TextBox _maoNoPilar = Campo();
    private readonly TextBox _maoNaVigaAlto = Campo();
    private readonly TextBox _maoNaVigaBaixo = Campo();
    private readonly TextBox _tercasPorModulo = Campo();
    private readonly TextBox _tercaDaPonta = Campo();

    private readonly (TextBox Nome, TextBox Altura, TextBox Largura)[] _secoes =
        [.. Enumerable.Range(0, 4).Select(_ => (Campo(110), Campo(), Campo()))];

    private readonly TextBox _chapa = Campo();
    private readonly TextBox _chapaEspessura = Campo();
    private readonly TextBox _bloco = Campo();
    private readonly TextBox _blocoAltura = Campo();

    private readonly CorteDoCarport _corte = new() { MinHeight = 230 };
    private readonly TextBlock _resumo = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _usar;

    private bool _enchendo;

    /// <summary>O carport confirmado em "Usar", ou null.</summary>
    internal CarportProfile? Resultado { get; private set; }

    /// <param name="perfil">O carport de partida.</param>
    /// <param name="modulosDoDesenho">Os módulos das mesas do desenho, para trazer um deles sem digitar.</param>
    internal JanelaDeCarport(CarportProfile perfil, IReadOnlyList<SolarModule> modulosDoDesenho)
    {
        _modulosDoDesenho = modulosDoDesenho;

        Title = Tr.T("Clivus Solar — Carport");
        Width = 1180;
        Height = 700;
        MinWidth = 1000;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _posicao.Items.Add(Tr.T("Retrato"));
        _posicao.Items.Add(Tr.T("Paisagem"));

        _doDesenho.Items.Add(Tr.T("(à mão)"));
        foreach (var m in modulosDoDesenho) _doDesenho.Items.Add(m.Describe());
        _doDesenho.SelectionChanged += (_, _) => Tentar(TrazerModulo);

        var colunas = new Grid();
        for (var i = 0; i < 4; i++) colunas.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var c1 = new StackPanel();
        c1.Children.Add(Secao(Tr.T("Módulo"),
            (Tr.T("Das mesas"), _doDesenho, Tr.T("Traz o módulo de uma mesa deste desenho; os campos continuam editáveis.")),
            (Tr.T("Marca"), _marca, null),
            (Tr.T("Modelo"), _modelo, null),
            (Tr.T("Potência (Wp)"), _potencia, null),
            (Tr.T("Altura (m)"), _altura, Tr.T("A medida maior do módulo.")),
            (Tr.T("Largura (m)"), _largura, null),
            (Tr.T("Espessura (m)"), _espessura, null)));

        var c2 = new StackPanel();
        c2.Children.Add(Secao(Tr.T("Cobertura"),
            (Tr.T("Posição"), _posicao, Tr.T("Retrato: a medida maior do módulo na descida.")),
            (Tr.T("Na descida"), _naDescida, Tr.T("Módulos da borda baixa à alta.")),
            (Tr.T("Por vão"), _porVao, Tr.T("Módulos ao longo da fila entre dois pórticos.")),
            (Tr.T("Folga colunas (m)"), _folgaColunas, null),
            (Tr.T("Folga descida (m)"), _folgaDescida, null),
            (Tr.T("Inclinação (°)"), _inclinacao, null),
            (Tr.T("Altura livre (m)"), _alturaLivre, Tr.T("Do chão do pilar mais alto do pórtico à borda baixa dos módulos, por baixo do quadro.")),
            (Tr.T("Corredor (m)"), _corredor, Tr.T("O espaço livre entre uma fila de carport e a próxima, no Usina (a pista do estacionamento)."))));
        c2.Children.Add(Secao(Tr.T("Terças"),
            (Tr.T("Por módulo"), _tercasPorModulo, null),
            (Tr.T("Da ponta (m)"), _tercaDaPonta, Tr.T("Da ponta do módulo à terça mais perto, na rampa. Com uma terça por módulo, ela vai no meio."))));

        var c3 = new StackPanel();
        c3.Children.Add(Secao(Tr.T("Pórtico"),
            (Tr.T("Bases (m)"), _bases, Tr.T("A distância em planta entre as bases dos dois pilares.")),
            (Tr.T("Abertura (°)"), _abertura, Tr.T("Quanto cada pilar abre para fora a partir da vertical.")),
            (Tr.T("Deslocamento (m)"), _deslocamento, Tr.T("Quanto o meio das bases sai do meio da cobertura; positivo para o lado alto.")),
            (Tr.T("Viga, recuo baixo (m)"), _recuoBaixo, Tr.T("Quanto a viga para antes da borda baixa dos módulos, na rampa.")),
            (Tr.T("Viga, recuo alto (m)"), _recuoAlto, null),
            (Tr.T("Mão fr. no pilar (m)"), _maoNoPilar, Tr.T("Do topo do pilar para baixo, ao longo dele.")),
            (Tr.T("Mão fr. viga, alto (m)"), _maoNaVigaAlto, Tr.T("Do topo do pilar do lado alto para dentro do pórtico, na rampa.")),
            (Tr.T("Mão fr. viga, baixo (m)"), _maoNaVigaBaixo, null)));
        c3.Children.Add(Secao(Tr.T("Base"),
            (Tr.T("Chapa (m)"), _chapa, null),
            (Tr.T("Espessura (m)"), _chapaEspessura, null),
            (Tr.T("Bloco (m)"), _bloco, null),
            (Tr.T("Bloco, altura (m)"), _blocoAltura, Tr.T("Quanto o bloco sobe acima do chão; o pilar começa na chapa, em cima dele."))));

        var c4 = new StackPanel();
        c4.Children.Add(TabelaDeSecoes());

        Grid.SetColumn(c1, 0);
        Grid.SetColumn(c2, 1);
        Grid.SetColumn(c3, 2);
        Grid.SetColumn(c4, 3);
        colunas.Children.Add(c1);
        colunas.Children.Add(c2);
        colunas.Children.Add(c3);
        colunas.Children.Add(c4);

        var topo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        topo.Children.Add(new TextBlock { Text = Tr.T("Nome"), Width = 50, VerticalAlignment = VerticalAlignment.Center });
        topo.Children.Add(_nome);
        var abrir = new Button { Content = Tr.T("Abrir arquivo..."), Height = 24, Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = Tr.T("Traz um carport salvo (.clivuscarport.json).") };
        abrir.Click += (_, _) => Tentar(AbrirArquivo);
        var salvarArquivo = new Button { Content = Tr.T("Salvar arquivo..."), Height = 24, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = Tr.T("Grava este carport num arquivo, para usar em outro desenho.") };
        salvarArquivo.Click += (_, _) => Tentar(SalvarArquivo);
        var padrao = new Button { Content = Tr.T("Voltar ao padrão"), Height = 24, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = Tr.T("Os valores do carport biposte de referência.") };
        padrao.Click += (_, _) => Tentar(() => Encher(CarportProfile.Reference with { Name = _nome.Text }));
        topo.Children.Add(abrir);
        topo.Children.Add(salvarArquivo);
        topo.Children.Add(padrao);

        _usar = new Button { Content = Tr.T("Usar"), Width = 110, Height = 26, IsDefault = true, ToolTip = Tr.T("Volta às Configurações com este carport (grava no desenho em \"Salvar no desenho\").") };
        _usar.Click += (_, _) => Tentar(Usar);
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        botoes.Children.Add(_usar);
        botoes.Children.Add(new Button { Content = Tr.T("Cancelar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });

        var raiz = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(topo, Dock.Top);
        raiz.Children.Add(topo);
        DockPanel.SetDock(colunas, Dock.Top);
        raiz.Children.Add(colunas);
        DockPanel.SetDock(botoes, Dock.Bottom);
        raiz.Children.Add(botoes);
        DockPanel.SetDock(_resumo, Dock.Bottom);
        raiz.Children.Add(_resumo);
        raiz.Children.Add(_corte);
        Content = raiz;

        foreach (var caixa in Caixas()) caixa.TextChanged += (_, _) => Recalcular();
        _posicao.SelectionChanged += (_, _) => Tentar(TrocarPosicao);

        Encher(perfil);
    }

    private static TextBox Campo(double largura = 70) => new() { Height = 24, Width = largura, VerticalContentAlignment = VerticalAlignment.Center };

    private IEnumerable<TextBox> Caixas() =>
        new[]
        {
            _nome, _corredor, _marca, _modelo, _potencia, _altura, _largura, _espessura, _naDescida, _porVao, _folgaColunas, _folgaDescida,
            _inclinacao, _alturaLivre, _bases, _abertura, _deslocamento, _recuoBaixo, _recuoAlto, _maoNoPilar, _maoNaVigaAlto,
            _maoNaVigaBaixo, _tercasPorModulo, _tercaDaPonta, _chapa, _chapaEspessura, _bloco, _blocoAltura,
        }.Concat(_secoes.SelectMany(s => new[] { s.Nome, s.Altura, s.Largura }));

    private static GroupBox Secao(string titulo, params (string Rotulo, Control Campo, string? Dica)[] linhas)
    {
        var grade = new Grid { Margin = new Thickness(4) };
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        for (var i = 0; i < linhas.Length; i++)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (rotulo, campo, dica) = linhas[i];
            var texto = new TextBlock { Text = rotulo, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2), ToolTip = dica };
            campo.Margin = new Thickness(0, 2, 0, 2);
            if (dica is not null) campo.ToolTip = dica;
            Grid.SetRow(texto, i);
            Grid.SetRow(campo, i);
            Grid.SetColumn(campo, 1);
            grade.Children.Add(texto);
            grade.Children.Add(campo);
        }

        return new GroupBox { Header = titulo, Content = grade, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Top };
    }

    private GroupBox TabelaDeSecoes()
    {
        var grade = new Grid { Margin = new Thickness(4) };
        for (var c = 0; c < 4; c++) grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        void Celula(UIElement e, int linha, int coluna)
        {
            Grid.SetRow(e, linha);
            Grid.SetColumn(e, coluna);
            grade.Children.Add(e);
        }

        grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Celula(new TextBlock { Text = Tr.T("Seção"), Margin = new Thickness(0, 0, 4, 2), Foreground = Brushes.Gray }, 0, 1);
        Celula(new TextBlock { Text = Tr.T("Alt. (m)"), Margin = new Thickness(4, 0, 4, 2), Foreground = Brushes.Gray }, 0, 2);
        Celula(new TextBlock { Text = Tr.T("Larg. (m)"), Margin = new Thickness(4, 0, 4, 2), Foreground = Brushes.Gray }, 0, 3);

        string[] pecas = [Tr.T("Pilar"), Tr.T("Viga"), Tr.T("Terça"), Tr.T("Mão francesa")];
        for (var i = 0; i < 4; i++)
        {
            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Celula(new TextBlock { Text = pecas[i], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) }, i + 1, 0);
            var (nome, altura, largura) = _secoes[i];
            nome.Margin = new Thickness(0, 2, 4, 2);
            nome.ToolTip = Tr.T("O nome que vai para a lista de material: \"2 × C 130×50\".");
            altura.Margin = new Thickness(4, 2, 4, 2);
            largura.Margin = new Thickness(4, 2, 0, 2);
            largura.ToolTip = Tr.T("A largura do conjunto: dois C costas com costas contam juntos.");
            Celula(nome, i + 1, 1);
            Celula(altura, i + 1, 2);
            Celula(largura, i + 1, 3);
        }

        return new GroupBox { Header = Tr.T("Seções do aço"), Content = grade, Margin = new Thickness(0, 0, 0, 6), VerticalAlignment = VerticalAlignment.Top };
    }

    private void Encher(CarportProfile p)
    {
        _enchendo = true;
        try
        {
            var a = p.Array;
            var f = p.Frame;
            _nome.Text = p.Name;
            _doDesenho.SelectedIndex = 0;
            _marca.Text = a.Module.Brand;
            _modelo.Text = a.Module.Model;
            _potencia.Text = Numero(a.Module.PowerWatts);
            _altura.Text = Numero(a.Module.Height);
            _largura.Text = Numero(a.Module.Width);
            _espessura.Text = Numero(a.Module.Thickness);
            _posicao.SelectedIndex = a.Orientation == CarportOrientation.Portrait ? 0 : 1;
            _naDescida.Text = Numero(a.ModulesDown);
            _porVao.Text = Numero(a.ModulesAcross);
            _folgaColunas.Text = Numero(a.GapAcross);
            _folgaDescida.Text = Numero(a.GapDown);
            _inclinacao.Text = Numero(p.TiltDegrees);
            _alturaLivre.Text = Numero(p.ClearHeight);
            _corredor.Text = Numero(p.Aisle);
            _bases.Text = Numero(f.PillarBaseSpacing);
            _abertura.Text = Numero(f.PillarLeanDegrees);
            _deslocamento.Text = Numero(f.PillarOffset);
            _recuoBaixo.Text = Numero(f.BeamLowInset);
            _recuoAlto.Text = Numero(f.BeamHighInset);
            _maoNoPilar.Text = Numero(f.BraceOnPillar);
            _maoNaVigaAlto.Text = Numero(f.BraceOnBeamHigh);
            _maoNaVigaBaixo.Text = Numero(f.BraceOnBeamLow);
            _tercasPorModulo.Text = Numero(f.PurlinsPerModule);
            _tercaDaPonta.Text = Numero(f.PurlinEdgeOffset);
            CarportSection[] secoes = [f.Pillar, f.Beam, f.Purlin, f.Brace];
            for (var i = 0; i < 4; i++)
            {
                _secoes[i].Nome.Text = secoes[i].Name;
                _secoes[i].Altura.Text = Numero(secoes[i].Depth);
                _secoes[i].Largura.Text = Numero(secoes[i].Width);
            }

            _chapa.Text = Numero(f.Base.PlateSize);
            _chapaEspessura.Text = Numero(f.Base.PlateThickness);
            _bloco.Text = Numero(f.Base.PedestalSize);
            _blocoAltura.Text = Numero(f.Base.PedestalHeight);
        }
        finally
        {
            _enchendo = false;
        }

        Recalcular();
    }

    /// <summary>
    /// Retrato e paisagem trocam a medida do módulo na descida: a terça que
    /// cabia a 0,57 m da ponta num módulo de 2,28 m não cabe num de 1,13 m.
    /// Sem caber, ela vai a um quarto do módulo (revisão do 34).
    /// </summary>
    private void TrocarPosicao()
    {
        if (_enchendo) return;

        var naDescida = _posicao.SelectedIndex == 1 ? _largura.Text : _altura.Text;
        if (NumberInput.TryParseMeasure(naDescida, out var modulo) && modulo > 0
            && NumberInput.TryParseMeasure(_tercaDaPonta.Text, out var daPonta) && daPonta >= modulo / 2)
        {
            _tercaDaPonta.Text = Numero(Math.Round(modulo / 4, 3));
            return;                                  // o TextChanged recalcula
        }

        Recalcular();
    }

    private static string Numero(double valor) => valor.ToString("0.######", Tr.Culture);

    private void TrazerModulo()
    {
        if (_enchendo || _doDesenho.SelectedIndex <= 0) return;
        var m = _modulosDoDesenho[_doDesenho.SelectedIndex - 1];

        _enchendo = true;
        try
        {
            _marca.Text = m.Brand;
            _modelo.Text = m.Model;
            _potencia.Text = Numero(m.PowerWatts);
            _altura.Text = Numero(m.Height);
            _largura.Text = Numero(m.Width);
            _espessura.Text = Numero(m.Thickness);
        }
        finally
        {
            _enchendo = false;
        }

        Recalcular();
    }

    /// <summary>O perfil dos campos, ou null com o campo que não é número.</summary>
    private CarportProfile? Ler(out string? motivo)
    {
        motivo = null;
        string? ruim = null;

        double M(TextBox caixa, string campo)
        {
            if (NumberInput.TryParseMeasure(caixa.Text, out var v)) return v;
            ruim ??= campo;
            return double.NaN;
        }

        int N(TextBox caixa, string campo)
        {
            if (NumberInput.TryParseCount(caixa.Text, out var v)) return v;
            ruim ??= campo;
            return 0;
        }

        var modulo = new SolarModule(_marca.Text.Trim(), _modelo.Text.Trim(), M(_potencia, Tr.T("Potência")), M(_altura, Tr.T("Altura")), M(_largura, Tr.T("Largura")), M(_espessura, Tr.T("Espessura")));
        var secoes = _secoes.Select((s, i) => new CarportSection(s.Nome.Text.Trim(), M(s.Altura, Tr.T("Seções do aço")), M(s.Largura, Tr.T("Seções do aço")))).ToArray();

        var perfil = new CarportProfile(
            _nome.Text,
            new CarportArray(modulo, _posicao.SelectedIndex == 1 ? CarportOrientation.Landscape : CarportOrientation.Portrait,
                N(_naDescida, Tr.T("Na descida")), N(_porVao, Tr.T("Por vão")), M(_folgaColunas, Tr.T("Folga colunas")), M(_folgaDescida, Tr.T("Folga descida"))),
            new CarportFrame(
                M(_bases, Tr.T("Bases")),
                M(_abertura, Tr.T("Abertura")) * Math.PI / 180,
                M(_deslocamento, Tr.T("Deslocamento")),
                M(_recuoBaixo, Tr.T("Viga, recuo baixo")),
                M(_recuoAlto, Tr.T("Viga, recuo alto")),
                N(_tercasPorModulo, Tr.T("Terças por módulo")),
                M(_tercaDaPonta, Tr.T("Da ponta")),
                M(_maoNoPilar, Tr.T("Mão fr. no pilar")),
                M(_maoNaVigaAlto, Tr.T("Mão fr. viga, alto")),
                M(_maoNaVigaBaixo, Tr.T("Mão fr. viga, baixo")),
                secoes[0], secoes[1], secoes[2], secoes[3],
                new CarportBase(M(_chapa, Tr.T("Chapa")), M(_chapaEspessura, Tr.T("Espessura")), M(_bloco, Tr.T("Bloco")), M(_blocoAltura, Tr.T("Bloco, altura")))),
            M(_inclinacao, Tr.T("Inclinação")) * Math.PI / 180,
            M(_alturaLivre, Tr.T("Altura livre")))
        {
            Aisle = M(_corredor, Tr.T("Corredor")),
        };

        if (ruim is not null)
        {
            motivo = Tr.F("o campo \"{0}\" não é um número", ruim);
            return null;
        }

        if (perfil.WhyInvalid is { } porque)
        {
            motivo = porque;
            return null;
        }

        return perfil;
    }

    private void Recalcular()
    {
        if (_enchendo) return;

        try
        {
            var perfil = Ler(out var motivo);
            if (perfil is null)
            {
                _corte.Dizer(Tr.F("Não dá para desenhar: {0}.", motivo));
                _resumo.Text = string.Empty;
                _usar.IsEnabled = false;
                return;
            }

            _corte.Mostrar(perfil);
            var portico = CarportGeometry.Portal(perfil, 0, 0, 1, 0, true, (_, _) => 0, out var porque);
            _usar.IsEnabled = portico is not null;
            if (portico is null)
            {
                _resumo.Text = string.Empty;
                return;
            }

            var pilares = portico.Bars.Where(b => b.Part == CarportPart.Pillar).ToList();
            var porVao = perfil.Array.ModulesDown * perfil.Array.ModulesAcross;
            _resumo.Text = Tr.F(
                "Vão de {0:0.###} m com {1} módulos ({2:0.##} kWp) · pilares de {3:0.###} e {4:0.###} m em chão plano · ponto mais baixo {5:0.###} m do chão. Em terreno com desnível, cada pilar sai com o comprimento que o chão pede.",
                perfil.Array.BayWidth, porVao, porVao * perfil.Array.Module.PowerWatts / 1000, pilares[0].Length, pilares[1].Length, portico.LowestAboveGround);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular o carport.", erro);
            _corte.Dizer(Tr.F("Não consegui recalcular: {0}", erro.Message));
            _usar.IsEnabled = false;
        }
    }

    private void Usar()
    {
        var perfil = Ler(out var motivo);
        if (perfil is null)
        {
            MessageBox.Show(this, Tr.F("O carport não serve: {0}.", motivo), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Resultado = perfil;
        DialogResult = true;
    }

    private void AbrirArquivo()
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Filter = Tr.T("Carport do Clivus") + " (*" + CarportProfile.Extensao + ")|*" + CarportProfile.Extensao };
        if (dialogo.ShowDialog(this) != true) return;

        try
        {
            Encher(CarportProfile.Parse(File.ReadAllText(dialogo.FileName)));
        }
        catch (InvalidOperationException erro)
        {
            MessageBox.Show(this, erro.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SalvarArquivo()
    {
        var perfil = Ler(out var motivo);
        if (perfil is null)
        {
            MessageBox.Show(this, Tr.F("O carport não serve: {0}.", motivo), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nome = string.Concat(perfil.Name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            FileName = nome + CarportProfile.Extensao,
            Filter = Tr.T("Carport do Clivus") + " (*" + CarportProfile.Extensao + ")|*" + CarportProfile.Extensao,
        };
        if (dialogo.ShowDialog(this) != true) return;

        File.WriteAllText(dialogo.FileName, perfil.ToJson());
    }

    /// <summary>Manipulador de clique do WPF: exceção solta aqui fecha o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela do carport.", erro);
            MessageBox.Show(this, Tr.F("Não consegui: {0}", erro.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
