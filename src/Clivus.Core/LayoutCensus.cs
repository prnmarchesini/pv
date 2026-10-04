using System.Globalization;

namespace Clivus.Core;

/// <summary>Uma mesa como está no desenho, para a contagem.</summary>
/// <param name="Identity">A identidade do contorno, ou null se o contorno sumiu (peças órfãs).</param>
/// <param name="Contours">Quantos contornos têm este GUID: 1 normalmente; 0 numa órfã; 2 ou mais numa cópia sem identidade própria.</param>
/// <param name="Modules">Quantos blocos de módulo apontam para ela.</param>
/// <param name="PillarLengths">Os comprimentos (P1) dos pilares que têm comprimento, em metro.</param>
/// <param name="PillarsWithoutLength">Quantos pilares não têm comprimento (problema).</param>
public sealed record CountedTable(TableIdentity? Identity, int Contours, int Modules, IReadOnlyList<double> PillarLengths, int PillarsWithoutLength);

/// <summary>
/// A contagem da usina como ela ESTÁ no desenho (7.6, "recontar"): não o
/// que o motor calculou, mas o que sobrou depois de apagar, copiar e
/// recalcular. É por isso que a fonte é o XData das entidades, e não o
/// resultado de um processamento.
/// </summary>
/// <param name="Tables">Contornos de mesa (uma mesa duplicada conta pelos contornos dela).</param>
/// <param name="Duplicated">GUIDs de mesa com mais de um contorno (cópia sem identidade própria).</param>
/// <param name="Orphans">Mesas sem contorno (só peças).</param>
/// <param name="OrphanModules">Módulos das mesas sem contorno.</param>
/// <param name="Dirty">Mesas sujas.</param>
/// <param name="Marked">Mesas que não cabem no terreno.</param>
/// <param name="Modules">Blocos de módulo, no total.</param>
/// <param name="Pillars">Pilares, no total.</param>
/// <param name="PillarsWithoutLength">Pilares sem comprimento.</param>
/// <param name="PillarLengths">Todos os comprimentos de pilar, em metro.</param>
/// <param name="PowerKwp">A potência instalada, em kWp: a soma, mesa a mesa, de módulos × potência do módulo da mesa.</param>
/// <param name="TablesWithoutPower">Mesas sem potência gravada (de antes do 7.6), que usaram a potência do perfil atual.</param>
/// <param name="FallbackPowerWatts">A potência do perfil atual, em W, usada nessas mesas.</param>
public sealed record LayoutCensus(
    int Tables,
    int Duplicated,
    int Orphans,
    int OrphanModules,
    int Dirty,
    int Marked,
    int Modules,
    int Pillars,
    int PillarsWithoutLength,
    IReadOnlyList<double> PillarLengths,
    double PowerKwp,
    int TablesWithoutPower,
    double FallbackPowerWatts)
{
    /// <summary>Conta.</summary>
    /// <param name="fallbackPowerWatts">A potência do módulo do perfil atual, para as mesas que não têm a sua gravada.</param>
    /// <exception cref="ArgumentOutOfRangeException">Potência de reserva que não é um número positivo.</exception>
    public static LayoutCensus Count(IReadOnlyList<CountedTable> tables, double fallbackPowerWatts)
    {
        ArgumentNullException.ThrowIfNull(tables);

        if (!double.IsFinite(fallbackPowerWatts) || fallbackPowerWatts <= 0)
            throw new ArgumentOutOfRangeException(nameof(fallbackPowerWatts), fallbackPowerWatts, "A potência do módulo não é válida.");

        var comprimentos = tables.SelectMany(t => t.PillarLengths).Where(double.IsFinite).ToList();
        var semComprimento = tables.Sum(t => t.PillarsWithoutLength) + tables.Sum(t => t.PillarLengths.Count(l => !double.IsFinite(l)));

        var semPotencia = 0;
        var watts = 0.0;

        foreach (var t in tables)
        {
            var potencia = t.Identity?.ModulePowerWatts;

            if (potencia is null && t.Identity is not null) semPotencia++;

            watts += t.Modules * (potencia ?? fallbackPowerWatts);
        }

        return new LayoutCensus(
            tables.Sum(t => t.Contours),
            tables.Count(t => t.Contours > 1),
            tables.Count(t => t.Identity is null),
            tables.Where(t => t.Identity is null).Sum(t => t.Modules),
            tables.Count(t => t.Identity is { Dirty: true }),
            tables.Count(t => t.Identity is { Marked: true }),
            tables.Sum(t => t.Modules),
            comprimentos.Count + semComprimento,
            semComprimento,
            comprimentos,
            watts / 1000.0,
            semPotencia,
            fallbackPowerWatts);
    }

    /// <summary>As linhas do relatório, em português.</summary>
    public IReadOnlyList<string> Lines()
    {
        var linhas = new List<string>
        {
            Tr.F("{0} mesa(s), {1} módulo(s), {2:0.#} kWp", Tables, Modules, PowerKwp),
            Tr.F("{0} pilar(es)", Pillars) + (PillarsWithoutLength > 0 ? Tr.F(", {0} sem comprimento", PillarsWithoutLength) : string.Empty)
                + (PillarLengths.Count > 0
                    ? Tr.F("; comprimento de {0:0.00} a {1:0.00} m, média {2:0.00} m", PillarLengths.Min(), PillarLengths.Max(), PillarLengths.Average())
                    : string.Empty),
        };

        if (Dirty > 0 || Marked > 0 || Orphans > 0)
        {
            linhas.Add(
                OrphanModules > 0
                    ? Tr.F("{0} pendente(s), {1} que não cabe(m) no terreno, {2} com peças órfãs (sem contorno; {3} módulo(s) órfão(s) contado(s) acima)", Dirty, Marked, Orphans, OrphanModules)
                    : Tr.F("{0} pendente(s), {1} que não cabe(m) no terreno, {2} com peças órfãs (sem contorno)", Dirty, Marked, Orphans));
        }

        if (Duplicated > 0)
            linhas.Add(Tr.F("ATENÇÃO: {0} mesa(s) com mais de um contorno na mesma identidade (cópia sem identidade própria): use o Regerar área", Duplicated));

        if (TablesWithoutPower > 0)
            linhas.Add(Tr.F("ATENÇÃO: {0} mesa(s) sem potência gravada (desenhadas antes do 7.6): usaram {1:N0} W do perfil atual", TablesWithoutPower, FallbackPowerWatts));

        return linhas;
    }
}
