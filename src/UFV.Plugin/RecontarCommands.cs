using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.RecontarCommands))]

namespace UFV.Plugin;

/// <summary>
/// UFV_RECONTAR (7.6): conta a usina como ela ESTÁ no desenho (mesas,
/// módulos, kWp, pilares e comprimentos), pelo XData, e consome as
/// remoções que o vigia registrou desde a última recontagem (lista e
/// limpa). O buraco na numeração fica: renumerar é o 7.10.
/// </summary>
public static class RecontarCommands
{
    [CommandMethod(PluginInfo.ComandoRecontar)]
    public static void Recontar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var perfil = FileiraCommands.PerfilDaMesa(editor);
            var censo = Contar(documento.Database, perfil.Layout.Module.PowerWatts);
            var removidas = RemovalStore.Ler(documento.Database);
            var linhas = censo.Lines();

            editor.WriteMessage($"\nRECONTAR {linhas[0]}\n");
            foreach (var linha in linhas.Skip(1)) editor.WriteMessage($"  {linha}\n");

            editor.WriteMessage($"  RECONTAR_TOTAIS mesas={censo.Tables} modulos={censo.Modules} pilares={censo.Pillars} orfas={censo.Orphans}\n");

            if (removidas.Items.Count > 0)
            {
                editor.WriteMessage($"  {removidas.Items.Count} removida(s) desde a última recontagem: {string.Join(", ", removidas.Items.Select(r => r.Label))}. Registro limpo.\n");
                RemovalStore.Save(documento.Database, []);
            }
            else if (removidas.Problem is not null)
            {
                // Registro ilegível e vazio: descartado, senão o aviso repete para sempre.
                editor.WriteMessage($"  ATENÇÃO: {removidas.Problem}; registro descartado.\n");
                RemovalStore.Save(documento.Database, []);
            }
            else
            {
                editor.WriteMessage("  nenhuma removida desde a última recontagem.\n");
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recontar.", erro);
            editor.WriteMessage($"\nNão consegui recontar: {erro.Message}\n");
        }
    }

    /// <summary>A contagem, pelo XData das entidades.</summary>
    internal static LayoutCensus Contar(Database database, double potenciaDoModuloWatts)
    {
        var mesas = new List<CountedTable>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var partes in LayoutScan.Tables(transacao, database).Values)
        {
            var comprimentos = new List<double>();
            var semComprimento = 0;

            foreach (var id in partes.Pillars)
            {
                var pilar = LayoutXData.LoadPillar((Entity)transacao.GetObject(id, OpenMode.ForRead));

                if (pilar?.Length is { } p1) comprimentos.Add(p1);
                else semComprimento++;
            }

            mesas.Add(new CountedTable(partes.Identity, partes.Contours.Count, partes.Modules.Count, comprimentos, semComprimento));
        }

        return LayoutCensus.Count(mesas, potenciaDoModuloWatts);
    }
}
