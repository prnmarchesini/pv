using System.Globalization;

namespace Clivus.Core;

/// <summary>A sombra guardada de um módulo: o bloco (handle), a mesa, a fração, a causa, quando e o período do cálculo.</summary>
/// <param name="Handle">O handle do bloco do módulo.</param>
/// <param name="Table">O GUID da mesa dele (vazio: não se sabe, registro antigo).</param>
/// <param name="Fraction">A maior fração da face na sombra no período (0 a 1).</param>
/// <param name="Cause">O que fez a sombra nesse pior instante.</param>
/// <param name="When">O pior instante, ou null.</param>
/// <param name="Period">O período do cálculo, como a janela descreve.</param>
public sealed record ModuleShadow(string Handle, Guid Table, double Fraction, ShadowCause Cause, DateTime? When, string Period);

/// <summary>
/// O registro dos motivos da sombra (SOMBRA_MOTIVOS), por módulo (42.1):
/// serve ao "Por que essa sombra?" e à janela de análises. Cada cálculo num
/// escopo troca só as mesas dele: as linhas das mesas de fora ficam (as
/// áreas são calculadas uma a uma e convivem no desenho).
/// <para>
/// Formato V2, uma linha por item: <c>V2</c>; <c>T|mesa|período</c> para
/// cada mesa calculada (com ou sem sombra); <c>M|handle|mesa|fração|causa|quando|período</c>
/// para cada módulo com sombra. O V1 (até 30/10/2026: um cálculo só, do
/// desenho inteiro) é lido: <c>V1</c>, o período, e <c>handle|fração|causa|quando</c>;
/// a mesa de cada módulo sai de quem lê (o desenho), e a primeira troca ou
/// apagamento grava em V2.
/// </para>
/// </summary>
public sealed class ShadowReasons
{
    private const string Versao = "V2";

    private readonly Dictionary<Guid, string> _mesas;
    private readonly Dictionary<string, ModuleShadow> _modulos;

    private ShadowReasons(Dictionary<Guid, string> mesas, Dictionary<string, ModuleShadow> modulos, string? legado)
    {
        _mesas = mesas;
        _modulos = modulos;
        LegacyPeriod = legado;
    }

    /// <summary>O registro vazio.</summary>
    public static ShadowReasons Empty => new([], new Dictionary<string, ModuleShadow>(StringComparer.OrdinalIgnoreCase), null);

    /// <summary>As mesas calculadas e o período de cada uma.</summary>
    public IReadOnlyDictionary<Guid, string> Tables => _mesas;

    /// <summary>Os módulos com sombra.</summary>
    public IReadOnlyCollection<ModuleShadow> Modules => _modulos.Values;

    /// <summary>O período de um registro V1 (o desenho inteiro de uma vez), ou null.</summary>
    public string? LegacyPeriod { get; }

    /// <summary>Se não há nada guardado.</summary>
    public bool IsEmpty => _mesas.Count == 0 && _modulos.Count == 0 && LegacyPeriod is null;

    /// <summary>Lê o registro (V1 ou V2); o que não se entende é ignorado.</summary>
    /// <param name="lines">As linhas do registro, na ordem.</param>
    /// <param name="tableOfHandle">A mesa de um módulo pelo handle (só para o V1, que não guardava a mesa).</param>
    public static ShadowReasons Parse(IReadOnlyList<string> lines, Func<string, Guid?>? tableOfHandle = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var mesas = new Dictionary<Guid, string>();
        var modulos = new Dictionary<string, ModuleShadow>(StringComparer.OrdinalIgnoreCase);
        if (lines.Count == 0) return new ShadowReasons(mesas, modulos, null);

        if (lines[0] == "V1")
        {
            if (lines.Count < 2) return new ShadowReasons(mesas, modulos, null);

            var periodo = lines[1];
            foreach (var linha in lines.Skip(2))
            {
                var c = linha.Split('|');
                if (c.Length < 4 || c[0].Length == 0) continue;
                if (!double.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) continue;

                var causa = Enum.TryParse<ShadowCause>(c[2], out var k) ? k : ShadowCause.None;
                modulos[c[0]] = new ModuleShadow(c[0], tableOfHandle?.Invoke(c[0]) ?? Guid.Empty, f, causa, Quando(c[3]), periodo);
            }

            return new ShadowReasons(mesas, modulos, periodo);
        }

        if (lines[0] != Versao) return new ShadowReasons(mesas, modulos, null);

        foreach (var linha in lines.Skip(1))
        {
            if (linha.StartsWith("T|", StringComparison.Ordinal))
            {
                var c = linha.Split('|', 3);
                if (c.Length == 3 && Guid.TryParse(c[1], out var mesa)) mesas[mesa] = c[2];
            }
            else if (linha.StartsWith("M|", StringComparison.Ordinal))
            {
                var c = linha.Split('|', 7);
                if (c.Length < 7 || c[1].Length == 0) continue;
                if (!Guid.TryParse(c[2], out var mesa)) mesa = Guid.Empty;
                if (!double.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) continue;

                var causa = Enum.TryParse<ShadowCause>(c[4], out var k) ? k : ShadowCause.None;
                modulos[c[1]] = new ModuleShadow(c[1], mesa, f, causa, Quando(c[5]), c[6]);
            }
        }

        return new ShadowReasons(mesas, modulos, null);
    }

    /// <summary>As linhas do registro, em V2 (as mesas e os módulos em ordem, para o arquivo não mudar à toa).</summary>
    public IReadOnlyList<string> Serialize()
    {
        var linhas = new List<string> { Versao };

        foreach (var (mesa, periodo) in _mesas.OrderBy(m => m.Key))
            linhas.Add(string.Join('|', "T", mesa.ToString("D"), periodo));

        foreach (var m in _modulos.Values.OrderBy(m => m.Table).ThenBy(m => m.Handle, StringComparer.OrdinalIgnoreCase))
        {
            linhas.Add(string.Join('|',
                "M", m.Handle, m.Table.ToString("D"), m.Fraction.ToString("R", CultureInfo.InvariantCulture), m.Cause.ToString(),
                m.When?.ToString("s", CultureInfo.InvariantCulture) ?? string.Empty, m.Period));
        }

        return linhas;
    }

    /// <summary>
    /// Um cálculo novo nestas mesas: as linhas delas (e dos módulos delas)
    /// saem e entram as novas; as das outras mesas ficam. Do V1 ficam os
    /// módulos com sombra das outras mesas (o período deles vai junto).
    /// </summary>
    /// <param name="tables">As mesas calculadas (as que receberam sombra no escopo).</param>
    /// <param name="period">O período do cálculo.</param>
    /// <param name="modules">Os módulos com sombra dessas mesas.</param>
    public ShadowReasons Replace(IEnumerable<Guid> tables, string period, IEnumerable<ModuleShadow> modules)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(modules);

        var calculadas = tables.ToHashSet();
        var novo = Remove(calculadas.Contains);

        foreach (var mesa in calculadas) novo._mesas[mesa] = period;
        foreach (var m in modules.Where(m => m.Fraction > 0)) novo._modulos[m.Handle] = m with { Period = period };

        return novo;
    }

    /// <summary>O registro sem as mesas que <paramref name="tables"/> aceita (o Apagar num escopo).</summary>
    public ShadowReasons Remove(Func<Guid, bool> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var mesas = _mesas.Where(m => !tables(m.Key)).ToDictionary(m => m.Key, m => m.Value);
        var modulos = _modulos.Values
            .Where(m => !tables(m.Table))
            .ToDictionary(m => m.Handle, m => m, StringComparer.OrdinalIgnoreCase);

        return new ShadowReasons(mesas, modulos, null);
    }

    /// <summary>A sombra guardada do módulo, ou null.</summary>
    public ModuleShadow? Find(string handle) => _modulos.TryGetValue(handle, out var m) ? m : null;

    /// <summary>O período do último cálculo que pegou a mesa (no V1, o do desenho inteiro), ou null se ela não foi calculada.</summary>
    public string? PeriodOf(Guid table) => _mesas.TryGetValue(table, out var p) ? p : LegacyPeriod;

    private static DateTime? Quando(string texto) =>
        DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
}

/// <summary>Um módulo com sombra para a janela de análises: o registro mais o que o desenho diz dele.</summary>
/// <param name="Shadow">A sombra guardada.</param>
/// <param name="Area">O nome da área da mesa, ou null fora das áreas.</param>
/// <param name="AreaHandle">O handle da polilinha da área (para o zoom), ou null.</param>
/// <param name="TableLabel">O letreiro da mesa (F1.3).</param>
/// <param name="TableHandle">O handle do contorno da mesa (para o zoom), ou null.</param>
/// <param name="Column">A coluna do módulo na mesa (de 0).</param>
/// <param name="Row">A fila do módulo na mesa (de 0).</param>
public sealed record ShadowTopicInput(ModuleShadow Shadow, string? Area, string? AreaHandle, string TableLabel, string? TableHandle, int Column, int Row);

/// <summary>Um tópico da janela de análises: o texto, o que mostrar no desenho (handle) e os de dentro.</summary>
/// <param name="Text">O texto do tópico.</param>
/// <param name="Handle">A entidade do zoom (área, contorno da mesa ou bloco do módulo), ou null.</param>
/// <param name="Worst">A maior fração dentro do tópico.</param>
/// <param name="Children">Os tópicos de dentro (área › mesa › módulo).</param>
public sealed record ShadowTopic(string Text, string? Handle, double Worst, IReadOnlyList<ShadowTopic> Children);

/// <summary>
/// Os tópicos da janela de análises e a frase do resumo (42.1, Renan: "quero
/// que vc coloque um botão ver analises, e ai abre uma janela com TOPICOs,
/// onde eu clico e ele me guia para o local da sombra, porque do jeito que ta
/// um super texto é inutil").
/// </summary>
public static class ShadowTopics
{
    /// <summary>O nome da causa, para a tela.</summary>
    public static string CauseName(ShadowCause cause) => cause switch
    {
        ShadowCause.Object => Tr.T("árvore"),
        ShadowCause.Table => Tr.T("mesa"),
        ShadowCause.Terrain => Tr.T("terreno"),
        _ => "-",
    };

    /// <summary>
    /// Área › mesa › módulo, do pior para o melhor em cada nível (empate:
    /// pelo nome, pela coluna). As mesas fora das áreas ficam num tópico
    /// próprio, no fim.
    /// </summary>
    public static IReadOnlyList<ShadowTopic> Build(IEnumerable<ShadowTopicInput> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var nomes = StringComparer.Create(Tr.Culture, true);

        ShadowTopic Mesa(IGrouping<(Guid Table, string TableLabel), ShadowTopicInput> mesa)
        {
            var modulos = mesa
                .OrderByDescending(m => m.Shadow.Fraction).ThenBy(m => m.Column).ThenBy(m => m.Row)
                .Select(m => new ShadowTopic(
                    Tr.F("col {0} fil {1} — {2:0}% · {3} · {4}", m.Column + 1, m.Row + 1, m.Shadow.Fraction * 100, CauseName(m.Shadow.Cause), Quando(m.Shadow.When)),
                    m.Shadow.Handle, m.Shadow.Fraction, []))
                .ToList();

            var pior = mesa.MaxBy(m => m.Shadow.Fraction)!;
            return new ShadowTopic(
                Tr.F("{0} — {1} módulo(s); pior {2:0}% ({3}) {4}", mesa.Key.TableLabel, modulos.Count, pior.Shadow.Fraction * 100, CauseName(pior.Shadow.Cause), Quando(pior.Shadow.When)),
                mesa.First().TableHandle, pior.Shadow.Fraction, modulos);
        }

        return modules
            .Where(m => m.Shadow.Fraction > 0)
            .GroupBy(m => m.Area)
            .Select(area =>
            {
                var mesas = area
                    .GroupBy(m => (m.Shadow.Table, m.TableLabel))
                    .Select(Mesa)
                    .OrderByDescending(t => t.Worst).ThenBy(t => t.Text, nomes)
                    .ToList();

                var topico = new ShadowTopic(
                    Tr.F("{0} — {1} mesa(s), {2} módulo(s) com sombra; pior {3:0}%", area.Key ?? Tr.T("Fora das áreas"), mesas.Count, mesas.Sum(t => t.Children.Count), mesas.Max(t => t.Worst) * 100),
                    area.First().AreaHandle, mesas.Max(t => t.Worst), mesas);

                return (ForaDasAreas: area.Key is null, Topico: topico);
            })
            .OrderBy(a => a.ForaDasAreas)
            .ThenByDescending(a => a.Topico.Worst)
            .ThenBy(a => a.Topico.Text, nomes)
            .Select(a => a.Topico)
            .ToList();
    }

    /// <summary>
    /// A frase do resultado, numa ou duas linhas (42.1: "560 módulo(s) com
    /// sombra em 64 mesa(s) de Área 1; o pior: F3.2 100% (mesa) às 07:00.
    /// Conta em 7,3 s.").
    /// </summary>
    /// <param name="modules">Os módulos com sombra do escopo: letreiro da mesa e a sombra.</param>
    /// <param name="scope">O escopo com a preposição ("de Área 1", "da seleção", "do desenho inteiro").</param>
    /// <param name="seconds">Quanto a conta levou.</param>
    public static string Summary(IReadOnlyList<(string TableLabel, ModuleShadow Shadow)> modules, string scope, double seconds)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(scope);

        var comSombra = modules.Where(m => m.Shadow.Fraction > 0).ToList();
        if (comSombra.Count == 0) return Tr.F("Nenhum módulo {0} com sombra. Conta em {1:0.0} s.", scope, seconds);

        var mesas = comSombra.Select(m => m.Shadow.Table == Guid.Empty ? m.TableLabel : m.Shadow.Table.ToString("D")).Distinct().Count();
        var pior = comSombra.OrderByDescending(m => m.Shadow.Fraction).ThenBy(m => m.Shadow.When ?? DateTime.MaxValue).First();
        var unicoDia = comSombra.Where(m => m.Shadow.When is not null).Select(m => m.Shadow.When!.Value.Date).Distinct().Count() <= 1;
        var quando = pior.Shadow.When is not { } t ? string.Empty : unicoDia ? Tr.F(" às {0:HH:mm}", t) : Tr.F(" em {0:dd/MM} {0:HH:mm}", t);

        return Tr.F("{0} módulo(s) com sombra em {1} mesa(s) {2}; o pior: {3} {4:0}% ({5}){6}. Conta em {7:0.0} s.",
            comSombra.Count, mesas, scope, pior.TableLabel, pior.Shadow.Fraction * 100, CauseName(pior.Shadow.Cause), quando, seconds);
    }

    private static string Quando(DateTime? t) => t is { } d ? d.ToString("dd/MM/yyyy HH:mm", Tr.Culture) : "-";
}
