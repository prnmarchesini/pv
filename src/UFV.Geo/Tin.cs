namespace UFV.Geo;

/// <summary>
/// A malha triangular do terreno, já fora do CAD.
///
/// É a única coisa que o motor sabe sobre o terreno: uma lista de triângulos e
/// a pergunta "qual a cota em (x, y)?". O Civil 3D fica do lado de lá, no
/// plugin, que lê a TinSurface e monta isto.
///
/// Os triângulos que não servem para responder cota — vértice com NaN ou
/// infinito, faceta vertical, fatia sem área útil — são separados na
/// construção, uma vez, em vez de reconferidos a cada consulta. Quantos foram
/// é informação, não detalhe: uma malha com muitos descartes é um levantamento
/// com problema, e o resumo do passo 1.4 precisa poder dizer isso.
///
/// A busca usa um índice espacial (<see cref="TriangleGrid"/>): sem ele, cada
/// consulta varre a malha inteira, o que num terreno real é meia hora de
/// espera para o que devia levar um segundo. O índice não muda a resposta —
/// só o caminho até ela — e <see cref="TryGetZLinear"/> existe para os testes
/// poderem conferir isso.
/// </summary>
public sealed class Tin
{
    private readonly Triangle[] _triangles;

    /// <summary>
    /// Denominador baricêntrico de cada triângulo, calculado uma vez. Ele é o
    /// dobro da área com sinal, e antes era recalculado duas vezes por
    /// triângulo por consulta.
    /// </summary>
    private readonly double[] _denominators;

    /// <summary>
    /// Grade que diz quais triângulos podem conter um ponto. Nula só quando a
    /// malha está vazia.
    /// </summary>
    private readonly TriangleGrid? _grid;

    public Tin(IEnumerable<Triangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        var aproveitados = new List<Triangle>();
        var descartados = 0;

        foreach (var triangle in triangles)
        {
            // IsDegenerate2D já recusa vértice com NaN ou infinito: sem os
            // três pontos finitos não há área que se possa medir.
            if (triangle.IsDegenerate2D)
            {
                descartados++;
                continue;
            }

            aproveitados.Add(triangle);
        }

        _triangles = aproveitados.ToArray();
        _denominators = new double[_triangles.Length];

        for (var i = 0; i < _triangles.Length; i++)
            _denominators[i] = _triangles[i].DoubleSignedArea2D;

        DiscardedTriangleCount = descartados;
        _grid = _triangles.Length > 0 ? new TriangleGrid(_triangles) : null;
    }

    /// <summary>Quantos triângulos a malha usa para responder cota.</summary>
    public int TriangleCount => _triangles.Length;

    /// <summary>
    /// Quantos triângulos foram separados na construção por não servirem para
    /// responder cota.
    /// </summary>
    public int DiscardedTriangleCount { get; }

    /// <summary>
    /// Cota do terreno em (x, y), interpolada dentro do triângulo que contém o
    /// ponto.
    /// </summary>
    /// <returns>
    /// Falso quando o ponto cai fora do terreno — fora da borda, ou num buraco
    /// da triangulação. Fora do terreno não é cota zero: é ausência de
    /// resposta, e quem chama precisa decidir o que fazer.
    /// </returns>
    public bool TryGetZ(double x, double y, out double z)
    {
        z = 0;

        // NaN e infinito não caem em triângulo nenhum, mas as comparações com
        // NaN são todas falsas e um infinito vira NaN no meio da conta: os
        // dois passariam pelo teste de dentro/fora sem disparar nada e sairiam
        // como cota, contaminando tudo que vier depois.
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        if (_grid is null) return false;

        foreach (var i in _grid.Candidates(x, y))
        {
            if (TryInterpolate(_triangles[i], _denominators[i], x, y, out z)) return true;
        }

        // Os grandes demais ficam fora da grade e são olhados sempre. Numa
        // malha regular esta lista é vazia; numa superfície de curvas de nível
        // ela tem os poucos triângulos compridos e diagonais.
        foreach (var i in _grid.Oversized)
        {
            if (TryInterpolate(_triangles[i], _denominators[i], x, y, out z)) return true;
        }

        z = 0;
        return false;
    }

    /// <summary>
    /// A mesma consulta, varrendo todos os triângulos, sem índice.
    ///
    /// Existe para os testes poderem afirmar que o índice não mudou resposta
    /// nenhuma: um índice que perde um triângulo devolve "fora do terreno"
    /// para um ponto que está dentro, e isso não aparece em teste que só use o
    /// próprio índice.
    /// </summary>
    internal bool TryGetZLinear(double x, double y, out double z)
    {
        z = 0;

        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;

        for (var i = 0; i < _triangles.Length; i++)
        {
            if (TryInterpolate(_triangles[i], _denominators[i], x, y, out z)) return true;
        }

        z = 0;
        return false;
    }

    /// <summary>Quantas células o índice tem. Só para diagnóstico e teste.</summary>
    internal long CellCount => _grid?.CellCount ?? 0;

    /// <summary>Entradas do índice, contando triângulo que entra em mais de uma célula.</summary>
    internal long OccupancyCount => _grid?.OccupancyCount ?? 0;

    /// <summary>Triângulos varridos em toda consulta por cobrirem células demais.</summary>
    internal int OversizedCount => _grid?.OversizedCount ?? 0;

    /// <summary>
    /// Coordenadas baricêntricas do ponto em relação ao triângulo, em planta.
    /// Se as três forem não negativas, o ponto está dentro (ou na borda), e as
    /// mesmas três pesam as cotas dos vértices.
    /// </summary>
    /// <param name="denominator">
    /// O dobro da área com sinal, já calculado. Quem monta o Tin garante que
    /// não é zero: triângulo sem área útil não chega aqui.
    /// </param>
    internal static bool TryInterpolate(
        in Triangle triangle,
        double denominator,
        double x,
        double y,
        out double z)
    {
        z = 0;

        // A guarda mora aqui, e não só em quem chama: é aqui que o estrago
        // aconteceria. Com x ou y não finito os pesos viram NaN, toda
        // comparação com NaN é falsa, e o ponto sairia "dentro" com cota NaN.
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;

        var (a, b, c) = (triangle.A, triangle.B, triangle.C);

        var pesoA = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / denominator;
        var pesoB = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / denominator;
        var pesoC = 1.0 - pesoA - pesoB;

        // Os pesos são adimensionais, então a folga aqui também é. Ela existe
        // para o ponto que cai exatamente numa aresta ou num vértice não ser
        // recusado pelos dois triângulos vizinhos por erro de arredondamento:
        // um buraco de um ponto no meio do terreno é pior que uma sobreposição
        // de um ponto, porque nas duas faces a cota interpolada é a mesma.
        //
        // O peso é distância normalizada à aresta oposta, então esta folga
        // vale altura × 1e-9: nanômetros num triângulo de 25 cm, dezenas de
        // nanômetros num de 50 m. Cinco ordens abaixo do milímetro dos
        // verificadores de regra sagrada, e sete acima do ruído que ela
        // absorve. Há teste prendendo essa ordem de grandeza.
        const double folga = 1e-9;

        if (pesoA < -folga || pesoB < -folga || pesoC < -folga) return false;

        z = pesoA * a.Z + pesoB * b.Z + pesoC * c.Z;
        return true;
    }
}
