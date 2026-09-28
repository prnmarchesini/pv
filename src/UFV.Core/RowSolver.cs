using System.Globalization;

namespace UFV.Core;

/// <summary>Uma mesa a resolver numa fileira.</summary>
/// <param name="Label">O letreiro da mesa (F1.3), só para o relatório.</param>
/// <param name="GapBefore">
/// O vão em planta entre o fim da mesa anterior e o início desta, em metro.
/// Zero na primeira. Vão maior que <see cref="SystemConfiguration.MaxGapBeforeBreak"/>
/// quebra a fileira ali.
/// </param>
/// <param name="Viable">As cotas viáveis desta mesa (5.3).</param>
public sealed record RowTable(string Label, double GapBefore, ViableElevations Viable);

/// <summary>Uma mesa resolvida: as cotas da ponta baixa e o que estourou.</summary>
/// <param name="Label">O letreiro.</param>
/// <param name="StartElevation">A cota da ponta baixa na estação zero.</param>
/// <param name="EndElevation">A cota da ponta baixa na estação final.</param>
/// <param name="Violations">Quantos módulos da fileira de baixo ficaram fora da faixa.</param>
/// <param name="Marked">
/// Se a mesa não cabe: mais módulos fora que a tolerância, declividade
/// acima do limite, ou sem terreno. A mesa fica assim mesmo, com cota, e é
/// marcada — o motor não move nem apaga. Uma marcada pode ficar inclinada
/// quando isso serve de escada para as vizinhas caberem; a que FLUTUARIA
/// acima do próprio terreno é assentada nele, nivelada (26/09/2026).
/// </param>
/// <param name="Reason">Por que foi marcada, ou null.</param>
/// <param name="Seated">
/// Se é marcada que flutuaria e foi assentada nivelada no próprio terreno
/// (26/09/2026): o degrau até ela não é degrau de fileira.
/// </param>
public sealed record SolvedTable(
    string Label,
    double StartElevation,
    double EndElevation,
    int Violations,
    bool Marked,
    string? Reason,
    bool Seated = false);

/// <summary>Um trecho contínuo de fileira, resolvido de uma vez.</summary>
/// <param name="Tables">As mesas, na ordem da fileira.</param>
public sealed record SolvedRun(IReadOnlyList<SolvedTable> Tables)
{
    /// <summary>
    /// Os degraus entre mesas vizinhas: cota inicial da seguinte menos cota
    /// final da anterior. Só a junta com mesa ASSENTADA não entra: ela foi
    /// posta pela análise de pesos (27/09/2026), fora da cota que a
    /// programação escolheu, e o degrau até uma mesa que não cabe não é
    /// degrau de fileira. Desde então toda marcada é assentada.
    /// </summary>
    public IReadOnlyList<double> Steps =>
        Tables.Zip(Tables.Skip(1), (a, b) => (a, b))
            .Where(par => !par.a.Seated && !par.b.Seated)
            .Select(par => par.b.StartElevation - par.a.EndElevation)
            .ToList();

    /// <summary>Os degraus de TODA junta, inclusive com assentada (a régua de custo do solver, na solução crua).</summary>
    public IReadOnlyList<double> AllSteps =>
        Tables.Zip(Tables.Skip(1), (a, b) => b.StartElevation - a.EndElevation).ToList();

    /// <summary>Quantas mesas foram marcadas.</summary>
    public int MarkedCount => Tables.Count(t => t.Marked);

    /// <summary>Quantos módulos estouraram, somando as mesas não marcadas.</summary>
    public int ViolationCount => Tables.Where(t => !t.Marked).Sum(t => t.Violations);
}

/// <summary>O resultado do alinhamento de uma fileira.</summary>
/// <param name="Runs">Os trechos contínuos, na ordem; um vão grande separa dois.</param>
public sealed record RowSolution(IReadOnlyList<SolvedRun> Runs)
{
    /// <summary>Todas as mesas, trecho a trecho.</summary>
    public IReadOnlyList<SolvedTable> Tables => Runs.SelectMany(r => r.Tables).ToList();

    /// <summary>Quantas mesas foram marcadas.</summary>
    public int MarkedCount => Runs.Sum(r => r.MarkedCount);

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A linha que descreve o resultado para o usuário.</summary>
    public string Describe()
    {
        var mesas = Tables.Count;
        var marcadas = MarkedCount;
        var estouros = Runs.Sum(r => r.ViolationCount);

        return $"{mesas} mesa(s) em {Runs.Count} trecho(s): {marcadas} marcada(s), "
            + $"{estouros} módulo(s) fora da faixa nas demais"
            + (Runs.Count > 0 && Runs.Any(r => r.Steps.Count > 0)
                ? $", maior degrau {Runs.SelectMany(r => r.Steps).Select(Math.Abs).DefaultIfEmpty(0).Max().ToString("0.###", Brasil)} m"
                : string.Empty);
    }
}

/// <summary>
/// O alinhamento na fileira: escolhe a cota de cada mesa de modo que os
/// degraus entre mesas vizinhas respeitem a configuração e o estouro seja
/// o menor possível — a fileira inteira de uma vez.
///
/// É programação dinâmica sobre a grade de cotas das juntas, e não iteração
/// até convergir como o plano de execução pedia. O plano de requisitos já
/// registrava a alternativa: "programação dinâmica resolve isso de forma
/// exata e rápida, sem depender de convergência". Com as cotas viáveis de
/// cada mesa como intervalos (5.3), o custo de uma mesa é quantos módulos
/// dela estouram, o custo de uma junta é o degrau, e o ótimo sai numa
/// passada. Não há "máximo de iterações" porque não há iteração; a
/// divergência em relação ao plano está registrada em PROGRESSO.md.
///
/// O espaço de estados é a grade de cotas (1 cm) num alcance limitado: as
/// cotas viáveis de cada mesa, mais o que os degraus e um giro de mesa
/// marcada alcançam. A solução é ótima DENTRO desse espaço; uma mesa
/// marcada só experimenta cinco giros (nivelada, meio giro e giro inteiro
/// para cada lado), o que basta para ela servir de escada entre vizinhas
/// mas não é "qualquer giro".
///
/// O que o resultado garante, sempre: todo degrau está em
/// {0} ∪ [degrau mínimo, degrau máximo] (um degrau que a estrutura não
/// consegue fazer não aparece); toda mesa tem cota; a mesa que não cabe
/// fica marcada. O motor não move mesa, não apaga mesa, não quebra fileira
/// por conta própria — só quebra onde o vão passa do limite, que é a
/// definição de fileira do plano.
///
/// A ordem do que se minimiza: primeiro mesas marcadas, depois módulos
/// fora da faixa, depois a soma dos degraus, e por fim a inclinação
/// longitudinal de cada mesa. Cada critério vale mais que todos os
/// seguintes juntos: o custo de uma marca é escalado pelo total de módulos
/// do trecho, e o de um módulo fora vale mais que a soma de degraus de
/// duzentas mil mesas.
/// </summary>
public static class RowSolver
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O custo de um módulo fora da faixa. Vale mais que qualquer soma de
    /// degraus: com degrau máximo de 0,5 m, são 200 mil juntas até empatar.
    /// </summary>
    private const double CustoDoEstouro = 1e5;

    /// <summary>O custo de um metro de degrau numa junta.</summary>
    private const double CustoDoDegrau = 1.0;

    /// <summary>O custo de um metro de desnível dentro da mesa, só para desempate.</summary>
    private const double CustoDoGiro = 1e-3;

    /// <summary>
    /// Resolve a fileira.
    /// </summary>
    /// <param name="tables">As mesas, na ordem da fileira.</param>
    /// <param name="configuration">Degrau mínimo e máximo, vão que quebra a fileira.</param>
    /// <exception cref="ArgumentException">Lista vazia, ou mesas com passos de grade diferentes.</exception>
    /// <exception cref="InvalidOperationException">Configuração que não fecha.</exception>
    public static RowSolution Solve(IReadOnlyList<RowTable> tables, SystemConfiguration configuration) =>
        Solve(tables, configuration, seatMarked: true);

    /// <summary>
    /// O mesmo, com a opção de NÃO assentar a marcada que flutua no próprio
    /// terreno (<paramref name="seatMarked"/> falso): a solução crua da
    /// programação dinâmica, para comparar com a força bruta no teste.
    /// </summary>
    public static RowSolution Solve(IReadOnlyList<RowTable> tables, SystemConfiguration configuration, bool seatMarked)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(configuration);

        if (tables.Count == 0) throw new ArgumentException("A fileira não tem mesa.", nameof(tables));

        if (configuration.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");

        foreach (var mesa in tables)
        {
            ArgumentNullException.ThrowIfNull(mesa?.Viable, nameof(tables));

            if (!double.IsFinite(mesa.GapBefore) || mesa.GapBefore < 0)
                throw new ArgumentException($"O vão antes da mesa {mesa.Label} não é uma medida.", nameof(tables));
        }

        var passo = tables[0].Viable.Step;

        if (tables.Any(t => Math.Abs(t.Viable.Step - passo) > 1e-12))
            throw new ArgumentException("As mesas da fileira usam passos de grade diferentes.", nameof(tables));

        var trechos = new List<SolvedRun>();
        var atual = new List<RowTable>();

        foreach (var mesa in tables)
        {
            if (atual.Count > 0 && mesa.GapBefore > configuration.MaxGapBeforeBreak + 1e-9)
            {
                trechos.Add(ResolverTrecho(atual, configuration, passo, seatMarked));
                atual = [];
            }

            atual.Add(mesa);
        }

        trechos.Add(ResolverTrecho(atual, configuration, passo, seatMarked));

        return new RowSolution(trechos);
    }

    /// <summary>O vão em planta entre o fim de uma mesa e o início da seguinte, na direção da fileira.</summary>
    public static double GapBetween(PlacedTable before, PlacedTable after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var dx = Math.Cos(before.DirectionRadians);
        var dy = Math.Sin(before.DirectionRadians);

        var aoLongo = (after.Origin.X - before.Origin.X) * dx + (after.Origin.Y - before.Origin.Y) * dy;

        return aoLongo - before.Length;
    }

    private static SolvedRun ResolverTrecho(List<RowTable> mesas, SystemConfiguration config, double passo, bool assentarMarcadas)
    {
        // Degraus em chaves da grade. Para baixo no máximo (0,5 / 0,01 dá
        // 50,000000000001 e o teto seria 51) e para cima no mínimo.
        var degrauMax = (long)Math.Floor(config.MaxStep / passo + 1e-9);
        var degrauMin = (long)Math.Ceiling(config.MinStep / passo - 1e-9);

        // O custo de uma marca: mais que todos os estouros possíveis do
        // trecho juntos, para uma marca nunca valer a pena.
        var custoDaMarca = CustoDoEstouro * (mesas.Sum(m => m.Viable.ModuleCount) + 1);

        // As cotas que cada mesa alcança: início (as cotas viáveis, ou o
        // terreno dela se nenhuma cabe) e fim (o que as viáveis alcançam).
        var inicios = new (long Baixa, long Alta)?[mesas.Count];
        var fins = new (long Baixa, long Alta)?[mesas.Count];
        var giros = new long[mesas.Count];
        var todas = new List<long>();

        for (var i = 0; i < mesas.Count; i++)
        {
            var viavel = mesas[i].Viable;

            // O giro que uma mesa marcada pode usar para servir de escada:
            // até o limite de declividade; sem limite, até um degrau.
            giros[i] = viavel.Configuration.MaxLongitudinalSlope is { } limite
                ? (long)Math.Floor(Math.Sin(limite) * viavel.Length / passo + 1e-9)
                : degrauMax;

            if (viavel.Starts.Count > 0)
            {
                var chaves = viavel.Starts.Select(st => st.Key(passo)).ToList();
                inicios[i] = (chaves.Min(), chaves.Max());

                var fimMin = viavel.Starts.SelectMany(st => st.EndRanges).Min(f => f.Min);
                var fimMax = viavel.Starts.SelectMany(st => st.EndRanges).Max(f => f.Max);
                fins[i] = ((long)Math.Floor(fimMin / passo), (long)Math.Ceiling(fimMax / passo));

                todas.Add(inicios[i]!.Value.Baixa);
                todas.Add(inicios[i]!.Value.Alta);
                todas.Add(fins[i]!.Value.Baixa);
                todas.Add(fins[i]!.Value.Alta);
            }
            else if (viavel.HighestGroundOrNull() is { } terreno)
            {
                var ancora = (long)Math.Round((terreno + config.MinLowEdge) / passo);
                inicios[i] = (ancora, ancora);
                todas.Add(ancora);
            }
        }

        if (todas.Count == 0)
        {
            // Nenhuma mesa tem terreno: não há cota a escolher. Tudo marcado
            // em cota zero, dita como tal.
            return new SolvedRun(mesas
                .Select(m => new SolvedTable(m.Label, 0, 0, m.Viable.ModuleCount, true,
                    m.Viable.Problem ?? "sem cota viável"))
                .ToList());
        }

        // A grade do trecho: tudo que alguma mesa alcança, com folga de um
        // giro e um degrau. Mais que isso não ajuda: uma marcada só serve de
        // escada ENTRE cotas que alguma mesa alcança, nunca além de todas.
        var folga = degrauMax + giros.Max() + 1;
        var chaveMin = todas.Min() - folga;
        var chaveMax = todas.Max() + folga;
        var n = (int)(chaveMax - chaveMin + 1);

        // custoInicio[k]: o menor custo até a mesa atual começar na chave k.
        // A primeira mesa pode começar em qualquer cota da sua janela de
        // início (viáveis ou âncora) mais um degrau para cada lado.
        var custoInicio = new double[n];
        Array.Fill(custoInicio, double.PositiveInfinity);

        {
            var (baixa, alta) = inicios[0] ?? (todas.Min(), todas.Max());

            for (var k = baixa - degrauMax; k <= alta + degrauMax; k++)
                custoInicio[(int)(k - chaveMin)] = 0;
        }

        // Para reconstruir: por mesa, para cada chave de fim, de que início
        // veio; e por junta, para cada chave de início, de que fim veio.
        var deInicio = new List<int[]>();
        var deFim = new List<int[]>();
        var custoFimDaUltima = Array.Empty<double>();
        var carimbo = new int[n];
        var rodada = 0;

        for (var i = 0; i < mesas.Count; i++)
        {
            var mesa = mesas[i];
            var custoFim = new double[n];
            var origemDoFim = new int[n];

            Array.Fill(custoFim, double.PositiveInfinity);
            Array.Fill(origemDoFim, -1);

            // As chaves de início em que a mesa tem opção viável.
            var viaveisBaixa = inicios[i] is { } ini && mesa.Viable.Starts.Count > 0 ? ini.Baixa : long.MaxValue;
            var viaveisAlta = inicios[i] is { } ini2 && mesa.Viable.Starts.Count > 0 ? ini2.Alta : long.MinValue;

            var giro = giros[i];
            long[] girosDaMarcada = giro > 0 ? [0, giro / 2, giro, -giro / 2, -giro] : [0];

            // Só o intervalo em que há custo finito: fora dele não há nada
            // a propagar, e varrer a grade inteira por mesa custava caro.
            var (kBaixa, kAlta) = Finitos(custoInicio);

            for (var k = kBaixa; k <= kAlta; k++)
            {
                var custo = custoInicio[k];
                if (double.IsPositiveInfinity(custo)) continue;

                var chave = chaveMin + k;
                var z0 = chave * passo;

                if (chave >= viaveisBaixa && chave <= viaveisAlta)
                {
                    // Opções viáveis, do menor estouro para o maior: o primeiro
                    // custo gravado numa chave de fim é o menor, e as opções com
                    // mais estouro só preenchem chaves que as anteriores não
                    // alcançaram.
                    rodada++;

                    for (var estouro = 0; estouro <= mesa.Viable.ToleratedModules; estouro++)
                    {
                        foreach (var faixa in mesa.Viable.EndRanges(z0, estouro))
                        {
                            var eMin = (long)Math.Ceiling(faixa.Min / passo - 1e-9) - chaveMin;
                            var eMax = (long)Math.Floor(faixa.Max / passo + 1e-9) - chaveMin;

                            for (var e = Math.Max(eMin, 0); e <= Math.Min(eMax, n - 1); e++)
                            {
                                if (carimbo[e] == rodada) continue;
                                carimbo[e] = rodada;

                                var z1 = (chaveMin + e) * passo;
                                var total = custo + estouro * CustoDoEstouro + Math.Abs(z1 - z0) * CustoDoGiro;

                                if (total < custoFim[e])
                                {
                                    custoFim[e] = total;
                                    origemDoFim[e] = k;
                                }
                            }
                        }
                    }
                }

                // A opção marcada: em qualquer cota alcançável, nivelada ou
                // com um dos cinco giros, custo da marca mais o estouro que
                // ela tem assim. Nunca ganha de uma viável, mas mantém a
                // fileira inteira com cota, e serve de escada quando o
                // terreno dá um salto que os degraus não vencem.
                foreach (var d in girosDaMarcada)
                {
                    var e = k + d;
                    if (e < 0 || e >= n) continue;

                    var z1 = (chaveMin + e) * passo;
                    var estouros = mesa.Viable.Problem is null ? mesa.Viable.Violations(z0, z1) : mesa.Viable.ModuleCount;
                    var marcada = custo + custoDaMarca + estouros * CustoDoEstouro + Math.Abs(z1 - z0) * CustoDoGiro;

                    if (marcada < custoFim[e])
                    {
                        custoFim[e] = marcada;
                        origemDoFim[e] = k;
                    }
                }
            }

            deInicio.Add(origemDoFim);

            if (i == mesas.Count - 1)
            {
                custoFimDaUltima = custoFim;
                break;
            }

            // A junta: a próxima mesa começa a um degrau permitido do fim
            // desta. Degrau zero sempre pode; fora disso, entre o mínimo e o
            // máximo, para cima ou para baixo.
            var proximo = new double[n];
            var origemDoInicio = new int[n];

            Array.Fill(proximo, double.PositiveInfinity);
            Array.Fill(origemDoInicio, -1);

            var (eBaixa, eAlta) = Finitos(custoFim);

            for (var e = eBaixa; e <= eAlta; e++)
            {
                if (double.IsPositiveInfinity(custoFim[e])) continue;

                for (var d = -degrauMax; d <= degrauMax; d++)
                {
                    if (d != 0 && Math.Abs(d) < degrauMin) continue;

                    var k = e + d;
                    if (k < 0 || k >= n) continue;

                    var total = custoFim[e] + Math.Abs(d) * passo * CustoDoDegrau;

                    if (total < proximo[k])
                    {
                        proximo[k] = total;
                        origemDoInicio[k] = e;
                    }
                }
            }

            deFim.Add(origemDoInicio);
            custoInicio = proximo;
        }

        // O melhor fim da última mesa, e a volta.
        var melhor = -1;

        for (var e = 0; e < n; e++)
        {
            if (double.IsPositiveInfinity(custoFimDaUltima[e])) continue;
            if (melhor < 0 || custoFimDaUltima[e] < custoFimDaUltima[melhor]) melhor = e;
        }

        if (melhor < 0)
        {
            // A opção marcada nivelada existe em toda chave alcançável e o
            // degrau zero sempre alcança a próxima mesa: isto não acontece.
            // Fica como rede, com mensagem, e não como silêncio.
            throw new InvalidOperationException(
                "A fileira não tem solução, o que não deveria acontecer: a opção marcada e o degrau zero sempre existem.");
        }

        var resolvidas = new SolvedTable[mesas.Count];
        var fim = melhor;

        for (var i = mesas.Count - 1; i >= 0; i--)
        {
            var inicio = deInicio[i][fim];
            var z0 = (chaveMin + inicio) * passo;
            var z1 = (chaveMin + fim) * passo;
            var mesa = mesas[i];

            var viavel = mesa.Viable.IsViable(z0, z1);
            var estouros = mesa.Viable.Problem is null ? mesa.Viable.Violations(z0, z1) : mesa.Viable.ModuleCount;

            resolvidas[i] = new SolvedTable(
                mesa.Label, z0, z1, estouros, !viavel,
                viavel ? null : mesa.Viable.Problem ?? Motivo(mesa.Viable, z0, z1, estouros));

            if (i > 0) fim = deFim[i - 1][inicio];
        }

        // A marcada é posta pela análise de pesos (27/09/2026), e não na
        // cota que a programação escolheu para ela servir de escada entre as
        // vizinhas. Renan: "enfiou a ponta na terra sendo que poderia ter
        // levantado" — a escada era um de cinco giros na cota da junta, sem
        // olhar quanto enterrava. Agora: acima da faixa é barato, abaixo é
        // caro, enterrado é muito caro, e o degrau até a vizinha só pesa no
        // que passa do máximo (a mesa num buraco fica no próprio chão, e não
        // "na altura das nuvens", 26/09/2026). As vizinhas não mudam: a
        // viabilidade delas não depende da marcada, e o degrau até uma mesa
        // que não cabe não é degrau de fileira. Só as vizinhas não marcadas
        // servem de âncora: a cota de outra marcada é chute da programação.
        if (assentarMarcadas)
        {
            for (var i = 0; i < resolvidas.Length; i++)
            {
                var r = resolvidas[i];
                if (!r.Marked) continue;

                var viavel = mesas[i].Viable;
                double? antes = i > 0 && !resolvidas[i - 1].Marked ? resolvidas[i - 1].EndElevation : null;
                double? depois = i < resolvidas.Length - 1 && !resolvidas[i + 1].Marked ? resolvidas[i + 1].StartElevation : null;

                // Sem terreno sob parte dos módulos, os pesos só veriam os que
                // têm chão e a mesa poderia girar para qualquer lado sobre o
                // resto: ela fica nivelada no ponto mais alto do que existe.
                if (viavel.Problem is not null)
                {
                    if (viavel.HighestGroundOrNull() is not { } alto) continue;

                    var ancora = Math.Round((alto + config.MinLowEdge) / passo) * passo;
                    resolvidas[i] = r with { StartElevation = ancora, EndElevation = ancora, Violations = viavel.ModuleCount, Reason = viavel.Problem, Seated = true };
                    continue;
                }

                if (viavel.Compromise(antes, depois) is not { } posicao) continue;

                var (z0, z1) = posicao;
                var estouros = viavel.Violations(z0, z1);
                var cabe = viavel.IsViable(z0, z1);

                bool Permitido(double? vizinha, double cota)
                {
                    if (vizinha is not { } v) return true;

                    var d = Math.Abs(cota - v);

                    return d < 1e-9 || (d >= config.MinStep - 1e-9 && d <= config.MaxStep + 1e-9);
                }

                // Coube no próprio terreno e os degraus até as vizinhas são
                // permitidos: não é mesa marcada. A programação a marcou só
                // porque a olhava como escada numa grade de giros.
                if (cabe && Permitido(antes, z0) && Permitido(depois, z1))
                {
                    resolvidas[i] = r with { StartElevation = z0, EndElevation = z1, Violations = estouros, Marked = false, Reason = null, Seated = false };
                    continue;
                }

                // Coube no próprio terreno: o que a marca é o degrau até a
                // vizinha, e o motivo diz o desnível de verdade.
                var desnivel = Math.Max(
                    antes is { } a ? Math.Abs(z0 - a) : 0,
                    depois is { } b ? Math.Abs(z1 - b) : 0);

                var motivo = cabe
                    ? $"assentada no próprio terreno: o degrau até a vizinha ({desnivel.ToString("0.##", Brasil)} m) não é permitido (de {config.MinStep.ToString("0.##", Brasil)} a {config.MaxStep.ToString("0.##", Brasil)} m, ou zero)"
                    : Motivo(viavel, z0, z1, estouros);

                resolvidas[i] = r with { StartElevation = z0, EndElevation = z1, Violations = estouros, Reason = motivo, Seated = true };
            }
        }

        return new SolvedRun(resolvidas);
    }

    /// <summary>O primeiro e o último índice com custo finito; (1, 0) se não há nenhum.</summary>
    private static (int Baixa, int Alta) Finitos(double[] custos)
    {
        var baixa = 0;
        while (baixa < custos.Length && double.IsPositiveInfinity(custos[baixa])) baixa++;

        var alta = custos.Length - 1;
        while (alta >= 0 && double.IsPositiveInfinity(custos[alta])) alta--;

        return (baixa, alta);
    }

    private static string Motivo(ViableElevations viavel, double z0, double z1, int estouros)
    {
        if (viavel.IsEmpty) return viavel.WhyItDoesNotFit() ?? "nenhuma cota da ponta baixa respeita a faixa nesta mesa";

        if (viavel.Configuration.MaxLongitudinalSlope is { } limite && viavel.LongitudinalSlope(z0, z1) > limite)
            return "a declividade longitudinal passa do limite";

        return $"{estouros} módulo(s) fora da faixa, e a tolerância é {viavel.ToleratedModules}";
    }
}
