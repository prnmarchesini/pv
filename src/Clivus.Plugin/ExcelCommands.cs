using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ExcelCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Passo 8.12: o Excel das quantidades. Lê do desenho mesas, módulos, kWp e
/// pilares (enterrado, acima e total) pelo XData, mais a última
/// quantificação de cada análise, e grava um .xlsx (<see cref="QuantityReport"/>).
/// Desde 10/10/2026 é o menu Exportar: antes do arquivo, a janela de escolher
/// o que vai (layout, resumo elétrico, cabos com totalização).
/// </summary>
public static class ExcelCommands
{
    /// <summary>CLIVUS_EXCEL: pergunta o arquivo e grava.</summary>
    [CommandMethod(PluginInfo.ComandoExportarExcel)]
    public static void Exportar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var escolha = new JanelaDeExportar();
            if (AcadApp.ShowModalWindow(escolha) != true || escolha.Abas == ExportSheets.None)
            {
                editor.WriteMessage(Tr.T("\nEXCEL Cancelado.\n"));
                return;
            }

            var nome = Path.GetFileNameWithoutExtension(documento.Name);
            if (string.IsNullOrWhiteSpace(nome)) nome = "usina";

            // Sempre a janela do Windows (regra de 02/10/2026).
            var caminho = DialogoDeArquivo.Salvar(
                Tr.T("Exportar para o Excel"), Tr.T("Pasta de trabalho do Excel (*.xlsx)|*.xlsx"), nome + " - quantidades.xlsx",
                documento.IsNamedDrawing ? Path.GetDirectoryName(documento.Name) : null);

            if (caminho is null)
            {
                editor.WriteMessage(Tr.T("\nEXCEL Cancelado.\n"));
                return;
            }

            Gravar(editor, documento, caminho, escolha.Abas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar para o Excel.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar para o Excel: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_EXCEL_AUTO: o caminho vem da linha de comando. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoExportarExcelAutomatico)]
    public static void ExportarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resposta = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo XLSX: ")) { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return;

            // O nível 2 confere as quatro abas do layout: o _AUTO continua o Excel de antes.
            Gravar(editor, documento, resposta.StringResult.Trim(), ExportSheets.Layout);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_EXCEL_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar para o Excel: {0}\n", erro.Message));
        }
    }

    internal static void Gravar(Editor editor, Document documento, string caminho, ExportSheets abas)
    {
        var database = documento.Database;
        if (!caminho.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) caminho += ".xlsx";

        var (mesas, modulos, kwp, pilares) = Ler(database);

        var quantificacoes = Enum.GetValues<IndependentKind>()
            .Select(t => QuantificacaoGravada.Ler(database, t))
            .OfType<AnalysisTally>()
            .ToList();

        File.WriteAllBytes(caminho, QuantityReport.Build(mesas, modulos, kwp, pilares, quantificacoes, abas, Extras(documento, abas)).ToBytes());

        editor.WriteMessage(Tr.F(
            "\nEXCEL {0} mesa(s), {1} módulo(s), {2} pilar(es), {3} análise(s) quantificada(s) em {4}\n",
            mesas, modulos, pilares.Count, quantificacoes.Count, caminho));

        if (quantificacoes.Count == 0)
            editor.WriteMessage(Tr.T("  Nenhuma análise quantificada ainda: use o Quantificar de cada análise para elas entrarem.\n"));
    }

    /// <summary>As abas a mais: o resumo elétrico e, dos cabos, uma por rota que tem cabo (recontada antes, regra 7), o resumo e a lista de material.</summary>
    private static List<CableTable> Extras(Document documento, ExportSheets abas)
    {
        var tabelas = new List<CableTable>();

        if (abas.HasFlag(ExportSheets.ElectricalSummary))
        {
            var r = ResumoEletricoCommands.Ler(documento).Resumo;
            tabelas.Add(new CableTable(Tr.T("Resumo elétrico"),
                [Tr.T("Item"), Tr.T("Detalhe"), Tr.T("Strings"), Tr.T("Módulos"), "kWp", Tr.T("Observação")],
                r.Rows().Select(l => (IReadOnlyList<object?>)[new string(' ', l.Level * 4) + l.Name, l.Detail, (double)l.Strings, (double)l.Modules, Math.Round(l.PowerKwp, 3), l.Note]).ToList(),
                [], r.Pending()));
        }

        if (abas.HasFlag(ExportSheets.Cables))
        {
            var lances = RotaDeCabosStore.Lances(documento.Database);
            foreach (var rota in CableRoutes.All.Where(r => lances.Any(l => l.Lance.Route == r)))
                tabelas.AddRange(RotaDeCabosTabelas.Montar(documento, rota, out _));

            var medidos = RotaDeCabosTabelas.Medidos(documento.Database);
            if (medidos.Count > 0)
            {
                // Um resumo por tipo de cabo, um circuito por linha (10/10/2026), e a lista de material.
                var leitura = LeituraDaRota.Ler(documento.Database);
                for (var i = 0; i < RotaDeCabosTabelas.TiposDeCabo.Length; i++)
                    if (RotaDeCabosTabelas.Resumo(documento.Database, leitura, i) is { Circuitos.Count: > 0 } resumo) tabelas.Add(resumo.Tabela);
                var material = CableReport.Material(medidos, 0);
                if (RotaDeCabosTabelas.ProblemaDasVias(documento.Database) is { } ilegivel) material = material with { Notes = [.. material.Notes, ilegivel] };
                tabelas.Add(material);
            }
        }

        return tabelas;
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
