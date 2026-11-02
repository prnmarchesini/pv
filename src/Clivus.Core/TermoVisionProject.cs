using System.Text.Json;

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
