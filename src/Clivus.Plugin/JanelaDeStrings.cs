using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela das strings (elétrica, 11.1): solta, abas Configuração (a
/// biblioteca de tipos de string) e Gerar. Uma por desenho.
/// </summary>
internal sealed class JanelaDeStrings : Window
{
    private static readonly Dictionary<Document, JanelaDeStrings> Abertas = [];

    private readonly Document _documento;
    private readonly ListBox _lista = new() { MinHeight = 220 };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CartesianoDaString _cartesiano = new();
    private readonly TabControl _abas = new() { Margin = new Thickness(10) };
    private readonly StackPanel _tiposDaGeracao = new();
    private readonly HashSet<Guid> _tiposConhecidos = [];
    private readonly ListBox _relatorio = new() { BorderBrush = Brushes.LightGray };
    private readonly TextBlock _resumoDoTracado = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

    private readonly ComboBox _tipoDoTrecho = new() { Height = 26, MinWidth = 110, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly CheckBox _fileiraInteira = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

    /// <summary>A string sendo montada por cliques no cartesiano (do tipo escolhido), ou null.</summary>
    private RouteBuilder? _montagem;
    private Guid _tipoDaMontagem;

    private JanelaDeStrings(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Strings — Clivus Solar");
        Width = 980;
        Height = 560;
        MinWidth = 720;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var abas = _abas;
        abas.Items.Add(new TabItem { Header = Tr.T("Configuração"), Content = AbaConfiguracao(), ToolTip = Tr.T("Os tipos de string: quais mesas cada um cobre e o traçado.") });
        abas.Items.Add(new TabItem { Header = Tr.T("Gerar"), Content = AbaGerar(), ToolTip = Tr.T("Escolhe os tipos que valem e gera o traçado nas mesas selecionadas.") });

        Content = abas;
        _lista.SelectionChanged += (_, _) => MostrarTipo();
        _cartesiano.CelulaClicada += Clicou;
        Atualizar();
    }

    private UIElement AbaConfiguracao()
    {
        // A lista e os botões à esquerda; o cartesiano do tipo escolhido
        // ocupa o resto (02/10/2026: nada de espaço em branco).
        var botoes = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        Button Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, MinWidth = 108, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 0, 8, 0), ToolTip = dica };
            b.Click += (_, _) => Protegido(acao);
            botoes.Children.Add(b);
            return b;
        }

        Botao(Tr.T("Adicionar tipo de string"), Tr.T("Cria um tipo novo na biblioteca (Modelo 1, 2, 3...).") + " " + Tr.T("Esconde a janela: selecione as mesas do tipo (só mesas entram) e tecle Enter."), Adicionar);
        Botao(Tr.T("Trocar mesas"), Tr.T("Escolhe de novo em campo as mesas do tipo escolhido."), TrocarMesas);
        Botao(Tr.T("Renomear"), Tr.T("Troca o nome do tipo escolhido."), Renomear);
        Botao(Tr.T("Apagar"), Tr.T("Tira o tipo escolhido da biblioteca. Strings já desenhadas não mudam."), Apagar);
        Botao(Tr.T("Clonar"), Tr.T("Cria um tipo igual ao escolhido (mesas e traçado), com o próximo nome."), Clonar);
        Botao(Tr.T("Espelhar"), Tr.T("Reflete o traçado do tipo escolhido de um lado ao outro da mesa: o + e o − vão para a outra ponta."), Espelhar);

        var esquerda = new DockPanel { Width = 280, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(botoes, Dock.Bottom);
        esquerda.Children.Add(botoes);
        esquerda.Children.Add(_lista);

        var direita = new DockPanel();
        var tracado = BarraDoTracado();
        DockPanel.SetDock(tracado, Dock.Top);
        direita.Children.Add(tracado);
        DockPanel.SetDock(_recado, Dock.Bottom);
        direita.Children.Add(_recado);
        direita.Children.Add(_cartesiano);

        var painel = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(esquerda, Dock.Left);
        painel.Children.Add(esquerda);
        painel.Children.Add(direita);
        return painel;
    }

    /// <summary>
    /// A barra do traçado (11.3, 11.4): clique no módulo do + e depois em
    /// cada ponto de virada (traçado livre: um U na metade da mesa são quatro
    /// cliques), cada trecho convencional ou leapfrog; Concluir string grava.
    /// Fileira inteira: um clique liga a fileira toda do cartesiano numa
    /// string, no tipo de trecho escolhido.
    /// </summary>
    private UIElement BarraDoTracado()
    {
        var barra = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        void Botao(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Height = 26, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = dica };
            b.Click += (_, _) => Protegido(acao);
            barra.Children.Add(b);
        }

        _tipoDoTrecho.Items.Add(new ComboBoxItem { Content = Tr.T("Convencional"), ToolTip = Tr.T("Módulo a módulo, em sequência: o + e o − ficam em pontas opostas do trecho.") });
        _tipoDoTrecho.Items.Add(new ComboBoxItem { Content = Tr.T("Leapfrog"), ToolTip = Tr.T("Alternado: vai pulando um módulo e volta pelos pulados; o − fica ao lado do começo do trecho.") });
        _tipoDoTrecho.SelectedIndex = 0;
        _fileiraInteira.Content = Tr.T("Fileira inteira");
        _fileiraInteira.ToolTip = Tr.T("Um clique liga a fileira toda do cartesiano numa string, começando (+) pela ponta mais perto do clique.");

        barra.Children.Add(_tipoDoTrecho);
        barra.Children.Add(_fileiraInteira);
        Botao(Tr.T("Concluir string"), Tr.T("Grava a string montada com o − no último módulo clicado (o mesmo que Ctrl + clique nele)."), ConcluirString);
        Botao(Tr.T("Desfazer trecho"), Tr.T("Tira o último trecho da string em montagem."), DesfazerTrecho);
        Botao(Tr.T("Apagar última string"), Tr.T("Tira a última string gravada do tipo escolhido."), ApagarUltimaString);
        Botao(Tr.T("Limpar traçado"), Tr.T("Tira todas as strings do tipo escolhido (as já desenhadas em campo não mudam)."), LimparTracado);

        // O resumo numa linha só abaixo dos botões.
        var painel = new DockPanel();
        DockPanel.SetDock(barra, Dock.Top);
        painel.Children.Add(barra);
        _resumoDoTracado.Margin = new Thickness(0, 0, 0, 6);
        painel.Children.Add(_resumoDoTracado);
        return painel;
    }

    /// <summary>O tipo de trecho do próximo clique.</summary>
    private RoutingKind TipoDoTrecho => _tipoDoTrecho.SelectedIndex == 1 ? RoutingKind.Leapfrog : RoutingKind.Conventional;

    private PlanView? _vistaDoDesenho;

    /// <summary>
    /// O tipo como aparece em planta: o tipo gravado antes de 05/10/2026 não
    /// guarda a vista, e aí vale a da maioria das mesas do desenho (só para
    /// mostrar; o tipo gravado não muda).
    /// </summary>
    private StringType? ComVista(StringType? tipo)
    {
        if (tipo is null || tipo.Sketch?.View is not null) return tipo;

        try
        {
            if (_vistaDoDesenho is null)
            {
                using var trava = _documento.LockDocument();
                _vistaDoDesenho = MesasDaString.VistaDoDesenho(_documento.Database);
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ler a vista das mesas para o cartesiano.", erro);
            _vistaDoDesenho = default(PlanView);
        }

        return tipo with { Sketch = tipo.SketchOrDefault with { View = _vistaDoDesenho } };
    }

    private void MostrarTipo()
    {
        var tipo = Escolhido;
        if (tipo is null || tipo.Id != _tipoDaMontagem) _montagem = null;

        _cartesiano.Mostrar(ComVista(tipo), _montagem?.Cells);

        if (tipo is null || tipo.Arrangement.IsEmpty)
        {
            _resumoDoTracado.Text = string.Empty;
            return;
        }

        var ligados = tipo.Routes.Sum(r => r.ModuleCount);
        var texto = Tr.F("{0} string(s), {1} de {2} módulo(s) ligados.", tipo.Routes.Count, ligados, tipo.Arrangement.ModuleCount);
        texto += _montagem is { IsEmpty: false } m
            ? " " + Tr.F("Em montagem: {0} módulo(s).", m.Cells.Count)
            : " " + Tr.T("Ctrl + clique no módulo do + para começar; Ctrl + clique no do − fecha a string (no leapfrog, o − é o vizinho do +). Clique simples marca pontos de virada.");
        _resumoDoTracado.Text = texto;
    }

    /// <summary>
    /// O clique de um botão: erro vira aviso na janela, nunca exceção solta
    /// (num clique de WPF ela derruba o Civil 3D, 05/10/2026).
    /// </summary>
    private void Protegido(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha num botão da janela de strings.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>
    /// O clique no cartesiano (05/10/2026): Ctrl + clique marca o + (o
    /// primeiro) e o − (fecha e grava a string); clique simples, com o + já
    /// marcado, é um ponto de virada do traçado livre.
    /// </summary>
    private void Clicou(RoutingCell celula, bool comCtrl)
    {
        try
        {
            if (Escolhido is not { } tipo) return;

            if (_fileiraInteira.IsChecked == true)
            {
                FileiraInteira(tipo, celula);
                return;
            }

            if (_montagem is null || _tipoDaMontagem != tipo.Id)
            {
                _montagem = new RouteBuilder(tipo.Arrangement, tipo.Routes.SelectMany(r => r.Cells));
                _tipoDaMontagem = tipo.Id;
            }

            if (_montagem.IsEmpty && !comCtrl)
            {
                Avisar(Tr.T("Ctrl + clique no módulo do + para começar a string."), erro: true);
                return;
            }

            if (!_montagem.IsEmpty && comCtrl)
            {
                if (_montagem.FinishAt(celula, TipoDoTrecho, out var problema) is { } nova) Gravar(tipo, nova);
                else Avisar(Tr.F("Não fechei a string: {0}.", problema ?? string.Empty), erro: true);

                MostrarTipo();
                return;
            }

            if (_montagem.Click(celula, TipoDoTrecho) is { } porque) Avisar(Tr.F("Não liguei: {0}.", porque), erro: true);
            else _recado.Text = string.Empty;

            MostrarTipo();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no clique do cartesiano das strings.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>A fileira inteira do clique numa string (11.4), gravada já.</summary>
    private void FileiraInteira(StringType tipo, RoutingCell celula)
    {
        if (_montagem is { IsEmpty: false } && _tipoDaMontagem == tipo.Id)
        {
            Avisar(Tr.T("Conclua ou desfaça a string em montagem antes."), erro: true);
            return;
        }

        if (StringRouting.WholeRow(tipo.Arrangement, celula, TipoDoTrecho, out var porque) is not { } nova)
        {
            Avisar(Tr.F("Não liguei: {0}.", porque ?? string.Empty), erro: true);
            return;
        }

        string? recusa = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => recusa = b.SetStrings(tipo.Id, [.. tipo.Routes, nova]));
            return recusa is null ? Tr.F("String {0} gravada: {1}.", tipo.Routes.Count + 1, RouteBuilder.Describe(nova)) : null;
        }, tipo.Id);

        if (recusa is not null) Avisar(Tr.F("Não gravei: {0}.", recusa), erro: true);
    }

    private void DesfazerTrecho()
    {
        if (_montagem is null || !_montagem.Undo()) Avisar(Tr.T("Nada a desfazer na string em montagem."), erro: true);
        MostrarTipo();
    }

    private void ConcluirString()
    {
        if (Escolhido is not { } tipo || _montagem is null || _tipoDaMontagem != tipo.Id)
        {
            Avisar(Tr.T("Monte a string clicando nos módulos do cartesiano."), erro: true);
            return;
        }

        if (_montagem.Finish(out var porque) is not { } nova)
        {
            Avisar(Tr.F("Não gravei: {0}.", porque), erro: true);
            return;
        }

        Gravar(tipo, nova);
    }

    /// <summary>Grava a string montada no tipo; a montagem recomeça se gravou.</summary>
    private void Gravar(StringType tipo, StringRoute nova)
    {
        string? recusa = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => recusa = b.SetStrings(tipo.Id, [.. tipo.Routes, nova]));
            return recusa is null ? Tr.F("String {0} gravada: {1}.", tipo.Routes.Count + 1, RouteBuilder.Describe(nova)) : null;
        }, tipo.Id);

        if (recusa is not null) Avisar(Tr.F("Não gravei: {0}.", recusa), erro: true);
        else
        {
            _montagem = null;
            MostrarTipo();
        }
    }

    private void LimparTracado()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        _montagem = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => b.SetStrings(tipo.Id, []));
            return Tr.F("Traçado de {0} limpo.", tipo.Name);
        }, tipo.Id);
    }

    /// <summary>
    /// A aba Gerar (11.6): os tipos que valem nesta porção da usina (os com
    /// traçado vêm marcados), o botão que pede as mesas em campo e o
    /// relatório: que tipo caiu em que grupo e os avisos (mesa sem tipo,
    /// grupo com string ligada a inversor), em vermelho.
    /// </summary>
    private UIElement AbaGerar()
    {
        var explicacao = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap,
            Text = Tr.T("Escolha os tipos de string que valem e selecione as mesas: cada tipo só preenche grupos de mesas iguais aos dele."),
        };

        var gerar = new Button { Content = Tr.T("Gerar nas mesas selecionadas"), Height = 28, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = Tr.T("Esconde a janela: selecione as mesas (só mesas entram) e tecle Enter. Mesa que já tem string livre é regerada; string ligada a inversor não é tocada.") };
        gerar.Click += (_, _) => Protegido(PedirGeracao);

        var esquerda = new DockPanel { Width = 280, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(explicacao, Dock.Top);
        esquerda.Children.Add(explicacao);
        DockPanel.SetDock(gerar, Dock.Bottom);
        esquerda.Children.Add(gerar);
        esquerda.Children.Add(new ScrollViewer { Content = _tiposDaGeracao, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var painel = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(esquerda, Dock.Left);
        painel.Children.Add(esquerda);
        painel.Children.Add(_relatorio);
        return painel;
    }

    /// <summary>Os tipos na aba Gerar: marcados os que podem gerar; os outros desligados, com o porquê.</summary>
    private void AtualizarGeracao(IReadOnlyList<StringType> tipos)
    {
        var marcados = _tiposDaGeracao.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (Guid)c.Tag).ToHashSet();
        var primeiraVez = _tiposDaGeracao.Children.Count == 0;
        _tiposDaGeracao.Children.Clear();

        foreach (var tipo in tipos)
        {
            var caixa = new CheckBox { Content = Descrever(tipo), Tag = tipo.Id, Margin = new Thickness(0, 2, 0, 2), IsEnabled = tipo.CanGenerate };
            caixa.IsChecked = tipo.CanGenerate && (primeiraVez || marcados.Contains(tipo.Id) || !_tiposConhecidos.Contains(tipo.Id));
            if (!tipo.CanGenerate) caixa.ToolTip = Tr.T("Sem mesas ou sem traçado: monte o traçado na aba Configuração.");
            _tiposDaGeracao.Children.Add(caixa);
        }

        _tiposConhecidos.Clear();
        _tiposConhecidos.UnionWith(tipos.Select(t => t.Id));
    }

    private void PedirGeracao()
    {
        try
        {
            var tipos = _tiposDaGeracao.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (Guid)c.Tag).ToList();
            if (tipos.Count == 0)
            {
                MostrarRelatorio([Tr.T("Marque ao menos um tipo de string com traçado.")], erro: true);
                return;
            }

            if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
            {
                MostrarRelatorio([Tr.T("Ative o desenho desta janela antes de escolher as mesas.")], erro: true);
                return;
            }

            if (ComandoEmCurso())
            {
                MostrarRelatorio([Tr.T("Termine (ou cancele com Esc) o comando em curso no desenho antes.")], erro: true);
                return;
            }

            StringCommands.PedirGeracao(_documento, tipos);
            Hide();
            _documento.SendStringToExecute("_" + PluginInfo.ComandoStringGerar + " ", true, false, false);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao pedir a geração das strings.", erro);
            Show();
            MostrarRelatorio([Tr.F("Não consegui: {0}", erro.Message)], erro: true);
        }
    }

    private void MostrarRelatorio(IReadOnlyList<string> linhas, bool erro)
    {
        _relatorio.Items.Clear();
        for (var i = 0; i < linhas.Count; i++)
        {
            var aviso = erro
                || linhas[i].StartsWith(Tr.F("Aviso: {0}.", string.Empty).TrimEnd('.', ' '), StringComparison.Ordinal)
                || linhas[i].StartsWith(Tr.F("ATENÇÃO: {0}.", string.Empty).TrimEnd('.', ' '), StringComparison.Ordinal);
            _relatorio.Items.Add(new TextBlock { Text = linhas[i], TextWrapping = TextWrapping.Wrap, Foreground = aviso ? Brushes.Firebrick : i == 0 ? Brushes.ForestGreen : Brushes.Black });
        }
    }

    /// <summary>O CLIVUS_STRING_GERAR pedido pela janela terminou: ela volta, na aba Gerar, com o relatório.</summary>
    internal static void RetomarGeracao(Document documento, IReadOnlyList<string> linhas, bool erro)
    {
        if (!Abertas.TryGetValue(documento, out var janela)) return;

        try
        {
            if (!janela.IsVisible) janela.Show();
            janela._abas.SelectedIndex = 1;
            janela.Atualizar();
            janela.MostrarRelatorio(linhas, erro);
            janela.Activate();
        }
        catch (Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trazer de volta a janela de strings.", falha);
        }
    }

    private StringType? Escolhido => (_lista.SelectedItem as ListBoxItem)?.Tag as StringType;

    private void Atualizar(Guid? manter = null)
    {
        var lido = StringTypeStore.Ler(_documento.Database);
        var anterior = manter ?? Escolhido?.Id;

        // O traçado pode ter mudado (outra string gravada, mesas trocadas): a
        // montagem em curso começa de novo sobre o que está gravado.
        _montagem = null;

        _lista.Items.Clear();
        foreach (var tipo in lido.Items)
        {
            var item = new ListBoxItem { Content = Descrever(tipo), Tag = tipo };
            _lista.Items.Add(item);
            if (tipo.Id == anterior) _lista.SelectedItem = item;
        }

        if (_lista.SelectedItem is null && _lista.Items.Count > 0) _lista.SelectedIndex = 0;
        MostrarTipo();
        AtualizarGeracao(lido.Items);

        if (_lista.Items.Count == 0) _recado.Text = Tr.T("Nenhum tipo de string ainda: use Adicionar tipo de string.");
        if (lido.Problem is { } problema) Avisar(problema, erro: true);
    }

    internal static string Descrever(StringType tipo) => tipo.Arrangement.IsEmpty
        ? Tr.F("{0} — sem mesas escolhidas", tipo.Name)
        : Tr.F("{0} — {1} mesa(s), {2} módulo(s) ({3})", tipo.Name, tipo.Arrangement.Tables.Count, tipo.Arrangement.ModuleCount, tipo.Arrangement.ToText())
            + (tipo.Routes.Count > 0 ? Tr.F(", {0} string(s) de {1}", tipo.Routes.Count, string.Join("/", tipo.Routes.Select(r => r.ModuleCount))) : string.Empty);

    private void Avisar(string texto, bool erro = false)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }

    /// <summary>O problema de leitura do registro achado na última mudança (mostrado depois do Fazer).</summary>
    private string? _problemaDoRegistro;

    /// <summary>
    /// Muda a biblioteca guardando o problema de leitura, se houve: a
    /// mudança regrava o registro limpo, e sem isto o aviso se perderia.
    /// </summary>
    private void MudarBiblioteca(Action<StringLibrary> mudanca) =>
        _problemaDoRegistro = StringTypeStore.Mudar(_documento.Database, mudanca) ?? _problemaDoRegistro;

    /// <summary>Escreve no desenho fora de comando (trava e vigia calado), sem derrubar o Civil 3D num clique.</summary>
    private void Fazer(Func<string?> operacao, Guid? manter = null)
    {
        try
        {
            _problemaDoRegistro = null;
            var frase = EscritaForaDeComando.Fazer(_documento, operacao);
            Atualizar(manter);
            if (_problemaDoRegistro is { } problema) Avisar(Tr.F("ATENÇÃO: {0}.", problema), erro: true);
            else if (frase is not null) Avisar(frase);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de strings.", erro);
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>Novo tipo: a janela some, o usuário escolhe as mesas em campo, e ela volta com o cartesiano.</summary>
    private void Adicionar() => PedirMesas(Guid.Empty);

    private void TrocarMesas()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        PedirMesas(tipo.Id);
    }

    /// <summary>
    /// A seleção em campo roda no comando CLIVUS_STRING_MESAS (seleção de
    /// janela solta só é segura dentro de um comando). A janela se esconde
    /// e o comando a traz de volta em <see cref="Retomar"/>.
    /// </summary>
    private void PedirMesas(Guid alvo)
    {
        try
        {
            if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
            {
                Avisar(Tr.T("Ative o desenho desta janela antes de escolher as mesas."), erro: true);
                return;
            }

            if (ComandoEmCurso())
            {
                Avisar(Tr.T("Termine (ou cancele com Esc) o comando em curso no desenho antes."), erro: true);
                return;
            }

            if (alvo != Guid.Empty && Escolhido is { Routes.Count: > 0 } comTracado
                && MessageBox.Show(this, Tr.F("Trocar as mesas de {0} por mesas de outra grade descarta o traçado dele ({1} string(s)). Continuar?", comTracado.Name, comTracado.Routes.Count), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            StringCommands.PedirMesas(_documento, alvo);
            Hide();
            _documento.SendStringToExecute("_" + PluginInfo.ComandoStringMesas + " ", true, false, false);
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao pedir as mesas do tipo de string.", erro);
            Show();
            Avisar(Tr.F("Não consegui: {0}", erro.Message), erro: true);
        }
    }

    /// <summary>
    /// Se há comando em curso no desenho: o texto do SendStringToExecute
    /// iria como resposta a ele, e o pedido ficaria pendurado para o próximo
    /// comando digitado (revisão da etapa 11).
    /// </summary>
    private bool ComandoEmCurso() => !string.IsNullOrEmpty(_documento.CommandInProgress);

    /// <summary>O comando terminou: a janela volta, com o tipo escolhido e a frase do resultado.</summary>
    internal static void Retomar(Document documento, Guid? mostrar, string? frase, bool erro)
    {
        if (!Abertas.TryGetValue(documento, out var janela)) return;

        try
        {
            if (!janela.IsVisible) janela.Show();
            janela.Atualizar(mostrar);
            if (frase is not null) janela.Avisar(frase, erro);
            janela.Activate();
        }
        catch (Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trazer de volta a janela de strings.", falha);
        }
    }

    private void Renomear()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        var janela = new JanelaDeNome(Tr.T("Renomear tipo de string"), Tr.T("Como se chama este tipo de string?"));
        if (AcadApp.ShowModalWindow(janela) != true || janela.Nome is not { } nome) return;

        string? problema = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => problema = b.Rename(tipo.Id, nome));
            return problema is null ? Tr.F("Renomeado para {0}.", nome.Trim()) : null;
        }, tipo.Id);
        if (problema is not null) Avisar(Tr.F("Não renomeei: {0}.", problema), erro: true);
    }

    private void Clonar()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        StringType? copia = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => copia = b.Clone(tipo.Id));
            return copia is null ? null : Tr.F("{0} criado como cópia de {1}.", copia.Name, tipo.Name);
        });
        if (copia is not null) Atualizar(copia.Id);
    }

    private void Espelhar()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        string? porque = null;
        Fazer(() =>
        {
            MudarBiblioteca(b => porque = b.Mirror(tipo.Id));
            return porque is null ? Tr.F("{0} espelhado: o traçado foi refletido e o + e o − estão do outro lado da mesa.", tipo.Name) : null;
        }, tipo.Id);
        if (porque is not null) Avisar(Tr.F("Não gravei: {0}.", porque), erro: true);
    }

    private void ApagarUltimaString()
    {
        if (Escolhido is not { Routes.Count: > 0 } tipo)
        {
            Avisar(Tr.T("O tipo escolhido não tem string gravada."), erro: true);
            return;
        }

        Fazer(() =>
        {
            MudarBiblioteca(b => b.RemoveString(tipo.Id, tipo.Routes.Count - 1));
            return Tr.F("String {0} tirada de {1}.", tipo.Routes.Count, tipo.Name);
        }, tipo.Id);
    }

    private void Apagar()
    {
        if (Escolhido is not { } tipo)
        {
            Avisar(Tr.T("Escolha um tipo na lista."), erro: true);
            return;
        }

        Fazer(() =>
        {
            MudarBiblioteca(b => b.Remove(tipo.Id));
            return Tr.F("{0} apagado da biblioteca.", tipo.Name);
        });
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (!aberta.IsVisible) aberta.Show();
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeStrings(documento);
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
}
