using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AreaListCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Lista as áreas do desenho.
///
/// Com a interface do Civil 3D (o botão Áreas da Implantação, etapa 41), abre
/// a janela das áreas (<see cref="JanelaDeAreas"/>): o resumo de cada uma, a
/// hachura, o renomear e o Excel por área. Sem interface (Core Console, o
/// nível 2), lista na linha de comando o que o registro central diz e o que
/// as próprias entidades dizem, lado a lado. Os dois podem divergir — o
/// registro não viaja na cópia entre desenhos, e o XData viaja — e ver a
/// divergência é o que permite saber que falta reindexar; a janela avisa o
/// mesmo.
/// </summary>
public static class AreaListCommands
{
    /// <summary>CLIVUS_AREAS: a janela das áreas; sem interface, a lista das registradas e das encontradas no desenho.</summary>
    [CommandMethod(PluginInfo.ComandoAreas)]
    public static void Areas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (ClivusExtension.TemInterface())
        {
            try
            {
                AbrirJanela(documento);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao abrir a janela das áreas.", erro);
                editor.WriteMessage(Tr.F("\nNão consegui abrir a janela das áreas: {0}\n", erro.Message));
            }

            return;
        }

        try
        {
            var registradas = AreaStore.Load(documento.Database);
            var noDesenho = Varrer(documento.Database);

            editor.WriteMessage(Tr.F("\nÁreas registradas: {0}\n", registradas.Count));
            foreach (var area in registradas)
            {
                editor.WriteMessage(
                    $"  REGISTRADA {area.Identity.Id:D} {area.Identity.DisplayName} handle={area.Handle}\n");
            }

            editor.WriteMessage(Tr.F("Áreas no desenho: {0}\n", noDesenho.Count));
            foreach (var area in noDesenho)
            {
                editor.WriteMessage(
                    $"  NODESENHO {area.Identity.Id:D} {area.Identity.DisplayName} handle={area.Handle}\n");
            }

            var faltando = noDesenho.Count(a => registradas.All(r => r.Identity.Id != a.Identity.Id));
            if (faltando > 0)
            {
                editor.WriteMessage(
                    Tr.F("  {0} área(s) do desenho não estão no registro. Use {1}.\n", faltando, PluginInfo.ComandoReindexar));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar as áreas.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui listar as áreas: {0}\n", erro.Message));
        }
    }

    /// <summary>A janela é WPF: só com interface, por um método que não é embutido no comando.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AbrirJanela(Autodesk.AutoCAD.ApplicationServices.Document documento) => JanelaDeAreas.Abrir(documento);

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
