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
        var apagadas = LayoutEraser.ApagarDentro(documento.Database, area.Vertices);

        editor.WriteMessage(
            $"\nREFAZER {apagadas.Tables} mesa(s) apagada(s) dentro de {area.Nome} "
            + $"({apagadas.Entities} entidade(s)); desenhando de novo com a configuração atual...\n");

        UsinaCommands.Executar(editor, documento, terreno, area, alinhamento, perfil, avisarSeJaHaMesas: false);
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

/// <summary>O que o apagar por área removeu.</summary>
internal sealed record Erased(int Tables, int Entities);

/// <summary>
/// Apaga o que o plugin desenhou dentro de uma área: toda mesa cujo
/// contorno (ou, sem contorno, a primeira peça) está dentro do polígono,
/// com pilares, módulos, faces e notas. Pelo XData, nunca pela camada.
/// </summary>
internal static class LayoutEraser
{
    internal static Erased ApagarDentro(Database database, IReadOnlyList<Point3> area)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(area);

        var mesas = 0;
        var entidades = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
        {
            var referencia = mesa.Contour ?? mesa.All.Cast<ObjectId?>().FirstOrDefault();
            if (referencia is not { } id || id.IsNull) continue;

            var ponto = Ponto(transacao, id);
            if (ponto is null || !Polygons.Contains(area, ponto.Value.X, ponto.Value.Y)) continue;

            foreach (var peca in mesa.All)
            {
                var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
                entidade.Erase();
                entidades++;
            }

            mesas++;
        }

        transacao.Commit();

        return new Erased(mesas, entidades);
    }

    /// <summary>Um ponto de referência da entidade, em planta.</summary>
    private static Point3? Ponto(Transaction transacao, ObjectId id)
    {
        switch (transacao.GetObject(id, OpenMode.ForRead))
        {
            case Polyline3d polilinha:
                foreach (ObjectId v in polilinha)
                {
                    var vertice = (PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead);
                    return new Point3(vertice.Position.X, vertice.Position.Y, 0);
                }
                return null;
            case BlockReference bloco:
                return new Point3(bloco.Position.X, bloco.Position.Y, 0);
            case Face face:
                var p = face.GetVertexAt(0);
                return new Point3(p.X, p.Y, 0);
            case MText texto:
                return new Point3(texto.Location.X, texto.Location.Y, 0);
            default:
                return null;
        }
    }
}
