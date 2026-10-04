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
    /// Os estilos do Renan, com os nomes como estão no desenho do Itatiba
    /// (lidos dele em 01/10/2026; no Word ele escreveu "Marchengg Anotativa
    /// – Detalhe", "Marcheng_anotativa" e "Marcheng anotativo"). Valem sozinhos
    /// quando o desenho os tem e nada foi escolhido.
    /// </summary>
    public static readonly ProjectStyles Marcheng = new("Marcheng Anotativa - Detalhe", "Marcheng_Anotativa", "Marchen Anotativo");

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
