using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.GrupoCommands))]

namespace UFV.Plugin;

/// <summary>Um grupo com a sua contagem, como o painel e a lista mostram.</summary>
internal sealed record GroupSummary(TableGroup Group, LayoutCensus Census, int MissingTables)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"Bloco A: 12 mesa(s), 336 módulo(s), 84 pilar(es), 241,9 kWp".</summary>
    public string Describe() =>
        $"{Group.Name}: {Census.Tables} mesa(s), {Census.Modules} módulo(s), {Census.Pillars} pilar(es), {Census.PowerKwp.ToString("0.#", Brasil)} kWp"
        + (Census.Dirty > 0 ? $", {Census.Dirty} suja(s)" : string.Empty)
        + (Census.Duplicated > 0 ? $", {Census.Duplicated} duplicada(s)" : string.Empty)
        + (Census.TablesWithoutPower > 0 ? $", {Census.TablesWithoutPower} sem potência gravada" : string.Empty)
        + (MissingTables > 0 ? $", {MissingTables} que não está(ão) mais no desenho" : string.Empty);
}

/// <summary>
/// Os grupos de mesas (7.9): criar a partir da seleção, listar com a
/// contagem, recalcular por grupo, selecionar e apagar. O painel
/// (<see cref="PainelDeGrupos"/>) usa estas mesmas funções.
/// </summary>
public static class GrupoCommands
{
    /// <summary>UFV_GRUPO_CRIAR: a seleção vira um grupo com o nome pedido.</summary>
    [CommandMethod(PluginInfo.ComandoGrupoCriar, CommandFlags.UsePickSet)]
    public static void Criar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var selecao = editor.SelectImplied();

            if (selecao.Status != PromptStatus.OK)
            {
                selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nSelecione as mesas do grupo: " });
                if (selecao.Status != PromptStatus.OK) return;
            }

            var mesas = SelecaoCommands.MesasTocadas(documento, selecao.Value.GetObjectIds());

            if (mesas.Count == 0)
            {
                editor.WriteMessage("\nGRUPO Nenhuma mesa do plugin na seleção.\n");
                return;
            }

            var nome = Perguntas.Nome(editor, "grupo", "O grupo");
            if (nome is null) return;

            var anterior = GroupStore.Find(documento.Database, nome);

            if (anterior is not null && !ConfirmarSubstituir(editor, anterior))
            {
                editor.WriteMessage($"\nGRUPO \"{anterior.Name}\" mantido como estava.\n");
                return;
            }

            var grupo = new TableGroup(anterior?.Id ?? Guid.NewGuid(), nome, mesas.OrderBy(g => g).ToList(), DateTime.UtcNow);
            var problema = GroupStore.Upsert(documento.Database, grupo, out var substituiu);
            grupo = GroupStore.Find(documento.Database, nome) ?? grupo;

            // A marca no desenho: contorno, hachura e número (apaga a antiga
            // ao substituir).
            if (anterior is not null) GroupDrawer.Apagar(documento.Database, anterior.Id);
            var marca = GroupDrawer.Desenhar(documento.Database, grupo, CantosDasMesas(documento, grupo));

            editor.WriteMessage(
                $"\nGRUPO {grupo.Number} \"{nome}\" {(substituiu ? "substituído" : "criado")} com {mesas.Count} mesa(s). {Resumir(documento, grupo).Describe()}"
                + (marca > 0 ? $" Marca desenhada na camada {LayoutLayers.Grupo}." : string.Empty) + "\n");

            if (problema is not null) editor.WriteMessage($"  ATENÇÃO: {problema}.\n");

            AtualizarPainel();
            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar o grupo.", erro);
            editor.WriteMessage($"\nNão consegui criar o grupo: {erro.Message}\n");
        }
    }

    /// <summary>UFV_GRUPOS: lista os grupos com a contagem de cada um.</summary>
    [CommandMethod(PluginInfo.ComandoGrupos, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Listar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resumos = Resumir(documento);

            editor.WriteMessage($"\nGRUPOS {resumos.Count} grupo(s).\n");

            foreach (var resumo in resumos) editor.WriteMessage($"  {resumo.Describe()}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar os grupos.", erro);
            editor.WriteMessage($"\nNão consegui listar os grupos: {erro.Message}\n");
        }
    }

    /// <summary>UFV_GRUPO_RECALCULAR: recalcula as mesas de um grupo, pelo nome.</summary>
    [CommandMethod(PluginInfo.ComandoGrupoRecalcular)]
    public static void Recalcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, "recalcular");
            if (nome is null) return;

            RecalcularGrupo(documento, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular o grupo.", erro);
            editor.WriteMessage($"\nNão consegui recalcular o grupo: {erro.Message}\n");
        }
    }

    /// <summary>UFV_GRUPO_SELECIONAR: põe as mesas de um grupo na seleção.</summary>
    [CommandMethod(PluginInfo.ComandoGrupoSelecionar, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Selecionar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, "selecionar");
            if (nome is null) return;

            SelecionarGrupo(documento, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar o grupo.", erro);
            editor.WriteMessage($"\nNão consegui selecionar o grupo: {erro.Message}\n");
        }
    }

    /// <summary>UFV_GRUPO_APAGAR: apaga um grupo (só o registro; as mesas ficam).</summary>
    [CommandMethod(PluginInfo.ComandoGrupoApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, "apagar");
            if (nome is null) return;

            var grupo = GroupStore.Find(documento.Database, nome);

            if (grupo is not null && GroupStore.Remove(documento.Database, grupo.Name))
            {
                var marca = GroupDrawer.Apagar(documento.Database, grupo.Id);
                editor.WriteMessage($"\nGRUPO \"{grupo.Name}\" apagado, com {marca} entidade(s) da marca. As mesas continuam no desenho.\n");
            }
            else
            {
                editor.WriteMessage($"\nGRUPO Não há grupo \"{nome}\".\n");
            }

            AtualizarPainel();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar o grupo.", erro);
            editor.WriteMessage($"\nNão consegui apagar o grupo: {erro.Message}\n");
        }
    }

    /// <summary>UFV_GRUPOS_PAINEL: abre o painel dos grupos.</summary>
    [CommandMethod(PluginInfo.ComandoGruposPainel, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Painel()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!UfvExtension.TemInterface())
            {
                documento.Editor.WriteMessage("\nO painel dos grupos precisa da interface do Civil 3D. Use UFV_GRUPOS.\n");
                return;
            }

            MostrarPainel();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir o painel dos grupos.", erro);
            documento.Editor.WriteMessage($"\nNão consegui abrir o painel: {erro.Message}\n");
        }
    }

    /// <summary>
    /// Só com interface, e por um método que não é embutido: tocar em
    /// <see cref="PainelDeGrupos"/> carrega o tipo da paleta do AutoCAD, que
    /// derruba o Core Console.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AtualizarPainel()
    {
        if (UfvExtension.TemInterface()) PainelDeGrupos.Atualizar();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void MostrarPainel() => PainelDeGrupos.Mostrar();

    /// <summary>"Já existe o grupo X com N mesa(s). Substituir? [Sim/Não]"; o Enter mantém.</summary>
    private static bool ConfirmarSubstituir(Editor editor, TableGroup existente)
    {
        var pergunta = new PromptKeywordOptions(
            $"\nJá existe o grupo \"{existente.Name}\" com {existente.Tables.Count} mesa(s). Substituir pela seleção? [Sim/Não]", "Sim Não")
        {
            AllowNone = true,
        };

        var resposta = editor.GetKeywords(pergunta);

        return resposta.Status == PromptStatus.OK && resposta.StringResult == "Sim";
    }

    // ------------------------------------------------------------ o miolo, para os comandos e o painel

    internal static IReadOnlyList<GroupSummary> Resumir(Document documento)
    {
        var grupos = GroupStore.Load(documento.Database);
        if (grupos.Count == 0) return [];

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);
        var reserva = Reserva(documento, mesas);

        return grupos.Select(g => Resumir(transacao, mesas, g, reserva)).ToList();
    }

    internal static GroupSummary Resumir(Document documento, TableGroup grupo)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);
        var reserva = Reserva(documento, mesas);

        return Resumir(transacao, mesas, grupo, reserva);
    }

    /// <summary>
    /// A potência de módulo para mesa sem potência gravada: só lê o perfil do
    /// disco se alguma mesa precisar (o mesmo critério da caixa da seleção).
    /// </summary>
    private static double Reserva(Document documento, IReadOnlyDictionary<Guid, TableParts> mesas)
    {
        var identidades = mesas.Values.Select(m => m.Identity).ToList();

        return identidades.Any(i => i is { ModulePowerWatts: null })
            ? FileiraCommands.PerfilDaMesa(documento.Editor, silencioso: true).Layout.Module.PowerWatts
            : identidades.Select(i => i?.ModulePowerWatts).FirstOrDefault(p => p is > 0) ?? 1;
    }

    private static GroupSummary Resumir(Transaction transacao, IReadOnlyDictionary<Guid, TableParts> mesas, TableGroup grupo, double reserva)
    {
        var contadas = new List<CountedTable>();
        var faltando = 0;

        foreach (var guid in grupo.Tables)
        {
            if (!mesas.TryGetValue(guid, out var partes) || partes.Identity is null)
            {
                faltando++;
                continue;
            }

            contadas.Add(RecontarCommands.Contar(transacao, partes));
        }

        return new GroupSummary(grupo, LayoutCensus.Count(contadas, reserva), faltando);
    }

    /// <summary>Os cantos dos contornos das mesas do grupo (para a casca da marca).</summary>
    private static List<Point3> CantosDasMesas(Document documento, TableGroup grupo)
    {
        var cantos = new List<Point3>();

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);

        foreach (var guid in grupo.Tables)
        {
            if (!mesas.TryGetValue(guid, out var partes)) continue;

            foreach (var id in partes.Contours)
                cantos.AddRange(FileiraCommands.Vertices((Polyline3d)transacao.GetObject(id, OpenMode.ForRead), transacao));
        }

        return cantos;
    }

    internal static void RecalcularGrupo(Document documento, string nome)
    {
        var editor = documento.Editor;
        var grupo = GroupStore.Find(documento.Database, nome);

        if (grupo is null)
        {
            editor.WriteMessage($"\nGRUPO Não há grupo \"{nome}\".\n");
            return;
        }

        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return;

        editor.WriteMessage($"\nGRUPO \"{grupo.Name}\": recalculando {grupo.Tables.Count} mesa(s)...\n");
        RecalcularCommands.RecalcularMesas(editor, documento, terreno, grupo.Tables, FileiraCommands.PerfilDaMesa(editor));
        AtualizarPainel();
    }

    internal static void SelecionarGrupo(Document documento, string nome)
    {
        var editor = documento.Editor;
        var grupo = GroupStore.Find(documento.Database, nome);

        if (grupo is null)
        {
            editor.WriteMessage($"\nGRUPO Não há grupo \"{nome}\".\n");
            return;
        }

        var ids = new List<ObjectId>();

        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var mesas = LayoutScan.Tables(transacao, documento.Database);

            foreach (var guid in grupo.Tables)
            {
                if (mesas.TryGetValue(guid, out var partes)) ids.AddRange(partes.All);
            }
        }

        editor.SetImpliedSelection([.. ids]);
        editor.WriteMessage($"\nGRUPO \"{grupo.Name}\": {ids.Count} entidade(s) selecionada(s).\n");
    }

    private static string? PerguntarNome(Editor editor, Document documento, string paraQue)
    {
        var grupos = GroupStore.Load(documento.Database);

        if (grupos.Count == 0)
        {
            editor.WriteMessage("\nGRUPO Nenhum grupo neste desenho. Selecione mesas e use Criar grupo.\n");
            return null;
        }

        editor.WriteMessage($"\nGrupos: {string.Join(", ", grupos.Select(g => g.Name))}\n");

        var resposta = editor.GetString(new PromptStringOptions($"\nNome do grupo a {paraQue}: ") { AllowSpaces = true });

        if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return null;

        // O painel manda o nome cru; se alguém digitar entre aspas, tira.
        var nome = resposta.StringResult.Trim();
        if (nome.Length >= 2 && nome[0] == '"' && nome[^1] == '"') nome = nome[1..^1].Trim();

        return nome.Length == 0 ? null : nome;
    }
}
