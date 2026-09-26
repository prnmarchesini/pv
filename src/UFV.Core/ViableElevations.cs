using System.Globalization;

namespace UFV.Core;

/// <summary>Um intervalo fechado de cotas, em metro.</summary>
/// <param name="Min">A menor cota.</param>
/// <param name="Max">A maior cota.</param>
public sealed record ElevationRange(double Min, double Max)
{
    /// <summary>Se a cota está no intervalo, com folga numérica de um nanômetro (não é a de regra, que é um milímetro).</summary>
    public bool Contains(double z) => z >= Min - 1e-9 && z <= Max + 1e-9;

    /// <summary>O tamanho do intervalo.</summary>
    public double Width => Max - Min;
}

/// <summary>
/// Para uma cota da ponta baixa no início da mesa, as cotas viáveis no fim.
/// </summary>
/// <param name="StartElevation">A cota da ponta baixa na estação zero, em metro, na grade.</param>
/// <param name="EndRanges">
/// Os intervalos de cota da ponta baixa na estação final em que a mesa é
/// viável, em ordem crescente e sem se tocar. Contínuos: quem precisar de
/// grade (a otimização da fileira) quantiza.
/// </param>
public sealed record ViableStart(double StartElevation, IReadOnlyList<ElevationRange> EndRanges)
{
    /// <summary>
    /// A chave inteira da cota inicial na grade: a cota dividida pelo passo,
    /// arredondada. É por ela que mesas vizinhas casam as juntas — nunca por
    /// igualdade de double.
    /// </summary>
    public long Key(double step) => (long)Math.Round(StartElevation / step);
}

/// <summary>
/// O conjunto de pares (cota da ponta baixa no início, cota no fim) em que
/// uma mesa é viável sobre o terreno amostrado.
///
/// É o passo 5.3 do plano: "conjunto de pares que respeitam a faixa, com a
/// tolerância de lombo e o limite de inclinação longitudinal". A mesa é
/// rígida (regra sagrada 2), então a cota da ponta baixa varia linearmente
/// da estação zero à estação final, e a altura livre de cada módulo da
/// fileira de baixo é a cota da ponta baixa na estação dele menos o terreno
/// mais alto sob ela (5.2). Viável é: no máximo <see cref="SystemConfiguration.BumpToleranceFor"/>
/// módulos fora da faixa da ponta baixa, e a declividade longitudinal dentro
/// do limite, se houver.
///
/// A cota inicial anda numa grade (<see cref="Step"/>, 1 cm por padrão),
/// alinhada ao múltiplo do passo e não ao terreno: mesas vizinhas de uma
/// fileira precisam falar da mesma grade para a otimização casar as juntas
/// (pela chave inteira <see cref="ViableStart.Key"/>, nunca por igualdade
/// de double). Para cada cota inicial, as cotas finais viáveis saem como
/// intervalos contínuos, e não como pares um a um: com n módulos, a
/// condição "no máximo k fora" é "coberto por pelo menos n − k intervalos
/// de módulo", e isso se resolve numa varredura em vez de testar cada cota
/// final.
///
/// A grade cobre todas as cotas iniciais viáveis quando o conjunto é
/// limitado: com limite de declividade, o giro máximo é tan(limite) × L;
/// sem limite, o giro é limitado pelos módulos que precisam ficar dentro —
/// com m = n − k deles, o mais íngreme que a mesa consegue ficar é o
/// desnível máximo da faixa sobre o menor vão que m estações consecutivas
/// cobrem. Com m ≤ 1 nada limita o giro, o conjunto é ilimitado, e
/// <see cref="IsUnbounded"/> diz isso: a grade então cobre só uma janela em
/// volta do terreno, declarada, e não "todas".
///
/// O pilar não entra aqui. A regra sagrada 4 manda: a ponta baixa manda, o
/// pilar é consequência — ele é calculado depois (5.5) e estoura se tiver
/// que estourar.
/// </summary>
public sealed class ViableElevations
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Folga numérica das comparações, em metro.</summary>
    private const double Tolerancia = 1e-9;

    /// <summary>O passo padrão da grade de cotas: um centímetro, como o plano de requisitos sugere.</summary>
    public const double DefaultStep = 0.01;

    private readonly List<(double Station, double Ground)> _modulos;
    private readonly int _toleradas;
    private readonly double? _maxDeclive;

    private ViableElevations(
        double length, double step, SystemConfiguration configuration, int moduleCount,
        List<(double, double)> modulos, IReadOnlyList<ViableStart> starts, string? problem, bool unbounded)
    {
        Length = length;
        Step = step;
        Configuration = configuration;
        ModuleCount = moduleCount;
        _modulos = modulos;
        _toleradas = configuration.BumpToleranceFor(moduleCount);
        _maxDeclive = configuration.MaxLongitudinalSlope is { } inclinacao ? Math.Tan(inclinacao) : null;
        Starts = starts;
        Problem = problem;
        IsUnbounded = unbounded;
    }

    /// <summary>O comprimento da mesa, da estação zero à final.</summary>
    public double Length { get; }

    /// <summary>O passo da grade de cotas iniciais.</summary>
    public double Step { get; }

    /// <summary>A configuração de onde vieram a faixa, a tolerância e o limite.</summary>
    public SystemConfiguration Configuration { get; }

    /// <summary>As cotas iniciais que têm alguma cota final viável, em ordem crescente.</summary>
    public IReadOnlyList<ViableStart> Starts { get; }

    /// <summary>
    /// Por que não há cota viável, quando o motivo é do terreno e não da
    /// geometria: amostra incompleta (módulo sem terreno embaixo). Null se
    /// a conta pôde ser feita — mesmo que o resultado seja vazio.
    /// </summary>
    public string? Problem { get; }

    /// <summary>Se não há nenhum par viável.</summary>
    public bool IsEmpty => Starts.Count == 0;

    /// <summary>
    /// Se o conjunto de pares é ilimitado: sem limite de declividade e com
    /// tolerância que deixa no máximo um módulo dentro, qualquer cota
    /// inicial tem cota final viável, e <see cref="Starts"/> cobre só uma
    /// janela em volta do terreno (do terreno mais baixo menos o desnível e
    /// a faixa até o mais alto mais o mesmo). Quem otimiza precisa saber que
    /// fora da janela também há solução.
    /// </summary>
    public bool IsUnbounded { get; }

    /// <summary>Quantos módulos a mesa tem na fileira de baixo (com ou sem terreno).</summary>
    public int ModuleCount { get; }

    /// <summary>Quantos deles podem estourar a faixa antes de a mesa ser marcada.</summary>
    public int ToleratedModules => _toleradas;

    /// <summary>
    /// Calcula o conjunto.
    /// </summary>
    /// <param name="samples">O terreno amostrado sob a mesa.</param>
    /// <param name="length">O comprimento da mesa, em metro.</param>
    /// <param name="configuration">Faixa da ponta baixa, tolerância de lombo e limite de declividade.</param>
    /// <param name="step">O passo da grade de cotas iniciais.</param>
    /// <exception cref="ArgumentOutOfRangeException">Comprimento ou passo que não são medidas.</exception>
    /// <exception cref="InvalidOperationException">Configuração que não fecha.</exception>
    public static ViableElevations Compute(
        TableSamples samples, double length, SystemConfiguration configuration, double step = DefaultStep)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!double.IsFinite(length) || length < RowDistributor.MenorMedida || length > 50)
            throw new ArgumentOutOfRangeException(nameof(length), length, "O comprimento da mesa não é uma medida válida.");

        if (!double.IsFinite(step) || step < 1e-4 || step > 1)
            throw new ArgumentOutOfRangeException(nameof(step), step, "O passo da grade precisa ficar entre 0,1 mm e 1 m.");

        if (configuration.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");

        var modulos = new List<(double Station, double Ground)>();
        var semTerreno = 0;
        var total = samples.LowEdge.Count;

        foreach (var amostra in samples.LowEdge)
        {
            // NaN não é cota: comparar com NaN é sempre falso, e um módulo
            // com NaN nunca contaria como violação.
            if (amostra.GroundZ is { } cota && double.IsFinite(cota) && double.IsFinite(amostra.Station))
                modulos.Add((amostra.Station, cota));
            else
                semTerreno++;
        }

        if (total == 0)
        {
            return new ViableElevations(length, step, configuration, total, modulos, [],
                "a mesa não tem módulo na fileira de baixo para conferir a ponta baixa", false);
        }

        if (semTerreno > 0)
        {
            return new ViableElevations(length, step, configuration, total, modulos, [],
                $"{semTerreno} módulo(s) da fileira de baixo sem terreno embaixo", false);
        }

        foreach (var (estacao, _) in modulos)
        {
            if (estacao < -Tolerancia || estacao > length + Tolerancia)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), estacao,
                    "Há módulo com estação fora do comprimento da mesa.");
            }
        }

        var vazio = new ViableElevations(length, step, configuration, total, modulos, [], null, false);
        var starts = new List<ViableStart>();

        // Com a mesa nivelada, a cota inicial só faz sentido entre "o terreno
        // mais baixo mais a altura mínima" e "o mais alto mais a máxima". O
        // giro alarga isso dos dois lados.
        var terrenoMin = modulos.Min(m => m.Ground);
        var terrenoMax = modulos.Max(m => m.Ground);
        var desnivel = terrenoMax - terrenoMin + configuration.MaxLowEdge - configuration.MinLowEdge;

        // O giro máximo. Com limite, é tan(limite) × L. Sem limite, é o que
        // os módulos que precisam ficar dentro permitem: m = n − k deles,
        // e o mais íngreme possível é o desnível máximo da faixa sobre o
        // menor vão que m estações consecutivas cobrem. Com m ≤ 1 não há
        // limite nenhum: conjunto ilimitado, grade numa janela declarada.
        var exigidos = modulos.Count - vazio._toleradas;
        var ilimitado = false;
        double giro;

        if (vazio._maxDeclive is { } tan)
        {
            giro = tan * length;
        }
        else if (exigidos >= 2)
        {
            var estacoes = modulos.Select(m => m.Station).OrderBy(s => s).ToList();
            var menorVao = double.PositiveInfinity;

            for (var i = 0; i + exigidos - 1 < estacoes.Count; i++)
                menorVao = Math.Min(menorVao, estacoes[i + exigidos - 1] - estacoes[i]);

            giro = menorVao > Tolerancia ? desnivel / menorVao * length : double.PositiveInfinity;

            if (!double.IsFinite(giro))
            {
                ilimitado = true;
                giro = desnivel + configuration.MaxLowEdge;
            }
        }
        else
        {
            ilimitado = true;
            giro = desnivel + configuration.MaxLowEdge;
        }

        var primeira = Math.Floor((terrenoMin + configuration.MinLowEdge - giro) / step) * step;
        var ultima = Math.Ceiling((terrenoMax + configuration.MaxLowEdge + giro) / step) * step;

        var quantas = (long)Math.Round((ultima - primeira) / step);

        for (long i = 0; i <= quantas; i++)
        {
            var z0 = Math.Round((primeira + i * step) / step) * step;
            var intervalos = vazio.EndRangesFor(z0, vazio._toleradas);

            if (intervalos.Count > 0) starts.Add(new ViableStart(z0, intervalos));
        }

        return new ViableElevations(length, step, configuration, total, modulos, starts, null, ilimitado);
    }

    /// <summary>
    /// Quantos módulos ficam fora da faixa da ponta baixa com estas cotas.
    /// É a conta direta, módulo a módulo, e é contra ela que os intervalos
    /// de <see cref="Starts"/> são conferidos nos testes.
    /// </summary>
    public int Violations(double startElevation, double endElevation)
    {
        var fora = 0;

        foreach (var (estacao, terreno) in _modulos)
        {
            var cota = startElevation + (endElevation - startElevation) * estacao / Length;
            var livre = cota - terreno;

            if (livre < Configuration.MinLowEdge - Tolerancia || livre > Configuration.MaxLowEdge + Tolerancia)
                fora++;
        }

        return fora;
    }

    /// <summary>A declividade longitudinal destas cotas, em radianos, sempre positiva.</summary>
    public double LongitudinalSlope(double startElevation, double endElevation) =>
        Math.Atan(Math.Abs(endElevation - startElevation) / Length);

    /// <summary>
    /// Se a mesa é viável com estas cotas: no máximo a tolerância de módulos
    /// fora da faixa, e a declividade dentro do limite. É a definição; os
    /// intervalos são a mesma coisa calculada de uma vez.
    /// </summary>
    public bool IsViable(double startElevation, double endElevation)
    {
        if (!double.IsFinite(startElevation) || !double.IsFinite(endElevation)) return false;
        if (Problem is not null) return false;

        if (_maxDeclive is { } tan && Math.Abs(endElevation - startElevation) > tan * Length + Tolerancia)
            return false;

        return Violations(startElevation, endElevation) <= _toleradas;
    }

    /// <summary>
    /// Os intervalos de cota final viáveis para uma cota inicial qualquer,
    /// com no máximo <paramref name="maxViolations"/> módulos fora da faixa.
    ///
    /// É o que <see cref="Starts"/> guarda para a tolerância configurada,
    /// calculado sob demanda para qualquer tolerância menor: a otimização
    /// da fileira (5.4) precisa saber não só se a mesa cabe, mas com quantos
    /// módulos estourando, para minimizar o estouro e não só evitá-lo.
    /// </summary>
    public IReadOnlyList<ElevationRange> EndRanges(double startElevation, int maxViolations)
    {
        if (!double.IsFinite(startElevation)) return [];
        if (Problem is not null) return [];

        return EndRangesFor(startElevation, Math.Clamp(maxViolations, 0, _modulos.Count));
    }

    /// <summary>
    /// O terreno mais alto sob a ponta baixa entre os módulos que têm
    /// terreno, ou null se nenhum tem. Serve para a fileira dar cota a uma
    /// mesa sem cota viável: ela fica nivelada, marcada, mas em cima do
    /// terreno dela, e não em zero.
    /// </summary>
    public double? HighestGroundOrNull() => _modulos.Count == 0 ? null : _modulos.Max(m => m.Ground);

    /// <summary>A linha que descreve o conjunto para o usuário.</summary>
    public string Describe()
    {
        if (Problem is { } problema) return $"Sem cota viável: {problema}.";
        if (IsEmpty) return "Sem cota viável: nenhuma cota da ponta baixa respeita a faixa nesta mesa.";

        return $"cota inicial de {Starts[0].StartElevation.ToString("0.00", Brasil)} a "
            + $"{Starts[^1].StartElevation.ToString("0.00", Brasil)} m, {Starts.Count} posições";
    }

    /// <summary>
    /// Os intervalos de cota final viáveis para uma cota inicial.
    ///
    /// Cada módulo, com a cota inicial fixa, aceita a cota final num
    /// intervalo (a cota dele é afim na cota final). "No máximo k fora" é
    /// "coberto por pelo menos n − k intervalos": uma varredura pelas pontas
    /// dos intervalos, contando quantos estão abertos, dá as regiões. O
    /// limite de declividade é mais um intervalo, esse obrigatório.
    /// </summary>
    private List<ElevationRange> EndRangesFor(double z0, int minimo)
    {
        var eventos = new List<(double Z, int Delta)>();
        var semAlcance = 0;
        var fixos = 0;

        foreach (var (estacao, terreno) in _modulos)
        {
            var fracao = estacao / Length;

            if (fracao <= Tolerancia)
            {
                // Módulo na estação zero: a cota final não muda a dele. Ou
                // está dentro com z0, ou é uma violação fixa.
                fixos++;

                var livre = z0 - terreno;

                if (livre < Configuration.MinLowEdge - Tolerancia || livre > Configuration.MaxLowEdge + Tolerancia)
                    semAlcance++;

                continue;
            }

            // z0 + (z1 − z0)·f ∈ [terreno + min, terreno + max]
            var baixo = z0 + (terreno + Configuration.MinLowEdge - z0) / fracao;
            var alto = z0 + (terreno + Configuration.MaxLowEdge - z0) / fracao;

            eventos.Add((baixo, +1));
            eventos.Add((alto, -1));
        }

        if (semAlcance > minimo) return [];

        // Os módulos com alcance são n − fixos. Precisam estar dentro pelo
        // menos (n − fixos) − (tolerância − violações fixas).
        var exigidos = _modulos.Count - fixos - (minimo - semAlcance);

        var declive = _maxDeclive is { } tan
            ? new ElevationRange(z0 - tan * Length, z0 + tan * Length)
            : new ElevationRange(double.NegativeInfinity, double.PositiveInfinity);

        if (exigidos <= 0)
        {
            return [new ElevationRange(declive.Min, declive.Max)];
        }

        // Varredura: em cada ponta, o número de intervalos abertos muda. A
        // ordem em empate é abrir antes de fechar, para intervalos que só se
        // tocam contarem juntos no ponto de toque (a faixa é fechada).
        eventos.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : b.Delta.CompareTo(a.Delta));

        var resultado = new List<ElevationRange>();
        var abertos = 0;
        double? inicio = null;

        foreach (var (z, delta) in eventos)
        {
            var antes = abertos;
            abertos += delta;

            if (antes < exigidos && abertos >= exigidos) inicio = z;

            if (antes >= exigidos && abertos < exigidos && inicio is { } de)
            {
                var min = Math.Max(de, declive.Min);
                var max = Math.Min(z, declive.Max);

                if (max >= min) resultado.Add(new ElevationRange(min, max));

                inicio = null;
            }
        }

        // Os intervalos não se tocam: com abrir-antes-de-fechar, a contagem
        // nunca cai abaixo do exigido e volta a subir no mesmo Z, então dois
        // intervalos consecutivos têm sempre um vão entre si.
        return resultado;
    }
}
