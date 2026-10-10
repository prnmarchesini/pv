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
/// abas CC, Combiner, CA e MT. Cada aba só fica habilitada quando os dois
/// lados do trecho existem no desenho; a indisponível diz o que falta. A regra
/// é do Core (<see cref="CableRoutes"/>); aqui só se lê o desenho e se mostra.
/// Sem interface (Core Console), o mesmo vai para a linha de comando.
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

/// <summary>A janela da rota de cabos (17.1): uma por desenho; relê o desenho ao voltar para ela.</summary>
internal sealed class JanelaDeRotaDeCabos : Window
{
    private static readonly Dictionary<Document, JanelaDeRotaDeCabos> Abertas = [];

    private readonly Document _documento;
    private readonly TabControl _abas = new() { Margin = new Thickness(8) };
    private readonly Dictionary<CableRoute, TabItem> _itens = [];
    private readonly TextBlock _recado = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 8, 8) };

    private JanelaDeRotaDeCabos(Document documento)
    {
        _documento = documento;

        Title = Tr.T("Rota de cabos — Clivus Solar");
        Width = 720;
        Height = 420;
        MinWidth = 520;
        MinHeight = 300;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        foreach (var rota in CableRoutes.All)
        {
            var item = new TabItem { Header = CableRoutes.Title(rota) };
            // A dica da aba desabilitada diz o que falta: sem isto o WPF não a mostra.
            ToolTipService.SetShowOnDisabled(item, true);
            _itens[rota] = item;
            _abas.Items.Add(item);
        }

        var raiz = new DockPanel();
        DockPanel.SetDock(_recado, Dock.Bottom);
        raiz.Children.Add(_recado);
        raiz.Children.Add(_abas);
        Content = raiz;

        // Voltar para a janela relê o desenho (o usuário pode ter posto um trafo em campo).
        Activated += (_, _) => Atualizar();
        Atualizar();
    }

    private void Atualizar()
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
                item.Content = new TextBlock
                {
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap,
                    Text = CableRoutes.Description(rota) + "\n\n" +
                           Tr.T("Profundidade da vala, seleção da vala, cabo, gerar, ver cabos e apagar entram nos próximos passos."),
                };

                if (!livre) indisponiveis.Add(Tr.F("{0}: falta {1}.", CableRoutes.Title(rota), string.Join("; ", falta)));
            }

            // A aba aberta não pode ficar numa desabilitada: vai para a primeira livre.
            if (_abas.SelectedItem is not TabItem { IsEnabled: true })
                _abas.SelectedItem = CableRoutes.All.Select(r => _itens[r]).FirstOrDefault(i => i.IsEnabled);

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
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            aberta.Activate();
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
}
