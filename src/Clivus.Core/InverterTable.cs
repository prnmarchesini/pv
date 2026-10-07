namespace Clivus.Core;

// A tabela dos inversores da aba Inversor (05/10/2026, pedido do Renan:
// "falta uma coluna de kWp e quantidade de strings"). O kWp de cada inversor
// é o do resumo elétrico (ElectricalSummary: a potência dos módulos das
// strings dele, pela mesa dona); aqui só se junta a linha e o total.

/// <summary>Uma linha da tabela dos inversores.</summary>
/// <param name="Strings">As strings alocadas nele.</param>
/// <param name="Capacity">O total de entradas do modelo (0 sem modelo no cadastro).</param>
/// <param name="PowerKwp">A potência CC (os módulos das strings dele), ou null se o resumo não pôde ser lido.</param>
public sealed record InverterTableRow(Inverter Inverter, InverterModel? Model, int Strings, int Capacity, double? PowerKwp)
{
    /// <summary>A potência nominal CA do modelo, em kW (0 = não informada).</summary>
    public double PowerKw => Model?.PowerKw ?? 0;

    /// <summary>A razão CC/CA (kWp / kW), só se o modelo tem potência e o kWp foi lido.</summary>
    public double? DcAcRatio => PowerKw > 0 && PowerKwp is { } kwp ? kwp / PowerKw : null;

    /// <summary>Mais strings que entradas (regra elétrica 6: aviso vermelho).</summary>
    public bool OverCapacity => Model is not null && Strings > Capacity;
}

/// <summary>O rodapé da tabela: a soma das colunas.</summary>
/// <param name="PowerKwpWithKw">O kWp só dos inversores com potência CA informada (o numerador do CC/CA).</param>
public sealed record InverterTableTotal(int Inverters, int Strings, int Capacity, double? PowerKwp, double PowerKw, double? PowerKwpWithKw = null)
{
    /// <summary>
    /// A razão CC/CA da usina: o kWp dos inversores com potência informada
    /// sobre a soma dessas potências (inversor sem kW não entra nos dois lados).
    /// </summary>
    public double? DcAcRatio => PowerKw > 0 && PowerKwpWithKw is { } kwp ? kwp / PowerKw : null;
}

public static class InverterTable
{
    /// <summary>
    /// O limite de strings escrito na caixa (07/10/2026): vazio é sem limite
    /// (todas as entradas); senão um inteiro de 1 para cima.
    /// </summary>
    public static (int? Target, string? Problem) ParseTarget(string? text)
    {
        var t = text?.Trim() ?? string.Empty;
        if (t.Length == 0) return (null, null);
        return int.TryParse(t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1
            ? (n, null)
            : (null, Tr.T("o limite é de 1 string para cima (vazio: todas as entradas)"));
    }

    /// <summary>
    /// As linhas, na ordem do cadastro. As strings vêm da contagem pelo
    /// vínculo (<see cref="StringAllocation.CountByInverter"/>, a mesma do
    /// aviso de excesso); o kWp, do resumo (<see cref="SystemSummary.AllInverters"/>),
    /// ou null em todas se o resumo não veio.
    /// </summary>
    public static IReadOnlyList<InverterTableRow> Rows(
        IReadOnlyList<Inverter> inverters,
        IReadOnlyList<InverterModel> models,
        IReadOnlyDictionary<Guid, int> stringsByInverter,
        IEnumerable<InverterSummary>? summary)
    {
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(stringsByInverter);

        var modelos = models.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        var potencia = summary?.GroupBy(s => s.Inverter.Id).ToDictionary(g => g.Key, g => g.First().PowerKwp);

        return inverters.Select(i =>
        {
            var modelo = modelos.GetValueOrDefault(i.Model);
            double? kwp = potencia is null ? null : potencia.GetValueOrDefault(i.Id);
            return new InverterTableRow(i, modelo, stringsByInverter.GetValueOrDefault(i.Id), modelo?.TotalInputs ?? 0, kwp);
        }).ToList();
    }

    /// <summary>A soma das linhas (o kWp null se alguma linha não tem).</summary>
    public static InverterTableTotal Total(IReadOnlyList<InverterTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        double? kwp = rows.All(r => r.PowerKwp is not null) ? rows.Sum(r => r.PowerKwp!.Value) : null;
        double? comKw = kwp is null ? null : rows.Where(r => r.PowerKw > 0).Sum(r => r.PowerKwp!.Value);
        return new InverterTableTotal(rows.Count, rows.Sum(r => r.Strings), rows.Sum(r => r.Capacity), kwp, rows.Sum(r => r.PowerKw), comKw);
    }
}
