namespace Clivus.Core;

/// <summary>
/// A alocação de strings em inversores (elétrica, 14.2 a 14.5). O vínculo
/// mora só na string (<see cref="ElectricalString.Inverter"/>): as strings de
/// um inversor são as que apontam para ele. Nada aqui mexe em geometria.
/// </summary>
public static class StringAllocation
{
    /// <summary>Quantas strings cada inversor tem (inversor sem string não aparece).</summary>
    public static IReadOnlyDictionary<Guid, int> CountByInverter(IEnumerable<ElectricalString> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        return strings.Where(s => s.IsAllocated).GroupBy(s => s.Inverter).ToDictionary(g => g.Key, g => g.Count());
    }
}
