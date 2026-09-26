namespace UFV.Geo;

/// <summary>
/// O polígono em planta: só X e Y, o Z é ignorado.
///
/// É o contorno da área de implantação visto de cima. O que a distribuição de
/// mesas precisa dele é pouco e preciso: se um ponto está dentro, e em que
/// parâmetros uma reta o atravessa. As duas contas são clássicas, e as duas
/// têm um caso que engana — a reta que passa exatamente por um vértice, que
/// sem cuidado conta o vértice duas vezes e inverte dentro e fora dali para a
/// frente. Por isso vivem aqui, com teste, e não espalhadas por quem as usa.
///
/// O polígono é dado pelos vértices em ordem, sem precisar repetir o primeiro
/// no fim (se repetir, a aresta de comprimento zero é ignorada). Horário ou
/// anti-horário tanto faz.
/// </summary>
public static class Polygons
{
    /// <summary>
    /// Tolerância para aresta e direção de comprimento zero, e para "ponto
    /// sobre a aresta", em metro. Mais fina que o 1e-6 da arquitetura de
    /// propósito: aqui ela decide se um vértice está EM CIMA da reta, e um
    /// micrômetro já é mais do que qualquer erro de coordenada UTM carrega.
    /// </summary>
    private const double Tolerancia = 1e-9;

    /// <summary>
    /// Se o ponto está dentro do polígono ou na borda dele.
    ///
    /// Dentro é pelo critério par-ímpar: um raio horizontal a partir do ponto
    /// cruza o contorno um número ímpar de vezes. A borda é conferida à
    /// parte, porque pelo par-ímpar um ponto exatamente sobre ela cai de
    /// qualquer lado — e o canto de uma mesa encostada na borda da área é o
    /// caso mais comum que existe, não uma curiosidade.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Menos de três vértices, ou ponto não finito.</exception>
    public static bool Contains(IReadOnlyList<Point3> vertices, double x, double y)
    {
        Conferir(vertices);

        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "O ponto não é finito.");

        var dentro = false;
        var n = vertices.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = vertices[j];
            var b = vertices[i];

            if (SobreAAresta(a, b, x, y)) return true;

            // A aresta cruza a horizontal do ponto se uma ponta está acima e a
            // outra não. A meia-abertura ((a.Y > y) != (b.Y > y)) é o que faz
            // um vértice exatamente na altura do ponto contar uma vez só.
            if ((b.Y > y) != (a.Y > y))
            {
                var xCruz = a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);

                if (x < xCruz) dentro = !dentro;
            }
        }

        return dentro;
    }

    /// <summary>
    /// Os parâmetros <c>t</c> em que a reta <c>origem + t·direção</c> atravessa
    /// o contorno, em ordem crescente. Entre um cruzamento e o seguinte a reta
    /// está dentro; antes do primeiro e depois do último, fora.
    ///
    /// O parâmetro é em unidades da direção: com direção unitária, é metro.
    ///
    /// Vértice ou aresta em cima da reta decide pelo lado dos vizinhos: um
    /// vértice em cima da reta herda o lado do último vértice antes dele que
    /// não está em cima, e o cruzamento acontece só onde o lado muda. Assim
    /// a reta que atravessa o polígono por um vértice cruza uma vez, a que só
    /// encosta num vértice por fora não cruza, e a que corre por cima de uma
    /// aresta é tangente e não cruza — de qualquer lado que o polígono esteja.
    /// A paridade fora/dentro/fora se mantém sempre, que é o que importa para
    /// quem lê os cruzamentos aos pares.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Menos de três vértices, direção nula ou ponto não finito.</exception>
    public static IReadOnlyList<double> Crossings(IReadOnlyList<Point3> vertices, Point3 origem, Point3 direcao)
    {
        Conferir(vertices);

        if (!origem.IsFinite) throw new ArgumentOutOfRangeException(nameof(origem), "A origem não é finita.");
        if (!direcao.IsFinite) throw new ArgumentOutOfRangeException(nameof(direcao), "A direção não é finita.");

        var dx = direcao.X;
        var dy = direcao.Y;

        if (dx * dx + dy * dy <= Tolerancia * Tolerancia)
            throw new ArgumentOutOfRangeException(nameof(direcao), "A direção tem comprimento zero em planta.");

        var cruzamentos = new List<double>();
        var n = vertices.Count;
        var comprimento = Math.Sqrt(dx * dx + dy * dy);

        // Lado de cada vértice em relação à reta: +1, −1, ou 0 se está em
        // cima dela (a menos da tolerância, em metro).
        var lados = new int[n];

        for (var i = 0; i < n; i++)
        {
            var v = vertices[i];
            var distancia = ((v.X - origem.X) * dy - (v.Y - origem.Y) * dx) / comprimento;

            lados[i] = Math.Abs(distancia) <= Tolerancia ? 0 : Math.Sign(distancia);
        }

        // Vértice em cima da reta herda o lado do último vértice antes dele
        // que não está em cima. Polígono inteiro em cima da reta não tem lado
        // e não cruza nada.
        var ultimo = 0;

        for (var i = n - 1; i >= 0; i--)
        {
            if (lados[i] != 0)
            {
                ultimo = lados[i];
                break;
            }
        }

        if (ultimo == 0) return cruzamentos;

        for (var i = 0; i < n; i++)
        {
            if (lados[i] == 0) lados[i] = ultimo;
            else ultimo = lados[i];
        }

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (lados[j] == lados[i]) continue;

            var a = vertices[j];
            var b = vertices[i];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;

            // Resolvendo origem + t·d = a + s·e: t = ((a − origem) × e) / (d × e),
            // com u × v = u.x·v.y − u.y·v.x. O denominador não é zero: uma
            // aresta com lados diferentes nas pontas não é paralela à reta.
            var denominador = dx * ey - dy * ex;
            var t = ((a.X - origem.X) * ey - (a.Y - origem.Y) * ex) / denominador;

            cruzamentos.Add(t);
        }

        cruzamentos.Sort();
        return cruzamentos;
    }

    /// <summary>
    /// Se os segmentos a–b e c–d se cruzam de verdade: um atravessa o outro,
    /// com as quatro pontas fora da reta do outro. Encostar ponta em ponta,
    /// ponta em cima do outro, ou correr colinear não é cruzar — é o que
    /// acontece quando uma mesa está encostada na borda da área, e encostar
    /// não é sair.
    /// </summary>
    public static bool SegmentsCross(Point3 a, Point3 b, Point3 c, Point3 d)
    {
        var ab = Lado(a, b, c) * Lado(a, b, d);
        var cd = Lado(c, d, a) * Lado(c, d, b);

        return ab < 0 && cd < 0;
    }

    /// <summary>O sinal do ponto p em relação à reta a–b: +1, −1, ou 0 em cima dela.</summary>
    private static int Lado(Point3 a, Point3 b, Point3 p)
    {
        var ex = b.X - a.X;
        var ey = b.Y - a.Y;
        var comprimento = Math.Sqrt(ex * ex + ey * ey);

        if (comprimento <= Tolerancia) return 0;

        var distancia = ((p.X - a.X) * ey - (p.Y - a.Y) * ex) / comprimento;

        return Math.Abs(distancia) <= Tolerancia ? 0 : Math.Sign(distancia);
    }

    /// <summary>Se o ponto está sobre o segmento a–b, com a tolerância da classe.</summary>
    private static bool SobreAAresta(Point3 a, Point3 b, double x, double y)
    {
        var ex = b.X - a.X;
        var ey = b.Y - a.Y;
        var comprimento2 = ex * ex + ey * ey;

        if (comprimento2 <= Tolerancia * Tolerancia) return false;

        // Projeção do ponto no segmento, presa às pontas.
        var s = ((x - a.X) * ex + (y - a.Y) * ey) / comprimento2;

        if (s < 0 || s > 1) return false;

        var px = a.X + s * ex - x;
        var py = a.Y + s * ey - y;

        return px * px + py * py <= Tolerancia * Tolerancia;
    }

    private static void Conferir(IReadOnlyList<Point3> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(vertices), vertices.Count,
                "Um polígono precisa de pelo menos três vértices.");
        }

        foreach (var v in vertices)
        {
            if (!v.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(vertices), "O polígono tem vértice não finito.");
        }
    }
}
