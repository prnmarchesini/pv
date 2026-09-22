namespace UFV.Geo;

/// <summary>
/// Índice espacial dos triângulos: uma grade uniforme em planta.
///
/// Sem índice, achar o triângulo de um ponto é varrer a malha inteira. Com 2
/// milhões de triângulos e dezenas de milhares de consultas — que é o tamanho
/// real de uma usina de 12 ha levantada a 25 cm — isso é mais de uma hora de
/// espera para responder o que devia levar um décimo de segundo.
///
/// A grade é barata de construir, o que importa: a construção acontece a cada
/// processamento de superfície, com o usuário esperando na frente.
///
/// A ocupação é guardada em formato comprimido (CSR): um vetor de índices e um
/// vetor de onde começa cada célula. Uma lista por célula custaria um objeto
/// por célula — milhões de objetos para o coletor de lixo cuidar.
///
/// Duas defesas existem por causa de superfície feita a partir de CURVAS DE
/// NÍVEL, que é caso comum e não se parece nada com uma malha regular:
///
/// 1. Triângulo comprido e diagonal tem caixa envolvente enorme, e entraria em
///    milhares de células. <see cref="LimiteDeCelulasPorTriangulo"/> manda os
///    exagerados para uma lista à parte, varrida em toda consulta. São poucos,
///    e melhor varrer poucos sempre do que duplicá-los aos milhares.
/// 2. O tamanho da célula é reavaliado: se a ocupação total ou o número de
///    células passar do teto, a célula cresce e a conta é refeita. Sem isso,
///    uma malha desigual pedia dezenas de gigabytes e o índice quebrava antes
///    de responder a primeira cota.
///
/// Sobre os tetos, para não parecerem exagero de quem escreveu:
///
/// - O número de células sai perto de n/2 por construção, porque a célula é
///   dimensionada pela área dividida pelo número de triângulos. Então o teto
///   de células só entra em ação acima de uns 8 milhões de triângulos, ou
///   numa malha em fita, onde a área quase nula faz a célula encolher e o
///   número de colunas explodir.
/// - O teto por dimensão (em <see cref="Dividir"/>) e o do produto se cobrem:
///   removida só uma, a outra segura. O teste da malha em fita só fica
///   vermelho quando as duas somem — foi conferido assim.
/// - O teto de ocupação é a rede embaixo do limite por triângulo: com o
///   limite valendo, a ocupação não passa de 32 vezes o número de triângulos,
///   e só uma malha de milhões chegaria perto dele.
/// </summary>
internal sealed class TriangleGrid
{
    /// <summary>Alvo de triângulos por célula. Menos é desperdício de busca; mais é desperdício de memória.</summary>
    private const double AlvoPorCelula = 2.0;

    /// <summary>Teto de células. Cada célula custa 4 bytes em <see cref="_inicios"/>.</summary>
    private const long MaximoDeCelulas = 4_000_000;

    /// <summary>
    /// Teto de entradas em <see cref="_indices"/>, que é onde a duplicação
    /// aparece: 32 milhões de int, 128 MB.
    /// </summary>
    private const long MaximoDeOcupacao = 32_000_000;

    /// <summary>
    /// Em quantas células um triângulo pode entrar antes de ser tratado como
    /// grande demais. Numa malha regular o normal é de uma a quatro.
    /// </summary>
    private const long LimiteDeCelulasPorTriangulo = 32;

    /// <summary>Quantas vezes a célula pode crescer antes de desistirmos de afinar.</summary>
    private const int TentativasDeAjuste = 24;

    private readonly double _minX;
    private readonly double _minY;
    private readonly double _maxX;
    private readonly double _maxY;
    private readonly double _tamanhoDaCelula;
    private readonly int _colunas;
    private readonly int _linhas;

    /// <summary>Onde começa cada célula dentro de <see cref="_indices"/>. Tem uma posição a mais, com o fim da última.</summary>
    private readonly int[] _inicios;

    /// <summary>Índices de triângulo, agrupados por célula.</summary>
    private readonly int[] _indices;

    /// <summary>
    /// Triângulos que cobrem células demais para serem espalhados. Toda
    /// consulta olha estes também.
    /// </summary>
    private readonly int[] _grandes;

    internal TriangleGrid(Triangle[] triangles)
    {
        var (minX, minY, maxX, maxY) = Envolver(triangles);

        _minX = minX;
        _minY = minY;
        _maxX = maxX;
        _maxY = maxY;

        var largura = Math.Max(maxX - minX, double.Epsilon);
        var altura = Math.Max(maxY - minY, double.Epsilon);

        // Descobre um tamanho de célula que caiba nos tetos. A conta de
        // ocupação é O(n) e roda poucas vezes: na malha regular, a primeira
        // tentativa já passa.
        var tamanho = TamanhoInicial(largura, altura, triangles.Length);
        int colunas = 1, linhas = 1;

        for (var tentativa = 0; ; tentativa++)
        {
            colunas = (int)Dividir(largura, tamanho);
            linhas = (int)Dividir(altura, tamanho);

            if ((long)colunas * linhas > MaximoDeCelulas && tentativa < TentativasDeAjuste)
            {
                tamanho *= 2;
                continue;
            }

            _tamanhoDaCelula = tamanho;
            _colunas = colunas;
            _linhas = linhas;

            var (ocupacao, _) = Medir(triangles);

            if (ocupacao > MaximoDeOcupacao && tentativa < TentativasDeAjuste)
            {
                tamanho *= 2;
                continue;
            }

            break;
        }

        // Duas passadas: a primeira conta quantos triângulos caem em cada
        // célula, a segunda preenche. Assim os vetores nascem do tamanho
        // exato, sem realocar nada no meio.
        _inicios = new int[(long)_colunas * _linhas + 1];

        var grandes = new List<int>();

        for (var t = 0; t < triangles.Length; t++)
        {
            var (c0, l0, c1, l1) = FaixaDeCelulas(triangles[t]);

            if (EhGrandeDemais(c0, l0, c1, l1))
            {
                grandes.Add(t);
                continue;
            }

            for (var linha = l0; linha <= l1; linha++)
            for (var coluna = c0; coluna <= c1; coluna++)
                _inicios[linha * _colunas + coluna + 1]++;
        }

        for (var i = 1; i < _inicios.Length; i++)
            _inicios[i] += _inicios[i - 1];

        _indices = new int[_inicios[^1]];
        _grandes = grandes.ToArray();

        var proximo = (int[])_inicios.Clone();

        foreach (var t in Enumerable.Range(0, triangles.Length))
        {
            var (c0, l0, c1, l1) = FaixaDeCelulas(triangles[t]);
            if (EhGrandeDemais(c0, l0, c1, l1)) continue;

            for (var linha = l0; linha <= l1; linha++)
            for (var coluna = c0; coluna <= c1; coluna++)
                _indices[proximo[linha * _colunas + coluna]++] = t;
        }
    }

    /// <summary>Quantas células a grade tem.</summary>
    internal long CellCount => (long)_colunas * _linhas;

    /// <summary>Quantas entradas o vetor de ocupação tem, contando duplicação.</summary>
    internal long OccupancyCount => _indices.Length;

    /// <summary>Quantos triângulos ficaram fora da grade por cobrirem células demais.</summary>
    internal int OversizedCount => _grandes.Length;

    /// <summary>Os triângulos varridos em toda consulta, por serem grandes demais para a grade.</summary>
    internal ReadOnlySpan<int> Oversized => _grandes;

    /// <summary>
    /// Os índices dos triângulos da célula que contém (x, y). Vazio quando o
    /// ponto cai fora da área da malha.
    ///
    /// Não é a resposta inteira: <see cref="Oversized"/> também precisa ser
    /// olhado, sempre.
    /// </summary>
    internal ReadOnlySpan<int> Candidates(double x, double y)
    {
        // Fora da caixa da malha não há o que procurar. NaN cai aqui também:
        // toda comparação com NaN é falsa, então nenhuma das quatro barra o
        // ponto — por isso o teste é de "está dentro", não de "está fora".
        if (!(x >= _minX) || !(x <= _maxX) || !(y >= _minY) || !(y <= _maxY))
            return ReadOnlySpan<int>.Empty;

        // Dentro da caixa, o índice é preso à última célula. A borda superior
        // é o caso: um ponto exatamente em maxX cai na coluna seguinte à
        // última, que não existe, e o canto do terreno sumia do índice — a
        // consulta respondia "fora do terreno" para um ponto que está dentro.
        var coluna = Math.Clamp(Coluna(x), 0, _colunas - 1);
        var linha = Math.Clamp(Linha(y), 0, _linhas - 1);

        var celula = linha * _colunas + coluna;
        var inicio = _inicios[celula];

        return _indices.AsSpan(inicio, _inicios[celula + 1] - inicio);
    }

    /// <summary>Ocupação total e quantos triângulos são grandes demais, com o tamanho de célula atual.</summary>
    private (long Ocupacao, int Grandes) Medir(Triangle[] triangles)
    {
        long ocupacao = 0;
        var grandes = 0;

        foreach (var t in triangles)
        {
            var (c0, l0, c1, l1) = FaixaDeCelulas(t);

            if (EhGrandeDemais(c0, l0, c1, l1))
            {
                grandes++;
                continue;
            }

            ocupacao += (long)(c1 - c0 + 1) * (l1 - l0 + 1);
        }

        return (ocupacao, grandes);
    }

    private static bool EhGrandeDemais(int c0, int l0, int c1, int l1) =>
        (long)(c1 - c0 + 1) * (l1 - l0 + 1) > LimiteDeCelulasPorTriangulo;

    private static (double MinX, double MinY, double MaxX, double MaxY) Envolver(Triangle[] triangles)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        // Sem alocar um array por triângulo: com 2 milhões deles isso era
        // quase 200 MB de lixo só para percorrer três pontos.
        foreach (var t in triangles)
        {
            minX = Math.Min(minX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)));
            maxX = Math.Max(maxX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)));
            minY = Math.Min(minY, Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)));
            maxY = Math.Max(maxY, Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)));
        }

        return (minX, minY, maxX, maxY);
    }

    private static double TamanhoInicial(double largura, double altura, int quantidade)
    {
        // Célula do tamanho médio de um triângulo, vezes o alvo de ocupação.
        var tamanho = Math.Sqrt(largura * altura * AlvoPorCelula / quantidade);

        return tamanho > 0 && double.IsFinite(tamanho)
            ? tamanho
            : Math.Max(largura, altura);
    }

    /// <summary>
    /// Quantas células cabem numa extensão, sem estourar int: converter um
    /// double maior que int.MaxValue é conversão sem verificação, e o
    /// resultado indefinido viraria uma grade de uma coluna só — um índice que
    /// não indexa nada, em silêncio.
    /// </summary>
    private static long Dividir(double extensao, double tamanho)
    {
        var quantas = Math.Ceiling(extensao / tamanho);

        if (!double.IsFinite(quantas) || quantas < 1) return 1;

        return (long)Math.Min(quantas, MaximoDeCelulas);
    }

    private (int C0, int L0, int C1, int L1) FaixaDeCelulas(in Triangle t)
    {
        // A caixa do triângulo, não o triângulo: um triângulo pode cruzar
        // várias células, e todas precisam apontar para ele.
        var minX = Math.Min(t.A.X, Math.Min(t.B.X, t.C.X));
        var maxX = Math.Max(t.A.X, Math.Max(t.B.X, t.C.X));
        var minY = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y));
        var maxY = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));

        return (
            Math.Clamp(Coluna(minX), 0, _colunas - 1),
            Math.Clamp(Linha(minY), 0, _linhas - 1),
            Math.Clamp(Coluna(maxX), 0, _colunas - 1),
            Math.Clamp(Linha(maxY), 0, _linhas - 1));
    }

    private int Coluna(double x) => (int)Math.Clamp(
        Math.Floor((x - _minX) / _tamanhoDaCelula), int.MinValue, int.MaxValue);

    private int Linha(double y) => (int)Math.Clamp(
        Math.Floor((y - _minY) / _tamanhoDaCelula), int.MinValue, int.MaxValue);
}
