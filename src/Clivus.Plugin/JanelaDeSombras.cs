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

    private readonly Document _documento;

    // Ocultar e Mostrar num botão só (42.3, tela de 31/10/2026: "ocultei e não
    // tem ver sombra"; o Mostrar ficava cortado pela borda da janela).
    private readonly Button _ligar;

    // O que calcular (a mesma escolha da exportação para o PVsyst, 42.4).
    private readonly SeletorDeEscopo _seletor;

    // O que faz sombra (42.3: "escolher o que eu quero (mesas, arvores, etc)").
    private readonly CheckBox _porMesas = new() { Content = Tr.T("Mesas"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 14, 2), ToolTip = Tr.T("As mesas fazem sombra umas nas outras e no chão.") };
    private readonly CheckBox _porArvores = new() { Content = Tr.T("Árvores e objetos"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 14, 2), ToolTip = Tr.T("As árvores e os objetos de Sombreamento > Objetos.") };
    private readonly CheckBox _porRelevo = new() { Content = Tr.T("Relevo (barranco)"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 14, 2), ToolTip = Tr.T("O próprio terreno: o barranco que esconde o sol.") };

    // O período.
    private readonly ComboBox _modo = new() { Width = 260, ToolTip = Tr.T("Um instante; um dia inteiro; o mesmo horário em cada dia de um período; ou todas as horas de um período. No período, a sombra desenhada é a de todos os passos juntos e cada módulo guarda o pior caso dele.") };
    private readonly DatePicker _de = new() { Width = 130, ToolTip = Tr.T("O dia (ou o primeiro dia do período).") };
    private readonly DatePicker _ate = new() { Width = 130, ToolTip = Tr.T("O último dia do período.") };
    private readonly TextBox _horaDe = new() { Width = 60, ToolTip = Tr.T("A hora (ou a primeira hora de cada dia), hh:mm, no relógio local.") };
    private readonly TextBox _horaAte = new() { Width = 60, ToolTip = Tr.T("A última hora de cada dia, hh:mm.") };
    private readonly TextBox _passo = new() { Width = 50, Text = "30", ToolTip = Tr.T("De quantos em quantos minutos a sombra é calculada no dia.") };
    private readonly TextBox _dias = new() { Width = 40, Text = "1", ToolTip = Tr.T("De quantos em quantos dias, no período: 1 é todo dia, 7 é um dia por semana (o ano inteiro fica rápido).") };
    // A altura solar junto com o horário (02/11/2026, Renan: "deixe assim e
    // tambem coloque a trava em altura solar, faz muito mais sentido, ai eu
    // escolho 'simular somente com altura solar maior que 15 graus'"): só
    // contam os instantes da janela de horário com o sol acima dela.
    private readonly CheckBox _soAcima = new() { Content = Tr.T("Simular só com o sol acima de"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2), ToolTip = Tr.T("No Dia inteiro e no Período inteiro, junto com o horário: só contam os instantes com o sol acima desta altura (as horas em que isso acontece mudam dia a dia). Para só a altura, ponha o horário de 00:00 às 23:59.") };
    private readonly TextBox _altura = new() { Width = 40, Text = "15", ToolTip = Tr.T("A altura solar mínima, em graus: só contam sombras com o sol acima dela.") };

    // O destaque dos piores módulos (42.15): os X piores, ou os com sombra em N dias ou mais.
    private readonly TextBox _quantosPiores = new() { Width = 40, Text = "10", ToolTip = Tr.T("Quantos módulos destacar: os de mais horas de sombra no período do último cálculo.") };
    private readonly TextBox _quantosDias = new() { Width = 40, Text = "30", ToolTip = Tr.T("Destaca os módulos com sombra em pelo menos tantos dias do período do último cálculo (com o ano inteiro, dias por ano).") };
    private readonly TextBlock _fuso = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
    private readonly double _fusoDoLugar;

    // O pior dia do ano no lugar do desenho, para o rótulo do atalho; sem localização, sem data.
    private readonly DateOnly? _piorDia;

    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    internal JanelaDeSombras(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Sombras");
        Width = 780;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        // Linha que quebra em vez de cortar (42.3): com a fonte ou a escala do
        // Windows maiores, o fim da linha ia para fora da janela.
        WrapPanel Linha(params UIElement[] itens)
        {
            var linha = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
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

        // ---- o que calcular: as áreas (uma caixa cada, à vista), o desenho
        // inteiro e as mesas escolhidas no desenho (42.3: "uma area pintou e na
        // outra nao [...] cade o botão de escolher area"). Abre com a primeira
        // área que tem mesa, o jeito de não travar o PC numa usina grande. Os
        // botões de escolher no desenho vão como comando: a janela fica aberta.

        _seletor = new SeletorDeEscopo(documento, Tr.T("Calcular"), Recado, Tentar);
        Closed += (_, _) => _seletor.Soltar();

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
        _piorDia = SombrasCommands.Lugar(documento) is { IsValid: true } doDesenho
            ? SolarCalculator.WorstShadowDay(doDesenho.Latitude, doDesenho.Longitude, hoje.Year, _fusoDoLugar)
            : null;

        // A caixa só liga o campo dos graus (revisão do 42.13: chamar o
        // AplicarModo inteiro resetava as horas). Desmarcada com o dia inteiro
        // do atalho de 20°, o horário volta ao usual, das 9h às 15h.
        _soAcima.Checked += (_, _) => Tentar(() => _altura.IsEnabled = _soAcima.IsEnabled);
        _soAcima.Unchecked += (_, _) => Tentar(() =>
        {
            _altura.IsEnabled = false;
            if (DiaInteiroNasCaixas()) (_horaDe.Text, _horaAte.Text) = ("09:00", "15:00");
        });

        _modo.SelectionChanged += (_, _) => Tentar(AplicarModo);
        _modo.SelectedIndex = 0;

        var pilha = new StackPanel { Margin = new Thickness(12) };

        pilha.Children.Add(_seletor.Painel);
        pilha.Children.Add(Linha(R(Tr.T("Fazem sombra"), 88), _porMesas, _porArvores, _porRelevo));
        pilha.Children.Add(Linha(R(Tr.T("Modo"), 88), _modo));
        pilha.Children.Add(Linha(R(Tr.T("Dias"), 88), _de, R(Tr.T("  a")), _ate, R(Tr.T("   a cada")), _dias, R(Tr.T(" dia(s)"))));
        pilha.Children.Add(Linha(R(Tr.T("Horário"), 88), _horaDe, R(Tr.T("  às")), _horaAte, R(Tr.T("   passo")), _passo, R(Tr.T(" min")), R("  "), _fuso));
        pilha.Children.Add(Linha(R(Tr.T("Altura solar"), 88), _soAcima, _altura, R("°")));
        pilha.Children.Add(Linha(
            R(Tr.T("Atalhos"), 88),
            // O pior dia do lugar do desenho (42.3: "Solsticio de inverno para
            // quem?"): junho no hemisfério sul, dezembro no norte.
            Atalho(_piorDia is { } pior ? Tr.F("Pior dia do ano ({0:dd/MM})", pior) : Tr.T("Pior dia do ano"),
                Tr.T("O dia de sombra mais longa no lugar do desenho (o sol mais baixo ao meio-dia): no hemisfério sul, como no Brasil, é o solstício de junho; no norte, como no Panamá, o de dezembro. Das 9h às 15h, de meia em meia hora: o critério usual (nenhuma sombra das 9h às 15h no pior dia do ano)."),
                () =>
                {
                    var lugar = SombrasCommands.Lugar(_documento);
                    if (lugar is not { IsValid: true })
                        throw new InvalidOperationException(Tr.T("O desenho não tem localização: defina em Terreno > Resumo > Localização para saber o pior dia do ano do lugar."));

                    var dia = SolarCalculator.WorstShadowDay(lugar.Latitude, lugar.Longitude, (_de.SelectedDate ?? hoje).Year, _fusoDoLugar).ToDateTime(TimeOnly.MinValue);
                    Preencher(1, dia, dia, "09:00", "15:00", "30", "1", soAcima: false);
                }),
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
            // O dia inteiro (00:00 às 23:59) com a altura: os horários saem do sol.
            Atalho(Tr.T("Ano, sol acima de 20°"), Tr.T("Um dia por semana do ano, de 30 em 30 minutos, contando só o sol acima de 20°: os horários se ajustam ao dia."), () =>
            {
                var d = _de.SelectedDate ?? hoje;
                Preencher(3, new DateTime(d.Year, 1, 1), new DateTime(d.Year, 12, 31), "00:00", "23:59", "30", "7", soAcima: true, alturaMinima: "20");
            })));

        // Os piores módulos (42.15): pelos dias e horas que o último cálculo guardou, no escopo de "Calcular".
        pilha.Children.Add(Linha(
            R(Tr.T("Destacar"), 88),
            R(Tr.T("os")), _quantosPiores, R(Tr.T("piores módulos")),
            Atalho(Tr.T("Mostrar"), Tr.T("Pinta no desenho os piores módulos do escopo, do vinho (o pior) ao amarelo, com o lugar escrito em cima; o destaque anterior sai."), () =>
                Fazer(() => SombrasCommands.Destacar(_documento, _seletor.Escopo(), piores: true, Quantos(_quantosPiores)))),
            R("   "),
            R(Tr.T("os com sombra em")), _quantosDias, R(Tr.T("dias ou mais")),
            Atalho(Tr.T("Mostrar"), Tr.T("Pinta no desenho os módulos do escopo com sombra em pelo menos tantos dias, dos mais sombreados aos menos."), () =>
                Fazer(() => SombrasCommands.Destacar(_documento, _seletor.Escopo(), piores: false, Quantos(_quantosDias)))),
            R("   "),
            Atalho(Tr.T("Limpar destaque"), Tr.T("Tira o destaque dos piores módulos do desenho (as sombras ficam)."), () =>
                Fazer(() => SombrasCommands.LimparDestaque(_documento)))));

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("A sombra é desenhada em magenta no chão (acompanhando o terreno) e sobre os módulos, até onde ela pega; num dia ou período, é a soma das sombras de todos os passos. Cada cálculo troca só as sombras do que foi marcado, e as áreas convivem no desenho."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });

        pilha.Children.Add(_recado);

        // ---- os botões

        var gerar = Botao(Tr.T("Gerar sombras"), Tr.T("Calcula o escopo no período e troca as sombras dele pelas novas: hachura magenta no chão e sobre os módulos."), Gerar);
        gerar.IsDefault = true;
        var apagar = Botao(Tr.T("Apagar sombras"), Tr.T("Apaga as sombras do escopo escolhido (as de outras áreas ficam) e devolve a cor dos módulos pintados por desenho antigo."), Apagar);
        _ligar = Botao(Tr.T("Ocultar sombras"), Tr.T("Esconde ou mostra todas as sombras: desliga ou liga as camadas delas. Nada é apagado."), () =>
        {
            // O que o botão diz é o que ele faz: à vista, oculta; escondidas, mostra.
            Fazer(() => SombrasCommands.Ligar(_documento, SombrasCommands.SombrasAVista(_documento.Database) == false));
        });
        var analises = Botao(Tr.T("Ver análises…"), Tr.T("Abre a lista dos módulos com sombra, por área e mesa, com quanto, de quê e quando; o clique leva ao lugar no desenho."), () => JanelaDeAnalisesDeSombra.Abrir(_documento));

        // O porquê pede um clique no desenho: vai como comando (a janela fica aberta).
        var porQue = Botao(Tr.T("Por que essa sombra?"), Tr.T("Clique num módulo: diz quanto da face pega sombra, de quê e em que dia e hora."), () =>
        {
            if (!ClivusExtension.TemInterface()) return;
            _documento.SendStringToExecute($"_{PluginInfo.ComandoSombraPorQue} ", true, false, false);
        });

        // O resumo de uma mesa (42.14): o período desta janela vai junto; a mesa se escolhe no desenho.
        var daMesa = Botao(Tr.T("Resumo de uma mesa…"), Tr.T("Clique numa mesa no desenho: o calendário das sombras dela no período desta janela, módulo a módulo, com os dias e a % em cada hora."), () =>
        {
            if (!ClivusExtension.TemInterface()) return;
            SombrasCommands.PedidoDaMesa = (PeriodoDaTela(), QueFazSombra());
            _documento.SendStringToExecute($"_{PluginInfo.ComandoSombrasMesa} ", true, false, false);
        });

        var fechar = new Button { Content = Tr.T("Fechar"), Width = 80, Height = 26, IsCancel = true, ToolTip = Tr.T("Fecha a janela; as sombras desenhadas ficam.") };
        fechar.Click += (_, _) => Tentar(Close);

        // Os botões numa linha que quebra: nenhum fica escondido atrás de outro
        // (o Mostrar ficava atrás do Ver análises, 42.3).
        var botoes = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        botoes.Children.Add(gerar);
        botoes.Children.Add(apagar);
        botoes.Children.Add(_ligar);
        botoes.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(4, 3, 10, 3), Background = Brushes.Silver });
        botoes.Children.Add(analises);
        botoes.Children.Add(porQue);
        botoes.Children.Add(daMesa);
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        Content = pilha;


        // O roxo da versão antiga sai ao abrir (42.3): ele não é a sombra de
        // hoje, o Ocultar não o escondia e o 3D o mostrava como sombra.
        Tentar(() =>
        {
            var voltaram = EscritaForaDeComando.Fazer(_documento, () => SombrasCommands.TirarPinturaAntiga(_documento.Database));
            if (voltaram == 0) return;

            RegistroDeDiagnostico.Registrar($"Sombras: a pintura da versão antiga saiu de {voltaram} módulo(s) ({_documento.Name}).");
            if (ClivusExtension.TemInterface()) _documento.Editor.Regen();
            Recado(Tr.F("A pintura roxa da versão antiga saiu de {0} módulo(s): a sombra agora é a hachura magenta, no chão e sobre os módulos (Gerar sombras).", voltaram), false);
        });

        Tentar(MostrarCamadas);
    }

    /// <summary>O que faz sombra, pelas três caixas.</summary>
    private ShadowCasters QueFazSombra() => new(_porMesas.IsChecked == true, _porArvores.IsChecked == true, _porRelevo.IsChecked == true);

    /// <summary>O botão de ligar diz o que vai fazer: Ocultar com as sombras à vista, Mostrar com elas escondidas.</summary>
    private void MostrarCamadas()
    {
        var aVista = SombrasCommands.SombrasAVista(_documento.Database);
        _ligar.Content = aVista == false ? Tr.T("Mostrar sombras") : Tr.T("Ocultar sombras");
        _ligar.IsEnabled = aVista is not null;
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Tentar(() => aberta._seletor.LerAreas());
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

    // ------------------------------------------------------------ as ações

    private void Gerar()
    {
        var escopo = _seletor.Escopo();

        Fazer(() =>
        {
            var periodo = PeriodoDaTela();

            // Com o andamento e o Cancelar (05/10/2026); esta janela fica travada
            // enquanto isso. O recado diz o período que valeu (revisão do 42.13).
            var relato = SombrasCommands.GerarComAndamento(_documento, periodo, escopo, this, QueFazSombra());
            return relato with { Resumo = relato.Resumo + Environment.NewLine + Tr.F("Período: {0}.", periodo.Descrever()) };
        });
    }

    /// <summary>
    /// O período como a tela está (o Gerar e o resumo de uma mesa): o modo
    /// trava o que não vale nele, e a altura solar vai à parte, junto com o
    /// horário (revisão do 42.13). Período que não vale: ArgumentException com o porquê.
    /// </summary>
    private PeriodoDeSombra PeriodoDaTela()
    {
        AjustarModo();

        var comAltura = _soAcima.IsChecked == true && _modo.SelectedIndex is 1 or 3;
        return SombrasCommands.Ler(Data(_de), Data(_ate), _horaDe.Text, _horaAte.Text, _passo.Text, _fusoDoLugar.ToString(CultureInfo.InvariantCulture), out var porque,
                _dias.Text, comAltura ? _altura.Text : null)
            ?? throw new ArgumentException(porque);
    }

    /// <summary>O número de uma caixa do destaque; o que não é número vira 0 (e o destaque diz).</summary>
    private static int Quantos(TextBox caixa) =>
        int.TryParse(caixa.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private void Apagar()
    {
        var escopo = _seletor.Escopo();

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
            Tentar(MostrarCamadas);
        }
    }

    // ------------------------------------------------------------ o período

    private static string Data(DatePicker d) => (d.SelectedDate ?? DateTime.Today).ToString("dd/MM/yyyy", Brasil);

#if DEBUG
    /// <summary>Para o nível 2 desenhar a janela (42.13): o ano inteiro, das 08:00 às 16:30, com o sol acima de 15°.</summary>
    internal void PeriodoComAlturaParaTeste() =>
        Preencher(3, new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), "08:00", "16:30", "30", "1", soAcima: true, alturaMinima: "15");
#endif

    /// <summary>
    /// O atalho preenche o período. A altura solar só muda com
    /// <paramref name="soAcima"/>: o pior dia desliga (o critério das 9h às
    /// 15h), o "Ano, sol acima de 20°" liga; os outros deixam como está
    /// (revisão do 42.13).
    /// </summary>
    private void Preencher(int modo, DateTime de, DateTime ate, string horaDe, string horaAte, string? passo = null, string? dias = null, bool? soAcima = null, string? alturaMinima = null)
    {
        _modo.SelectedIndex = modo;
        _de.SelectedDate = de;
        _ate.SelectedDate = ate;
        _horaDe.Text = horaDe;
        _horaAte.Text = horaAte;
        if (passo is not null) _passo.Text = passo;
        if (dias is not null) _dias.Text = dias;
        if (alturaMinima is not null) _altura.Text = alturaMinima;
        if (soAcima is { } liga) _soAcima.IsChecked = liga;
        AplicarModo();
    }

    /// <summary>O horário nas caixas é o dia inteiro (00:00 às 23:59), o do atalho da altura.</summary>
    private bool DiaInteiroNasCaixas() => _horaDe.Text.Trim() == "00:00" && _horaAte.Text.Trim() == "23:59";

    /// <summary>O modo trava o que não vale nele.</summary>
    private void AplicarModo()
    {
        // O dia inteiro do atalho da altura não serve ao instante nem ao horário fixo: 9h (revisão do 42.13).
        if (_modo.SelectedIndex is 0 or 2 && DiaInteiroNasCaixas()) (_horaDe.Text, _horaAte.Text) = ("09:00", "09:00");

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

        // A altura solar vale no dia inteiro e no período inteiro, junto com o
        // horário (02/11/2026): só contam os instantes da janela com o sol
        // acima dela. No instante e no horário fixo, não.
        var podeAltura = _modo.SelectedIndex is 1 or 3;
        _soAcima.IsEnabled = podeAltura;
        _altura.IsEnabled = podeAltura && _soAcima.IsChecked == true;
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
