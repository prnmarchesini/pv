using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UFV.Core;

/// <summary>
/// A lista de módulos conhecidos.
///
/// Existe para o usuário não digitar seis números toda vez, e para dois
/// projetos do mesmo módulo saírem com as mesmas medidas — hoje isso depende de
/// alguém reabrir o mesmo PDF e não errar nenhuma casa.
///
/// Ela é conveniência, não autoridade: o módulo digitado na mão vale tanto
/// quanto o da lista, e quem manda é o datasheet que está na frente do
/// projetista. Por isso um modelo que não está aqui nunca é um impedimento.
///
/// Só entra nesta lista módulo cujo datasheet foi conferido. Medida
/// aproximada, "puxada de memória", é pior que nenhuma: ela parece dado e
/// ninguém volta a conferir.
/// </summary>
public static class ModuleLibrary
{
    private const string Arquivo = "UFV.Core.modulos.json";

    /// <summary>
    /// O comparador de nomes, um só para procurar, ordenar e achar repetido.
    ///
    /// É invariante de propósito: com <c>CurrentCulture</c> a ordem da
    /// biblioteca passaria a depender do idioma da máquina, e dois projetistas
    /// veriam a mesma lista em ordens diferentes assim que entrasse uma marca
    /// com acento.
    /// </summary>
    private static readonly StringComparer Nomes = StringComparer.InvariantCultureIgnoreCase;

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// A biblioteca embutida, lida uma vez só.
    ///
    /// Lazy e não <c>??=</c>: UFV.Core é C# puro e também roda fora do AutoCAD,
    /// onde não existe a garantia de thread única do documento.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<SolarModule>> Embutida =
        new(Ler, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Os módulos que vêm com o plugin, em ordem de marca e modelo.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a biblioteca embutida não puder ser lida — instalação corrompida.
    /// Vale para <see cref="Find"/> também, que passa por aqui.
    /// </exception>
    public static IReadOnlyList<SolarModule> Default() => Embutida.Value;

    /// <summary>
    /// O módulo com este modelo, ou null se não houver nenhum.
    ///
    /// A busca ignora maiúscula e espaço em volta: o usuário copia o modelo do
    /// PDF, e o que vem colado quase nunca está do jeito que foi gravado.
    ///
    /// Modelo vazio ou nulo devolve null. O que não devolve null é biblioteca
    /// quebrada: aí <see cref="Default"/> lança, e deve lançar mesmo — instalação
    /// corrompida é coisa para aparecer, não para virar "não achei o modelo".
    /// </summary>
    public static SolarModule? Find(string? modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo)) return null;

        var procurado = modelo.Trim();

        return Default().FirstOrDefault(m => Nomes.Equals(m.Model.Trim(), procurado));
    }

    /// <summary>
    /// Lê uma biblioteca de módulos a partir do texto JSON.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se o texto não for a biblioteca esperada, se ela estiver vazia, ou se
    /// algum módulo dela tiver medida impossível.
    ///
    /// Devolver lista vazia seria o pior resultado: o usuário abriria a janela,
    /// não veria módulo nenhum e não teria como saber por quê.
    /// </exception>
    public static IReadOnlyList<SolarModule> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<Entrada>? entradas;

        try
        {
            entradas = JsonSerializer.Deserialize<List<Entrada>>(json, Opcoes);
        }
        catch (JsonException erro)
        {
            throw new InvalidOperationException(
                $"A biblioteca de módulos não pôde ser lida: {erro.Message}", erro);
        }

        // O array vazio conta como biblioteca quebrada, e não como biblioteca
        // sem módulos: é o que sobra de alguém editando o arquivo e apagando as
        // entradas, e é justamente o silêncio que este método existe para não
        // produzir.
        if (entradas is null or { Count: 0 })
        {
            throw new InvalidOperationException("A biblioteca de módulos está vazia.");
        }

        var modulos = new List<SolarModule>();

        foreach (var entrada in entradas)
        {
            var modulo = new SolarModule(
                entrada.Brand ?? string.Empty,
                entrada.Model ?? string.Empty,
                entrada.PowerWatts,
                entrada.Height,
                entrada.Width,
                entrada.Thickness);

            if (!modulo.IsValid)
            {
                throw new InvalidOperationException(
                    "A biblioteca de módulos traz um módulo com medida impossível: "
                    + $"{Identificar(entrada)}.");
            }

            if (modulo.LooksSwapped is { } aviso)
            {
                // Um módulo mais largo que alto passa em toda validação de
                // medida e sai uma mesa com metade do comprimento. Na
                // biblioteca, que é dado nosso, isso é defeito e não aviso.
                throw new InvalidOperationException(
                    $"A biblioteca de módulos traz um módulo suspeito: {aviso}.");
            }

            modulos.Add(modulo);
        }

        var repetido = modulos
            .GroupBy(m => m.Model.Trim(), Nomes)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            // Dois módulos com o mesmo modelo fazem a busca devolver um deles
            // sem critério, e o projeto sai com as medidas de um ou de outro
            // conforme a ordem do arquivo.
            throw new InvalidOperationException(
                $"A biblioteca de módulos tem o modelo {repetido.Key} repetido.");
        }

        // AsReadOnly e não a List: devolver a lista de dentro deixaria qualquer
        // chamador esvaziar a biblioteca para o resto da sessão do AutoCAD, e a
        // lista apareceria vazia sem nada que explicasse.
        return new ReadOnlyCollection<SolarModule>(
            modulos
                .OrderBy(m => m.Brand, Nomes)
                .ThenBy(m => m.Model, Nomes)
                .ToList());
    }

    private static string Identificar(Entrada entrada) =>
        string.IsNullOrWhiteSpace(entrada.Model) ? "(sem modelo)" : entrada.Model.Trim();

    private static IReadOnlyList<SolarModule> Ler()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var fluxo = assembly.GetManifestResourceStream(Arquivo)
            ?? throw new InvalidOperationException(
                $"A biblioteca de módulos não está na DLL (recurso {Arquivo}).");

        using var leitor = new StreamReader(fluxo);

        return Parse(leitor.ReadToEnd());
    }

    /// <summary>
    /// O módulo como ele está no JSON.
    ///
    /// Separado de <see cref="SolarModule"/> de propósito: o formato do arquivo
    /// pode mudar sem arrastar o modelo, e um campo faltando vira zero aqui —
    /// que a validação recusa — em vez de quebrar a leitura inteira.
    /// </summary>
    private sealed class Entrada
    {
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public double PowerWatts { get; init; }
        public double Height { get; init; }
        public double Width { get; init; }
        public double Thickness { get; init; }
    }
}
