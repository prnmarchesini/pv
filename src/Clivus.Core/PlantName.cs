using System.Text;

namespace Clivus.Core;

/// <summary>
/// O nome da usina no 3D (42.10, 31/10/2026, Renan: "na hora de exportar o 3D
/// eu quero que apareça a opção de colocar o nome no desenho, porque hoje eu
/// exporto e fica o nome do arquivo do dwg, isso é ruim"). O nome dado fica
/// gravado no desenho, com o nome do arquivo em que foi dado, e vai no título
/// da página e no projeto da área do cliente (o "desenho" e o "titulo" do
/// contrato); sem nome dado, o nome do arquivo, exatamente como antes.
/// </summary>
public static class PlantName
{
    /// <summary>A chave do nome no dicionário do plugin dentro do desenho.</summary>
    public const string StorageKey = "NOME_DA_USINA";

    /// <summary>O maior nome aceito, o mesmo das áreas e dos alinhamentos (o servidor guarda até 300).</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// O nome que o 3D usa: o gravado no desenho; sem ele, o nome do arquivo
    /// como sempre foi (sem a pasta e a extensão, sem limpar: o servidor agrupa
    /// os projetos por ele); sem arquivo, <paramref name="fallback"/>.
    /// </summary>
    public static string Resolve(string? saved, string? drawingPath, string fallback)
    {
        if (Clean(saved) is { } nome) return nome;

        var arquivo = FileName(drawingPath);
        return string.IsNullOrWhiteSpace(arquivo) ? fallback : arquivo;
    }

    /// <summary>O nome do arquivo do desenho, sem a pasta e sem a extensão ("" sem arquivo).</summary>
    public static string FileName(string? drawingPath) =>
        string.IsNullOrWhiteSpace(drawingPath) ? string.Empty : Path.GetFileNameWithoutExtension(drawingPath);

    /// <summary>
    /// O nome digitado, limpo: sem espaço nas pontas, a quebra de linha, a
    /// tabulação e os espaços seguidos viram um espaço só, e o que passa de
    /// <see cref="MaxLength"/> é cortado (sem partir um caractere em dois).
    /// Null quando não sobra nada.
    /// </summary>
    public static string? Clean(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;

        var s = new StringBuilder(typed.Length);
        var espaco = false;

        foreach (var c in typed)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                espaco = s.Length > 0;
                continue;
            }

            if (espaco) s.Append(' ');
            espaco = false;
            s.Append(c);
        }

        if (s.Length == 0) return null;
        if (s.Length <= MaxLength) return s.ToString();

        var corte = char.IsHighSurrogate(s[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return s.ToString(0, corte).TrimEnd();
    }

    /// <summary>
    /// Se o nome gravado foi dado num arquivo com outro nome: o desenho foi
    /// copiado ("Salvar como" da plataforma A para a B) ou renomeado. A janela
    /// avisa, para a outra usina não sair no projeto da primeira.
    /// </summary>
    public static bool GivenInOtherFile(string? savedFile, string? drawingPath) =>
        !string.IsNullOrWhiteSpace(savedFile)
        && !string.Equals(savedFile, FileName(drawingPath), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Se o nome escolhido precisa ir para o desenho: quando muda o que está
    /// gravado, ou quando o gravado foi dado em outro arquivo (o confirmado
    /// aqui passa a ser deste); sem nada gravado, quando não é o nome do
    /// arquivo (aceitar o nome do arquivo, como antes, não mexe no desenho).
    /// </summary>
    public static bool NeedsSaving(string chosen, string? saved, string? savedFile, string? drawingPath) =>
        Clean(saved) is { } gravado
            ? !string.Equals(gravado, chosen, StringComparison.Ordinal) || GivenInOtherFile(savedFile, drawingPath)
            : !string.Equals(Clean(FileName(drawingPath)), chosen, StringComparison.Ordinal);
}
