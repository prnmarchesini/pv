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
/// pendência pode ser só do salvar (<see cref="PendingChanges.MayBeFromSave"/>:
/// com sólido, no lugar, inteira e sozinha) volta a limpa, com as cores de
/// antes (as do Apagar › Cores: a do tipo de mesa, a da camada, magenta na
/// marcada, roxo na que tentou todas as mesas). Nada se move nem se
/// recalcula. Cada mesa numa transação: a que falhar (camada que não deixa
/// escrever) continua pendente e é dita. Uma vez por desenho: a chave no
/// dicionário diz que, dali em diante, toda pendência é do vigia corrigido
/// (ele grava a chave quando marca uma mesa).
/// </summary>
internal static class PendenciasDoSalvar
{
    /// <summary>A chave no dicionário do desenho.</summary>
    internal const string Chave = "VIGIA_SALVAR";

    /// <summary>Duas âncoras a menos que isto em planta são o mesmo lugar (a cópia colada em cima).</summary>
    private const double MesmoLugar = 0.01;

    private static Color PelaCamada => Color.FromColorIndex(ColorMethod.ByLayer, 256);

    /// <summary>O que o conserto fez: as mesas limpas e as que não deu para limpar.</summary>
    internal sealed record Resultado(IReadOnlyList<string> Limpas, IReadOnlyList<string> Falharam);

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
    /// Limpa as mesas que o salvar sujou. Fora de comando (ao abrir): trava o
    /// documento e cala o vigia.
    /// </summary>
    internal static Resultado Consertar(Document documento)
    {
        var database = documento.Database;

        // Os registros antes da transação de escrita (Limpeza: ler o
        // dicionário com ela aberta derrubava o Core Console).
        if (PluginDictionary.Contains(database, Chave)) return new Resultado([], []);

        var tiposDeMesa = MesasDoDesenho.Ler(database);
        var antesRoxas = AntesRoxas(database);

        return EscritaForaDeComando.Fazer(documento, () =>
        {
            var limpas = new List<string>();
            var falharam = new List<string>();

            using (var transacao = database.TransactionManager.StartTransaction())
            {
                Limpar(transacao, database, tiposDeMesa, antesRoxas, limpas, falharam);
                transacao.Commit();
            }

            if (limpas.Count > 0) Lembrar(database);

            return new Resultado(limpas, falharam);
        });
    }

    /// <summary>As peças que alguma análise pintou e que, antes, estavam roxas (a mesa que tentou todas).</summary>
    private static HashSet<ObjectId> AntesRoxas(Database database)
    {
        var roxas = new HashSet<ObjectId>();

        foreach (var tipo in Enum.GetValues<IndependentKind>())
        {
            foreach (var (id, antes, _) in PecasPintadas.Ler(database, tipo))
                if (ERoxo(antes)) roxas.Add(id);
        }

        return roxas;
    }

    private static bool ERoxo(Color? cor) =>
        cor is { ColorMethod: ColorMethod.ByColor } c
        && c.Red == Cleanup.TriedAllColor.R && c.Green == Cleanup.TriedAllColor.G && c.Blue == Cleanup.TriedAllColor.B;

    private static TablePieceCount Contagem(TableParts mesa) =>
        new(mesa.Modules.Count, mesa.Faces.Count, mesa.Pillars.Count, mesa.StructureBars.Count);

    private static (long, long) Lugar(double x, double y) => ((long)Math.Round(x / MesmoLugar), (long)Math.Round(y / MesmoLugar));

    private static void Limpar(
        Transaction transacao, Database database, IReadOnlyList<DrawingTable> tiposDeMesa, IReadOnlySet<ObjectId> antesRoxas,
        List<string> limpas, List<string> falharam)
    {
        var mesas = LayoutScan.Tables(transacao, database).Values.Where(m => m.Identity is not null).ToList();

        // A mesa inteira de cada tipo, e quantas mesas há em cada lugar (a
        // cópia colada em cima tem a âncora da original).
        var inteiras = PendingChanges.TypicalPieces(mesas.Select(m => (m.Identity!.ProfileName, Contagem(m))));
        var porLugar = mesas
            .Where(m => m.Identity!.Anchor is not null)
            .GroupBy(m => Lugar(m.Identity!.Anchor!.Value.X, m.Identity!.Anchor!.Value.Y))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var mesa in mesas)
        {
            var identidade = mesa.Identity!;
            if (mesa.Contour is not { } contorno || mesa.IsDuplicated) continue;

            bool? moveu = identidade.Anchor is null ? null : ValidarCommands.Moveu(transacao, mesa);
            var inteira = inteiras.TryGetValue(identidade.ProfileName ?? string.Empty, out var tipica) && Contagem(mesa) == tipica;
            var sozinha = identidade.Anchor is { } a && porLugar.GetValueOrDefault(Lugar(a.X, a.Y)) == 1;

            if (!PendingChanges.MayBeFromSave(identidade, mesa.StructureBars.Count > 0, moveu, inteira, sozinha)) continue;

            try
            {
                using var uma = database.TransactionManager.StartTransaction();

                // Camada travada não impede (como no Apagar › Cores): o
                // conserto devolve o que o vigia pintou.
                LayoutXData.SaveTable(uma, (Entity)uma.GetObject(contorno, OpenMode.ForWrite, false, true), identidade.AsClean());

                var corDoTipo = DrawingTables.Find(tiposDeMesa, identidade.ProfileName)?.Color;
                var tentouTodas = identidade.Marked && mesa.Contours.Concat(mesa.Modules).Any(antesRoxas.Contains);

                void Devolver(ObjectId id, CleanupPiece peca)
                {
                    if (id.IsErased || uma.GetObject(id, OpenMode.ForRead) is not Entity entidade) return;

                    var cor = Cleanup.OriginalColor(peca, identidade.Marked, tentouTodas, corDoTipo) is { } c
                        ? Color.FromRgb(c.R, c.G, c.B)
                        : PelaCamada;

                    if (entidade.Color.Equals(cor)) return;

                    ((Entity)uma.GetObject(id, OpenMode.ForWrite, false, true)).Color = cor;
                }

                foreach (var id in mesa.Contours) Devolver(id, CleanupPiece.Contour);
                foreach (var id in mesa.Pillars) Devolver(id, CleanupPiece.Pillar);
                foreach (var id in mesa.Modules) Devolver(id, CleanupPiece.Module);
                foreach (var id in mesa.StructureBars) Devolver(id, CleanupPiece.Structure);

                uma.Commit();
                limpas.Add(identidade.Label);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Não consegui desfazer a pendência do salvar em {identidade.Label}.", erro);
                falharam.Add(identidade.Label);
            }
        }
    }
}
