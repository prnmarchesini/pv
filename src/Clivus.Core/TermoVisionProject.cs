using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clivus.Core;

/// <summary>
/// O sistema de coordenadas do desenho para o TermoVision (contrato v2, o
/// projeto sem envio, 02/11/2026): a zona UTM e o hemisfério, o EPSG quando
/// se sabe e o nome do sistema do desenho, quando ele tem um.
/// </summary>
/// <param name="Epsg">O EPSG, ou null.</param>
/// <param name="Name">O código do sistema de coordenadas do desenho (UTM84-17N), ou null.</param>
/// <param name="Zone">A zona UTM (1 a 60).</param>
/// <param name="South">Se o hemisfério é o sul.</param>
public sealed record TermoVisionProjectCrs(int? Epsg, string? Name, int Zone, bool South)
{
    /// <summary>
    /// Pela latitude e longitude do desenho: a zona e o hemisfério; o EPSG é
    /// o do sistema do desenho, se ele tem um, senão o do SIRGAS 2000 na
    /// América do Sul, senão null.
    /// </summary>
    public static TermoVisionProjectCrs Of(double latitude, double longitude, int? drawingEpsg = null, string? drawingName = null)
    {
        var utm = TerrainPlace.Utm(latitude, longitude);
        return new TermoVisionProjectCrs(drawingEpsg ?? utm.SirgasEpsg, string.IsNullOrWhiteSpace(drawingName) ? null : drawingName.Trim(), utm.Zone, utm.South);
    }

    /// <summary>
    /// Se o ponto (E, N) do desenho cai perto de onde a latitude e a longitude
    /// dele caem na zona (25 km): o desenho está mesmo em UTM, nessa zona. A
    /// conta é a aproximada (um grau é uns 111 km), que basta para pegar o
    /// desenho em coordenada local ou em outra zona antes de mandar a usina
    /// para o lugar errado.
    /// </summary>
    public bool LooksLikeUtm(double east, double north, double latitude, double longitude)
    {
        if (!double.IsFinite(east) || !double.IsFinite(north)) return false;

        var meridiano = Zone * 6 - 183;
        var esperadoE = 500_000 + (longitude - meridiano) * 111_320 * Math.Cos(latitude * Math.PI / 180);
        var esperadoN = (South ? 10_000_000 : 0) + latitude * 110_600;
        return Math.Abs(east - esperadoE) < 25_000 && Math.Abs(north - esperadoN) < 25_000;
    }

    // O fuso no código do sistema do desenho: UTM84-17N, UTM83-17, WGS84.UTM-17N,
    // SIRGAS2000.UTM-23S, "WGS 84 / UTM zone 17N".
    private static readonly Regex FusoNoNome = new("UTM(?:[0-9]{2})?[^0-9]{0,8}?([0-9]{1,2})[ ]*([NS])?(?![0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// O fuso e o hemisfério escritos no código do sistema do desenho (o
    /// hemisfério null quando o código não diz, como no UTM83-17), ou null
    /// sem fuso no código.
    /// </summary>
    public static (int Zone, bool? South)? ZoneInName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var achado = FusoNoNome.Match(name);
        if (!achado.Success || !int.TryParse(achado.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var zona) || zona is < 1 or > 60) return null;

        bool? sul = achado.Groups[2].Success ? char.ToUpperInvariant(achado.Groups[2].Value[0]) == 'S'
            : Regex.IsMatch(name, "south|sul|sur", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ? true
            : Regex.IsMatch(name, "north|norte", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ? false
            : null;
        return (zona, sul);
    }

    /// <summary>O EPSG pelo datum do código: WGS 84 (326xx no norte, 327xx no sul) ou SIRGAS 2000; null nos outros.</summary>
    private static int? EpsgOfName(string name, int zone, bool south)
    {
        if (Regex.IsMatch(name, "WGS[ _-]*84|UTM84", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return (south ? 32700 : 32600) + zone;
        if (!Regex.IsMatch(name, "SIRGAS", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return null;
        return south ? zone is >= 17 and <= 25 ? 31960 + zone : null : zone is >= 17 and <= 22 ? 31954 + zone : null;
    }

    /// <summary>
    /// O sistema do desenho para o TermoVision (revisão do 33.9): a zona e o
    /// hemisfério do código do sistema do desenho, se ele tem um, senão os da
    /// localização; a zona tem de bater com as coordenadas (E, N) das mesas
    /// e, se não bate, valem as vizinhas (a usina na divisa, desenhada na
    /// zona de lá). O EPSG é o do desenho, senão o do datum do código (WGS 84,
    /// SIRGAS 2000), senão o do SIRGAS na zona da localização. Null se
    /// nenhuma zona bate: o desenho não está em UTM, ou a localização está
    /// errada, e a usina iria parar em outro lugar do mapa.
    /// </summary>
    public static TermoVisionProjectCrs? Resolve(double latitude, double longitude, double east, double north, int? drawingEpsg = null, string? drawingName = null)
    {
        var utm = TerrainPlace.Utm(latitude, longitude);
        var nome = string.IsNullOrWhiteSpace(drawingName) ? null : drawingName.Trim();

        var candidatas = new List<(int Zona, bool Sul)>();
        void Somar(int zona, bool sul)
        {
            zona = (zona + 59) % 60 + 1;   // o 0 é o 60, e o 61 é o 1
            if (!candidatas.Contains((zona, sul))) candidatas.Add((zona, sul));
        }

        if (ZoneInName(nome) is { } doNome) Somar(doNome.Zone, doNome.South ?? utm.South);
        Somar(utm.Zone, utm.South);
        Somar(utm.Zone - 1, utm.South);
        Somar(utm.Zone + 1, utm.South);

        foreach (var (zona, sul) in candidatas)
        {
            var crs = new TermoVisionProjectCrs(null, nome, zona, sul);
            if (!crs.LooksLikeUtm(east, north, latitude, longitude)) continue;

            var epsg = drawingEpsg
                ?? (nome is not null ? EpsgOfName(nome, zona, sul) : null)
                ?? (zona == utm.Zone && sul == utm.South ? utm.SirgasEpsg : null);
            return crs with { Epsg = epsg };
        }

        return null;
    }
}

/// <summary>
/// O projeto do TermoVision sem envio (contrato v2, 02/11/2026, Renan: "o
/// cliente vai ver outro produto e ficar curioso"): o desenho que não veio do
/// TermoVision vira uma usina pública de lá, com as mesas sobre o terreno e o
/// satélite. O plugin manda ao servidor do Clivus (que repassa com a chave
/// dele); o mesmo GUID do desenho atualiza a mesma usina e devolve o mesmo link.
/// </summary>
public static class TermoVisionProject
{
    /// <summary>A folga do terreno em volta das mesas, em metro.</summary>
    public const double TerrainMargin = 100;

    /// <summary>
    /// Quantas células a grade do terreno tem no lado maior: o passo é de 2 m
    /// até 2 km de lado; acima, o lado dividido por mil.
    /// </summary>
    public static int GridCells(double longestSide) =>
        double.IsFinite(longestSide) && longestSide > 0 ? (int)Math.Clamp(Math.Ceiling(longestSide / 2.0), 2, 1000) : 2;

    /// <summary>
    /// O JSON do projeto. A mesa que não serve fica fora e vai para
    /// <paramref name="recusadas"/> com o motivo.
    /// </summary>
    /// <param name="plugin">A versão do plugin.</param>
    /// <param name="desenho">O nome do arquivo.</param>
    /// <param name="nome">O nome da usina.</param>
    /// <param name="drawing">O GUID do desenho (o do Clivus, guardado no desenho).</param>
    /// <param name="crs">O sistema de coordenadas.</param>
    /// <param name="terreno">A grade do terreno (linha 0 ao sul, null sem superfície).</param>
    /// <param name="mesas">As mesas, como no retorno, com o tipo da estrutura.</param>
    /// <param name="recusadas">As mesas que ficaram de fora, com o porquê.</param>
    public static byte[] Build(string plugin, string desenho, string nome, Guid drawing, TermoVisionProjectCrs crs, Scene3DTerrain terreno,
        IEnumerable<TermoVisionTable> mesas, out List<string> recusadas)
    {
        ArgumentNullException.ThrowIfNull(crs);
        ArgumentNullException.ThrowIfNull(terreno);
        ArgumentNullException.ThrowIfNull(mesas);
        recusadas = [];

        using var memoria = new MemoryStream();
        using (var w = new Utf8JsonWriter(memoria))
        {
            w.WriteStartObject();
            w.WriteNumber("versao", 1);
            w.WriteString("plugin", plugin);
            w.WriteString("desenho", desenho);
            w.WriteString("nome", nome);
            w.WriteString("guid_do_desenho", drawing.ToString("D"));

            w.WriteStartObject("crs");
            if (crs.Epsg is { } epsg) w.WriteNumber("epsg", epsg);
            else w.WriteNull("epsg");
            if (crs.Name is { } nomeDoSistema) w.WriteString("nome", nomeDoSistema);
            else w.WriteNull("nome");
            w.WriteNumber("zona", crs.Zone);
            w.WriteString("hemisferio", crs.South ? "S" : "N");
            w.WriteEndObject();

            w.WriteStartObject("terreno");
            w.WriteNumber("passo_m", Math.Round(terreno.Step, 4));
            w.WriteNumber("e0", Math.Round(terreno.X0, 3));
            w.WriteNumber("n0", Math.Round(terreno.Y0, 3));
            w.WriteNumber("colunas", terreno.Columns);
            w.WriteNumber("linhas", terreno.Rows);
            w.WriteStartArray("z");
            foreach (var z in terreno.Z)
            {
                if (z is { } cota && double.IsFinite(cota)) w.WriteNumberValue(Math.Round(cota, 2));
                else w.WriteNullValue();
            }
            w.WriteEndArray();
            w.WriteEndObject();

            w.WriteStartArray("mesas");
            TermoVisionReturn.WriteTables(w, mesas, recusadas, subtype: true);
            w.WriteEndArray();

            w.WriteEndObject();
        }

        return memoria.ToArray();
    }
}

/// <summary>A resposta do projeto: a usina e o link público.</summary>
/// <param name="Saved">Se o TermoVision gravou.</param>
/// <param name="PlantCode">O código da usina lá.</param>
/// <param name="PlantName">O nome da usina lá.</param>
/// <param name="Url">O link público.</param>
/// <param name="ExpiresAt">Até quando o link vale (sem acesso), ou null.</param>
public sealed record TermoVisionProjectResult(bool Saved, string? PlantCode, string? PlantName, string? Url, DateTimeOffset? ExpiresAt)
{
    /// <summary>Lê a resposta; nulo quando não é o JSON do contrato (sem o link).</summary>
    public static TermoVisionProjectResult? TryParse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            string? Texto(JsonElement o, string nome) =>
                o.ValueKind == JsonValueKind.Object && o.TryGetProperty(nome, out var v)
                    ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null
                    : null;

            var usina = r.TryGetProperty("usina", out var u) ? u : default;
            var link = r.TryGetProperty("link", out var l) ? l : default;
            var url = Texto(link, "url");
            if (string.IsNullOrWhiteSpace(url)) return null;

            DateTimeOffset? expira = DateTimeOffset.TryParse(Texto(link, "expira_em"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var quando) ? quando : null;

            return new TermoVisionProjectResult(
                r.TryGetProperty("gravado", out var g) && g.ValueKind == JsonValueKind.True,
                Texto(usina, "codigo"), Texto(usina, "nome"), url, expira);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
