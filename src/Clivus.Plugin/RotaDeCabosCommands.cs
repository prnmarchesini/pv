using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RotaDeCabosCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ROTA_CABOS (roteamento, 17.1): a janela da rota de cabos, com as
/// abas CC, Combiner, CA e MT (e o Resumo, etapa 24). Cada aba de rota só
/// fica habilitada quando os dois lados do trecho existem no desenho; a
/// indisponível diz o que falta. A regra é do Core (<see cref="CableRoutes"/>);
/// aqui só se lê o desenho e se mostra. Sem interface (Core Console), o que
/// cada aba precisa vai para a linha de comando.
/// </summary>
public static class RotaDeCabosCommands
{
    [CommandMethod(PluginInfo.ComandoRotaCabos)]
    public static void RotaCabos()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                var (desenho, problema) = Ler(documento);
                Escrever(documento.Editor, desenho, problema);
                return;
            }

            JanelaDeRotaDeCabos.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a rota de cabos.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a rota de cabos: {0}\n", erro.Message));
        }
    }

    /// <summary>Uma linha por aba: "ROTA CC: disponível" ou o que falta; e o cadastro ilegível, se houver.</summary>
    private static void Escrever(Editor editor, CableRouteDrawing desenho, string? problema)
    {
        if (problema is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}", problema));

        foreach (var rota in CableRoutes.All)
        {
            var falta = CableRoutes.Missing(rota, desenho);
            editor.WriteMessage(falta.Count == 0
                ? Tr.F("\nROTA {0}: disponível.", CableRoutes.Title(rota))
                : Tr.F("\nROTA {0}: falta {1}.", CableRoutes.Title(rota), string.Join("; ", falta)));
        }

        editor.WriteMessage("\n");
    }

    /// <summary>
    /// O que o desenho tem: as strings desenhadas (XData), o cadastro e o que
    /// dele está em campo (a conta é do Core); e o problema de leitura do
    /// cadastro, se houver, para não dizer "não há nenhum" calado.
    /// </summary>
    internal static (CableRouteDrawing Desenho, string? Problema) Ler(Document documento)
    {
        var database = documento.Database;
        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        return (CableRoutes.Drawing(setup, StringsDoDesenho.Ler(database).Count, EquipamentoEmCampo.EmCampo(database)), problema);
    }
}

/// <summary>
/// A janela da rota de cabos: uma por desenho. O que pede clique em campo
/// (selecionar vala, gerar, apagar, forçar lado) esconde a janela e roda o
/// comando (<see cref="RotaDeCabosCampo"/>), que a devolve no fim (<see cref="Voltar"/>).
/// </summary>
internal sealed class JanelaDeRotaDeCabos : Window
{
    private static readonly Dictionary<Document, JanelaDeRotaDeCabos> Abertas = [];

    private readonly Document _documento;
    private readonly TabControl _abas = new() { Margin = new Thickness(8) };
    private readonly Dictionary<CableRoute, TabItem> _itens = [];
    private readonly Dictionary<CableRoute, AbaDeRota> _paginas = [];
    private readonly AbaResumoDeCabos _resumo;
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 8, 8) };

    private JanelaDeRotaDeCabos(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Rota de cabos — Clivus Solar");
        Width = 1080;
        Height = 720;
        MinWidth = 720;
        MinHeight = 480;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        foreach (var rota in CableRoutes.All)
        {
            var pagina = new AbaDeRota(documento, rota) { AoMudar = Atualizar };
            var item = new TabItem { Header = CableRoutes.Title(rota), Content = pagina };
            // A dica da aba desabilitada diz o que falta: sem isto o WPF não a mostra.
            ToolTipService.SetShowOnDisabled(item, true);
            _itens[rota] = item;
            _paginas[rota] = pagina;
            _abas.Items.Add(item);
        }

        _resumo = new AbaResumoDeCabos(documento);
        _abas.Items.Add(new TabItem { Header = Tr.T("Resumo"), ToolTip = Tr.T("Os cabos da usina inteira: metros por rota, por cabo e por polaridade, e a lista de material."), Content = _resumo });

        var raiz = new DockPanel();
        DockPanel.SetDock(_recado, Dock.Bottom);
        raiz.Children.Add(_recado);
        raiz.Children.Add(_abas);
        Content = raiz;

        // Voltar para a janela relê só quais abas abrem (o usuário pode ter posto
        // um trafo em campo); os formulários ficam como estão, com o que foi digitado.
        Activated += (_, _) => Atualizar(paginas: false);
        Atualizar();
    }

    /// <summary>Relê o desenho: quais abas abrem e o que cada uma mostra (as tabelas só no "Ver cabos", que reconta).</summary>
    internal void Atualizar() => Atualizar(paginas: true);

    private void Atualizar(bool paginas)
    {
        try
        {
            var (desenho, problema) = RotaDeCabosCommands.Ler(_documento);
            var indisponiveis = new List<string>();
            if (problema is not null) indisponiveis.Add(Tr.F("ATENÇÃO: {0}", problema));

            foreach (var rota in CableRoutes.All)
            {
                var falta = CableRoutes.Missing(rota, desenho);
                var item = _itens[rota];
                var livre = falta.Count == 0;

                item.IsEnabled = livre;
                item.ToolTip = livre ? CableRoutes.Description(rota) : Tr.F("Falta: {0}.", string.Join("; ", falta));
                if (!livre) indisponiveis.Add(Tr.F("{0}: falta {1}.", CableRoutes.Title(rota), string.Join("; ", falta)));
                else if (paginas) _paginas[rota].Atualizar();
            }

            // A aba aberta não pode ficar numa desabilitada: vai para a primeira livre.
            if (_abas.SelectedItem is not TabItem { IsEnabled: true })
                _abas.SelectedItem = CableRoutes.All.Select(r => _itens[r]).FirstOrDefault(i => i.IsEnabled) ?? _abas.Items[^1];

            _recado.Foreground = indisponiveis.Count == 0 ? Brushes.ForestGreen : Brushes.Firebrick;
            _recado.Text = indisponiveis.Count == 0 ? Tr.T("Todas as rotas estão disponíveis.") : string.Join("\n", indisponiveis);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar a rota de cabos.", erro);
            _recado.Foreground = Brushes.Firebrick;
            _recado.Text = Tr.F("Não consegui ler o desenho: {0}", erro.Message);
        }
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente (atualizada) a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            aberta.Mostrar();
            return;
        }

        var janela = new JanelaDeRotaDeCabos(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            if (e.Document == documento) janela.Close();
        }

        void Soltar()
        {
            Abertas.Remove(documento);
            AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) => Soltar();

        try
        {
            AcadApp.ShowModelessWindow(janela);
        }
        catch
        {
            // A janela nunca apareceu: sem isto, o próximo comando só ativaria uma janela invisível.
            Soltar();
            throw;
        }
    }

    private void Mostrar()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Atualizar();
        Activate();
    }

    /// <summary>Depois de um comando de campo: a janela (se existe) volta e relê. Sem janela aberta (comando digitado), nada.</summary>
    internal static void Voltar(Document documento)
    {
        try
        {
            if (Abertas.TryGetValue(documento, out var janela)) janela.Mostrar();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao devolver a janela da rota de cabos.", erro);
        }
    }

    /// <summary>Esconde a janela e manda o comando de campo (o argumento responde às primeiras perguntas).</summary>
    internal static void Campo(Document documento, string comando, string argumento)
    {
        if (Abertas.TryGetValue(documento, out var janela)) janela.Hide();
        // Os dois ESC cancelam um comando que estivesse no meio.
        documento.SendStringToExecute($"\x03\x03_{comando} {argumento}\n", true, false, false);
    }
}

/// <summary>Mostra as tabelas do memorial: uma lista por tabela, com o rodapé de totais e as notas.</summary>
internal static class TabelaNaTela
{
    internal static void Mostrar(Panel onde, IEnumerable<CableTable> tabelas)
    {
        onde.Children.Clear();

        foreach (var t in tabelas)
        {
            onde.Children.Add(new TextBlock { Text = t.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });

            var grade = new GridView();
            for (var i = 0; i < t.Header.Count; i++)
                grade.Columns.Add(new GridViewColumn { Header = t.Header[i], DisplayMemberBinding = new System.Windows.Data.Binding($"[{i}]") });

            var linhas = t.Rows.Select(r => r.Select(Texto).ToArray()).ToList();
            if (t.Total.Count > 0) linhas.Add(t.Total.Select(Texto).ToArray());

            onde.Children.Add(new ListView { View = grade, ItemsSource = linhas, MaxHeight = 420 });

            foreach (var nota in t.Notes)
                onde.Children.Add(new TextBlock { Text = "• " + nota, Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
        }
    }

    internal static string Texto(object? c) => c switch
    {
        null => string.Empty,
        double d when !double.IsFinite(d) => string.Empty,
        double d => d.ToString("0.##", Tr.Culture),
        _ => Convert.ToString(c, Tr.Culture) ?? string.Empty,
    };
}

/// <summary>
/// Uma aba de rota (CC, Combiner, CA, MT), todas com a mesma mecânica e
/// cada uma com os seus valores: profundidade da vala (17.3), raio, cabo e
/// método (22.2, 22.5); selecionar vala (17.4); gerar e apagar (17.9);
/// forçar lado (18.3, só CC e Combiner); módulo PAN (22.3, no CC); e o
/// Ver cabos, que reconta antes de mostrar (17.8).
/// </summary>
internal sealed class AbaDeRota : AbaEletrica
{
    private static readonly string[] Metodos = ["A1", "A2", "B1", "B2", "C", "D", "E", "F", "G"];

    private readonly CableRoute _rota;
    private readonly TextBox _profundidade;
    private readonly TextBox _raio;
    private readonly TextBox? _alcance;
    private readonly TextBox? _fatorDePotencia;
    private readonly CheckBox? _trifasico;
    private readonly ComboBox _metodo;
    private readonly ComboBox _cabo;
    private readonly TextBlock _valas = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) };
    private readonly TextBlock _modulos = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly StackPanel _tabelas = new();
    private List<CableTable> _ultimas = [];

    internal AbaDeRota(Document documento, CableRoute rota) : base(documento)
    {
        _rota = rota;
        var cadeia = rota is CableRoute.DirectCurrent or CableRoute.Combiner;
        var pilha = new StackPanel { Margin = new Thickness(4) };

        Titulo(pilha, Tr.T("Vala e cabo"));
        var grade = Grade(330);
        _profundidade = Campo(grade, Tr.T("Profundidade da vala (m)"), Tr.T("O cabo desce até esta profundidade na vala; entra no comprimento contado."));
        _raio = Campo(grade, Tr.T("Raio de busca da vala em volta do equipamento (m)"), Tr.T("A vala mais perto do equipamento dentro deste raio; fora dele, o equipamento fica sem rota e é pintado."));
        if (cadeia) _alcance = Campo(grade, Tr.T("Alcance da reta do fim da fileira até a vala (m)"), Tr.T("Do fim da fileira o cabo vai reto até bater na vala; sem vala neste alcance, a mesa fica sem rota e é pintada."));
        else _fatorDePotencia = Campo(grade, Tr.T("Fator de potência"), Tr.T("Para a corrente e a queda de tensão."));
        _metodo = Escolha(grade, Tr.T("Método de instalação (NBR 5410)"), Tr.T("A corrente admissível mostrada é a deste método para o cabo escolhido (da biblioteca)."));
        _metodo.IsEditable = true;
        foreach (var m in Metodos) _metodo.Items.Add(m);
        _cabo = Escolha(grade, Tr.T("Cabo"), Tr.T("Da biblioteca de cabos. Trocar o cabo não redesenha: só refaz as contas da tabela."));
        pilha.Children.Add(grade);
        if (rota == CableRoute.AlternatingCurrent)
        {
            _trifasico = new CheckBox { Content = Tr.T("Trifásico (desmarcado: monofásico)"), ToolTip = Tr.T("A corrente e a queda do inversor ao trafo: trifásicas ou monofásicas."), Margin = new Thickness(0, 0, 0, 6) };
            pilha.Children.Add(_trifasico);
        }

        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Salvar"), Tr.T("Grava os valores desta aba no desenho (as outras abas não mudam)."), () => Salvar());
        Botao(botoes, Tr.T("Biblioteca de cabos..."), Tr.T("Os cabos com as resistências e a corrente admissível por método. Valores de partida: revise."), Biblioteca);
        pilha.Children.Add(botoes);

        Titulo(pilha, Tr.T("Vala"));
        var linhaDaVala = new WrapPanel();
        linhaDaVala.Children.Add(_valas);
        Botao(linhaDaVala, Tr.T("Selecionar vala"), Tr.T("A janela some; clique nas polilinhas que são vala desta rota (Shift+clique tira; Enter termina). Elas vão para a camada da vala da rota; o traçado não muda."),
            () => Comando(PluginInfo.ComandoRotaVala, _rota.ToString()));
        pilha.Children.Add(linhaDaVala);

        Titulo(pilha, Tr.T("Cabos"));
        var acoes = new WrapPanel();
        Botao(acoes, Tr.T("Gerar"), Tr.T("Apaga os cabos desta rota e desenha de novo, pelas valas. O que não der para rotear é avisado e pintado de vermelho."),
            () => Comando(PluginInfo.ComandoRotaGerar, _rota.ToString()));
        Botao(acoes, Tr.T("Apagar tudo"), Tr.T("Apaga todos os cabos desta rota. Só cabo: strings, valas e mesas ficam."),
            () => Comando(PluginInfo.ComandoRotaApagar, _rota + " Tudo"));
        Botao(acoes, Tr.T("Apagar escolhendo"), Tr.T("Escolha em campo os cabos desta rota a apagar."),
            () => Comando(PluginInfo.ComandoRotaApagar, _rota + " Selecionar"));
        if (cadeia)
            Botao(acoes, Tr.T("Forçar lado"), Tr.T("Escolha strings em campo e clique do lado (ponta da fileira) para onde os cabos delas vão. Fica gravado; para voltar ao automático, digite A no lugar do clique."),
                () => Comando(PluginInfo.ComandoRotaLado, _rota.ToString()));
        pilha.Children.Add(acoes);

        if (rota == CableRoute.DirectCurrent)
        {
            Titulo(pilha, Tr.T("Módulo (arquivo PAN do PVsyst) e temperaturas"));
            pilha.Children.Add(_modulos);
            var pan = new WrapPanel();
            Botao(pan, Tr.T("Carregar .PAN..."), Tr.T("Lê Voc, Isc, Vmp, Imp, potência e os coeficientes de temperatura. Se faltar campo, diz qual; nunca inventa valor."), CarregarPan);
            Botao(pan, Tr.T("Tirar os PAN"), Tr.T("Tira do desenho os módulos lidos de PAN."), TirarPan);
            pilha.Children.Add(pan);
        }

        Titulo(pilha, Tr.T("Ver cabos"));
        var ver = new WrapPanel();
        Botao(ver, Tr.T("Ver cabos (reconta)"), Tr.T("Reconta o desenho antes de mostrar: cabo apagado com Delete por fora do plugin sai da conta e a origem dele é pintada."), VerCabos);
        Botao(ver, Tr.T("Exportar CSV"), Tr.T("Grava a tabela (com os totais) num CSV que o Excel abre."), ExportarCsv);
        pilha.Children.Add(ver);
        pilha.Children.Add(_tabelas);

        Children.Add(new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
    }

    private static void Titulo(Panel onde, string texto) =>
        onde.Children.Add(new TextBlock { Text = texto, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });

    private static string CaminhoDaBiblioteca => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, CableLibrary.FileName);

    internal override void Atualizar()
    {
        var db = Documento.Database;
        var config = RotaDeCabosStore.Configuracoes(db, out var problema)[_rota];

        _profundidade.Text = Numero(config.Depth);
        _raio.Text = Numero(config.Radius);
        if (_alcance is not null) _alcance.Text = Numero(config.Reach);
        if (_fatorDePotencia is not null) _fatorDePotencia.Text = Numero(config.PowerFactor);
        if (_trifasico is not null) _trifasico.IsChecked = config.ThreePhase;
        _metodo.Text = config.Method;

        _cabo.Items.Clear();
        _cabo.Items.Add(new ComboBoxItem { Content = Tr.T("(nenhum)"), Tag = null });
        var biblioteca = CableLibrary.Load(CaminhoDaBiblioteca, out _).Where(c => c.Type == CableLibrary.TypeFor(_rota)).ToList();
        if (config.Cable is { } doDesenho && biblioteca.All(c => c.Id != doDesenho.Id)) biblioteca.Insert(0, doDesenho);
        foreach (var c in biblioteca)
        {
            var item = new ComboBoxItem { Content = c.Name, Tag = c };
            _cabo.Items.Add(item);
            if (config.Cable?.Id == c.Id) _cabo.SelectedItem = item;
        }

        if (_cabo.SelectedItem is null) _cabo.SelectedIndex = 0;

        _valas.Text = Tr.F("{0} vala(s) desta rota no desenho.", RotaDeCabosStore.QuantasValas(db)[_rota]);

        if (_rota == CableRoute.DirectCurrent)
        {
            var pans = RotaDeCabosStore.ModulosPan(db);
            var projeto = SettingsStore.Load(db).Settings ?? ProjectSettings.Default;
            _modulos.Text = (pans.Count == 0
                    ? Tr.T("Nenhum módulo lido de PAN: as tensões, correntes e quedas ficam em branco.")
                    : string.Join("\n", pans.Select(p => Tr.F("{0} {1}: Voc {2} V, Isc {3} A, Vmp {4} V, Imp {5} A, β Voc {6} V/°C", p.Manufacturer, p.Model, p.Voc, p.Isc, p.Vmp, p.Imp, p.VocCoefficient))))
                + "\n" + Tr.F("Temperaturas (em Configurações): mínima {0} °C, máxima {1} °C.", projeto.MinTemperature, projeto.MaxTemperature);
        }

        if (RotaDeCabosCampo.Relatorios.Remove((Documento, _rota), out var relatorio))
        {
            LimparTabelas();
            Avisar(relatorio);
        }
        else if (problema is not null)
        {
            Avisar(Tr.F("ATENÇÃO: {0}", problema), erro: true);
        }
    }

    /// <summary>
    /// O comando de campo usa o que está GRAVADO: o que foi digitado e não
    /// salvo é salvo antes (senão o Gerar usaria o valor velho e a tela
    /// voltaria a ele, parecendo que gerou com o digitado). Recusado, não roda.
    /// </summary>
    private void Comando(string comando, string argumento)
    {
        if (Formulario() is not { } digitado) return;
        if (digitado != RotaDeCabosStore.Configuracao(Documento.Database, _rota) && !Salvar()) return;
        JanelaDeRotaDeCabos.Campo(Documento, comando, argumento);
    }

    /// <summary>A configuração como está na tela; null (e o recado) se algum número não se lê ou não serve.</summary>
    private RouteSettings? Formulario()
    {
        if (!NumberInput.TryParseMeasure(_profundidade.Text, out var profundidade) || !NumberInput.TryParseMeasure(_raio.Text, out var raio))
        {
            Avisar(Tr.T("Não consigo ler a profundidade ou o raio."), erro: true);
            return null;
        }

        var atual = RotaDeCabosStore.Configuracao(Documento.Database, _rota);
        var alcance = atual.Reach;
        var fp = atual.PowerFactor;
        if (_alcance is not null && !NumberInput.TryParseMeasure(_alcance.Text, out alcance) || _fatorDePotencia is not null && !NumberInput.TryParseMeasure(_fatorDePotencia.Text, out fp))
        {
            Avisar(Tr.T("Não consigo ler o alcance ou o fator de potência."), erro: true);
            return null;
        }

        var nova = atual with
        {
            Depth = profundidade,
            Radius = raio,
            Reach = alcance,
            PowerFactor = fp,
            Method = (_metodo.Text ?? string.Empty).Trim().ToUpperInvariant(),
            Cable = (_cabo.SelectedItem as ComboBoxItem)?.Tag as Cable,
            ThreePhase = _trifasico?.IsChecked ?? atual.ThreePhase,
        };

        if (nova.WhyInvalid() is { } porque)
        {
            Avisar(Tr.F("Não salvei: {0}.", porque), erro: true);
            return null;
        }

        return nova;
    }

    private bool Salvar()
    {
        if (Formulario() is not { } nova) return false;

        var gravou = Gravar(() => RotaDeCabosStore.GravarConfiguracao(Documento.Database, nova) is { } problema
            ? throw new InvalidOperationException(problema)
            : Tr.F("Aba {0} salva. Trocar o cabo não redesenha: use Ver cabos para a tabela com as contas novas.", CableRoutes.Title(_rota)));
        if (gravou) LimparTabelas();
        return gravou;
    }

    private void LimparTabelas()
    {
        _tabelas.Children.Clear();
        _ultimas = [];
    }

    private void Biblioteca()
    {
        var janela = new JanelaDeCabos(CaminhoDaBiblioteca);
        AcadApp.ShowModalWindow(janela);
        Atualizar();
    }

    private void CarregarPan()
    {
        var caminho = DialogoDeArquivo.Abrir(Tr.T("Arquivo PAN do módulo"), Tr.T("Módulo do PVsyst (*.pan)|*.pan|Todos os arquivos (*.*)|*.*"), null);
        if (caminho is null) return;

        var texto = System.IO.File.ReadAllText(caminho, System.Text.Encoding.Latin1);
        if (PanModule.Parse(texto, out var faltam) is not { } modulo)
        {
            Avisar(Tr.F("Não li o PAN {0}: faltou ou está ilegível {1}. Nenhum valor foi inventado.", System.IO.Path.GetFileName(caminho), string.Join(", ", faltam)), erro: true);
            return;
        }

        Fazer(() =>
        {
            var lista = RotaDeCabosStore.ModulosPan(Documento.Database);
            lista.RemoveAll(p => string.Equals(p.Model, modulo.Model, StringComparison.OrdinalIgnoreCase));
            lista.Add(modulo);
            RotaDeCabosStore.GravarModulosPan(Documento.Database, lista);
            return Tr.F("PAN lido: {0} {1}.", modulo.Manufacturer, modulo.Model);
        });
    }

    private void TirarPan() => Fazer(() =>
    {
        RotaDeCabosStore.GravarModulosPan(Documento.Database, []);
        return Tr.T("Módulos de PAN tirados do desenho.");
    });

    private void VerCabos()
    {
        _ultimas = RotaDeCabosTabelas.Montar(Documento, _rota, out var recontagem);
        TabelaNaTela.Mostrar(_tabelas, _ultimas);
        Avisar(recontagem.Sumidos.Count == 0
                ? Tr.F("Recontado: {0} lance(s) no desenho.", recontagem.Lances.Count)
                : Tr.F("Recontado: {0} lance(s) no desenho; {1} gerado(s) e apagado(s) à mão (a origem foi pintada).", recontagem.Lances.Count, recontagem.Sumidos.Count),
            erro: recontagem.Sumidos.Count > 0);
    }

    private void ExportarCsv()
    {
        // Sempre recontado agora (regra 7): nunca o número de uma tela velha.
        VerCabos();
        var nome = System.IO.Path.GetFileNameWithoutExtension(Documento.Name);
        var caminho = DialogoDeArquivo.Salvar(Tr.T("Exportar cabos"), Tr.T("CSV (*.csv)|*.csv"), Tr.F("{0} - cabos {1}.csv", nome, CableLayers.Code(_rota)),
            Documento.IsNamedDrawing ? System.IO.Path.GetDirectoryName(Documento.Name) : null);
        if (caminho is null) return;

        RotaDeCabosTabelas.GravarCsv(caminho, _ultimas);
        Avisar(Tr.F("Exportado: {0}", caminho));
    }
}

/// <summary>O resumo de cabos da usina (24.1) e a lista de material com a folga do usuário (24.2), recontando antes.</summary>
internal sealed class AbaResumoDeCabos : AbaEletrica
{
    private readonly TextBox _folga;
    private readonly StackPanel _tabelas = new();
    private List<CableTable> _ultimas = [];

    internal AbaResumoDeCabos(Document documento) : base(documento)
    {
        var pilha = new StackPanel { Margin = new Thickness(4) };
        var grade = Grade(330);
        _folga = Campo(grade, Tr.T("Folga para a lista de material (%)"), Tr.T("Opcional, sua: o sistema não põe folga nenhuma sozinho. 0 = só o medido."));
        _folga.Text = "0";
        pilha.Children.Add(grade);

        var botoes = new WrapPanel();
        Botao(botoes, Tr.T("Atualizar (reconta)"), Tr.T("Reconta todas as rotas no desenho e monta o resumo e a lista de material."), Montar);
        Botao(botoes, Tr.T("Exportar CSV"), Tr.T("Grava o resumo e a lista de material num CSV."), Exportar);
        pilha.Children.Add(botoes);
        pilha.Children.Add(_tabelas);

        Children.Add(new ScrollViewer { Content = pilha, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
    }

    internal override void Atualizar()
    {
    }

    private void Montar()
    {
        if (!NumberInput.TryParseMeasure(_folga.Text, out var folga) || folga < 0)
        {
            Avisar(Tr.T("Não consigo ler a folga (um número de 0 para cima)."), erro: true);
            return;
        }

        var leitura = LeituraDaRota.Ler(Documento.Database);
        var sumidos = CableRoutes.All.Sum(r => RotaDeCabosTabelas.Recontar(Documento, r, leitura).Sumidos.Count);
        var medidos = RotaDeCabosTabelas.Medidos(Documento.Database);

        _ultimas = [CableReport.Summary(medidos), CableReport.Material(medidos, folga)];
        TabelaNaTela.Mostrar(_tabelas, _ultimas);
        Avisar(sumidos == 0 ? Tr.T("Recontado.") : Tr.F("Recontado: {0} lance(s) gerado(s) e apagado(s) à mão (a origem foi pintada).", sumidos), erro: sumidos > 0);
    }

    private void Exportar()
    {
        _ultimas = [];
        Montar();
        if (_ultimas.Count == 0) return;
        var nome = System.IO.Path.GetFileNameWithoutExtension(Documento.Name);
        var caminho = DialogoDeArquivo.Salvar(Tr.T("Exportar cabos"), Tr.T("CSV (*.csv)|*.csv"), Tr.F("{0} - resumo de cabos.csv", nome),
            Documento.IsNamedDrawing ? System.IO.Path.GetDirectoryName(Documento.Name) : null);
        if (caminho is null) return;

        RotaDeCabosTabelas.GravarCsv(caminho, _ultimas);
        Avisar(Tr.F("Exportado: {0}", caminho));
    }
}
