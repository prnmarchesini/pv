namespace Clivus.Core;

/// <summary>
/// O resumo do terreno da aba Terreno (passo 8.15): a superfície escolhida,
/// a área, as cotas e onde ela está (cidade, país e fuso UTM SIRGAS 2000).
/// </summary>
public static class TerrainReport
{
    /// <summary>As linhas do resumo.</summary>
    /// <param name="terreno">O terreno processado.</param>
    /// <param name="estado">O estado do carimbo ("Atual", "Desatualizado"), ou null.</param>
    /// <param name="lugar">A localização, ou null quando o desenho não sabe.</param>
    public static IReadOnlyList<string> Lines(TerrainSummary terreno, string? estado, GeoLocation? lugar)
    {
        ArgumentNullException.ThrowIfNull(terreno);

        var linhas = new List<string>
        {
            Tr.F("Terreno: {0}", terreno.SurfaceName) + (estado is null ? "" : $" ({estado})"),
            Tr.F("Área: {0:N2} ha em planta ({1:N0} m²), {2:N2} ha na superfície", terreno.Hectares, terreno.Area2D, terreno.Area3D / 10_000),
            Tr.F("Cotas: {0:N2} a {1:N2} m (desnível {2:N2} m)", terreno.MinZ, terreno.MaxZ, terreno.Desnivel),
        };

        if (lugar is null || !lugar.IsValid || lugar.LooksUnset)
        {
            linhas.Add(Tr.T("Localização: não definida (defina com o botão Localização, ou dê um sistema de coordenadas ao desenho)"));
            return linhas;
        }

        linhas.Add(Tr.F("Localização: {0}", lugar.Describe()));

        var cidade = TerrainPlace.Nearest(lugar.Latitude, lugar.Longitude);
        var pais = TerrainPlace.Country(cidade);

        linhas.Add(pais is null
            ? Tr.F("Cidade: fora do Brasil (a sede brasileira mais perto, {0} - {1}, fica a {2:N0} km)", cidade.City.Name, cidade.City.State, cidade.DistanceKm)
            : Tr.F("Cidade: {0} - {1} (sede a {2:N1} km)", cidade.City.Name, cidade.City.State, cidade.DistanceKm));
        linhas.Add(Tr.F("País: {0}", pais is null ? Tr.T("não identificado") : Tr.T(pais)));
        linhas.Add(Tr.F("Fuso: {0}", TerrainPlace.Utm(lugar.Latitude, lugar.Longitude).Describe()));

        return linhas;
    }
}
