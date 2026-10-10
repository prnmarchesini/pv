using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.GrupoCommands))]

namespace Clivus.Plugin;

/// <summary>Um grupo com a sua contagem, como o painel e a lista mostram.</summary>
internal sealed record GroupSummary(TableGroup Group, LayoutCensus Census, int MissingTables)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"Bloco A: 12 mesa(s), 336 módulo(s), 84 pilar(es), 241,9 kWp".</summary>
    public string Describe() =>
        Tr.F("{0}: {1} mesa(s), {2} módulo(s), {3} pilar(es), {4:0.#} kWp", Group.Name, Census.Tables, Census.Modules, Census.Pillars, Census.PowerKwp)
        + (Census.Dirty > 0 ? Tr.F(", {0} pendente(s)", Census.Dirty) : string.Empty)
        + (Census.Duplicated > 0 ? Tr.F(", {0} duplicada(s)", Census.Duplicated) : string.Empty)
        + (Census.TablesWithoutPower > 0 ? Tr.F(", {0} sem potência gravada", Census.TablesWithoutPower) : string.Empty)
        + (MissingTables > 0 ? Tr.F(", {0} que não está(ão) mais no desenho", MissingTables) : string.Empty);
}

/// <summary>
/// Os grupos de mesas (7.9): criar a partir da seleção, listar com a
/// contagem, recalcular por grupo, selecionar e apagar. O painel
/// (<see cref="PainelDeGrupos"/>) usa estas mesmas funções.
/// </summary>
public static class GrupoCommands
{
    /// <summary>CLIVUS_GRUPO_CRIAR: a seleção vira um grupo com o nome pedido.</summary>
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
                selecao = SelecionarModulos(documento);
                if (selecao.Status != PromptStatus.OK) return;
            }

            var mesas = SelecaoCommands.MesasTocadas(documento, selecao.Value.GetObjectIds());

            if (mesas.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nGRUPO Nenhuma mesa do plugin na seleção.\n"));
                return;
            }

            var nome = Perguntas.Nome(editor, Tr.T("grupo"), Tr.T("O grupo"));
            if (nome is null) return;

            var anterior = GroupStore.Find(documento.Database, nome);

            if (anterior is not null && !ConfirmarSubstituir(editor, anterior))
            {
                editor.WriteMessage(Tr.F("\nGRUPO \"{0}\" mantido como estava.\n", anterior.Name));
                return;
            }

            var grupo = new TableGroup(anterior?.Id ?? Guid.NewGuid(), nome, mesas.OrderBy(g => g).ToList(), DateTime.UtcNow);

            // Grupo que não se lê de volta não é gravado (04/10/2026: um grupo
            // gravado "sumia" ao reabrir, com "diz ter 1 item e só 0 foram lidos").
            if (!grupo.IsValid)
            {
                RegistroDeDiagnostico.Registrar($"Grupo inválido recusado: {string.Join(" | ", grupo.ToFields())}.");
                editor.WriteMessage(Tr.T("\nGRUPO Não gravei: a seleção não forma um grupo válido (mesa sem identidade). Rode Validar e tente de novo.\n"));
                return;
            }
            var problema = GroupStore.Upsert(documento.Database, grupo, out var substituiu);
            grupo = GroupStore.Find(documento.Database, nome) ?? grupo;

            // A marca no desenho: contorno, hachura e número (apaga a antiga
            // ao substituir).
            if (anterior is not null) GroupDrawer.Apagar(documento.Database, anterior.Id);
            var marca = GroupDrawer.Desenhar(documento.Database, grupo, CantosDasMesas(documento, grupo));

            editor.WriteMessage(
                (substituiu
                    ? Tr.F("\nGRUPO {0} \"{1}\" substituído com {2} mesa(s). {3}", grupo.Number, nome, mesas.Count, Resumir(documento, grupo).Describe())
                    : Tr.F("\nGRUPO {0} \"{1}\" criado com {2} mesa(s). {3}", grupo.Number, nome, mesas.Count, Resumir(documento, grupo).Describe()))
                + (marca > 0 ? Tr.F(" Marca desenhada na camada {0}.", LayoutLayers.Grupo) : string.Empty) + "\n");

            if (problema is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problema));

            AtualizarPainel();
            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar o grupo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui criar o grupo: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// A seleção do grupo (02/10/2026: "conforme vou selecionando mostre um
    /// texto na tela, de bom tamanho, mostrando a potência em kWp; e não
    /// quero que selecione tudo, a ferramenta tem que pegar SOMENTE os
    /// módulos"). O filtro só aceita os blocos de módulo do plugin; o placar
    /// mostra mesas, módulos e kWp das mesas tocadas a cada clique ou janela.
    /// </summary>
    private static PromptSelectionResult SelecionarModulos(Document documento)
    {
        var editor = documento.Editor;
        var escolhidos = new HashSet<ObjectId>();
        CaixaDeSelecao? placar = null;

        void Atualizar()
        {
            try
            {
                var mesas = SelecaoCommands.MesasTocadas(documento, escolhidos);
                var resumo = SelecaoCommands.Resumir(documento, mesas);

                placar ??= NovoPlacar();
                placar.TextoLivre = mesas.Count == 0 ? Tr.T("Grupo: nada selecionado") : Tr.F("Grupo: {0}", resumo.Describe());
                if (!placar.IsVisible) placar.Show();
            }
            catch (System.Exception erro)
            {
                // Evento do editor: exceção solta aqui derrubaria o Civil 3D.
                RegistroDeDiagnostico.Registrar("Falha no placar do grupo.", erro);
            }
        }

        void Somou(object? _, SelectionAddedEventArgs e)
        {
            foreach (ObjectId id in e.AddedObjects.GetObjectIds()) escolhidos.Add(id);
            Atualizar();
        }

        void Tirou(object? _, SelectionRemovedEventArgs e)
        {
            foreach (ObjectId id in e.RemovedObjects.GetObjectIds()) escolhidos.Remove(id);
            Atualizar();
        }

        var filtro = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "INSERT"),
            new TypedValue((int)DxfCode.BlockName, "CLIVUS_MODULO_*"),
        ]);

        editor.SelectionAdded += Somou;
        editor.SelectionRemoved += Tirou;

        // 04/10/2026: "quando eu seleciono e passo o mouse, as mesas vão
        // ficando brancas, tipo deselecionando". A pré-visualização da seleção
        // destaca a face de cima do módulo (que o filtro não aceita) e, ao
        // sair, redesenha a face por cima do destaque do bloco já escolhido.
        // Desligada só durante esta seleção; o valor do usuário volta no fim.
        object? previaAntes = null;
        try
        {
            previaAntes = AcadApp.GetSystemVariable("SELECTIONPREVIEW");
            AcadApp.SetSystemVariable("SELECTIONPREVIEW", (short)0);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui desligar a prévia da seleção.", erro);
            previaAntes = null;
        }

        try
        {
            return editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nSelecione os módulos do grupo (só módulos entram): ") }, filtro);
        }
        finally
        {
            editor.SelectionAdded -= Somou;
            editor.SelectionRemoved -= Tirou;
            placar?.Close();

            if (previaAntes is not null)
            {
                try
                {
                    AcadApp.SetSystemVariable("SELECTIONPREVIEW", previaAntes);
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Não consegui devolver a prévia da seleção.", erro);
                }
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static CaixaDeSelecao NovoPlacar()
    {
        var caixa = new CaixaDeSelecao(tamanhoDaLetra: 26, largura: 560);

        try
        {
            var janela = AcadApp.MainWindow.DeviceIndependentLocation;
            caixa.Left = janela.X + 60;
            caixa.Top = janela.Y + 240;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui posicionar o placar do grupo.", erro);
        }

        return caixa;
    }

    /// <summary>CLIVUS_GRUPOS: lista os grupos com a contagem de cada um.</summary>
    [CommandMethod(PluginInfo.ComandoGrupos, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Listar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resumos = Resumir(documento);

            editor.WriteMessage(Tr.F("\nGRUPOS {0} grupo(s).\n", resumos.Count));

            foreach (var resumo in resumos) editor.WriteMessage($"  {resumo.Describe()}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar os grupos.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui listar os grupos: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_GRUPO_RECALCULAR: recalcula as mesas de um grupo, pelo nome.</summary>
    [CommandMethod(PluginInfo.ComandoGrupoRecalcular)]
    public static void Recalcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, Tr.T("recalcular"));
            if (nome is null) return;

            RecalcularGrupo(documento, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular o grupo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui recalcular o grupo: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_GRUPO_SELECIONAR: põe as mesas de um grupo na seleção.</summary>
    [CommandMethod(PluginInfo.ComandoGrupoSelecionar, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Selecionar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, Tr.T("selecionar"));
            if (nome is null) return;

            SelecionarGrupo(documento, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar o grupo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui selecionar o grupo: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_GRUPO_APAGAR: apaga um grupo (só o registro; as mesas ficam).</summary>
    [CommandMethod(PluginInfo.ComandoGrupoApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var nome = PerguntarNome(editor, documento, Tr.T("apagar"));
            if (nome is null) return;

            var grupo = GroupStore.Find(documento.Database, nome);

            if (grupo is not null && GroupStore.Remove(documento.Database, grupo.Name))
            {
                var marca = GroupDrawer.Apagar(documento.Database, grupo.Id);
                editor.WriteMessage(Tr.F("\nGRUPO \"{0}\" apagado, com {1} entidade(s) da marca. As mesas continuam no desenho.\n", grupo.Name, marca));
            }
            else
            {
                editor.WriteMessage(Tr.F("\nGRUPO Não há grupo \"{0}\".\n", nome));
            }

            AtualizarPainel();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar o grupo.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar o grupo: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_GRUPOS_PAINEL: abre o painel dos grupos.</summary>
    [CommandMethod(PluginInfo.ComandoGruposPainel, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Painel()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nO painel dos grupos precisa da interface do Civil 3D. Use CLIVUS_GRUPOS.\n"));
                return;
            }

            MostrarPainel();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir o painel dos grupos.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir o painel: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Só com interface, e por um método que não é embutido: tocar em
    /// <see cref="PainelDeGrupos"/> carrega o tipo da paleta do AutoCAD, que
    /// derruba o Core Console.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void AtualizarPainel()
    {
        if (ClivusExtension.TemInterface()) PainelDeGrupos.Atualizar();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void MostrarPainel() => PainelDeGrupos.Mostrar();

    /// <summary>"Já existe o grupo X com N mesa(s). Substituir? [Sim/Não]"; o Enter mantém.</summary>
    private static bool ConfirmarSubstituir(Editor editor, TableGroup existente)
    {
        var pergunta = new PromptKeywordOptions(
            Tr.F("\nJá existe o grupo \"{0}\" com {1} mesa(s). Substituir pela seleção?", existente.Name, existente.Tables.Count) + " [Sim/Não]", "Sim Não")
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
        var simulada = FonteDoModulo.Simulada(documento.Database)?.Watts;

        return grupos.Select(g => Resumir(transacao, mesas, g, reserva, simulada)).ToList();
    }

    internal static GroupSummary Resumir(Document documento, TableGroup grupo)
    {
        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var mesas = LayoutScan.Tables(transacao, documento.Database);
        var reserva = Reserva(documento, mesas);

        return Resumir(transacao, mesas, grupo, reserva, FonteDoModulo.Simulada(documento.Database)?.Watts);
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

    private static GroupSummary Resumir(Transaction transacao, IReadOnlyDictionary<Guid, TableParts> mesas, TableGroup grupo, double reserva, double? simulada)
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

        return new GroupSummary(grupo, LayoutCensus.Count(contadas, reserva, simulada), faltando);
    }

    /// <summary>Os cantos dos contornos das mesas do grupo (para a casca da marca).</summary>
    internal static List<Point3> CantosDasMesas(Document documento, TableGroup grupo)
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
            editor.WriteMessage(Tr.F("\nGRUPO Não há grupo \"{0}\".\n", nome));
            return;
        }

        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return;

        editor.WriteMessage(Tr.F("\nGRUPO \"{0}\": recalculando {1} mesa(s)...\n", grupo.Name, grupo.Tables.Count));
        RecalcularCommands.RecalcularMesas(editor, documento, terreno, grupo.Tables, FileiraCommands.PerfilDaMesa(editor));
        AtualizarPainel();
    }

    internal static void SelecionarGrupo(Document documento, string nome)
    {
        var editor = documento.Editor;
        var grupo = GroupStore.Find(documento.Database, nome);

        if (grupo is null)
        {
            editor.WriteMessage(Tr.F("\nGRUPO Não há grupo \"{0}\".\n", nome));
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
        editor.WriteMessage(Tr.F("\nGRUPO \"{0}\": {1} entidade(s) selecionada(s).\n", grupo.Name, ids.Count));
    }

    private static string? PerguntarNome(Editor editor, Document documento, string paraQue)
    {
        var grupos = GroupStore.Load(documento.Database);

        if (grupos.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nGRUPO Nenhum grupo neste desenho. Selecione mesas e use Criar grupo.\n"));
            return null;
        }

        editor.WriteMessage(Tr.F("\nGrupos: {0}\n", string.Join(", ", grupos.Select(g => g.Name))));

        var resposta = editor.GetString(new PromptStringOptions(Tr.F("\nNome do grupo a {0}: ", paraQue)) { AllowSpaces = true });

        if (resposta.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(resposta.StringResult)) return null;

        // O painel manda o nome cru; se alguém digitar entre aspas, tira.
        var nome = resposta.StringResult.Trim();
        if (nome.Length >= 2 && nome[0] == '"' && nome[^1] == '"') nome = nome[1..^1].Trim();

        return nome.Length == 0 ? null : nome;
    }
}
