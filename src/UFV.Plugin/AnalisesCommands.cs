using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.AnalisesCommands))]

namespace UFV.Plugin;

/// <summary>
/// A seção Análises (Renan, 27/09/2026: "no menu análises é simples, quero
/// ter seções para poder alterar os parâmetros, um clique o sistema pinta
/// para mim o que estourou, e um outro clique, outro botão, o sistema regera
/// para mim seguindo novas configurações").
///
/// - Parâmetros: a tela de configuração só com o que decide o que estoura
///   (faixa da ponta baixa, lombo, degraus, declividade) e as cores.
/// - Pintar estouros: repinta toda mesa COMO ESTÁ (mesmas cotas, mesmo
///   GUID) com as regras gravadas: módulo com a ponta baixa abaixo ou acima
///   da faixa, pilar fora do comprimento, declividade. Não move nada. Não
///   olha degrau entre mesas (repinta mesa a mesa); a mesa que passa do
///   lombo com a regra nova fica marcada (regra sagrada 4).
/// - Regerar: refaz todas as áreas registradas com a configuração atual (é
///   o Refazer de cada área, em sequência).
/// </summary>
public static class AnalisesCommands
{
    /// <summary>UFV_ANALISES_PARAMETROS: a tela dos parâmetros das análises.</summary>
    [CommandMethod(PluginInfo.ComandoAnalisesParametros)]
    public static void Parametros()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (!UfvExtension.TemInterface())
        {
            editor.WriteMessage("\nA tela dos parâmetros precisa da interface do Civil 3D.\n");
            return;
        }

        try
        {
            AbrirParametros(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir os parâmetros das análises.", erro);
            editor.WriteMessage($"\nNão consegui abrir os parâmetros: {erro.Message}\n");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbrirParametros(Document documento) => ConfigCommands.Abrir(documento, soAnalises: true);

    /// <summary>UFV_PINTAR: repinta todas as mesas como estão, com as regras gravadas.</summary>
    [CommandMethod(PluginInfo.ComandoPintar)]
    public static void Pintar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Repintar(editor, documento, terreno, FileiraCommands.PerfilDaMesa(editor, silencioso: true));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao pintar os estouros.", erro);
            editor.WriteMessage($"\nNão consegui pintar os estouros: {erro.Message}\n");
        }
    }

    /// <summary>UFV_PINTAR_AUTO: o mesmo com a mesa de exemplo. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoPintarAutomatico)]
    public static void PintarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            Repintar(editor, documento, terreno, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no UFV_PINTAR_AUTO.", erro);
            editor.WriteMessage($"\nNão consegui pintar os estouros: {erro.Message}\n");
        }
    }

    /// <summary>UFV_REGERAR: refaz todas as áreas com a configuração atual.</summary>
    [CommandMethod(PluginInfo.ComandoRegerar)]
    public static void Regerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            RegerarTudo(editor, documento, terreno, FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao regerar as áreas.", erro);
            editor.WriteMessage($"\nNão consegui regerar: {erro.Message}\n");
        }
    }

    /// <summary>
    /// Repinta cada mesa onde está, com as cotas que ela tem (os cantos da
    /// borda baixa do contorno), a marca e o motivo que ela tem, e as pontas
    /// à mão que ela tem. Mesa suja fica de fora: o que está desenhado nela
    /// não é o que o motor calculou, e repintar a limparia sem recalcular.
    /// </summary>
    private static void Repintar(Editor editor, Document documento, ProcessedTerrain terreno, TableProfile perfil)
    {
        var settings = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        var processadas = new List<ProcessedTable>();
        // Por referência: ProcessedTable é record, e a igualdade de valor
        // (recursiva) não é o que se quer para achar a mesa de volta.
        var guids = new Dictionary<ProcessedTable, Guid>(ReferenceEqualityComparer.Instance);
        var pontas = new Dictionary<ProcessedTable, (double, double)>(ReferenceEqualityComparer.Instance);

        // O perfil de cada mesa pelo tamanho desenhado (29/09/2026): numa
        // usina com mesas de dois tamanhos cada uma se repinta com o seu.
        var perfilDa = new Dictionary<ProcessedTable, TableProfile>(ReferenceEqualityComparer.Instance);
        var geometrias = new Dictionary<TableProfile, TableGeometry>(ReferenceEqualityComparer.Instance) { [perfil] = geometria };
        var aApagar = new List<TableParts>();
        var sujas = 0;
        var puladas = new List<string>();

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var (guid, mesa) in LayoutScan.Tables(transacao, documento.Database))
            {
                if (mesa.Identity is not { } identidade) continue;

                if (mesa.Contour is not { } contorno)
                {
                    puladas.Add($"{identidade.Label} (sem contorno)");
                    continue;
                }

                if (identidade.Dirty)
                {
                    sujas++;
                    continue;
                }

                if (mesa.IsDuplicated || transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha)
                {
                    puladas.Add(identidade.Label);
                    continue;
                }

                var cantos = FileiraCommands.Vertices(polilinha, transacao);
                var perfilDela = FileiraCommands.PerfilDaMesaDesenhada(cantos, perfil);

                if (!geometrias.TryGetValue(perfilDela, out var geometriaDela))
                {
                    geometriaDela = FileiraCommands.GeometriaDe(perfilDela);
                    geometrias[perfilDela] = geometriaDela;
                }

                PlacedTable celula;

                try
                {
                    celula = TableCells.FromCorners(cantos, identidade.Label, geometriaDela.Length, geometriaDela.Depth * Math.Cos(perfilDela.TiltRadians));
                }
                catch (ArgumentException)
                {
                    puladas.Add(identidade.Label);
                    continue;
                }

                var linha = RowPipeline.ProcessFixed(
                    celula, geometriaDela, perfilDela.TiltRadians, terreno.Mesh, settings.ForTable(perfilDela.Frame), cantos[0].Z, cantos[1].Z, identidade.Reason, identidade.Marked);

                var processada = linha.Tables[0];
                processadas.Add(processada);
                perfilDa[processada] = perfilDela;
                guids[processada] = guid;
                if (identidade.HasManualEnds) pontas[processada] = (identidade.ManualFirstLowEdge!.Value, identidade.ManualLastLowEdge!.Value);
                aApagar.Add(mesa);
            }
        }

        if (processadas.Count == 0)
        {
            editor.WriteMessage("\nPINTAR Nenhuma mesa para pintar.\n");
            return;
        }

        RecalcularCommands.Apagar(documento, aApagar);

        var pintadas = 0;
        var marcadas = 0;

        foreach (var grupo in processadas.GroupBy(p => perfilDa[p], ReferenceEqualityComparer.Instance))
        {
            var doGrupo = grupo.ToList();
            var perfilDoGrupo = (TableProfile)grupo.Key!;

            var todas = new ProcessedRow(
                new PlanRow(0, doGrupo.Select(p => p.Cell).ToList()),
                new RowSolution([new SolvedRun(doGrupo.Select(p => p.Solved).ToList(), [])]),
                doGrupo,
                []);

            var desenho = LayoutDrawer.Draw(
                documento.Database, todas, geometrias[perfilDoGrupo], perfilDoGrupo.Layout.Module, perfilDoGrupo.TiltRadians, settings.Analyses,
                p => guids[p], p => pontas.TryGetValue(p, out var v) ? v : null);

            pintadas += desenho.Painted;
            marcadas += desenho.Marked;
        }

        var abaixo = processadas.Sum(p => p.Report.Modules.Count(m => m.Verdict.Outcome == AnalysisOutcome.Below));
        var acima = processadas.Sum(p => p.Report.Modules.Count(m => m.Verdict.Outcome == AnalysisOutcome.Above));

        editor.WriteMessage(
            $"\nPINTAR {processadas.Count} mesa(s) repintada(s) com as regras gravadas: {abaixo} módulo(s) com a ponta baixa abaixo "
            + $"da faixa, {acima} acima; {pintadas} peça(s) pintada(s); {marcadas} mesa(s) que não cabem.\n");

        if (sujas > 0) editor.WriteMessage($"  {sujas} mesa(s) suja(s) ficaram como estão: use Recalcular sujas.\n");
        if (puladas.Count > 0) editor.WriteMessage($"  Não repintei {string.Join(", ", puladas)}: sem contorno, contorno repetido ou de outra mesa. Use o Refazer da área.\n");

        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>Cada área registrada com o alinhamento mais perto dela, uma a uma.</summary>
    private static void RegerarTudo(Editor editor, Document documento, ProcessedTerrain terreno, TableProfile perfil)
    {
        var areas = AreaStore.Load(documento.Database);
        var alinhamentos = AlignmentStore.Load(documento.Database);

        if (areas.Count == 0 || alinhamentos.Count == 0)
        {
            editor.WriteMessage("\nREGERAR Sem área ou sem alinhamento registrado neste desenho.\n");
            return;
        }

        var linhas = alinhamentos
            .Select(a => FileiraCommands.LerAlinhamento(documento.Database, a))
            .Where(a => a is not null)
            .Select(a => a!.Value)
            .ToList();

        if (linhas.Count == 0)
        {
            editor.WriteMessage("\nREGERAR Os alinhamentos registrados não estão mais no desenho.\n");
            return;
        }

        var feitas = 0;

        foreach (var registro in areas)
        {
            var area = FileiraCommands.LerArea(documento.Database, registro.Handle);

            if (area is null)
            {
                editor.WriteMessage($"\nREGERAR A área {registro.Identity.DisplayName} não está mais no desenho; pulada.\n");
                continue;
            }

            var alinhamento = linhas.MinBy(l => Distancia(l.Vertices, area.Value.Vertices));

            editor.WriteMessage($"\nREGERAR {area.Value.Nome} com o alinhamento {alinhamento.Identidade.Describe()}.\n");
            RefazerCommands.Executar(editor, documento, terreno, area.Value, alinhamento, perfil);
            feitas++;
        }

        editor.WriteMessage($"\nREGERAR {feitas} de {areas.Count} área(s) refeita(s) com a configuração atual.\n");
    }

    /// <summary>Zero se algum vértice da linha cai na área; senão, a menor distância entre vértices.</summary>
    private static double Distancia(IReadOnlyList<Point3> linha, IReadOnlyList<Point3> area)
    {
        if (linha.Any(p => Polygons.Contains(area, p.X, p.Y))) return 0;

        return linha.Min(p => area.Min(q => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y))));
    }
}
