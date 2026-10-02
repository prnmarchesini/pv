using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.JanelasDeAnaliseCommands))]

namespace UFV.Plugin;

/// <summary>UFV_ANALISES e UFV_TAGS: as janelas com abas (Renan, 02/10/2026).</summary>
public static class JanelasDeAnaliseCommands
{
    [CommandMethod(PluginInfo.ComandoAnalises)]
    public static void Analises() => Abrir("análises", AbrirAnalises);

    [CommandMethod(PluginInfo.ComandoTags)]
    public static void Tags() => Abrir("tags", AbrirTags);

    private static void Abrir(string nome, Action<Document> abrir)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!UfvExtension.TemInterface())
        {
            documento.Editor.WriteMessage($"\nA janela de {nome} precisa da interface do Civil 3D.\n");
            return;
        }

        try
        {
            abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha na janela de {nome}.", erro);
            documento.Editor.WriteMessage($"\nNão consegui abrir a janela de {nome}: {erro.Message}\n");
        }
    }

    /// <summary>O desenho atrás da janela atualiza depois de cada operação.</summary>
    private static Action Atualizar(Document documento) => () =>
    {
        documento.Editor.Regen();
        AcadApp.UpdateScreen();
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbrirAnalises(Document documento) =>
        AcadApp.ShowModalWindow(new JanelaDeAnalises(documento.Database, documento.Editor, Atualizar(documento)));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbrirTags(Document documento) =>
        AcadApp.ShowModalWindow(new JanelaDeTags(documento.Database, documento.Editor, Atualizar(documento)));
}
