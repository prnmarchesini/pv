using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ConfiguracaoEletricaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_ELETRICA (etapas 12 a 15): a janela da configuração elétrica, com
/// as abas Subestação, Transformador, Inversor e Numeração (esta vem de
/// <see cref="PainelDeNumeracao"/>).
/// </summary>
public static class ConfiguracaoEletricaCommands
{
    [CommandMethod(PluginInfo.ComandoEletrica)]
    public static void Eletrica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela da configuração elétrica precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaEletrica.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a configuração elétrica.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a configuração elétrica: {0}\n", erro.Message));
        }
    }
}
