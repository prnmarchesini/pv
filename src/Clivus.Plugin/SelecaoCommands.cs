using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.SelecaoCommands))]

namespace Clivus.Plugin;

/// <summary>O resumo de uma seleção: a contagem das mesas tocadas (inteiras, mesmo que só um pilar esteja selecionado).</summary>
internal sealed record SelectionSummary(LayoutCensus Census)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    public bool IsEmpty => Census.Tables == 0;

    /// <summary>"3 mesa(s), 84 módulo(s), 60,5 kWp", e os avisos de duplicada e de potência do perfil quando há.</summary>
    public string Describe() =>
        Census.Lines()[0]
        + (Census.Duplicated > 0 ? Tr.F(" ({0} com contorno duplicado)", Census.Duplicated) : string.Empty)
        + (Census.TablesWithoutPower > 0 ? Tr.F(" ({0} com a potência do perfil atual)", Census.TablesWithoutPower) : string.Empty);
}

/// <summary>
/// O kWp da seleção (7.8): a conta, num comando que imprime, para a caixa
/// flutuante (<see cref="AutoSelecao"/>) e para o nível 2 dividirem a mesma
/// fonte. Uma mesa entra inteira quando qualquer peça dela está na seleção.
/// </summary>
public static class SelecaoCommands
{
    /// <summary>CLIVUS_KWP_SELECAO: o resumo da seleção prévia (ou de uma pedida).</summary>
    [CommandMethod(PluginInfo.ComandoKwpSelecao, CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.NoUndoMarker)]
    public static void KwpDaSelecao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var selecao = editor.SelectImplied();

            if (selecao.Status != PromptStatus.OK)
            {
                selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nSelecione as mesas: ") });
                if (selecao.Status != PromptStatus.OK) return;
            }

            var resumo = Resumir(documento, MesasTocadas(documento, selecao.Value.GetObjectIds()));

            editor.WriteMessage(resumo.IsEmpty
                ? Tr.T("\nSELECAO nenhuma mesa do plugin na seleção.\n")
                : Tr.F("\nSELECAO {0}\n", resumo.Describe()));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao resumir a seleção.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui resumir a seleção: {0}\n", erro.Message));
        }
    }

    /// <summary>Os GUIDs das mesas com alguma peça entre os ids dados. Só abre o que é da classe de peça nossa.</summary>
    internal static HashSet<Guid> MesasTocadas(Document documento, IEnumerable<ObjectId> ids)
    {
        var tocadas = new HashSet<Guid>();

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        foreach (var id in ids)
        {
            if (id.IsNull || id.IsErased || !LayoutScan.ENossaClasse(id)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            // Peça sem mesa válida (GUID vazio) não entra: um grupo com ela é
            // gravado mas não volta a ser lido (04/10/2026, registro de grupos).
            if (LayoutScan.TableOf(entidade) is { } guid && guid != Guid.Empty) tocadas.Add(guid);
        }

        return tocadas;
    }

    /// <summary>O resumo das mesas dadas, inteiras. Uma varredura do desenho; o perfil só é lido do disco se alguma mesa não tem potência gravada.</summary>
    internal static SelectionSummary Resumir(Document documento, IReadOnlySet<Guid> tocadas)
    {
        if (tocadas.Count == 0) return new SelectionSummary(LayoutCensus.Count([], 1));

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);

        var contadas = tocadas
            .Where(mesas.ContainsKey)
            .Select(g => new CountedTable(mesas[g].Identity, mesas[g].Contours.Count, mesas[g].Modules.Count, [], 0))
            .ToList();

        var reserva = contadas.Any(t => t.Identity is { ModulePowerWatts: null })
            ? FileiraCommands.PerfilDaMesa(documento.Editor, silencioso: true).Layout.Module.PowerWatts
            : contadas.Select(t => t.Identity?.ModulePowerWatts).FirstOrDefault(p => p is > 0) ?? 1;

        return new SelectionSummary(LayoutCensus.Count(contadas, reserva));
    }
}
