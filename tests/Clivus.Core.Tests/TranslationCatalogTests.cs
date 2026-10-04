using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clivus.Core.Tests;

/// <summary>
/// A guarda dos idiomas (10.2): toda frase passada a <c>Tr.T</c>, <c>Tr.F</c> ou <c>Tr.N</c>
/// no código tem tradução em inglês e em espanhol, com os mesmos marcadores, e
/// o catálogo não guarda tradução de frase que saiu do código.
/// </summary>
public partial class TranslationCatalogTests
{
    [GeneratedRegex(@"\bTr\.[TFN]\(\s*(\$?)""((?:[^""\\]|\\.)*)""")]
    private static partial Regex Chamada();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Marcador();

    /// <summary>As frases do código (a chave: sem as pontas em branco).</summary>
    internal static SortedSet<string> FrasesDoCodigo()
    {
        var frases = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var pasta in new[] { "src/Clivus.Core", "src/Clivus.Plugin", "src/Clivus.Instalador" })
        {
            var raiz = Path.Combine(Repositorio.Raiz, pasta);
            if (!Directory.Exists(raiz)) continue;

            foreach (var arquivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
            {
                if (arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
                if (arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;

                foreach (Match m in Chamada().Matches(File.ReadAllText(arquivo)))
                {
                    Assert.True(m.Groups[1].Value.Length == 0,
                        $"{Path.GetFileName(arquivo)}: Tr com texto interpolado ($\"...\") não tem chave fixa; use Tr.F com {{0}}: {m.Value}");

                    var frase = Desescapar(m.Groups[2].Value).Trim();
                    if (frase.Length > 0) frases.Add(frase);
                }
            }
        }

        return frases;
    }

    private static string Desescapar(string s)
    {
        var b = new StringBuilder(s.Length);

        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 == s.Length)
            {
                b.Append(s[i]);
                continue;
            }

            i++;
            b.Append(s[i] switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '0' => '\0',
                _ => s[i],
            });
        }

        return b.ToString();
    }

    private static Dictionary<string, string> Catalogo(string codigo) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Repositorio.Raiz, "src", "Clivus.Core", "Translations", $"{codigo}.json"))) ?? [];

    private static SortedSet<string> Marcadores(string s) =>
        new(Marcador().Matches(s).Select(m => m.Groups[1].Value), StringComparer.Ordinal);

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData("en")]
    [InlineData("es")]
    public void TodaFraseDoCodigoTemTraducaoComOsMesmosMarcadores(string codigo)
    {
        var catalogo = Catalogo(codigo);
        var frases = FrasesDoCodigo();

        var faltam = frases.Where(f => !catalogo.TryGetValue(f, out var t) || string.IsNullOrWhiteSpace(t)).ToList();
        Assert.True(faltam.Count == 0,
            $"{faltam.Count} frase(s) sem tradução em {codigo}.json (python tools/traducoes.py lista todas). As primeiras: "
            + string.Join(" | ", faltam.Take(8)));

        var tortas = frases
            .Where(f => !Marcadores(f).SetEquals(Marcadores(catalogo[f])))
            .ToList();
        Assert.True(tortas.Count == 0,
            $"{tortas.Count} tradução(ões) em {codigo}.json com marcadores diferentes da frase: " + string.Join(" | ", tortas.Take(5)));
    }

    [Theory]
    [Trait("Etapa", "10")]
    [InlineData("en")]
    [InlineData("es")]
    public void OCatalogoNaoGuardaFraseQueSaiuDoCodigo(string codigo)
    {
        var frases = FrasesDoCodigo();
        var sobram = Catalogo(codigo).Keys.Where(k => !frases.Contains(k)).ToList();

        Assert.True(sobram.Count == 0,
            $"{sobram.Count} tradução(ões) em {codigo}.json de frase que não está mais no código (python tools/traducoes.py --limpar): "
            + string.Join(" | ", sobram.Take(8)));
    }

    [Fact]
    [Trait("Etapa", "10")]
    public void AGuardaAchaAsFrasesEDesescapa()
    {
        // A própria guarda tem que ler as chamadas que existem: hoje o Core
        // não tem nenhuma, então o teste prova o leitor num texto de exemplo.
        var exemplo = "Tr.T(\"\\nOlá \\\"mundo\\\"\\n\") + Tr.F(\"{0} mesa(s)\", n)";
        var achadas = Chamada().Matches(exemplo).Select(m => Desescapar(m.Groups[2].Value).Trim()).ToList();

        Assert.Equal(["Olá \"mundo\"", "{0} mesa(s)"], achadas);
        Assert.Equal(["0", "1"], Marcadores("{0} e {1:0.0} e {0}"));
    }
}
