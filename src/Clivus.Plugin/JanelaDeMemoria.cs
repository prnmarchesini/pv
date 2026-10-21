using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// Memória › Cálculo (31.2 a 31.5): os parâmetros do projeto (padrão: o
/// memorial de Valparaiso), as tabelas do memorial em abas, os alertas, o
/// Excel e a altura sugerida. Recalcula sempre antes de mostrar. Solta, como
/// as outras janelas da drenagem; parâmetros em duas colunas (regra de
/// 02/10/2026: nada de espaço vazio).
/// </summary>
internal sealed class JanelaDeMemoria : Window
{
    private static readonly Dictionary<Document, JanelaDeMemoria> Abertas = [];

    private readonly Document _documento;
    private readonly TextBox _fonte = Campo(Tr.T("De onde vêm K, a, b e c (ex. Plúvio 2.1, cidade)."), 190);
    private readonly TextBox _k = Campo("K");
    private readonly TextBox _a = Campo("a");
    private readonly TextBox _b = Campo("b");
    private readonly TextBox _c = Campo("c");
    private readonly TextBox _t = Campo(Tr.T("Período de retorno, em anos."));
    private readonly TextBox _tc = Campo(Tr.T("Tempo de concentração mínimo, em minutos."));
    private readonly TextBox _kerby = Campo(Tr.T("Rugosidade de Kerby (0,5 no memorial)."));
    private readonly ComboBox _superficie = new() { Width = 190, ToolTip = Tr.T("A superfície da Tabela 39 do DNIT: dá a faixa do coeficiente C.") };
    private readonly TextBox _cEsc = Campo(Tr.T("Coeficiente de escoamento C do método racional."));
    private readonly ComboBox _cobertura = new() { Width = 190, ToolTip = Tr.T("A cobertura do terreno (Tabela 31 do DNIT): dá a velocidade máxima do escoamento superficial.") };
    private readonly TextBox _vCobertura = Campo(Tr.T("Velocidade máxima da cobertura, em m/s."));
    private readonly TextBox _vMin = Campo(Tr.T("Velocidade mínima de autolimpeza na canaleta, em m/s."));
    private readonly TextBox _iMin = Campo(Tr.T("Declividade mínima da canaleta, em m/m (NBR 10844: 0,005)."));
    private readonly TextBox _passo = Campo(Tr.T("Passo do arredondamento da altura sugerida, em m (0,05; 0,10 reproduz o memorial)."));
    private readonly TextBox _folga = Campo(Tr.T("Folga total da laje da transposição sobre a abertura, em m."));
    private readonly TextBox _vias = Campo(Tr.T("As camadas das vias, separadas por ponto e vírgula: onde a canaleta cruza, entra transposição."), 190);
    private readonly CheckBox _bacias = new() { Content = Tr.T("Recalcular as bacias antes"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), ToolTip = Tr.T("Calcula de novo as bacias (com a região gravada) para a memória usar as canaletas como estão agora.") };
    private readonly TabControl _abas = new() { Height = 300, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private static TextBox Campo(string dica, double largura = 70) => new() { Width = largura, ToolTip = dica };

    private JanelaDeMemoria(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Memória de cálculo da drenagem");
        Width = 900;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var (nome, _, _) in HydrologyParameters.RunoffSurfaces) _superficie.Items.Add(nome);
        foreach (var (nome, _, _) in HydrologyParameters.Covers) _cobertura.Items.Add(nome);

        StackPanel Linha(params UIElement[] itens)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
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

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => acao();
            return b;
        }

        var esquerda = new StackPanel { Width = 420 };
        esquerda.Children.Add(Linha(R(Tr.T("Chuva (IDF)"), 120), _fonte));
        esquerda.Children.Add(Linha(R("K · a", 120), _k, R(""), _a));
        esquerda.Children.Add(Linha(R("b · c", 120), _b, R(""), _c));
        esquerda.Children.Add(Linha(R(Tr.T("Retorno · tc mín."), 120), _t, R(Tr.T("anos")), _tc, R("min")));
        esquerda.Children.Add(Linha(R(Tr.T("Kerby (N)"), 120), _kerby));

        var direita = new StackPanel();
        direita.Children.Add(Linha(R(Tr.T("Superfície (C)"), 120), _superficie, R(""), _cEsc));
        direita.Children.Add(Linha(R(Tr.T("Cobertura"), 120), _cobertura, R(""), _vCobertura, R("m/s")));
        direita.Children.Add(Linha(R(Tr.T("V mín. · I mín."), 120), _vMin, R("m/s"), _iMin, R("m/m")));
        direita.Children.Add(Linha(R(Tr.T("Passo · folga TSS"), 120), _passo, R("m"), _folga, R("m")));
        direita.Children.Add(Linha(R(Tr.T("Camadas das vias"), 120), _vias));

        var parametros = new StackPanel { Orientation = Orientation.Horizontal };
        parametros.Children.Add(esquerda);
        parametros.Children.Add(direita);

        _superficie.SelectionChanged += (_, _) =>
        {
            if (_superficie.SelectedItem is not string nome) return;
            var (_, min, max) = HydrologyParameters.RunoffSurfaces.First(s => s.Name == nome);
            _cEsc.ToolTip = Tr.F("Faixa da Tabela 39 do DNIT: {0:0.00} a {1:0.00}.", min, max);
        };
        _cobertura.SelectionChanged += (_, _) =>
        {
            if (_cobertura.SelectedItem is not string nome) return;
            _vCobertura.Text = HydrologyParameters.Covers.First(c => c.Name == nome).Max.ToString("0.##", CultureInfo.CurrentCulture);
        };

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(parametros);

        var botoes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        botoes.Children.Add(_bacias);
        var calcular = Botao(Tr.T("Calcular"), Tr.T("Grava os parâmetros e calcula a memória das canaletas do desenho."), () => Calcular());
        calcular.IsDefault = true;
        botoes.Children.Add(calcular);
        botoes.Children.Add(Botao(Tr.T("Exportar Excel…"), Tr.T("Uma aba por tabela do memorial, mais parâmetros e alertas."), Exportar));
        botoes.Children.Add(Botao(Tr.T("Aplicar altura sugerida…"), Tr.T("Escolha as canaletas: a altura (ou o raio) da seção vira a sugerida e o 3D é refeito. Ação sua, nunca automática."), () => Mandar(PluginInfo.ComandoDrenagemMemoriaAltura)));
        botoes.Children.Add(Botao(Tr.T("Valores do memorial"), Tr.T("Volta os parâmetros ao memorial de Valparaiso (não grava até Calcular)."), () => Mostrar(new HydrologyParameters())));
        var fechar = Botao(Tr.T("Fechar"), Tr.T("Fecha a janela."), Close);
        fechar.IsCancel = true;
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        pilha.Children.Add(_abas);
        pilha.Children.Add(_recado);

        Content = pilha;

        Mostrar(DrenagemStore.Ler(documento.Database).Hydrology);
    }

    private static string N(double v) => v.ToString("0.#####", CultureInfo.CurrentCulture);

    private void Mostrar(HydrologyParameters p)
    {
        _fonte.Text = p.Source;
        _k.Text = N(p.K);
        _a.Text = N(p.A);
        _b.Text = N(p.B);
        _c.Text = N(p.C);
        _t.Text = N(p.ReturnPeriod);
        _tc.Text = N(p.MinimumTc);
        _kerby.Text = N(p.KerbyN);
        _superficie.SelectedItem = HydrologyParameters.RunoffSurfaces.Any(s => s.Name == p.RunoffSurface) ? p.RunoffSurface : null;
        _cEsc.Text = N(p.RunoffC);
        _cobertura.SelectedItem = HydrologyParameters.Covers.Any(c => c.Name == p.Cover) ? p.Cover : null;
        _vCobertura.Text = N(p.CoverMaxVelocity);
        _vMin.Text = N(p.MinimumVelocity);
        _iMin.Text = N(p.MinimumSlope);
        _passo.Text = N(p.HeightStep);
        _folga.Text = N(p.CrossingMargin);
        _vias.Text = p.RoadLayers;
    }

    /// <summary>Os parâmetros dos campos; o que não for número vira o motivo.</summary>
    private HydrologyParameters Ler()
    {
        double Numero(TextBox caixa, string nome) =>
            NumberInput.TryParseMeasure(caixa.Text, out var v) ? v : throw new ArgumentException(Tr.F("{0}: \"{1}\" não é número.", nome, caixa.Text));

        var p = new HydrologyParameters
        {
            Source = _fonte.Text.Trim(),
            K = Numero(_k, "K"),
            A = Numero(_a, "a"),
            B = Numero(_b, "b"),
            C = Numero(_c, "c"),
            ReturnPeriod = Numero(_t, Tr.T("Retorno")),
            MinimumTc = Numero(_tc, Tr.T("tc mín.")),
            KerbyN = Numero(_kerby, "N"),
            RunoffSurface = _superficie.SelectedItem as string ?? string.Empty,
            RunoffC = Numero(_cEsc, "C"),
            Cover = _cobertura.SelectedItem as string ?? string.Empty,
            CoverMaxVelocity = Numero(_vCobertura, Tr.T("Cobertura")),
            MinimumVelocity = Numero(_vMin, Tr.T("V mín.")),
            MinimumSlope = Numero(_iMin, Tr.T("I mín.")),
            HeightStep = Numero(_passo, Tr.T("Passo")),
            CrossingMargin = Numero(_folga, Tr.T("Folga")),
            RoadLayers = _vias.Text.Trim(),
        };

        if (p.Problem() is { } problema) throw new ArgumentException(problema);
        return p;
    }

    /// <param name="gravar">
    /// Grava os parâmetros da tela no desenho antes. Ao abrir a janela não:
    /// só abrir não pode deixar o desenho modificado.
    /// </param>
    private void Calcular(bool gravar = true)
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            var parametros = Ler();
            var terreno = TerrainCache.Get(_documento) ?? throw new ArgumentException(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));
            var frases = new List<string>();
            DrainageMemoryResult memoria;

            using (DrenagemVigia.Calar())
            {
                memoria = EscritaForaDeComando.Fazer(_documento, () =>
                {
                    if (gravar) DrenagemStore.Mudar(_documento.Database, d => d with { Hydrology = parametros });

                    if (_bacias.IsChecked == true)
                    {
                        // As bacias podem demorar: com andamento e Cancelar, como na janela delas.
                        var andamento = JanelaDeProgresso.Abrir(this, Tr.T("Bacias de contribuição"), podeCancelar: true);
                        try
                        {
                            frases.Add(BaciasCommands.CalcularComARegiaoGravada(_documento, terreno.Mesh, p =>
                            {
                                andamento.Avancar(p * 100, Tr.T("Descendo a água de cada célula…"));
                                return !andamento.Cancelado;
                            }));
                        }
                        catch (OperationCanceledException)
                        {
                            frases.Add(Tr.T("BACIAS Cancelado; as bacias anteriores ficaram."));
                        }
                        catch (ArgumentException erro)
                        {
                            frases.Add(Tr.F("BACIAS {0}", erro.Message));
                        }
                        finally
                        {
                            andamento.Fechar();
                        }
                    }

                    return MemoriaDeDrenagem.Montar(_documento.Database, terreno.Mesh, parametros);
                });
            }

            Preencher(memoria);

            var alertas = memoria.Alerts.Count();
            frases.Add(Tr.F("MEMÓRIA {0} canaleta(s) calculada(s), {1} alerta(s).", memoria.Channels.Count, alertas));
            foreach (var f in frases) _documento.Editor.WriteMessage($"\n{f}\n");

            _recado.Foreground = alertas > 0 ? Brushes.DarkOrange : Brushes.ForestGreen;
            _recado.Text = string.Join(" ", frases);
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

    /// <summary>Uma aba por tabela; a dos alertas em vermelho quando há algum.</summary>
    private void Preencher(DrainageMemoryResult memoria)
    {
        var selecionada = _abas.SelectedIndex;
        _abas.Items.Clear();

        foreach (var tabela in DrainageMemory.Tables(memoria))
        {
            var dados = new DataTable();
            for (var i = 0; i < tabela.Header.Count; i++) dados.Columns.Add(new DataColumn($"c{i}", typeof(string)) { Caption = tabela.Header[i] });

            string Texto(object? v) => v switch
            {
                null => string.Empty,
                double d => d.ToString("0.####", CultureInfo.CurrentCulture),
                _ => Convert.ToString(v, CultureInfo.CurrentCulture) ?? string.Empty,
            };

            foreach (var linha in tabela.Rows) dados.Rows.Add(linha.Select(Texto).Cast<object>().ToArray());
            if (tabela.Total.Count > 0) dados.Rows.Add(tabela.Total.Select(Texto).Cast<object>().ToArray());

            var grade = new DataGrid
            {
                ItemsSource = dados.DefaultView,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                AutoGenerateColumns = true,
            };
            grade.AutoGeneratingColumn += (_, e) => e.Column.Header = dados.Columns[e.PropertyName]?.Caption ?? e.PropertyName;

            var conteudo = new DockPanel();
            var notas = new TextBlock { Text = string.Join(" ", tabela.Notes), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(notas, Dock.Bottom);
            conteudo.Children.Add(notas);
            conteudo.Children.Add(grade);

            var aba = new TabItem { Header = tabela.Title, Content = conteudo };
            if (tabela.Title == Tr.T("Alertas") && tabela.Rows.Count > 0) aba.Foreground = Brushes.Firebrick;
            _abas.Items.Add(aba);
        }

        _abas.SelectedIndex = selecionada >= 0 && selecionada < _abas.Items.Count ? selecionada : 0;
    }

    private void Exportar()
    {
        try
        {
            var terreno = TerrainCache.Get(_documento) ?? throw new ArgumentException(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));
            var nome = Path.GetFileNameWithoutExtension(_documento.Name);
            if (string.IsNullOrWhiteSpace(nome)) nome = "usina";

            var caminho = DialogoDeArquivo.Salvar(Tr.T("Memória de cálculo da drenagem"), Tr.T("Pasta de trabalho do Excel (*.xlsx)|*.xlsx"),
                nome + " - drenagem.xlsx", _documento.IsNamedDrawing ? Path.GetDirectoryName(_documento.Name) : null);
            if (caminho is null) return;

            var frase = MemoriaDeDrenagem.Exportar(_documento, terreno.Mesh, caminho);
            _documento.Editor.WriteMessage($"\n{frase}\n");
            _recado.Foreground = Brushes.ForestGreen;
            _recado.Text = frase;
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

    private void Falhou(Exception erro)
    {
        // Clique de WPF: exceção solta aqui derrubaria o Civil 3D.
        RegistroDeDiagnostico.Registrar("Falha na janela da memória da drenagem.", erro);
        _recado.Foreground = Brushes.Firebrick;
        _recado.Text = erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message);
    }

    /// <summary>Abre a janela do desenho (já calculando), ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeMemoria(documento);
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

        // Sem bacias recalculadas na abertura (pode demorar): mostra a memória com as gravadas.
        janela._bacias.IsChecked = false;
        janela.Calcular(gravar: false);
        janela._bacias.IsChecked = true;
    }
}
