using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Desenho › Canaleta (28.2): a seção (trapezoidal ou meia-cana) com o
/// desenho cotado que muda enquanto se digita, o revestimento, o assentamento
/// e os botões que convertem as linhas, pegam a seção de uma canaleta e
/// voltam a canaleta a linha. Solta, como as outras janelas da drenagem.
/// </summary>
internal sealed class JanelaDeCanaleta : Window
{
    private static readonly Dictionary<Document, JanelaDeCanaleta> Abertas = [];

    private readonly Document _documento;
    private readonly ComboBox _secoes = new() { Width = 170, IsEditable = true, ToolTip = Tr.T("As seções salvas no desenho. Digite um nome novo para salvar outra.") };
    private readonly RadioButton _trapezoidal = new() { Content = Tr.T("Trapezoidal"), GroupName = "tipo", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly RadioButton _meiaCana = new() { Content = Tr.T("Meia-cana"), GroupName = "tipo", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _b = Campo(Tr.T("Base b, em m."));
    private readonly TextBox _z = Campo(Tr.T("Talude z = x / y (horizontal por vertical)."));
    private readonly TextBox _y = Campo(Tr.T("Altura y, em m."));
    private readonly TextBox _r = Campo(Tr.T("Raio r, em m."));
    private readonly TextBox _e = Campo(Tr.T("Espessura e da parede, em m."));
    private readonly ComboBox _revestimento = new() { Width = 200, IsEditable = true, ToolTip = Tr.T("O revestimento dá o n de Manning e a velocidade máxima.") };
    private readonly TextBox _n = Campo(Tr.T("Coeficiente de rugosidade de Manning."));
    private readonly TextBox _vmax = Campo(Tr.T("Velocidade máxima do revestimento, em m/s."));
    private readonly RadioButton _borda = new() { Content = Tr.T("Borda no terreno"), GroupName = "modo", Margin = new Thickness(0, 0, 12, 0), ToolTip = Tr.T("Fundo = terreno − altura. Trecho abaixo da declividade mínima é pintado.") };
    private readonly RadioButton _minima = new() { Content = Tr.T("Fundo com declividade mínima"), GroupName = "modo", ToolTip = Tr.T("O fundo desce sempre pelo menos a mínima; onde o terreno não deixa, a canaleta aprofunda.") };
    private readonly Canvas _desenho = new() { Width = 300, Height = 170, Background = new SolidColorBrush(Color.FromRgb(36, 40, 48)), ClipToBounds = true };
    private readonly TextBlock _medidas = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _linhaTrapezio;
    private readonly StackPanel _linhaRaio;
    private bool _carregando;

    private static TextBox Campo(string dica) => new() { Width = 55, ToolTip = dica };

    private JanelaDeCanaleta(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Canaleta");
        Width = 680;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

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

        Button Botao(string texto, string dica, Action acao, double largura = 0)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            if (largura > 0) b.Width = largura;
            b.Click += (_, _) => acao();
            return b;
        }

        foreach (var (nome, _, _) in ChannelSection.Linings) _revestimento.Items.Add(nome);

        _linhaTrapezio = Linha(R("b", 14), _b, R(Tr.T("m")), R("z", 14), _z, R("y", 14), _y, R(Tr.T("m")));
        _linhaRaio = Linha(R("r", 14), _r, R(Tr.T("m")));

        // Esquerda: os campos; direita: o desenho.
        var campos = new StackPanel { Width = 340 };
        campos.Children.Add(Linha(R(Tr.T("Seção"), 90), _secoes));
        campos.Children.Add(Linha(R(Tr.T("Tipo"), 90), _trapezoidal, _meiaCana));
        campos.Children.Add(Linha(R(string.Empty, 90), _linhaTrapezio));
        campos.Children.Add(Linha(R(string.Empty, 90), _linhaRaio));
        campos.Children.Add(Linha(R(Tr.T("Espessura"), 90), R("e", 14), _e, R(Tr.T("m"))));
        campos.Children.Add(Linha(R(Tr.T("Revestimento"), 90), _revestimento));
        campos.Children.Add(Linha(R(string.Empty, 90), R("n", 14), _n, R(Tr.T("v máx")), _vmax, R(Tr.T("m/s"))));
        campos.Children.Add(Linha(R(Tr.T("Assentamento"), 90), _borda));
        campos.Children.Add(Linha(R(string.Empty, 90), _minima));
        campos.Children.Add(Linha(
            R(string.Empty, 90),
            Botao(Tr.T("Salvar seção"), Tr.T("Grava a seção com este nome na biblioteca do desenho."), Salvar),
            Botao(Tr.T("Excluir"), Tr.T("Tira a seção da biblioteca (as canaletas feitas com ela continuam com as medidas delas)."), Excluir)));

        var direita = new StackPanel();
        direita.Children.Add(_desenho);
        direita.Children.Add(_medidas);

        var topo = new StackPanel { Orientation = Orientation.Horizontal };
        topo.Children.Add(campos);
        topo.Children.Add(direita);

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(topo);

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Converter: escolha no desenho as linhas de análise (ou polilinhas) que viram canaleta com esta seção; numa canaleta, troca a seção. Cada canaleta ganha o perfil (fundo, terreno e topo da parede), o sólido 3D e, em planta, os trechos abaixo da declividade mínima pintados de amarelo e os planos ou subindo de vermelho."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });

        pilha.Children.Add(_recado);

        var botoes = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        botoes.Children.Add(Botao(Tr.T("Converter linhas…"), Tr.T("Salva a seção e pede no desenho as linhas que viram canaleta (ou as canaletas que trocam de seção)."), Converter));
        botoes.Children.Add(Botao(Tr.T("Pegar da canaleta…"), Tr.T("Clique numa canaleta: a seção dela vem para esta janela."), () => Mandar(PluginInfo.ComandoDrenagemCanaletaLer)));
        botoes.Children.Add(Botao(Tr.T("Voltar a linha…"), Tr.T("As canaletas escolhidas voltam a ser linha de análise; o 3D sai."), () => Mandar(PluginInfo.ComandoDrenagemCanaletaDesfazer)));
        botoes.Children.Add(Botao(Tr.T("Recriar gráfico…"), Tr.T("Escolha as canaletas (Enter = todas): o gráfico é desenhado de novo, onde está."), () => Mandar(PluginInfo.ComandoDrenagemGraficoRecriar)));
        botoes.Children.Add(Botao(Tr.T("Apagar gráfico…"), Tr.T("Escolha as canaletas (Enter = todas): o gráfico sai; Recriar gráfico traz de volta."), () => Mandar(PluginInfo.ComandoDrenagemGraficoApagar)));
        botoes.Children.Add(Botao(Tr.T("Atualizar"), Tr.T("Refaz o perfil, o 3D e a pintura de todas as linhas e canaletas do desenho."), Atualizar));
        var fechar = Botao(Tr.T("Fechar"), Tr.T("Fecha a janela; as canaletas ficam."), Close);
        fechar.IsCancel = true;
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        Content = pilha;

        foreach (var caixa in new[] { _b, _z, _y, _r, _e, _n, _vmax }) caixa.TextChanged += (_, _) => Redesenhar();
        _trapezoidal.Checked += (_, _) => { AplicarTipo(); Redesenhar(); };
        _meiaCana.Checked += (_, _) => { AplicarTipo(); Redesenhar(); };
        _secoes.SelectionChanged += (_, _) => { if (!_carregando && _secoes.SelectedItem is string nome) CarregarDaBiblioteca(nome); };
        _revestimento.SelectionChanged += (_, _) =>
        {
            if (_carregando || _revestimento.SelectedItem is not string nome) return;
            var (_, n, v) = ChannelSection.Linings.First(l => l.Name == nome);
            _n.Text = N(n);
            _vmax.Text = N(v);
        };

        var configuracao = DrenagemStore.Ler(documento.Database);
        PreencherLista(configuracao);
        Mostrar(configuracao.Section(configuracao.CurrentSection) ?? configuracao.Sections[0], configuracao.CurrentMode);
    }

    private static string N(double v) => v.ToString("0.###", CultureInfo.CurrentCulture);

    private void PreencherLista(DrainageSettings configuracao)
    {
        _carregando = true;
        var texto = _secoes.Text;
        _secoes.Items.Clear();
        foreach (var s in configuracao.Sections) _secoes.Items.Add(s.Name);
        _secoes.Text = texto;
        _carregando = false;
    }

    private void CarregarDaBiblioteca(string nome)
    {
        var configuracao = DrenagemStore.Ler(_documento.Database);
        if (configuracao.Section(nome) is { } s) Mostrar(s, _minima.IsChecked == true ? ChannelBottomMode.MinimumSlope : ChannelBottomMode.FollowTerrain);
    }

    /// <summary>Põe a seção nos campos.</summary>
    private void Mostrar(ChannelSection s, ChannelBottomMode modo)
    {
        _carregando = true;
        _secoes.Text = s.Name;
        (s.Kind == ChannelKind.HalfPipe ? _meiaCana : _trapezoidal).IsChecked = true;
        _b.Text = N(s.Base);
        _z.Text = N(s.SideSlope);
        _y.Text = N(s.Height);
        _r.Text = N(s.Radius);
        _e.Text = N(s.Thickness);
        _revestimento.Text = s.Lining;
        _n.Text = N(s.Manning);
        _vmax.Text = N(s.MaxVelocity);
        (modo == ChannelBottomMode.MinimumSlope ? _minima : _borda).IsChecked = true;
        _carregando = false;
        AplicarTipo();
        Redesenhar();
    }

    private void AplicarTipo()
    {
        var meiaCana = _meiaCana.IsChecked == true;
        _linhaTrapezio.Visibility = meiaCana ? Visibility.Collapsed : Visibility.Visible;
        _linhaRaio.Visibility = meiaCana ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A seção dos campos; medida que não é número vira o motivo.</summary>
    private ChannelSection Ler()
    {
        double Numero(TextBox caixa, string nome) =>
            NumberInput.TryParseMeasure(caixa.Text, out var v) ? v : throw new ArgumentException(Tr.F("{0}: \"{1}\" não é número.", nome, caixa.Text));

        var meiaCana = _meiaCana.IsChecked == true;

        var secao = new ChannelSection
        {
            Name = _secoes.Text.Trim(),
            Kind = meiaCana ? ChannelKind.HalfPipe : ChannelKind.Trapezoidal,
            Base = meiaCana ? 0 : Numero(_b, "b"),
            SideSlope = meiaCana ? 0 : Numero(_z, "z"),
            Height = meiaCana ? 0 : Numero(_y, "y"),
            Radius = meiaCana ? Numero(_r, "r") : 0,
            Thickness = Numero(_e, "e"),
            Lining = _revestimento.Text.Trim(),
            Manning = Numero(_n, "n"),
            MaxVelocity = Numero(_vmax, Tr.T("v máx")),
        };

        if (secao.Problem() is { } problema) throw new ArgumentException(problema);

        return secao;
    }

    /// <summary>O desenho da seção, cotado, no tamanho do quadro.</summary>
    private void Redesenhar()
    {
        if (_carregando) return;

        _desenho.Children.Clear();

        ChannelSection s;
        try
        {
            s = Ler() with { Name = "x" };
        }
        catch (ArgumentException erro)
        {
            _medidas.Text = erro.Message;
            return;
        }

        var u = ChannelSolid.UShape(s, 24);
        var (minX, maxX) = (u.Min(p => p.X), u.Max(p => p.X));
        var (minY, maxY) = (u.Min(p => p.Y), u.Max(p => p.Y));
        var escala = Math.Min((_desenho.Width - 70) / (maxX - minX), (_desenho.Height - 50) / (maxY - minY));
        var (ox, oy) = (_desenho.Width / 2, _desenho.Height - 22);

        Point P(double x, double y) => new(ox + x * escala, oy - (y - minY) * escala);

        var parede = new Polygon { Fill = new SolidColorBrush(Color.FromRgb(150, 150, 150)), Stroke = Brushes.White, StrokeThickness = 1 };
        foreach (var p in u) parede.Points.Add(P(p.X, p.Y));
        _desenho.Children.Add(parede);

        void Texto(string t, Point onde, Brush? cor = null)
        {
            var bloco = new TextBlock { Text = t, Foreground = cor ?? Brushes.Gold, FontSize = 11 };
            Canvas.SetLeft(bloco, onde.X);
            Canvas.SetTop(bloco, onde.Y);
            _desenho.Children.Add(bloco);
        }

        void Cota(Point a, Point b)
        {
            _desenho.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.Gold, StrokeThickness = 1, StrokeDashArray = [3, 2] });
        }

        if (s.Kind == ChannelKind.Trapezoidal)
        {
            var (b2, B2, h) = (s.Base / 2, s.TopWidth / 2, s.Height);
            Cota(P(-B2, h + 0.08 * (maxY - minY)), P(B2, h + 0.08 * (maxY - minY)));
            Texto(Tr.F("B {0}", N(s.TopWidth)), P(-0.12 * (maxX - minX), h + 0.25 * (maxY - minY)));
            Cota(P(-b2, 0), P(b2, 0));
            Texto(Tr.F("b {0}", N(s.Base)), P(-0.08 * (maxX - minX), 0.12 * (maxY - minY)));
            Cota(P(B2 + 0.05 * (maxX - minX), 0), P(B2 + 0.05 * (maxX - minX), h));
            Texto(Tr.F("y {0}", N(h)), P(B2 + 0.07 * (maxX - minX), 0.6 * h));
            Texto(Tr.F("x {0}", N(s.SideOpening)), P(b2 + 0.3 * s.SideOpening, 0.95 * h), Brushes.LightSkyBlue);
            Texto(Tr.F("e {0}", N(s.Thickness)), P(-b2, -0.02 * (maxY - minY)), Brushes.LightGray);
        }
        else
        {
            Cota(P(0, s.Radius), P(0, 0));
            Texto(Tr.F("r {0}", N(s.Radius)), P(0.03 * (maxX - minX), 0.6 * s.Radius));
            Cota(P(-s.Radius, s.Radius + 0.08 * (maxY - minY)), P(s.Radius, s.Radius + 0.08 * (maxY - minY)));
            Texto(Tr.F("B {0}", N(s.TopWidth)), P(-0.12 * (maxX - minX), s.Radius + 0.25 * (maxY - minY)));
            Texto(Tr.F("e {0}", N(s.Thickness)), P(-0.08 * (maxX - minX), -0.02 * (maxY - minY)), Brushes.LightGray);
        }

        _medidas.Text = Tr.F("Altura útil {0} m · abertura B {1} m · área molhada cheia {2} m² · concreto {3} m³/m",
            N(s.UsefulHeight), N(s.TopWidth), s.WetArea(s.UsefulHeight).ToString("0.####", CultureInfo.CurrentCulture), s.WallArea.ToString("0.####", CultureInfo.CurrentCulture));
    }

    private void Salvar() => Fazer(() =>
    {
        var secao = Ler();
        var configuracao = DrenagemStore.Mudar(_documento.Database, d => d.WithSection(secao) with { CurrentSection = secao.Name, CurrentMode = Modo });
        PreencherLista(configuracao);
        return Tr.F("Seção {0} salva na biblioteca do desenho.", secao.Name);
    });

    private void Excluir() => Fazer(() =>
    {
        var nome = _secoes.Text.Trim();
        var configuracao = DrenagemStore.Ler(_documento.Database);
        if (configuracao.Section(nome) is null) throw new ArgumentException(Tr.F("Não há a seção \"{0}\" na biblioteca.", nome));
        if (configuracao.Sections.Count == 1) throw new ArgumentException(Tr.T("A biblioteca precisa de pelo menos uma seção."));

        var novas = DrenagemStore.Mudar(_documento.Database, d => d with { Sections = d.Sections.Where(s => !string.Equals(s.Name, nome, StringComparison.OrdinalIgnoreCase)).ToList() });
        PreencherLista(novas);
        return Tr.F("Seção {0} tirada da biblioteca.", nome);
    });

    private ChannelBottomMode Modo => _minima.IsChecked == true ? ChannelBottomMode.MinimumSlope : ChannelBottomMode.FollowTerrain;

    private void Converter()
    {
        try
        {
            var secao = Ler();
            EscritaForaDeComando.Fazer(_documento, () => DrenagemStore.Mudar(_documento.Database, d => d.WithSection(secao) with { CurrentSection = secao.Name, CurrentMode = Modo }));
            PreencherLista(DrenagemStore.Ler(_documento.Database));

            // A seleção é no desenho: vai como comando (cancelando o que
            // estiver em curso), com a seção e o assentamento como padrão: um
            // Enter fecha o nome do comando, os outros dois aceitam os padrões.
            _documento.SendStringToExecute($"\x03\x03_{PluginInfo.ComandoDrenagemCanaletaConverter}\n\n\n", true, false, false);
            Recado(Tr.F("Escolha no desenho as linhas que viram canaleta {0}.", secao.Name), false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
    }

    private void Mandar(string comando)
    {
        try
        {
            _documento.SendStringToExecute($"\x03\x03_{comando} ", true, false, false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
    }

    private void Atualizar() => Fazer(() =>
    {
        var terreno = TerrenoDoDesenho.Garantir(_documento) ?? throw new ArgumentException(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));
        return LinhaCommands.AtualizarTodas(_documento, terreno.Mesh);
    });

    /// <summary>A seção de uma canaleta clicada (CLIVUS_DRN_CANALETA_LER) vem para a janela aberta.</summary>
    internal static void Carregar(Document documento, ChannelSection secao, ChannelBottomMode modo)
    {
        if (!Abertas.TryGetValue(documento, out var janela)) return;

        janela.Mostrar(secao, modo);
        janela.Recado(Tr.F("Seção da canaleta: {0}. Salve com outro nome para guardar uma variação.", secao.Name), false);
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

        var janela = new JanelaDeCanaleta(documento);
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

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    private void Falhou(Exception erro)
    {
        // Clique de WPF: exceção solta aqui derrubaria o Civil 3D.
        RegistroDeDiagnostico.Registrar("Falha na janela da canaleta.", erro);
        Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
    }

    /// <summary>Roda com o documento travado e o vigia calado (janela solta) e mostra o relato.</summary>
    private void Fazer(Func<string> operacao)
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;

            string frase;
            using (DrenagemVigia.Calar())
            {
                frase = EscritaForaDeComando.Fazer(_documento, () =>
                {
                    var f = operacao();
                    _documento.Editor.Regen();
                    return f;
                });
            }

            _documento.Editor.WriteMessage($"\n{frase}\n");
            Recado(frase, false);
        }
        catch (Exception erro)
        {
            Falhou(erro);
        }
        finally
        {
            Cursor = null;
        }
    }
}
