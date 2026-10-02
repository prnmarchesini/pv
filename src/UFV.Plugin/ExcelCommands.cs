using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.ExcelCommands))]

namespace UFV.Plugin;

/// <summary>
/// Passo 8.12: o Excel das quantidades. Lê do desenho mesas, módulos, kWp e
/// pilares (enterrado, acima e total) pelo XData, mais a última
/// quantificação de cada análise, e grava um .xlsx (<see cref="QuantityReport"/>).
/// </summary>
public static class ExcelCommands
{
    /// <summary>UFV_EXCEL: pergunta o arquivo e grava.</summary>
    [CommandMethod(PluginInfo.ComandoExportarExcel)]
    public static void Exportar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = Path.GetFileNameWithoutExtension(documento.Name);
            if (string.IsNullOrWhiteSpace(nome)) nome = "usina";

            // Sempre a janela do Windows (regra de 02/10/2026).
            var caminho = DialogoDeArquivo.Salvar(
                "Exportar para o Excel", "Pasta de trabalho do Excel (*.xlsx)|*.xlsx", nome + " - quantidades.xlsx",
                documento.IsNamedDrawing ? Path.GetDirectoryName(documento.Name) : null);

            if (caminho is null)
            {
                editor.WriteMessage("\nEXCEL Cancelado.\n");
                return;
            }

            Gravar(editor, documento.Database, caminho);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar para o Excel.", erro);
            editor.WriteMessage($"\nNão consegui exportar para o Excel: {erro.Message}\n");
        }
    }

    /// <summary>UFV_EXCEL_AUTO: o caminho vem da linha de comando. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoExportarExcelAutomatico)]
    public static void ExportarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resposta = editor.GetString(new PromptStringOptions("\nArquivo XLSX: ") { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return;

            Gravar(editor, documento.Database, resposta.StringResult.Trim());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no UFV_EXCEL_AUTO.", erro);
            editor.WriteMessage($"\nNão consegui exportar para o Excel: {erro.Message}\n");
        }
    }

    internal static void Gravar(Editor editor, Database database, string caminho)
    {
        if (!caminho.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) caminho += ".xlsx";

        var (mesas, modulos, kwp, pilares) = Ler(database);

        var quantificacoes = Enum.GetValues<IndependentKind>()
            .Select(t => QuantificacaoGravada.Ler(database, t))
            .OfType<AnalysisTally>()
            .ToList();

        File.WriteAllBytes(caminho, QuantityReport.Build(mesas, modulos, kwp, pilares, quantificacoes).ToBytes());

        editor.WriteMessage(
            $"\nEXCEL {mesas} mesa(s), {modulos} módulo(s), {pilares.Count} pilar(es), "
            + $"{quantificacoes.Count} análise(s) quantificada(s) em {caminho}\n");

        if (quantificacoes.Count == 0)
            editor.WriteMessage("  Nenhuma análise quantificada ainda: use o Quantificar de cada análise para elas entrarem.\n");
    }

    private static (int Mesas, int Modulos, double Kwp, List<QuantityPillar> Pilares) Ler(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var mesas = 0;
        var modulos = 0;
        var watts = 0.0;
        var pilares = new List<QuantityPillar>();

        foreach (var (_, partes) in LayoutScan.Tables(transacao, database))
        {
            if (partes.Identity is not { } mesa) continue;

            mesas++;
            modulos += partes.Modules.Count;
            watts += partes.Modules.Count * (mesa.ModulePowerWatts ?? 0);

            foreach (var id in partes.Pillars)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadPillar(e) is { } p)
                    pilares.Add(new QuantityPillar(mesa.Label, p.Number, p.Embedment, p.FreeHeight, p.Length));
            }
        }

        transacao.Commit();
        return (mesas, modulos, watts / 1000, pilares);
    }
}
