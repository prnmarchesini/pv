using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AlocacaoDeStringsCommands))]

namespace Clivus.Plugin;

/// <summary>
/// As strings de cada inversor em campo (14.3 a 14.5): alocar pela seleção
/// (só strings; a de outro inversor fica travada), com a contagem ao vivo
/// numa caixa sobre o CAD. Alocar regrava só o XData da string (o campo
/// inversor); a geometria não muda. Chamado pelo botão da janela (que some
/// e volta) ou digitado.
/// </summary>
public static class AlocacaoDeStringsCommands
{
    /// <summary>CLIVUS_ELETRICA_ALOCAR: o inversor (nome ou GUID) e a seleção das strings.</summary>
    [CommandMethod(PluginInfo.ComandoEletricaAlocar)]
    public static void Alocar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarInversor(editor, documento.Database) is not { } escolha) return;
            var (setup, inversor) = escolha;
            var modelo = setup.FindModel(inversor.Model);

            var strings = StringsDoDesenho.Ler(documento.Database);
            if (strings.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nINVERSOR Não há strings no desenho: gere o traçado das strings primeiro.\n"));
                return;
            }

            if (Selecionar(documento, inversor, modelo, strings, Cadastrados(setup)) is not { } selecao) return;

            var escolhidas = selecao.Ids.Select(id => strings[id]).ToList();
            var plano = StringAllocation.Allocate(inversor.Id, escolhidas, Cadastrados(setup));
            StringsDoDesenho.Gravar(documento.Database, plano.Changed);

            var total = StringsDoDesenho.Ler(documento.Database).Values.Count(s => s.Inverter == inversor.Id);
            editor.WriteMessage(Tr.F("\nINVERSOR {0}: {1} string(s) alocada(s), {2} já eram dele, {3} recusada(s) por serem de outro inversor. Agora {4} de {5} entradas.\n",
                inversor.Name, plano.Changed.Count, plano.AlreadyHere, plano.Refused.Count + selecao.RecusadasAoVivo, total, modelo?.TotalInputs ?? 0));
            if (StringAllocation.ExcessWarning(inversor, modelo, total) is { } excesso) editor.WriteMessage($"  {excesso}\n");
            if (modelo is null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0} está sem modelo; não dá para saber o excesso de capacidade.\n", inversor.Name));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao alocar strings no inversor.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui alocar as strings: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento);
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_SELECIONAR (14.5, "selecionar todas"): as strings do
    /// inversor ficam selecionadas no CAD (seleção implícita), para ver onde
    /// estão ou agir sobre elas. Nada é gravado.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaSelecionar, CommandFlags.Modal | CommandFlags.Redraw | CommandFlags.NoUndoMarker)]
    public static void SelecionarTodas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarInversor(editor, documento.Database) is not { } escolha) return;

            var ids = StringsDoDesenho.Ler(documento.Database).Where(x => x.Value.Inverter == escolha.Inversor.Id).Select(x => x.Key).ToArray();
            editor.SetImpliedSelection(ids);
            editor.WriteMessage(ids.Length == 0
                ? Tr.F("\nINVERSOR {0} não tem string alocada.\n", escolha.Inversor.Name)
                : Tr.F("\nINVERSOR {0}: {1} string(s) selecionada(s).\n", escolha.Inversor.Name, ids.Length));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar as strings do inversor.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui selecionar as strings: {0}\n", erro.Message));
        }
    }

    /// <summary>Os inversores do cadastro (a string que aponta para outro, que sumiu, não trava).</summary>
    internal static IReadOnlySet<Guid> Cadastrados(ElectricalSetup setup) => setup.Inverters.Select(i => i.Id).ToHashSet();

    /// <summary>Pergunta o inversor (nome ou GUID); null e o recado se não há um só que responda.</summary>
    internal static (ElectricalSetup Setup, Inverter Inversor)? PerguntarInversor(Editor editor, Database database)
    {
        var qual = editor.GetString(new PromptStringOptions(Tr.T("\nInversor (nome): ")) { AllowSpaces = true });
        if (qual.Status != PromptStatus.OK) return null;

        var (setup, problema) = ConfiguracaoEletricaStore.Ler(database);
        if (problema is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", problema));

        if (setup.FindInverter(qual.StringResult) is { } inversor) return (setup, inversor);

        editor.WriteMessage(Tr.F("\nINVERSOR Não há inversor \"{0}\" no cadastro.\n", qual.StringResult.Trim()));
        return null;
    }

    /// <summary>
    /// A seleção em campo, só de strings (regra elétrica 4): o filtro pega
    /// polilinhas do plugin, e cada uma que entra é conferida: o que não é
    /// string sai, a string de outro inversor sai (travada) e é contada. A
    /// caixa sobre o CAD conta ao vivo. Shift+clique tira da seleção (o
    /// padrão do AutoCAD). As strings que já são deste inversor ficam
    /// destacadas durante a seleção. Null se o usuário desistiu; senão as
    /// strings escolhidas e quantas foram recusadas ao vivo (já fora da seleção).
    /// </summary>
    private static (List<ObjectId> Ids, int RecusadasAoVivo)? Selecionar(Document documento, Inverter inversor, InverterModel? modelo, IReadOnlyDictionary<ObjectId, ElectricalString> strings, IReadOnlySet<Guid> cadastrados)
    {
        var editor = documento.Editor;
        var escolhidas = new HashSet<ObjectId>();
        var recusadas = new HashSet<ObjectId>();
        var jaDele = strings.Where(x => x.Value.Inverter == inversor.Id).Select(x => x.Key).ToList();
        CaixaDeSelecao? placar = null;

        void Atualizar()
        {
            try
            {
                if (!ClivusExtension.TemInterface()) return;

                var novas = escolhidas.Count(id => strings[id].Inverter != inversor.Id);
                var total = jaDele.Count + novas;
                var entradas = modelo?.TotalInputs ?? 0;

                placar ??= NovoPlacar(620);
                placar.TextoLivre = Tr.F("{0}: {1} de {2} entradas ({3} já dele, {4} nova(s) na seleção)", inversor.Name, total, entradas, jaDele.Count, novas)
                    + (recusadas.Count > 0 ? "\n" + Tr.F("{0} recusada(s): de outro inversor", recusadas.Count) : string.Empty)
                    + (StringAllocation.Excess(total, modelo) is > 0 and var excesso ? "\n" + Tr.F("EXCESSO: {0} string(s) a mais que as entradas do modelo", excesso) : string.Empty)
                    + (modelo is null ? "\n" + Tr.T("Inversor sem modelo: sem como saber o excesso") : string.Empty);
                if (!placar.IsVisible) placar.Show();
            }
            catch (System.Exception erro)
            {
                // Evento do editor: exceção solta aqui derrubaria o Civil 3D.
                RegistroDeDiagnostico.Registrar("Falha no placar da alocação de strings.", erro);
            }
        }

        void Somou(object? _, SelectionAddedEventArgs e)
        {
            try
            {
                var ids = e.AddedObjects.GetObjectIds();
                for (var i = ids.Length - 1; i >= 0; i--)
                {
                    if (!strings.TryGetValue(ids[i], out var s))
                    {
                        e.Remove(i);
                    }
                    else if (StringAllocation.IsLockedFor(s, inversor.Id, cadastrados))
                    {
                        recusadas.Add(ids[i]);
                        e.Remove(i);
                    }
                    else
                    {
                        escolhidas.Add(ids[i]);
                    }
                }
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao conferir as strings da seleção.", erro);
            }

            Atualizar();
        }

        void Tirou(object? _, SelectionRemovedEventArgs e)
        {
            try
            {
                foreach (ObjectId id in e.RemovedObjects.GetObjectIds()) escolhidas.Remove(id);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao tirar strings da seleção.", erro);
            }

            Atualizar();
        }

        // Só polilinhas com XData do plugin; o resto (mesa, módulo, curva,
        // terreno) nem entra. O evento acima separa a string do alinhamento.
        var filtro = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "POLYLINE"),
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginInfo.PrefixoDeDados),
        ]);

        editor.SelectionAdded += Somou;
        editor.SelectionRemoved += Tirou;
        Destacar(documento.Database, jaDele, true);

        try
        {
            Atualizar();
            var opcoes = new PromptSelectionOptions { MessageForAdding = Tr.F("\nStrings do {0} (só strings; Shift+clique tira; Enter termina): ", inversor.Name) };
            var r = editor.GetSelection(opcoes, filtro);

            if (r.Status == PromptStatus.Cancel) return null;

            // A conferência vale também sem o evento (script, Core Console):
            // só string entra, e a de outro inversor é recusada pelo Core.
            return (r.Status == PromptStatus.OK ? r.Value.GetObjectIds().Where(strings.ContainsKey).ToList() : [], recusadas.Count);
        }
        finally
        {
            editor.SelectionAdded -= Somou;
            editor.SelectionRemoved -= Tirou;
            Destacar(documento.Database, jaDele, false);
            placar?.Close();
        }
    }

    /// <summary>Liga ou desliga o destaque do AutoCAD nas entidades (só tela; nada é gravado).</summary>
    private static void Destacar(Database database, IReadOnlyList<ObjectId> ids, bool ligar)
    {
        if (ids.Count == 0 || !ClivusExtension.TemInterface()) return;

        try
        {
            using var transacao = database.TransactionManager.StartOpenCloseTransaction();
            foreach (var id in ids)
            {
                if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                if (ligar) e.Highlight();
                else e.Unhighlight();
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui destacar as strings do inversor.", erro);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static CaixaDeSelecao NovoPlacar(double largura)
    {
        var caixa = new CaixaDeSelecao(tamanhoDaLetra: 22, largura: largura);

        try
        {
            var janela = AcadApp.MainWindow.DeviceIndependentLocation;
            caixa.Left = janela.X + 60;
            caixa.Top = janela.Y + 240;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui posicionar o placar da seleção elétrica.", erro);
        }

        return caixa;
    }
}

/// <summary>As strings desenhadas (o XData das polilinhas do traçado), lidas e gravadas em lote.</summary>
internal static class StringsDoDesenho
{
    /// <summary>Todas as strings do espaço do modelo, pela entidade.</summary>
    internal static Dictionary<ObjectId, ElectricalString> Ler(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        return ElectricalStore.Strings(transacao, database).ToDictionary(x => x.Id, x => x.String);
    }

    /// <summary>
    /// Solta todas as strings do inversor (14.5, "apagar todas"): só o
    /// vínculo no XData; as strings continuam no desenho, livres. Quantas.
    /// </summary>
    internal static int Soltar(Database database, Guid inversor) =>
        Gravar(database, StringAllocation.Release(inversor, Ler(database).Values));

    /// <summary>
    /// Regrava o XData das strings mudadas (o vínculo com o inversor), numa
    /// transação só. Acha a entidade pelo GUID da string. A geometria não é
    /// tocada; a cor da string (e dos sinais dela) passa a ser a do inversor,
    /// ou ByLayer se ficou livre (<see cref="CorDasStrings"/>). Quantas gravou.
    /// </summary>
    internal static int Gravar(Database database, IReadOnlyCollection<ElectricalString> mudadas)
    {
        if (mudadas.Count == 0) return 0;

        var setup = ConfiguracaoEletricaStore.Ler(database).Setup;
        using var transacao = database.TransactionManager.StartTransaction();

        var porGuid = mudadas.ToDictionary(s => s.Id);
        var alvos = ElectricalStore.Strings(transacao, database).Where(x => porGuid.ContainsKey(x.String.Id)).ToList();

        // Um COPY da polilinha leva o XData junto: duas entidades com o mesmo
        // GUID. Gravar numa regravaria a outra (que pode ser de outro
        // inversor). Nada é gravado; o usuário apaga as cópias.
        if (alvos.GroupBy(x => x.String.Id).Any(g => g.Count() > 1))
            throw new InvalidOperationException(Tr.T("há string copiada no desenho (duas polilinhas com o mesmo GUID); apague as cópias e tente de novo"));

        foreach (var (id, atual) in alvos)
            ElectricalStore.SaveString(transacao, (Entity)transacao.GetObject(id, OpenMode.ForWrite), porGuid[atual.Id]);

        CorDasStrings.Pintar(transacao, database, CorDasStrings.PeloCadastro(setup, mudadas));
        transacao.Commit();
        return alvos.Count;
    }
}
