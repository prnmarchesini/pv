using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.JanelasDeAnaliseCommands))]

namespace Clivus.Plugin;

/// <summary>CLIVUS_ANALISES e CLIVUS_TAGS: as janelas com abas (Renan, 02/10/2026).</summary>
public static class JanelasDeAnaliseCommands
{
    [CommandMethod(PluginInfo.ComandoAnalises)]
    public static void Analises() => Abrir(Tr.N("análises"), AbrirAnalises);

    [CommandMethod(PluginInfo.ComandoTags)]
    public static void Tags() => Abrir(Tr.N("tags"), AbrirTags);

    private static void Abrir(string nome, Action<Document> abrir)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.F("\nA janela de {0} precisa da interface do Civil 3D.\n", Tr.T(nome)));
            return;
        }

        try
        {
            abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha na janela de {nome}.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a janela de {0}: {1}\n", Tr.T(nome), erro.Message));
        }
    }

    /// <summary>O desenho atrás da janela atualiza depois de cada operação.</summary>
    private static Action Atualizar(Document documento) => () =>
    {
        documento.Editor.Regen();
        AcadApp.UpdateScreen();
    };

    /// <summary>A janela de Análises aberta de cada desenho: uma só por desenho.</summary>
    private static readonly Dictionary<Document, JanelaDeAnalises> Abertas = [];

    /// <summary>
    /// A janela de Análises é solta (03/10/2026: "eu clico no botão, analiso,
    /// volto, troco algo na tela, analiso, mas hoje a tela é fixa"): o CAD
    /// segue usável com ela aberta. Clicar de novo traz a mesma para a frente;
    /// fechar o desenho fecha a janela dele.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbrirAnalises(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == System.Windows.WindowState.Minimized) aberta.WindowState = System.Windows.WindowState.Normal;
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeAnalises(documento.Database, documento.Editor, Atualizar(documento));
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbrirTags(Document documento)
    {
        var janela = new JanelaDeTags(documento.Database, documento.Editor, Atualizar(documento));
        AcadApp.ShowModalWindow(janela);

        // As tags de fileira pedem cliques no desenho: rodam depois que a janela fecha.
        if (janela.Fileiras) documento.SendStringToExecute(PluginInfo.ComandoTagFileirasInserir + " ", true, false, true);
    }
}
