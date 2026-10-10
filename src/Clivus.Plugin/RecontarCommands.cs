using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RecontarCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_RECONTAR (7.6): conta a usina como ela ESTÁ no desenho (mesas,
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

            editor.WriteMessage(Tr.F("\nRECONTAR {0}\n", linhas[0]));
            foreach (var linha in linhas.Skip(1)) editor.WriteMessage($"  {linha}\n");

            editor.WriteMessage($"  RECONTAR_TOTAIS mesas={censo.Tables} modulos={censo.Modules} pilares={censo.Pillars} orfas={censo.Orphans}\n");

            if (removidas.Items.Count > 0)
            {
                editor.WriteMessage(Tr.F("  {0} removida(s) desde a última recontagem: {1}. Registro limpo.\n", removidas.Items.Count, string.Join(", ", removidas.Items.Select(r => r.Label))));
                RemovalStore.Save(documento.Database, []);
            }
            else if (removidas.Problem is not null)
            {
                // Registro ilegível e vazio: descartado, senão o aviso repete para sempre.
                editor.WriteMessage(Tr.F("  ATENÇÃO: {0}; registro descartado.\n", removidas.Problem));
                RemovalStore.Save(documento.Database, []);
            }
            else
            {
                editor.WriteMessage(Tr.T("  nenhuma removida desde a última recontagem.\n"));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recontar.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui recontar: {0}\n", erro.Message));
        }
    }

    /// <summary>A contagem, pelo XData das entidades.</summary>
    internal static LayoutCensus Contar(Database database, double potenciaDoModuloWatts)
    {
        var mesas = new List<CountedTable>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var partes in LayoutScan.Tables(transacao, database).Values)
        {
            mesas.Add(Contar(transacao, partes));
        }

        // A potência trocada pela área (item 14 de 10/10/2026) vale para todas as mesas.
        return LayoutCensus.Count(mesas, potenciaDoModuloWatts, FonteDoModulo.Simulada(database)?.Watts);
    }

    /// <summary>Uma mesa contada com os comprimentos dos pilares (lidos do XData de cada um).</summary>
    internal static CountedTable Contar(Transaction transacao, TableParts partes)
    {
        var comprimentos = new List<double>();
        var semComprimento = 0;

        foreach (var id in partes.Pillars)
        {
            var pilar = LayoutXData.LoadPillar((Entity)transacao.GetObject(id, OpenMode.ForRead));

            if (pilar?.Length is { } p1) comprimentos.Add(p1);
            else semComprimento++;
        }

        return new CountedTable(partes.Identity, partes.Contours.Count, partes.Modules.Count, comprimentos, semComprimento);
    }
}
