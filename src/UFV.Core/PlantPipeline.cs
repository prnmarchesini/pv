using System.Globalization;
using UFV.Geo;

namespace UFV.Core;

/// <summary>A usina inteira processada: todas as fileiras da distribuição.</summary>
/// <param name="Layout">A distribuição em planta (5.1).</param>
/// <param name="Rows">As fileiras processadas, na ordem da distribuição.</param>
/// <param name="ModulesPerTable">Quantos módulos cada mesa tem.</param>
/// <param name="ModulePowerWatts">A potência de um módulo, em Wp.</param>
/// <param name="Elapsed">Quanto tempo o processamento levou (sem o desenho).</param>
public sealed record ProcessedPlant(
    PlanLayout Layout,
    IReadOnlyList<ProcessedRow> Rows,
    int ModulesPerTable,
    double ModulePowerWatts,
    TimeSpan Elapsed)
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

    /// <summary>Quantos módulos há no total: mesas × módulos por mesa (regra sagrada 3: a contagem é a soma).</summary>
    public int ModuleCount => Tables.Count * ModulesPerTable;

    /// <summary>A potência instalada, em kWp: a soma das potências dos módulos.</summary>
    public double PowerKwp => ModuleCount * ModulePowerWatts / 1000;

    /// <summary>Os avisos de todas as fileiras.</summary>
    public IReadOnlyList<string> Warnings => Rows.SelectMany(r => r.Warnings).ToList();

    /// <summary>A linha que descreve a usina para o usuário.</summary>
    public string Describe() =>
        $"{Rows.Count} fileira(s), {Tables.Count} mesa(s), {ModuleCount} módulo(s), "
        + $"{PowerKwp.ToString("0.#", Brasil)} kWp, {PillarCount} pilar(es); "
        + $"{MarkedCount} mesa(s) marcada(s), {PillarProblemCount} pilar(es) com problema, "
        + $"{Layout.PartlyOutsideCount} na borda, {Layout.SkippedForOverlap} pulada(s) por sobreposição; "
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
    /// <param name="side">De que lado da linha ficam as mesas.</param>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="tiltRadians">A inclinação transversal.</param>
    /// <param name="modulesPerTable">Quantos módulos cada mesa tem.</param>
    /// <param name="modulePowerWatts">A potência de um módulo, em Wp.</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="settings">Configuração e regras.</param>
    /// <param name="progress">Chamado depois de cada fileira, com quantas já foram; para a tela dizer que está vivo.</param>
    public static ProcessedPlant ProcessAll(
        PlanLayout layout,
        LineSide side,
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
            fileiras.Add(RowPipeline.ProcessRow(fileira, side, geometry, tiltRadians, terrain, settings));
            progress?.Invoke(fileiras.Count, layout.Rows.Count);
        }

        relogio.Stop();

        return new ProcessedPlant(layout, fileiras, modulesPerTable, modulePowerWatts, relogio.Elapsed);
    }
}
