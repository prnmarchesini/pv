using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.DrenagemCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Os botões do painel Drenagem (etapa 25, plano/drenagem): Análise › Flechas
/// e Linhas, Canaleta, Memória › Bacias e Cálculo. Cada um abre a janela ou o
/// fluxo do seu passo.
/// </summary>
public static class DrenagemCommands
{
    /// <summary>CLIVUS_DRN_FLECHAS: a janela das flechas (26).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemFlechas)]
    public static void Flechas() => EmConstrucao(Tr.T("Flechas"));

    /// <summary>CLIVUS_DRN_LINHA: a linha de análise e o perfil (27).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemLinha)]
    public static void Linha() => EmConstrucao(Tr.T("Linhas"));

    /// <summary>CLIVUS_DRN_CANALETA: a janela da canaleta (28).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCanaleta)]
    public static void Canaleta() => EmConstrucao(Tr.T("Canaleta"));

    /// <summary>CLIVUS_DRN_BACIAS: a janela das bacias (30).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemBacias)]
    public static void Bacias() => EmConstrucao(Tr.T("Bacias"));

    /// <summary>CLIVUS_DRN_MEMORIA: a janela do cálculo (31).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoria)]
    public static void Memoria() => EmConstrucao(Tr.T("Cálculo"));

    private static void EmConstrucao(string nome) =>
        AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(Tr.F("\n{0}: em construção (drenagem, plano/drenagem).\n", nome));
}
