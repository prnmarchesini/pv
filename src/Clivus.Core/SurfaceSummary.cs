namespace Clivus.Core;

/// <summary>
/// O que o usuário precisa ver para escolher qual superfície é o terreno.
///
/// Um projeto costuma ter mais de uma: o terreno natural, o projetado, e
/// antigas de referência que ficaram no desenho. Escolher a errada não dá erro
/// nenhum — dá uma usina inteira calculada sobre o terreno errado.
/// </summary>
/// <param name="Name">Nome da superfície como aparece no Civil 3D.</param>
/// <param name="PointCount">Quantos pontos ela tem. Zero é superfície vazia.</param>
public sealed record SurfaceSummary(string Name, int PointCount)
{
    /// <summary>Nome usado na lista quando a superfície está sem nome.</summary>
    public const string SemNome = "(sem nome)";

    /// <summary>
    /// O nome como ele aparece na tela: sem espaço em volta, e com um texto
    /// próprio quando a superfície não tem nome.
    /// </summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Tr.T("(sem nome)") : Name.Trim();

    /// <summary>
    /// A linha que aparece na lista de escolha. O número sai na cultura do
    /// idioma da tela (<see cref="Tr.Culture"/>), não na da máquina, para a
    /// lista sair igual em qualquer lugar, inclusive nos testes.
    /// </summary>
    public string Describe()
    {
        return PointCount == 1
            ? Tr.F("{0} — 1 ponto", DisplayName)
            : Tr.F("{0} — {1:N0} pontos", DisplayName, PointCount);
    }

    /// <summary>
    /// Superfície sem ponto nenhum não serve de terreno: não há cota para ler.
    /// Ela continua aparecendo na lista, para o usuário entender por que não
    /// pode escolhê-la, mas não pode ser confirmada.
    /// </summary>
    public bool CanBeTerrain => PointCount > 0;
}
