using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// A identidade de uma linha de alinhamento dentro do desenho.
///
/// A linha de alinhamento é a referência de onde as fileiras de mesas começam.
/// O que ela guarda, além do nome, é o <b>lado</b>: de que lado dela as mesas
/// ficam.
///
/// E o lado só faz sentido junto com o SENTIDO do traçado. "Esquerda" é
/// esquerda de quem caminha do primeiro ponto para o segundo; desenhar a mesma
/// linha ao contrário troca os dois lados. Por isso a identidade guarda o lado
/// escolhido, e a entidade guarda a ordem dos pontos — separar os dois poria a
/// usina do lado errado ao reabrir o desenho, com tudo parecendo normal.
///
/// Vale o mesmo que para a área: layer é aparência, o GUID daqui é identidade,
/// e ele viaja grudado na entidade em XData sob CLIVUS.
/// </summary>
/// <param name="Id">
/// Identificador próprio, gerado uma vez e nunca reaproveitado. Diferente do
/// handle do AutoCAD, que é único só dentro de um arquivo.
/// </param>
/// <param name="Name">Nome que o usuário deu, para ele se reconhecer na lista.</param>
/// <param name="Side">
/// De que lado da linha ficam as mesas, no sentido em que ela foi traçada.
/// </param>
/// <param name="CreatedAt">Quando o alinhamento foi criado.</param>
public sealed record AlignmentIdentity(Guid Id, string Name, LineSide Side, DateTime CreatedAt)
{
    /// <summary>O tipo gravado no XData, para distinguir de outras coisas nossas.</summary>
    public const string Tipo = "Alinhamento";

    /// <summary>Nome usado quando o usuário não dá nenhum.</summary>
    public const string SemNome = "(sem nome)";

    /// <summary>Cria a identidade de um alinhamento novo.</summary>
    public static AlignmentIdentity Create(string? nome, LineSide lado, DateTime agora) =>
        new(Guid.NewGuid(), Limpar(nome), lado, agora);

    /// <summary>O nome como ele aparece, sem espaço em volta e nunca vazio.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Tr.T("(sem nome)") : Name.Trim();

    /// <summary>A linha que descreve o alinhamento para o usuário.</summary>
    public string Describe() =>
        Side switch
        {
            LineSide.Left => Tr.F("{0} — mesas à esquerda (criado em {1:dd/MM/yyyy HH:mm})", DisplayName, CreatedAt),
            LineSide.Right => Tr.F("{0} — mesas à direita (criado em {1:dd/MM/yyyy HH:mm})", DisplayName, CreatedAt),
            _ => Tr.F("{0} — mesas à sobre a linha (criado em {1:dd/MM/yyyy HH:mm})", DisplayName, CreatedAt),
        };

    /// <summary>
    /// Se a identidade é utilizável.
    ///
    /// Um GUID vazio é o que sobra de um XData truncado. E um lado "em cima da
    /// linha" não é escolha: é o que resulta de o usuário ter clicado sobre a
    /// própria linha, e um alinhamento sem lado não diz onde pôr mesa nenhuma.
    /// </summary>
    public bool IsValid => Id != Guid.Empty && Side != LineSide.On;

    private static string Limpar(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? string.Empty : nome.Trim();
}
