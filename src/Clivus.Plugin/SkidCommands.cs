using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.SkidCommands))]

namespace Clivus.Plugin;

/// <summary>
/// O skid (14.7): agrupar inversores num trafo pela seleção em campo, só dos
/// retângulos de inversor (regra elétrica 4). O vínculo gravado é
/// <see cref="Inverter.Transformer"/>; a posição dos retângulos não entra
/// em conta nenhuma (regra elétrica 1).
/// </summary>
public static class SkidCommands
{
    /// <summary>CLIVUS_ELETRICA_SKID: o trafo (apelido ou GUID), o nome do skid (Enter = "Skid T1") e a seleção dos inversores.</summary>
    [CommandMethod(PluginInfo.ComandoEletricaSkid)]
    public static void Agrupar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var qual = editor.GetString(new PromptStringOptions(Tr.T("\nTrafo do skid (apelido): ")) { AllowSpaces = true });
            if (qual.Status != PromptStatus.OK) return;

            var (setup, problema) = ConfiguracaoEletricaStore.Ler(documento.Database);
            if (problema is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", problema));

            var achados = setup.FindEquipment(qual.StringResult).Where(e => e.Kind == EquipmentKind.Transformer).ToList();
            if (achados.Count != 1)
            {
                editor.WriteMessage(Tr.F("\nSKID Não há trafo \"{0}\" no cadastro.\n", qual.StringResult.Trim()));
                return;
            }

            var trafo = setup.FindTransformer(achados[0].Id)!;
            var nome = editor.GetString(new PromptStringOptions(Tr.F("\nNome do skid <{0}>: ", setup.FindSkid(trafo.Id)?.Name ?? Tr.F("Skid {0}", trafo.Nickname))) { AllowSpaces = true });
            if (nome.Status != PromptStatus.OK) return;

            var emCampo = InversoresEmCampo(documento.Database);
            if (emCampo.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nSKID Nenhum inversor em campo: aloque os inversores em campo antes (Alocar em campo).\n"));
                return;
            }

            if (Selecionar(documento, trafo, emCampo) is not { } escolhidos) return;

            SkidResult? r = null;
            ConfiguracaoEletricaStore.Mudar(documento.Database, s => r = s.Group(trafo.Id, nome.StringResult, escolhidos));

            if (r!.Problem is { } porque)
            {
                editor.WriteMessage(Tr.F("\nSKID Não agrupei: {0}.\n", porque));
                return;
            }

            var depois = ConfiguracaoEletricaStore.Ler(documento.Database).Setup;
            editor.WriteMessage(Tr.F("\nSKID {0} ({1}): {2} inversor(es) agrupado(s), {3} já eram dele, {4} recusado(s) por serem de outro skid. Agora {5} inversor(es).\n",
                depois.FindSkid(trafo.Id)?.Name ?? "—", trafo.Nickname, r.Added, r.AlreadyHere, r.Refused.Count, depois.InvertersOf(trafo.Id).Count));
            if (r.Refused.Count > 0)
                editor.WriteMessage(Tr.F("  Recusados (tire do skid deles antes): {0}\n", string.Join(", ", r.Refused.Select(i => i.Name))));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao agrupar inversores no skid.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui agrupar os inversores: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento);
        }
    }

    /// <summary>Os retângulos de inversor em campo, pela referência (cópias também respondem pelo inversor).</summary>
    private static Dictionary<ObjectId, Guid> InversoresEmCampo(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        return EquipamentoEmCampo.Posicionados(transacao, database)
            .Where(x => x.Key.Kind == EquipmentKind.Inverter)
            .SelectMany(x => x.Value.Select(id => (id, x.Key.Id)))
            .ToDictionary(x => x.id, x => x.Item2);
    }

    /// <summary>
    /// A seleção em campo, só de inversores: o filtro pega blocos do plugin, e
    /// o que não é retângulo de inversor sai na hora. A caixa sobre o CAD
    /// conta ao vivo. Null se o usuário desistiu; senão os inversores (GUID).
    /// </summary>
    private static List<Guid>? Selecionar(Autodesk.AutoCAD.ApplicationServices.Document documento, Transformer trafo, IReadOnlyDictionary<ObjectId, Guid> emCampo)
    {
        var editor = documento.Editor;
        var escolhidos = new HashSet<ObjectId>();
        CaixaDeSelecao? placar = null;

        void Atualizar()
        {
            try
            {
                if (!ClivusExtension.TemInterface()) return;

                placar ??= AlocacaoDeStringsCommands.NovoPlacar(560);
                placar.TextoLivre = Tr.F("Skid do {0}: {1} inversor(es) selecionado(s)", trafo.Nickname, escolhidos.Select(id => emCampo[id]).Distinct().Count());
                if (!placar.IsVisible) placar.Show();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha no placar do skid.", erro);
            }
        }

        void Somou(object? _, SelectionAddedEventArgs e)
        {
            try
            {
                var ids = e.AddedObjects.GetObjectIds();
                for (var i = ids.Length - 1; i >= 0; i--)
                {
                    if (emCampo.ContainsKey(ids[i])) escolhidos.Add(ids[i]);
                    else e.Remove(i);
                }
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao conferir os inversores da seleção.", erro);
            }

            Atualizar();
        }

        void Tirou(object? _, SelectionRemovedEventArgs e)
        {
            try
            {
                foreach (ObjectId id in e.RemovedObjects.GetObjectIds()) escolhidos.Remove(id);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao tirar inversores da seleção.", erro);
            }

            Atualizar();
        }

        var filtro = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "INSERT"),
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginInfo.PrefixoDeDados),
        ]);

        editor.SelectionAdded += Somou;
        editor.SelectionRemoved += Tirou;

        try
        {
            Atualizar();
            var r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nInversores do skid do {0} (só inversores; Shift+clique tira; Enter termina): ", trafo.Nickname) }, filtro);
            if (r.Status == PromptStatus.Cancel) return null;

            // Sem o evento (script): a conferência é a mesma.
            return r.Status == PromptStatus.OK
                ? r.Value.GetObjectIds().Where(emCampo.ContainsKey).Select(id => emCampo[id]).Distinct().ToList()
                : [];
        }
        finally
        {
            editor.SelectionAdded -= Somou;
            editor.SelectionRemoved -= Tirou;
            placar?.Close();
        }
    }
}
