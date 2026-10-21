using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.MemoriaCommands))]

namespace Clivus.Plugin;

/// <summary>Memória › Cálculo (31): a altura sugerida e o caminho do nível 2.</summary>
public static class MemoriaCommands
{
    /// <summary>CLIVUS_DRN_MEMORIA_ALTURA: as canaletas escolhidas ganham a altura (ou o raio) que a memória sugere.</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoriaAltura, CommandFlags.UsePickSet)]
    public static void Altura()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as canaletas que recebem a altura sugerida: ") },
                new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE")]));
            if (selecao.Status != PromptStatus.OK) return;

            editor.WriteMessage($"\n{MemoriaDeDrenagem.AplicarAltura(documento, terreno.Mesh, selecao.Value.GetObjectIds())}\n");
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nMEMÓRIA {erro.Message}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao aplicar a altura sugerida.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui aplicar a altura: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_DRN_MEMORIA_AUTO: o arquivo XLSX; grava o Excel e escreve cada canaleta numa linha (nível 2).</summary>
    [CommandMethod(PluginInfo.ComandoDrenagemMemoriaAutomatico)]
    public static void Automatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var resposta = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo XLSX: ")) { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return;

            editor.WriteMessage($"\n{MemoriaDeDrenagem.Exportar(documento, terreno.Mesh, resposta.StringResult.Trim())}\n");

            var inv = CultureInfo.InvariantCulture;
            var memoria = MemoriaDeDrenagem.Montar(documento.Database, terreno.Mesh);
            foreach (var c in memoria.Channels)
            {
                editor.WriteMessage(string.Format(inv,
                    "\nDRN_MEMORIA nome={0} area={1:0.##} tc={2:0.###} i={3:0.###} q={4:0.####} imin={5:0.#####} imax={6:0.#####} hmax={7:0.####} vmax={8:0.###} f={9:0.##} ysug={10:0.##} des={11} alertas={12} fim\n",
                    c.Input.Name.Replace(' ', '_'), c.Input.Basin?.Area ?? 0, c.Tc, c.Intensity, c.Flow, c.MinSlope, c.MaxSlope, c.MaxDepth, c.MaxVelocity,
                    c.Freeboard, c.SuggestedHeight, (c.DissipatorModel ?? "-").Replace(' ', '_'), c.Alerts.Count));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_DRN_MEMORIA_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui fazer a memória: {0}\n", erro.Message));
        }
    }
}
