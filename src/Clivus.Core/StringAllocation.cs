namespace Clivus.Core;

/// <summary>
/// O que alocar um punhado de strings num inversor dá (14.3): as strings que
/// mudam (já com o inversor novo), quantas já eram dele e as recusadas por
/// serem de outro inversor (regra elétrica 2: travadas).
/// </summary>
public sealed record AllocationPlan(IReadOnlyList<ElectricalString> Changed, int AlreadyHere, IReadOnlyList<ElectricalString> Refused);

/// <summary>
/// A alocação de strings em inversores (elétrica, 14.2 a 14.5). O vínculo
/// mora só na string (<see cref="ElectricalString.Inverter"/>): as strings de
/// um inversor são as que apontam para ele. Alocar e soltar trocam só esse
/// campo; nada aqui mexe em geometria nem na tag.
/// </summary>
public static class StringAllocation
{
    /// <summary>Quantas strings cada inversor tem (inversor sem string não aparece).</summary>
    public static IReadOnlyDictionary<Guid, int> CountByInverter(IEnumerable<ElectricalString> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        return strings.Where(s => s.IsAllocated).GroupBy(s => s.Inverter).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Quantas strings passam do que o modelo comporta (14.4): 28 strings num
    /// modelo de 26 entradas dá 2; dentro da capacidade, 0. Sem modelo, não
    /// há como saber: 0 (quem chama avisa que falta o modelo).
    /// </summary>
    public static int Excess(int strings, InverterModel? model) =>
        model is null ? 0 : Math.Max(0, strings - model.TotalInputs);

    /// <summary>
    /// O aviso vermelho de excesso (14.4), ou null se cabe. Avisa, não
    /// impede: a alocação fica feita (decisão conservadora, registrada no
    /// relatório: nada é desfeito sozinho).
    /// </summary>
    public static string? ExcessWarning(Inverter inverter, InverterModel? model, int strings)
    {
        ArgumentNullException.ThrowIfNull(inverter);

        var excesso = Excess(strings, model);
        return excesso == 0 ? null : Tr.F("EXCESSO no {0}: {1} strings para {2} entradas do modelo {3} ({4} a mais)", inverter.Name, strings, model!.TotalInputs, model.Name, excesso);
    }

    /// <summary>
    /// Solta todas as strings do inversor (14.5, "apagar todas"): as que
    /// apontam para ele voltam a ficar livres. Só o campo Inverter muda; a
    /// string continua no desenho (regra elétrica 3). Devolve as mudadas.
    /// </summary>
    public static IReadOnlyList<ElectricalString> Release(Guid inverter, IEnumerable<ElectricalString> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        if (inverter == Guid.Empty) return [];

        return strings.Where(s => s.Inverter == inverter).DistinctBy(s => s.Id).Select(s => s with { Inverter = Guid.Empty }).ToList();
    }

    /// <summary>A string está travada para este inversor: é de outro (regra elétrica 2).</summary>
    public static bool IsLockedFor(ElectricalString s, Guid inverter)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.IsAllocated && s.Inverter != inverter;
    }

    /// <summary>
    /// Aloca as strings escolhidas no inversor: as livres passam a ser dele,
    /// as dele ficam como estão, as de outro inversor são recusadas (nunca
    /// trocam de dono por aqui: soltar antes é um ato explícito). A mesma
    /// string escolhida duas vezes conta uma vez.
    /// </summary>
    public static AllocationPlan Allocate(Guid inverter, IEnumerable<ElectricalString> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (inverter == Guid.Empty) throw new ArgumentException("inversor vazio", nameof(inverter));

        var mudam = new List<ElectricalString>();
        var recusadas = new List<ElectricalString>();
        var jaEram = 0;

        foreach (var s in selected.DistinctBy(s => s.Id))
        {
            if (s.Inverter == inverter) jaEram++;
            else if (IsLockedFor(s, inverter)) recusadas.Add(s);
            else mudam.Add(s with { Inverter = inverter });
        }

        return new AllocationPlan(mudam, jaEram, recusadas);
    }
}
