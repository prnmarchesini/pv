using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.EstadoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// O estado das mesas na linha de comando (7.1): sujar uma mesa à mão e
/// ver quantas estão limpas e quais estão sujas. O vigia (7.2) é quem vai
/// sujar sozinho; aqui é o usuário quem manda.
/// </summary>
public static class EstadoCommands
{
    private const string MotivoDoUsuario = "pedido do usuário";

    /// <summary>CLIVUS_PENDENTE: clica numa peça da mesa, e a mesa fica suja e vermelha.</summary>
    [CommandMethod(PluginInfo.ComandoPendente)]
    public static void Sujar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var opcoes = new PromptEntityOptions("\nClique numa peça da mesa a marcar como pendente: ");
            opcoes.SetRejectMessage("\nIsso não é uma peça de mesa do plugin.");
            opcoes.AddAllowedClass(typeof(Entity), false);

            var resposta = editor.GetEntity(opcoes);
            if (resposta.Status != PromptStatus.OK) return;

            using var transacao = documento.Database.TransactionManager.StartTransaction();

            var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
            var guid = LayoutScan.TableOf(entidade);

            if (guid is null)
            {
                editor.WriteMessage("\nPENDENTE Isso não é uma peça de mesa do plugin.\n");
                return;
            }

            var mesas = LayoutScan.Tables(transacao, documento.Database);

            if (!mesas.TryGetValue(guid.Value, out var mesa) || mesa.Identity is null)
            {
                editor.WriteMessage("\nPENDENTE A mesa dessa peça não tem mais contorno; não há onde gravar o estado.\n");
                return;
            }

            var pintadas = TableState.MarkDirty(transacao, mesa, MotivoDoUsuario);
            transacao.Commit();

            editor.WriteMessage($"\nPENDENTE {mesa.Identity.Label} pendente ({MotivoDoUsuario}); {pintadas} peça(s) pintada(s) de vermelho.\n");
            GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception erro) when (erro.ErrorStatus == ErrorStatus.OnLockedLayer)
        {
            editor.WriteMessage("\nPENDENTE Uma peça da mesa está em camada bloqueada; desbloqueie as camadas da usina e repita.\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao marcar a mesa como pendente.", erro);
            editor.WriteMessage($"\nNão consegui marcar a mesa como pendente: {erro.Message}\n");
        }
    }

    /// <summary>CLIVUS_PENDENTE_AUTO: suja a mesa de menor letreiro, sem perguntar. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoPendenteAutomatico)]
    public static void SujarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            using var transacao = documento.Database.TransactionManager.StartTransaction();

            var mesa = LayoutScan.Tables(transacao, documento.Database).Values
                .Where(m => m.Identity is not null)
                .OrderBy(m => m.Identity!.Label, StringComparer.Ordinal)
                .FirstOrDefault();

            if (mesa is null)
            {
                editor.WriteMessage("\nPENDENTE Nenhuma mesa no desenho.\n");
                return;
            }

            var pintadas = TableState.MarkDirty(transacao, mesa, MotivoDoUsuario);
            transacao.Commit();

            editor.WriteMessage(
                $"\nPENDENTE {mesa.Identity!.Label} pendente ({MotivoDoUsuario}); {pintadas} peça(s) pintada(s) de vermelho.\n"
                + $"  PENDENTE_GUID {mesa.Identity.Id:D} pecas={pintadas}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao marcar a mesa como pendente automaticamente.", erro);
            editor.WriteMessage($"\nNão consegui marcar a mesa como pendente: {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_ESTADO: quantas mesas limpas, quais sujas e por quê, e as
    /// removidas. Só lê: sem marca de undo, para um U depois dele desfazer o
    /// comando anterior do usuário, e não este.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEstado, CommandFlags.Modal | CommandFlags.NoUndoMarker)]
    public static void Estado()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            using var transacao = documento.Database.TransactionManager.StartTransaction();

            var mesas = LayoutScan.Tables(transacao, documento.Database).Values.ToList();
            var removidas = RemovalStore.Ler(documento.Database);
            transacao.Commit();

            var comContorno = mesas.Where(m => m.Identity is not null).OrderBy(m => m.Identity!.Label, StringComparer.Ordinal).ToList();
            var semContorno = mesas.Count - comContorno.Count;
            var sujas = comContorno.Where(m => m.Identity!.Dirty).ToList();

            editor.WriteMessage(
                $"\nESTADO {comContorno.Count} mesa(s), {comContorno.Count - sujas.Count} limpa(s), {sujas.Count} pendente(s)"
                + (semContorno > 0 ? $", {semContorno} com peças órfãs (sem contorno)" : string.Empty) + "\n");

            foreach (var mesa in sujas)
                editor.WriteMessage($"  {mesa.Identity!.DescribeState()}\n");

            editor.WriteMessage($"  {removidas.Items.Count} removida(s)" + (removidas.Items.Count > 0 ? ":" : ".") + "\n");

            foreach (var remocao in removidas.Items)
                editor.WriteMessage($"  {remocao.Describe()}\n");

            if (removidas.Problem is not null) editor.WriteMessage($"  ATENÇÃO: {removidas.Problem}.\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ler o estado das mesas.", erro);
            editor.WriteMessage($"\nNão consegui ler o estado: {erro.Message}\n");
        }
    }
}
