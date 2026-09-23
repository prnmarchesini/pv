using System.Globalization;
using UFV.Geo;

namespace UFV.Core.Invariants;

/// <summary>
/// Regra sagrada 2: a mesa é monolito e nunca entorta.
///
/// "A mesa é corpo rígido. Todos os módulos e todos os topos de pilar de uma
/// mesa estão no mesmo plano. Verificador: distância de cada vértice ao plano
/// da mesa &lt; 1 mm."
///
/// Este verificador existe porque o defeito que ele pega é invisível. Uma mesa
/// entortada por dois milímetros parece perfeita no desenho, orbita bonito em
/// 3D, e só aparece quando o montador não consegue parafusar o módulo. A
/// origem costuma ser inocente: calcular cada vértice com sua própria
/// trigonometria em vez de passar a mesa inteira por uma matriz só.
/// </summary>
public static class RigidTable
{
    /// <summary>
    /// Um milímetro, como manda a arquitetura para verificador de regra.
    ///
    /// Não é 1e-6: essa é a tolerância de comparação geométrica. Aqui o que se
    /// mede é fabricação, e um décimo de milímetro de ruído de arredondamento
    /// não pode reprovar uma mesa — um verificador assim é desligado na
    /// primeira semana, e aí não sobra verificador nenhum.
    /// </summary>
    public const double Tolerancia = 0.001;

    /// <summary>
    /// A tolerância de comparação geométrica da arquitetura, 1e-6 m.
    ///
    /// É outra coisa que a <see cref="Tolerancia"/> de regra: esta diz quando
    /// dois pontos são o mesmo ponto; aquela diz quando uma mesa está torta.
    /// </summary>
    private const double ToleranciaGeometrica = 1e-6;

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O que há de errado com a mesa, em português, ou null se ela é monolito.
    /// </summary>
    public static string? Check(TableGeometry mesa)
    {
        if (mesa is null) return "não há mesa para conferir";

        var pontos = mesa.PlanePoints().ToList();

        if (pontos.Count < 3) return "a mesa não tem vértices suficientes para ter um plano";

        foreach (var ponto in pontos)
        {
            // Antes de qualquer conta: com NaN em jogo, toda comparação dá
            // falso e o verificador aprovaria a mesa exatamente quando ela
            // está mais quebrada.
            if (!ponto.IsFinite) return "a mesa tem vértice com coordenada inválida";
        }

        if (!TryPlano(pontos, out var origem, out var normal))
            return "os vértices da mesa não definem um plano";

        var pior = 0.0;
        Point3 culpado = default;

        foreach (var ponto in pontos)
        {
            var distancia = Math.Abs(
                normal.X * (ponto.X - origem.X)
                + normal.Y * (ponto.Y - origem.Y)
                + normal.Z * (ponto.Z - origem.Z));

            if (distancia <= pior) continue;

            pior = distancia;
            culpado = ponto;
        }

        // A regra escrita é "menor que 1 mm", e não "até 1 mm".
        if (pior < Tolerancia) return null;

        return $"a mesa não é plana: o vértice ({Texto(culpado.X)}, {Texto(culpado.Y)}, "
            + $"{Texto(culpado.Z)}) está a {Milimetros(pior)} mm do plano da mesa, "
            + $"e o limite é {Milimetros(Tolerancia)} mm";
    }

    /// <summary>
    /// O plano que melhor cabe nos pontos: o centroide e a normal unitária.
    ///
    /// É o plano dos mínimos quadrados, obtido da matriz de covariância — a
    /// normal é a direção em que os pontos menos se espalham.
    ///
    /// Duas tentativas anteriores não serviram, e vale registrar por quê.
    /// Passar o plano pelo primeiro vértice prendia o resultado a ele: o
    /// verificador media até três vezes mais desvio do que existia e, quando o
    /// vértice fora do lugar era justamente o primeiro, a mensagem acusava um
    /// vizinho inocente. Tirar a direção de três pontos extremos era pior de
    /// um jeito mais perigoso: o vértice torto costuma SER um dos extremos, e
    /// aí o plano se inclinava para acompanhá-lo — o verificador aprovava
    /// exatamente a mesa que devia reprovar.
    ///
    /// Com a covariância, um vértice fora do lugar entre cento e poucos
    /// arrasta o plano por um cento e poucos avos do desvio dele, e o resto
    /// aparece.
    /// </summary>
    private static bool TryPlano(IReadOnlyList<Point3> pontos, out Point3 origem, out Point3 normal)
    {
        origem = Centroide(pontos);
        normal = default;

        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;

        foreach (var ponto in pontos)
        {
            var dx = ponto.X - origem.X;
            var dy = ponto.Y - origem.Y;
            var dz = ponto.Z - origem.Z;

            xx += dx * dx; xy += dx * dy; xz += dx * dz;
            yy += dy * dy; yz += dy * dz; zz += dz * dz;
        }

        var covariancia = new[,] { { xx, xy, xz }, { xy, yy, yz }, { xz, yz, zz } };

        Jacobi(covariancia, out var valores, out var vetores);

        // A normal é a direção de menor espalhamento.
        var menor = 0;
        for (var i = 1; i < 3; i++)
        {
            if (valores[i] < valores[menor]) menor = i;
        }

        // Se as OUTRAS duas direções também mal se espalham, os pontos estão
        // numa reta ou num ponto só, e não há plano nenhum.
        var espalhamento = 0.0;
        for (var i = 0; i < 3; i++)
        {
            if (i != menor) espalhamento = Math.Max(espalhamento, valores[i]);
        }

        if (espalhamento <= ToleranciaGeometrica * ToleranciaGeometrica) return false;

        normal = new Point3(vetores[0, menor], vetores[1, menor], vetores[2, menor]);

        var tamanho = Norma(normal);
        if (tamanho <= 0) return false;

        normal = new Point3(normal.X / tamanho, normal.Y / tamanho, normal.Z / tamanho);
        return true;
    }

    /// <summary>
    /// Diagonaliza uma matriz simétrica 3×3 por rotações de Jacobi.
    ///
    /// Três por três teria solução fechada, mas ela é frágil justamente no
    /// caso que aqui é o normal: autovalores repetidos, que é o que acontece
    /// numa mesa perfeitamente plana. Jacobi não se importa com isso e
    /// converge em poucas varreduras.
    /// </summary>
    private static void Jacobi(double[,] matriz, out double[] valores, out double[,] vetores)
    {
        var a = (double[,])matriz.Clone();

        vetores = new double[3, 3];
        for (var i = 0; i < 3; i++) vetores[i, i] = 1;

        for (var varredura = 0; varredura < 32; varredura++)
        {
            var forte = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
            if (forte == 0) break;

            for (var p = 0; p < 2; p++)
            {
                for (var q = p + 1; q < 3; q++)
                {
                    if (a[p, q] == 0) continue;

                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));

                    if (theta == 0) t = 1;

                    var c = 1 / Math.Sqrt(t * t + 1);
                    var s = t * c;

                    for (var i = 0; i < 3; i++)
                    {
                        var aip = a[i, p];
                        var aiq = a[i, q];

                        a[i, p] = c * aip - s * aiq;
                        a[i, q] = s * aip + c * aiq;
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        var api = a[p, i];
                        var aqi = a[q, i];

                        a[p, i] = c * api - s * aqi;
                        a[q, i] = s * api + c * aqi;
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        var vip = vetores[i, p];
                        var viq = vetores[i, q];

                        vetores[i, p] = c * vip - s * viq;
                        vetores[i, q] = s * vip + c * viq;
                    }
                }
            }
        }

        valores = [a[0, 0], a[1, 1], a[2, 2]];
    }

    private static Point3 Centroide(IReadOnlyList<Point3> pontos)
    {
        double x = 0, y = 0, z = 0;

        foreach (var ponto in pontos)
        {
            x += ponto.X;
            y += ponto.Y;
            z += ponto.Z;
        }

        return new Point3(x / pontos.Count, y / pontos.Count, z / pontos.Count);
    }

    private static double Norma(Point3 a) => Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);

    private static string Texto(double valor) => valor.ToString("0.###", Brasil);

    private static string Milimetros(double metros) => (metros * 1000).ToString("0.##", Brasil);
}
