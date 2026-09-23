using System.Globalization;

namespace UFV.Core;

/// <summary>
/// A identidade de uma área de implantação dentro do desenho.
///
/// O desenho pode ter milhares de polilinhas. Esta é a resposta para "como o
/// plugin sabe que aquela polilinha é a área um?" — e a resposta não é layer.
///
/// Layer é aparência: o usuário renomeia, copia para outro desenho, muda de
/// camada sem querer, e a ferramenta perderia tudo. O GUID daqui viaja grudado
/// na entidade, em XData sob MARCHENG_UFV, onde mais ninguém escreve
/// (02-arquitetura.md).
/// </summary>
/// <param name="Id">
/// Identificador próprio, gerado uma vez e nunca reaproveitado. Diferente do
/// handle do AutoCAD, que é único só dentro de um arquivo: copiada a área para
/// outro desenho, o handle muda e este continua.
/// </param>
/// <param name="Name">Nome que o usuário deu, para ele se reconhecer na lista.</param>
/// <param name="CreatedAt">Quando a área foi criada.</param>
public sealed record AreaIdentity(Guid Id, string Name, DateTime CreatedAt)
{
    /// <summary>O tipo gravado no XData, para distinguir de outras coisas nossas.</summary>
    public const string Tipo = "Area";

    /// <summary>Nome usado quando o usuário não dá nenhum.</summary>
    public const string SemNome = "(sem nome)";

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Cria a identidade de uma área nova.</summary>
    public static AreaIdentity Create(string? nome, DateTime agora) =>
        new(Guid.NewGuid(), Limpar(nome), agora);

    /// <summary>O nome como ele aparece, sem espaço em volta e nunca vazio.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? SemNome : Name.Trim();

    /// <summary>A linha que descreve a área para o usuário.</summary>
    public string Describe() =>
        $"{DisplayName} (criada em {CreatedAt.ToString("dd/MM/yyyy HH:mm", Brasil)})";

    /// <summary>
    /// Se a identidade é utilizável. Um GUID vazio é o que sobra de um XData
    /// truncado ou de um campo que não pôde ser lido — e tratar isso como
    /// identidade faria duas áreas diferentes parecerem a mesma.
    /// </summary>
    public bool IsValid => Id != Guid.Empty;

    private static string Limpar(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? string.Empty : nome.Trim();
}
