namespace Clivus.Core;

/// <summary>Uma mesa assentada num terreno diferente do de hoje: quanto ele subiu e desceu debaixo dos pilares dela.</summary>
/// <param name="Label">O letreiro da mesa.</param>
/// <param name="MaxRise">O quanto o terreno de hoje está acima do gravado, no pilar em que mais subiu (0 se não subiu).</param>
/// <param name="MaxDrop">O quanto está abaixo, no pilar em que mais desceu (0 se não desceu).</param>
public sealed record DriftedTable(string Label, double MaxRise, double MaxDrop);

/// <summary>
/// As mesas que o terreno de hoje não sustenta mais (30/10/2026, o 3D do
/// Aurora no navegador: "o 3d no navegador cagou forte, no 3d do cad não tem
/// isso"). O terreno mudou depois que elas foram geradas (outra superfície,
/// a superfície editada, outro processamento), e em volta de 570 pilares o
/// terreno de hoje passava até 5 m por cima da cota em que o pilar foi
/// assentado: no 3D, o chão aparece por cima das mesas. Cada pilar guarda a
/// cota do terreno do dia em que foi desenhado (o GroundZ); comparada com o
/// terreno de hoje no mesmo X/Y, diz quais mesas precisam ser recalculadas.
/// </summary>
public static class TerrainDrift
{
    /// <summary>Até isto de diferença o terreno é o mesmo (a grade do motor e o arredondamento).</summary>
    public const double Tolerance = 0.30;

    /// <summary>
    /// As mesas com algum pilar cuja cota gravada difere do terreno de hoje
    /// mais que a tolerância, da que mais mudou para a que menos. Pilar sem
    /// cota gravada ou sem terreno embaixo hoje não conta.
    /// </summary>
    public static IReadOnlyList<DriftedTable> Tables(IEnumerable<(string Label, double? Recorded, double? Today)> pillars, double tolerance = Tolerance)
    {
        ArgumentNullException.ThrowIfNull(pillars);

        return pillars
            .Where(p => p.Recorded is { } r && double.IsFinite(r) && p.Today is { } t && double.IsFinite(t))
            .GroupBy(p => p.Label, StringComparer.Ordinal)
            .Select(g => new DriftedTable(
                g.Key,
                Math.Max(0, g.Max(p => p.Today!.Value - p.Recorded!.Value)),
                Math.Max(0, g.Max(p => p.Recorded!.Value - p.Today!.Value))))
            .Where(m => m.MaxRise > tolerance || m.MaxDrop > tolerance)
            .OrderByDescending(m => Math.Max(m.MaxRise, m.MaxDrop))
            .ThenBy(m => m.Label, StringComparer.Ordinal)
            .ToList();
    }
}
