using System.Globalization;
using System.Reflection;

namespace UFV.Core;

/// <summary>Um município do Brasil (IBGE).</summary>
public sealed record Municipality(string Name, string State, double Latitude, double Longitude);

/// <summary>A cidade mais próxima e a que distância.</summary>
public sealed record NearestCity(Municipality City, double DistanceKm);

/// <summary>Um fuso UTM e, quando há, o código EPSG do SIRGAS 2000.</summary>
/// <param name="Zone">O número do fuso, 1 a 60.</param>
/// <param name="South">Hemisfério sul.</param>
/// <param name="SirgasEpsg">O EPSG do SIRGAS 2000 / UTM daquele fuso, ou null fora da América do Sul.</param>
public sealed record UtmZone(int Zone, bool South, int? SirgasEpsg)
{
    /// <summary>"23S".</summary>
    public string Label => $"{Zone}{(South ? "S" : "N")}";

    /// <summary>"SIRGAS 2000 / UTM zone 23S (EPSG:31983)", ou só o fuso fora da América do Sul.</summary>
    public string Describe() =>
        SirgasEpsg is { } epsg ? $"SIRGAS 2000 / UTM zone {Label} (EPSG:{epsg})" : $"UTM fuso {Label} (fora da América do Sul: datum a definir)";
}

/// <summary>
/// Onde o terreno está (passo 8.15, Melhorias.docx, 01/10/2026): "um resumo
/// mostrando qual a área do terreno, qual a cidade que ele se encontra, país
/// e fuso na UTM SIRGAS 2000 para o Brasil e América do Sul".
///
/// A cidade é o município do IBGE cuja sede está mais perto, sem internet;
/// a base vem embutida. Longe de toda sede brasileira (mais de
/// <see cref="BrazilRadiusKm"/>), o país não é afirmado.
/// </summary>
public static class TerrainPlace
{
    /// <summary>Até quantos km da sede mais próxima o lugar conta como Brasil.</summary>
    public const double BrazilRadiusKm = 60;

    private const string Arquivo = "UFV.Core.municipios.csv";

    private const double RaioDaTerraKm = 6371.0088;

    private static readonly Lazy<IReadOnlyList<Municipality>> Base = new(Ler, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Os municípios embutidos.</summary>
    public static IReadOnlyList<Municipality> Municipalities => Base.Value;

    /// <summary>O município com a sede mais perto do ponto.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Latitude ou longitude impossível.</exception>
    public static NearestCity Nearest(double latitude, double longitude)
    {
        Conferir(latitude, longitude);

        Municipality? melhor = null;
        var menor = double.MaxValue;

        foreach (var m in Municipalities)
        {
            var d = DistanceKm(latitude, longitude, m.Latitude, m.Longitude);
            if (d < menor)
            {
                menor = d;
                melhor = m;
            }
        }

        return new NearestCity(melhor!, menor);
    }

    /// <summary>"Brasil", ou null quando a sede mais perto está longe demais para afirmar.</summary>
    public static string? Country(NearestCity cidade) =>
        cidade.DistanceKm <= BrazilRadiusKm ? "Brasil" : null;

    /// <summary>O fuso UTM do ponto, com o EPSG do SIRGAS 2000 na América do Sul.</summary>
    public static UtmZone Utm(double latitude, double longitude)
    {
        Conferir(latitude, longitude);

        var fuso = (int)Math.Floor((longitude + 180) / 6) + 1;
        if (fuso > 60) fuso = 60;

        var sul = latitude < 0;

        // SIRGAS 2000 cobre a América do Sul com as ilhas (Fernando de Noronha
        // fica em -32°) e um pouco ao norte do
        // Equador): fusos 17 a 25. Os códigos EPSG são 31977 a 31985 no sul
        // (17S a 25S) e 31971 a 31976 no norte (17N a 22N).
        var naAmericaDoSul = latitude is >= -56 and <= 13 && longitude is >= -82 and <= -28;
        int? epsg = null;

        if (naAmericaDoSul)
        {
            if (sul && fuso is >= 17 and <= 25) epsg = 31960 + fuso;
            else if (!sul && fuso is >= 17 and <= 22) epsg = 31954 + fuso;
        }

        return new UtmZone(fuso, sul, epsg);
    }

    /// <summary>A distância em km pela fórmula de haversine.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double g) => g * Math.PI / 180;

        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * RaioDaTerraKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static void Conferir(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude fora de -90 a 90.");

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude fora de -180 a 180.");
    }

    private static IReadOnlyList<Municipality> Ler()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream(Arquivo)
            ?? throw new InvalidOperationException($"A base de municípios não está na DLL (recurso {Arquivo}).");
        using var leitor = new StreamReader(fluxo);

        var lista = new List<Municipality>(5600);

        while (leitor.ReadLine() is { } linha)
        {
            if (linha.Length == 0 || linha[0] == '#') continue;

            var c = linha.Split(';');
            if (c.Length != 4) continue;

            if (!double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                continue;

            lista.Add(new Municipality(c[0], c[1], lat, lon));
        }

        return lista;
    }
}
