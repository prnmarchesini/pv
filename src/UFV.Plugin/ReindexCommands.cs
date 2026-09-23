using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.ReindexCommands))]

namespace UFV.Plugin;

/// <summary>
/// Reconstrói o registro central a partir do que está gravado nas entidades.
///
/// É necessário porque as duas coisas viajam de maneiras diferentes: o XData
/// vai junto quando a área é copiada para outro desenho; o dicionário do
/// desenho, não. No arquivo de destino a área existe, com nome e identidade
/// intactos, e o plugin não sabe dela até varrer.
///
/// Uma ressalva que o plano de requisitos faz questão de registrar: o que
/// viaja é a área, não o terreno. As cotas que a área carrega vieram da
/// superfície do desenho de origem, e no desenho novo elas podem estar
/// mentindo. Por isso o reindexar conta o que achou, mas não promete que o
/// resultado calculado continua valendo — isso é o carimbo, e ele não viaja.
/// </summary>
public static class ReindexCommands
{
    private static string Capitalize(string problema) =>
        problema.Length == 0 ? problema : char.ToUpperInvariant(problema[0]) + problema[1..];

    /// <summary>UFV_REINDEXAR: acha as áreas pelo XData e refaz o registro.</summary>
    [CommandMethod(PluginInfo.ComandoReindexar)]
    public static void Reindexar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var registro = AreaStore.Ler(documento.Database);
            var antes = registro.Areas;

            if (registro.Problema is not null)
            {
                // Dizer o que estava quebrado importa: sem isto o comando
                // anunciaria "passaram a ser reconhecidas" para áreas que
                // já estavam registradas e cujo registro se perdeu.
                editor.WriteMessage($"\n  {Capitalize(registro.Problema)}.\n");
            }
            var noDesenho = AreaListCommands.Varrer(documento.Database);

            var novas = noDesenho.Where(a => antes.All(r => r.Identity.Id != a.Identity.Id)).ToList();
            var sumidas = antes.Where(r => noDesenho.All(a => a.Identity.Id != r.Identity.Id)).ToList();

            // O desenho manda. Uma área que está no registro e não está mais
            // no desenho foi apagada, e insistir nela deixaria a lista
            // mentindo.
            AreaStore.Save(documento.Database, noDesenho);

            editor.WriteMessage($"\nREINDEXADO {noDesenho.Count} área(s) no desenho.\n");

            if (novas.Count > 0)
            {
                editor.WriteMessage($"  {novas.Count} passaram a ser reconhecidas:\n");
                foreach (var area in novas)
                    editor.WriteMessage($"    {area.Identity.Describe()}\n");

                // Quem chegou por cópia traz cota do terreno de outro desenho.
                editor.WriteMessage(
                    "  As cotas destas áreas vieram do desenho de origem. Se a topografia\n"
                    + "  daqui for outra, elas precisam ser traçadas de novo.\n");
            }

            if (sumidas.Count > 0)
            {
                editor.WriteMessage($"  {sumidas.Count} não estão mais no desenho e saíram do registro:\n");
                foreach (var area in sumidas)
                    editor.WriteMessage($"    {area.Identity.Describe()}\n");
            }

            if (novas.Count == 0 && sumidas.Count == 0)
            {
                editor.WriteMessage("  O registro já estava em dia.\n");
            }

            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao reindexar as áreas.", erro);
            editor.WriteMessage($"\nNão consegui reindexar: {erro.Message}\n");
        }
    }
}
