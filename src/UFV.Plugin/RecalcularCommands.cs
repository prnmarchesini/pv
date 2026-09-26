using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.RecalcularCommands))]

namespace UFV.Plugin;

/// <summary>
/// Recalcular uma mesa (7.3) e recalcular as sujas (7.4).
///
/// Uma mesa é recalculada ONDE ESTÁ: a célula vem dos quatro cantos do
/// contorno desenhado (<see cref="TableCells"/>), o terreno é reamostrado
/// ali, os pilares e as pontas baixas são refeitos com a configuração e o
/// perfil de mesa ATUAIS, tudo dela é apagado e desenhado de novo com o
/// MESMO GUID, e a mesa nasce limpa. O alinhamento de fileira (5.4) não é
/// refeito para as vizinhas: isso é o Refazer da área.
/// </summary>
public static class RecalcularCommands
{
    /// <summary>UFV_RECALCULAR: a mesa da peça selecionada (ou clicada).</summary>
    [CommandMethod(PluginInfo.ComandoRecalcular, CommandFlags.UsePickSet)]
    public static void Recalcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var guid = MesaDaSelecao(editor, documento) ?? MesaClicada(editor, documento);
            if (guid is null) return;

            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            RecalcularMesas(editor, documento, terreno, [guid.Value], FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular a mesa.", erro);
            editor.WriteMessage($"\nNão consegui recalcular a mesa: {erro.Message}\n");
        }
    }

    /// <summary>UFV_RECALCULAR_SUJAS: só as mesas sujas, uma a uma.</summary>
    [CommandMethod(PluginInfo.ComandoRecalcularSujas)]
    public static void RecalcularSujas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var sujas = Sujas(documento);

            if (sujas.Count == 0)
            {
                editor.WriteMessage("\nRECALCULAR Nenhuma mesa suja.\n");
                return;
            }

            RecalcularMesas(editor, documento, terreno, sujas, FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular as mesas sujas.", erro);
            editor.WriteMessage($"\nNão consegui recalcular as mesas sujas: {erro.Message}\n");
        }
    }

    /// <summary>UFV_RECALCULAR_AUTO: as sujas, com a mesa de exemplo, sem perguntar. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoRecalcularAutomatico)]
    public static void RecalcularAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var sujas = Sujas(documento);

            if (sujas.Count == 0)
            {
                editor.WriteMessage("\nRECALCULAR Nenhuma mesa suja.\n");
                return;
            }

            RecalcularMesas(editor, documento, terreno, sujas, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular automaticamente.", erro);
            editor.WriteMessage($"\nNão consegui recalcular: {erro.Message}\n");
        }
    }

    private static IReadOnlyList<Guid> Sujas(Document documento)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        return LayoutScan.Tables(transacao, documento.Database).Values
            .Where(m => m.Identity is { Dirty: true })
            .OrderBy(m => m.Identity!.Label, StringComparer.Ordinal)
            .Select(m => m.Identity!.Id)
            .ToList();
    }

    internal static void RecalcularMesas(Editor editor, Document documento, ProcessedTerrain terreno, IReadOnlyList<Guid> mesas, TableProfile perfil)
    {
        var settings = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        var pilares = PillarTable.Distribute(perfil.Layout.Length, perfil.Frame.PillarSpanTarget, perfil.Frame.PillarCantilever);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        // Uma varredura só: recalcular uma mesa troca as entidades DELA, e
        // as das outras continuam válidas.
        IReadOnlyDictionary<Guid, TableParts> todas;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
            todas = LayoutScan.Tables(transacao, documento.Database);

        var feitas = 0;

        foreach (var guid in mesas)
        {
            if (RecalcularUma(editor, documento, terreno, guid, todas, perfil, geometria, settings)) feitas++;
        }

        editor.WriteMessage($"\nRECALCULAR {feitas} de {mesas.Count} mesa(s) recalculada(s) com a configuração atual.\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static bool RecalcularUma(
        Editor editor, Document documento, ProcessedTerrain terreno, Guid guid, IReadOnlyDictionary<Guid, TableParts> todas,
        TableProfile perfil, TableGeometry geometria, ProjectSettings settings)
    {
        if (!todas.TryGetValue(guid, out var mesa) || mesa.Identity is null || mesa.Contour is not { } contorno)
        {
            editor.WriteMessage($"\nRECALCULAR A mesa {guid:D} não tem contorno; não há como saber onde ela está.\n");
            return false;
        }

        if (mesa.IsDuplicated)
        {
            editor.WriteMessage(
                $"\nRECALCULAR {mesa.Identity.Label} tem {mesa.Contours.Count} contornos com a mesma identidade (mesa copiada e colada). "
                + "Apague a cópia, ou use o Refazer da área.\n");
            return false;
        }

        List<Point3> cantos;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var polilinha = (Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead);
            cantos = FileiraCommands.Vertices(polilinha, transacao).ToList();
        }

        PlacedTable celula;

        try
        {
            celula = TableCells.FromCorners(cantos, mesa.Identity!.Label, geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nRECALCULAR {mesa.Identity!.Label}: {erro.Message} Use o Refazer da área.\n");
            return false;
        }

        var fileira = RowPipeline.ProcessRow(new PlanRow(celula.Row, [celula]), geometria, perfil.TiltRadians, terreno.Mesh, settings);

        using (var transacao = documento.Database.TransactionManager.StartTransaction())
        {
            foreach (var peca in mesa.All)
            {
                var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
                entidade.Erase();
            }

            transacao.Commit();
        }

        var desenho = LayoutDrawer.Draw(documento.Database, fileira, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses, _ => guid);
        var processada = fileira.Tables[0];

        foreach (var aviso in fileira.Warnings) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");

        if (processada.Orientation.DivergenceRadians > 5 * Math.PI / 180)
        {
            editor.WriteMessage(
                $"\n  ATENÇÃO: {mesa.Identity.Label} está girada {processada.Orientation.DivergenceRadians * 180 / Math.PI:0.#}° em relação ao azimute "
                + "configurado (foi girada à mão?). A mesa foi recalculada como está.\n");
        }

        editor.WriteMessage(
            $"\nRECALCULAR {mesa.Identity!.Label} refeita onde está: {processada.Report.Describe()}; {processada.Pillars.Describe()}; "
            + $"{desenho.Pillars} pilar(es), {desenho.Modules} módulo(s)" + (desenho.Marked > 0 ? ", NÃO CABE NO TERRENO" : string.Empty) + ".\n");

        return true;
    }

    private static Guid? MesaDaSelecao(Editor editor, Document documento)
    {
        var selecao = editor.SelectImplied();
        if (selecao.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        foreach (var id in selecao.Value.GetObjectIds())
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            if (LayoutScan.TableOf(entidade) is { } guid)
            {
                editor.SetImpliedSelection([]);
                return guid;
            }
        }

        return null;
    }

    private static Guid? MesaClicada(Editor editor, Document documento)
    {
        var opcoes = new PromptEntityOptions("\nClique numa peça da mesa a recalcular: ");
        opcoes.SetRejectMessage("\nIsso não é uma peça de mesa do plugin.");
        opcoes.AddAllowedClass(typeof(Entity), false);

        var resposta = editor.GetEntity(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
        var guid = LayoutScan.TableOf(entidade);

        if (guid is null) editor.WriteMessage("\nRECALCULAR Isso não é uma peça de mesa do plugin.\n");

        return guid;
    }
}
