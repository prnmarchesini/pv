using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A cor do tipo de mesa nas mesas já desenhadas. Renan, 03/10/2026: "não
/// tem nenhuma mesa laranja ou verde, as configurações de cores estão
/// falhas". Trocar a cor em Configurações e salvar repinta o contorno e os
/// módulos das mesas daquele tipo, sem precisar de Refazer.
///
/// Só muda o que é a cor do tipo: contorno na camada da mesa e módulo na
/// camada de módulo, com a cor antiga do tipo, por camada ou por bloco. Mesa
/// marcada (magenta/roxo) e mesa suja (vermelha) ficam como estão; peça que
/// uma análise pintou também, mas a "cor de antes" que a análise guardou
/// passa a ser a nova, para Tirar cores devolver a cor certa.
/// </summary>
internal static class CoresDosTipos
{
    /// <summary>Repinta as mesas desenhadas com a cor de cada tipo. Quantas mesas mudaram.</summary>
    internal static int Repintar(Database database, IReadOnlyList<DrawingTable> antes, IReadOnlyList<DrawingTable> depois)
    {
        ArgumentNullException.ThrowIfNull(database);

        var trocadas = new Dictionary<string, Color>();
        var mesas = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            foreach (var partes in LayoutScan.Tables(transacao, database).Values)
            {
                if (partes.Identity is not { Marked: false, Dirty: false } identidade) continue;
                if (DrawingTables.Find(depois, identidade.ProfileName) is not { } nova) continue;

                var cor = Color.FromRgb(nova.Color.R, nova.Color.G, nova.Color.B);
                var antiga = DrawingTables.Find(antes, identidade.ProfileName) is { } velha
                    ? Color.FromRgb(velha.Color.R, velha.Color.G, velha.Color.B)
                    : null;

                if (antiga is not null && antiga != cor) trocadas[PecasPintadas.Texto(antiga)] = cor;

                var mudou = false;

                foreach (var id in partes.Contours)
                    mudou |= Trocar(transacao, id, LayoutLayers.Mesa, antiga, cor);

                foreach (var id in partes.Modules)
                    mudou |= Trocar(transacao, id, LayoutLayers.Modulo, antiga, cor);

                if (mudou) mesas++;
            }

            transacao.Commit();
        }

        if (trocadas.Count > 0) AtualizarCorDeAntes(database, trocadas);

        return mesas;
    }

    /// <summary>Põe a cor do tipo na peça que está com a cor do tipo (a antiga, por camada ou por bloco).</summary>
    private static bool Trocar(Transaction transacao, ObjectId id, string camada, Color? antiga, Color nova)
    {
        if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) return false;
        if (!string.Equals(entidade.Layer, camada, StringComparison.OrdinalIgnoreCase)) return false;
        if (entidade.Color == nova) return false;

        var eDoTipo = entidade.Color.IsByLayer || entidade.Color.IsByBlock || (antiga is not null && entidade.Color == antiga);
        if (!eDoTipo) return false;

        entidade.UpgradeOpen();
        entidade.Color = nova;
        return true;
    }

    /// <summary>A cor de antes guardada pelas análises: a antiga do tipo vira a nova.</summary>
    private static void AtualizarCorDeAntes(Database database, IReadOnlyDictionary<string, Color> trocadas)
    {
        foreach (var tipo in Enum.GetValues<IndependentKind>())
        {
            var pecas = PecasPintadas.Ler(database, tipo);
            if (pecas.Count == 0 || !pecas.Any(p => trocadas.ContainsKey(PecasPintadas.Texto(p.Antes)))) continue;

            PecasPintadas.Gravar(database, tipo, pecas
                .Select(p => (p.Id, trocadas.TryGetValue(PecasPintadas.Texto(p.Antes), out var nova) ? nova : p.Antes, p.Pintura ?? p.Antes))
                .ToList());
        }
    }
}
