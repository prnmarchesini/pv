using System.Globalization;

namespace UFV.Core;

/// <summary>
/// O resumo do terreno processado, como o usuário lê.
///
/// Estes são os números que ele confere contra as propriedades da superfície
/// no Civil 3D — é a validação do passo 1.4. Por isso todos saem do que o
/// motor de fato leu, e não do que o CAD diz ter: um número copiado do Civil
/// 3D concordaria com o Civil 3D mesmo se a leitura tivesse dado errado.
///
/// Uma divergência é esperada e não é defeito: as cotas saem dos triângulos
/// APROVEITADOS. Um triângulo descartado por não ter área em planta — uma
/// faceta vertical de talude — tem vértices que são pontos reais da
/// superfície, e o Civil 3D os conta na elevação mínima e máxima dele. Numa
/// superfície com talude vertical, portanto, as cotas do resumo podem ficar
/// dentro das do Civil 3D. Quando isso acontecer, a linha de descartados
/// aparece junto e explica.
/// </summary>
/// <param name="SurfaceName">Nome da superfície escolhida.</param>
/// <param name="TriangleCount">Triângulos aproveitados.</param>
/// <param name="DiscardedTriangleCount">Triângulos que não servem para responder cota.</param>
/// <param name="MinZ">Cota mais baixa, em metros.</param>
/// <param name="MaxZ">Cota mais alta, em metros.</param>
/// <param name="Area2D">Área projetada em planta, em metros quadrados.</param>
/// <param name="Area3D">Área da superfície no espaço, em metros quadrados.</param>
public sealed record TerrainSummary(
    string SurfaceName,
    int TriangleCount,
    int DiscardedTriangleCount,
    double MinZ,
    double MaxZ,
    double Area2D,
    double Area3D)
{
    /// <summary>
    /// A cultura do número na tela. Fixa, e não a da máquina, pelo mesmo
    /// motivo de <see cref="SurfaceSummary"/> — e com a mesma dependência:
    /// InvariantGlobalization precisa continuar falso em
    /// Directory.Build.props, senão esta busca devolve a cultura invariante
    /// em silêncio e o separador de milhar vira vírgula.
    /// </summary>
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Diferença entre a cota mais alta e a mais baixa, em metros.</summary>
    public double Desnivel => MaxZ - MinZ;

    /// <summary>Área em planta, em hectares — a unidade em que se fala de usina.</summary>
    public double Hectares => Area2D / 10_000.0;

    /// <summary>
    /// As linhas do resumo, na ordem em que aparecem na linha de comando.
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        var linhas = new List<string>
        {
            $"Terreno processado: {SurfaceName}",
            $"  triângulos:      {TriangleCount.ToString("N0", Brasil)}",
            $"  cotas:           {Metros(MinZ)} a {Metros(MaxZ)}  (desnível de {Metros(Desnivel)})",
            $"  área em planta:  {Metros2(Area2D)}  ({Hectares.ToString("N2", Brasil)} ha)",
            $"  área do terreno: {Metros2(Area3D)}",
        };

        // Só aparece quando há o que dizer. Malha com muitos descartes é
        // levantamento com problema, e o usuário precisa saber disso agora, e
        // não quando um pilar sair num lugar estranho.
        if (DiscardedTriangleCount > 0)
        {
            linhas.Add(
                $"  descartados:     {DiscardedTriangleCount.ToString("N0", Brasil)} "
                + "triângulo(s) sem área útil (faceta vertical ou ponto repetido)");
        }

        return linhas;
    }

    private static string Metros(double valor) =>
        valor.ToString("N3", Brasil) + " m";

    private static string Metros2(double valor) =>
        valor.ToString("N2", Brasil) + " m²";
}
