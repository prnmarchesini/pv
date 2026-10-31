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

    /// <summary>A janela Áreas já perguntou o "todas" da parte de baixo: o comando não pergunta de novo.</summary>
    internal static bool TodasConfirmadasPelaJanela;

    /// <summary>
    /// CLIVUS_AREAS_LIMPAR: as mesas de todas as áreas (cada área as dela);
    /// as áreas e os alinhamentos ficam (31/10/2026, Renan: "Limpar todas
    /// áreas, apagar todas áreas, regerar todas áreas").
    /// </summary>
    [CommandMethod(PluginInfo.ComandoAreasLimpar)]
    public static void LimparTodas() => Todas(Tr.T("Limpar as mesas de todas as áreas"), (editor, documento, areas) =>
    {
        var mesas = 0;
        foreach (var area in areas)
        {
            var dela = MesasDaArea(documento.Database, AreasDoDesenho.Varrer(documento.Database), area);
            if (dela.Count == 0) continue;

            ApagarTudoCommands.Executar(editor, documento, (area.Contorno, area.Identity.DisplayName), dela.ToHashSet());
            mesas += dela.Count;
        }

        editor.WriteMessage(Tr.F("\nAREAS {0} mesa(s) apagada(s) em {1} área(s); as áreas ficam. U desfaz.\n", mesas, areas.Count));
    });

    /// <summary>
    /// CLIVUS_AREAS_REGERAR: regera todas as áreas, da maior para a menor (a de
    /// dentro fica com o desenho dela): a que segue os objetos do cliente, por
    /// eles; as outras, pelo alinhamento (perguntado uma vez só, se o desenho
    /// tem mais de um).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoAreasRegerar)]
    public static void RegerarTodas() => Todas(Tr.T("Regerar todas as áreas"), (editor, documento, areas) =>
    {
        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return;

        var perfil = FileiraCommands.PerfilDaMesa(editor);
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? alinhamento = null;
        var perguntou = false;
        var regeradas = 0;

        foreach (var area in areas.OrderByDescending(a => InverterSites.PlanArea(a.Contorno)))
        {
            if (CampoCommands.RefazerSeSegueObjetos(editor, documento, terreno, area.Contorno, () => perfil) is not null)
            {
                regeradas++;
                continue;
            }

            if (!perguntou)
            {
                alinhamento = FileiraCommands.EscolherAlinhamento(editor, documento);
                perguntou = true;
            }

            if (alinhamento is null)
            {
                editor.WriteMessage(Tr.F("\nAREAS {0} ficou como estava: sem alinhamento para regerar.\n", area.Identity.DisplayName));
                continue;
            }

            RefazerCommands.Executar(editor, documento, terreno, (area.Contorno, area.Identity.DisplayName), alinhamento.Value, perfil);
            regeradas++;
        }

        editor.WriteMessage(Tr.F("\nAREAS {0} de {1} área(s) regerada(s). U desfaz.\n", regeradas, areas.Count));
    });

    /// <summary>CLIVUS_AREAS_APAGAR: todas as áreas, cada uma com as mesas dela.</summary>
    [CommandMethod(PluginInfo.ComandoAreasApagar)]
    public static void ApagarTodas() => Todas(Tr.T("Apagar todas as áreas e as mesas delas"), (editor, documento, areas) =>
    {
        foreach (var area in areas.OrderBy(a => InverterSites.PlanArea(a.Contorno)))
            ApagarArea(editor, documento, area, confirmada: true);
    });

    /// <summary>
    /// O "todas" da janela e da linha de comando: as áreas do desenho (as
    /// repetidas uma vez só), a pergunta (a janela já perguntou:
    /// <see cref="TodasConfirmadasPelaJanela"/>) e a ação.
    /// </summary>
    private static void Todas(string oQue, Action<Editor, Document, List<AreaNoDesenho>> acao)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var confirmadas = TodasConfirmadasPelaJanela;
        TodasConfirmadasPelaJanela = false;

        try
        {
            var areas = EscolhaDeArea.Distintas(AreasDoDesenho.Varrer(documento.Database));
            if (areas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nAREAS Nenhuma área neste desenho.\n"));
                return;
            }

            if (!confirmadas)
            {
                var pergunta = new PromptKeywordOptions(Tr.F("\n{0} ({1} área(s))?", oQue, areas.Count) + " [Sim/Não] <Não>", "Sim Não") { AllowNone = true };
                var resposta = editor.GetKeywords(pergunta);
                if (resposta.Status != PromptStatus.OK || resposta.StringResult != "Sim")
                {
                    editor.WriteMessage(Tr.T("\nNada foi feito.\n"));
                    return;
                }
            }

            acao(editor, documento, areas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Falha em: {oQue}.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui: {0}\n", erro.Message));
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

            // As mesas DA área (a regra da janela: o centro dentro, e dentro de
            // duas, a menor): a "Area 1" que repete a "area1" não recalcula as dela.
            var mesas = MesasDaArea(documento.Database, AreasDoDesenho.Varrer(documento.Database), area);

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
    /// As mesas DELA vão junto (a regra da janela Áreas: o centro dentro, e
    /// dentro de duas, a menor), depois de perguntar (a janela já perguntou:
    /// <paramref name="confirmada"/>). A área repetida (outra com o mesmo
    /// contorno) ou a que só cobre as de outra área menor sai sozinha: as
    /// mesas são da outra (31/10/2026: a "Area 1" do Aurora, quase igual à
    /// "area1", ia levar as 78 mesas dela).
    /// </summary>
    internal static void ApagarArea(Editor editor, Document documento, AreaNoDesenho area, bool confirmada)
    {
        var database = documento.Database;
        var todas = AreasDoDesenho.Varrer(database);
        var gemeas = EscolhaDeArea.Repetidas(todas, area).Where(a => a.Id != area.Id).ToList();

        if (gemeas.Count == 0)
        {
            var dela = MesasDaArea(database, todas, area);

            if (dela.Count > 0 && !confirmada && !Confirmar(editor, area, dela.Count))
            {
                editor.WriteMessage(Tr.T("\nNada foi apagado.\n"));
                return;
            }

            if (dela.Count > 0) ApagarTudoCommands.Executar(editor, documento, (area.Contorno, area.Identity.DisplayName), dela.ToHashSet());
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

    /// <summary>
    /// As mesas DA área cuja linha é este contorno (o Limpar mesas da janela e
    /// do botão direito), ou null se o contorno não é de uma área registrada
    /// (aí vale tudo o que está dentro, como sempre).
    /// </summary>
    internal static IReadOnlySet<Guid>? MesasDaAreaDoContorno(Database database, IReadOnlyList<Point3> contorno)
    {
        var todas = AreasDoDesenho.Varrer(database);

        static bool Igual(IReadOnlyList<Point3> a, IReadOnlyList<Point3> b) =>
            a.Count == b.Count && a.Zip(b).All(p => Math.Abs(p.First.X - p.Second.X) < 1e-6 && Math.Abs(p.First.Y - p.Second.Y) < 1e-6);

        return todas.FirstOrDefault(a => Igual(a.Contorno, contorno)) is { } area ? MesasDaArea(database, todas, area).ToHashSet() : null;
    }

    /// <summary>
    /// As mesas DA área: o centro dentro dela e, dentro de duas, a menor (a
    /// regra da janela Áreas, do resumo e do Recontar, <see cref="AreaSummary.AreaOf"/>).
    /// A área é a polilinha (o ObjectId): duas cópias com o mesmo GUID contam
    /// cada uma as suas.
    /// </summary>
    internal static List<Guid> MesasDaArea(Database database, IReadOnlyList<AreaNoDesenho> todas, AreaNoDesenho area)
    {
        var mesas = new List<Guid>();
        if (area.Contorno.Count < 3) return mesas;

        var entradas = todas.Select(a => new AreaInput(a.Identity.Id, a.Identity.DisplayName, a.Contorno, false)).ToList();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
        {
            if (mesa.Identity is null || mesa.Contour is not { } contorno) continue;
            if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var cantos = FileiraCommands.Vertices(polilinha, transacao);
            if (cantos.Count == 0) continue;

            if (AreaSummary.AreaOf(cantos, entradas) is { } i && todas[i].Id == area.Id) mesas.Add(mesa.Identity.Id);
        }

        return mesas;
    }

}
