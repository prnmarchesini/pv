using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.StringCommands))]

namespace Clivus.Plugin;

/// <summary>
/// As strings (elétrica, etapa 11): CLIVUS_STRING abre a janela;
/// CLIVUS_STRING_TIPO_AUTO mexe na biblioteca pela linha de comando (nível 2).
/// </summary>
public static class StringCommands
{
    [CommandMethod(PluginInfo.ComandoString)]
    public static void Strings()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela das strings precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaDeStrings.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela de strings.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir as strings: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// [Adicionar/Listar/Renomear/Apagar]: Adicionar cria um tipo sem mesas;
    /// Renomear e Apagar pedem o nome do tipo (e o novo nome).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringTipoAutomatico)]
    public static void TipoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var pergunta = new PromptKeywordOptions(Tr.T("\nBiblioteca de strings [Adicionar/Listar/Renomear/Apagar]: ")) { AllowNone = false };
            foreach (var palavra in new[] { "Adicionar", "Listar", "Renomear", "Apagar" }) pergunta.Keywords.Add(palavra);

            var resposta = editor.GetKeywords(pergunta);
            if (resposta.Status != PromptStatus.OK) return;

            var database = documento.Database;

            switch (resposta.StringResult)
            {
                case "Adicionar":
                    StringType? novo = null;
                    var problema = StringTypeStore.Mudar(database, b => novo = b.Add(StringArrangement.Empty));
                    editor.WriteMessage(Tr.F("\nSTRING {0} criado.\n", novo!.Name));
                    if (problema is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problema));
                    break;

                case "Renomear":
                    if (Achar(editor, database) is not { } tipo) return;
                    var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome novo: ")) { AllowSpaces = true });
                    if (nome.Status != PromptStatus.OK) return;
                    string? porque = null;
                    StringTypeStore.Mudar(database, b => porque = b.Rename(tipo.Id, nome.StringResult));
                    editor.WriteMessage(porque is null
                        ? Tr.F("\nSTRING {0} renomeado para {1}.\n", tipo.Name, nome.StringResult.Trim())
                        : Tr.F("\nSTRING Não renomeei: {0}.\n", porque));
                    break;

                case "Apagar":
                    if (Achar(editor, database) is not { } apagado) return;
                    StringTypeStore.Mudar(database, b => b.Remove(apagado.Id));
                    editor.WriteMessage(Tr.F("\nSTRING {0} apagado da biblioteca.\n", apagado.Name));
                    break;
            }

            var lido = StringTypeStore.Ler(database);
            editor.WriteMessage(Tr.F("STRING {0} tipo(s): {1}\n", lido.Items.Count, string.Join("; ", lido.Items.Select(JanelaDeStrings.Descrever))));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_TIPO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na biblioteca de strings: {0}\n", erro.Message));
        }
    }

    private static StringType? Achar(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome do tipo: ")) { AllowSpaces = true });
        if (nome.Status != PromptStatus.OK) return null;

        var tipo = StringTypeStore.Ler(database).Items.FirstOrDefault(t => string.Equals(t.Name, nome.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (tipo is null) editor.WriteMessage(Tr.F("\nSTRING Não há tipo chamado \"{0}\".\n", nome.StringResult.Trim()));
        return tipo;
    }
}
