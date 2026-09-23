namespace UFV.Geo;

/// <summary>
/// O resultado de drapear uma linha sobre o terreno.
/// </summary>
/// <param name="Vertices">
/// A linha em 3D, com os vértices originais mais os acrescentados onde ela
/// cruza o terreno.
/// </param>
/// <param name="OutsideIndices">
/// As posições, em <paramref name="Vertices"/>, dos pontos que caíram fora do
/// terreno.
///
/// Eles ficam na lista com a cota que já tinham — nem apagados, nem zerados.
/// Apagar mudaria o traçado que o usuário desenhou; zerar seria pior ainda,
/// porque cota zero é um número plausível, e um ponto no nível do mar no meio
/// de um terreno a 700 m passa despercebido até virar altura de pilar.
///
/// Quem chama decide o que fazer: avisar, pintar de outra cor, recusar a
/// área. O que não pode é não saber.
/// </param>
public sealed record DrapedLine(IReadOnlyList<Point3> Vertices, IReadOnlyList<int> OutsideIndices)
{
    /// <summary>Se algum trecho da linha ficou sem terreno embaixo.</summary>
    public bool HasGaps => OutsideIndices.Count > 0;

    /// <summary>Quantos pontos ficaram sem terreno embaixo.</summary>
    public int OutsideCount => OutsideIndices.Count;
}

/// <summary>
/// Drapeia uma linha sobre o terreno: dada a linha em planta, devolve a linha
/// em 3D, acompanhando o relevo.
///
/// O trabalho não é só ler a cota de cada vértice. Entre dois vértices
/// distantes, uma corda reta em 3D passa POR DENTRO do morro — em planta a
/// linha parece certa, e em 3D ela atravessa o terreno. Por isso a linha ganha
/// um vértice em cada aresta de triângulo que ela cruza: é ali que a
/// inclinação do terreno muda, e é o mínimo necessário para a linha assentar.
/// </summary>
public static class Draping
{
    /// <summary>
    /// Um milímetro: abaixo disso um vértice a mais não muda o desenho, e é a
    /// tolerância dos verificadores de regra sagrada.
    /// </summary>
    private const double Tolerancia = 0.001;

    /// <summary>
    /// A linha drapejada sobre o terreno.
    /// </summary>
    /// <param name="tin">O terreno.</param>
    /// <param name="vertices">
    /// A linha como o usuário desenhou. Só X e Y são usados; a cota de cada
    /// ponto vem do terreno.
    /// </param>
    public static DrapedLine Along(Tin tin, IReadOnlyList<Point3> vertices)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count == 0) return new DrapedLine([], []);

        var saida = new List<Point3>(vertices.Count * 2);
        var fora = new List<int>();

        // Quais pontos da saída o usuário desenhou. Os acrescentados por nós
        // podem ser enxugados depois; os dele, nunca — apagar um vértice que
        // ele traçou muda a área que ele delimitou.
        var doUsuario = new HashSet<int>();

        // O primeiro vértice entra sempre: é o começo da linha do usuário.
        doUsuario.Add(0);

        var (primeiro, primeiroDentro) = Amostrar(tin, vertices[0]);

        saida.Add(primeiro);
        if (!primeiroDentro) fora.Add(saida.Count - 1);

        for (var i = 1; i < vertices.Count; i++)
        {
            var inicio = vertices[i - 1];
            var fim = vertices[i];

            foreach (var t in Cruzamentos(tin, inicio, fim))
            {
                var x = inicio.X + (fim.X - inicio.X) * t;
                var y = inicio.Y + (fim.Y - inicio.Y) * t;

                var (cruzamento, cruzamentoDentro) = Amostrar(tin, new Point3(x, y, inicio.Z));

                // A posição só é anotada depois de o ponto ser aceito. Anotar
                // antes marcaria a vaga de um ponto recusado por repetir o
                // anterior, e o aviso apontaria o vértice errado.
                if (Acrescentar(saida, cruzamento) && !cruzamentoDentro)
                {
                    fora.Add(saida.Count - 1);
                }
            }

            var (ponta, pontaDentro) = Amostrar(tin, fim);

            if (Acrescentar(saida, ponta))
            {
                doUsuario.Add(saida.Count - 1);
                if (!pontaDentro) fora.Add(saida.Count - 1);
            }
        }

        return Enxugar(saida, fora, doUsuario);
    }

    /// <summary>
    /// Os parâmetros, entre 0 e 1, onde o segmento cruza alguma aresta de
    /// triângulo. Em ordem, sem repetir as pontas.
    /// </summary>
    private static IEnumerable<double> Cruzamentos(Tin tin, Point3 inicio, Point3 fim)
    {
        var encontrados = new List<double>();

        foreach (var triangulo in tin.TrianglesAlong(inicio.X, inicio.Y, fim.X, fim.Y))
        {
            Cruzar(encontrados, inicio, fim, triangulo.A, triangulo.B);
            Cruzar(encontrados, inicio, fim, triangulo.B, triangulo.C);
            Cruzar(encontrados, inicio, fim, triangulo.C, triangulo.A);
        }

        encontrados.Sort();

        // As pontas já entram por fora; e dois triângulos vizinhos dão o mesmo
        // cruzamento na aresta que compartilham.
        var anterior = double.NegativeInfinity;

        foreach (var t in encontrados)
        {
            if (t <= 1e-9 || t >= 1 - 1e-9) continue;
            if (t - anterior <= 1e-9) continue;

            anterior = t;
            yield return t;
        }
    }

    /// <summary>
    /// Se o segmento (p0, p1) cruza o segmento (a, b) em planta, guarda o
    /// parâmetro do cruzamento ao longo do primeiro.
    /// </summary>
    private static void Cruzar(List<double> destino, Point3 p0, Point3 p1, Point3 a, Point3 b)
    {
        var dx = p1.X - p0.X;
        var dy = p1.Y - p0.Y;
        var ex = b.X - a.X;
        var ey = b.Y - a.Y;

        var denominador = dx * ey - dy * ex;

        // Paralelos ou degenerados: não há um ponto de cruzamento. Uma aresta
        // deitada em cima do segmento também cai aqui, e é o caso em que os
        // vértices dela já entram pelos triângulos vizinhos.
        if (Math.Abs(denominador) < 1e-12) return;

        var ax = a.X - p0.X;
        var ay = a.Y - p0.Y;

        var t = (ax * ey - ay * ex) / denominador;
        var u = (ax * dy - ay * dx) / denominador;

        if (t is < 0 or > 1) return;
        if (u is < 0 or > 1) return;

        destino.Add(t);
    }

    /// <summary>
    /// O ponto com a cota do terreno, e se ela veio mesmo de lá.
    ///
    /// Amostrar não conhece a posição do ponto na saída de propósito: quem
    /// sabe se ele foi aceito é <see cref="Acrescentar"/>, e só depois disso a
    /// posição existe.
    /// </summary>
    private static (Point3 Ponto, bool Dentro) Amostrar(Tin tin, Point3 ponto)
    {
        if (tin.TryGetZ(ponto.X, ponto.Y, out var z)) return (ponto with { Z = z }, true);

        // A cota que veio é mantida de propósito. Zerar aqui seria inventar um
        // ponto no nível do mar, e ninguém desconfia de um zero.
        return (ponto, false);
    }

    /// <summary>Acrescenta o ponto, ou devolve falso se ele repetiria o anterior.</summary>
    private static bool Acrescentar(List<Point3> saida, Point3 ponto)
    {
        // Dois pontos no mesmo lugar não acrescentam nada ao traçado e ainda
        // atrapalham quem for medir comprimento depois.
        var ultimo = saida[^1];

        if (Math.Abs(ponto.X - ultimo.X) <= Tolerancia
            && Math.Abs(ponto.Y - ultimo.Y) <= Tolerancia)
        {
            return false;
        }

        saida.Add(ponto);
        return true;
    }

    /// <summary>
    /// Tira os vértices que não mudam o traçado: os que caem sobre a reta
    /// entre o anterior e o seguinte, dentro de um milímetro.
    ///
    /// Sem isto, uma linha reta sobre terreno plano sairia com um vértice em
    /// cada triângulo atravessado — dezenas de milhares numa área de usina —
    /// sem nenhum ganho de precisão, e o desenho ficaria pesado à toa.
    /// </summary>
    private static DrapedLine Enxugar(List<Point3> pontos, List<int> fora, HashSet<int> doUsuario)
    {
        if (pontos.Count <= 2) return new DrapedLine(pontos, fora);

        var semTerreno = new HashSet<int>(fora);

        var enxuto = new List<Point3>(pontos.Count) { pontos[0] };
        var foraEnxuto = new List<int>(fora.Count);

        if (semTerreno.Contains(0)) foraEnxuto.Add(0);

        for (var i = 1; i < pontos.Count - 1; i++)
        {
            // Vértice do usuário nunca é enxugado, mesmo parecendo
            // redundante: apagá-lo muda o traçado dele, e um vértice colinear
            // por acaso continua sendo um vértice que ele escolheu.
            //
            // Os pontos sem terreno embaixo estão cobertos por esta mesma
            // regra, e não por acaso: os que nós acrescentamos vêm de
            // cruzamentos com arestas de triângulo, que só existem dentro do
            // terreno. Todo ponto fora, portanto, é um vértice do usuário.
            // Uma checagem separada aqui pareceria uma proteção e não teria
            // como ser testada — não há entrada que a exercite sozinha.
            if (doUsuario.Contains(i) || !SobreARetaEntre(pontos[i], enxuto[^1], pontos[i + 1]))
            {
                if (semTerreno.Contains(i)) foraEnxuto.Add(enxuto.Count);
                enxuto.Add(pontos[i]);
            }
        }

        if (semTerreno.Contains(pontos.Count - 1)) foraEnxuto.Add(enxuto.Count);
        enxuto.Add(pontos[^1]);

        return new DrapedLine(enxuto, foraEnxuto);
    }

    private static bool SobreARetaEntre(Point3 meio, Point3 antes, Point3 depois)
    {
        var dx = depois.X - antes.X;
        var dy = depois.Y - antes.Y;
        var dz = depois.Z - antes.Z;

        var comprimento = Math.Sqrt(dx * dx + dy * dy);
        if (comprimento <= Tolerancia) return false;

        // Onde o ponto do meio cai ao longo do trecho, em planta.
        var t = ((meio.X - antes.X) * dx + (meio.Y - antes.Y) * dy) / (comprimento * comprimento);
        if (t is < 0 or > 1) return false;

        // A cota que a reta teria ali. Se a diferença couber no milímetro, o
        // vértice não está dizendo nada.
        var zDaReta = antes.Z + dz * t;

        return Math.Abs(meio.Z - zDaReta) <= Tolerancia;
    }
}
