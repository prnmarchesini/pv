using System.Globalization;

namespace UFV.Core;

/// <summary>
/// O resumo do terreno da aba Terreno (passo 8.15): a superfície escolhida,
/// a área, as cotas e onde ela está (cidade, país e fuso UTM SIRGAS 2000).
/// </summary>
public static class TerrainReport
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>As linhas do resumo.</summary>
    /// <param name="terreno">O terreno processado.</param>
    /// <param name="estado">O estado do carimbo ("Atual", "Desatualizado"), ou null.</param>
    /// <param name="lugar">A localização, ou null quando o desenho não sabe.</param>
    public static IReadOnlyList<string> Lines(TerrainSummary terreno, string? estado, GeoLocation? lugar)
    {
        ArgumentNullException.ThrowIfNull(terreno);

        var linhas = new List<string>
        {
            $"Terreno: {terreno.SurfaceName}" + (estado is null ? "" : $" ({estado})"),
            $"Área: {terreno.Hectares.ToString("N2", Brasil)} ha em planta ({terreno.Area2D.ToString("N0", Brasil)} m²), "
                + $"{(terreno.Area3D / 10_000).ToString("N2", Brasil)} ha na superfície",
            $"Cotas: {terreno.MinZ.ToString("N2", Brasil)} a {terreno.MaxZ.ToString("N2", Brasil)} m "
                + $"(desnível {terreno.Desnivel.ToString("N2", Brasil)} m)",
        };

        if (lugar is null || !lugar.IsValid || lugar.LooksUnset)
        {
            linhas.Add("Localização: não definida (defina com o botão Localização, ou dê um sistema de coordenadas ao desenho)");
            return linhas;
        }

        linhas.Add($"Localização: {lugar.Describe()}");

        var cidade = TerrainPlace.Nearest(lugar.Latitude, lugar.Longitude);
        var pais = TerrainPlace.Country(cidade);

        linhas.Add(pais is null
            ? $"Cidade: fora do Brasil (a sede brasileira mais perto, {cidade.City.Name} - {cidade.City.State}, fica a {cidade.DistanceKm.ToString("N0", Brasil)} km)"
            : $"Cidade: {cidade.City.Name} - {cidade.City.State} (sede a {cidade.DistanceKm.ToString("N1", Brasil)} km)");
        linhas.Add($"País: {pais ?? "não identificado"}");
        linhas.Add($"Fuso: {TerrainPlace.Utm(lugar.Latitude, lugar.Longitude).Describe()}");

        return linhas;
    }
}
