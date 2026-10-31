using System.Globalization;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>
/// As preferências do usuário que não são do desenho (etapa 10): o idioma
/// ("auto", "pt", "en", "es") e, desde 21/10/2026, se o rastreio planta ↔
/// gráfico da drenagem está ligado (32.2). Desde 30/10/2026, os modelos
/// apagados da aba Modelos da janela de Mesa (os prontos vêm com o plugin:
/// apagar é deixar de mostrar, e "Mostrar os apagados" traz de volta). Desde
/// 31/10/2026 (etapa 43), quando a abertura do Civil 3D consultou a versão
/// publicada pela última vez (UTC), para consultar no máximo uma vez por dia.
/// Ficam em <c>%LOCALAPPDATA%\Clivus Solar\preferencias.json</c>; arquivo que
/// falta ou não se lê vale o padrão, e campo que falta (o arquivo de uma
/// versão anterior) vale o padrão dele.
/// </summary>
public sealed record UserPreferences(string Language = "auto", bool ProfileTracking = true, IReadOnlyList<string>? HiddenModels = null, DateTime? LastUpdateCheck = null)
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    /// <summary>As escolhas de idioma, na ordem da lista da tela.</summary>
    public static IReadOnlyList<string> LanguageChoices { get; } = ["auto", "pt", "en", "es"];

    /// <summary>Os modelos apagados da aba Modelos, pelo identificador (nunca null).</summary>
    public IReadOnlyList<string> HiddenModelIds => HiddenModels ?? [];

    /// <summary>Lê o arquivo; o padrão se ele não existe ou está estragado.</summary>
    public static UserPreferences Load(string caminho)
    {
        TryLoad(caminho, out var preferencias);
        return preferencias;
    }

    /// <summary>
    /// Lê o arquivo. Falso quando ele existe e não se lê (estragado, ou preso
    /// por outro programa no meio de uma gravação): aí as preferências são o
    /// padrão, e quem grava sozinho (a consulta da versão, etapa 43) não grava
    /// o padrão por cima das do usuário. Arquivo que não existe se lê: o padrão.
    /// </summary>
    public static bool TryLoad(string caminho, out UserPreferences preferencias)
    {
        preferencias = new UserPreferences();

        try
        {
            if (!File.Exists(caminho)) return true;

            using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
            var idioma = doc.RootElement.TryGetProperty("idioma", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
            var rastreio = !doc.RootElement.TryGetProperty("rastreio", out var r) || r.ValueKind != JsonValueKind.False;

            var apagados = doc.RootElement.TryGetProperty("modelosApagados", out var m) && m.ValueKind == JsonValueKind.Array
                ? m.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList()
                : [];

            // A última consulta da versão (etapa 43), em UTC; fora do formato, nunca consultou.
            DateTime? consulta = doc.RootElement.TryGetProperty("ultimaConsultaDeVersao", out var c) && c.ValueKind == JsonValueKind.String
                && DateTime.TryParse(c.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)
                ? quando
                : null;

            preferencias = new UserPreferences(LanguageChoices.Contains(idioma) ? idioma! : "auto", rastreio, apagados, consulta);
            return true;
        }
        catch (Exception erro) when (erro is IOException or JsonException or UnauthorizedAccessException)
        {
            preferencias = new UserPreferences();
            return false;
        }
    }

    /// <summary>Grava o arquivo (cria a pasta).</summary>
    public void Save(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);

        var campos = new Dictionary<string, object>
        {
            ["idioma"] = Language,
            ["rastreio"] = ProfileTracking,
            ["modelosApagados"] = HiddenModelIds.Distinct(StringComparer.Ordinal).ToArray(),
        };

        // Só depois da primeira consulta; a versão anterior do plugin ignora o campo.
        if (LastUpdateCheck is { } consulta)
            campos["ultimaConsultaDeVersao"] = consulta.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        File.WriteAllText(caminho, JsonSerializer.Serialize(campos, Opcoes));
    }

    /// <summary>
    /// A cultura do Civil 3D pela variável LOCALE (três letras: PTB, ENU,
    /// ESP...); null se não é conhecida.
    /// </summary>
    public static CultureInfo? CultureOfAutoCadLocale(string? locale) => locale?.Trim().ToUpperInvariant() switch
    {
        "PTB" or "PTG" => CultureInfo.GetCultureInfo("pt-BR"),
        "ENU" or "ENG" => CultureInfo.GetCultureInfo("en-US"),
        "ESP" or "ESM" => CultureInfo.GetCultureInfo("es-ES"),
        "DEU" => CultureInfo.GetCultureInfo("de-DE"),
        "FRA" => CultureInfo.GetCultureInfo("fr-FR"),
        "ITA" => CultureInfo.GetCultureInfo("it-IT"),
        _ => null,
    };
}
