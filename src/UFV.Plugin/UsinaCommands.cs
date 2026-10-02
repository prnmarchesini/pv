using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.UsinaCommands))]

namespace UFV.Plugin;

/// <summary>
/// A área inteira no CAD: todas as fileiras da distribuição, processadas e
/// desenhadas, com o tempo medido. É o passo 5.9, o que o Renan compara com
/// o PVcase em contagens e alturas.
///
/// É o 5.8 repetido fileira a fileira; o que muda é a escala, e por isso o
/// tempo é medido e dito — do motor e do desenho, em separado.
/// </summary>
public static class UsinaCommands
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>UFV_USINA: escolhe área e alinhamento, processa e desenha todas as fileiras.</summary>
    [CommandMethod(PluginInfo.ComandoUsina)]
    public static void Usina()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var area = FileiraCommands.EscolherArea(editor, documento);
            if (area is null) return;

            var alinhamento = FileiraCommands.EscolherAlinhamento(editor, documento);
            if (alinhamento is null) return;

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a usina.", erro);
            editor.WriteMessage($"\nNão consegui processar a usina: {erro.Message}\n");
        }
    }

    /// <summary>UFV_USINA_AUTO: a primeira área e o primeiro alinhamento, sem perguntar. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoUsinaAutomatico)]
    public static void UsinaAutomatica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var areas = AreaStore.Load(documento.Database);
            var alinhamentos = AlignmentStore.Load(documento.Database);

            if (areas.Count == 0 || alinhamentos.Count == 0)
            {
                editor.WriteMessage("\nUSINA Sem área ou sem alinhamento registrado neste desenho.\n");
                return;
            }

            var area = FileiraCommands.LerArea(documento.Database, areas[0].Handle);
            var alinhamento = FileiraCommands.LerAlinhamento(documento.Database, alinhamentos[0]);

            if (area is null || alinhamento is null)
            {
                editor.WriteMessage("\nUSINA A área ou o alinhamento registrado não está mais no desenho.\n");
                return;
            }

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a usina automática.", erro);
            editor.WriteMessage($"\nNão consegui processar a usina: {erro.Message}\n");
        }
    }

    /// <summary>
    /// As mesas do desenho em uso (8.5/8.6), na ordem da lista (a
    /// prioridade). Lista vazia: nenhuma em uso, vale o perfil de sempre. Null,
    /// com a mensagem dada: em uso, mas com inclinações diferentes.
    /// </summary>
    internal static IReadOnlyList<DrawingTable>? MesasEmUso(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database, string prefixo)
    {
        var lidas = MesasDoDesenho.Ler(database, out var problemas);
        foreach (var problema in problemas) editor.WriteMessage($"\n  ATENÇÃO: {problema}.\n");

        // As do desenho, pela prioridade da lista; sem nenhuma marcada, todas.
        var emUso = DrawingTables.ForEngine(lidas);

        if (lidas.Count == 0 && TemNaBiblioteca())
            editor.WriteMessage($"\n{prefixo} ATENÇÃO: este desenho não tem mesas cadastradas; vale a da janela de Mesa. As mesas salvas na biblioteca só valem depois de Configurações > Salvar no desenho.\n");

        if (emUso.Count > 0)
            editor.WriteMessage($"\n{prefixo} Mesas, pela prioridade: {string.Join(", ", emUso.Select((m, i) => $"{i + 1}ª {m.Name}"))}.\n");

        if (emUso.Select(m => Math.Round(m.Profile.TiltDegrees, 3)).Distinct().Count() > 1)
        {
            editor.WriteMessage(
                $"\n{prefixo} As mesas marcadas para uso têm inclinações diferentes ("
                + string.Join(", ", emUso.Select(m => $"{m.Name} a {m.Profile.TiltDegrees.ToString("0.#", Brasil)}°"))
                + "): numa fileira elas precisam ter a mesma. Ajuste em Configurações > Escolha das estruturas.\n");
            return null;
        }

        return emUso;
    }

    private static bool TemNaBiblioteca()
    {
        try
        {
            return new TableProfileStore(MesaCommands.PastaDosPerfis).List().Any();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui listar a biblioteca de perfis.", erro);
            return false;
        }
    }

    /// <summary>Os tipos para o desenho e as pegadas para a distribuição, das mesas em uso.</summary>
    internal static (LayoutDrawer.TiposDeMesa Tipos, List<TableFootprint> Pegadas, List<int> Modulos) Tipos(IReadOnlyList<DrawingTable> emUso)
    {
        var geometrias = emUso.Select(m => FileiraCommands.GeometriaDe(m.Profile)).ToList();
        var tilt = emUso[0].Profile.TiltRadians;

        return (
            new LayoutDrawer.TiposDeMesa(
                geometrias,
                emUso.Select(m => m.Profile.Layout.Module).ToList(),
                emUso.Select(m => (string?)m.Name).ToList(),
                emUso.Select(m => (RgbColor?)m.Color).ToList()),
            geometrias.Select(g => new TableFootprint(g.Length, g.Depth * Math.Cos(tilt))).ToList(),
            emUso.Select(m => m.Profile.Layout.ModuleCount).ToList());
    }

    /// <summary>O que o planejamento da usina produz, para o desenho vir depois (o Refazer apaga entre os dois).</summary>
    internal sealed record PlanoDaUsina(
        ProjectSettings Settings, TableGeometry Geometria, TableProfile Perfil, PlanLayout Layout, ProcessedPlant Usina,
        LayoutDrawer.TiposDeMesa? Tipos = null);

    internal static void Executar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        (IReadOnlyList<Point3> Vertices, string Nome) area,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade) alinhamento,
        TableProfile perfil,
        bool avisarSeJaHaMesas = true)
    {
        var plano = Planejar(editor, documento, terreno, area, alinhamento, perfil, avisarSeJaHaMesas);
        if (plano is null) return;

        Desenhar(editor, documento, plano);
    }

    /// <summary>
    /// Distribui e processa a usina inteira, sem tocar no desenho. Null,
    /// com a mensagem já dada, quando não há o que desenhar.
    /// </summary>
    internal static PlanoDaUsina? Planejar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        (IReadOnlyList<Point3> Vertices, string Nome) area,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade) alinhamento,
        TableProfile perfil,
        bool avisarSeJaHaMesas)
    {
        // As mesas do desenho marcadas para uso (8.5/8.6); sem nenhuma, o
        // perfil de sempre, e a usina sai como saía.
        var doDesenho = MesasEmUso(editor, documento.Database, "USINA");
        if (doDesenho is null) return null;

        if (doDesenho.Count > 0) perfil = doDesenho[0].Profile;

        var doProjeto = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (doProjeto.EmbedmentNote(perfil.Frame) is { } notaDoT3) editor.WriteMessage($"\n  ATENÇÃO: {notaDoT3}.\n");
        var settings = doProjeto.ForTable(perfil.Frame);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        if (avisarSeJaHaMesas) FileiraCommands.AvisarSeJaHaMesas(editor, documento.Database);

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);
        var config = settings.Configuration;
        var celula = new TableFootprint(geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));

        LayoutDrawer.TiposDeMesa? tipos = null;
        var footprints = new List<TableFootprint> { celula };

        if (doDesenho.Count > 0)
        {
            (tipos, footprints, _) = Tipos(doDesenho);

            if (doDesenho.Count > 1)
                editor.WriteMessage($"\nMesas em uso: {string.Join(", ", doDesenho.Select(m => $"{m.Name} ({m.Profile.Layout.ModuleCount} módulos)"))}\n");
        }

        editor.WriteMessage(
            $"\nMesa: {perfil.Describe()}\n"
            + $"Configuração: {settings.Describe()}\n"
            + $"Área: {area.Nome}; alinhamento: {alinhamento.Identidade.Describe()}\n");

        PlanLayout layout;

        try
        {
            layout = tipos is null
                ? RowDistributor.Distribute(
                    area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, celula,
                    config.UpslopeAzimuthRadians)
                : RowDistributor.Distribute(
                    area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, footprints,
                    doDesenho.Select(m => m.Profile.Layout.ModuleCount).ToList(), config.UpslopeAzimuthRadians);
        }
        catch (ArgumentException erro)
        {
            editor.WriteMessage($"\nUSINA {erro.Message}\n");
            return null;
        }

        if (layout.Rows.Count == 0)
        {
            editor.WriteMessage("\nUSINA Nenhuma fileira cabe: a área está do outro lado da linha, a linha não a atravessa, ou ela é pequena demais.\n");
            return null;
        }

        editor.WriteMessage($"\nProcessando {layout.Rows.Count} fileira(s), {layout.Tables.Count} mesa(s)...\n");

        void Progresso(int feitas, int total)
        {
            if (feitas % 10 == 0 || feitas == total) editor.WriteMessage($"  {feitas}/{total} fileira(s)\n");
        }

        var usina = tipos is null
            ? PlantPipeline.ProcessAll(
                layout, geometria, perfil.TiltRadians,
                perfil.Layout.ModuleCount, perfil.Layout.Module.PowerWatts, terreno.Mesh, settings, Progresso)
            : PlantPipeline.ProcessAll(
                layout, tipos.Geometrias, perfil.TiltRadians,
                doDesenho.Select(m => m.Profile.Layout.ModuleCount).ToList(),
                doDesenho.Select(m => m.Profile.Layout.Module.PowerWatts).ToList(),
                terreno.Mesh, settings, Progresso);

        if (tipos is not null && doDesenho.Count > 1)
        {
            var porTipo = usina.TablesByKind;
            editor.WriteMessage($"  por mesa: {string.Join(", ", doDesenho.Select((m, k) => $"{porTipo[k]} × {m.Name}"))}\n");
        }

        return new PlanoDaUsina(settings, geometria, perfil, layout, usina, tipos);
    }

    /// <summary>Desenha o que foi planejado e relata.</summary>
    internal static void Desenhar(Editor editor, Document documento, PlanoDaUsina plano)
    {
        var (settings, geometria, perfil, _, usina, tipos) = plano;

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var desenhadas = 0;
        var pilaresDesenhados = 0;
        var modulosDesenhados = 0;
        var pintadas = 0;
        var marcadas = 0;

        foreach (var fileira in usina.Rows)
        {
            var desenho = LayoutDrawer.Draw(
                documento.Database, fileira, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses,
                analisar: LayoutDrawer.Analise.Nada, tipos: tipos);

            desenhadas += desenho.Tables;
            pilaresDesenhados += desenho.Pillars;
            modulosDesenhados += desenho.Modules;
            pintadas += desenho.Painted;
            marcadas += desenho.Marked;
        }

        relogio.Stop();

        foreach (var aviso in usina.Warnings.Take(10)) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");
        if (usina.Warnings.Count > 10) editor.WriteMessage($"\n  ... e mais {usina.Warnings.Count - 10} aviso(s).\n");

        var cotas = usina.Tables.SelectMany(t => t.Pillars.Pillars).Where(p => p.Length is not null).Select(p => p.Length!.Value).ToList();

        editor.WriteMessage(
            $"\nUSINA {usina.Describe()}\n"
            + $"  desenhado: {desenhadas} mesa(s), {pilaresDesenhados} pilar(es), {modulosDesenhados} módulo(s) com face, "
            + $"{pintadas} peça(s) pintada(s), {marcadas} marcada(s)\n"
            + (cotas.Count > 0
                ? $"  comprimento de pilar: de {cotas.Min().ToString("0.00", Brasil)} a {cotas.Max().ToString("0.00", Brasil)} m, "
                  + $"média {cotas.Average().ToString("0.00", Brasil)} m\n"
                : "  nenhum pilar com comprimento\n")
            + $"  tempo: motor {usina.Elapsed.TotalSeconds.ToString("0.0", Brasil)} s, desenho {relogio.Elapsed.TotalSeconds.ToString("0.0", Brasil)} s\n");

        foreach (var fileira in usina.Rows)
            editor.WriteMessage($"  {fileira.Describe()}\n");

        editor.WriteMessage("\n  Gerado sem análise: alturas, declividade e cores de análise saem pelo botão Análises.\n");
        editor.WriteMessage(LayoutDrawer.TiposDeMesa.Legenda(tipos, marcadas) + "\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }
}
