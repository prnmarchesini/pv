using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>Os idiomas da tela (etapa 10).</summary>
public enum UiLanguage
{
    Portuguese,
    English,
    Spanish,
}

/// <summary>
/// A tradução da tela (etapa 10, 04/10/2026: "Quero ter web e plug-in multi
/// linguagem, iniciando por inglês e espanhol"). O português continua escrito
/// no código e é a chave; inglês e espanhol vêm dos catálogos embutidos
/// (<c>Translations/en.json</c>, <c>es.json</c>), "frase em português →
/// tradução". Frase sem tradução volta em português (nada quebra) e fica em
/// <see cref="Missing"/>.
/// <para>
/// Os espaços e quebras de linha das pontas não fazem parte da chave:
/// <c>T("\nOlá\n")</c> procura "Olá" e devolve a tradução com as mesmas pontas.
/// </para>
/// </summary>
public static class Tr
{
    private static readonly CultureInfo Portugues = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo Ingles = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Espanhol = CultureInfo.GetCultureInfo("es");

    private static readonly Lazy<IReadOnlyDictionary<string, string>> En = new(() => Carregar("en"));
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Es = new(() => Carregar("es"));

    // Os testes trocam o idioma (e o catálogo) só no fluxo deles, sem mexer
    // nos outros testes que rodam em paralelo.
    private static readonly AsyncLocal<(UiLanguage Idioma, IReadOnlyDictionary<string, string>? Catalogo)?> Local = new();

    private static UiLanguage _atual = UiLanguage.Portuguese;

    /// <summary>As frases que faltaram no catálogo do idioma atual (para o registro de diagnóstico).</summary>
    public static ConcurrentDictionary<string, byte> Missing { get; } = new();

    /// <summary>O idioma da tela agora.</summary>
    public static UiLanguage Current
    {
        get => Local.Value?.Idioma ?? _atual;
        set => _atual = value;
    }

    /// <summary>A cultura dos números e das datas do idioma atual.</summary>
    public static CultureInfo Culture => CultureOf(Current);

    /// <summary>A cultura dos números e das datas de um idioma.</summary>
    public static CultureInfo CultureOf(UiLanguage idioma) => idioma switch
    {
        UiLanguage.English => Ingles,
        UiLanguage.Spanish => Espanhol,
        _ => Portugues,
    };

    /// <summary>
    /// Troca o idioma só neste fluxo (testes), opcionalmente com um catálogo
    /// próprio; o idioma de antes volta no Dispose.
    /// </summary>
    public static IDisposable Use(UiLanguage idioma, IReadOnlyDictionary<string, string>? catalogo = null)
    {
        var antes = Local.Value;
        Local.Value = (idioma, catalogo);
        return new Volta(() => Local.Value = antes);
    }

    /// <summary>A frase no idioma atual.</summary>
    public static string T(string portugues)
    {
        if (string.IsNullOrEmpty(portugues)) return portugues;

        var idioma = Current;
        if (idioma == UiLanguage.Portuguese) return portugues;

        var inicio = 0;
        while (inicio < portugues.Length && char.IsWhiteSpace(portugues[inicio])) inicio++;
        if (inicio == portugues.Length) return portugues;

        var fim = portugues.Length;
        while (char.IsWhiteSpace(portugues[fim - 1])) fim--;

        var chave = portugues[inicio..fim];
        var catalogo = Local.Value?.Catalogo ?? CatalogOf(idioma);

        if (!catalogo.TryGetValue(chave, out var traducao) || string.IsNullOrEmpty(traducao))
        {
            Missing.TryAdd(chave, 0);
            return portugues;
        }

        return portugues[..inicio] + traducao + portugues[fim..];
    }

    /// <summary>
    /// Marca uma frase de tela guardada como dado (a ribbon) sem traduzir:
    /// ela é traduzida com <see cref="T"/> na hora de mostrar, e a guarda dos
    /// idiomas a enxerga no código.
    /// </summary>
    public static string N(string portugues) => portugues;

    /// <summary>A frase com marcadores ({0}, {1:0.0}) no idioma atual, os números na cultura dele.</summary>
    public static string F(string portugues, params object?[] valores) =>
        string.Format(Culture, T(portugues), valores);

    /// <summary>O catálogo embutido de um idioma (vazio para o português).</summary>
    public static IReadOnlyDictionary<string, string> CatalogOf(UiLanguage idioma) => idioma switch
    {
        UiLanguage.English => En.Value,
        UiLanguage.Spanish => Es.Value,
        _ => new Dictionary<string, string>(),
    };

    /// <summary>
    /// O idioma pela escolha do usuário ("auto", "pt", "en", "es") e, no
    /// automático, pela cultura do Civil 3D (português e espanhol pelo
    /// prefixo; outro idioma, inglês; sem cultura, português).
    /// </summary>
    public static UiLanguage Resolve(string? escolha, CultureInfo? culturaDoProduto)
    {
        switch (escolha?.Trim().ToLowerInvariant())
        {
            case "pt": return UiLanguage.Portuguese;
            case "en": return UiLanguage.English;
            case "es": return UiLanguage.Spanish;
        }

        // Sem saber o idioma do Civil 3D, português; Civil 3D noutro idioma
        // que não temos (alemão, francês), inglês.
        return culturaDoProduto?.TwoLetterISOLanguageName switch
        {
            null => UiLanguage.Portuguese,
            "pt" => UiLanguage.Portuguese,
            "es" => UiLanguage.Spanish,
            _ => UiLanguage.English,
        };
    }

    /// <summary>
    /// O cabeçalho Accept-Language dos pedidos ao servidor: o erro da API
    /// volta no idioma da tela (contratos, seção "Idioma").
    /// </summary>
    public static string AcceptLanguage => Current switch
    {
        UiLanguage.English => "en",
        UiLanguage.Spanish => "es",
        _ => "pt-BR",
    };

    /// <summary>O código curto do idioma ("pt", "en", "es").</summary>
    public static string Code(UiLanguage idioma) => idioma switch
    {
        UiLanguage.English => "en",
        UiLanguage.Spanish => "es",
        _ => "pt",
    };

    private static IReadOnlyDictionary<string, string> Carregar(string codigo)
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Clivus.Core.Translations.{codigo}.json");
        if (fluxo is null) return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(fluxo) ?? [];
    }

    private sealed class Volta(Action acao) : IDisposable
    {
        private Action? _acao = acao;

        public void Dispose()
        {
            _acao?.Invoke();
            _acao = null;
        }
    }
}
