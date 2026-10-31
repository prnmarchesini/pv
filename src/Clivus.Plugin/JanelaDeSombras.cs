using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela das sombras (9.7, 9.8 e 42.1), solta como a de Áreas: dá para
/// mexer no desenho (pôr árvores, escolher mesas) e gerar de novo sem fechar.
/// <para>
/// Em cima, o que calcular (42.1, Renan, 31/10/2026: "Seleção, tudo de uma
/// area, tudo de uma area mais seleção, varias areas, desenho inteiro"); no
/// meio, o período (um instante, um dia inteiro, um horário fixo num período
/// ou um período inteiro); embaixo, o resultado numa ou duas linhas (o
/// detalhe vai para a linha de comando e os motivos, para Ver análises) e os
/// botões: Gerar e Apagar no escopo, Ocultar e Mostrar, Ver análises e o Por
/// que essa sombra?.
/// </para>
/// <para>
/// Todo clique passa por <see cref="Tentar"/> (exceção solta num manipulador
/// do WPF fecha o Civil 3D), e toda escrita é feita fora de comando por
/// <see cref="EscritaForaDeComando"/> (documento travado, vigia calado).
/// Compacta (regra de 02/10/2026): rótulo ao lado, nada de espaço vazio.
/// </para>
/// </summary>
internal sealed class JanelaDeSombras : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<Document, JanelaDeSombras> Abertas = [];

    /// <summary>Uma área na lista: o GUID e o nome.</summary>
    private sealed record AreaDaLista(Guid Id, string Nome)
    {
        public override string ToString() => Nome;
    }

    private readonly Document _documento;

    // O que calcular.
    private readonly ComboBox _escopo = new() { Width = 150, ToolTip = Tr.T("O que calcular: as mesas escolhidas no desenho, uma área, uma área e mais as mesas escolhidas, várias áreas ou o desenho inteiro. Fazem sombra também as mesas, as árvores e o relevo em volta.") };
    private readonly ComboBox _area = new() { Width = 190, Margin = new Thickness(6, 0, 0, 0), ToolTip = Tr.T("A área: as mesas com o centro nela e o chão dela.") };
    private readonly WrapPanel _areas = new() { Margin = new Thickness(76, 2, 0, 2) };
    private readonly Button _pegar;
    private readonly TextBlock _selecao = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, Margin = new Thickness(6, 0, 0, 0) };
    private HashSet<Guid> _mesasEscolhidas = [];

    // O período.
    private readonly ComboBox _modo = new() { Width = 260, ToolTip = Tr.T("Um instante; um dia inteiro; o mesmo horário em cada dia de um período; ou todas as horas de um período. No período, a sombra desenhada é a de todos os passos juntos e cada módulo guarda o pior caso dele.") };
    private readonly DatePicker _de = new() { Width = 130, ToolTip = Tr.T("O dia (ou o primeiro dia do período).") };
    private readonly DatePicker _ate = new() { Width = 130, ToolTip = Tr.T("O último dia do período.") };
    private readonly TextBox _horaDe = new() { Width = 60, ToolTip = Tr.T("A hora (ou a primeira hora de cada dia), hh:mm, no relógio local.") };
    private readonly TextBox _horaAte = new() { Width = 60, ToolTip = Tr.T("A última hora de cada dia, hh:mm.") };
    private readonly TextBox _passo = new() { Width = 50, Text = "30", ToolTip = Tr.T("De quantos em quantos minutos a sombra é calculada no dia.") };
    private readonly TextBox _dias = new() { Width = 40, Text = "1", ToolTip = Tr.T("De quantos em quantos dias, no período: 1 é todo dia, 7 é um dia por semana (o ano inteiro fica rápido).") };
    private readonly ComboBox _criterio = new() { Width = 170, ToolTip = Tr.T("Que instantes contam no período: os de uma janela de horário, ou os com o sol acima de uma altura (os horários se ajustam dia a dia).") };
    private readonly TextBox _altura = new() { Width = 40, Text = "20", ToolTip = Tr.T("A altura solar mínima, em graus: só contam sombras com o sol acima dela.") };
    private readonly TextBlock _fuso = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
    private readonly double _fusoDoLugar;

    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private JanelaDeSombras(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Sombras");
        Width = 780;
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
            b.Click += (_, _) => Tentar(acao);
            return b;
        }

        Button Atalho(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Padding = new Thickness(8, 0, 8, 0), Height = 24, Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => Tentar(acao);
            return b;
        }

        // ---- o que calcular

        foreach (var k in ShadowScope.Kinds) _escopo.Items.Add(ShadowScope.Name(k));
        _escopo.SelectedIndex = ShadowScope.Kinds.ToList().IndexOf(ShadowScopeKind.Area);
        _escopo.SelectionChanged += (_, _) => Tentar(AplicarEscopo);
        _area.DropDownOpened += (_, _) => Tentar(LerAreas);

        _pegar = Botao(Tr.T("Pegar seleção"), Tr.T("Usa as mesas selecionadas agora no desenho (clique nelas com a janela aberta e depois aqui)."), PegarSelecao);
        _pegar.Margin = new Thickness(6, 0, 0, 0);

        // ---- o período

        foreach (var m in new[] { Tr.T("Instante (dia e hora)"), Tr.T("Dia inteiro"), Tr.T("Horário fixo num período"), Tr.T("Período inteiro (todas as horas)") }) _modo.Items.Add(m);

        var hoje = DateTime.Today;
        _de.SelectedDate = hoje;
        _ate.SelectedDate = hoje;
        _horaDe.Text = "09:00";
        _horaAte.Text = "09:00";

        // O fuso vem do lugar do desenho (Terreno > Resumo > Localização), não desta janela.
        _fusoDoLugar = SombrasCommands.FusoDoLugar(documento);
        _fuso.Text = Tr.F("Fuso {0:+0.#;-0.#;0} h, do lugar do desenho (Terreno > Resumo).", _fusoDoLugar);

        _criterio.Items.Add(Tr.T("Janela de horário"));
        _criterio.Items.Add(Tr.T("Sol acima de"));
        _criterio.SelectedIndex = 0;
        _criterio.SelectionChanged += (_, _) => Tentar(AplicarModo);

        _modo.SelectionChanged += (_, _) => Tentar(AplicarModo);
        _modo.SelectedIndex = 0;

        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(Linha(R(Tr.T("Calcular"), 70), _escopo, _area, _pegar, _selecao));
        pilha.Children.Add(_areas);
        pilha.Children.Add(Linha(R(Tr.T("Modo"), 70), _modo));
        pilha.Children.Add(Linha(R(Tr.T("Dias"), 70), _de, R(Tr.T("  a")), _ate, R(Tr.T("   a cada")), _dias, R(Tr.T(" dia(s)"))));
        pilha.Children.Add(Linha(R(Tr.T("Considerar"), 70), _criterio, R(" "), _altura, R("°")));
        pilha.Children.Add(Linha(R(Tr.T("Horário"), 70), _horaDe, R(Tr.T("  às")), _horaAte, R(Tr.T("   passo")), _passo, R(Tr.T(" min")), R("  "), _fuso));
        pilha.Children.Add(Linha(
            R(Tr.T("Atalhos"), 70),
            Atalho(Tr.T("Solstício de inverno"), Tr.T("21 de junho, das 9h às 15h de meia em meia hora: o critério usual (sem sombra das 9h às 15h no dia de sombra mais longa do ano no Brasil)."), () => Preencher(1, new DateTime(hoje.Year, 6, 21), new DateTime(hoje.Year, 6, 21), "09:00", "15:00", "30", "1")),
            Atalho(Tr.T("Este mês"), Tr.T("Todos os dias do mês do primeiro dia, das 9h às 15h, de hora em hora."), () =>
            {
                var d = _de.SelectedDate ?? hoje;
                Preencher(3, new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)), "09:00", "15:00", "60", "1");
            }),
            Atalho(Tr.T("Este ano"), Tr.T("Um dia por semana do ano do primeiro dia, das 9h às 15h, de hora em hora."), () =>
            {
                var d = _de.SelectedDate ?? hoje;
                Preencher(3, new DateTime(d.Year, 1, 1), new DateTime(d.Year, 12, 31), "09:00", "15:00", "60", "7");
            }),
            Atalho(Tr.T("Ano, sol acima de 20°"), Tr.T("Um dia por semana do ano, de 30 em 30 minutos, contando só o sol acima de 20°: os horários se ajustam ao dia."), () =>
            {
                var d = _de.SelectedDate ?? hoje;
                Preencher(3, new DateTime(d.Year, 1, 1), new DateTime(d.Year, 12, 31), "09:00", "15:00", "30", "7");
                _criterio.SelectedIndex = 1;
                _altura.Text = "20";
            })));

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Fazem sombra as mesas, as árvores (Sombreamento > Objetos) e o relevo. A sombra é desenhada em magenta no chão (acompanhando o terreno) e sobre os módulos, até onde ela pega; cada escopo troca só as sombras dele, e as áreas convivem no desenho."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });

        pilha.Children.Add(_recado);

        // ---- os botões

        var gerar = Botao(Tr.T("Gerar sombras"), Tr.T("Calcula o escopo no período e troca as sombras dele pelas novas: hachura magenta no chão e sobre os módulos."), Gerar);
        gerar.IsDefault = true;
        var apagar = Botao(Tr.T("Apagar sombras"), Tr.T("Apaga as sombras do escopo escolhido (as de outras áreas ficam) e devolve a cor dos módulos pintados por desenho antigo."), Apagar);
        var ocultar = Botao(Tr.T("Ocultar"), Tr.T("Esconde todas as sombras: desliga as camadas delas. Nada é apagado."), () => Fazer(() => SombrasCommands.Ligar(_documento, false)));
        var mostrar = Botao(Tr.T("Mostrar"), Tr.T("Mostra as sombras de novo: liga as camadas delas."), () => Fazer(() => SombrasCommands.Ligar(_documento, true)));
        var analises = Botao(Tr.T("Ver análises…"), Tr.T("Abre a lista dos módulos com sombra, por área e mesa, com quanto, de quê e quando; o clique leva ao lugar no desenho."), () => JanelaDeAnalisesDeSombra.Abrir(_documento));

        // O porquê pede um clique no desenho: vai como comando (a janela fica aberta).
        var porQue = Botao(Tr.T("Por que essa sombra?"), Tr.T("Clique num módulo: diz quanto da face pega sombra, de quê e em que dia e hora."), () =>
        {
            if (!ClivusExtension.TemInterface()) return;
            _documento.SendStringToExecute($"_{PluginInfo.ComandoSombraPorQue} ", true, false, false);
        });

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 80, Height = 26, IsCancel = true, ToolTip = Tr.T("Fecha a janela; as sombras desenhadas ficam.") };
        fechar.Click += (_, _) => Tentar(Close);

        var esquerda = new StackPanel { Orientation = Orientation.Horizontal };
        esquerda.Children.Add(gerar);
        esquerda.Children.Add(apagar);
        esquerda.Children.Add(new Border { Width = 1, Margin = new Thickness(4, 3, 10, 3), Background = Brushes.Silver });
        esquerda.Children.Add(ocultar);
        esquerda.Children.Add(mostrar);

        var direita = new StackPanel { Orientation = Orientation.Horizontal };
        direita.Children.Add(analises);
        direita.Children.Add(porQue);
        direita.Children.Add(fechar);

        var botoes = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        DockPanel.SetDock(direita, Dock.Right);
        botoes.Children.Add(direita);
        botoes.Children.Add(esquerda);
        pilha.Children.Add(botoes);

        Content = pilha;

        // Abre em "Uma área" (o jeito de não travar o PC numa usina grande);
        // sem área no desenho, no desenho inteiro.
        Tentar(LerAreas);
        if (_area.Items.Count == 0)
        {
            _escopo.SelectedIndex = ShadowScope.Kinds.ToList().IndexOf(ShadowScopeKind.Drawing);
            _recado.Text = string.Empty;
        }

        Tentar(AplicarEscopo);
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Tentar(aberta.LerAreas);
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeSombras(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            try
            {
                if (e.Document == documento) janela.Close();
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao fechar a janela de sombras junto com o desenho.", erro);
            }
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) =>
        {
            try
            {
                Abertas.Remove(documento);
                AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar a janela de sombras.", erro);
            }
        };

        AcadApp.ShowModelessWindow(janela);
    }

    // ------------------------------------------------------------ o escopo

    private ShadowScopeKind Tipo => _escopo.SelectedIndex >= 0 ? ShadowScope.Kinds[_escopo.SelectedIndex] : ShadowScopeKind.Area;

    /// <summary>Lê as áreas do desenho para a lista e as caixas, mantendo as escolhidas.</summary>
    private void LerAreas()
    {
        var antes = (_area.SelectedItem as AreaDaLista)?.Id;
        var marcadas = _areas.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToHashSet();

        var areas = EscolhaDeArea.Distintas(AreasDoDesenho.Varrer(_documento.Database))
            .Select(a => new AreaDaLista(a.Identity.Id, a.Identity.DisplayName))
            .ToList();

        _area.ItemsSource = areas;
        _area.SelectedItem = areas.FirstOrDefault(a => a.Id == antes) ?? areas.FirstOrDefault();

        _areas.Children.Clear();
        foreach (var a in areas)
            _areas.Children.Add(new CheckBox { Content = a.Nome, Tag = a, IsChecked = marcadas.Contains(a.Id), Margin = new Thickness(0, 2, 14, 2) });

        if (areas.Count == 0 && Tipo is not (ShadowScopeKind.Selection or ShadowScopeKind.Drawing))
            Recado(Tr.T("Nenhuma área neste desenho: use Seleção ou Desenho inteiro (ou crie a área com o botão Área)."), true);
    }

    /// <summary>O escopo mostra só o que vale nele.</summary>
    private void AplicarEscopo()
    {
        var tipo = Tipo;

        _area.Visibility = tipo is ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection ? Visibility.Visible : Visibility.Collapsed;
        _areas.Visibility = tipo == ShadowScopeKind.Areas ? Visibility.Visible : Visibility.Collapsed;

        var selecao = tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection;
        _pegar.Visibility = selecao ? Visibility.Visible : Visibility.Collapsed;
        _selecao.Visibility = selecao ? Visibility.Visible : Visibility.Collapsed;
        MostrarSelecao();
    }

    /// <summary>As mesas selecionadas agora no desenho.</summary>
    private void PegarSelecao()
    {
        var implicita = _documento.Editor.SelectImplied();
        var ids = implicita.Status == PromptStatus.OK ? implicita.Value.GetObjectIds() : [];
        _mesasEscolhidas = SelecaoCommands.MesasTocadas(_documento, ids);
        MostrarSelecao();

        if (_mesasEscolhidas.Count == 0) Recado(Tr.T("Nenhuma mesa selecionada no desenho: clique nas mesas (com a janela aberta) e use Pegar seleção."), true);
        else _recado.Text = string.Empty;
    }

    private void MostrarSelecao() =>
        _selecao.Text = _mesasEscolhidas.Count == 0 ? Tr.T("nenhuma mesa escolhida") : Tr.F("{0} mesa(s) escolhida(s)", _mesasEscolhidas.Count);

    /// <summary>O escopo escolhido; sem seleção guardada, a do desenho agora. Lança <see cref="ArgumentException"/> com o que falta.</summary>
    private ShadowScope Escopo()
    {
        var tipo = Tipo;

        if (tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection && _mesasEscolhidas.Count == 0) PegarSelecao();

        IReadOnlyList<Guid> areas = tipo switch
        {
            ShadowScopeKind.Area or ShadowScopeKind.AreaAndSelection => _area.SelectedItem is AreaDaLista a ? [a.Id] : [],
            ShadowScopeKind.Areas => _areas.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ((AreaDaLista)c.Tag).Id).ToList(),
            _ => [],
        };

        var comSelecao = tipo is ShadowScopeKind.Selection or ShadowScopeKind.AreaAndSelection;
        var escopo = new ShadowScope(tipo, areas, comSelecao ? [.. _mesasEscolhidas] : []);
        return escopo.WhyInvalid() is { } falta ? throw new ArgumentException(falta) : escopo;
    }

    // ------------------------------------------------------------ as ações

    private void Gerar()
    {
        var escopo = Escopo();

        Fazer(() =>
        {
            AjustarModo();

            // Altura solar: a hora inicial vai como ">20" (SombrasCommands.Ler).
            var porAltura = _criterio.SelectedIndex == 1 && _modo.SelectedIndex != 0;
            var horaDe = porAltura ? ">" + _altura.Text.Trim() : _horaDe.Text;
            var periodo = SombrasCommands.Ler(Data(_de), Data(_ate), horaDe, _horaAte.Text, _passo.Text, _fusoDoLugar.ToString(CultureInfo.InvariantCulture), out var porque, _dias.Text)
                ?? throw new ArgumentException(porque);

            // Com o andamento e o Cancelar (05/10/2026); esta janela fica travada enquanto isso.
            return SombrasCommands.GerarComAndamento(_documento, periodo, escopo, this);
        });
    }

    private void Apagar()
    {
        var escopo = Escopo();

        if (escopo.Kind == ShadowScopeKind.Drawing
            && MessageBox.Show(this, Tr.T("Apagar as sombras do desenho inteiro (todas as áreas)?"), Tr.T("Apagar sombras"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        Fazer(() => SombrasCommands.Apagar(_documento, escopo));
    }

    /// <summary>Roda com o documento travado e o vigia calado (janela solta), mostra o resumo aqui e o relato na linha de comando.</summary>
    private void Fazer(Func<RelatoDasSombras> operacao)
    {
        if (AcadApp.DocumentManager.MdiActiveDocument != _documento)
        {
            Recado(Tr.T("Este desenho não é o ativo: clique nele e repita."), true);
            return;
        }

        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;

            // Fora de comando: trava o documento e cala o vigia (senão o que a
            // sombra desenha virava pendência na folga).
            var relato = EscritaForaDeComando.Fazer(_documento, () =>
            {
                var r = operacao();
                if (ClivusExtension.TemInterface()) _documento.Editor.Regen();
                return r;
            });

            _documento.Editor.WriteMessage($"\n{relato}\n");
            Recado(relato.Resumo, false);
        }
        finally
        {
            Cursor = null;
        }
    }

    // ------------------------------------------------------------ o período

    private static string Data(DatePicker d) => (d.SelectedDate ?? DateTime.Today).ToString("dd/MM/yyyy", Brasil);

    private void Preencher(int modo, DateTime de, DateTime ate, string horaDe, string horaAte, string? passo = null, string? dias = null)
    {
        _modo.SelectedIndex = modo;
        _de.SelectedDate = de;
        _ate.SelectedDate = ate;
        _horaDe.Text = horaDe;
        _horaAte.Text = horaAte;
        if (passo is not null) _passo.Text = passo;
        if (dias is not null) _dias.Text = dias;
        AplicarModo();
    }

    /// <summary>O modo trava o que não vale nele.</summary>
    private void AplicarModo()
    {
        switch (_modo.SelectedIndex)
        {
            case 0:
                _ate.SelectedDate = _de.SelectedDate;
                _horaAte.Text = _horaDe.Text;
                _ate.IsEnabled = false;
                _horaAte.IsEnabled = false;
                _passo.IsEnabled = false;
                _dias.IsEnabled = false;
                break;
            case 1:
                _ate.SelectedDate = _de.SelectedDate;
                if (_horaAte.Text == _horaDe.Text) (_horaDe.Text, _horaAte.Text) = ("09:00", "15:00");
                _ate.IsEnabled = false;
                _horaAte.IsEnabled = true;
                _passo.IsEnabled = true;
                _dias.IsEnabled = false;
                break;
            case 2:
                _horaAte.Text = _horaDe.Text;
                _ate.IsEnabled = true;
                _horaAte.IsEnabled = false;
                _passo.IsEnabled = false;
                _dias.IsEnabled = true;
                break;
            default:
                if (_horaAte.Text == _horaDe.Text) (_horaDe.Text, _horaAte.Text) = ("09:00", "15:00");
                _ate.IsEnabled = true;
                _horaAte.IsEnabled = true;
                _passo.IsEnabled = true;
                _dias.IsEnabled = true;
                break;
        }

        // A altura solar vale no dia inteiro e no período inteiro; com ela,
        // o horário não conta (os horários saem do sol, dia a dia).
        var podeAltura = _modo.SelectedIndex is 1 or 3;
        _criterio.IsEnabled = podeAltura;
        if (!podeAltura && _criterio.SelectedIndex == 1) _criterio.SelectedIndex = 0;

        var porAltura = podeAltura && _criterio.SelectedIndex == 1;
        _altura.IsEnabled = porAltura;
        if (porAltura)
        {
            _horaDe.IsEnabled = false;
            _horaAte.IsEnabled = false;
        }
        else
        {
            _horaDe.IsEnabled = true;
        }
    }

    /// <summary>Antes de gerar, o que o modo trava acompanha o que foi digitado.</summary>
    private void AjustarModo()
    {
        if (_modo.SelectedIndex == 0)
        {
            _ate.SelectedDate = _de.SelectedDate;
            _horaAte.Text = _horaDe.Text;
        }
        else if (_modo.SelectedIndex == 1)
        {
            _ate.SelectedDate = _de.SelectedDate;
        }
        else if (_modo.SelectedIndex == 2)
        {
            _horaAte.Text = _horaDe.Text;
        }
    }

    // ------------------------------------------------------------ apoio

    /// <summary>Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de sombras.", erro);
            Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
        }
    }

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }
}
