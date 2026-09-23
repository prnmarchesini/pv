namespace UFV.Core;

/// <summary>
/// A lista de superfícies do desenho, pronta para ser mostrada.
///
/// Mora no Core e não no plugin porque a ordem em que as superfícies aparecem,
/// e o que se diz quando não há nenhuma, são decisões que o usuário enxerga —
/// e que podem ser testadas sem abrir o CAD.
/// </summary>
public static class SurfaceCatalog
{
    /// <summary>O que dizer quando o desenho não tem superfície alguma.</summary>
    public const string NenhumaSuperficie =
        "Este desenho não tem nenhuma superfície. Crie a superfície do terreno no Civil 3D antes de continuar.";

    /// <summary>
    /// O mesmo, quando o desenho tem referências externas: a superfície pode
    /// estar lá dentro, e é preciso dizer isso em vez de deixar o usuário
    /// achando que o plugin não enxergou nada.
    /// </summary>
    public const string NenhumaSuperficieComXref =
        "Este desenho não tem superfície própria. Se a topografia vem de uma referência externa, "
        + "vincule-a ao desenho (BIND) ou abra o desenho da referência.";

    /// <summary>O que dizer quando há superfícies, mas todas vazias.</summary>
    public const string TodasVazias =
        "As superfícies deste desenho estão vazias. Nenhuma delas tem ponto para ler cota.";

    /// <summary>
    /// Ordena a lista para a tela: alfabética pelo nome COMO ELE APARECE, sem
    /// diferenciar maiúscula de minúscula.
    ///
    /// Pelo nome exibido, e não pelo cru, porque ordenar por um texto e
    /// mostrar outro deixa a lista fora de ordem aos olhos de quem lê: uma
    /// superfície chamada "  ZZZ" iria para o topo por causa do espaço.
    ///
    /// A ordem em que o CAD devolve as superfícies não é estável entre
    /// aberturas, e uma lista que muda de ordem sozinha faz o usuário clicar
    /// na linha errada por decoreba.
    /// </summary>
    public static IReadOnlyList<SurfaceSummary> Organize(IEnumerable<SurfaceSummary> surfaces) =>
        Organize(surfaces, s => s);

    /// <summary>
    /// A mesma ordenação, para quando quem chama carrega junto algo mais que o
    /// resumo — no plugin, o identificador da superfície no desenho, sem o
    /// qual a escolha do usuário não aponta para nada.
    /// </summary>
    public static IReadOnlyList<T> Organize<T>(IEnumerable<T> itens, Func<T, SurfaceSummary> resumo)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(resumo);

        return itens
            .OrderBy(i => resumo(i).DisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(i => resumo(i).PointCount)
            .ToArray();
    }

    /// <summary>
    /// A mensagem a mostrar quando não há o que escolher, ou null quando há
    /// pelo menos uma superfície aproveitável.
    /// </summary>
    /// <param name="temReferenciaExterna">
    /// Se o desenho tem XRef. Muda a explicação: sem superfície própria e com
    /// XRef, o provável é que a topografia esteja na referência.
    /// </param>
    public static string? WhyNothingToChoose(
        IReadOnlyList<SurfaceSummary> surfaces,
        bool temReferenciaExterna = false)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        if (surfaces.Count == 0)
            return temReferenciaExterna ? NenhumaSuperficieComXref : NenhumaSuperficie;

        if (surfaces.All(s => !s.CanBeTerrain)) return TodasVazias;

        return null;
    }
}
