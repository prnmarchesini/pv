using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// O que exportar para o PVsyst (42.4, Renan, 31/10/2026: "tenho que ter a
/// liberdade de exportar uma area, varias, todas, e pode selecionar as mesas
/// que eu quero se for o caso"): a mesma escolha da janela das sombras
/// (<see cref="SeletorDeEscopo"/>). Modal; o Exportar… devolve as mesas do
/// escopo e a janela do Windows pede o arquivo depois.
/// </summary>
internal sealed class JanelaDoPvsyst : Window
{
    private readonly Document _documento;
    private readonly SeletorDeEscopo _seletor;
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private IReadOnlySet<Guid>? _mesas;
    private string _nome = string.Empty;

    internal JanelaDoPvsyst(Document documento, IReadOnlyCollection<Guid> escolhidas)
    {
        _documento = documento;

        Title = Tr.T("Clivus Solar — Exportar para o PVsyst");
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        // A janela é modal: para escolher no desenho, ela se esconde enquanto a
        // pessoa clica (StartUserInteraction) e volta.
        _seletor = new SeletorDeEscopo(documento, Tr.T("Exportar"), Recado, Tentar,
            escolherArea: () =>
            {
                using (_documento.Editor.StartUserInteraction(this))
                {
                    if (EscolhaDeArea.Pedir(_documento.Editor, _documento) is { } area) _seletor!.MarcarArea(area.Identity.Id);
                }
            },
            escolherMesas: () =>
            {
                using (_documento.Editor.StartUserInteraction(this))
                {
                    var selecao = _documento.Editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as mesas a exportar: ") });
                    if (selecao.Status == PromptStatus.OK) _seletor!.DefinirMesas(SelecaoCommands.MesasTocadas(_documento, selecao.Value.GetObjectIds()));
                }
            },
            escolhidas);

        var pilha = new StackPanel { Margin = new Thickness(12) };
        pilha.Children.Add(_seletor.Painel);
        pilha.Children.Add(new TextBlock
        {
            Text = Tr.T("Cada mesa escolhida vai como uma fiada do PVsyst, com os módulos dentro (colunas, fileiras, medida do módulo e vãos), num arquivo PVC como o do PVcase. Vão junto as árvores e os equipamentos em campo até 100 m das mesas, e o terreno num CSV ao lado. No PVsyst, importe o PVC como cena 3D e o CSV como dados do terreno. A área leva as áreas que estão dentro dela."),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0),
        });
        pilha.Children.Add(_recado);

        var exportar = new Button { Content = Tr.T("Exportar…"), Height = 26, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 6, 0), IsDefault = true, ToolTip = Tr.T("Escolhe o arquivo e grava as mesas do escopo.") };
        exportar.Click += (_, _) => Tentar(Exportar);
        var cancelar = new Button { Content = Tr.T("Cancelar"), Width = 80, Height = 26, IsCancel = true };

        var botoes = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        botoes.Children.Add(exportar);
        botoes.Children.Add(cancelar);
        pilha.Children.Add(botoes);

        Content = pilha;
    }

    /// <summary>
    /// Pergunta o escopo. As mesas dele e o nome do escopo ("de area1"), ou
    /// null se a pessoa cancelou. <paramref name="escolhidas"/> são as mesas
    /// selecionadas antes do botão: com elas, a janela abre em Seleção.
    /// </summary>
    internal static (IReadOnlySet<Guid> Mesas, string Nome)? Perguntar(Document documento, IReadOnlyCollection<Guid> escolhidas)
    {
        var janela = new JanelaDoPvsyst(documento, escolhidas);
        return AcadApp.ShowModalWindow(janela) == true && janela._mesas is { } mesas ? (mesas, janela._nome) : null;
    }

    private void Exportar()
    {
        var escopo = _seletor.Escopo();
        var (mesas, nome) = ExportCommands.MesasDoEscopo(_documento.Database, escopo);

        if (mesas.Count == 0)
        {
            Recado(Tr.F("Nenhuma mesa {0}: nada a exportar.", nome), true);
            return;
        }

        _mesas = mesas;
        _nome = nome;
        DialogResult = true;
    }

    /// <summary>Manipulador de evento do WPF: exceção solta aqui fecha o Civil 3D.</summary>
    private void Tentar(Action acao)
    {
        try
        {
            acao();
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de exportar para o PVsyst.", erro);
            Recado(erro is ArgumentException or InvalidOperationException ? erro.Message : Tr.F("Não consegui: {0}", erro.Message), true);
        }
    }

    private void Recado(string texto, bool erro)
    {
        _recado.Foreground = erro ? Brushes.Firebrick : Brushes.ForestGreen;
        _recado.Text = texto;
    }
}
