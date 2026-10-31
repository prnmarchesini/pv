using System.Globalization;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>
/// As preferências do usuário que não são do desenho (etapa 10): o idioma
/// ("auto", "pt", "en", "es") e, desde 21/10/2026, se o rastreio planta ↔
/// gráfico da drenagem está ligado (32.2). Desde 30/10/2026, os modelos
/// apagados da aba Modelos da janela de Mesa (os prontos vêm com o plugin:
/// apagar é deixar de mostrar, e "Mostrar os apagados" traz de volta). Ficam
/// em <c>%LOCALAPPDATA%\Clivus Solar\preferencias.json</c>; arquivo que
/// falta ou não se lê vale o padrão.
/// </summary>
public sealed record UserPreferences(string Language = "auto", bool ProfileTracking = true, IReadOnlyList<string>? HiddenModels = null)
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    /// <summary>As escolhas de idioma, na ordem da lista da tela.</summary>
    public static IReadOnlyList<string> LanguageChoices { get; } = ["auto", "pt", "en", "es"];

    /// <summary>Os modelos apagados da aba Modelos, pelo identificador (nunca null).</summary>
    public IReadOnlyList<string> HiddenModelIds => HiddenModels ?? [];

    /// <summary>Lê o arquivo; o padrão se ele não existe ou está estragado.</summary>
    public static UserPreferences Load(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return new UserPreferences();

            using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
            var idioma = doc.RootElement.TryGetProperty("idioma", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
            var rastreio = !doc.RootElement.TryGetProperty("rastreio", out var r) || r.ValueKind != JsonValueKind.False;

            var apagados = doc.RootElement.TryGetProperty("modelosApagados", out var m) && m.ValueKind == JsonValueKind.Array
                ? m.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList()
                : [];

            return new UserPreferences(LanguageChoices.Contains(idioma) ? idioma! : "auto", rastreio, apagados);
        }
        catch (Exception erro) when (erro is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UserPreferences();
        }
    }

    /// <summary>Grava o arquivo (cria a pasta).</summary>
    public void Save(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["idioma"] = Language,
            ["rastreio"] = ProfileTracking,
            ["modelosApagados"] = HiddenModelIds.Distinct(StringComparer.Ordinal).ToArray(),
        }, Opcoes));
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
