using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.RefazerCommands))]

namespace UFV.Plugin;

/// <summary>
/// Refazer as mesas de uma área (pedido do Renan em 26/09/2026): apaga tudo
/// que o plugin desenhou dentro da área e desenha de novo com a
/// configuração ATUAL. "Sempre, as mesas serão desenhadas baseadas nas
/// configurações": trocou a configuração, refaz.
///
/// Chega por três caminhos: o botão Refazer, o comando UFV_REFAZER, e o
/// menu de botão direito sobre a polilinha da área (<see cref="MenuDeContexto"/>).
/// </summary>
public static class RefazerCommands
{
    /// <summary>UFV_REFAZER: a área (da seleção, ou clicada), o alinhamento, apaga e redesenha.</summary>
    [CommandMethod(PluginInfo.ComandoRefazer, CommandFlags.UsePickSet)]
    public static void Refazer()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var area = AreaDaSelecao(editor, documento) ?? FileiraCommands.EscolherArea(editor, documento);
            if (area is null) return;

            var alinhamento = FileiraCommands.EscolherAlinhamento(editor, documento);
            if (alinhamento is null) return;

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao refazer as mesas.", erro);
            editor.WriteMessage($"\nNão consegui refazer as mesas: {erro.Message}\n");
        }
    }

    /// <summary>UFV_REFAZER_AUTO: a primeira área e o primeiro alinhamento, com a mesa de exemplo. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoRefazerAutomatico)]
    public static void RefazerAutomatico()
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
                editor.WriteMessage("\nREFAZER Sem área ou sem alinhamento registrado neste desenho.\n");
                return;
            }

            var area = FileiraCommands.LerArea(documento.Database, areas[0].Handle);
            var alinhamento = FileiraCommands.LerAlinhamento(documento.Database, alinhamentos[0]);

            if (area is null || alinhamento is null)
            {
                editor.WriteMessage("\nREFAZER A área ou o alinhamento registrado não está mais no desenho.\n");
                return;
            }

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao refazer as mesas automaticamente.", erro);
            editor.WriteMessage($"\nNão consegui refazer as mesas: {erro.Message}\n");
        }
    }

    private static void Executar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        (IReadOnlyList<Point3> Vertices, string Nome) area,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade) alinhamento,
        TableProfile perfil)
    {
        // Planeja ANTES de apagar: se a distribuição não dá fileira (linha
        // paralela, área do outro lado), nada é apagado.
        var plano = UsinaCommands.Planejar(editor, documento, terreno, area, alinhamento, perfil, avisarSeJaHaMesas: false);

        if (plano is null)
        {
            editor.WriteMessage("\nREFAZER Nada foi apagado.\n");
            return;
        }

        var apagadas = LayoutEraser.ApagarDentro(documento.Database, area.Vertices);
        var orfas = LayoutEraser.ApagarOrfasDentro(documento.Database, area.Vertices);

        if (apagadas.Tables.Count > 0) RemovalStore.Remove(documento.Database, apagadas.Tables);

        editor.WriteMessage(
            $"\nREFAZER {apagadas.Tables.Count} mesa(s) apagada(s) dentro de {area.Nome} "
            + $"({apagadas.Entities} entidade(s)"
            + (orfas > 0 ? $", mais {orfas} nota(s) órfã(s) de desenho antigo" : string.Empty)
            + "); desenhando de novo com a configuração atual (se algo falhar, U devolve as apagadas)...\n");

        UsinaCommands.Desenhar(editor, documento, plano);
    }

    /// <summary>A área que veio selecionada antes do comando (o botão direito sobre ela), ou null.</summary>
    private static (IReadOnlyList<Point3> Vertices, string Nome)? AreaDaSelecao(Editor editor, Document documento)
    {
        var selecao = editor.SelectImplied();
        if (selecao.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        foreach (var id in selecao.Value.GetObjectIds())
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var identidade = AreaXData.Load(polilinha);
            if (identidade is null) continue;

            editor.SetImpliedSelection([]);
            editor.WriteMessage($"\nÁrea: {identidade.DisplayName}.\n");

            return (FileiraCommands.Vertices(polilinha, transacao), identidade.DisplayName);
        }

        return null;
    }
}

/// <summary>O que o apagar por área removeu: os GUIDs das mesas e quantas entidades.</summary>
internal sealed record Erased(IReadOnlyList<Guid> Tables, int Entities);

/// <summary>
/// Apaga o que o plugin desenhou dentro de uma área: toda mesa com algum
/// vértice do contorno (ou, sem contorno, a primeira peça) dentro do
/// polígono, com pilares, módulos, faces e notas; mesa copiada (dois
/// contornos com o mesmo GUID) vai inteira, com as duas cópias. Pelo
/// XData, nunca pela camada.
/// </summary>
internal static class LayoutEraser
{
    internal static Erased ApagarDentro(Database database, IReadOnlyList<Point3> area)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(area);

        var mesas = new List<Guid>();
        var entidades = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        foreach (var (guid, mesa) in LayoutScan.Tables(transacao, database))
        {
            var referencias = mesa.Contours.Count > 0 ? mesa.Contours : mesa.All.Take(1).ToList();

            if (!referencias.Any(id => Pontos(transacao, id).Any(p => Polygons.Contains(area, p.X, p.Y)))) continue;

            foreach (var peca in mesa.All)
            {
                var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
                entidade.Erase();
                entidades++;
            }

            mesas.Add(guid);
        }

        transacao.Commit();

        return new Erased(mesas, entidades);
    }

    /// <summary>
    /// Apaga, dentro da área, as notas SEM identidade nas nossas camadas de
    /// alturas e de marcadas (riscos e textos de desenho feito antes de as
    /// notas ganharem XData, 26/09/2026): ninguém as reconhece como peça de
    /// mesa, e elas ficavam para trás no Refazer. Quantas foram.
    /// </summary>
    internal static int ApagarOrfasDentro(Database database, IReadOnlyList<Point3> area)
    {
        var apagadas = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDoTexto && id.ObjectClass != ClasseDaLinha) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            var nossaCamada = string.Equals(entidade.Layer, LayoutLayers.Alturas, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entidade.Layer, LayoutLayers.Marcada, StringComparison.OrdinalIgnoreCase);
            if (!nossaCamada) continue;

            using (var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo))
                if (dados is not null) continue;

            var ponto = entidade switch
            {
                MText texto => new Point3(texto.Location.X, texto.Location.Y, 0),
                Line linha => new Point3((linha.StartPoint.X + linha.EndPoint.X) / 2, (linha.StartPoint.Y + linha.EndPoint.Y) / 2, 0),
                _ => (Point3?)null,
            } ?? new Point3(double.NaN, double.NaN, 0);

            if (!ponto.IsFinite || !Polygons.Contains(area, ponto.X, ponto.Y)) continue;

            entidade.UpgradeOpen();
            entidade.Erase();
            apagadas++;
        }

        transacao.Commit();

        return apagadas;
    }

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));

    /// <summary>Os pontos de referência da entidade, em planta: os vértices do contorno, ou um ponto da peça.</summary>
    private static IEnumerable<Point3> Pontos(Transaction transacao, ObjectId id)
    {
        switch (transacao.GetObject(id, OpenMode.ForRead))
        {
            case Polyline3d polilinha:
                foreach (ObjectId v in polilinha)
                {
                    var vertice = (PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead);
                    yield return new Point3(vertice.Position.X, vertice.Position.Y, 0);
                }
                break;
            case BlockReference bloco:
                yield return new Point3(bloco.Position.X, bloco.Position.Y, 0);
                break;
            case Face face:
                var p = face.GetVertexAt(0);
                yield return new Point3(p.X, p.Y, 0);
                break;
            case MText texto:
                yield return new Point3(texto.Location.X, texto.Location.Y, 0);
                break;
        }
    }
}
