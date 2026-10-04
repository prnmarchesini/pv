using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.IdiomaCommands))]

namespace Clivus.Plugin;

/// <summary>CLIVUS_IDIOMA (etapa 10): o idioma da tela pela linha de comando.</summary>
public static class IdiomaCommands
{
    [CommandMethod(PluginInfo.ComandoIdioma)]
    public static void Idioma()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            // As palavras-chave são as mesmas em todo idioma (o código curto),
            // para os scripts funcionarem igual.
            var pergunta = new PromptKeywordOptions(Tr.F("\nIdioma do Clivus Solar (agora {0}) [auto/pt/en/es]: ", Tr.Code(Tr.Current)))
            {
                AllowNone = false,
            };
            foreach (var escolha in UserPreferences.LanguageChoices) pergunta.Keywords.Add(escolha);

            var resposta = editor.GetKeywords(pergunta);
            if (resposta.Status != PromptStatus.OK) return;

            IdiomaDoPlugin.Trocar(resposta.StringResult.ToLowerInvariant());
            editor.WriteMessage(Tr.F("\nIDIOMA {0} (escolha {1}).\n", Tr.Code(Tr.Current), resposta.StringResult.ToLowerInvariant()));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao trocar o idioma.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui trocar o idioma: {0}\n", erro.Message));
        }
    }
}
