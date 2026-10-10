using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Uma ponta de string para o roteamento CC: o ponto do módulo, os cantos da mesa dele e os de todas as mesas da fileira.</summary>
public sealed record StringEndInput(Point3 Point, Guid Table, IReadOnlyList<Point3> TableCorners, IReadOnlyList<Point3> RowCorners);

/// <summary>Uma string a rotear (CC ou o trecho string -> combiner): as duas pontas e o destino (o ponto dele em campo, null se não está em campo).</summary>
public sealed record StringRouteInput(Guid String, string Tag, StringEndInput Positive, StringEndInput Negative, CableEnd Destination, string DestinationName, Point3? DestinationPoint, RowEnd? ForcedEnd);

/// <summary>Um trecho entre dois equipamentos (CA, MT, combiner -> inversor): os pontos em campo (null = não está em campo).</summary>
public sealed record EquipmentRouteInput(CableEnd From, string FromName, Point3? FromPoint, CableEnd To, string ToName, Point3? ToPoint);

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
    public static RouteResult Strings(
        IReadOnlyList<StringRouteInput> strings, CableRoute rota, TrenchNetwork valas, RouteSettings config, Func<double, double, double?> chao)
    {
        var lances = new List<PlannedRun>();
        var falhas = new List<RouteFailure>();

        // As árvores de caminho a partir das valas de cada destino: uma vez por destino, não por string.
        var arvores = new Dictionary<CableEnd, IReadOnlyList<TrenchNetwork.TrenchTree>>();

        foreach (var s in strings)
        {
            var nome = Tr.F("string {0}", string.IsNullOrWhiteSpace(s.Tag) ? s.String.ToString("D")[..8] : s.Tag);
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

            // As valas que chegam no destino: todas dentro do raio (a mais perto pode estar solta).
            if (!arvores.TryGetValue(s.Destination, out var chegadas))
                arvores[s.Destination] = chegadas = valas.Within(destino, config.Radius).Select(valas.Tree).ToList();

            if (chegadas.Count == 0)
            {
                falhas.Add(new RouteFailure(nome, Tr.F("{0} sem vala no raio de {1:0.#} m", s.DestinationName, config.Radius), [s.Destination], mesas));
                continue;
            }

            (PlannedRun Pos, PlannedRun Neg)? melhor = null;
            string? porque = null;

            foreach (var ponta in s.ForcedEnd is { } forcada ? new[] { forcada } : new[] { RowEnd.Start, RowEnd.End })
            {
                var pos = Lance(s.Positive, ponta, CablePolarity.Positive, out var p1);
                var neg = Lance(s.Negative, ponta, CablePolarity.Negative, out var p2);
                if (pos is null || neg is null)
                {
                    porque ??= p1 ?? p2;
                    continue;
                }

                if (melhor is null || pos.Length + neg.Length < melhor.Value.Pos.Length + melhor.Value.Neg.Length) melhor = (pos, neg);
            }

            if (melhor is { } m)
            {
                lances.Add(m.Pos);
                lances.Add(m.Neg);
            }
            else
            {
                falhas.Add(new RouteFailure(nome, porque ?? Tr.T("sem caminho"), [new CableEnd(CableEndKind.String, s.String)], mesas));
            }

            PlannedRun? Lance(StringEndInput ponta, RowEnd lado, CablePolarity polaridade, out string? problema)
            {
                problema = null;
                (IReadOnlyList<Point3> Points, Point3 Direction) saida;
                try
                {
                    saida = RowExit.Plan(ponta.Point, ponta.TableCorners, ponta.RowCorners, lado);
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

                // A chegada com o menor percurso (pela vala até ela, mais dela ao destino).
                var arvore = chegadas.MinBy(a => a.Length(batida) + Plano(a.From.At, destino));
                if (arvore is null || double.IsInfinity(arvore.Length(batida)) || arvore.Path(batida) is not { } deChegada)
                {
                    problema = Tr.F("a vala do fim da fileira não se liga às valas de {0}", s.DestinationName);
                    return null;
                }

                var pelaVala = Enumerable.Reverse(deChegada).ToList();
                var percurso = CablePath.Build(ponta.Point, [.. saida.Points, batida.At], pelaVala, [], destino, config.Depth, chao, out var fora);
                if (percurso is null)
                {
                    problema = Tr.F("trecho fora do terreno em ({0:0.##}; {1:0.##})", fora!.Value.X, fora.Value.Y);
                    return null;
                }

                var run = new CableRun(Guid.NewGuid(), rota, polaridade, new CableEnd(CableEndKind.String, s.String), s.Destination);
                return new PlannedRun(run, percurso);
            }
        }

        return new RouteResult(lances, falhas);
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

            var saidas = valas.Within(de, config.Radius);
            var chegadas = valas.Within(para, config.Radius);
            if (saidas.Count == 0 || chegadas.Count == 0)
            {
                var sem = new List<CableEnd>();
                if (saidas.Count == 0) sem.Add(t.From);
                if (chegadas.Count == 0) sem.Add(t.To);
                var quem = string.Join(Tr.T(" e "), sem.Select(e => e == t.From ? t.FromName : t.ToName));
                falhas.Add(new RouteFailure(nome, Tr.F("{0} sem vala no raio de {1:0.#} m", quem, config.Radius), sem, []));
                continue;
            }

            // O par (saída, chegada) de menor total que se liga.
            (TrenchPoint Saida, IReadOnlyList<Point3> Caminho, double Total)? melhor = null;
            foreach (var s in saidas)
            {
                var arvore = valas.Tree(s);
                foreach (var c in chegadas)
                {
                    var total = Plano(de, s.At) + arvore.Length(c) + Plano(c.At, para);
                    if (double.IsInfinity(total) || (melhor is { } m && m.Total <= total)) continue;
                    if (arvore.Path(c) is { } caminho) melhor = (s, caminho, total);
                }
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

    private static double Plano(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
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
