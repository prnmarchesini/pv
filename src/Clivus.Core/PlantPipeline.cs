using System.Globalization;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>A usina inteira processada: todas as fileiras da distribuição.</summary>
/// <param name="Layout">A distribuição em planta (5.1).</param>
/// <param name="Rows">As fileiras processadas, na ordem da distribuição.</param>
/// <param name="ModulesPerTable">Quantos módulos cada mesa tem.</param>
/// <param name="ModulePowerWatts">A potência de um módulo, em Wp.</param>
/// <param name="Elapsed">Quanto tempo o processamento levou (sem o desenho).</param>
/// <param name="ModulesByKind">
/// Com mais de um tipo de mesa (8.6), os módulos de cada tipo, pelo
/// <see cref="PlacedTable.Kind"/>; null quando há um só.
/// </param>
/// <param name="PowerByKind">A potência do módulo de cada tipo, em Wp; null quando há um só.</param>
public sealed record ProcessedPlant(
    PlanLayout Layout,
    IReadOnlyList<ProcessedRow> Rows,
    int ModulesPerTable,
    double ModulePowerWatts,
    TimeSpan Elapsed,
    IReadOnlyList<int>? ModulesByKind = null,
    IReadOnlyList<double>? PowerByKind = null)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Todas as mesas, fileira a fileira.</summary>
    public IReadOnlyList<ProcessedTable> Tables => Rows.SelectMany(r => r.Tables).ToList();

    /// <summary>Quantas mesas foram marcadas pela fileira.</summary>
    public int MarkedCount => Rows.Sum(r => r.MarkedCount);

    /// <summary>Quantos pilares estouraram.</summary>
    public int PillarProblemCount => Rows.Sum(r => r.PillarProblemCount);

    /// <summary>Quantos pilares há no total.</summary>
    public int PillarCount => Tables.Sum(t => t.Pillars.Pillars.Count);

    /// <summary>Quantos módulos há no total: a soma dos de cada mesa (regra sagrada 3: a contagem é a soma).</summary>
    public int ModuleCount => ModulesByKind is null
        ? Tables.Count * ModulesPerTable
        : Tables.Sum(t => ModulesByKind[t.Cell.Kind]);

    /// <summary>A potência instalada, em kWp: a soma das potências dos módulos.</summary>
    public double PowerKwp => ModulesByKind is null || PowerByKind is null
        ? ModuleCount * ModulePowerWatts / 1000
        : Tables.Sum(t => ModulesByKind[t.Cell.Kind] * PowerByKind[t.Cell.Kind]) / 1000;

    /// <summary>Quantas mesas de cada tipo (só com mais de um tipo; com um, a lista tem um item).</summary>
    public IReadOnlyList<int> TablesByKind =>
        Enumerable.Range(0, ModulesByKind?.Count ?? 1).Select(k => Tables.Count(t => t.Cell.Kind == k)).ToList();

    /// <summary>Os avisos de todas as fileiras.</summary>
    public IReadOnlyList<string> Warnings => Rows.SelectMany(r => r.Warnings).ToList();

    /// <summary>A linha que descreve a usina para o usuário.</summary>
    public string Describe() =>
        $"{Rows.Count} fileira(s), {Tables.Count} mesa(s), {ModuleCount} módulo(s), "
        + $"{PowerKwp.ToString("0.#", Brasil)} kWp, {PillarCount} pilar(es); "
        + $"{MarkedCount} mesa(s) marcada(s), {PillarProblemCount} pilar(es) com problema, "
        + $"{Layout.DroppedOutside} posição(ões) descartada(s) por passar da área; "
        + $"{Elapsed.TotalSeconds.ToString("0.0", Brasil)} s";
}

/// <summary>
/// A área inteira: todas as fileiras da distribuição, uma a uma, com o
/// tempo medido. É o passo 5.9, do lado do motor; o comando mede o desenho
/// à parte.
///
/// As fileiras são independentes entre si (o plano diz que as pontas baixas
/// de fileiras vizinhas não precisam casar), então cada uma é a mesma
/// conta do 5.8, na ordem. Uma exceção numa fileira sobe e derruba o
/// processamento inteiro, de propósito: o que o terreno tem de errado vira
/// marca (mesa sem terreno, pilar fora de escala); exceção é defeito nosso,
/// e esconder defeito atrás de "marcada" seria mentir.
/// </summary>
public static class PlantPipeline
{
    /// <summary>
    /// Processa todas as fileiras.
    /// </summary>
    /// <param name="layout">A distribuição em planta.</param>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="tiltRadians">A inclinação transversal.</param>
    /// <param name="modulesPerTable">Quantos módulos cada mesa tem.</param>
    /// <param name="modulePowerWatts">A potência de um módulo, em Wp.</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="settings">Configuração e regras.</param>
    /// <param name="progress">Chamado depois de cada fileira, com quantas já foram; para a tela dizer que está vivo.</param>
    public static ProcessedPlant ProcessAll(
        PlanLayout layout,
        TableGeometry geometry,
        double tiltRadians,
        int modulesPerTable,
        double modulePowerWatts,
        Tin terrain,
        ProjectSettings settings,
        Action<int, int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        if (modulesPerTable <= 0)
            throw new ArgumentOutOfRangeException(nameof(modulesPerTable), modulesPerTable, "A mesa precisa de módulos.");

        if (!double.IsFinite(modulePowerWatts) || modulePowerWatts <= 0)
            throw new ArgumentOutOfRangeException(nameof(modulePowerWatts), modulePowerWatts, "A potência do módulo não é válida.");

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var fileiras = new List<ProcessedRow>(layout.Rows.Count);

        foreach (var fileira in layout.Rows)
        {
            fileiras.Add(RowPipeline.ProcessRow(fileira, geometry, tiltRadians, terrain, settings));
            progress?.Invoke(fileiras.Count, layout.Rows.Count);
        }

        relogio.Stop();

        return new ProcessedPlant(layout, fileiras, modulesPerTable, modulePowerWatts, relogio.Elapsed);
    }

    /// <summary>
    /// O mesmo com mais de um tipo de mesa (passo 8.6): cada mesa da
    /// distribuição usa a geometria, os módulos e a potência do tipo dela
    /// (<see cref="PlacedTable.Kind"/>). A inclinação é uma só.
    /// </summary>
    /// <summary>
    /// O teste da mesa no terreno para a distribuição (<see cref="RowDistributor.Distribute(IReadOnlyList{Point3}, IReadOnlyList{Point3}, LineSide, double, double, IReadOnlyList{TableFootprint}, IReadOnlyList{int}, double, Func{PlacedTable, bool}?)"/>):
    /// a mesa resolvida sozinha no terreno não fica marcada (sem módulo
    /// enterrado, desde 03/10/2026) nem tem pilar com problema. Na fileira, com
    /// as juntas fechadas com as vizinhas (regra 6), ela ainda pode ficar
    /// marcada: aí sai magenta.
    /// </summary>
    public static Func<PlacedTable, bool> FitsOnTerrain(
        IReadOnlyList<TableGeometry> geometries, double tiltRadians, Tin terrain, ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        return celula =>
        {
            try
            {
                var sozinha = RowPipeline.ProcessRow(new PlanRow(celula.Row, [celula]), [geometries[celula.Kind]], tiltRadians, terrain, settings);
                return sozinha.MarkedCount == 0 && sozinha.Tables.All(t => t.Pillars.ProblemCount == 0);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        };
    }

    public static ProcessedPlant ProcessAll(
        PlanLayout layout,
        IReadOnlyList<TableGeometry> geometries,
        double tiltRadians,
        IReadOnlyList<int> modulesByKind,
        IReadOnlyList<double> powerByKind,
        Tin terrain,
        ProjectSettings settings,
        Action<int, int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(geometries);
        ArgumentNullException.ThrowIfNull(modulesByKind);
        ArgumentNullException.ThrowIfNull(powerByKind);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);

        if (geometries.Count == 0 || modulesByKind.Count != geometries.Count || powerByKind.Count != geometries.Count)
            throw new ArgumentException("Uma geometria, um número de módulos e uma potência por tipo de mesa.", nameof(geometries));

        if (modulesByKind.Any(m => m <= 0))
            throw new ArgumentOutOfRangeException(nameof(modulesByKind), "Toda mesa precisa de módulos.");

        if (powerByKind.Any(w => !double.IsFinite(w) || w <= 0))
            throw new ArgumentOutOfRangeException(nameof(powerByKind), "A potência do módulo não é válida.");

        if (layout.Tables.Any(t => t.Kind < 0 || t.Kind >= geometries.Count))
            throw new ArgumentException("A distribuição tem mesa de um tipo que não veio.", nameof(layout));

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var fileiras = new List<ProcessedRow>(layout.Rows.Count);

        foreach (var fileira in layout.Rows)
        {
            // A mesa fica onde a distribuição pôs, do tipo que ela escolheu,
            // mesmo que não dê no terreno: fica marcada (enterrada) e o
            // usuário decide. Renan, 02/10/2026, com print de fileiras
            // esburacadas pela troca de 28 por 14: "isso não pode acontecer,
            // buracos; é melhor colocar a mesa e deixar ela enterrada e aí
            // eu vejo o que faço".
            fileiras.Add(RowPipeline.ProcessRow(fileira, fileira.Tables.Select(t => geometries[t.Kind]).ToList(), tiltRadians, terrain, settings));
            progress?.Invoke(fileiras.Count, layout.Rows.Count);
        }

        relogio.Stop();

        return new ProcessedPlant(layout, fileiras, modulesByKind[0], powerByKind[0], relogio.Elapsed, modulesByKind, powerByKind);
    }
}
