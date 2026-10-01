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

    /// <summary>O que o planejamento da usina produz, para o desenho vir depois (o Refazer apaga entre os dois).</summary>
    internal sealed record PlanoDaUsina(ProjectSettings Settings, TableGeometry Geometria, TableProfile Perfil, PlanLayout Layout, ProcessedPlant Usina);

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
        var settings = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        if (avisarSeJaHaMesas) FileiraCommands.AvisarSeJaHaMesas(editor, documento.Database);

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);
        var config = settings.Configuration;
        var celula = new TableFootprint(geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));

        editor.WriteMessage(
            $"\nMesa: {perfil.Describe()}\n"
            + $"Configuração: {settings.Describe()}\n"
            + $"Área: {area.Nome}; alinhamento: {alinhamento.Identidade.Describe()}\n");

        PlanLayout layout;

        try
        {
            layout = RowDistributor.Distribute(
                area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, celula,
                config.UpslopeAzimuthRadians);
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

        var usina = PlantPipeline.ProcessAll(
            layout, geometria, perfil.TiltRadians,
            perfil.Layout.ModuleCount, perfil.Layout.Module.PowerWatts, terreno.Mesh, settings,
            (feitas, total) => { if (feitas % 10 == 0 || feitas == total) editor.WriteMessage($"  {feitas}/{total} fileira(s)\n"); });

        return new PlanoDaUsina(settings, geometria, perfil, layout, usina);
    }

    /// <summary>Desenha o que foi planejado e relata.</summary>
    internal static void Desenhar(Editor editor, Document documento, PlanoDaUsina plano)
    {
        var (settings, geometria, perfil, _, usina) = plano;

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var desenhadas = 0;
        var pilaresDesenhados = 0;
        var modulosDesenhados = 0;
        var pintadas = 0;
        var marcadas = 0;

        foreach (var fileira in usina.Rows)
        {
            var desenho = LayoutDrawer.Draw(documento.Database, fileira, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses);

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

        editor.WriteMessage($"\n  As alturas estão na camada {LayoutLayers.Alturas}, desligada. {PluginInfo.ComandoAlturas} liga.\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }
}
