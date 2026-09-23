using System.Globalization;

namespace UFV.Core;

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
    /// A cultura do número na tela. Fixa, e não a da máquina, para a lista
    /// sair igual em qualquer lugar — inclusive nos testes.
    ///
    /// Depende de InvariantGlobalization continuar falso em
    /// Directory.Build.props: com ele ligado, esta busca devolve a cultura
    /// invariante em silêncio e o separador de milhar vira vírgula.
    /// </summary>
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O nome como ele aparece na tela: sem espaço em volta, e com um texto
    /// próprio quando a superfície não tem nome.
    /// </summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? SemNome : Name.Trim();

    /// <summary>A linha que aparece na lista de escolha.</summary>
    public string Describe()
    {
        var pontos = PointCount.ToString("N0", Brasil);

        return PointCount == 1
            ? $"{DisplayName} — 1 ponto"
            : $"{DisplayName} — {pontos} pontos";
    }

    /// <summary>
    /// Superfície sem ponto nenhum não serve de terreno: não há cota para ler.
    /// Ela continua aparecendo na lista, para o usuário entender por que não
    /// pode escolhê-la, mas não pode ser confirmada.
    /// </summary>
    public bool CanBeTerrain => PointCount > 0;
}
