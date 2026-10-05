namespace Clivus.Core;

// O resumo do sistema elétrico (elétrica, 16.1; plano/eletrica/etapas/
// etapa-16-resumo.md): subestações -> trafos -> inversores -> strings ->
// módulos e potência, consolidado SÓ pelos vínculos guardados em dado
// (Transformer.ConsumerUnit, Inverter.Transformer, ElectricalString.Inverter),
// nunca pela posição dos retângulos no desenho (regra elétrica 1).

/// <summary>Um inversor no resumo: as strings que apontam para ele, os módulos delas e a potência.</summary>
/// <param name="Model">O modelo do cadastro, ou null se ele não está lá (capacidade desconhecida).</param>
/// <param name="Capacity">As entradas do modelo (MPPT × entradas por MPPT); 0 se o modelo não está no cadastro.</param>
/// <param name="TransformerMissing">O inversor aponta para um trafo que não está no cadastro.</param>
public sealed record InverterSummary(Inverter Inverter, InverterModel? Model, int Strings, int Modules, double PowerKwp, int Capacity, bool TransformerMissing)
{
    /// <summary>Mais strings que entradas (regra elétrica 6: aviso vermelho).</summary>
    public bool OverCapacity => Model is not null && Strings > Capacity;
}

/// <summary>Um trafo no resumo, com os inversores do skid dele.</summary>
/// <param name="UnitMissing">O trafo aponta para uma subestação que não está no cadastro.</param>
public sealed record TransformerSummary(Transformer Transformer, IReadOnlyList<InverterSummary> Inverters, bool UnitMissing)
{
    public int Strings => Inverters.Sum(i => i.Strings);

    public int Modules => Inverters.Sum(i => i.Modules);

    public double PowerKwp => Inverters.Sum(i => i.PowerKwp);
}

/// <summary>Uma subestação (UC) no resumo, com os trafos dela.</summary>
public sealed record UnitSummary(ConsumerUnit Unit, IReadOnlyList<TransformerSummary> Transformers)
{
    public int Strings => Transformers.Sum(t => t.Strings);

    public int Modules => Transformers.Sum(t => t.Modules);

    public double PowerKwp => Transformers.Sum(t => t.PowerKwp);
}

/// <summary>O tipo de uma linha da tabela do resumo.</summary>
public enum SummaryRowKind
{
    Unit,
    Transformer,
    Inverter,

    /// <summary>Cabeçalho de grupo: trafos sem subestação, inversores sem trafo.</summary>
    Group,

    /// <summary>As strings sem inversor.</summary>
    Free,

    Total,
}

/// <summary>Uma linha da tabela do resumo (a mesma na janela e na linha de comando).</summary>
/// <param name="Level">A indentação: 0 subestação ou grupo, 1 trafo, 2 inversor.</param>
/// <param name="Warning">Linha que precisa de atenção (excesso de capacidade, elo quebrado).</param>
public sealed record SummaryRow(SummaryRowKind Kind, int Level, string Name, string Detail, int Strings, int Modules, double PowerKwp, string Note, bool Warning);

/// <summary>
/// O resumo do sistema (16.1): a árvore pelos vínculos, os totais e as
/// pendências. Nada falha calado (regra elétrica 6): elo que aponta para
/// cadastro que não existe, módulo que não está no desenho, potência que
/// faltou, string livre, inversor acima da capacidade, tudo é contado.
/// </summary>
public sealed record SystemSummary(
    IReadOnlyList<UnitSummary> Units,
    IReadOnlyList<TransformerSummary> TransformersWithoutUnit,
    IReadOnlyList<InverterSummary> InvertersWithoutTransformer,
    int UnitCount,
    int TransformerCount,
    int InverterCount,
    int StringCount,
    int AllocatedStrings,
    int FreeStrings,
    int Modules,
    double PowerKwp,
    int FreeModules,
    double FreePowerKwp,
    int StringsWithUnknownInverter,
    int InvertersWithUnknownModel,
    int InvertersWithUnknownTransformer,
    int TransformersWithUnknownUnit,
    int ModulesNotInDrawing,
    int ModulesWithFallbackPower,
    int ModulesWithoutPower,
    int AllocatedWithoutTag,
    int DuplicateStrings = 0,
    int ModulesInMoreThanOneString = 0,
    int ModulesWithoutTable = 0)
{
    /// <summary>Todos os inversores, na ordem da árvore.</summary>
    public IEnumerable<InverterSummary> AllInverters =>
        Units.SelectMany(u => u.Transformers).Concat(TransformersWithoutUnit).SelectMany(t => t.Inverters).Concat(InvertersWithoutTransformer);

    /// <summary>Os inversores acima da capacidade.</summary>
    public IReadOnlyList<InverterSummary> OverCapacity => AllInverters.Where(i => i.OverCapacity).ToList();

    /// <summary>As linhas da tabela: cada subestação com os trafos e inversores dela; depois os trafos sem subestação, os inversores sem trafo, as strings livres e o total.</summary>
    public IReadOnlyList<SummaryRow> Rows()
    {
        var linhas = new List<SummaryRow>();

        void Inversor(InverterSummary i, int nivel)
        {
            var nota = i.Model is null
                ? Tr.T("modelo fora do cadastro: capacidade desconhecida")
                : i.OverCapacity
                    ? Tr.F("ACIMA DA CAPACIDADE: {0} strings em {1} entradas", i.Strings, i.Capacity)
                    : Tr.F("{0} de {1} entradas", i.Strings, i.Capacity);
            if (i.TransformerMissing) nota += "; " + Tr.T("o trafo dele não está no cadastro");

            linhas.Add(new SummaryRow(SummaryRowKind.Inverter, nivel, i.Inverter.Name, i.Model?.Name ?? string.Empty, i.Strings, i.Modules, i.PowerKwp, nota, i.Model is null || i.OverCapacity || i.TransformerMissing));
        }

        void Trafo(TransformerSummary t, int nivel)
        {
            var nota = Tr.F("{0} inversor(es)", t.Inverters.Count) + (t.UnitMissing ? "; " + Tr.T("a subestação dele não está no cadastro") : string.Empty);
            linhas.Add(new SummaryRow(SummaryRowKind.Transformer, nivel, t.Transformer.Nickname, Tr.F("{0:0.##} kVA", t.Transformer.PowerKva), t.Strings, t.Modules, t.PowerKwp, nota, t.UnitMissing));
            foreach (var i in t.Inverters) Inversor(i, nivel + 1);
        }

        foreach (var u in Units)
        {
            linhas.Add(new SummaryRow(SummaryRowKind.Unit, 0, u.Unit.Code, u.Unit.Name, u.Strings, u.Modules, u.PowerKwp, Tr.F("{0} trafo(s)", u.Transformers.Count), false));
            foreach (var t in u.Transformers) Trafo(t, 1);
        }

        if (TransformersWithoutUnit.Count > 0)
        {
            linhas.Add(new SummaryRow(SummaryRowKind.Group, 0, Tr.T("Trafos sem subestação"), string.Empty,
                TransformersWithoutUnit.Sum(t => t.Strings), TransformersWithoutUnit.Sum(t => t.Modules), TransformersWithoutUnit.Sum(t => t.PowerKwp),
                Tr.F("{0} trafo(s)", TransformersWithoutUnit.Count), true));
            foreach (var t in TransformersWithoutUnit) Trafo(t, 1);
        }

        if (InvertersWithoutTransformer.Count > 0)
        {
            linhas.Add(new SummaryRow(SummaryRowKind.Group, 0, Tr.T("Inversores sem trafo"), string.Empty,
                InvertersWithoutTransformer.Sum(i => i.Strings), InvertersWithoutTransformer.Sum(i => i.Modules), InvertersWithoutTransformer.Sum(i => i.PowerKwp),
                Tr.F("{0} inversor(es)", InvertersWithoutTransformer.Count), true));
            foreach (var i in InvertersWithoutTransformer) Inversor(i, 1);
        }

        if (FreeStrings > 0)
            linhas.Add(new SummaryRow(SummaryRowKind.Free, 0, Tr.T("Strings sem inversor"), string.Empty, FreeStrings, FreeModules, FreePowerKwp, Tr.T("fora dos totais"), true));

        linhas.Add(new SummaryRow(SummaryRowKind.Total, 0, Tr.T("Total alocado"), Tr.F("{0} subestação(ões), {1} trafo(s), {2} inversor(es)", UnitCount, TransformerCount, InverterCount),
            AllocatedStrings, Modules, PowerKwp, Tr.F("{0} string(s) no desenho", StringCount), false));

        return linhas;
    }

    /// <summary>As pendências, uma frase cada (vazia: nada pendente).</summary>
    public IReadOnlyList<string> Pending()
    {
        var p = new List<string>();
        if (FreeStrings > 0) p.Add(Tr.F("{0} string(s) sem inversor ({1} módulo(s), {2:0.00} kWp fora dos totais)", FreeStrings, FreeModules, FreePowerKwp));
        foreach (var i in OverCapacity) p.Add(Tr.F("{0} acima da capacidade: {1} strings em {2} entradas", i.Inverter.Name, i.Strings, i.Capacity));
        if (InvertersWithoutTransformer.Count > 0) p.Add(Tr.F("{0} inversor(es) sem trafo: {1}", InvertersWithoutTransformer.Count, string.Join(", ", InvertersWithoutTransformer.Select(i => i.Inverter.Name))));
        if (TransformersWithoutUnit.Count > 0) p.Add(Tr.F("{0} trafo(s) sem subestação: {1}", TransformersWithoutUnit.Count, string.Join(", ", TransformersWithoutUnit.Select(t => t.Transformer.Nickname))));
        if (StringsWithUnknownInverter > 0) p.Add(Tr.F("{0} string(s) apontam para inversor que não está no cadastro (fora dos totais)", StringsWithUnknownInverter));
        if (InvertersWithUnknownModel > 0) p.Add(Tr.F("{0} inversor(es) com modelo que não está no cadastro", InvertersWithUnknownModel));
        if (InvertersWithUnknownTransformer > 0) p.Add(Tr.F("{0} inversor(es) apontam para trafo que não está no cadastro", InvertersWithUnknownTransformer));
        if (TransformersWithUnknownUnit > 0) p.Add(Tr.F("{0} trafo(s) apontam para subestação que não está no cadastro", TransformersWithUnknownUnit));
        if (DuplicateStrings > 0) p.Add(Tr.F("{0} string(s) com a identidade repetida (polilinha copiada?), fora dos totais", DuplicateStrings));
        if (ModulesInMoreThanOneString > 0) p.Add(Tr.F("{0} módulo(s) em mais de uma string alocada (contados em cada uma)", ModulesInMoreThanOneString));
        if (ModulesNotInDrawing > 0) p.Add(Tr.F("{0} módulo(s) das strings não estão no desenho (sem potência)", ModulesNotInDrawing));
        if (ModulesWithoutTable > 0) p.Add(Tr.F("{0} módulo(s) das strings sem a mesa dona no desenho (sem potência)", ModulesWithoutTable));
        if (ModulesWithFallbackPower > 0) p.Add(Tr.F("{0} módulo(s) de mesa sem potência gravada usaram a do perfil atual", ModulesWithFallbackPower));
        if (ModulesWithoutPower > 0) p.Add(Tr.F("{0} módulo(s) sem potência conhecida ficaram fora do kWp", ModulesWithoutPower));
        if (AllocatedWithoutTag > 0) p.Add(Tr.F("{0} string(s) alocada(s) ainda sem tag (gere a numeração)", AllocatedWithoutTag));
        return p;
    }

    /// <summary>O resumo em texto, para a linha de comando: o total, a árvore indentada e as pendências.</summary>
    public IReadOnlyList<string> Lines()
    {
        var linhas = new List<string>
        {
            Tr.F("{0} subestação(ões), {1} trafo(s), {2} inversor(es); {3} string(s) alocada(s) de {4}, {5} módulo(s), {6:0.00} kWp",
                UnitCount, TransformerCount, InverterCount, AllocatedStrings, StringCount, Modules, PowerKwp),
        };

        foreach (var r in Rows().Where(r => r.Kind != SummaryRowKind.Total))
        {
            var nome = r.Detail.Length > 0 ? Tr.F("{0} ({1})", r.Name, r.Detail) : r.Name;
            linhas.Add(new string(' ', 2 + r.Level * 2) + Tr.F("{0}: {1} string(s), {2} módulo(s), {3:0.00} kWp; {4}", nome, r.Strings, r.Modules, r.PowerKwp, r.Note));
        }

        var pendencias = Pending();
        linhas.Add(pendencias.Count == 0 ? Tr.T("Nenhuma pendência.") : Tr.F("{0} pendência(s):", pendencias.Count));
        linhas.AddRange(pendencias.Select(p => "  - " + p));
        return linhas;
    }
}

/// <summary>A consolidação do resumo (16.1).</summary>
public static class ElectricalSummary
{
    /// <summary>
    /// Monta o resumo pelos vínculos.
    /// </summary>
    /// <param name="modulePowerWatts">
    /// A potência de cada módulo do desenho, em W, pela mesa dona
    /// (<see cref="TableIdentity.ModulePowerWatts"/>); null numa mesa sem
    /// potência gravada. Módulo ausente da lista não está no desenho.
    /// </param>
    /// <param name="fallbackWatts">A potência do perfil atual para as mesas sem potência gravada, ou null (esses módulos ficam fora do kWp, contados).</param>
    /// <param name="modulesWithoutTable">Módulos que estão no desenho mas cuja mesa dona não foi achada (sem potência; contados à parte).</param>
    /// <remarks>Os contadores de módulo (fora do desenho, reserva, sem potência) são só das strings alocadas, as dos totais.</remarks>
    public static SystemSummary Build(
        IReadOnlyList<ConsumerUnit> units,
        IReadOnlyList<Transformer> transformers,
        IReadOnlyList<InverterModel> models,
        IReadOnlyList<Inverter> inverters,
        IReadOnlyList<ElectricalString> strings,
        IReadOnlyDictionary<Guid, double?> modulePowerWatts,
        double? fallbackWatts,
        IReadOnlySet<Guid>? modulesWithoutTable = null)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(transformers);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(modulePowerWatts);

        var reserva = fallbackWatts is { } w && double.IsFinite(w) && w > 0 ? w : (double?)null;
        var modelos = models.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        var idsDeInversor = inverters.Select(i => i.Id).ToHashSet();
        var idsDeTrafo = transformers.Select(t => t.Id).ToHashSet();
        var idsDeUc = units.Select(u => u.Id).ToHashSet();

        var foraDoDesenho = 0;
        var semMesa = 0;
        var comReserva = 0;
        var semPotencia = 0;

        // A potência de uma string, em kWp; conta o que faltou (só das que entram nos totais).
        double Potencia(ElectricalString s, bool contar)
        {
            var watts = 0.0;
            foreach (var m in s.Modules)
            {
                if (!modulePowerWatts.TryGetValue(m, out var p))
                {
                    if (!contar) continue;
                    if (modulesWithoutTable?.Contains(m) == true) semMesa++;
                    else foraDoDesenho++;
                }
                else if (p is { } v && double.IsFinite(v) && v > 0) watts += v;
                else if (reserva is { } r)
                {
                    watts += r;
                    if (contar) comReserva++;
                }
                else if (contar) semPotencia++;
            }

            return watts / 1000.0;
        }

        // GUID repetido (polilinha copiada com o XData): nenhuma das cópias
        // entra nos totais, e a pendência diz quantas são.
        var porGuid = strings.GroupBy(s => s.Id).ToList();
        var duplicadas = porGuid.Where(g => g.Count() > 1).Sum(g => g.Count());
        var unicas = porGuid.Where(g => g.Count() == 1).Select(g => g.First()).ToList();
        var porInversor = new Dictionary<Guid, (int Strings, int Modulos, double Kwp)>();
        var livres = 0;
        var modulosLivres = 0;
        var kwpLivre = 0.0;
        var inversorFantasma = 0;
        var semTag = 0;

        var vistos = new HashSet<Guid>();
        var emDuas = new HashSet<Guid>();

        foreach (var s in unicas)
        {
            var nosTotais = s.IsAllocated && idsDeInversor.Contains(s.Inverter);
            var kwp = Potencia(s, nosTotais);

            if (nosTotais)
                foreach (var m in s.Modules)
                    if (!vistos.Add(m)) emDuas.Add(m);

            if (!s.IsAllocated)
            {
                livres++;
                modulosLivres += s.Modules.Count;
                kwpLivre += kwp;
            }
            else if (!idsDeInversor.Contains(s.Inverter))
            {
                inversorFantasma++;
            }
            else
            {
                var soma = porInversor.GetValueOrDefault(s.Inverter);
                porInversor[s.Inverter] = (soma.Strings + 1, soma.Modulos + s.Modules.Count, soma.Kwp + kwp);
                if (string.IsNullOrEmpty(s.Tag)) semTag++;
            }
        }

        // Inversores (cada GUID uma vez), agrupados pelo trafo do vínculo.
        var resumoDosInversores = inverters
            .GroupBy(i => i.Id).Select(g => g.First())
            .Select(i =>
            {
                var soma = porInversor.GetValueOrDefault(i.Id);
                var modelo = modelos.GetValueOrDefault(i.Model);
                var trafoSumido = i.Transformer != Guid.Empty && !idsDeTrafo.Contains(i.Transformer);
                return new InverterSummary(i, modelo, soma.Strings, soma.Modulos, soma.Kwp, modelo?.TotalInputs ?? 0, trafoSumido);
            })
            .ToList();

        var inversoresDoTrafo = resumoDosInversores.Where(i => idsDeTrafo.Contains(i.Inverter.Transformer)).ToLookup(i => i.Inverter.Transformer);
        var semTrafo = resumoDosInversores.Where(i => !idsDeTrafo.Contains(i.Inverter.Transformer)).ToList();

        var resumoDosTrafos = transformers
            .GroupBy(t => t.Id).Select(g => g.First())
            .Select(t => new TransformerSummary(t, inversoresDoTrafo[t.Id].ToList(), t.ConsumerUnit != Guid.Empty && !idsDeUc.Contains(t.ConsumerUnit)))
            .ToList();

        var trafosDaUc = resumoDosTrafos.Where(t => idsDeUc.Contains(t.Transformer.ConsumerUnit)).ToLookup(t => t.Transformer.ConsumerUnit);
        var semUc = resumoDosTrafos.Where(t => !idsDeUc.Contains(t.Transformer.ConsumerUnit)).ToList();

        var resumoDasUcs = units
            .GroupBy(u => u.Id).Select(g => g.First())
            .Select(u => new UnitSummary(u, trafosDaUc[u.Id].ToList()))
            .ToList();

        var alocadas = porInversor.Values.Sum(v => v.Strings);

        return new SystemSummary(
            resumoDasUcs,
            semUc,
            semTrafo,
            resumoDasUcs.Count,
            resumoDosTrafos.Count,
            resumoDosInversores.Count,
            unicas.Count + duplicadas,
            alocadas,
            livres,
            porInversor.Values.Sum(v => v.Modulos),
            porInversor.Values.Sum(v => v.Kwp),
            modulosLivres,
            kwpLivre,
            inversorFantasma,
            resumoDosInversores.Count(i => i.Model is null),
            resumoDosInversores.Count(i => i.TransformerMissing),
            resumoDosTrafos.Count(t => t.UnitMissing),
            foraDoDesenho,
            comReserva,
            semPotencia,
            semTag,
            duplicadas,
            emDuas.Count,
            semMesa);
    }
}
