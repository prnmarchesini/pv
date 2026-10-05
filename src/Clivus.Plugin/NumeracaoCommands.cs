using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.NumeracaoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A numeração das strings (elétrica, etapa 15). CLIVUS_NUMERACAO abre a aba
/// Numeração numa janela solta (a mesma aba da janela da configuração
/// elétrica); CLIVUS_NUMERACAO_AUTO faz o mesmo pela linha de comando, para o
/// nível 2.
/// </summary>
public static class NumeracaoCommands
{
    [CommandMethod(PluginInfo.ComandoNumeracao)]
    public static void Numeracao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela da numeração precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaDeNumeracao.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela da numeração.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a numeração: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// [Tag/Listar]: Listar só mostra o que está gravado; Tag pede a composição em uma linha, "trafo|inversor|string|separador"
    /// (trafo "-" tira o pedaço do trafo; ex. "T|I|S|." ou "-||S|"), grava e
    /// mostra o exemplo.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoNumeracaoAutomatico)]
    public static void NumeracaoAutomatica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var pergunta = new PromptKeywordOptions(Tr.T("\nNumeração [Tag/Listar]: ")) { AllowNone = false };
            foreach (var palavra in new[] { "Tag", "Listar" }) pergunta.Keywords.Add(palavra);

            var resposta = editor.GetKeywords(pergunta);
            if (resposta.Status != PromptStatus.OK) return;

            var database = documento.Database;

            switch (resposta.StringResult)
            {
                case "Tag":
                    var texto = editor.GetString(new PromptStringOptions(Tr.T("\nComposição (trafo|inversor|string|separador): ")) { AllowSpaces = true });
                    if (texto.Status != PromptStatus.OK) return;

                    var partes = texto.StringResult.Split('|');
                    if (partes.Length != 4)
                    {
                        editor.WriteMessage(Tr.T("\nNUMERACAO A composição tem quatro partes separadas por |.\n"));
                        return;
                    }

                    var esquema = new TagScheme(partes[0] != "-", partes[0] == "-" ? string.Empty : partes[0], partes[1], partes[2], partes[3]);
                    if (esquema.Problem() is { } problema)
                    {
                        editor.WriteMessage(Tr.F("\nNUMERACAO Não salvei: {0}.\n", problema));
                        return;
                    }

                    NumeracaoStore.GravarEsquema(database, esquema);
                    break;
            }

            var (gravado, problemaDoRegistro) = NumeracaoStore.Esquema(database);
            editor.WriteMessage(Tr.F("\nNUMERACAO tag {0}\n", PainelDeNumeracao.Exemplo(gravado)));
            if (problemaDoRegistro is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problemaDoRegistro));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_NUMERACAO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na numeração: {0}\n", erro.Message));
        }
    }
}

/// <summary>A aba Numeração numa janela solta, uma por desenho (enquanto a janela da configuração elétrica não a hospeda).</summary>
internal sealed class JanelaDeNumeracao : Window
{
    private static readonly Dictionary<Document, JanelaDeNumeracao> Abertas = [];

    private JanelaDeNumeracao(Document documento)
    {
        Title = Tr.T("Numeração das strings — Clivus Solar");
        Width = 760;
        Height = 560;
        MinWidth = 600;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = PainelDeNumeracao.Criar(documento);
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

        var janela = new JanelaDeNumeracao(documento);
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
