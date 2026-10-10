using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma ponta de string para o roteamento CC: o ponto do módulo, os cantos da mesa dele e os de todas as mesas da fileira.</summary>
public sealed record StringEndInput(Point3 Point, Guid Table, IReadOnlyList<Point3> TableCorners, IReadOnlyList<Point3> RowCorners);

/// <summary>Uma string a rotear (CC ou o trecho string -> combiner): as duas pontas e o destino (o ponto dele em campo, null se não está em campo).</summary>
/// <remarks><paramref name="DestinationOutline"/>: o contorno em planta do destino (ou da área onde ele está); vala que entra nele vale antes do raio.</remarks>
public sealed record StringRouteInput(Guid String, string Tag, StringEndInput Positive, StringEndInput Negative, CableEnd Destination, string DestinationName, Point3? DestinationPoint, RowEnd? ForcedEnd, IReadOnlyList<Point3>? DestinationOutline = null);

/// <summary>Um trecho entre dois equipamentos (CA, MT, combiner -> inversor): os pontos em campo (null = não está em campo).</summary>
/// <remarks>Os contornos em planta (null = sem contorno): vala que entra no contorno vale antes do raio.</remarks>
public sealed record EquipmentRouteInput(CableEnd From, string FromName, Point3? FromPoint, CableEnd To, string ToName, Point3? ToPoint,
    IReadOnlyList<Point3>? FromOutline = null, IReadOnlyList<Point3>? ToOutline = null);

/// <summary>Um lance planejado: o lance e o percurso 3D.</summary>
public sealed record PlannedRun(CableRun Run, IReadOnlyList<Point3> Path)
{
    public double Length => CablePath.Length(Path);
}

/// <summary>
/// Um trecho que não pôde ser roteado (regra 4: nunca calado): o motivo e o
/// que pintar (a mesa da string, ou o equipamento sem vala no alcance).
/// </summary>
public sealed record RouteFailure(string What, string Reason, IReadOnlyList<CableEnd> Paint, IReadOnlyList<Guid> PaintTables);

/// <summary>O resultado de um Gerar: os lances e as falhas.</summary>
public sealed record RouteResult(IReadOnlyList<PlannedRun> Runs, IReadOnlyList<RouteFailure> Failures);

/// <summary>
/// O roteador (roteamento, etapas 18, 20 e 21): decide por onde vai cada
/// cabo e devolve o percurso 3D. A geometria é do Clivus.Geo (rede de valas,
/// saída da mesa, percurso 3D); aqui ficam as regras: menor percurso (3),
/// nunca falhar calado (4), dois lances por string no CC (6), cabo em 3D (9).
/// </summary>
public static class CableRouter
{
    /// <summary>
    /// CC (e o trecho string -> combiner): cada string sai pela ponta da
    /// fileira de menor comprimento total (18.2), ou pela forçada (18.3); as
    /// duas pontas (+ e −) vão pelo mesmo lado e viram dois lances (18.7).
    /// </summary>
    /// <remarks>
    /// Uma entrada só por mesa (Renan, 10/10/2026, item 10): todas as strings
    /// saem pelo lado alto das mesas (<see cref="ExitPattern"/>, o padrão da
    /// usina, ou <paramref name="pattern"/> se quem chama já o tem), passam
    /// pelo ponto de juntar da mesa e vão para a mesma ponta da fileira: a de
    /// menor soma das strings da mesa (as mesas ligadas por uma string decidem
    /// juntas). A string com o lado forçado (18.3) vai pelo dela.
    /// </remarks>
    public static RouteResult Strings(
        IReadOnlyList<StringRouteInput> strings, CableRoute rota, TrenchNetwork valas, RouteSettings config, Func<double, double, double?> chao, ExitPattern? pattern = null)
    {
        var lances = new List<PlannedRun>();
        var falhas = new List<RouteFailure>();
        var padrao = pattern ?? ExitPattern.For(strings);

        // As árvores de caminho a partir das valas de cada destino, por nível de
        // preferência (as que entram no contorno, depois as do raio): uma vez por destino.
        var arvores = new Dictionary<CableEnd, List<List<TrenchNetwork.TrenchTree>>>();

        // As tentativas de cada string que pode ser roteada, pelas duas pontas da fileira (ou só a forçada).
        var tentativas = new Dictionary<StringRouteInput, Dictionary<RowEnd, ((PlannedRun Pos, PlannedRun Neg)? Par, string? Porque)>>(ReferenceEqualityComparer.Instance);

        foreach (var s in strings)
        {
            var nome = Nome(s);
            var mesas = new[] { s.Positive.Table, s.Negative.Table }.Distinct().ToList();

            if (s.Destination.Id == Guid.Empty)
            {
                falhas.Add(new RouteFailure(nome, Tr.T("a string não está alocada em nenhum inversor"), [new CableEnd(CableEndKind.String, s.String)], mesas));
                continue;
            }

            if (s.DestinationPoint is not { } destino)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("{0} não está em campo", s.DestinationName), [new CableEnd(CableEndKind.String, s.String)], mesas));
                continue;
            }

            // As valas que chegam no destino: as que entram no contorno dele; sem
            // nenhuma que se ligue, todas dentro do raio (a mais perto pode estar solta).
            if (!arvores.TryGetValue(s.Destination, out var chegadas))
                arvores[s.Destination] = chegadas = TrenchAccessLevels(valas, destino, s.DestinationOutline, config.Radius).Select(n => n.Select(valas.Tree).ToList()).ToList();

            if (chegadas.Count == 0)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("{0} sem vala no raio de {1:0.#} m", s.DestinationName, config.Radius), [s.Destination], mesas));
                continue;
            }

            var porPonta = new Dictionary<RowEnd, ((PlannedRun Pos, PlannedRun Neg)? Par, string? Porque)>();
            foreach (var ponta in s.ForcedEnd is { } forcada ? new[] { forcada } : new[] { RowEnd.Start, RowEnd.End })
            {
                var pos = Lance(s.Positive, ponta, CablePolarity.Positive, out var p1);
                var neg = Lance(s.Negative, ponta, CablePolarity.Negative, out var p2);
                porPonta[ponta] = pos is null || neg is null ? (null, p1 ?? p2) : ((pos, neg), null);
            }

            tentativas[s] = porPonta;

            PlannedRun? Lance(StringEndInput ponta, RowEnd lado, CablePolarity polaridade, out string? problema)
            {
                if (StringExit(ponta, lado, valas, config, out problema, padrao) is not { } saida) return null;
                var batida = saida.Hit;

                // A chegada com o menor percurso (pela vala até ela, mais dela ao
                // destino), no primeiro nível em que alguma se liga.
                var arvore = chegadas
                    .Select(nivel => nivel.Where(a => double.IsFinite(a.Length(batida))).MinBy(a => a.Length(batida) + Plano(a.From.At, destino)))
                    .FirstOrDefault(a => a is not null);
                if (arvore is null || arvore.Path(batida) is not { } deChegada)
                {
                    problema = Tr.F("a vala do fim da fileira não se liga às valas de {0}", s.DestinationName);
                    return null;
                }

                var pelaVala = Enumerable.Reverse(deChegada).ToList();
                var percurso = CablePath.Build(ponta.Point, [.. saida.Exit, batida.At], pelaVala, [], destino, config.Depth, chao, out var fora);
                if (percurso is null)
                {
                    problema = Tr.F("trecho fora do terreno em ({0:0.##}; {1:0.##})", fora!.Value.X, fora.Value.Y);
                    return null;
                }

                var run = new CableRun(Guid.NewGuid(), rota, polaridade, new CableEnd(CableEndKind.String, s.String), s.Destination);
                return new PlannedRun(run, percurso);
            }
        }

        // A ponta da fileira de cada grupo de mesas (as ligadas por uma string decidem
        // juntas): a que roteia mais strings livres e, empatado, a de menor soma.
        var pontaDoGrupo = new Dictionary<Guid, RowEnd>();
        foreach (var grupo in tentativas.Keys.GroupBy(s => padrao.Group(s.Positive.Table)))
        {
            var livres = grupo.Where(s => s.ForcedEnd is null).ToList();
            if (livres.Count == 0) continue;
            pontaDoGrupo[grupo.Key] = new[] { RowEnd.Start, RowEnd.End }
                .OrderByDescending(p => livres.Count(s => tentativas[s][p].Par is not null))
                .ThenBy(p => livres.Sum(s => tentativas[s][p].Par is { } par ? par.Pos.Length + par.Neg.Length : 0))
                .First();
        }

        foreach (var s in strings)
        {
            if (!tentativas.TryGetValue(s, out var porPonta)) continue;

            // A do grupo; se esta string não passa por ela, a outra (cabo é melhor que falha).
            var escolhida = s.ForcedEnd ?? pontaDoGrupo[padrao.Group(s.Positive.Table)];
            var (par, porque) = porPonta[escolhida];
            if (par is null && s.ForcedEnd is null && porPonta[escolhida == RowEnd.Start ? RowEnd.End : RowEnd.Start].Par is { } outra) par = outra;

            if (par is { } m)
            {
                lances.Add(m.Pos);
                lances.Add(m.Neg);
            }
            else
            {
                falhas.Add(new RouteFailure(Nome(s), porque ?? porPonta.Values.Select(v => v.Porque).FirstOrDefault(p => p is not null) ?? Tr.T("sem caminho"),
                    [new CableEnd(CableEndKind.String, s.String)], new[] { s.Positive.Table, s.Negative.Table }.Distinct().ToList()));
            }
        }

        return new RouteResult(lances, falhas);

        static string Nome(StringRouteInput s) => Tr.F("string {0}", string.IsNullOrWhiteSpace(s.Tag) ? s.String.ToString("D")[..8] : s.Tag);
    }

    /// <summary>
    /// CA, MT e combiner -> inversor (20.1, 21.1): em volta de cada
    /// equipamento, as valas dentro do raio; entre elas, o caminho mais curto
    /// pela rede (a mais perto pode estar solta: vale o menor total que se
    /// liga). Sem vala no raio: avisa e pinta o equipamento (20.3).
    /// </summary>
    public static RouteResult Equipment(
        IReadOnlyList<EquipmentRouteInput> trechos, CableRoute rota, TrenchNetwork valas, RouteSettings config, Func<double, double, double?> chao)
    {
        var lances = new List<PlannedRun>();
        var falhas = new List<RouteFailure>();

        foreach (var t in trechos)
        {
            var nome = Tr.F("{0} → {1}", t.FromName, t.ToName);

            if (t.To.Id == Guid.Empty)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("{0} não tem para onde ir (sem vínculo na cadeia)", t.FromName), [t.From], []));
                continue;
            }

            if (t.FromPoint is not { } de || t.ToPoint is not { } para)
            {
                var foraDeCampo = new List<CableEnd>();
                if (t.FromPoint is null) foraDeCampo.Add(t.From);
                if (t.ToPoint is null) foraDeCampo.Add(t.To);
                var quem = string.Join(Tr.T(" e "), foraDeCampo.Select(e => e == t.From ? t.FromName : t.ToName));
                // Pinta a ponta que ESTÁ em campo (a outra não tem o que pintar).
                falhas.Add(new RouteFailure(nome, Tr.F("{0} não está em campo", quem), t.FromPoint is null ? [t.To] : [t.From], []));
                continue;
            }

            var saidas = TrenchAccessLevels(valas, de, t.FromOutline, config.Radius);
            var chegadas = TrenchAccessLevels(valas, para, t.ToOutline, config.Radius);
            if (saidas.Count == 0 || chegadas.Count == 0)
            {
                var sem = new List<CableEnd>();
                if (saidas.Count == 0) sem.Add(t.From);
                if (chegadas.Count == 0) sem.Add(t.To);
                var quem = string.Join(Tr.T(" e "), sem.Select(e => e == t.From ? t.FromName : t.ToName));
                falhas.Add(new RouteFailure(nome, Tr.F("{0} sem vala no raio de {1:0.#} m", quem, config.Radius), sem, []));
                continue;
            }

            // O par (saída, chegada) de menor total que se liga, nos níveis de
            // preferência: as valas que entram nos contornos antes das do raio.
            (TrenchPoint Saida, IReadOnlyList<Point3> Caminho, double Total)? melhor = null;
            foreach (var nivelDeSaida in saidas)
            {
                foreach (var nivelDeChegada in chegadas)
                {
                    foreach (var s in nivelDeSaida)
                    {
                        var arvore = valas.Tree(s);
                        foreach (var c in nivelDeChegada)
                        {
                            var total = Plano(de, s.At) + arvore.Length(c) + Plano(c.At, para);
                            if (double.IsInfinity(total) || (melhor is { } m && m.Total <= total)) continue;
                            if (arvore.Path(c) is { } caminho) melhor = (s, caminho, total);
                        }
                    }

                    if (melhor is not null) break;
                }

                if (melhor is not null) break;
            }

            if (melhor is not { } escolhido)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("as valas de {0} e de {1} não se ligam", t.FromName, t.ToName), [t.From, t.To], []));
                continue;
            }

            var percurso = CablePath.Build(de, [escolhido.Saida.At], escolhido.Caminho, [], para, config.Depth, chao, out var fora, groundFromStart: true);
            if (percurso is null)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("trecho fora do terreno em ({0:0.##}; {1:0.##})", fora!.Value.X, fora.Value.Y), [t.From], []));
                continue;
            }

            lances.Add(new PlannedRun(new CableRun(Guid.NewGuid(), rota, CablePolarity.None, t.From, t.To), percurso));
        }

        return new RouteResult(lances, falhas);
    }

    /// <summary>
    /// A saída de uma ponta de string até a vala, que não depende do destino
    /// (18.1, 18.4): da ponta, contornando a fileira pelo lado
    /// <paramref name="lado"/>, e do fim da fileira reto até bater na vala.
    /// Null, com o motivo, se a mesa não tem os cantos ou se nenhuma vala
    /// cruza a reta no alcance (18.5).
    /// </summary>
    /// <remarks>Com <paramref name="pattern"/>, a saída é pelo lado da usina e passa pelo ponto de juntar da mesa (item 10).</remarks>
    public static (IReadOnlyList<Point3> Exit, TrenchPoint Hit)? StringExit(StringEndInput ponta, RowEnd lado, TrenchNetwork valas, RouteSettings config, out string? problema, ExitPattern? pattern = null)
    {
        problema = null;
        (IReadOnlyList<Point3> Points, Point3 Direction) saida;
        try
        {
            saida = RowExit.Plan(ponta.Point, ponta.TableCorners, ponta.RowCorners, lado, RowExit.Margin, pattern?.SideFor(ponta), pattern?.Gather(ponta, lado));
        }
        catch (ArgumentException)
        {
            // Mesa sem os 4 cantos ou sem direção: avisa e pinta, não derruba o Gerar.
            problema = Tr.T("a mesa da ponta da string não tem os 4 cantos legíveis");
            return null;
        }

        // Do fim da fileira, reto até bater na vala (18.4); não bateu: a vala não passa da mesa (18.5).
        if (valas.Ray(saida.Points[^1], saida.Direction, config.Reach) is not { } batida)
        {
            problema = Tr.F("nenhuma vala cruza a reta do fim da fileira (alcance {0:0.#} m): falta a referência da vala", config.Reach);
            return null;
        }

        return (saida.Points, batida);
    }

    /// <summary>
    /// Por onde o cabo entra e sai da rede de valas num equipamento, em
    /// níveis de preferência: primeiro as valas que entram no contorno dele
    /// (o rabicho que o usuário desenhou até ele, só em planta); depois as que
    /// passam dentro do raio (Renan, 10/10/2026: "o motor deve procurar
    /// primeiro por valas que entram e em seguida fazer a busca pelo raio").
    /// Quem roteia usa o segundo nível só se nenhuma do primeiro se liga
    /// (rabicho solto). Níveis vazios ficam de fora; sem nenhum, vazio.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TrenchPoint>> TrenchAccessLevels(TrenchNetwork valas, Point3 ponto, IReadOnlyList<Point3>? contorno, double raio)
    {
        var niveis = new List<IReadOnlyList<TrenchPoint>>();
        if (contorno is { Count: >= 3 } && valas.Entering(contorno, ponto) is { Count: > 0 } entram) niveis.Add(entram);
        if (valas.Within(ponto, raio) is { Count: > 0 } perto) niveis.Add(perto);
        return niveis;
    }

    private static double Plano(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>
/// O padrão de saída das strings da usina (Renan, 10/10/2026, item 10: "cada
/// mesa tivesse apenas uma entrada de cabo ... o software se orienta pela
/// altura das mesas mesmo e cria o padrão da usina"):
/// <list type="bullet">
/// <item>o lado: a soma das direções do lado alto de cada mesa (pela cota real
/// dos cantos; na estrutura fixa do hemisfério sul, o sul). Toda mesa sai
/// pela borda comprida que olha para ele, mesmo a que num terreno torto
/// ficou com a outra borda mais alta: um padrão só. Sem nenhuma mesa com
/// altura (todas planas), null: cada ponta sai pela borda mais perto, como era;</item>
/// <item>o ponto de juntar de cada mesa, para cada ponta da fileira: a ponta de
/// string mais adiantada naquela direção. Todas as strings da mesa passam por
/// ele, e dali seguem juntas;</item>
/// <item>os grupos de mesas que decidem juntos a ponta da fileira: as ligadas
/// por uma string (a + numa mesa e a − na vizinha).</item>
/// </list>
/// </summary>
public sealed class ExitPattern
{
    private readonly Dictionary<Guid, (double Ux, double Uy, double Min, double Max)> _juntar = [];

    // A soma das pontas de string de cada mesa (para o lado da mesa sem padrão: a borda mais perto do meio delas).
    private readonly Dictionary<Guid, (double X, double Y, int N)> _pontas = [];
    private readonly Dictionary<Guid, Guid> _pai = [];

    /// <summary>A direção em planta (unitária) do lado de saída da usina; null = a borda mais perto de cada ponta.</summary>
    public Point3? Side { get; }

    private ExitPattern(Point3? side) => Side = side;

    /// <summary>
    /// O padrão das strings: o lado (<paramref name="side"/>, se quem chama
    /// já o tem, ou o das mesas das próprias strings), o ponto de juntar e os grupos.
    /// </summary>
    public static ExitPattern For(IEnumerable<StringRouteInput> strings, Point3? side = null)
    {
        var lista = strings.ToList();
        var padrao = new ExitPattern(side ?? PlantSide(lista.SelectMany(s => new[] { s.Positive, s.Negative }).GroupBy(p => p.Table).Select(g => g.First().TableCorners)));

        foreach (var ponta in lista.SelectMany(s => new[] { s.Positive, s.Negative }))
        {
            if (ponta.TableCorners.Count < 4) continue;
            var dx = ponta.TableCorners[1].X - ponta.TableCorners[0].X;
            var dy = ponta.TableCorners[1].Y - ponta.TableCorners[0].Y;
            var n = Math.Sqrt(dx * dx + dy * dy);
            if (n < 1e-9) continue;
            var soma = padrao._pontas.GetValueOrDefault(ponta.Table);
            padrao._pontas[ponta.Table] = (soma.X + ponta.Point.X, soma.Y + ponta.Point.Y, soma.N + 1);

            // As pontas de uma mesa têm os mesmos cantos: a direção é a da primeira.
            if (padrao._juntar.TryGetValue(ponta.Table, out var j))
            {
                var ao = ponta.Point.X * j.Ux + ponta.Point.Y * j.Uy;
                padrao._juntar[ponta.Table] = (j.Ux, j.Uy, Math.Min(j.Min, ao), Math.Max(j.Max, ao));
            }
            else
            {
                var ao = (ponta.Point.X * dx + ponta.Point.Y * dy) / n;
                padrao._juntar[ponta.Table] = (dx / n, dy / n, ao, ao);
            }
        }

        foreach (var s in lista) padrao.Unir(s.Positive.Table, s.Negative.Table);
        return padrao;
    }

    /// <summary>
    /// O lado da usina pelas mesas: a soma das direções do lado alto de cada
    /// uma (<see cref="RowExit.HighSide"/>), unitária; null se nenhuma tem
    /// altura ou se elas se anulam.
    /// </summary>
    public static Point3? PlantSide(IEnumerable<IReadOnlyList<Point3>> tables)
    {
        double x = 0, y = 0;
        foreach (var t in tables)
        {
            if (RowExit.HighSide(t) is not { } s) continue;
            x += s.X;
            y += s.Y;
        }

        var n = Math.Sqrt(x * x + y * y);
        return n < 1e-6 ? null : new Point3(x / n, y / n, 0);
    }

    /// <summary>
    /// O lado de saída da mesa da ponta: o da usina; se a mesa está atravessada
    /// em relação a ele (mais de 80°), o lado alto dela; sem altura, a borda
    /// mais perto do meio das pontas de string dela. Sempre um lado só por mesa.
    /// </summary>
    public Point3? SideFor(StringEndInput ponta)
    {
        var c = ponta.TableCorners;
        if (c.Count < 4) return Side;
        var dx = c[1].X - c[0].X;
        var dy = c[1].Y - c[0].Y;
        var n = Math.Sqrt(dx * dx + dy * dy);
        if (n < 1e-9) return Side;
        var (nx, ny) = (-dy / n, dx / n);

        if (Side is { } s && Math.Abs(s.X * nx + s.Y * ny) > Math.Cos(80 * Math.PI / 180)) return s;
        if (RowExit.HighSide(c) is { } alto) return alto;
        if (!_pontas.TryGetValue(ponta.Table, out var soma) || soma.N == 0) return null;

        // A normal (nx, ny) aponta da borda 0-1 para a 2-3 se o meio da 2-3 fica do lado dela.
        double Lado(double x, double y) => x * nx + y * ny;
        var meio = Lado(soma.X / soma.N, soma.Y / soma.N);
        var baixa = (Lado(c[0].X, c[0].Y) + Lado(c[1].X, c[1].Y)) / 2;
        var alta = (Lado(c[2].X, c[2].Y) + Lado(c[3].X, c[3].Y)) / 2;
        var paraAAlta = Math.Sign(alta - baixa);
        var sinal = Math.Abs(meio - baixa) <= Math.Abs(meio - alta) ? -paraAAlta : paraAAlta;
        return sinal == 0 ? null : new Point3(nx * sinal, ny * sinal, 0);
    }

    /// <summary>
    /// A coordenada ao longo da mesa (na direção do canto 0 para o 1 dela) do
    /// ponto de juntar da mesa para a ponta <paramref name="end"/> da fileira:
    /// a ponta de string mais adiantada naquela direção. Null se a mesa não
    /// está no padrão.
    /// </summary>
    public double? Gather(StringEndInput ponta, RowEnd end) =>
        _juntar.TryGetValue(ponta.Table, out var j) ? end == RowEnd.End ? j.Max : j.Min : null;

    /// <summary>O grupo de mesas (ligadas por strings) da mesa.</summary>
    public Guid Group(Guid mesa)
    {
        while (_pai.TryGetValue(mesa, out var pai) && pai != mesa) mesa = pai;
        return mesa;
    }

    private void Unir(Guid a, Guid b)
    {
        var ra = Group(a);
        var rb = Group(b);
        _pai.TryAdd(ra, ra);
        if (ra != rb) _pai[rb] = ra;
    }
}

/// <summary>Um equipamento da cadeia, com o nome para a tela e a ponta física (o retângulo em campo).</summary>
public sealed record ChainLink(CableEnd From, string FromName, CableEnd To, string ToName);

/// <summary>
/// Quem liga em quem, pela cadeia de vínculo (regra elétrica 1: o vínculo
/// manda, a posição é só representação). CA: cada inversor no trafo dele;
/// MT: cada trafo na subestação da UC a que está vinculado (21.2), nunca na
/// mais próxima. A subestação física é a UC unitária ou o bloco da compartilhada.
/// </summary>
public static class CableChain
{
    public static IReadOnlyList<ChainLink> AlternatingCurrent(ElectricalSetup setup) =>
        setup.Inverters
            .Select(i => new ChainLink(
                new CableEnd(CableEndKind.Inverter, i.Id), i.Name,
                new CableEnd(CableEndKind.Transformer, setup.FindTransformer(i.Transformer)?.Id ?? Guid.Empty),
                setup.FindTransformer(i.Transformer)?.Nickname ?? "-"))
            .ToList();

    public static IReadOnlyList<ChainLink> MediumVoltage(ElectricalSetup setup) =>
        setup.Transformers
            .Select(t =>
            {
                var (id, nome) = PhysicalSubstation(setup, t.ConsumerUnit);
                return new ChainLink(new CableEnd(CableEndKind.Transformer, t.Id), t.Nickname, new CableEnd(CableEndKind.Substation, id), nome);
            })
            .ToList();

    /// <summary>A subestação física de uma UC (o GUID do retângulo em campo) e o nome dela; vazio se a UC não existe.</summary>
    public static (Guid Id, string Name) PhysicalSubstation(ElectricalSetup setup, Guid unidade)
    {
        if (setup.FindUnit(unidade) is not { } uc) return (Guid.Empty, "-");
        if (uc.Mode == ConsumerUnitMode.Unitary) return (uc.Id, string.IsNullOrWhiteSpace(uc.Name) ? uc.Code : uc.Name);
        return setup.FindSubstation(uc.Substation) is { } bloco ? (bloco.Id, bloco.Name) : (Guid.Empty, uc.Code);
    }
}

/// <summary>Uma mesa do desenho para achar a fileira: o letreiro e os 4 cantos em planta.</summary>
public sealed record RowTable(Guid Id, string Label, IReadOnlyList<Point3> Corners);

/// <summary>A fileira de uma mesa (18.1): as mesas com o mesmo número de fileira no letreiro, paralelas e alinhadas com ela.</summary>
public static class CableRows
{
    /// <summary>Os cantos de todas as mesas da fileira da mesa <paramref name="table"/> (ela inclusive).</summary>
    public static IReadOnlyList<Point3> Corners(RowTable table, IEnumerable<RowTable> all)
    {
        if (!TableCells.TryParseLabel(table.Label, out var fileira, out _)) return table.Corners;

        var (ux, uy) = Direcao(table.Corners);
        var largura = Math.Abs(Lado(table.Corners[3], table.Corners[0], ux, uy));

        return all
            .Where(m => m.Id == table.Id || (TableCells.TryParseLabel(m.Label, out var f, out _) && f == fileira && Alinhada(m)))
            .SelectMany(m => m.Corners.Take(4))
            .ToList();

        bool Alinhada(RowTable m)
        {
            var (vx, vy) = Direcao(m.Corners);
            if (Math.Abs(ux * vy - uy * vx) > Math.Sin(Math.PI / 180)) return false;
            var meio = new Point3((m.Corners[0].X + m.Corners[2].X) / 2, (m.Corners[0].Y + m.Corners[2].Y) / 2, 0);
            var centro = new Point3((table.Corners[0].X + table.Corners[2].X) / 2, (table.Corners[0].Y + table.Corners[2].Y) / 2, 0);
            return Math.Abs(Lado(meio, centro, ux, uy)) <= largura / 2 + 1e-6;
        }
    }

    private static (double X, double Y) Direcao(IReadOnlyList<Point3> c)
    {
        var dx = c[1].X - c[0].X;
        var dy = c[1].Y - c[0].Y;
        var n = Math.Sqrt(dx * dx + dy * dy);
        return n < 1e-9 ? (1, 0) : (dx / n, dy / n);
    }

    private static double Lado(Point3 p, Point3 a, double ux, double uy) => -(p.X - a.X) * uy + (p.Y - a.Y) * ux;
}
