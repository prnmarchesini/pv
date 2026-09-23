using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.AreaListCommands))]

namespace UFV.Plugin;

/// <summary>
/// Lista as áreas do desenho.
///
/// Mostra o que o registro central diz e o que as próprias entidades dizem,
/// lado a lado. Os dois podem divergir — o registro não viaja na cópia entre
/// desenhos, e o XData viaja — e ver a divergência é o que permite saber que
/// falta reindexar.
/// </summary>
public static class AreaListCommands
{
    /// <summary>UFV_AREAS: lista as áreas registradas e as encontradas no desenho.</summary>
    [CommandMethod(PluginInfo.ComandoAreas)]
    public static void Areas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var registradas = AreaStore.Load(documento.Database);
            var noDesenho = Varrer(documento.Database);

            editor.WriteMessage($"\nÁreas registradas: {registradas.Count}\n");
            foreach (var area in registradas)
            {
                editor.WriteMessage(
                    $"  REGISTRADA {area.Identity.Id:D} {area.Identity.DisplayName} handle={area.Handle}\n");
            }

            editor.WriteMessage($"Áreas no desenho: {noDesenho.Count}\n");
            foreach (var area in noDesenho)
            {
                editor.WriteMessage(
                    $"  NODESENHO {area.Identity.Id:D} {area.Identity.DisplayName} handle={area.Handle}\n");
            }

            var faltando = noDesenho.Count(a => registradas.All(r => r.Identity.Id != a.Identity.Id));
            if (faltando > 0)
            {
                editor.WriteMessage(
                    $"  {faltando} área(s) do desenho não estão no registro. "
                    + $"Use {PluginInfo.ComandoReindexar}.\n");
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar as áreas.", erro);
            editor.WriteMessage($"\nNão consegui listar as áreas: {erro.Message}\n");
        }
    }

    /// <summary>
    /// As áreas encontradas varrendo o desenho pelo XData.
    ///
    /// É a fonte de verdade: o XData viaja grudado na entidade, inclusive para
    /// outro desenho, onde o registro central não chega.
    /// </summary>
    internal static IReadOnlyList<AreaRecord> Varrer(Database database)
    {
        var achadas = new List<AreaRecord>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);

        // Só o espaço do modelo: é onde a área é desenhada, e varrer o banco
        // inteiro abriria entidade de bloco e de layout sem necessidade.
        var espaco = (BlockTableRecord)transacao.GetObject(
            tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var identidade = AreaXData.Load(entidade);
            if (identidade is null) continue;

            achadas.Add(new AreaRecord(identidade, entidade.Handle.ToString()));
        }

        transacao.Commit();
        return achadas;
    }
}
