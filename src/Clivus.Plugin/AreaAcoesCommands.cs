using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AreaAcoesCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Apagar uma área e recalcular as mesas dela (Renan, 30/10/2026: "a
/// ferramenta áreas, que lista áreas, quero um botão para APAGAR área, desde
/// que esteja vazia, se não estiver, o apagar vai apagar as mesas, aí quando
/// tem mesas, quero as opções de limpar, regerar, recalcular, etc."). Chegam
/// pela janela Áreas (a área vai pedida por <see cref="EscolhaDeArea.PedirPelaJanela"/>)
/// e pela linha de comando. Limpar e Regerar são os comandos que já existiam
/// (Apagar tudo e Refazer).
/// </summary>
public static class AreaAcoesCommands
{
    /// <summary>A área que a janela Áreas já confirmou apagar com as mesas: o comando não pergunta de novo.</summary>
    internal static ObjectId ConfirmadaPelaJanela;

    /// <summary>CLIVUS_AREA_APAGAR: a área; com mesas dentro, pergunta antes e as apaga junto.</summary>
    [CommandMethod(PluginInfo.ComandoAreaApagar, CommandFlags.UsePickSet)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var area = EscolhaDeArea.Pedir(editor, documento);
            if (area is null) return;

            var confirmada = ConfirmadaPelaJanela == area.Id;
            ConfirmadaPelaJanela = ObjectId.Null;

            ApagarArea(editor, documento, area, confirmada);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar a área.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar a área: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_AREA_RECALCULAR: todas as mesas da área, onde estão, com o terreno e a configuração de hoje.</summary>
    [CommandMethod(PluginInfo.ComandoAreaRecalcular, CommandFlags.UsePickSet)]
    public static void Recalcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var area = EscolhaDeArea.Pedir(editor, documento);
            if (area is null) return;

            var mesas = MesasComOCentroDentro(documento.Database, area.Contorno);

            if (mesas.Count == 0)
            {
                editor.WriteMessage(Tr.F("\nRECALCULAR Nenhuma mesa dentro de {0}.\n", area.Identity.DisplayName));
                return;
            }

            editor.WriteMessage(Tr.F("\nRECALCULAR {0} mesa(s) de {1}, onde estão, com o terreno e a configuração de hoje...\n", mesas.Count, area.Identity.DisplayName));
            RecalcularCommands.RecalcularMesas(editor, documento, terreno, mesas, FileiraCommands.PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular as mesas da área.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui recalcular as mesas da área: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Apaga a área: a linha, a hachura, o registro e o "segue os objetos".
    /// Com mesas dentro, elas vão junto (o Apagar tudo da área), depois de
    /// perguntar (a janela já perguntou: <paramref name="confirmada"/>). A
    /// área repetida (outra com o mesmo contorno) sai sozinha: as mesas são
    /// da outra.
    /// </summary>
    internal static void ApagarArea(Editor editor, Document documento, AreaNoDesenho area, bool confirmada)
    {
        var database = documento.Database;
        var todas = AreasDoDesenho.Varrer(database);
        var gemeas = EscolhaDeArea.Repetidas(todas, area).Where(a => a.Id != area.Id).ToList();

        if (gemeas.Count == 0)
        {
            var mesas = MesasComOCentroDentro(database, area.Contorno).Count;

            if (mesas > 0 && !confirmada && !Confirmar(editor, area, mesas))
            {
                editor.WriteMessage(Tr.T("\nNada foi apagado.\n"));
                return;
            }

            if (mesas > 0) ApagarTudoCommands.Executar(editor, documento, (area.Contorno, area.Identity.DisplayName));
        }

        var hachuras = HachuraDasAreas.Apagar(database, todas, area);
        CampoCommands.Esquecer(database, area.Contorno);

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            if (!area.Id.IsErased) transacao.GetObject(area.Id, OpenMode.ForWrite, false, true).Erase();
            transacao.Commit();
        }

        var registro = AreaStore.Ler(database);
        var restantes = registro.Items
            .Where(r => !(r.Identity.Id == area.Identity.Id && string.Equals(r.Handle, area.Handle, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (restantes.Count != registro.Items.Count) AreaStore.Save(database, restantes);

        editor.WriteMessage(gemeas.Count > 0
            ? Tr.F("\nAREA {0} apagada: era a mesma linha de {1}, e as mesas ficam com ela. U desfaz.\n", area.Identity.DisplayName, string.Join(", ", gemeas.Select(g => g.Identity.DisplayName)))
            : Tr.F("\nAREA {0} apagada{1}. U desfaz.\n", area.Identity.DisplayName, hachuras > 0 ? ", " + Tr.T("com a hachura") : string.Empty));
    }

    private static bool Confirmar(Editor editor, AreaNoDesenho area, int mesas)
    {
        var pergunta = new PromptKeywordOptions(
            Tr.F("\nA área \"{0}\" tem {1} mesa(s): apagar a área apaga as mesas, os pilares e as strings dela também. Apagar?", area.Identity.DisplayName, mesas) + " [Sim/Não] <Não>",
            "Sim Não")
        {
            AllowNone = true,
        };

        var resposta = editor.GetKeywords(pergunta);
        return resposta.Status == PromptStatus.OK && resposta.StringResult == "Sim";
    }

    /// <summary>As mesas com o centro dentro da área (a regra da janela Áreas e do Recontar por área).</summary>
    internal static List<Guid> MesasComOCentroDentro(Database database, IReadOnlyList<Point3> area)
    {
        var mesas = new List<Guid>();
        if (area.Count < 3) return mesas;

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
        {
            if (mesa.Identity is null || mesa.Contour is not { } contorno) continue;
            if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var cantos = FileiraCommands.Vertices(polilinha, transacao);
            if (cantos.Count == 0) continue;

            if (Polygons.Contains(area, cantos.Average(p => p.X), cantos.Average(p => p.Y))) mesas.Add(mesa.Identity.Id);
        }

        return mesas;
    }
}
