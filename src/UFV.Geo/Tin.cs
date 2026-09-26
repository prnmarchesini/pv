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

        Medir();
    }

    /// <summary>
    /// Cota mais baixa e mais alta do terreno, e as duas áreas. Sai de uma
    /// passada só na construção: são os números do resumo que o usuário
    /// confere contra o Civil 3D, e recalculá-los a cada pergunta seria
    /// desperdício.
    /// </summary>
    private void Medir()
    {
        if (_triangles.Length == 0)
        {
            MinZ = 0;
            MaxZ = 0;
            return;
        }

        var menor = double.MaxValue;
        var maior = double.MinValue;
        double area2d = 0, area3d = 0;

        for (var i = 0; i < _triangles.Length; i++)
        {
            var t = _triangles[i];

            // Sem alocar um array por triângulo para percorrer três pontos:
            // com 2 milhões deles isso eram quase 200 MB de lixo. É o mesmo
            // engano que TriangleGrid.Envolver já tinha corrigido.
            menor = Math.Min(menor, Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)));
            maior = Math.Max(maior, Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)));

            // Em planta, metade do dobro da área com sinal. O valor absoluto
            // não é preciosismo: o Civil 3D não garante que todos os
            // triângulos girem para o mesmo lado, e sem ele os horários
            // subtrairiam dos anti-horários — a área sairia menor, e
            // plausível.
            area2d += Math.Abs(_denominators[i]) * 0.5;

            // No espaço, metade da norma do produto vetorial dos dois lados.
            // É a área que o terreno teria se fosse desdobrado: num terreno
            // inclinado ela é sempre maior que a projetada, e é ela que diz
            // quanto de chão existe de verdade.
            var (ux, uy, uz) = (t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z);
            var (vx, vy, vz) = (t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z);

            var nx = uy * vz - uz * vy;
            var ny = uz * vx - ux * vz;
            var nz = ux * vy - uy * vx;

            area3d += Math.Sqrt(nx * nx + ny * ny + nz * nz) * 0.5;
        }

        MinZ = menor;
        MaxZ = maior;
        Area2D = area2d;
        Area3D = area3d;
    }

    /// <summary>Cota mais baixa da malha, em metros. Zero se a malha está vazia.</summary>
    public double MinZ { get; private set; }

    /// <summary>Cota mais alta da malha, em metros. Zero se a malha está vazia.</summary>
    public double MaxZ { get; private set; }

    /// <summary>Área projetada em planta, em metros quadrados. É a que se mede num mapa.</summary>
    public double Area2D { get; private set; }

    /// <summary>
    /// Área da superfície no espaço, em metros quadrados. Num terreno
    /// inclinado é maior que a projetada, e é ela que diz quanto de chão
    /// existe de verdade.
    /// </summary>
    public double Area3D { get; private set; }

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
    /// A cota mais alta do terreno ao longo do segmento em planta de (x0, y0)
    /// a (x1, y1).
    ///
    /// Não é o máximo de alguns pontos amostrados: é o máximo de verdade. A
    /// cota é linear dentro de cada triângulo, então ao longo do pedaço do
    /// segmento que cai num triângulo o máximo está numa das duas pontas do
    /// pedaço — e as pontas são onde o segmento entra e sai do triângulo. É
    /// isso que se avalia, triângulo a triângulo, pelos candidatos do índice.
    ///
    /// Existe para a ponta baixa do módulo: a regra sagrada 4 mede da ponta
    /// baixa até o terreno, e o terreno que importa é o que chega mais perto
    /// em QUALQUER ponto da aresta. Três pontos amostrados deixavam passar
    /// uma crista entre eles.
    /// </summary>
    /// <returns>
    /// Falso se qualquer parte do segmento fica sem terreno embaixo (fora da
    /// borda, ou num buraco da triangulação). Não se responde o máximo de
    /// metade de uma aresta: fora do terreno é ausência de resposta.
    /// </returns>
    public bool TryGetMaxZAlong(double x0, double y0, double x1, double y1, out double z)
    {
        z = 0;

        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1))
            return false;

        if (_grid is null) return false;

        // Segmento de comprimento zero: é um ponto.
        if (Math.Abs(x1 - x0) <= 1e-12 && Math.Abs(y1 - y0) <= 1e-12) return TryGetZ(x0, y0, out z);

        var pedacos = new List<(double T0, double T1)>();
        var maximo = double.NegativeInfinity;

        foreach (var i in _grid.CandidatesAlong(x0, y0, x1, y1).Concat(_grid.Oversized.ToArray()))
        {
            if (!PedacoDentro(_triangles[i], _denominators[i], x0, y0, x1, y1, out var t0, out var t1, out var zT0, out var zT1))
                continue;

            pedacos.Add((t0, t1));
            maximo = Math.Max(maximo, Math.Max(zT0, zT1));
        }

        if (pedacos.Count == 0) return false;

        // O segmento inteiro precisa estar coberto: os pedaços, em ordem,
        // têm que emendar de 0 a 1. Folga adimensional, como a dos pesos.
        const double folga = 1e-9;

        pedacos.Sort((p, q) => p.T0.CompareTo(q.T0));

        var coberto = 0.0;

        foreach (var (t0, t1) in pedacos)
        {
            if (t0 > coberto + folga) return false;

            coberto = Math.Max(coberto, t1);
        }

        if (coberto < 1 - folga) return false;

        z = maximo;
        return true;
    }

    /// <summary>
    /// O pedaço do segmento, em parâmetro t de 0 a 1, que cai dentro do
    /// triângulo, e a cota do terreno nas duas pontas do pedaço.
    ///
    /// Os pesos baricêntricos são afins em t, então cada um dá uma
    /// desigualdade linear em t, e o pedaço é a interseção das três com [0, 1].
    /// </summary>
    private static bool PedacoDentro(
        in Triangle triangle, double denominator,
        double x0, double y0, double x1, double y1,
        out double t0, out double t1, out double zT0, out double zT1)
    {
        t0 = 0;
        t1 = 1;
        zT0 = zT1 = 0;

        var (a, b, c) = (triangle.A, triangle.B, triangle.C);

        // Pesos nas duas pontas do segmento.
        var pa0 = ((b.Y - c.Y) * (x0 - c.X) + (c.X - b.X) * (y0 - c.Y)) / denominator;
        var pb0 = ((c.Y - a.Y) * (x0 - c.X) + (a.X - c.X) * (y0 - c.Y)) / denominator;
        var pc0 = 1.0 - pa0 - pb0;

        var pa1 = ((b.Y - c.Y) * (x1 - c.X) + (c.X - b.X) * (y1 - c.Y)) / denominator;
        var pb1 = ((c.Y - a.Y) * (x1 - c.X) + (a.X - c.X) * (y1 - c.Y)) / denominator;
        var pc1 = 1.0 - pa1 - pb1;

        const double folga = 1e-9;

        // Dois recortes: um com folga, para a cobertura (dois triângulos
        // vizinhos precisam emendar sem fresta de arredondamento), e um
        // exato, para a cota. Avaliar a cota no corte com folga extrapola o
        // plano do triângulo um nada além da aresta, e numa crista isso
        // devolve um máximo que o terreno não tem.
        var e0 = 0.0;
        var e1 = 1.0;

        // Cada peso p(t) = p0 + t·(p1 − p0) ≥ 0 recorta [t0, t1].
        foreach (var (p0, p1) in new[] { (pa0, pa1), (pb0, pb1), (pc0, pc1) })
        {
            var delta = p1 - p0;

            if (Math.Abs(delta) <= 1e-15)
            {
                if (p0 < -folga) return false;
                continue;
            }

            var comFolga = (-folga - p0) / delta;
            var exato = -p0 / delta;

            if (delta > 0)
            {
                t0 = Math.Max(t0, comFolga);
                e0 = Math.Max(e0, exato);
            }
            else
            {
                t1 = Math.Min(t1, comFolga);
                e1 = Math.Min(e1, exato);
            }
        }

        if (t1 < t0) return false;

        double Cota(double t)
        {
            var pa = pa0 + t * (pa1 - pa0);
            var pb = pb0 + t * (pb1 - pb0);
            var pc = 1.0 - pa - pb;

            return pa * a.Z + pb * b.Z + pc * c.Z;
        }

        // O segmento só encosta no triângulo (pedaço exato vazio): a cota é
        // a do ponto de encosto, que os vizinhos também respondem.
        if (e1 < e0) e0 = e1 = (t0 + t1) / 2;

        zT0 = Cota(e0);
        zT1 = Cota(e1);
        return true;
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

    /// <summary>
    /// Os triângulos que um segmento pode atravessar.
    ///
    /// É o que o drapeamento precisa: para acrescentar um vértice em cada
    /// aresta cruzada, ele tem que saber quais triângulos ficam no caminho —
    /// e percorrer a malha inteira por segmento seria voltar ao problema que
    /// o índice resolveu.
    /// </summary>
    internal IEnumerable<Triangle> TrianglesAlong(double x0, double y0, double x1, double y1)
    {
        if (_grid is null) yield break;

        foreach (var i in _grid.CandidatesAlong(x0, y0, x1, y1)) yield return _triangles[i];
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
