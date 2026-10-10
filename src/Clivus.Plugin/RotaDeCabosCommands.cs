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

        // Inversor de alocação automática fora de campo não bloqueia a aba CC: o Gerar CC o põe (item 19).
        var automaticos = LocalDosInversores.Ler(database, out var problemaDoLocal)
            .Where(l => l.Mode == InverterPlacementMode.Automatic).Select(l => l.Inverter).ToHashSet();
        if (problemaDoLocal is not null) problema = problema is null ? Tr.F("o local dos inversores não se lê ({0})", problemaDoLocal) : problema + "; " + Tr.F("o local dos inversores não se lê ({0})", problemaDoLocal);

        return (CableRoutes.Drawing(setup, StringsDoDesenho.Ler(database).Count, EquipamentoEmCampo.EmCampo(database), automaticos), problema);
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

    // Da última leitura: o que falta para cada rota e o problema do registro.
    private readonly Dictionary<CableRoute, IReadOnlyList<string>> _faltas = [];
    private string? _problemaDeLeitura;

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
        _abas.SelectionChanged += (_, e) =>
        {
            // As listas de dentro das abas também disparam SelectionChanged.
            if (ReferenceEquals(e.OriginalSource, _abas)) Rodape();
        };
        Atualizar();
    }

    /// <summary>Relê o desenho: quais abas abrem e o que cada uma mostra (as tabelas só no "Ver cabos", que reconta).</summary>
    internal void Atualizar() => Atualizar(paginas: true);

    private void Atualizar(bool paginas)
    {
        try
        {
            var (desenho, problema) = RotaDeCabosCommands.Ler(_documento);
            _problemaDeLeitura = problema;

            foreach (var rota in CableRoutes.All)
            {
                var falta = CableRoutes.Missing(rota, desenho);
                var item = _itens[rota];
                var livre = falta.Count == 0;
                _faltas[rota] = falta;

                item.IsEnabled = livre;
                item.ToolTip = livre ? CableRoutes.Description(rota) : Tr.F("Falta: {0}.", string.Join("; ", falta));
                if (livre && paginas) _paginas[rota].Atualizar();
            }

            // A aba aberta não pode ficar numa desabilitada: vai para a primeira livre.
            if (_abas.SelectedItem is not TabItem { IsEnabled: true })
                _abas.SelectedItem = CableRoutes.All.Select(r => _itens[r]).FirstOrDefault(i => i.IsEnabled) ?? _abas.Items[^1];

            Rodape();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar a rota de cabos.", erro);
            _recado.Foreground = Brushes.Firebrick;
            _recado.Visibility = Visibility.Visible;
            _recado.Text = Tr.F("Não consegui ler o desenho: {0}", erro.Message);
        }
    }

    /// <summary>
    /// O rodapé: o problema de leitura (se houver) e o que falta só para a
    /// rota da aba aberta (cada rota é independente; o Renan, 10/10/2026: "o
    /// gerar cabo é EXCLUSIVO por aba"). As abas desabilitadas dizem o que
    /// falta na dica.
    /// </summary>
    private void Rodape()
    {
        var linhas = new List<string>();
        if (_problemaDeLeitura is not null) linhas.Add(Tr.F("ATENÇÃO: {0}", _problemaDeLeitura));

        foreach (var rota in CableRoutes.All)
            if (ReferenceEquals(_itens[rota], _abas.SelectedItem) && _faltas.TryGetValue(rota, out var falta) && falta.Count > 0)
                linhas.Add(Tr.F("{0}: falta {1}.", CableRoutes.Title(rota), string.Join("; ", falta)));

        _recado.Foreground = Brushes.Firebrick;
        _recado.Text = string.Join("\n", linhas);
        _recado.Visibility = linhas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
    private readonly TextBox _vias;
    private bool _preenchendo;
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
        _vias = Campo(grade, Tr.T("Vias (cabos iguais em cada lance)"),
            Tr.T("Quantos cabos iguais correm em cada lance desenhado (ex.: 3 na MT de 3x1x25). Multiplica os metros do resumo e da lista de material; no Resumo dá para trocar por circuito."));
        _cabo.SelectionChanged += (_, _) =>
        {
            // Escolher um cabo de formação "3x1x..." já propõe as vias dele (o usuário pode mudar).
            if (!_preenchendo && (_cabo.SelectedItem as ComboBoxItem)?.Tag is Cable c && CableLibrary.WiresFromFormation(c.Formation) is { } n) _vias.Text = n.ToString(Tr.Culture);
        };
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
        Botao(linhaDaVala, Tr.T("Selecionar vala"), Tr.T("A janela some; clique nas polilinhas que são vala desta rota (Shift+clique tira; Enter termina). Elas vão para a camada da vala da rota e viram 3D, acompanhando o terreno na profundidade desta aba; o traçado em planta não muda."),
            () => Comando(PluginInfo.ComandoRotaVala, _rota.ToString()));
        Botao(linhaDaVala, Tr.T("Atualizar valas"), Tr.T("Relê as valas desta rota: as linhas que você apagou saem da rota, e as que ficaram voltam para o terreno na profundidade desta aba (se o terreno está processado)."),
            () => Comando(PluginInfo.ComandoRotaValaAtualizar, _rota.ToString()));
        Botao(linhaDaVala, Tr.T("Soltar valas"), Tr.T("A janela some; clique nas linhas que devem deixar de ser vala desta rota (Enter termina). A linha não é apagada: perde a marca de vala e vai para a camada corrente."),
            () => Comando(PluginInfo.ComandoRotaValaSoltar, _rota.ToString()));
        pilha.Children.Add(linhaDaVala);

        Titulo(pilha, Tr.T("Cabos"));
        var acoes = new WrapPanel();
        Botao(acoes, Tr.T("Gerar"), rota == CableRoute.DirectCurrent
                ? Tr.T("Apaga os cabos desta rota e desenha de novo, pelas valas, com os inversores onde estão (moveu um? Gere de novo). Os inversores automáticos que não estão em campo vão antes para o lado da vala, no ponto de menor cabo. O que não der para rotear é avisado e pintado de vermelho.")
                : Tr.T("Apaga os cabos desta rota e desenha de novo, pelas valas. O que não der para rotear é avisado e pintado de vermelho."),
            () => Comando(PluginInfo.ComandoRotaGerar, _rota.ToString()));
        if (rota == CableRoute.DirectCurrent)
            Botao(acoes, Tr.T("Recolocar automáticos"), Tr.T("Os inversores automáticos (aba Inversor da configuração elétrica) voltam ao lado da vala, no ponto de menor cabo CC das strings deles, mesmo os que você moveu; e a rota CC é refeita."),
                () => Comando(PluginInfo.ComandoRotaRecolocar, _rota.ToString()));
        if (rota == CableRoute.DirectCurrent)
            Botao(acoes, Tr.T("Recalcular rota..."), Tr.T("Refaz só os cabos CC dos inversores que você escolher (na lista ou no desenho), da posição de agora: mova um inversor à mão e recalcule só ele. Os cabos dos outros não mudam."),
                Recalcular);
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
        _vias.Text = config.Wires.ToString(Tr.Culture);

        _preenchendo = true;
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
        _preenchendo = false;

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

        if (!NumberInput.TryParseCount(_vias.Text, out var vias))
        {
            Avisar(Tr.T("Não consigo ler as vias (um número inteiro)."), erro: true);
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
            Wires = vias,
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

        var antes = RotaDeCabosStore.Configuracao(Documento.Database, _rota).Depth;
        var gravou = Gravar(() =>
        {
            if (RotaDeCabosStore.GravarConfiguracao(Documento.Database, nova) is { } problema) throw new InvalidOperationException(problema);
            var frase = Tr.F("Aba {0} salva. Trocar o cabo não redesenha: use Ver cabos para a tabela com as contas novas.", CableRoutes.Title(_rota));

            // Profundidade nova: as valas descem (ou sobem) junto, se o terreno já está na memória; senão o Gerar assenta.
            if (Math.Abs(antes - nova.Depth) > 1e-9)
            {
                if (TerrainCache.Get(Documento) is { } terreno)
                {
                    var (valas, _) = RotaDeCabosStore.AssentarValas(Documento.Database, _rota, nova.Depth, terreno.Mesh);
                    frase += " " + Tr.F("{0} vala(s) assentada(s) a {1:0.00} m.", valas, nova.Depth);
                }
                else
                {
                    frase += " " + Tr.T("O terreno não está processado nesta sessão: o Gerar assenta as valas na profundidade nova.");
                }
            }

            return frase;
        });
        if (gravou) LimparTabelas();
        return gravou;
    }

    /// <summary>"Recalcular rota": a lista dos inversores; os marcados (ou os escolhidos no desenho) vão para o comando.</summary>
    private void Recalcular()
    {
        var (setup, _) = ConfiguracaoEletricaStore.Ler(Documento.Database);
        if (setup.Inverters.Count == 0)
        {
            Avisar(Tr.T("Não há inversor no cadastro."), erro: true);
            return;
        }

        var janela = new JanelaDeRecalculo(setup.Inverters.OrderBy(i => i.Name, NaturalStringComparer.Instance).ToList(), EquipamentoEmCampo.EmCampo(Documento.Database));
        AcadApp.ShowModalWindow(janela);
        if (janela.Argumento is { } argumento) Comando(PluginInfo.ComandoRotaRecalcular, argumento);
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
        var frase = recontagem.Sumidos.Count == 0
            ? Tr.F("Recontado: {0} lance(s) no desenho.", recontagem.Lances.Count)
            : Tr.F("Recontado: {0} lance(s) no desenho; {1} gerado(s) e apagado(s) à mão (a origem foi pintada).", recontagem.Lances.Count, recontagem.Sumidos.Count);

        // Item 18: o cabo cuja ponta saiu de campo não entra na tabela; a aba diz quem.
        if (recontagem.Orfaos.Count > 0)
            frase += "\n" + Tr.F("{0} cabo(s) desenhado(s) ligam em equipamento que não está mais em campo: ficaram fora do resumo e foram pintados. Gere a rota de novo ou apague esses cabos.", recontagem.Orfaos.Count);
        Avisar(frase, erro: recontagem.Sumidos.Count > 0 || recontagem.Orfaos.Count > 0);
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

/// <summary>
/// "Recalcular rota" (item 19): a lista dos inversores, com caixas de marcar
/// (os em campo dizem isso), Marcar todos, e Escolher no desenho. O
/// argumento do comando fica em <see cref="Argumento"/> (null = cancelado).
/// </summary>
internal sealed class JanelaDeRecalculo : Window
{
    /// <summary>Os GUIDs marcados separados por ";", ou "Selecionar"; null se cancelou.</summary>
    internal string? Argumento { get; private set; }

    internal JanelaDeRecalculo(IReadOnlyList<Inverter> inversores, IReadOnlySet<(EquipmentKind Kind, Guid Id)> emCampo)
    {
        Title = Tr.T("Recalcular rota CC");
        SizeToContent = SizeToContent.WidthAndHeight;
        MaxHeight = 640;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var caixas = inversores.Select(i => new CheckBox
        {
            Content = emCampo.Contains((EquipmentKind.Inverter, i.Id)) ? Tr.F("{0} — em campo", i.Name) : Tr.F("{0} — fora de campo", i.Name),
            Tag = i.Id,
            Margin = new Thickness(0, 0, 12, 4),
        }).ToList();

        // Em colunas, para a lista longa não virar rolagem (regra de 02/10/2026).
        var lista = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = 420 };
        foreach (var c in caixas) lista.Children.Add(c);

        var todos = new CheckBox { Content = Tr.T("Marcar todos"), Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold };
        todos.Click += (_, _) => caixas.ForEach(c => c.IsChecked = todos.IsChecked);

        var botoes = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, ToolTip = dica, Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 6, 0) };
            b.Click += (_, _) =>
            {
                try
                {
                    acao();
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha na janela de recalcular rota.", erro);
                }
            };
            botoes.Children.Add(b);
            return b;
        }

        Botao(Tr.T("Recalcular os marcados"), Tr.T("Refaz só os cabos CC das strings dos inversores marcados, da posição de agora."), () =>
        {
            var marcados = caixas.Where(c => c.IsChecked == true).Select(c => ((Guid)c.Tag).ToString("D")).ToList();
            if (marcados.Count == 0) return;
            Argumento = string.Join(";", marcados);
            Close();
        }).IsDefault = true;
        Botao(Tr.T("Escolher no desenho"), Tr.T("A janela some; clique nos inversores em campo (Enter termina)."), () =>
        {
            Argumento = "Selecionar";
            Close();
        });
        Botao(Tr.T("Cancelar"), Tr.T("Fecha sem recalcular."), Close).IsCancel = true;

        var raiz = new StackPanel { Margin = new Thickness(12) };
        raiz.Children.Add(todos);
        raiz.Children.Add(lista);
        raiz.Children.Add(botoes);
        Content = raiz;
    }
}

/// <summary>
/// O resumo de cabos da usina (24.1, 24.2), recontando antes: uma aba por
/// tipo de cabo (CC, com as strings e as combiners; CA; MT) e a lista de
/// material com a folga do usuário. Desde os itens 12, 16 e 18 de
/// 10/10/2026: a tabela vem agrupada (CC: UC > trafo > inversor > strings;
/// CA: UC > trafo > inversores; MT: UC > trafos), cada grupo com o subtotal
/// de cabos e metros, tudo fechado no começo (só as UCs), cada grupo com
/// abrir/fechar tudo abaixo; no CC, a tag da string e as contas por string;
/// e o cabo cuja ponta saiu de campo (o inversor apagado) não entra: a aba diz quem.
/// </summary>
internal sealed class AbaResumoDeCabos : AbaEletrica
{
    private readonly TextBox _folga = new() { Width = 60, Text = "0", VerticalContentAlignment = VerticalAlignment.Center };
    private readonly List<(DataGrid Grade, TextBlock Rodape, HashSet<string> Abertos)> _circuitos = [];
    private readonly List<RotaDeCabosTabelas.ResumoDoTipo?> _resumos = [];
    private readonly StackPanel _material = new();
    private List<CableTable> _ultimas = [];

    internal AbaResumoDeCabos(Document documento) : base(documento)
    {
        // Tudo numa linha: a folga com o rótulo ao lado e os botões (regra de 02/10/2026).
        var topo = new WrapPanel();
        topo.Children.Add(new TextBlock { Text = Tr.T("Folga da lista de material (%)"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) });
        _folga.ToolTip = Tr.T("Opcional, sua: o sistema não põe folga nenhuma sozinho. 0 = só o medido.");
        _folga.Margin = new Thickness(0, 0, 12, 6);
        topo.Children.Add(_folga);
        Botao(topo, Tr.T("Atualizar (reconta)"), Tr.T("Reconta todas as rotas no desenho e monta os resumos e a lista de material."), Montar);
        Botao(topo, Tr.T("Exportar CSV"), Tr.T("Grava os resumos de CC, CA e MT (agrupados, com os subtotais) e a lista de material num CSV."), Exportar);
        topo.Children.Add(new TextBlock
        {
            Text = Tr.T("▸ abre um grupo; ⊞ abre tudo abaixo e ⊟ fecha. As vias se trocam na linha do circuito (duplo clique em Vias); vazio volta às da aba."),
            Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 6), TextWrapping = TextWrapping.Wrap,
        });

        var abas = new TabControl();
        foreach (var (titulo, rotas) in RotaDeCabosTabelas.TiposDeCabo)
        {
            var abertos = new HashSet<string>();
            var grade = Grade(rotas, abertos, _circuitos.Count);
            var rodape = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            var painel = new DockPanel();
            DockPanel.SetDock(rodape, Dock.Bottom);
            painel.Children.Add(rodape);
            painel.Children.Add(grade);
            abas.Items.Add(new TabItem { Header = Tr.T(titulo), Content = painel });
            _circuitos.Add((grade, rodape, abertos));
            _resumos.Add(null);
        }

        abas.Items.Add(new TabItem
        {
            Header = Tr.T("Material"),
            Content = new ScrollViewer { Content = _material, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto },
        });

        var raiz = new DockPanel();
        DockPanel.SetDock(topo, Dock.Top);
        raiz.Children.Add(topo);
        raiz.Children.Add(abas);
        Children.Add(raiz);
    }

    internal override void Atualizar()
    {
    }

    /// <summary>Uma linha da grade: um grupo (UC, trafo, inversor) com o subtotal, ou um circuito.</summary>
    private sealed class Linha
    {
        internal Linha(CableReport.CircuitGroup grupo, bool aberto)
        {
            Grupo = grupo;
            Nivel = grupo.Level;
            Aberto = aberto;
            Vias = string.Empty;
        }

        internal Linha(CableReport.CircuitRun circuito, int nivel, StringCheck? conta)
        {
            Circuito = circuito;
            Nivel = nivel;
            Conta = conta;
            Vias = circuito.Wires.ToString(Tr.Culture);
        }

        public CableReport.CircuitGroup? Grupo { get; }
        public CableReport.CircuitRun? Circuito { get; }
        public StringCheck? Conta { get; }
        public int Nivel { get; }
        public bool Aberto { get; }
        public bool EhGrupo => Grupo is not null;

        public Thickness Recuo => new(Nivel * 18 + (EhGrupo ? 0 : 18), 0, 0, 0);
        public Visibility DeGrupo => EhGrupo ? Visibility.Visible : Visibility.Collapsed;
        public string Seta => Aberto ? "▾" : "▸";

        public string Nome => Grupo is { } g ? Tr.F("{0} ({1} circuito(s))", g.Name, g.CircuitCount) : CableReport.DePara(Circuito!.FromName, Circuito.ToName);
        public string Tag => Circuito is { From.Kind: CableEndKind.String } c ? c.FromName : string.Empty;
        public string Rota => Circuito is { } c ? CableRoutes.Title(c.Route) : string.Empty;
        public string Cabo => Circuito is { } c ? c.Cable?.Name ?? Tr.T("(sem cabo escolhido)") : string.Empty;
        public string Formacao => Circuito?.Cable?.Formation ?? string.Empty;
        public string Secao => Circuito?.Cable is { } x ? x.SectionMm2.ToString("0.##", Tr.Culture) : string.Empty;
        public string Condutor => Circuito?.Cable?.Conductor ?? string.Empty;
        public string Isolacao => CableReport.Isolacao(Circuito?.Cable) ?? string.Empty;
        public string Metodo => Circuito?.Method ?? string.Empty;
        public int Lances => Grupo?.Runs ?? Circuito!.Runs;
        public string Comprimento => (Grupo?.Length ?? Circuito!.Length).ToString("0.00", Tr.Culture);
        public string Vias { get; set; }
        public int Cabos => Grupo?.Cables ?? Circuito!.Cables;
        public string Total => (Grupo?.CableLength ?? Circuito!.CableLength).ToString("0.00", Tr.Culture);

        public string Voc => Numero(Conta?.VocAtMin);
        public string Vmp => Numero(Conta?.Vmp);
        public string Isc => Numero(Conta?.Isc);
        public string Imp => Numero(Conta?.Imp);
        public string Capacidade => Numero(Conta?.Ampacity);
        public string Suporta => Conta?.Supports is { } s ? (s ? Tr.T("Sim") : Tr.T("Não")) : string.Empty;

        private static string Numero(double? v) => v is { } x && double.IsFinite(x) ? x.ToString("0.##", Tr.Culture) : string.Empty;
    }

    private DataGrid Grade(IReadOnlyCollection<CableRoute> rotas, HashSet<string> abertos, int tipo)
    {
        var grade = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionUnit = DataGridSelectionUnit.Cell,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        };

        // O grupo em negrito e com fundo: a hierarquia se lê de longe.
        var estilo = new Style(typeof(DataGridRow));
        var gatilho = new DataTrigger { Binding = new System.Windows.Data.Binding(nameof(Linha.EhGrupo)), Value = true };
        gatilho.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        gatilho.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF7))));
        estilo.Triggers.Add(gatilho);
        grade.RowStyle = estilo;

        // A primeira coluna: o recuo do nível, a seta (abre e fecha), ⊞ (abre tudo abaixo), ⊟ (fecha tudo abaixo) e o nome.
        var pilha = new FrameworkElementFactory(typeof(StackPanel));
        pilha.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        pilha.SetBinding(MarginProperty, new System.Windows.Data.Binding(nameof(Linha.Recuo)));
        FrameworkElementFactory BotaoDoGrupo(string acao, System.Windows.Data.BindingBase conteudo, string dica)
        {
            var b = new FrameworkElementFactory(typeof(Button));
            b.SetBinding(ContentControl.ContentProperty, conteudo);
            b.SetValue(TagProperty, acao);
            b.SetValue(ToolTipProperty, dica);
            b.SetValue(Control.PaddingProperty, new Thickness(3, 0, 3, 0));
            b.SetValue(MarginProperty, new Thickness(0, 0, 3, 0));
            b.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            b.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            b.SetValue(FocusableProperty, false);
            b.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(Linha.DeGrupo)));
            return b;
        }

        pilha.AppendChild(BotaoDoGrupo("alternar", new System.Windows.Data.Binding(nameof(Linha.Seta)), Tr.T("Abrir ou fechar este grupo")));
        pilha.AppendChild(BotaoDoGrupo("abrir", new System.Windows.Data.Binding { Source = "⊞" }, Tr.T("Abrir tudo abaixo")));
        pilha.AppendChild(BotaoDoGrupo("fechar", new System.Windows.Data.Binding { Source = "⊟" }, Tr.T("Fechar tudo abaixo")));
        var nome = new FrameworkElementFactory(typeof(TextBlock));
        nome.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Linha.Nome)));
        nome.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        pilha.AppendChild(nome);
        grade.Columns.Add(new DataGridTemplateColumn { Header = Tr.T("Agrupamento / circuito"), CellTemplate = new DataTemplate { VisualTree = pilha }, IsReadOnly = true });

        void Coluna(string titulo, string campo, bool editavel = false, bool numero = false)
        {
            var coluna = new DataGridTextColumn
            {
                Header = titulo,
                Binding = new System.Windows.Data.Binding(campo) { Mode = editavel ? System.Windows.Data.BindingMode.TwoWay : System.Windows.Data.BindingMode.OneWay },
                IsReadOnly = !editavel,
            };
            if (numero) coluna.ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } };
            grade.Columns.Add(coluna);
        }

        var dc = rotas.Contains(CableRoute.DirectCurrent);
        if (dc) Coluna(Tr.T("Tag da string"), nameof(Linha.Tag));
        if (rotas.Count > 1) Coluna(Tr.T("Rota"), nameof(Linha.Rota));
        Coluna(Tr.T("Cabo"), nameof(Linha.Cabo));
        Coluna(Tr.T("Formação"), nameof(Linha.Formacao));
        Coluna(Tr.T("Seção (mm²)"), nameof(Linha.Secao), numero: true);
        Coluna(Tr.T("Condutor"), nameof(Linha.Condutor));
        Coluna(Tr.T("Isolação"), nameof(Linha.Isolacao));
        Coluna(Tr.T("Método"), nameof(Linha.Metodo));
        Coluna(Tr.T("Lances"), nameof(Linha.Lances), numero: true);
        Coluna(Tr.T("Comprimento (m)"), nameof(Linha.Comprimento), numero: true);
        Coluna(Tr.T("Vias"), nameof(Linha.Vias), editavel: true, numero: true);
        Coluna(Tr.T("Cabos"), nameof(Linha.Cabos), numero: true);
        Coluna(Tr.T("Total de cabo (m)"), nameof(Linha.Total), numero: true);
        if (dc)
        {
            var titulos = StringCheck.Headers();
            string[] campos = [nameof(Linha.Voc), nameof(Linha.Vmp), nameof(Linha.Isc), nameof(Linha.Imp), nameof(Linha.Capacidade), nameof(Linha.Suporta)];
            for (var i = 0; i < campos.Length; i++) Coluna(titulos[i], campos[i], numero: i < campos.Length - 1);
        }

        // As vias só se trocam na linha do circuito.
        grade.BeginningEdit += (_, e) =>
        {
            if (e.Row.Item is Linha { EhGrupo: true }) e.Cancel = true;
        };

        grade.CellEditEnding += (_, e) =>
        {
            try
            {
                if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not Linha { Circuito: { } circuito } || e.EditingElement is not TextBox caixa) return;
                TrocarVias(circuito, caixa.Text);
            }
            catch (System.Exception falha)
            {
                RegistroDeDiagnostico.Registrar("Falha ao trocar as vias no resumo de cabos.", falha);
                Avisar(Tr.F("Não consegui: {0}", falha.Message), erro: true);
            }
        };

        // Os botões da primeira coluna: abrir/fechar o grupo, abrir/fechar tudo abaixo.
        grade.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            try
            {
                if (e.OriginalSource is not Button { Tag: string acao, DataContext: Linha { Grupo: { } g } }) return;
                var abaixo = g.Descendants.Select(d => d.Key).Prepend(g.Key);
                switch (acao)
                {
                    case "alternar":
                        if (!abertos.Remove(g.Key)) abertos.Add(g.Key);
                        break;
                    case "abrir":
                        abertos.UnionWith(abaixo);
                        break;
                    default:
                        abertos.ExceptWith(abaixo);
                        break;
                }

                e.Handled = true;
                Mostrar(tipo);
            }
            catch (System.Exception falha)
            {
                RegistroDeDiagnostico.Registrar("Falha ao abrir ou fechar um grupo do resumo de cabos.", falha);
            }
        }));

        return grade;
    }

    /// <summary>Monta as linhas visíveis de um tipo de cabo pelos grupos abertos (tudo fechado no começo: só as UCs).</summary>
    private void Mostrar(int tipo)
    {
        var (grade, _, abertos) = _circuitos[tipo];
        if (_resumos[tipo] is not { } resumo) return;

        var linhas = new List<Linha>();
        void Grupo(CableReport.CircuitGroup g)
        {
            var aberto = abertos.Contains(g.Key);
            linhas.Add(new Linha(g, aberto));
            if (!aberto) return;
            foreach (var f in g.Children) Grupo(f);
            foreach (var c in g.Circuits) linhas.Add(new Linha(c, g.Level + 1, resumo.Contas.GetValueOrDefault(c.From)));
        }

        foreach (var g in resumo.Grupos) Grupo(g);

        // Uma edição que ficou aberta (saiu da célula com Tab) não pode estar no meio quando a lista troca.
        grade.CancelEdit(DataGridEditingUnit.Row);
        grade.ItemsSource = linhas;
    }

    /// <summary>
    /// Grava as vias do circuito (vazio ou igual às da aba = volta às da aba)
    /// e remonta, depois que a grade fecha a edição.
    /// </summary>
    private void TrocarVias(CableReport.CircuitRun circuito, string texto)
    {
        int? vias = null;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            if (!NumberInput.TryParseCount(texto, out var n) || n < 1 || n > RouteSettings.MaxWires)
            {
                Avisar(Tr.F("Vias de {0}: tem que ser um inteiro de 1 a {1}. Nada mudou.", CableReport.DePara(circuito.FromName, circuito.ToName), RouteSettings.MaxWires), erro: true);
                Dispatcher.BeginInvoke(Montar);
                return;
            }

            vias = n;
        }

        var daAba = RotaDeCabosStore.Configuracao(Documento.Database, circuito.Route).Wires;
        if (vias == daAba) vias = null;

        var problema = EscritaForaDeComando.Fazer(Documento, () => RotaDeCabosStore.GravarVias(Documento.Database, circuito.Route, circuito.From, circuito.To, vias));
        Dispatcher.BeginInvoke(() =>
        {
            Montar();
            if (problema is not null) Avisar(Tr.F("Não gravei as vias: {0}", problema), erro: true);
            else Avisar(Tr.F("Vias de {0}: {1}.", CableReport.DePara(circuito.FromName, circuito.ToName), vias is { } v ? v.ToString(Tr.Culture) : Tr.F("as da aba ({0})", daAba)));
        });
    }

    private void Montar()
    {
        try
        {
            if (!NumberInput.TryParseMeasure(_folga.Text, out var folga) || folga < 0)
            {
                Avisar(Tr.T("Não consigo ler a folga (um número de 0 para cima)."), erro: true);
                return;
            }

            var db = Documento.Database;
            var usina = RotaDeCabosTabelas.Usina(Documento);
            var sumidos = usina.Sumidos;
            var orfaos = usina.Orfaos;

            _ultimas = [];
            for (var i = 0; i < RotaDeCabosTabelas.TiposDeCabo.Length; i++)
            {
                var resumo = usina.Tipos[i];
                _resumos[i] = resumo;
                _ultimas.Add(resumo.Tabela);
                Mostrar(i);

                var circuitos = resumo.Circuitos;
                var (_, rodape, _) = _circuitos[i];
                var linhas = new List<string>();
                if (resumo.ForaDeCampo is { } fora) linhas.Add(fora);
                linhas.Add(Tr.F("Total da usina: {0} circuito(s), {1} cabo(s), {2:0.00} m de traçado e {3:0.00} m de cabo.",
                    circuitos.Count, circuitos.Sum(c => c.Cables), circuitos.Sum(c => c.Length), circuitos.Sum(c => c.CableLength)));
                linhas.AddRange(resumo.Tabela.Notes.Where(n => n != resumo.ForaDeCampo).Select(n => "• " + n));
                rodape.Text = string.Join("\n", linhas);
                rodape.Foreground = resumo.ForaDeCampo is null ? SystemColors.ControlTextBrush : Brushes.Firebrick;
            }

            var material = CableReport.Material(usina.Medidos, folga);
            if (RotaDeCabosTabelas.ProblemaDasVias(db) is { } ilegivel) material = material with { Notes = [.. material.Notes, ilegivel] };
            _ultimas.Add(material);
            TabelaNaTela.Mostrar(_material, [material]);

            var frase = sumidos == 0 ? Tr.T("Recontado.") : Tr.F("Recontado: {0} lance(s) gerado(s) e apagado(s) à mão (a origem foi pintada).", sumidos);
            if (orfaos > 0) frase += "\n" + Tr.F("{0} cabo(s) desenhado(s) ligam em equipamento que não está mais em campo: ficaram fora do resumo e foram pintados. Gere a rota de novo ou apague esses cabos.", orfaos);
            var vias = RotaDeCabosTabelas.ProblemaDasVias(db);
            if (vias is not null) frase += "\n" + vias;
            Avisar(frase, erro: sumidos > 0 || orfaos > 0 || vias is not null);
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao montar o resumo de cabos.", falha);
            Avisar(Tr.F("Não consegui: {0}", falha.Message), erro: true);
        }
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
