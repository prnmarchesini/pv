namespace Clivus.Core;

/// <summary>
/// A pré-tag da string (item 8 de 10/10/2026: "quando distribuir, coloque a
/// pré tag indicando o inversor ... quando eu configurar as tags de verdade,
/// aí você apaga e coloca as tags configuradas"): o nome curto do inversor
/// ("I3") sobre a string, na camada própria. Fica enquanto a string é do
/// inversor e não tem tag de verdade.
/// </summary>
public sealed record StringPreTag(Guid String, string Text)
{
    public const string Tipo = "StringPreTag";
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [String.ToString("D"), Text ?? string.Empty];

    public static StringPreTag? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Count >= FieldCount && Guid.TryParse(c[0], out var s) && s != Guid.Empty ? new StringPreTag(s, c[1]) : null;
    }

    /// <summary>
    /// O nome curto do inversor: "I" e o número do fim do nome ("Inversor 3"
    /// dá "I3"); nome sem número no fim, "I" e a posição dele na lista (1, 2...).
    /// </summary>
    public static string ShortName(string? nome, int posicao)
    {
        var texto = nome?.Trim() ?? string.Empty;
        var k = texto.Length;
        while (k > 0 && char.IsAsciiDigit(texto[k - 1])) k--;
        var numero = texto[k..].TrimStart('0');
        if (k < texto.Length && numero.Length == 0) numero = "0";
        return "I" + (numero.Length > 0 ? numero : posicao.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A pré-tag que cada string deve ter: a string alocada a um inversor do
    /// cadastro e ainda sem tag de verdade ganha o nome curto dele; as outras
    /// (livre, com tag, inversor que sumiu) não têm (ficam de fora).
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> Expected(IReadOnlyList<Inverter> inversores, IEnumerable<ElectricalString> strings)
    {
        ArgumentNullException.ThrowIfNull(inversores);
        ArgumentNullException.ThrowIfNull(strings);

        var nomes = new Dictionary<Guid, string>();
        for (var i = 0; i < inversores.Count; i++) nomes.TryAdd(inversores[i].Id, ShortName(inversores[i].Name, i + 1));

        var saida = new Dictionary<Guid, string>();
        foreach (var s in strings)
            if (s.IsAllocated && string.IsNullOrEmpty(s.Tag) && nomes.TryGetValue(s.Inverter, out var curto)) saida[s.Id] = curto;
        return saida;
    }
}
