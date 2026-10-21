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
    public static void Flechas() => AbrirJanela(Tr.T("Flechas"), JanelaDeFlechas.Abrir, PluginInfo.ComandoDrenagemFlechasGerar);

    /// <summary>CLIVUS_DRN_LINHA: a linha de análise e o perfil (27).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemLinha)]
    public static void Linha() => LinhaCommands.Executar();

    /// <summary>CLIVUS_DRN_CANALETA: a janela da canaleta (28).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemCanaleta)]
    public static void Canaleta() => EmConstrucao(Tr.T("Canaleta"));

    /// <summary>CLIVUS_DRN_BACIAS: a janela das bacias (30).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemBacias)]
    public static void Bacias() => EmConstrucao(Tr.T("Bacias"));

    /// <summary>CLIVUS_DRN_MEMORIA: a janela do cálculo (31).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoria)]
    public static void Memoria() => EmConstrucao(Tr.T("Cálculo"));

    /// <summary>
    /// Abre a janela do botão; sem interface (Core Console), diz qual comando
    /// faz o mesmo pela linha de comando.
    /// </summary>
    private static void AbrirJanela(string nome, Action<Autodesk.AutoCAD.ApplicationServices.Document> abrir, string comandoSemJanela)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.F("\n{0}: a janela precisa da interface do Civil 3D; use {1}.\n", nome, comandoSemJanela));
            return;
        }

        try
        {
            abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha ao abrir a janela da drenagem ({nome}).", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir {0}: {1}\n", nome, erro.Message));
        }
    }

    private static void EmConstrucao(string nome) =>
        AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(Tr.F("\n{0}: em construção (drenagem, plano/drenagem).\n", nome));
}
