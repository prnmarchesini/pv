using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Clivus.Core;

/// <summary>Uma estação da base do Plúvio 2.1 (GPRH/UFV), com a IDF dela.</summary>
/// <param name="State">A sigla do estado.</param>
/// <param name="LatitudeSouth">Latitude em graus, positiva para o sul (como o Plúvio grava).</param>
/// <param name="LongitudeWest">Longitude em graus, positiva para o oeste.</param>
public sealed record PluvioStation(
    string State, string Name, double K, double A, double B, double C, string Source,
    double LatitudeSouth, double LongitudeWest);

/// <summary>Uma localidade (cidade ou distrito) do Plúvio, onde a IDF é interpolada.</summary>
public sealed record PluvioLocality(string State, string Name, double LatitudeSouth, double LongitudeWest);

/// <summary>Uma estação que entrou na interpolação, com a distância e o peso dela.</summary>
/// <param name="DistanceKm">Distância aproximada até o local, em km (só para mostrar).</param>
/// <param name="Share">A fração do peso total que ela teve (0 a 1).</param>
public sealed record PluvioNeighbor(PluvioStation Station, double DistanceKm, double Share);

/// <summary>K, a, b e c interpolados para um local, e as estações que mais pesaram.</summary>
public sealed record PluvioEstimate(double K, double A, double B, double C, IReadOnlyList<PluvioNeighbor> Nearest)
{
    /// <summary>A frase do campo "Chuva (IDF)" da Memória.</summary>
    /// <param name="place">A localidade escolhida ("Valparaíso-SP"); null = o local do desenho.</param>
    public string Source(string? place = null)
    {
        var vizinhas = Nearest
            .Take(3)
            .Select(v => Tr.F("{0}-{1} {2} km ({3} %)", v.Station.Name, v.Station.State,
                v.DistanceKm.ToString("0", CultureInfo.CurrentCulture),
                (100 * v.Share).ToString("0", CultureInfo.CurrentCulture)));

        return place is null
            ? Tr.F("Plúvio 2.1, interpolado: {0}", string.Join("; ", vizinhas))
            : Tr.F("Plúvio 2.1, {0}, interpolado: {1}", place, string.Join("; ", vizinhas));
    }
}

/// <summary>
/// A base de estações do Plúvio 2.1 (arquivo Estados_Plu21.bin, instalado em
/// "GPRH\Arquivos Comuns") e a interpolação que o Plúvio faz para um ponto
/// qualquer (32.9).
/// </summary>
/// <remarks>
/// O formato não é documentado; foi lido do arquivo em 21/10/2026. Um byte de
/// versão e, para cada um dos 27 estados (na ordem do Plúvio), um ushort com
/// o número de estações e, para cada estação: o nome (byte de tamanho + 68
/// bytes fixos), K, a, b e c (quatro double), a fonte, a latitude e a
/// longitude (byte de tamanho + texto, cp1252; "21°13'59\"").
///
/// A interpolação é o inverso da distância à décima potência, sobre todas as
/// estações, com a distância em graus sem correção de cosseno. Foi a única
/// combinação que reproduz os quatro números do memorial de Valparaiso
/// (1544,886 / 0,142 / 24,953 / 0,756); expoente 10 quer dizer que a
/// estação mais perto manda quase sozinha.
/// </remarks>
public sealed class PluvioDatabase
{
    /// <summary>A ordem dos estados no arquivo.</summary>
    public static readonly IReadOnlyList<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    /// <summary>O expoente do inverso da distância.</summary>
    public const double Power = 10;

    /// <summary>O nome do arquivo da base.</summary>
    public const string FileName = "Estados_Plu21.bin";

    /// <summary>O nome do arquivo das localidades (só alguns estados têm).</summary>
    public const string LocalitiesFileName = "Localidades_Plu21.bin";

    /// <summary>Tamanho fixo do campo do nome, sem o byte de tamanho.</summary>
    private const int NameCapacity = 68;

    private const double KmPorGrau = 111.2;

    public IReadOnlyList<PluvioStation> Stations { get; }

    /// <summary>As localidades, quando o arquivo delas foi lido; senão, vazia.</summary>
    public IReadOnlyList<PluvioLocality> Localities { get; }

    public PluvioDatabase(IReadOnlyList<PluvioStation> stations, IReadOnlyList<PluvioLocality>? localities = null)
    {
        ArgumentNullException.ThrowIfNull(stations);
        Stations = stations;
        Localities = localities ?? [];
    }

    /// <summary>A mesma base, com as localidades lidas do arquivo delas.</summary>
    public PluvioDatabase WithLocalities(ReadOnlySpan<byte> bytes) => new(Stations, ParseLocalities(bytes));

    /// <summary>
    /// Lê o arquivo das localidades: um byte de versão e, por estado, um
    /// ushort com o número delas e, para cada uma, o nome (byte de tamanho +
    /// 68 bytes fixos), a latitude e a longitude (como na base das estações).
    /// </summary>
    public static IReadOnlyList<PluvioLocality> ParseLocalities(ReadOnlySpan<byte> bytes)
    {
        var lista = new List<PluvioLocality>();
        var i = 1;

        try
        {
            foreach (var uf in States)
            {
                var n = BitConverter.ToUInt16(bytes.Slice(i, 2));
                i += 2;

                for (var e = 0; e < n; e++)
                {
                    var nome = Texto(bytes, ref i, NameCapacity);
                    var lat = Graus(Texto(bytes, ref i));
                    var lon = Graus(Texto(bytes, ref i));
                    lista.Add(new PluvioLocality(uf, nome, lat, lon));
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException(Tr.T("O arquivo de localidades do Plúvio acabou antes do fim."));
        }

        return lista;
    }

    /// <summary>A IDF na localidade, interpolada como em qualquer ponto.</summary>
    public PluvioEstimate Interpolate(PluvioLocality local)
    {
        ArgumentNullException.ThrowIfNull(local);
        return Interpolate(-local.LatitudeSouth, -local.LongitudeWest);
    }

    /// <summary>O estado da estação mais perto do ponto (para abrir a lista certa).</summary>
    public string NearestState(double latitude, double longitude) =>
        Stations.MinBy(e => Sq(e.LatitudeSouth + latitude) + Sq(e.LongitudeWest + longitude))!.State;

    /// <summary>Lê o arquivo do Plúvio; arquivo fora do formato vira ArgumentException.</summary>
    public static PluvioDatabase Parse(ReadOnlySpan<byte> bytes)
    {
        var estacoes = new List<PluvioStation>();
        var i = 1;

        try
        {
            foreach (var uf in States)
            {
                var n = BitConverter.ToUInt16(bytes.Slice(i, 2));
                i += 2;

                for (var e = 0; e < n; e++)
                {
                    var nome = Texto(bytes, ref i, NameCapacity);
                    var k = BitConverter.ToDouble(bytes.Slice(i, 8));
                    var a = BitConverter.ToDouble(bytes.Slice(i + 8, 8));
                    var b = BitConverter.ToDouble(bytes.Slice(i + 16, 8));
                    var c = BitConverter.ToDouble(bytes.Slice(i + 24, 8));
                    i += 32;
                    var fonte = Texto(bytes, ref i);
                    var lat = Graus(Texto(bytes, ref i));
                    var lon = Graus(Texto(bytes, ref i));

                    estacoes.Add(new PluvioStation(uf, nome, k, a, b, c, fonte, lat, lon));
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException(Tr.T("O arquivo do Plúvio acabou antes do fim: não é a base Estados_Plu21.bin."));
        }

        if (estacoes.Count == 0) throw new ArgumentException(Tr.T("O arquivo do Plúvio não tem nenhuma estação."));
        return new PluvioDatabase(estacoes);
    }

    /// <summary>Um texto com byte de tamanho; com <paramref name="capacidade"/>, o campo tem tamanho fixo.</summary>
    private static string Texto(ReadOnlySpan<byte> bytes, ref int i, int? capacidade = null)
    {
        int n = bytes[i];
        var texto = Encoding.Latin1.GetString(bytes.Slice(i + 1, n)).TrimEnd();
        i += 1 + (capacidade ?? n);
        return texto;
    }

    /// <summary>"21°13'59\"" em graus decimais.</summary>
    internal static double Graus(string gms)
    {
        var partes = Regex.Matches(gms, @"\d+(?:[.,]\d+)?")
            .Select(m => double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture))
            .ToList();

        if (partes.Count == 0) throw new ArgumentException(Tr.F("Coordenada do Plúvio sem número: \"{0}\".", gms));
        while (partes.Count < 3) partes.Add(0);
        return partes[0] + partes[1] / 60 + partes[2] / 3600;
    }

    /// <summary>
    /// K, a, b e c no local, como o Plúvio interpola.
    /// </summary>
    /// <param name="latitude">Graus, negativa no sul (como em <see cref="GeoLocation"/>).</param>
    /// <param name="longitude">Graus, negativa no oeste.</param>
    public PluvioEstimate Interpolate(double latitude, double longitude)
    {
        var latS = -latitude;
        var lonW = -longitude;

        var pesos = new List<(PluvioStation Estacao, double Graus, double Peso)>(Stations.Count);

        foreach (var e in Stations)
        {
            var d = Math.Sqrt(Sq(e.LatitudeSouth - latS) + Sq(e.LongitudeWest - lonW));

            // Em cima da estação: é ela, sem conta (o peso seria infinito).
            if (d < 1e-9)
            {
                return new PluvioEstimate(e.K, e.A, e.B, e.C, [new PluvioNeighbor(e, 0, 1)]);
            }

            pesos.Add((e, d, Math.Pow(d, -Power)));
        }

        var total = pesos.Sum(p => p.Peso);
        double Media(Func<PluvioStation, double> valor) => pesos.Sum(p => p.Peso * valor(p.Estacao)) / total;

        var perto = pesos
            .OrderBy(p => p.Graus)
            .Take(5)
            .Select(p => new PluvioNeighbor(p.Estacao, DistanciaKm(p.Estacao, latS, lonW), p.Peso / total))
            .ToList();

        return new PluvioEstimate(Media(e => e.K), Media(e => e.A), Media(e => e.B), Media(e => e.C), perto);
    }

    private static double Sq(double v) => v * v;

    private static double DistanciaKm(PluvioStation e, double latS, double lonW)
    {
        var cos = Math.Cos((e.LatitudeSouth + latS) / 2 * Math.PI / 180);
        return KmPorGrau * Math.Sqrt(Sq(e.LatitudeSouth - latS) + Sq((e.LongitudeWest - lonW) * cos));
    }

    /// <summary>
    /// Onde o Plúvio costuma instalar a base, na ordem de procura.
    /// </summary>
    public static IEnumerable<string> UsualPaths(string programFilesX86, string programFiles)
    {
        foreach (var raiz in new[] { programFilesX86, programFiles }.Where(r => !string.IsNullOrEmpty(r)).Distinct())
        {
            yield return Path.Combine(raiz, "GPRH", "Arquivos Comuns", FileName);
            yield return Path.Combine(raiz, "GPRH", "Pluvio", FileName);
        }
    }
}
