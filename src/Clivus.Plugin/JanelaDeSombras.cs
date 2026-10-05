using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A janela das sombras (9.7 e 9.8), solta como a de Análises: dá para mexer
/// no desenho (pôr ou arrastar árvores) e gerar de novo sem fechar. O modo
/// arruma os campos: um instante (dia e hora), um dia inteiro (todas as
/// horas do dia), um horário fixo num período (a mesma hora em cada dia) ou
/// um período inteiro (todas as horas de todos os dias: mês, ano).
/// </summary>
internal sealed class JanelaDeSombras : Window
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<Document, JanelaDeSombras> Abertas = [];

    private readonly Document _documento;
    private readonly ComboBox _modo = new() { Width = 260, ToolTip = Tr.T("Um instante; um dia inteiro; o mesmo horário em cada dia de um período; ou todas as horas de um período. No período, cada módulo fica com a cor do pior caso.") };
    private readonly DatePicker _de = new() { Width = 130, ToolTip = Tr.T("O dia (ou o primeiro dia do período).") };
    private readonly DatePicker _ate = new() { Width = 130, ToolTip = Tr.T("O último dia do período.") };
    private readonly TextBox _horaDe = new() { Width = 60, ToolTip = Tr.T("A hora (ou a primeira hora de cada dia), hh:mm, no relógio local.") };
    private readonly TextBox _horaAte = new() { Width = 60, ToolTip = Tr.T("A última hora de cada dia, hh:mm.") };
    private readonly TextBox _passo = new() { Width = 50, Text = "30", ToolTip = Tr.T("De quantos em quantos minutos a sombra é calculada no dia.") };
    private readonly TextBox _dias = new() { Width = 40, Text = "1", ToolTip = Tr.T("De quantos em quantos dias, no período: 1 é todo dia, 7 é um dia por semana (o ano inteiro fica rápido).") };
    private readonly TextBox _fuso = new() { Width = 50, ToolTip = Tr.T("O fuso do relógio, em horas: -3 em Brasília.") };
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };

    private JanelaDeSombras(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Sombras");
        Width = 640;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var m in new[] { Tr.T("Instante (dia e hora)"), Tr.T("Dia inteiro"), Tr.T("Horário fixo num período"), Tr.T("Período inteiro (todas as horas)") }) _modo.Items.Add(m);

        var hoje = DateTime.Today;
        _de.SelectedDate = hoje;
        _ate.SelectedDate = hoje;
        _horaDe.Text = "09:00";
        _horaAte.Text = "09:00";

        var lugar = SombrasCommands.Lugar(documento);
        _fuso.Text = (lugar is { IsValid: true } l ? SombrasCommands.FusoPelaLongitude(l.Longitude) : -3).ToString("0.#", Brasil);

        _modo.SelectionChanged += (_, _) => AplicarModo();
        _modo.SelectedIndex = 0;

        var pilha = new StackPanel { Margin = new Thickness(12) };

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

        Button Atalho(string texto, string dica, Action acao)
        {
            var b = new Button { Content = texto, Padding = new Thickness(8, 0, 8, 0), Height = 24, Margin = new Thickness(0, 0, 6, 0), ToolTip = dica };
            b.Click += (_, _) => acao();
            return b;
        }

        pilha.Children.Add(Linha(R(Tr.T("Modo"), 70), _modo));
        pilha.Children.Add(Linha(R(Tr.T("Dias"), 70), _de, R(Tr.T("  a")), _ate, R(Tr.T("   a cada")), _dias, R(Tr.T(" dia(s)"))));
        pilha.Children.Add(Linha(R(Tr.T("Horário"), 70), _horaDe, R(Tr.T("  às")), _horaAte, R(Tr.T("   passo")), _passo, R(Tr.T(" min    fuso")), _fuso, R(" h")));
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
            })));

        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Fazem sombra as árvores (Sombreamento > Objetos), as outras mesas (a fileira da frente na de trás) e o relevo. Os módulos com sombra ficam lilás (até 25% da face), violeta (até 50%) ou roxo-escuro (acima); a linha de comando diz a causa de cada um. A sombra das árvores é desenhada no chão: no módulo, mais alto, ela cai um pouco ao lado. No período, vale o pior caso de cada módulo."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });

        pilha.Children.Add(_recado);

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var gerar = new Button { Content = Tr.T("Gerar sombras"), Width = 120, Height = 26, IsDefault = true, ToolTip = Tr.T("Apaga as sombras anteriores, calcula e desenha as novas, marcando os módulos.") };
        gerar.Click += (_, _) => Fazer(() =>
        {
            var periodo = SombrasCommands.Ler(Data(_de), Data(_ate), _horaDe.Text, _horaAte.Text, _passo.Text, _fuso.Text, out var porque, _dias.Text)
                ?? throw new ArgumentException(porque);
            return SombrasCommands.Gerar(_documento, periodo);
        });
        var apagar = new Button { Content = Tr.T("Apagar sombras"), Width = 120, Height = 26, Margin = new Thickness(8, 0, 0, 0), ToolTip = Tr.T("Apaga os contornos de sombra e devolve a cor de antes dos módulos marcados.") };
        apagar.Click += (_, _) => Fazer(() => SombrasCommands.Apagar(_documento.Database));

        // O porquê pede um clique no desenho: vai como comando (a janela fica aberta).
        var porQue = new Button { Content = Tr.T("Por que essa sombra?"), Height = 26, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(8, 0, 0, 0), ToolTip = Tr.T("Clique num módulo marcado: diz quanto da face pega sombra, de quê e em que dia e hora.") };
        porQue.Click += (_, _) =>
        {
            try
            {
                _documento.SendStringToExecute($"_{PluginInfo.ComandoSombraPorQue} ", true, false, false);
            }
            catch (Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao pedir o porquê da sombra.", erro);
            }
        };
        var fechar = new Button { Content = Tr.T("Fechar"), Width = 90, Height = 26, Margin = new Thickness(8, 0, 0, 0), IsCancel = true, ToolTip = Tr.T("Fecha a janela; as sombras desenhadas ficam.") };
        fechar.Click += (_, _) => Close();
        botoes.Children.Add(gerar);
        botoes.Children.Add(apagar);
        botoes.Children.Add(porQue);
        botoes.Children.Add(fechar);
        pilha.Children.Add(botoes);

        Content = pilha;
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

        var janela = new JanelaDeSombras(documento);
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
    }

    /// <summary>Roda com o documento travado (janela solta) e mostra o relato.</summary>
    private void Fazer(Func<string> operacao)
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

        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            string frase;

            // Fora de comando: trava o documento e cala o vigia (senão as
            // mesas com sombra viravam pendentes vermelhas na folga).
            frase = EscritaForaDeComando.Fazer(_documento, () =>
            {
                var f = operacao();
                _documento.Editor.Regen();
                return f;
            });

            _documento.Editor.WriteMessage($"\n{frase}\n");
            _recado.Foreground = Brushes.ForestGreen;
            _recado.Text = frase;
        }
        catch (Exception erro)
        {
            // Clique de WPF: exceção solta aqui derrubaria o Civil 3D.
            RegistroDeDiagnostico.Registrar("Falha na janela de sombras.", erro);
            _recado.Foreground = Brushes.Firebrick;
            _recado.Text = erro is ArgumentException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message);
        }
        finally
        {
            Cursor = null;
        }
    }
}
