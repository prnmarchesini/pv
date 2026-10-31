using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As mesas que o salvar sujou. Até 30/10/2026, cada salvar deixava todas as
/// mesas biposte pendentes e vermelhas: o AutoCAD regrava os sólidos ao
/// salvar, e o vigia via edição (Renan: "depois de salvar as mesas ficaram
/// todas vermelhas, bug dos bravos"). O vigia não anota mais nada durante o
/// salvamento (<see cref="LayoutWatcher"/>); isto desfaz o que ficou nos
/// desenhos.
///
/// Ao abrir um desenho que o vigia corrigido ainda não marcou, a mesa cuja
/// pendência pode ser só do salvar (<see cref="PendingChanges.MayBeFromSave"/>)
/// volta a limpa, com as cores de antes (as do Apagar › Cores: a do tipo de
/// mesa, a da camada, ou magenta na marcada). Nada se move nem se recalcula.
/// Uma vez por desenho: a chave no dicionário diz que, dali em diante, toda
/// pendência é do vigia corrigido (ele grava a chave quando marca uma mesa).
/// </summary>
internal static class PendenciasDoSalvar
{
    /// <summary>A chave no dicionário do desenho.</summary>
    internal const string Chave = "VIGIA_SALVAR";

    private static Color PelaCamada => Color.FromColorIndex(ColorMethod.ByLayer, 256);

    /// <summary>Grava a chave, se ainda não está lá (fora de transação: o dicionário abre a sua).</summary>
    internal static void Lembrar(Database database)
    {
        try
        {
            if (PluginDictionary.Contains(database, Chave)) return;

            PluginDictionary.Save(database, Chave, new ResultBuffer(new TypedValue((int)DxfCode.Int16, 1)));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui gravar a chave do vigia corrigido.", erro);
        }
    }

    /// <summary>
    /// Limpa as mesas que o salvar sujou e devolve os letreiros delas. Fora
    /// de comando (ao abrir): trava o documento e cala o vigia.
    /// </summary>
    internal static IReadOnlyList<string> Consertar(Document documento)
    {
        var database = documento.Database;

        // Os registros antes da transação de escrita (Limpeza: ler o
        // dicionário com ela aberta derrubava o Core Console).
        if (PluginDictionary.Contains(database, Chave)) return [];

        var tiposDeMesa = MesasDoDesenho.Ler(database);

        return EscritaForaDeComando.Fazer(documento, () =>
        {
            var letreiros = new List<string>();

            using (var transacao = database.TransactionManager.StartTransaction())
            {
                Limpar(transacao, database, tiposDeMesa, letreiros);
                transacao.Commit();
            }

            if (letreiros.Count > 0) Lembrar(database);

            return (IReadOnlyList<string>)letreiros;
        });
    }

    private static void Limpar(Transaction transacao, Database database, IReadOnlyList<DrawingTable> tiposDeMesa, List<string> letreiros)
    {
        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
        {
            if (mesa.Identity is not { } identidade || mesa.Contour is not { } contorno || mesa.IsDuplicated) continue;

            bool? moveu = identidade.Anchor is null ? null : ValidarCommands.Moveu(transacao, mesa);
            if (!PendingChanges.MayBeFromSave(identidade, mesa.StructureBars.Count > 0, moveu)) continue;

            LayoutXData.SaveTable(transacao, (Entity)transacao.GetObject(contorno, OpenMode.ForWrite), identidade.AsClean());

            var corDoTipo = DrawingTables.Find(tiposDeMesa, identidade.ProfileName)?.Color;

            void Devolver(ObjectId id, CleanupPiece peca)
            {
                if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) return;

                var cor = Cleanup.OriginalColor(peca, identidade.Marked, triedAll: false, corDoTipo) is { } c
                    ? Color.FromRgb(c.R, c.G, c.B)
                    : PelaCamada;

                if (entidade.Color.Equals(cor)) return;

                entidade.UpgradeOpen();
                entidade.Color = cor;
            }

            foreach (var id in mesa.Contours) Devolver(id, CleanupPiece.Contour);
            foreach (var id in mesa.Pillars) Devolver(id, CleanupPiece.Pillar);
            foreach (var id in mesa.Modules) Devolver(id, CleanupPiece.Module);
            foreach (var id in mesa.StructureBars) Devolver(id, CleanupPiece.Structure);

            letreiros.Add(identidade.Label);
        }
    }
}
