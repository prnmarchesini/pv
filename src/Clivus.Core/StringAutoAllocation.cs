namespace Clivus.Core;

/// <summary>
/// A varredura da atribuição automática das strings nos inversores (pedido
/// do Renan em 05/10/2026): o sentido que avança (de cima para baixo, de
/// baixo para cima, da esquerda para a direita ou da direita para a esquerda)
/// e o sentido dentro da faixa (o perpendicular). É uma configuração própria,
/// gravada separada da varredura da numeração: esta atribui as strings, a da
/// numeração dá as tags. A ordem é a mesma regra (<see cref="ScanOrder"/>).
/// </summary>
public sealed record AllocationScan(ScanDirection Direction, ScanDirection Cross)
{
    public const int FieldCount = 2;

    /// <summary>De cima para baixo e, em cada faixa, da esquerda para a direita.</summary>
    public static AllocationScan Default { get; } = new(ScanDirection.TopToBottom, ScanDirection.LeftToRight);

    public bool IsValid => Enum.IsDefined(Direction) && Enum.IsDefined(Cross) && ScanOrder.IsPerpendicular(Direction, Cross);

    /// <summary>"de cima para baixo; na faixa, da esquerda para a direita".</summary>
    public string Describe() => Tr.F("{0}; na faixa, {1}", ScanOrder.Describe(Direction), ScanOrder.Describe(Cross));

    public IReadOnlyList<string> ToFields() => [Direction.ToString(), Cross.ToString()];

    public static AllocationScan? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Count < FieldCount || int.TryParse(c[0], out _) || int.TryParse(c[1], out _)) return null;
        if (!Enum.TryParse<ScanDirection>(c[0], out var sentido) || !Enum.TryParse<ScanDirection>(c[1], out var faixa)) return null;

        var v = new AllocationScan(sentido, faixa);
        return v.IsValid ? v : null;
    }
}

/// <summary>
/// O que a atribuição automática dá (nada é gravado aqui).
/// </summary>
/// <param name="Changed">As strings livres que ganharam inversor (já com ele), na ordem da varredura.</param>
/// <param name="Added">Quantas strings cada inversor ganhou (só os que ganharam).</param>
/// <param name="Leftover">Strings livres que sobraram: os inversores encheram antes.</param>
/// <param name="Unplaced">Strings livres sem posição (o primeiro módulo não está no desenho): ficam livres.</param>
/// <param name="Duplicates">Entidades livres com o GUID de outra (cópia da polilinha): ficam de fora.</param>
/// <param name="Full">Inversores que já estavam cheios (pulados).</param>
/// <param name="WithoutModel">Inversores sem modelo no cadastro: sem capacidade conhecida, pulados.</param>
public sealed record AutoAllocationResult(
    IReadOnlyList<ElectricalString> Changed,
    IReadOnlyDictionary<Guid, int> Added,
    int Leftover,
    int Unplaced,
    int Duplicates,
    IReadOnlyList<Guid> Full,
    IReadOnlyList<Guid> WithoutModel);

/// <summary>
/// A atribuição automática das strings nos inversores (pedido do Renan em
/// 05/10/2026): as strings LIVRES na ordem da varredura enchem os inversores
/// na ordem da lista do cadastro, cada um até a capacidade do modelo (o
/// total de entradas). As já alocadas não mudam e contam na capacidade do
/// inversor delas; inversor cheio é pulado. Uma string recebe no máximo um
/// inversor (regra elétrica 2): só as livres entram, e cada uma uma vez.
/// A que aponta para inversor que não está mais no cadastro conta como livre
/// (a mesma regra da alocação manual).
/// </summary>
public static class StringAutoAllocation
{
    public static AutoAllocationResult Allocate(
        IReadOnlyList<Inverter> inverters,
        IReadOnlyList<InverterModel> models,
        IReadOnlyList<ElectricalString> strings,
        IReadOnlyDictionary<Guid, ModuleSpot> modules,
        AllocationScan scan)
    {
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(scan);
        if (!scan.IsValid) throw new ArgumentException("Varredura com os dois sentidos no mesmo eixo.", nameof(scan));

        // GUID repetido (a polilinha copiada leva o XData junto): fica de fora,
        // senão gravar numa regravaria a outra.
        var repetidos = strings.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        // Livre: sem inversor, ou apontando para inversor que não está mais no
        // cadastro (como na alocação manual, StringAllocation.IsLockedFor: não
        // há de quem soltá-la).
        var cadastrados = inverters.Select(i => i.Id).ToHashSet();
        bool Livre(ElectricalString s) => !StringAllocation.IsLockedFor(s, Guid.Empty, cadastrados);
        var copias = strings.Count(s => repetidos.Contains(s.Id) && Livre(s));

        // A carga de cada inversor: as strings que já são dele (cada GUID uma vez).
        var carga = StringAllocation.CountByInverter(strings.DistinctBy(s => s.Id)).ToDictionary(x => x.Key, x => x.Value);

        var livres = strings.Where(s => Livre(s) && !repetidos.Contains(s.Id)).ToList();
        var porId = livres.ToDictionary(s => s.Id);
        var aVarrer = new List<ScanItem>(livres.Count);
        foreach (var s in livres)
            if (s.Modules.Count > 0 && modules.TryGetValue(s.Modules[0], out var lugar)) aVarrer.Add(new ScanItem(s.Id, lugar.X, lugar.Y, lugar.Table));

        var fila = ScanOrder.Order(aVarrer, scan.Direction, ScanOrder.Band, scan.Cross);
        var semPosicao = livres.Count - fila.Count;

        var capacidade = models.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First().TotalInputs);
        var mudam = new List<ElectricalString>();
        var ganharam = new Dictionary<Guid, int>();
        var cheios = new List<Guid>();
        var semModelo = new List<Guid>();
        var proxima = 0;

        foreach (var inversor in inverters.DistinctBy(i => i.Id))
        {
            if (!capacidade.TryGetValue(inversor.Model, out var entradas))
            {
                semModelo.Add(inversor.Id);
                continue;
            }

            // A meta do inversor (07/10/2026), quando há, limita antes das entradas.
            var cabe = inversor.Limit(entradas) - carga.GetValueOrDefault(inversor.Id);
            if (cabe <= 0)
            {
                cheios.Add(inversor.Id);
                continue;
            }

            var leva = Math.Min(cabe, fila.Count - proxima);
            for (var k = 0; k < leva; k++) mudam.Add(porId[fila[proxima + k]] with { Inverter = inversor.Id });
            if (leva > 0) ganharam[inversor.Id] = leva;
            proxima += leva;
        }

        return new AutoAllocationResult(mudam, ganharam, fila.Count - proxima, semPosicao, copias, cheios, semModelo);
    }
}
