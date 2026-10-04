namespace Clivus.Core;

/// <summary>
/// Os estilos que o plugin usa nos textos, cotas e chamadas (passo 8.13,
/// Melhorias.docx, 01/10/2026): "quero uma forma de ter um menu,
/// configuração do projeto, para eu escolher QUAL text style eu vou usar nos
/// textos, a mesma coisa para dimensions e leaders". Os estilos do Renan são
/// anotativos: ele escolhe a escala e usa a mesma no layout.
///
/// Null é "o corrente do desenho", como era antes.
/// </summary>
/// <param name="TextStyle">O estilo de texto (STYLE).</param>
/// <param name="DimensionStyle">O estilo de cota (DIMSTYLE).</param>
/// <param name="LeaderStyle">O estilo de chamada (MLEADERSTYLE).</param>
public sealed record ProjectStyles(string? TextStyle, string? DimensionStyle, string? LeaderStyle)
{
    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public const string StorageKey = "ESTILOS";

    private const string Versao = "1";

    /// <summary>Nada escolhido: valem os correntes do desenho.</summary>
    public static readonly ProjectStyles None = new(null, null, null);

    /// <summary>
    /// Sem escolha gravada, o estilo de partida: o primeiro anotativo
    /// próprio do desenho, em ordem de nome, deixando de lado o "Annotative"
    /// que vem de fábrica no AutoCAD. Os estilos do Renan são anotativos e
    /// são os únicos anotativos próprios dos desenhos dele; null se o desenho
    /// não tem nenhum (vale o corrente).
    /// </summary>
    public static string? FirstAnnotative(IEnumerable<(string Name, bool Annotative)> estilos)
    {
        ArgumentNullException.ThrowIfNull(estilos);

        return estilos
            .Where(e => e.Annotative && !string.IsNullOrWhiteSpace(e.Name) && !string.Equals(e.Name.Trim(), "Annotative", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Para o registro: versão e os três nomes (vazio = corrente).</summary>
    public IReadOnlyList<string> Encode() => [Versao, TextStyle ?? "", DimensionStyle ?? "", LeaderStyle ?? ""];

    /// <summary>Do registro; ilegível ou de outra versão, nada escolhido.</summary>
    public static ProjectStyles Decode(IReadOnlyList<string>? campos)
    {
        if (campos is null || campos.Count != 4 || campos[0] != Versao) return None;

        return new ProjectStyles(Nome(campos[1]), Nome(campos[2]), Nome(campos[3]));
    }

    /// <summary>
    /// O nome entre os existentes no desenho que bate com o pedido, ignorando
    /// maiúscula, espaço nas pontas e a diferença entre hífen e travessão (o
    /// Word troca um pelo outro sozinho); null se nenhum bate.
    /// </summary>
    public static string? Match(string? pedido, IEnumerable<string> existentes)
    {
        ArgumentNullException.ThrowIfNull(existentes);
        if (string.IsNullOrWhiteSpace(pedido)) return null;

        var chave = Chave(pedido);
        return existentes.FirstOrDefault(e => Chave(e) == chave);
    }

    private static string Chave(string nome) =>
        nome.Trim().Replace('–', '-').Replace('—', '-').ToUpperInvariant();

    private static string? Nome(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
