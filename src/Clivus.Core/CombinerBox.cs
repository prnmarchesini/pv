namespace Clivus.Core;

/// <summary>
/// Uma combiner box (roteamento, 19.1): elo opcional entre as strings e o
/// inversor. Cadastro no molde do inversor e do trafo: nome (a tag, visível
/// de cima), quantas entradas, a dimensão do retângulo 3D e o inversor a que
/// ela liga (vazio = ainda sem inversor).
/// </summary>
public sealed record Combiner(Guid Id, string Name, int Inputs, EquipmentSize Size, Guid Inverter)
{
    public const int FieldCount = 7;
    public const int MaxInputs = 100;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Inputs is > 0 and <= MaxInputs && Size.IsValid;

    public IReadOnlyList<string> ToFields() =>
        [Id.ToString("D"), Name, Inputs.ToString(System.Globalization.CultureInfo.InvariantCulture), .. Size.Fields(), Inverter == Guid.Empty ? string.Empty : Inverter.ToString("D")];

    public static Combiner? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Guid.TryParse(c[0], out var id)) return null;
        if (!int.TryParse(c[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var entradas)) return null;
        if (EquipmentSize.Parse(c, 3) is not { } tamanho || !ElectricalString.OptionalGuid(c[6], out var inversor)) return null;
        var cb = new Combiner(id, c[1], entradas, tamanho, inversor);
        return cb.IsValid ? cb : null;
    }
}

/// <summary>Uma string alocada numa combiner (19.2). Uma string fica em no máximo uma combiner.</summary>
public sealed record CombinerString(Guid String, Guid Combiner)
{
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [String.ToString("D"), Combiner.ToString("D")];

    public static CombinerString? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var s) && s != Guid.Empty && Guid.TryParse(c[1], out var cb) && cb != Guid.Empty
            ? new CombinerString(s, cb)
            : null;
}

/// <summary>O resultado de alocar strings numa combiner: a lista nova, quantas entraram, quantas recusadas (de outra combiner) e o excesso de entradas.</summary>
public sealed record CombinerAllocationResult(IReadOnlyList<CombinerString> Allocation, int Added, int Refused, int Excess);

/// <summary>
/// A alocação de strings na combiner (19.2), com as mesmas regras da
/// alocação no inversor: só string, trava de string já em outra combiner,
/// aviso de excesso de entradas. A string passa a ligar no inversor da
/// combiner (a cadeia fica inversor -> combiner -> string).
/// </summary>
public static class CombinerAllocation
{
    public static CombinerAllocationResult Allocate(Combiner combiner, IEnumerable<Guid> strings, IReadOnlyList<CombinerString> atual)
    {
        ArgumentNullException.ThrowIfNull(combiner);

        var lista = atual.ToList();
        int entraram = 0, recusadas = 0;

        foreach (var s in strings.Distinct())
        {
            var ja = lista.FirstOrDefault(x => x.String == s);
            if (ja is not null && ja.Combiner != combiner.Id)
            {
                recusadas++;
                continue;
            }

            if (ja is not null) continue;
            lista.Add(new CombinerString(s, combiner.Id));
            entraram++;
        }

        var total = lista.Count(x => x.Combiner == combiner.Id);
        return new CombinerAllocationResult(lista, entraram, recusadas, Math.Max(0, total - combiner.Inputs));
    }

    /// <summary>Solta todas as strings da combiner (só o vínculo; as strings ficam no desenho).</summary>
    public static IReadOnlyList<CombinerString> Release(Guid combiner, IReadOnlyList<CombinerString> atual) =>
        atual.Where(x => x.Combiner != combiner).ToList();
}
