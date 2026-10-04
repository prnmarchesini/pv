using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ResumoEletricoCommands))]

namespace Clivus.Plugin;

/// <summary>CLIVUS_ELETRICA_RESUMO (etapa 16): o resumo do sistema pela cadeia de vínculo. Ponto de encontro: preenchido na etapa 16.</summary>
public static class ResumoEletricoCommands
{
    [CommandMethod(PluginInfo.ComandoEletricaResumo)]
    public static void Resumo()
    {
        AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nCLIVUS_ELETRICA_RESUMO ainda vazio.\n");
    }
}
