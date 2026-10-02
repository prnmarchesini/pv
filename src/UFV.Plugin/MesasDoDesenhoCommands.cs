using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.MesasDoDesenhoCommands))]

namespace UFV.Plugin;

/// <summary>Comandos das mesas do desenho (8.5) que não precisam de janela.</summary>
public static class MesasDoDesenhoCommands
{
    /// <summary>
    /// UFV_MESAS_EXEMPLO_AUTO: a mesa de exemplo (28 módulos, 2V) e a mesma
    /// com 14 módulos, gravadas no desenho e em uso. Para o nível 2 da usina
    /// mista.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoMesasExemploAutomatico)]
    public static void Exemplo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            var longa = MesaCommands.MesaDeExemplo() with { Name = "Mesa 2V28" };
            var curta = longa with { Name = "Mesa 2V14", Layout = longa.Layout with { ModuleCount = 14 } };

            var mesas = new List<DrawingTable>();
            mesas.Add(new DrawingTable(longa, DrawingTables.NextColor(mesas), true));
            mesas.Add(new DrawingTable(curta, DrawingTables.NextColor(mesas), true));

            MesasDoDesenho.Gravar(documento.Database, mesas);
            documento.Editor.WriteMessage($"\nMESAS {mesas.Count} mesa(s) no desenho, em uso: {string.Join(", ", mesas.Select(m => m.Name))}.\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no UFV_MESAS_EXEMPLO_AUTO.", erro);
            documento.Editor.WriteMessage($"\nNão consegui gravar as mesas: {erro.Message}\n");
        }
    }
}
