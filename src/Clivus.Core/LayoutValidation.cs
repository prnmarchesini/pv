namespace Clivus.Core;

/// <summary>
/// O resultado da validação do desenho (7.7): o que o plugin registrou
/// contra o que existe, e o que mudou desde o último cálculo. Puro: quem
/// coleta é o plugin; aqui só se junta e se diz.
/// </summary>
/// <param name="MissingAreas">Áreas registradas cuja polilinha não está mais no desenho (ou perdeu a identidade).</param>
/// <param name="MissingAlignments">Alinhamentos registrados idem.</param>
/// <param name="DirtyTables">Letreiros das mesas que o vigia marcou sujas (movidas, editadas ou copiadas com o plugin carregado).</param>
/// <param name="MovedTables">Letreiros das mesas limpas cujo contorno não está mais onde foi desenhado: movidas sem o vigia ver (plugin descarregado, outra máquina).</param>
/// <param name="DuplicatedAreas">Quantas identidades de área aparecem em mais de uma polilinha (área copiada).</param>
/// <param name="DuplicatedAlignments">Idem para alinhamentos.</param>
/// <param name="DuplicatedTables">Letreiros das mesas com mais de um contorno na mesma identidade.</param>
/// <param name="DuplicatedPieces">Quantos GUIDs de peça (pilar, módulo, face, nota) se repetem.</param>
/// <param name="Orphans">Quantas mesas só têm peças, sem contorno.</param>
/// <param name="PendingRemovals">Letreiros das mesas removidas ainda não recontadas.</param>
/// <param name="TerrainWarning">O aviso do carimbo da superfície, ou null se está em dia.</param>
public sealed record LayoutValidation(
    IReadOnlyList<string> MissingAreas,
    IReadOnlyList<string> MissingAlignments,
    IReadOnlyList<string> DirtyTables,
    IReadOnlyList<string> MovedTables,
    int DuplicatedAreas,
    int DuplicatedAlignments,
    IReadOnlyList<string> DuplicatedTables,
    int DuplicatedPieces,
    int Orphans,
    IReadOnlyList<string> PendingRemovals,
    string? TerrainWarning)
{
    /// <summary>Nada a apontar.</summary>
    public bool IsClean => Count == 0;

    /// <summary>Quantos achados, no total.</summary>
    public int Count =>
        MissingAreas.Count + MissingAlignments.Count + DirtyTables.Count + MovedTables.Count
        + DuplicatedAreas + DuplicatedAlignments + DuplicatedTables.Count
        + DuplicatedPieces + Orphans + PendingRemovals.Count + (TerrainWarning is null ? 0 : 1);

    /// <summary>As linhas do relatório, em português, cada uma dizendo o que fazer.</summary>
    public IReadOnlyList<string> Lines()
    {
        if (IsClean) return [Tr.T("nada a apontar: registros, mesas e terreno em dia.")];

        var linhas = new List<string>();

        if (MissingAreas.Count > 0)
            linhas.Add(Tr.F("{0} área(s) registrada(s) não está(ão) no desenho ({1}): rode Reindexar, ou trace de novo.", MissingAreas.Count, Juntar(MissingAreas)));

        if (MissingAlignments.Count > 0)
            linhas.Add(Tr.F("{0} alinhamento(s) registrado(s) não está(ão) no desenho ({1}): rode Reindexar, ou trace de novo.", MissingAlignments.Count, Juntar(MissingAlignments)));

        if (DirtyTables.Count > 0)
            linhas.Add(Tr.F("{0} mesa(s) marcada(s) pendente(s) pelo vigia ({1}): movida, editada ou copiada; use Recalcular pendentes.", DirtyTables.Count, Juntar(DirtyTables)));

        if (MovedTables.Count > 0)
            linhas.Add(Tr.F("{0} mesa(s) fora de onde foi(ram) desenhada(s) sem o vigia ver ({1}): movida(s) com o plugin descarregado; use Recalcular nelas.", MovedTables.Count, Juntar(MovedTables)));

        if (DuplicatedAreas > 0)
            linhas.Add(Tr.F("{0} identidade(s) de área em mais de uma polilinha (área copiada): apague a cópia, ou trace de novo.", DuplicatedAreas));

        if (DuplicatedAlignments > 0)
            linhas.Add(Tr.F("{0} identidade(s) de alinhamento em mais de uma polilinha (alinhamento copiado): apague a cópia, ou trace de novo.", DuplicatedAlignments));

        if (DuplicatedTables.Count > 0)
            linhas.Add(Tr.F("{0} mesa(s) com mais de um contorno na mesma identidade ({1}): apague a cópia, ou use o Regerar área.", DuplicatedTables.Count, Juntar(DuplicatedTables)));

        if (DuplicatedPieces > 0)
            linhas.Add(Tr.F("{0} peça(s) com identidade repetida: use o Regerar área.", DuplicatedPieces));

        if (Orphans > 0)
            linhas.Add(Tr.F("{0} mesa(s) só com peças, sem contorno: apague as peças, ou use o Regerar área.", Orphans));

        if (PendingRemovals.Count > 0)
            linhas.Add(Tr.F("{0} mesa(s) removida(s) ainda não recontada(s) ({1}): use Recontar.", PendingRemovals.Count, Juntar(PendingRemovals)));

        if (TerrainWarning is not null)
            linhas.Add(Tr.F("terreno: {0}", TerrainWarning));

        return linhas;
    }

    private static string Juntar(IReadOnlyList<string> nomes) =>
        nomes.Count <= 5 ? string.Join(", ", nomes) : string.Join(", ", nomes.Take(5)) + Tr.F(" e mais {0}", nomes.Count - 5);
}
