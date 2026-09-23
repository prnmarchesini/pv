using Autodesk.Civil.DatabaseServices;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// O que a leitura da superfície produziu.
/// </summary>
/// <param name="Triangles">Os triângulos aproveitados, em metros.</param>
/// <param name="UnreadableCount">
/// Quantos triângulos não puderam ser lidos. Precisa chegar ao usuário: uma
/// superfície sistematicamente ilegível produziria um resumo com cotas e área
/// plausíveis, calculadas sobre um punhado de triângulos, e ninguém
/// desconfiaria do número.
/// </param>
internal sealed record SurfaceMesh(IReadOnlyList<Triangle> Triangles, int UnreadableCount);

/// <summary>
/// Converte a superfície do Civil 3D na malha que o motor entende.
///
/// É a fronteira: daqui para lá existe TinSurface, transação e ObjectId; daqui
/// para cá existe só <see cref="Triangle"/> com metros e double. Nenhuma
/// decisão de cálculo mora aqui.
/// </summary>
internal static class SurfaceExtractor
{
    /// <summary>
    /// Os triângulos da superfície, já em coordenadas de metro.
    /// </summary>
    /// <remarks>
    /// Só os visíveis. Um triângulo invisível foi excluído da superfície pelo
    /// contorno, pelo comprimento máximo de aresta ou à mão — o Civil 3D não
    /// responde cota nele, e o motor também não pode responder: seria terreno
    /// onde o engenheiro já decidiu que não há.
    /// </remarks>
    internal static SurfaceMesh Extract(TinSurface superficie)
    {
        ArgumentNullException.ThrowIfNull(superficie);

        // As coleções e os objetos do Civil 3D seguram recursos nativos: sem
        // descartar, uma superfície de milhões de triângulos deixa para trás
        // um wrapper por triângulo e três por vértice, todos esperando o
        // finalizador.
        using var colecao = superficie.Triangles;

        // A lista nasce do tamanho certo: sem isso ela dobra de tamanho umas
        // vinte vezes no caminho, copiando tudo a cada vez.
        var triangulos = new List<Triangle>(Math.Max(colecao.Count, 0));
        var ilegiveis = 0;
        Exception? primeiraFalha = null;

        foreach (TinSurfaceTriangle triangulo in colecao)
        {
            try
            {
                if (!triangulo.IsValid || !triangulo.IsVisible) continue;

                triangulos.Add(new Triangle(
                    Ler(triangulo.Vertex1),
                    Ler(triangulo.Vertex2),
                    Ler(triangulo.Vertex3)));
            }
            catch (Exception erro)
            {
                // Um triângulo ilegível não pode custar a malha inteira. Ele
                // vira um buraco, e buraco o motor sabe tratar: a consulta
                // devolve "fora do terreno" em vez de inventar cota.
                //
                // O registro é agregado de propósito. Escrever no log a cada
                // triângulo seria um abrir-e-fechar de arquivo por ocorrência
                // — o comando travaria numa superfície com falha sistemática,
                // e o rodízio do log apagaria o diagnóstico anterior.
                ilegiveis++;
                primeiraFalha ??= erro;
            }
            finally
            {
                triangulo.Dispose();
            }
        }

        if (ilegiveis > 0)
        {
            RegistroDeDiagnostico.Registrar(
                $"{ilegiveis} triângulo(s) ilegível(is) na superfície; ignorados. Primeira falha:",
                primeiraFalha);
        }

        return new SurfaceMesh(triangulos, ilegiveis);
    }

    private static Point3 Ler(TinSurfaceVertex vertice)
    {
        using (vertice)
        {
            var p = vertice.Location;
            return new Point3(p.X, p.Y, p.Z);
        }
    }
}
