using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.ValidarCommands))]

namespace UFV.Plugin;

/// <summary>
/// A validação (7.7): confere se o registrado existe (áreas, alinhamentos),
/// se algo mudou de posição (mesas sujas), identidade duplicada (mesas e
/// peças), mesas só com peças, removidas não recontadas e o carimbo da
/// superfície. Diz o que achou e o que fazer. Pelo botão Validar, e ao
/// abrir um desenho que tem coisas nossas (<see cref="ValidacaoAoAbrir"/>).
/// </summary>
public static class ValidarCommands
{
    [CommandMethod(PluginInfo.ComandoValidar, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Validar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            Relatar(documento, "VALIDAR");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao validar o desenho.", erro);
            documento.Editor.WriteMessage($"\nNão consegui validar: {erro.Message}\n");
        }
    }

    /// <summary>Valida e escreve na linha de comando, com o rótulo dado ("VALIDAR", "AO ABRIR").</summary>
    internal static LayoutValidation Relatar(Document documento, string rotulo)
    {
        var validacao = Conferir(documento);
        var editor = documento.Editor;

        editor.WriteMessage(validacao.IsClean
            ? $"\n{rotulo} {validacao.Lines()[0]}\n"
            : $"\n{rotulo} {validacao.Count} achado(s):\n");

        if (!validacao.IsClean)
        {
            foreach (var linha in validacao.Lines()) editor.WriteMessage($"  - {linha}\n");
        }

        editor.WriteMessage(
            $"  VALIDAR_TOTAIS areas={validacao.MissingAreas.Count} alinhamentos={validacao.MissingAlignments.Count} "
            + $"sujas={validacao.DirtyTables.Count} movidas={validacao.MovedTables.Count} "
            + $"areasdup={validacao.DuplicatedAreas} alinhdup={validacao.DuplicatedAlignments} "
            + $"duplicadas={validacao.DuplicatedTables.Count} pecas={validacao.DuplicatedPieces} "
            + $"orfas={validacao.Orphans} removidas={validacao.PendingRemovals.Count} terreno={(validacao.TerrainWarning is null ? "ok" : "aviso")}\n");

        return validacao;
    }

    /// <summary>Só confere; não muda nada no desenho.</summary>
    internal static LayoutValidation Conferir(Document documento)
    {
        var database = documento.Database;

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var areasFaltando = AreaStore.Load(database)
            .Where(a => !EntidadeComIdentidade(transacao, database, a.Handle, e => AreaXData.Load(e)?.Id == a.Identity.Id))
            .Select(a => a.Identity.DisplayName)
            .ToList();

        var alinhamentosFaltando = AlignmentStore.Load(database)
            .Where(a => !EntidadeComIdentidade(transacao, database, a.Handle, e => AlignmentXData.Load(e)?.Id == a.Identity.Id))
            .Select(a => a.Identity.Name)
            .ToList();

        // Identidade de área ou alinhamento em mais de uma polilinha: a cópia
        // da polilinha leva o XData junto, e o registro (um handle por GUID)
        // não enxerga a segunda.
        var areasDuplicadas = AreaListCommands.Varrer(database).GroupBy(a => a.Identity.Id).Count(g => g.Count() > 1);
        var alinhamentosDuplicados = AlignmentScan.Varrer(database).GroupBy(a => a.Identity.Id).Count(g => g.Count() > 1);

        var mesas = LayoutScan.Tables(transacao, database).Values.ToList();

        var sujas = mesas.Where(m => m.Identity is { Dirty: true }).Select(m => m.Identity!.Label).OrderBy(l => l, StringComparer.Ordinal).ToList();
        var movidas = mesas.Where(m => m.Identity is { Dirty: false, Anchor: not null } && !m.IsDuplicated && Moveu(transacao, m))
            .Select(m => m.Identity!.Label).OrderBy(l => l, StringComparer.Ordinal).ToList();
        var duplicadas = mesas.Where(m => m.IsDuplicated).Select(m => m.Identity?.Label ?? "(sem letreiro)").ToList();
        var orfas = mesas.Count(m => m.Identity is null);

        var pecasRepetidas = PecasRepetidas(transacao, mesas);

        var removidas = RemovalStore.Ler(database).Items.Select(r => r.Label).ToList();

        var terreno = TerrenoEnvelhecido.Conferir(documento);

        return new LayoutValidation(
            areasFaltando, alinhamentosFaltando, sujas, movidas, areasDuplicadas, alinhamentosDuplicados,
            duplicadas, pecasRepetidas, orfas, removidas, terreno);
    }

    /// <summary>Tolerância para "a mesa continua onde foi desenhada", em metro.</summary>
    private const double ToleranciaDaAncora = 1e-3;

    /// <summary>Se o primeiro vértice do contorno está longe da âncora gravada.</summary>
    private static bool Moveu(Transaction transacao, TableParts mesa)
    {
        if (mesa.Identity?.Anchor is not { } ancora || mesa.Contour is not { } contorno) return false;

        if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) return false;

        foreach (ObjectId v in polilinha)
        {
            var p = ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position;

            return Math.Abs(p.X - ancora.X) > ToleranciaDaAncora
                || Math.Abs(p.Y - ancora.Y) > ToleranciaDaAncora
                || Math.Abs(p.Z - ancora.Z) > ToleranciaDaAncora;
        }

        return false;
    }

    /// <summary>Se o handle registrado ainda aponta para uma entidade viva com a identidade esperada.</summary>
    private static bool EntidadeComIdentidade(Transaction transacao, Database database, string handle, Func<Entity, bool> confere)
    {
        var id = TerrenoEnvelhecido.AcharPorHandle(database, handle);
        if (id is null) return false;

        try
        {
            return transacao.GetObject(id.Value, OpenMode.ForRead) is Entity entidade && confere(entidade);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Não consegui abrir a entidade registrada {handle}.", erro);
            return false;
        }
    }

    /// <summary>Quantos GUIDs de peça (pilar, módulo, face, nota) aparecem mais de uma vez.</summary>
    private static int PecasRepetidas(Transaction transacao, IEnumerable<TableParts> mesas)
    {
        var vistos = new HashSet<Guid>();
        var repetidas = 0;

        foreach (var mesa in mesas)
        {
            foreach (var id in mesa.Pillars.Concat(mesa.Modules).Concat(mesa.Faces).Concat(mesa.Notes))
            {
                var entidade = (Entity)transacao.GetObject(id, OpenMode.ForRead);

                if (CopyFixer.PieceId(entidade) is { } g && !vistos.Add(g)) repetidas++;
            }
        }

        return repetidas;
    }
}

/// <summary>
/// Valida ao abrir um desenho que tem coisas nossas (área, alinhamento,
/// registro de removidas ou carimbo de terreno), e diz na linha de
/// comando. Um desenho sem nada nosso não recebe uma linha sequer. Os
/// desenhos já abertos quando o plugin carrega (NETLOAD, Core Console com
/// /i) são validados na hora.
/// </summary>
internal static class ValidacaoAoAbrir
{
    private static DocumentCollectionEventHandler? _aoAbrir;

    internal static void Instalar()
    {
        if (_aoAbrir is not null) return;

        _aoAbrir = (_, e) => Validar(e.Document);
        AcadApp.DocumentManager.DocumentCreated += _aoAbrir;

        foreach (Document documento in AcadApp.DocumentManager) Validar(documento);
    }

    internal static void Desinstalar()
    {
        if (_aoAbrir is null) return;

        try { AcadApp.DocumentManager.DocumentCreated -= _aoAbrir; }
        catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao desligar a validação ao abrir.", erro); }

        _aoAbrir = null;
    }

    private static void Validar(Document? documento)
    {
        if (documento is null) return;

        try
        {
            var database = documento.Database;

            if (AreaStore.Load(database).Count == 0 && AlignmentStore.Load(database).Count == 0
                && !PluginDictionary.Contains(database, "REMOVIDAS") && !PluginDictionary.Contains(database, "TERRENO"))
                return;

            var validacao = ValidarCommands.Relatar(documento, "AO ABRIR");

            if (!validacao.IsClean)
                documento.Editor.WriteMessage($"  ({PluginInfo.ComandoValidar} repete esta conferência a qualquer hora.)\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao validar ao abrir o desenho.", erro);
        }
    }
}
