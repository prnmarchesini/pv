using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RecalcularCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Recalcular uma mesa (7.3) e recalcular as sujas (7.4).
///
/// Uma mesa é recalculada ONDE ESTÁ: a célula vem dos quatro cantos do
/// contorno desenhado (<see cref="TableCells"/>), o terreno é reamostrado
/// ali, os pilares e as pontas baixas são refeitos com a configuração e o
/// perfil de mesa ATUAIS, tudo dela é apagado e desenhado de novo com o
/// MESMO GUID, e a mesa nasce limpa. O alinhamento de fileira (5.4) não é
/// refeito para as vizinhas: isso é o Regerar área.
/// </summary>
public static class RecalcularCommands
{
    /// <summary>CLIVUS_RECALCULAR: a mesa da peça selecionada (ou clicada).</summary>
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

    /// <summary>CLIVUS_RECALCULAR_SUJAS: só as mesas sujas, uma a uma.</summary>
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

    /// <summary>CLIVUS_RECALCULAR_AUTO: as sujas, com a mesa de exemplo, sem perguntar. Para o nível 2.</summary>
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
        var doProjeto = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (doProjeto.EmbedmentNote(perfil.Frame) is { } notaDoT3) editor.WriteMessage($"\n  ATENÇÃO: {notaDoT3}.\n");
        var settings = doProjeto.ForTable(perfil.Frame);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        // Recalcular uma mesa troca as entidades DELA; a varredura é refeita
        // depois de cada uma, porque as vizinhas são lidas dela (as pontas).
        IReadOnlyDictionary<Guid, TableParts> todas;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
            todas = LayoutScan.Tables(transacao, documento.Database);

        var feitas = 0;

        // Uma vez, antes de apagar qualquer mesa: as cotas da própria mesa
        // contam (numa mesa só, apagá-la antes deixaria o desenho sem cotas
        // e ela voltaria sem as dela), e varrer o desenho por mesa custaria
        // N varreduras num lote grande. Revisão do 8.8.
        var analise = LayoutDrawer.Analise.ComoODesenho(documento.Database);

        foreach (var guid in mesas)
        {
            if (!RecalcularUma(editor, documento, terreno, guid, todas, perfil, geometria, settings, analise)) continue;

            feitas++;

            // A mesa recalculada trocou de entidades: a próxima lê a PB
            // dela como vizinha, e precisa das novas.
            if (mesas.Count > 1)
            {
                using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();
                todas = LayoutScan.Tables(transacao, documento.Database);
            }
        }

        editor.WriteMessage($"\nRECALCULAR {feitas} de {mesas.Count} mesa(s) recalculada(s) com a configuração atual.\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static bool RecalcularUma(
        Editor editor, Document documento, ProcessedTerrain terreno, Guid guid, IReadOnlyDictionary<Guid, TableParts> todas,
        TableProfile perfil, TableGeometry geometria, ProjectSettings settings, LayoutDrawer.Analise analise)
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
                + "Apague a cópia, ou use o Regerar área.\n");
            return false;
        }

        List<Point3> cantos;

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var polilinha = (Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead);
            cantos = FileiraCommands.Vertices(polilinha, transacao).ToList();
        }

        // O perfil do tamanho da mesa desenhada, não o "atual" da biblioteca:
        // numa usina com mesas de dois tamanhos, cada uma se refaz com o seu.
        var doDesenho = FileiraCommands.PerfilDaMesaDesenhada(cantos, perfil, mesa.Identity!.ProfileName, documento.Database);

        if (!ReferenceEquals(doDesenho, perfil))
        {
            perfil = doDesenho;
            geometria = FileiraCommands.GeometriaDe(perfil);
        }

        PlacedTable celula;

        try
        {
            celula = TableCells.FromCorners(cantos, mesa.Identity!.Label, geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nRECALCULAR {mesa.Identity!.Label}: {erro.Message} Use o Regerar área.\n");
            return false;
        }

        // Mesa com as pontas escolhidas à mão (botão Pontas): refeita com as
        // mesmas alturas, no terreno de onde ela estiver agora. As cotas de
        // partida vêm dos cantos da borda baixa do contorno.
        ProcessedRow fileira;
        (double, double)? pontas = null;
        var avisos = new List<string>();

        if (mesa.Identity.HasManualEnds)
        {
            ManualEndsResult ajuste;

            // Uma mesa cujas pontas não se consegue refazer (ponta fora do
            // terreno no lugar novo, conta que não converge) não derruba o
            // lote: é pulada, dita, e as outras seguem.
            try
            {
                ajuste = ManualEnds.Apply(
                    celula, geometria, perfil.TiltRadians, terreno.Mesh, settings, cantos[0].Z, cantos[1].Z,
                    mesa.Identity.ManualFirstLowEdge, mesa.Identity.ManualLastLowEdge);
            }
            catch (InvalidOperationException erro)
            {
                editor.WriteMessage($"\nRECALCULAR {mesa.Identity.Label} tem as pontas escolhidas à mão e não deu para refazê-las: {erro.Message} Use Pontas > Automatico.\n");
                return false;
            }

            fileira = ajuste.Row;
            pontas = (mesa.Identity.ManualFirstLowEdge!.Value, mesa.Identity.ManualLastLowEdge!.Value);
            avisos.AddRange(ajuste.Warnings);
            editor.WriteMessage($"\n  {mesa.Identity.Label} tem as pontas escolhidas à mão: mantidas.\n");
        }
        else
        {
            // Regra sagrada 6: as pontas ficam presas na PB das vizinhas,
            // que continuam onde estão. A corrente escolhe só o giro.
            var (primeira, ultima) = PontasVizinhas.Ler(
                documento.Database, terreno.Mesh, guid, celula, todas, geometria, perfil.TiltRadians, settings);

            fileira = RowPipeline.ProcessRow(
                new PlanRow(celula.Row, [celula]), geometria, perfil.TiltRadians, terreno.Mesh, settings,
                firstTip: primeira?.Clearance, lastTip: ultima?.Clearance);

            // O que o solver de fato usou: a ponta presa que a declividade
            // não deixou ligar foi solta, e isso é dito.
            var brasil = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            var juntas = fileira.Solution.Runs.Count == 1 ? fileira.Solution.Runs[0].JointClearances : [];
            var presas = new List<string>();
            var soltas = new List<string>();

            void Conferir(PontaPresa? ponta, int indice)
            {
                if (ponta is null) return;

                var texto = $"{ponta.Label} (PB {ponta.Clearance.ToString("0.00", brasil)})";

                if (juntas.Count == 2 && Math.Abs(juntas[indice] - ponta.Clearance) <= 0.015) presas.Add(texto);
                else soltas.Add(texto);
            }

            Conferir(primeira, 0);
            Conferir(ultima, 1);

            if (presas.Count > 0)
                editor.WriteMessage($"\n  {mesa.Identity.Label}: pontas presas nas vizinhas {string.Join(" e ", presas)}.\n");

            if (soltas.Count > 0)
                editor.WriteMessage(
                    $"\n  ATENÇÃO: {mesa.Identity.Label} não conseguiu prender a ponta em {string.Join(" e ", soltas)}: "
                    + "a declividade não deixa. A junta ficou aberta; use o Regerar área.\n");
        }

        Apagar(documento, mesa);

        var desenho = LayoutDrawer.Draw(
            documento.Database, fileira, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses, _ => guid, _ => pontas, analise,
            LayoutDrawer.TiposDeMesa.DaMesa(documento.Database, mesa.Identity!.ProfileName, geometria, perfil.Layout.Module));
        var processada = fileira.Tables[0];

        foreach (var aviso in fileira.Warnings.Concat(avisos)) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");

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

    /// <summary>Apaga todas as peças de uma mesa, numa transação.</summary>
    internal static void Apagar(Document documento, TableParts mesa) => Apagar(documento, [mesa]);

    /// <summary>Apaga todas as peças destas mesas, e os grupos que ficarem vazios, numa transação só.</summary>
    internal static void Apagar(Document documento, IReadOnlyCollection<TableParts> mesas)
    {
        using var transacao = documento.Database.TransactionManager.StartTransaction();

        var pecas = mesas.SelectMany(m => m.All).ToList();
        var grupos = LayoutGroups.GruposDe(transacao, pecas);

        foreach (var peca in pecas)
        {
            var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
            entidade.Erase();
        }

        LayoutGroups.ApagarVazios(transacao, grupos);
        transacao.Commit();
    }

    internal static Guid? MesaDaSelecao(Editor editor, Document documento)
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

    internal static Guid? MesaClicada(Editor editor, Document documento, string pergunta = "\nClique numa peça da mesa a recalcular: ")
    {
        var opcoes = new PromptEntityOptions(pergunta);
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
