using System.Globalization;

namespace Clivus.Core;

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
    // A cultura do número na tela é a do idioma (Tr.Culture), e não a da
    // máquina, pelo mesmo motivo de SurfaceSummary, e com a mesma
    // dependência: InvariantGlobalization precisa continuar falso em
    // Directory.Build.props, senão a busca da cultura devolve a invariante
    // em silêncio e o separador de milhar vira vírgula.

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
            Tr.F("Terreno processado: {0}", SurfaceName),
            Tr.F("  triângulos:      {0:N0}", TriangleCount),
            Tr.F("  cotas:           {0} a {1}  (desnível de {2})", Metros(MinZ), Metros(MaxZ), Metros(Desnivel)),
            Tr.F("  área em planta:  {0}  ({1:N2} ha)", Metros2(Area2D), Hectares),
            Tr.F("  área do terreno: {0}", Metros2(Area3D)),
        };

        // Só aparece quando há o que dizer. Malha com muitos descartes é
        // levantamento com problema, e o usuário precisa saber disso agora, e
        // não quando um pilar sair num lugar estranho.
        if (DiscardedTriangleCount > 0)
        {
            linhas.Add(
                Tr.F("  descartados:     {0:N0} triângulo(s) sem área útil (faceta vertical ou ponto repetido)", DiscardedTriangleCount));
        }

        return linhas;
    }

    private static string Metros(double valor) =>
        valor.ToString("N3", Tr.Culture) + " m";

    private static string Metros2(double valor) =>
        valor.ToString("N2", Tr.Culture) + " m²";
}
