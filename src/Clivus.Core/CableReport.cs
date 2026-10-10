namespace Clivus.Core;

/// <summary>
/// Uma tabela do memorial (23.4, 23.6, 24.1, 24.2): o cabeçalho, as linhas e
/// o rodapé de totais. As células são número (double) ou texto; vazio (null)
/// onde falta dado. As notas dizem o que faltou para preencher.
/// </summary>
public sealed record CableTable(string Title, IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyList<object?> Total, IReadOnlyList<string> Notes)
{
    /// <summary>O texto para CSV (separador ";", vírgula decimal da cultura), cabeçalho, linhas e total.</summary>
    public string ToCsv(IFormatProvider cultura)
    {
        string Celula(object? c) => c switch
        {
            null => string.Empty,
            double d => double.IsFinite(d) ? d.ToString("0.###", cultura) : string.Empty,
            _ => Aspas(Convert.ToString(c, cultura) ?? string.Empty),
        };

        static string Aspas(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

        var linhas = new List<string> { string.Join(";", Header.Select(Aspas)) };
        linhas.AddRange(Rows.Select(r => string.Join(";", r.Select(Celula))));
        if (Total.Count > 0) linhas.Add(string.Join(";", Total.Select(Celula)));
        return string.Join("\r\n", linhas) + "\r\n";
    }
}

/// <summary>
/// Uma string para a tabela do CC: tag, módulos em série e os comprimentos
/// medidos (null = lance que não existe); o módulo dela, quando o desenho tem
/// mais de um PAN (null = o PAN geral da tabela).
/// </summary>
public sealed record DcStringRun(Guid String, string Tag, int Series, double? Positive, double? Negative, PanModule? Module = null);

/// <summary>Um alimentador CC combiner -> inversor: o comprimento do trecho (o par + e − vai junto), quantas strings e a série delas.</summary>
public sealed record DcFeederRun(string From, string To, double Length, int Strings, int Series, PanModule? Module);

/// <summary>Um lance entre equipamentos para a tabela de CA/MT: o comprimento e a corrente e a tensão de referência (null quando falta dado).</summary>
public sealed record EquipmentRun(string From, string To, double Length, double? Current, double? Voltage, string? Missing);

/// <summary>
/// As tabelas do memorial de cálculo (etapa 23). Só mostra os números lado a
/// lado: corrente calculada e admissível, queda em V e em %. Nenhuma
/// coluna de "aprovado" (regra 8).
/// </summary>
public static class CableReport
{
    /// <summary>A tabela do CC (23.4): uma linha por string, totais no rodapé.</summary>
    public static CableTable Dc(string titulo, IEnumerable<DcStringRun> strings, Cable? cabo, PanModule? pan, double tMin, double tMax)
    {
        var notas = new List<string>();
        if (cabo is null) notas.Add(Tr.T("Nenhum cabo escolhido nesta aba: a queda de tensão fica em branco."));
        var lista = strings.ToList();
        if (pan is null && lista.Any(s => s.Module is null)) notas.Add(Tr.T("Nenhum módulo com arquivo PAN neste desenho: tensões, correntes e quedas ficam em branco."));

        var linhas = new List<IReadOnlyList<object?>>();
        double somaPos = 0, somaNeg = 0;

        foreach (var s in lista.OrderBy(s => s.Tag, StringComparer.CurrentCulture))
        {
            somaPos += s.Positive ?? 0;
            somaNeg += s.Negative ?? 0;
            double? total = s.Positive is { } p && s.Negative is { } n ? p + n : null;

            double? voc = null, vop = null, imp = null, isc = null, queda = null, pct = null;
            if ((s.Module ?? pan) is { } modulo)
            {
                voc = CableCalc.OpenCircuitAtMin(modulo, s.Series, tMin);
                vop = CableCalc.OperatingAtMax(modulo, s.Series, tMax);
                imp = modulo.Imp;
                isc = modulo.Isc;
                if (cabo is not null && s.Positive is { } lp && s.Negative is { } ln)
                {
                    queda = CableCalc.DcDrop(modulo.Imp, cabo, lp, ln);
                    pct = CableCalc.Percent(queda.Value, vop.Value);
                }
            }

            linhas.Add([s.Tag, (double)s.Series, s.Positive, s.Negative, total, cabo?.Name, voc, vop, imp, isc, queda, pct]);
        }

        if (linhas.Any(l => l[2] is null || l[3] is null))
            notas.Add(Tr.T("Linha sem o lance + ou − : o cabo não foi gerado ou foi apagado à mão (a string foi pintada)."));

        return new CableTable(titulo,
            [Tr.T("String"), Tr.T("Módulos em série"), Tr.T("Lance + (m)"), Tr.T("Lance − (m)"), Tr.T("Total (m)"), Tr.T("Cabo"),
             Tr.T("Voc na mínima (V)"), Tr.T("Tensão de operação na máxima (V)"), Tr.T("Corrente de MPPT, Imp (A)"), Tr.T("Corrente de curto, Isc (A)"),
             Tr.T("Queda (V)"), Tr.T("Queda (%)")],
            linhas,
            [Tr.T("Total"), null, somaPos, somaNeg, somaPos + somaNeg, null, null, null, null, null, null, null],
            notas);
    }

    /// <summary>
    /// A tabela de CA ou MT (23.6): uma linha por trecho, com a corrente
    /// calculada e a admissível do método lado a lado, e a queda.
    /// </summary>
    public static CableTable Equipment(string titulo, IEnumerable<EquipmentRun> trechos, Cable? cabo, string metodo, double fatorDePotencia, bool trifasico = true)
    {
        var notas = new List<string>();
        if (cabo is null) notas.Add(Tr.T("Nenhum cabo escolhido nesta aba: a queda de tensão e a corrente admissível ficam em branco."));

        var admissivel = cabo?.AmpacityFor(metodo);
        if (cabo is not null && admissivel is null) notas.Add(Tr.F("O cabo {0} não tem corrente admissível para o método {1} na biblioteca.", cabo.Name, metodo));

        var linhas = new List<IReadOnlyList<object?>>();
        var soma = 0.0;

        foreach (var t in trechos.OrderBy(t => t.From, StringComparer.CurrentCulture).ThenBy(t => t.To, StringComparer.CurrentCulture))
        {
            soma += t.Length;
            double? queda = null, pct = null;
            if (cabo is not null && t.Current is { } i && t.Voltage is { } v)
            {
                queda = trifasico ? CableCalc.ThreePhaseDrop(i, cabo, t.Length, fatorDePotencia) : CableCalc.SinglePhaseDrop(i, cabo, t.Length, fatorDePotencia);
                pct = CableCalc.Percent(queda.Value, v);
            }

            if (t.Missing is not null) notas.Add(Tr.F("{0} → {1}: {2}", t.From, t.To, t.Missing));
            linhas.Add([t.From, t.To, t.Length, cabo?.Name, t.Current, admissivel, queda, pct]);
        }

        return new CableTable(titulo,
            [Tr.T("Origem"), Tr.T("Destino"), Tr.T("Comprimento (m)"), Tr.T("Cabo"), Tr.T("Corrente calculada (A)"),
             Tr.F("Corrente admissível, método {0} (A)", metodo), Tr.T("Queda (V)"), Tr.T("Queda (%)")],
            linhas,
            [Tr.T("Total"), null, soma, null, null, null, null, null],
            notas);
    }

    /// <summary>
    /// Os alimentadores CC combiner -> inversor (19.3): a corrente é a soma
    /// das strings (n × Imp), a tensão a de operação na máxima, e a queda vai
    /// e volta (2 × o comprimento do trecho).
    /// </summary>
    public static CableTable Feeders(string titulo, IEnumerable<DcFeederRun> trechos, Cable? cabo, double tMax)
    {
        var notas = new List<string>();
        if (cabo is null) notas.Add(Tr.T("Nenhum cabo escolhido nesta aba: a queda de tensão fica em branco."));

        var linhas = new List<IReadOnlyList<object?>>();
        var soma = 0.0;

        foreach (var t in trechos.OrderBy(t => t.From, StringComparer.CurrentCulture))
        {
            soma += t.Length;
            double? corrente = null, vop = null, queda = null, pct = null;
            if (t.Module is { } m)
            {
                corrente = t.Strings * m.Imp;
                vop = CableCalc.OperatingAtMax(m, t.Series, tMax);
                if (cabo is not null)
                {
                    queda = CableCalc.DcDrop(corrente.Value, cabo, t.Length, t.Length);
                    pct = CableCalc.Percent(queda.Value, vop.Value);
                }
            }
            else
            {
                notas.Add(Tr.F("{0}: sem módulo com PAN, a corrente e a queda ficam em branco.", t.From));
            }

            linhas.Add([t.From, t.To, t.Length, cabo?.Name, (double)t.Strings, corrente, vop, queda, pct]);
        }

        return new CableTable(titulo,
            [Tr.T("Combiner"), Tr.T("Inversor"), Tr.T("Comprimento do trecho (m)"), Tr.T("Cabo"), Tr.T("Strings"), Tr.T("Corrente (A)"),
             Tr.T("Tensão de operação na máxima (V)"), Tr.T("Queda, ida e volta (V)"), Tr.T("Queda (%)")],
            linhas,
            [Tr.T("Total"), null, soma, null, null, null, null, null, null],
            notas);
    }

    /// <summary>
    /// Um lance medido, para o resumo e a lista de material (24.1, 24.2): o
    /// comprimento é o do traçado; <paramref name="Wires"/> cabos iguais correm nele.
    /// </summary>
    public sealed record MeasuredRun(CableRoute Route, CablePolarity Polarity, string Cable, double Length, int Wires = 1)
    {
        /// <summary>Os metros de cabo: o traçado vezes as vias.</summary>
        public double CableLength => Length * Wires;
    }

    /// <summary>
    /// O resumo de cabos da usina (24.1): metros por rota, por cabo e, no CC,
    /// por polaridade, com o total geral (os metros de cabo já com as vias).
    /// </summary>
    public static CableTable Summary(IEnumerable<MeasuredRun> lances)
    {
        var lista = lances.ToList();
        var linhas = lista
            .GroupBy(l => (l.Route, l.Cable, l.Polarity))
            .OrderBy(g => g.Key.Route).ThenBy(g => g.Key.Cable, StringComparer.CurrentCulture).ThenBy(g => g.Key.Polarity)
            .Select(g => (IReadOnlyList<object?>)[CableRoutes.Title(g.Key.Route), Polaridade(g.Key.Polarity), g.Key.Cable, (double)g.Count(), g.Sum(l => l.CableLength)])
            .ToList();

        return new CableTable(Tr.T("Resumo de cabos"),
            [Tr.T("Rota"), Tr.T("Polaridade"), Tr.T("Cabo"), Tr.T("Lances"), Tr.T("Comprimento (m)")],
            linhas,
            [Tr.T("Total"), null, null, (double)lista.Count, lista.Sum(l => l.CableLength)],
            []);
    }

    /// <summary>
    /// A lista de material (24.2): cabo tal, tantos metros (o traçado vezes
    /// as vias). A folga é opcional e é do usuário (em %), nunca um número
    /// fixo do sistema.
    /// </summary>
    public static CableTable Material(IEnumerable<MeasuredRun> lances, double folgaPercentual)
    {
        var lista = lances.ToList();
        var fator = 1 + Math.Max(0, folgaPercentual) / 100;
        var linhas = lista
            .GroupBy(l => l.Cable)
            .OrderBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => (IReadOnlyList<object?>)[g.Key, g.Sum(l => l.CableLength), g.Sum(l => l.CableLength) * fator])
            .ToList();

        return new CableTable(Tr.T("Lista de material de cabo"),
            [Tr.T("Cabo"), Tr.T("Medido (m)"), Tr.F("Com folga de {0:0.#}% (m)", folgaPercentual)],
            linhas,
            [Tr.T("Total"), lista.Sum(l => l.CableLength), lista.Sum(l => l.CableLength) * fator],
            []);
    }

    /// <summary>
    /// Um circuito do resumo por tipo de cabo (Renan, 10/10/2026: "cada
    /// circuito seja uma linha ... e que mostre quantos cabos tem por
    /// circuito"): a rota, as duas pontas (com o nome para a tela), quantos
    /// lances desenhados (2 no CC: + e −), a soma dos comprimentos deles, as
    /// vias (cabos iguais por lance), o cabo e o método da aba.
    /// </summary>
    public sealed record CircuitRun(
        CableRoute Route, CableEnd From, CableEnd To, string FromName, string ToName, int Runs, double Length, int Wires, Cable? Cable, string Method)
    {
        /// <summary>Quantos cabos o circuito tem: os lances vezes as vias.</summary>
        public int Cables => Runs * Wires;

        /// <summary>Os metros de cabo do circuito: a soma dos lances vezes as vias.</summary>
        public double CableLength => Length * Wires;
    }

    /// <summary>
    /// O resumo de um tipo de cabo (CC, CA ou MT): uma linha por circuito com
    /// De → Para, a especificação do cabo (formação, seção, condutor,
    /// isolação), o método de instalação, os lances, o comprimento, as vias,
    /// os cabos e os metros de cabo; os totais no rodapé.
    /// </summary>
    public static CableTable Circuits(string titulo, IEnumerable<CircuitRun> circuitos)
    {
        var lista = circuitos
            .OrderBy(c => c.Route)
            .ThenBy(c => c.FromName, NaturalStringComparer.Instance)
            .ThenBy(c => c.ToName, NaturalStringComparer.Instance)
            .ToList();
        var notas = new List<string>();
        if (lista.Any(c => c.Cable is null)) notas.Add(Tr.T("Circuito sem cabo escolhido na aba da rota: a especificação fica em branco."));

        var linhas = lista.Select(c => (IReadOnlyList<object?>)
        [
            DePara(c.FromName, c.ToName), c.Cable?.Name, c.Cable?.Formation, c.Cable?.SectionMm2, c.Cable?.Conductor, Isolacao(c.Cable), c.Method,
            (double)c.Runs, c.Length, (double)c.Wires, (double)c.Cables, c.CableLength,
        ]).ToList();

        return new CableTable(titulo,
            [Tr.T("De → Para"), Tr.T("Cabo"), Tr.T("Formação"), Tr.T("Seção (mm²)"), Tr.T("Condutor"), Tr.T("Isolação"), Tr.T("Método"),
             Tr.T("Lances"), Tr.T("Comprimento (m)"), Tr.T("Vias"), Tr.T("Cabos"), Tr.T("Total de cabo (m)")],
            linhas,
            [Tr.F("Total ({0})", lista.Count), null, null, null, null, null, null,
             (double)lista.Sum(c => c.Runs), lista.Sum(c => c.Length), null, (double)lista.Sum(c => c.Cables), lista.Sum(c => c.CableLength)],
            notas);
    }

    /// <summary>
    /// Um grupo do resumo (Renan, 10/10/2026, item 12): UC, trafo ou inversor,
    /// com os grupos de baixo e os circuitos do último nível. Os subtotais são
    /// somados dos circuitos na hora (as vias trocadas mudam os totais).
    /// </summary>
    public sealed class CircuitGroup
    {
        internal CircuitGroup(string name, int level, string key)
        {
            Name = name;
            Level = level;
            Key = key;
        }

        /// <summary>O nome na tela: "UC1", "T1", "Inversor 1".</summary>
        public string Name { get; }

        /// <summary>0 = o de cima (UC).</summary>
        public int Level { get; }

        /// <summary>O caminho do grupo (os nomes de cima até ele), único na tabela: para lembrar o que está aberto.</summary>
        public string Key { get; }

        public List<CircuitGroup> Children { get; } = [];

        public List<CircuitRun> Circuits { get; } = [];

        /// <summary>Todos os circuitos abaixo dele.</summary>
        public IEnumerable<CircuitRun> AllCircuits => Circuits.Concat(Children.SelectMany(g => g.AllCircuits));

        /// <summary>Todos os grupos abaixo dele (ele não).</summary>
        public IEnumerable<CircuitGroup> Descendants => Children.SelectMany(g => g.Descendants.Prepend(g));

        public int CircuitCount => AllCircuits.Count();

        public int Runs => AllCircuits.Sum(c => c.Runs);

        public double Length => AllCircuits.Sum(c => c.Length);

        /// <summary>Os cabos do grupo (lances vezes vias).</summary>
        public int Cables => AllCircuits.Sum(c => c.Cables);

        /// <summary>Os metros de cabo do grupo (com as vias).</summary>
        public double CableLength => AllCircuits.Sum(c => c.CableLength);
    }

    /// <summary>"(sem UC)", "(sem trafo)"... para o elo que falta na cadeia.</summary>
    private static string Sem(string oQue) => Tr.F("(sem {0})", oQue);

    /// <summary>
    /// Onde o circuito entra na hierarquia do resumo (item 12), de cima para
    /// baixo, pela cadeia de vínculo: no CC (string -> inversor, string ->
    /// combiner e combiner -> inversor) UC, trafo e inversor; no CA (inversor
    /// -> trafo) UC e trafo; na MT (trafo -> subestação) a UC. O elo que falta
    /// vira "(sem trafo)", "(sem UC)"...
    /// </summary>
    public static IReadOnlyList<string> GroupPath(ElectricalSetup setup, CircuitRun c)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(c);

        Guid trafo;
        switch (c.Route)
        {
            case CableRoute.MediumVoltage:
                return [Uc(setup.FindTransformer(c.From.Id)?.ConsumerUnit ?? Guid.Empty)];

            case CableRoute.AlternatingCurrent:
                trafo = c.To.Kind == CableEndKind.Transformer ? c.To.Id : setup.FindInverter(c.From.Id)?.Transformer ?? Guid.Empty;
                return [Uc(setup.FindTransformer(trafo)?.ConsumerUnit ?? Guid.Empty), Trafo(trafo)];

            default:
                var inversor = c.To.Kind switch
                {
                    CableEndKind.Inverter => c.To.Id,
                    CableEndKind.Combiner => setup.FindCombiner(c.To.Id)?.Inverter ?? Guid.Empty,
                    _ => Guid.Empty,
                };
                trafo = setup.FindInverter(inversor)?.Transformer ?? Guid.Empty;
                return [Uc(setup.FindTransformer(trafo)?.ConsumerUnit ?? Guid.Empty), Trafo(trafo), setup.FindInverter(inversor)?.Name ?? Sem(Tr.T("inversor"))];
        }

        string Trafo(Guid id) => setup.FindTransformer(id)?.Nickname ?? Sem(Tr.T("trafo"));

        // A UC pelo código (U1, UC1): é a tag dela; o nome é livre ("Subestação U1").
        string Uc(Guid id) => setup.FindUnit(id)?.Code ?? Sem(Tr.T("UC"));
    }

    /// <summary>
    /// Agrupa os circuitos pela hierarquia (<paramref name="path"/>: os nomes
    /// de cima para baixo), em ordem natural em cada nível; os circuitos de
    /// cada grupo de baixo em ordem natural de De → Para. Uma UC com dois ou
    /// mais trafos soma os trafos (o subtotal é dos circuitos abaixo).
    /// </summary>
    public static IReadOnlyList<CircuitGroup> Group(IEnumerable<CircuitRun> circuitos, Func<CircuitRun, IReadOnlyList<string>> path)
    {
        ArgumentNullException.ThrowIfNull(circuitos);
        ArgumentNullException.ThrowIfNull(path);

        var raiz = new CircuitGroup(string.Empty, -1, string.Empty);
        foreach (var c in circuitos)
        {
            var grupo = raiz;
            foreach (var nome in path(c))
            {
                var chave = grupo.Key + "/" + nome;
                var filho = grupo.Children.FirstOrDefault(g => g.Key == chave);
                if (filho is null) grupo.Children.Add(filho = new CircuitGroup(nome, grupo.Level + 1, chave));
                grupo = filho;
            }

            grupo.Circuits.Add(c);
        }

        void Ordenar(CircuitGroup g)
        {
            g.Children.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
            g.Circuits.Sort((a, b) =>
            {
                var r = a.Route.CompareTo(b.Route);
                if (r == 0) r = NaturalStringComparer.Instance.Compare(a.FromName, b.FromName);
                return r != 0 ? r : NaturalStringComparer.Instance.Compare(a.ToName, b.ToName);
            });
            foreach (var f in g.Children) Ordenar(f);
        }

        Ordenar(raiz);
        return raiz.Children;
    }

    /// <summary>
    /// O resumo agrupado de um tipo de cabo (itens 12 e 16), como vai para o
    /// Excel: uma linha por grupo (o nome recuado pelo nível, com o subtotal
    /// de lances, comprimento, cabos e metros de cabo) e, embaixo de cada
    /// grupo do último nível, os circuitos dele; o total da usina no rodapé.
    /// No CC (<paramref name="dc"/>), as colunas da tag da string e as contas
    /// por string (<paramref name="checks"/>, pela ponta string do circuito).
    /// </summary>
    public static CableTable GroupedCircuits(string titulo, IReadOnlyList<CircuitGroup> grupos, bool dc, bool routeColumn,
        IReadOnlyDictionary<CableEnd, StringCheck>? checks = null, IEnumerable<string>? notes = null)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        var vaziasDoCalculo = Enumerable.Repeat<object?>(null, StringCheck.Headers().Count).ToList();
        var cabecalho = new List<string> { Tr.T("Agrupamento / circuito") };
        if (dc) cabecalho.Add(Tr.T("Tag da string"));
        if (routeColumn) cabecalho.Add(Tr.T("Rota"));
        cabecalho.AddRange([Tr.T("Cabo"), Tr.T("Formação"), Tr.T("Seção (mm²)"), Tr.T("Condutor"), Tr.T("Isolação"), Tr.T("Método"),
            Tr.T("Lances"), Tr.T("Comprimento (m)"), Tr.T("Vias"), Tr.T("Cabos"), Tr.T("Total de cabo (m)")]);
        if (dc) cabecalho.AddRange(StringCheck.Headers());

        var linhas = new List<IReadOnlyList<object?>>();

        void Grupo(CircuitGroup g)
        {
            var linha = new List<object?> { new string(' ', g.Level * 4) + g.Name };
            if (dc) linha.Add(null);
            if (routeColumn) linha.Add(null);
            linha.AddRange([null, null, null, null, null, null, (double)g.Runs, g.Length, null, (double)g.Cables, g.CableLength]);
            if (dc) linha.AddRange(vaziasDoCalculo);
            linhas.Add(linha);

            foreach (var f in g.Children) Grupo(f);
            foreach (var c in g.Circuits)
            {
                var l = new List<object?> { new string(' ', (g.Level + 1) * 4) + DePara(c.FromName, c.ToName) };
                if (dc) l.Add(c.From.Kind == CableEndKind.String ? c.FromName : null);
                if (routeColumn) l.Add(CableRoutes.Title(c.Route));
                l.AddRange([c.Cable?.Name, c.Cable?.Formation, c.Cable?.SectionMm2, c.Cable?.Conductor, Isolacao(c.Cable), c.Method,
                    (double)c.Runs, c.Length, (double)c.Wires, (double)c.Cables, c.CableLength]);
                if (dc) l.AddRange(checks is not null && checks.TryGetValue(c.From, out var k) ? k.Cells() : vaziasDoCalculo);
                linhas.Add(l);
            }
        }

        foreach (var g in grupos) Grupo(g);

        var todos = grupos.SelectMany(g => g.AllCircuits).ToList();
        var total = new List<object?> { Tr.F("Total da usina ({0} circuito(s))", todos.Count) };
        if (dc) total.Add(null);
        if (routeColumn) total.Add(null);
        total.AddRange([null, null, null, null, null, null, (double)todos.Sum(c => c.Runs), todos.Sum(c => c.Length), null, (double)todos.Sum(c => c.Cables), todos.Sum(c => c.CableLength)]);
        if (dc) total.AddRange(vaziasDoCalculo);

        var notas = new List<string>();
        if (todos.Any(c => c.Cable is null)) notas.Add(Tr.T("Circuito sem cabo escolhido na aba da rota: a especificação fica em branco."));
        if (notes is not null) notas.AddRange(notes);

        return new CableTable(titulo, cabecalho, linhas, total, notas);
    }

    /// <summary>
    /// Os itens cujas pontas existem em campo (item 18, erro grave de
    /// 10/10/2026: o inversor apagado e o resumo ainda mostrando os cabos
    /// dele): o resumo reflete o desenho de agora. Cabo cuja ponta (string,
    /// combiner, inversor, trafo ou subestação) não está mais em campo não
    /// entra; as pontas que faltam voltam para o aviso.
    /// </summary>
    public static (List<T> Kept, List<CableEnd> Missing) InField<T>(IEnumerable<T> itens, Func<T, CableEnd> from, Func<T, CableEnd> to, Func<CableEnd, bool> emCampo)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(emCampo);

        var ficam = new List<T>();
        var faltam = new List<CableEnd>();
        foreach (var i in itens)
        {
            var ok = true;
            foreach (var ponta in new[] { from(i), to(i) })
            {
                if (emCampo(ponta)) continue;
                ok = false;
                if (!faltam.Contains(ponta)) faltam.Add(ponta);
            }

            if (ok) ficam.Add(i);
        }

        return (ficam, faltam);
    }

    /// <summary>
    /// O aviso das pontas fora de campo (item 18): sem nada que fique,
    /// "Inversor não está em campo: não é possível mostrar o resumo"; com
    /// parte, quem sumiu e que os cabos dele ficaram de fora. Null se nada falta.
    /// </summary>
    public static string? MissingMessage(IReadOnlyCollection<CableEnd> faltam, Func<CableEnd, string> nome, bool nadaFica)
    {
        ArgumentNullException.ThrowIfNull(faltam);
        ArgumentNullException.ThrowIfNull(nome);
        if (faltam.Count == 0) return null;

        var nomes = string.Join(", ", faltam.Select(nome).Distinct().OrderBy(n => n, NaturalStringComparer.Instance));
        var tipos = faltam.Select(f => f.Kind).Distinct().ToList();
        var quem = tipos.Count == 1 ? KindName(tipos[0]) : Tr.T("Equipamento");

        return nadaFica
            ? Tr.F("{0} não está em campo: não é possível mostrar o resumo ({1}).", quem, nomes)
            : Tr.F("Fora de campo: {0}. Os cabos ligados a eles ficaram fora do resumo: gere a rota de novo ou apague esses cabos.", nomes);
    }

    /// <summary>O nome do tipo de ponta, para o aviso.</summary>
    public static string KindName(CableEndKind k) => k switch
    {
        CableEndKind.String => Tr.T("String"),
        CableEndKind.Combiner => Tr.T("Combiner box"),
        CableEndKind.Inverter => Tr.T("Inversor"),
        CableEndKind.Transformer => Tr.T("Transformador"),
        _ => Tr.T("Subestação"),
    };

    /// <summary>"T1 → UC1".</summary>
    public static string DePara(string de, string para) => Tr.F("{0} → {1}", de, para);

    /// <summary>A isolação para a tela: o material e a tensão ("EPR 8,7/15 kV").</summary>
    public static string? Isolacao(Cable? c) =>
        c is null ? null : string.Join(" ", new[] { c.InsulationMaterial, c.Insulation }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string Polaridade(CablePolarity p) => p switch
    {
        CablePolarity.Positive => "+",
        CablePolarity.Negative => "−",
        _ => string.Empty,
    };
}
