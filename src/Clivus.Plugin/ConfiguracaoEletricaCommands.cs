using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ConfiguracaoEletricaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ELETRICA (etapas 12 a 15): a janela da configuração elétrica, com
/// as abas Subestação, Transformador, Inversor e Numeração (esta vem de
/// <see cref="PainelDeNumeracao"/>). Ponto de encontro: preenchido nas etapas 12 a 14.
/// </summary>
public static class ConfiguracaoEletricaCommands
{
    [CommandMethod(PluginInfo.ComandoEletrica)]
    public static void Eletrica()
    {
        AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nCLIVUS_ELETRICA ainda vazio.\n");
    }
}
