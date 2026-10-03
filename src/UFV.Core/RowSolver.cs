using System.Globalization;

namespace UFV.Core;

/// <summary>A cota do terreno sob a ponta baixa de um módulo, na estação dele.</summary>
/// <param name="Station">A estação do meio da ponta baixa ao longo da mesa.</param>
/// <param name="Ground">O terreno mais alto sob a ponta baixa do módulo, ou null sem terreno.</param>
public sealed record ChainModule(double Station, double? Ground);

/// <summary>Uma mesa a resolver numa fileira, na ordem das estações locais.</summary>
/// <param name="Label">O letreiro da mesa (F1.3), só para o relatório.</param>
/// <param name="GapBefore">
/// O vão em planta entre esta mesa e a anterior, em metro. Zero na primeira.
/// Vão maior que <see cref="SystemConfiguration.MaxGapBeforeBreak"/> quebra
/// a fileira ali: as pontas dos dois lados deixam de ser vizinhas.
/// </param>
/// <param name="Length">O comprimento da mesa, da estação zero à final.</param>
/// <param name="Modules">Os módulos da fileira de baixo.</param>
/// <param name="FirstStation">A estação do primeiro pilar (a ponta que encosta na mesa anterior).</param>
/// <param name="FirstGround">O terreno sob a ponta baixa no primeiro pilar, ou null.</param>
/// <param name="LastStation">A estação do último pilar (a ponta que encosta na mesa seguinte).</param>
/// <param name="LastGround">O terreno sob a ponta baixa no último pilar, ou null.</param>
public sealed record ChainTable(
    string Label,
    double GapBefore,
    double Length,
    IReadOnlyList<ChainModule> Modules,
    double FirstStation,
    double? FirstGround,
    double LastStation,
    double? LastGround)
{
    /// <summary>Se a mesa tem terreno em tudo que o motor olha: módulos e as duas pontas.</summary>
    public bool HasGround =>
        Modules.Count > 0 && Modules.All(m => m.Ground is { } g && double.IsFinite(g))
        && FirstGround is { } a && double.IsFinite(a) && LastGround is { } b && double.IsFinite(b);
}

/// <summary>Uma mesa resolvida: as cotas da ponta baixa e o que ficou fora da faixa.</summary>
/// <param name="Label">O letreiro.</param>
/// <param name="StartElevation">A cota da ponta baixa na estação zero.</param>
/// <param name="EndElevation">A cota da ponta baixa na estação final.</param>
/// <param name="Violations">Quantos módulos da fileira de baixo ficaram fora da faixa.</param>
/// <param name="Marked">
/// Se a mesa não cabe: mais módulos fora que a tolerância de lombo, ou sem
/// terreno. A mesa fica assim mesmo, na posição que a corrente escolheu, e
/// é marcada e pintada — o motor não move nem apaga.
/// </param>
/// <param name="Reason">Por que foi marcada, ou null.</param>
/// <param name="Seated">
/// Se a mesa foi posta fora da corrente da fileira (pontas à mão, sem
/// terreno): as pontas dela não contam como juntas da fileira.
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
/// <param name="Tables">As mesas, na ordem das estações.</param>
/// <param name="JointClearances">
/// A PB de cada junta do trecho (n + 1 para n mesas): a primeira é a da ponta
/// solta da primeira mesa, a última a da ponta solta da última, e cada uma
/// do meio é a PB das DUAS pontas que se encontram ali. Vazia num trecho
/// posto fora da corrente.
/// </param>
public sealed record SolvedRun(IReadOnlyList<SolvedTable> Tables, IReadOnlyList<double> JointClearances)
{
    /// <summary>Quantas mesas foram marcadas.</summary>
    public int MarkedCount => Tables.Count(t => t.Marked);

    /// <summary>Quantos módulos ficaram fora da faixa, em todas as mesas.</summary>
    public int ViolationCount => Tables.Sum(t => t.Violations);
}

/// <summary>O resultado do alinhamento de uma fileira.</summary>
/// <param name="Runs">Os trechos contínuos, na ordem; um vão grande separa dois.</param>
public sealed record RowSolution(IReadOnlyList<SolvedRun> Runs)
{
    /// <summary>Todas as mesas, trecho a trecho.</summary>
    public IReadOnlyList<SolvedTable> Tables => Runs.SelectMany(r => r.Tables).ToList();

    /// <summary>Quantas mesas foram marcadas.</summary>
    public int MarkedCount => Runs.Sum(r => r.MarkedCount);

    /// <summary>A linha que descreve o resultado para o usuário.</summary>
    public string Describe()
    {
        var fora = Runs.Sum(r => r.ViolationCount);

        return $"{Tables.Count} mesa(s) em {Runs.Count} trecho(s): {MarkedCount} marcada(s), "
            + $"{fora} módulo(s) fora da faixa";
    }
}

/// <summary>
/// Os pesos da corrente. Custo por metro, somado módulo a módulo e ponta a
/// ponta.
///
/// A ordem é do Renan (29/09/2026, noite): "1 - pontas do último módulo da
/// primeira mesa com a mesma altura do primeiro módulo da segunda mesa.
/// 2 - não deixar enterrado, mesmo que estoure declividade da mesa e altura
/// do pilar." A junta é a própria variável (não abre nunca); ENTERRAR é o
/// mais caro de tudo; passar do limite de declividade e ficar acima da
/// faixa (pilar mais alto) vêm depois; abaixo da faixa mas fora da terra é
/// o mais barato dos estouros. De manhã a ordem era outra ("prefiro módulo
/// na terra do que voando"), e o que ele viu à mão à noite — mesas boas
/// enterradas porque uma vizinha impossível as puxava — a inverteu.
/// </summary>
/// <param name="TipAboveBand">Metro de PB acima da faixa na ponta (no pilar da ponta).</param>
/// <param name="AboveBand">Metro acima da faixa num módulo.</param>
/// <param name="BelowBand">Metro abaixo da faixa (ponta ou módulo), ainda fora da terra.</param>
/// <param name="Buried">Metro abaixo do chão, somado ao de abaixo da faixa.</param>
/// <param name="SlopeExcess">
/// Metro de desnível entre as pontas além do que o limite de declividade
/// deixa. O limite deixou de ser parede: a mesa que precisa passar dele
/// para não enterrar (nem puxar as vizinhas para a terra pela junta) passa,
/// e é marcada.
/// </param>
/// <param name="OutEach">Custo fixo de cada ponto fora da faixa, para não espalhar estouro pequeno por muitos módulos.</param>
/// <param name="Tiebreak">Metro de PB dentro da faixa, só para desempatar (a mais baixa: pilar mais curto).</param>
public sealed record ChainWeights(
    double TipAboveBand, double TipBelowBand, double TipBuried,
    double AboveBand, double BelowBand, double Buried, double SlopeExcess, double OutEach, double Tiebreak)
{
    /// <summary>Os pesos de partida.</summary>
    public static readonly ChainWeights Default = new(
        TipAboveBand: 20, TipBelowBand: 20, TipBuried: 200,
        AboveBand: WeightsModule.Above, BelowBand: WeightsModule.Below, Buried: WeightsModule.Buried,
        SlopeExcess: 10, OutEach: 0.05, Tiebreak: 1e-3);

    /// <summary>Os pesos do módulo, à parte para a bancada experimentar.</summary>
    internal static class WeightsModule
    {
        internal const double Above = 3;
        internal const double Below = 1;
        internal const double Buried = 5;
    }

    /// <summary>A folga numérica das comparações com a faixa (a PB 0,30 da grade é 0,29999…).</summary>
    private const double Folga = 1e-9;

    /// <summary>O custo de um módulo com esta altura livre da ponta baixa.</summary>
    internal double Cost(double clearance, double min, double max) => Custo(clearance, min, max, AboveBand, BelowBand, Buried);

    /// <summary>O custo de uma ponta (pilar da ponta) com esta PB.</summary>
    internal double TipCost(double clearance, double min, double max) => Custo(clearance, min, max, TipAboveBand, TipBelowBand, TipBuried);

    /// <summary>O custo de uma mesa cujas pontas têm este desnível, com este alcance permitido pela declividade.</summary>
    internal double SlopeCost(double drop, double allowed) => Math.Abs(drop) > allowed ? SlopeExcess * (Math.Abs(drop) - allowed) : 0;

    private double Custo(double clearance, double min, double max, double acima, double abaixo, double enterrado)
    {
        if (clearance > max + Folga) return acima * (clearance - max) + OutEach;

        if (clearance < min - Folga)
        {
            var custo = abaixo * (min - clearance) + OutEach;
            if (clearance < -Folga) custo += enterrado * -clearance;
            return custo;
        }

        return Tiebreak * Math.Max(0, clearance - min);
    }
}

/// <summary>
/// O alinhamento na fileira, refeito do zero em 29/09/2026 (o Renan:
/// "revisão conceitual completa").
///
/// A fileira é uma CORRENTE. Regra absoluta: onde duas mesas se encontram,
/// a ponta de uma e a ponta da outra têm a mesma altura (a PB do último
/// pilar de uma é a PB do primeiro pilar da seguinte). Cada mesa pode ter
/// alturas diferentes nas suas duas pontas — é o giro dela, até o limite de
/// declividade —, mas o elo com a vizinha não abre. Qual é a melhor altura
/// de cada junta, quem diz é a otimização.
///
/// A variável é a PB de cada junta, numa grade de 1 cm. A mesa é rígida
/// (regra sagrada 2): com as PBs das duas pontas, a cota de cada módulo
/// sai por uma reta, e o custo da mesa é a soma, módulo a módulo, do quanto
/// cada ponta baixa sai da faixa — acima pesa mais que abaixo, e abaixo do
/// chão soma mais um tanto, mas continua mais barato que voar
/// (<see cref="ChainWeights"/>). Programação dinâmica ao longo da corrente:
/// o ótimo exato na grade, numa passada.
///
/// Não há mais "mesa viável" e "mesa marcada" posicionadas por contas
/// diferentes (era isso que deixava a marcada solta das vizinhas): toda mesa
/// está na corrente, e marcada é só a que ficou com mais módulos fora da
/// faixa que a tolerância de lombo (regra sagrada 4). O degrau entre mesas
/// da configuração não entra: não há degrau, há junta.
///
/// Fora da corrente: a mesa sem terreno sob algum módulo ou ponta (fica
/// nivelada sobre o terreno mais alto que tiver, marcada) e o vão maior que
/// o limite, que quebra a fileira em trechos.
/// </summary>
public static class RowSolver
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O passo padrão da grade de PB: um centímetro.</summary>
    public const double DefaultStep = 0.01;

    /// <summary>Quanto a grade de PB vai além da faixa de cada lado, de partida. Alarga se a corrente não fecha.</summary>
    private const double Margem = 1.5;

    /// <summary>O maior alargamento da grade antes de desistir.</summary>
    private const double MargemMaxima = 24;

    /// <summary>
    /// Resolve a fileira.
    /// </summary>
    /// <param name="tables">As mesas, na ordem das estações locais.</param>
    /// <param name="configuration">Faixa da PB, tolerância de lombo, declividade, vão que quebra.</param>
    /// <param name="weights">Os pesos; null usa <see cref="ChainWeights.Default"/>.</param>
    /// <param name="step">O passo da grade de PB.</param>
    /// <param name="firstTip">
    /// A PB imposta na primeira ponta da corrente (primeiro pilar da primeira
    /// mesa), ou null para a otimização escolher. É a PB da vizinha que não
    /// está sendo recalculada: a junta com ela não pode abrir.
    /// </param>
    /// <param name="lastTip">O mesmo na última ponta (último pilar da última mesa).</param>
    public static RowSolution Solve(
        IReadOnlyList<ChainTable> tables, SystemConfiguration configuration, ChainWeights? weights = null, double step = DefaultStep,
        double? firstTip = null, double? lastTip = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(configuration);

        if (tables.Count == 0) throw new ArgumentException("A fileira não tem mesa.", nameof(tables));

        if (configuration.WhyInvalid is { } motivo)
            throw new InvalidOperationException($"A configuração não fecha: {motivo}.");

        if (!double.IsFinite(step) || step < 1e-4 || step > 0.5)
            throw new ArgumentOutOfRangeException(nameof(step), step, "O passo da grade precisa ficar entre 0,1 mm e 50 cm.");

        foreach (var mesa in tables)
        {
            ArgumentNullException.ThrowIfNull(mesa, nameof(tables));

            if (!double.IsFinite(mesa.GapBefore) || mesa.GapBefore < 0)
                throw new ArgumentException($"O vão antes da mesa {mesa.Label} não é uma medida.", nameof(tables));

            if (!double.IsFinite(mesa.Length) || mesa.Length <= 0)
                throw new ArgumentException($"O comprimento da mesa {mesa.Label} não é uma medida.", nameof(tables));

            if (!(mesa.LastStation - mesa.FirstStation > 1e-6))
                throw new ArgumentException($"A mesa {mesa.Label} não tem duas pontas (primeiro e último pilar).", nameof(tables));
        }

        var w = weights ?? ChainWeights.Default;
        var trechos = new List<SolvedRun>();
        var atual = new List<ChainTable>();

        void Fechar()
        {
            if (atual.Count > 0)
            {
                // A ponta presa só vale para o trecho que tem a primeira (ou
                // a última) mesa da fileira.
                var primeira = ReferenceEquals(atual[0], tables[0]) ? firstTip : null;
                var ultima = ReferenceEquals(atual[^1], tables[^1]) ? lastTip : null;

                trechos.Add(ResolverCorrente(atual, configuration, w, step, primeira, ultima));
            }

            atual = [];
        }

        foreach (var mesa in tables)
        {
            if (atual.Count > 0 && mesa.GapBefore > configuration.MaxGapBeforeBreak + 1e-9) Fechar();

            if (!mesa.HasGround)
            {
                Fechar();
                trechos.Add(new SolvedRun([SemTerreno(mesa, configuration)], []));
                continue;
            }

            atual.Add(mesa);
        }

        Fechar();

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

    /// <summary>A mesa sem terreno sob algum ponto: nivelada sobre o mais alto que tiver, marcada. Sem terreno nenhum, cota zero, dita.</summary>
    private static SolvedTable SemTerreno(ChainTable mesa, SystemConfiguration config)
    {
        var cotas = mesa.Modules.Select(m => m.Ground).Append(mesa.FirstGround).Append(mesa.LastGround)
            .Where(g => g is { } z && double.IsFinite(z)).Select(g => g!.Value).ToList();

        var semChao = mesa.Modules.Count(m => m.Ground is not { } g || !double.IsFinite(g));
        var motivo = mesa.Modules.Count == 0
            ? "a mesa não tem módulo na fileira de baixo para conferir a ponta baixa"
            : semChao > 0
                ? $"{semChao} módulo(s) da fileira de baixo sem terreno embaixo"
                : "uma ponta da mesa sem terreno embaixo";

        if (cotas.Count == 0) return new SolvedTable(mesa.Label, 0, 0, mesa.Modules.Count, true, motivo, Seated: true);

        var cota = cotas.Max() + config.MinLowEdge;

        return new SolvedTable(mesa.Label, cota, cota, mesa.Modules.Count, true, motivo, Seated: true);
    }

    /// <summary>
    /// A corrente de um trecho. Primeiro numa grade grossa (5 cm) por toda a
    /// folga, depois na grade fina só perto do que a grossa achou: a usina de
    /// mil mesas levava 33 s com a grade fina inteira.
    /// </summary>
    private static SolvedRun ResolverCorrente(
        List<ChainTable> mesas, SystemConfiguration config, ChainWeights w, double passo, double? primeira, double? ultima)
    {
        var razao = passo < PassoGrosso ? (long)Math.Max(1, Math.Round(PassoGrosso / passo)) : 1;
        var grosso = passo * razao;

        for (var margem = Margem; ; margem *= 2)
        {
            var lo = (long)Math.Floor((config.MinLowEdge - margem) / grosso);
            var hi = (long)Math.Ceiling((config.MaxLowEdge + margem) / grosso);
            var faixas = Enumerable.Repeat((Lo: lo, Hi: hi), mesas.Count + 1).ToArray();
            Prender(faixas, primeira, ultima, grosso, exata: false);

            var juntas = Corrente(mesas, config, w, grosso, faixas);

            // Junta encostada na borda da grade: a folga pode ter cortado a
            // solução. Alarga e refaz. Sem solução nenhuma (o terreno é mais
            // íngreme que a declividade por uma extensão longa), também.
            var cortada = juntas is null || juntas
                .Where((_, j) => !(j == 0 && primeira is not null) && !(j == juntas.Length - 1 && ultima is not null))
                .Any(k => k == lo || k == hi);

            if (cortada && margem < MargemMaxima) continue;

            // Com a ponta presa na vizinha a declividade pode não deixar
            // corrente nenhuma (a vizinha foi mexida à mão): solta a ponta e
            // resolve livre. A junta aberta aparece na validação.
            if (juntas is null && (primeira is not null || ultima is not null))
                return ResolverCorrente(mesas, config, w, passo, null, null);

            if (juntas is null)
            {
                throw new InvalidOperationException(
                    $"A fileira de {mesas[0].Label} a {mesas[^1].Label} não fecha nem com {MargemMaxima:0} m de folga na ponta baixa: "
                    + "o terreno é mais íngreme que a declividade permite por uma extensão grande demais.");
            }

            var finas = juntas.Select(k => k * razao).ToArray();
            var presa = primeira is not null || ultima is not null;

            // A grossa cabe na fina (o passo grosso é múltiplo do fino): a
            // corrente grossa está dentro das faixas finas, e a fina nunca
            // sai pior que ela. Com ponta presa a grossa só chegou perto
            // (o valor preso não está na grade de 5 cm): a fina prende no
            // valor exato, e se não fechar perto da grossa, tenta a grade
            // fina inteira antes de soltar a ponta.
            if (razao > 1 || presa)
            {
                var raio = 2 * razao;
                var faixasFinas = finas.Select(k => (Lo: k - raio, Hi: k + raio)).ToArray();
                Prender(faixasFinas, primeira, ultima, passo, exata: true);

                var fina = Corrente(mesas, config, w, passo, faixasFinas);

                if (fina is null && presa)
                {
                    var inteira = Enumerable.Repeat((Lo: lo * razao, Hi: hi * razao), mesas.Count + 1).ToArray();
                    Prender(inteira, primeira, ultima, passo, exata: true);

                    fina = Corrente(mesas, config, w, passo, inteira);

                    if (fina is null) return ResolverCorrente(mesas, config, w, passo, null, null);
                }

                finas = fina ?? finas;
            }

            return Montar(mesas, config, finas.Select(k => k * passo).ToList());
        }
    }

    /// <summary>
    /// Fecha a faixa da primeira e da última junta na PB imposta, se houver:
    /// no valor exato, ou (na grade grossa, onde o valor não cai num ponto
    /// da grade) nos dois pontos da grade em volta dele.
    /// </summary>
    private static void Prender((long Lo, long Hi)[] faixas, double? primeira, double? ultima, double passo, bool exata)
    {
        (long, long) Faixa(double pb) => exata
            ? ((long)Math.Round(pb / passo), (long)Math.Round(pb / passo))
            : ((long)Math.Floor(pb / passo + 1e-9), (long)Math.Ceiling(pb / passo - 1e-9));

        if (primeira is { } a) faixas[0] = Faixa(a);
        if (ultima is { } b) faixas[^1] = Faixa(b);
    }

    /// <summary>O maior giro que é mesa: nove décimos do comprimento de desnível.</summary>
    private const double SenoFisico = 0.9;

    /// <summary>O passo da grade grossa.</summary>
    private const double PassoGrosso = 0.05;

    /// <summary>
    /// A programação dinâmica: cada junta com a PB numa faixa de chaves da
    /// grade. Devolve a chave escolhida de cada junta, ou null se nenhuma
    /// corrente respeita a declividade dentro das faixas.
    /// </summary>
    private static long[]? Corrente(List<ChainTable> mesas, SystemConfiguration config, ChainWeights w, double passo, (long Lo, long Hi)[] faixas)
    {
        var min = config.MinLowEdge;
        var max = config.MaxLowEdge;

        // A declividade: o limite configurado (sen(limite) sobre o vão entre
        // as pontas) é custo, não parede; a parede é física, nove décimos
        // (desnível maior que o comprimento não é mesa).
        var seno = config.MaxLongitudinalSlope is { } limite ? Math.Min(Math.Sin(limite), SenoFisico) : SenoFisico;

        double[] CustoDasPontas(int j)
        {
            var (lo, hi) = faixas[j];
            var custos = new double[hi - lo + 1];

            for (var k = 0; k < custos.Length; k++) custos[k] = w.TipCost((lo + k) * passo, min, max);

            return custos;
        }

        var custo = CustoDasPontas(0);
        var origens = new List<int[]>(mesas.Count);

        for (var i = 0; i < mesas.Count; i++)
        {
            var mesa = mesas[i];
            var a = mesa.FirstStation;
            var vao = mesa.LastStation - a;
            var ga = mesa.FirstGround!.Value;
            var gb = mesa.LastGround!.Value;
            var alcance = SenoFisico * vao;
            var permitido = seno * vao;

            var fracoes = mesa.Modules.Select(m => (m.Station - a) / vao).ToArray();
            var chaos = mesa.Modules.Select(m => m.Ground!.Value).ToArray();

            var lo0 = faixas[i].Lo;
            var lo1 = faixas[i + 1].Lo;
            var pontas = CustoDasPontas(i + 1);
            var n1 = pontas.Length;

            var proximo = new double[n1];
            var origem = new int[n1];
            Array.Fill(proximo, double.PositiveInfinity);
            Array.Fill(origem, -1);

            for (var k = 0; k < custo.Length; k++)
            {
                var base0 = custo[k];
                if (double.IsPositiveInfinity(base0)) continue;

                var za = ga + (lo0 + k) * passo;

                // As PBs da outra ponta que a física deixa: zb em za ± alcance.
                var kb0 = (int)Math.Max(0, (long)Math.Ceiling((za - alcance - gb) / passo - 1e-9) - lo1);
                var kb1 = (int)Math.Min(n1 - 1, (long)Math.Floor((za + alcance - gb) / passo + 1e-9) - lo1);

                for (var kb = kb0; kb <= kb1; kb++)
                {
                    var zb = gb + (lo1 + kb) * passo;
                    var total = base0 + pontas[kb] + w.SlopeCost(zb - za, permitido);

                    // Todo custo é positivo: passou do melhor, para.
                    for (var m = 0; m < fracoes.Length && total < proximo[kb]; m++)
                        total += w.Cost(za + (zb - za) * fracoes[m] - chaos[m], min, max);

                    if (total < proximo[kb])
                    {
                        proximo[kb] = total;
                        origem[kb] = k;
                    }
                }
            }

            if (Array.TrueForAll(proximo, double.IsPositiveInfinity)) return null;

            origens.Add(origem);
            custo = proximo;
        }

        var melhor = 0;
        for (var k = 1; k < custo.Length; k++)
            if (custo[k] < custo[melhor]) melhor = k;

        var indices = new int[mesas.Count + 1];
        indices[mesas.Count] = melhor;

        for (var i = mesas.Count - 1; i >= 0; i--) indices[i] = origens[i][indices[i + 1]];

        return indices.Select((k, j) => faixas[j].Lo + k).ToArray();
    }

    /// <summary>As mesas do trecho com as PBs das juntas escolhidas.</summary>
    private static SolvedRun Montar(List<ChainTable> mesas, SystemConfiguration config, List<double> pbs)
    {
        var resolvidas = new List<SolvedTable>(mesas.Count);

        for (var i = 0; i < mesas.Count; i++)
        {
            var mesa = mesas[i];
            var za = mesa.FirstGround!.Value + pbs[i];
            var zb = mesa.LastGround!.Value + pbs[i + 1];
            var inclinacao = (zb - za) / (mesa.LastStation - mesa.FirstStation);

            var z0 = za - inclinacao * mesa.FirstStation;
            var z1 = za + inclinacao * (mesa.Length - mesa.FirstStation);

            resolvidas.Add(Relatar(mesa, z0, z1, config));
        }

        return new SolvedRun(resolvidas, pbs);
    }

    /// <summary>
    /// A mesa resolvida com estas cotas: quantos módulos fora da faixa, e se
    /// algum entrou na terra. Marcada (magenta) é SÓ módulo enterrado. Renan,
    /// 03/10/2026: "o não cabe deve ser somente módulo que entra na terra, o
    /// resto não, o resto eu valido por análises". Fora da faixa sem enterrar
    /// e passar do limite de declividade saem nas análises.
    /// </summary>
    private static SolvedTable Relatar(ChainTable mesa, double z0, double z1, SystemConfiguration config)
    {
        var fora = 0;
        var enterrados = new List<int>();
        var maisEnterrado = 0.0;

        for (var k = 0; k < mesa.Modules.Count; k++)
        {
            var m = mesa.Modules[k];
            var c = z0 + (z1 - z0) * m.Station / mesa.Length - m.Ground!.Value;

            if (c > config.MaxLowEdge + 1e-9 || c < config.MinLowEdge - 1e-9) fora++;

            if (c < 0)
            {
                enterrados.Add(k + 1);
                maisEnterrado = Math.Max(maisEnterrado, -c);
            }
        }

        var marcada = enterrados.Count > 0;
        var motivo = marcada
            ? $"{enterrados.Count} módulo(s) com a ponta baixa dentro da terra (o {string.Join(", ", enterrados)}º da fileira de baixo, "
                + $"contando da ponta inicial), até {Cm(maisEnterrado)} cm abaixo do chão"
            : null;

        return new SolvedTable(mesa.Label, z0, z1, fora, marcada, motivo);
    }

    private static string Cm(double metros) => (metros * 100).ToString("0", Brasil);
}
