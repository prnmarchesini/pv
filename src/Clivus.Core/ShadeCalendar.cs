using System.Globalization;
using System.Text;

namespace Clivus.Core;

/// <summary>
/// Os dias e as horas de sombra estimados no período, os mesmos para o
/// registro do Gerar sombras e para o resumo da mesa (revisão do 42.14):
/// cada dia calculado vale o peso, os dias do período sobre os dias
/// calculados (um ano de 7 em 7 tem 53 dias calculados e 365 dias: cada um
/// vale 6,9); as horas são a soma das frações vezes o passo do dia, vezes o
/// mesmo peso. Com uma hora só por dia (o horário fixo, o instante), o passo
/// não é duração: as horas são 0.
/// </summary>
public static class ShadeEstimate
{
    /// <summary>Os dias calculados do período: o primeiro e, de <paramref name="dayStep"/> em <paramref name="dayStep"/>, até o último.</summary>
    public static IEnumerable<DateOnly> SampledDays(DateOnly first, DateOnly last, int dayStep)
    {
        for (var d = first; d <= last; d = d.AddDays(Math.Max(1, dayStep))) yield return d;
    }

    /// <summary>O peso de cada dia calculado: os dias do período sobre os dias calculados.</summary>
    public static double DayWeight(DateOnly first, DateOnly last, int dayStep)
    {
        if (last < first) return 1;
        var dias = last.DayNumber - first.DayNumber + 1;
        var calculados = (last.DayNumber - first.DayNumber) / Math.Max(1, dayStep) + 1;
        return (double)dias / calculados;
    }

    /// <summary>A duração de cada instante, em horas: o passo do dia, ou 0 com uma hora só por dia.</summary>
    public static double StepHours(TimeOnly from, TimeOnly to, int stepMinutes) => from == to ? 0 : stepMinutes / 60.0;

    /// <summary>Os dias e as horas estimados de uma face.</summary>
    public static (int Days, double Hours) Of(ShadeTally tally, double dayWeight, double stepHours) =>
        ((int)Math.Round(tally.Days * dayWeight, MidpointRounding.AwayFromZero), tally.FractionSum * stepHours * dayWeight);
}

/// <summary>A sombra de um módulo em cada instante calculado: a fração (0 a 1) e a causa, na ordem dos instantes do calendário.</summary>
/// <param name="Handle">O handle do bloco do módulo.</param>
/// <param name="Label">Como o módulo aparece (col 3 fil 1).</param>
/// <param name="Column">A coluna na mesa (de 0).</param>
/// <param name="Row">A fileira na mesa (de 0).</param>
/// <param name="Fractions">A fração da face na sombra em cada instante.</param>
/// <param name="Causes">O que fez a sombra em cada instante.</param>
public sealed record ShadeSeries(string Handle, string Label, int Column, int Row, IReadOnlyList<float> Fractions, IReadOnlyList<ShadowCause> Causes);

/// <summary>
/// O resumo das sombras de uma mesa (02/11/2026, Renan: "um botão no modal
/// sombras que eu seleciono a mesa e ele me da o resumo das sombras, me uma
/// lista estruturadas dos dias que bate sombra em cada modulo e quantos % em
/// cada hora"): os instantes calculados (com sol) num calendário de dias ×
/// horas e, por módulo, a fração em cada um. O módulo −1 é a mesa inteira,
/// a média dos módulos em cada instante.
/// </summary>
public sealed class ShadeCalendar
{
    /// <summary>A mesa inteira, onde se pede um módulo: a média dos módulos em cada instante.</summary>
    public const int WholeTable = -1;

    private readonly int[,] _instante;
    private readonly float[] _media;

    /// <param name="instants">Os instantes calculados, em ordem (os com sol).</param>
    /// <param name="stepHours">Quanto cada instante dura, em horas (<see cref="ShadeEstimate.StepHours"/>; 0 com uma hora só por dia).</param>
    /// <param name="dayWeight">Quantos dias do período cada dia calculado vale (<see cref="ShadeEstimate.DayWeight"/>).</param>
    /// <param name="modules">A série de cada módulo, com um valor por instante.</param>
    /// <param name="sampledDays">Todos os dias calculados, também os sem instante nenhum (o sol não passou da altura): entram como colunas vazias.</param>
    public ShadeCalendar(IReadOnlyList<DateTime> instants, double stepHours, double dayWeight, IReadOnlyList<ShadeSeries> modules, IEnumerable<DateOnly>? sampledDays = null)
    {
        ArgumentNullException.ThrowIfNull(instants);
        ArgumentNullException.ThrowIfNull(modules);
        if (!double.IsFinite(stepHours) || stepHours < 0) throw new ArgumentOutOfRangeException(nameof(stepHours), "O passo não pode ser negativo.");
        if (!double.IsFinite(dayWeight) || dayWeight < 1) throw new ArgumentOutOfRangeException(nameof(dayWeight), "Cada dia calculado vale pelo menos 1 dia.");
        if (modules.Any(m => m.Fractions.Count != instants.Count || m.Causes.Count != instants.Count))
            throw new ArgumentException("Cada módulo precisa de um valor por instante.", nameof(modules));

        Instants = instants;
        StepHours = stepHours;
        DayWeight = dayWeight;
        Modules = modules;
        Days = instants.Select(DateOnly.FromDateTime).Concat(sampledDays ?? []).Distinct().Order().ToList();
        Times = instants.Select(TimeOnly.FromDateTime).Distinct().Order().ToList();

        var dia = Days.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);
        var hora = Times.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        _instante = new int[Days.Count, Times.Count];
        for (var d = 0; d < Days.Count; d++)
            for (var h = 0; h < Times.Count; h++) _instante[d, h] = -1;
        for (var i = 0; i < instants.Count; i++) _instante[dia[DateOnly.FromDateTime(instants[i])], hora[TimeOnly.FromDateTime(instants[i])]] = i;

        _media = new float[instants.Count];
        if (modules.Count > 0)
            for (var i = 0; i < instants.Count; i++) _media[i] = modules.Sum(m => m.Fractions[i]) / modules.Count;
    }

    /// <summary>Os instantes calculados, em ordem.</summary>
    public IReadOnlyList<DateTime> Instants { get; }

    /// <summary>Quanto cada instante dura, em horas.</summary>
    public double StepHours { get; }

    /// <summary>Quantos dias do período cada dia calculado vale.</summary>
    public double DayWeight { get; }

    /// <summary>Os módulos da mesa.</summary>
    public IReadOnlyList<ShadeSeries> Modules { get; }

    /// <summary>Os dias calculados, em ordem.</summary>
    public IReadOnlyList<DateOnly> Days { get; }

    /// <summary>As horas do dia que aparecem em algum dia, em ordem.</summary>
    public IReadOnlyList<TimeOnly> Times { get; }

    /// <summary>A fração no instante <paramref name="instant"/> do módulo (ou da mesa inteira, <see cref="WholeTable"/>).</summary>
    public float Fraction(int module, int instant) => module == WholeTable ? _media[instant] : Modules[module].Fractions[instant];

    /// <summary>O instante do dia e da hora (índices em <see cref="Days"/> e <see cref="Times"/>), ou −1 se ele não foi calculado (sol abaixo, fora da janela).</summary>
    public int InstantAt(int day, int time) => _instante[day, time];

    /// <summary>A fração no dia e na hora, ou null se o instante não foi calculado.</summary>
    public float? At(int module, int day, int time) => _instante[day, time] is var i and >= 0 ? Fraction(module, i) : null;

    /// <summary>Os dias (índices em <see cref="Days"/>) em que o módulo pegou alguma sombra.</summary>
    public IReadOnlyList<int> ShadedDays(int module)
    {
        var dias = new List<int>();
        for (var d = 0; d < Days.Count; d++)
        {
            for (var h = 0; h < Times.Count; h++)
            {
                if (At(module, d, h) is not > 0) continue;
                dias.Add(d);
                break;
            }
        }

        return dias;
    }

    /// <summary>As horas de sombra cheia no período, como no registro (<see cref="ShadeEstimate"/>): a soma das frações vezes o passo, vezes o peso do dia.</summary>
    public double Hours(int module)
    {
        var soma = 0.0;
        for (var i = 0; i < Instants.Count; i++) soma += Fraction(module, i);
        return soma * StepHours * DayWeight;
    }

    /// <summary>Em quantos dias do período ele pegou sombra, como no registro: os dias calculados com sombra vezes o peso do dia.</summary>
    public int DaysWithShade(int module) => (int)Math.Round(ShadedDays(module).Count * DayWeight, MidpointRounding.AwayFromZero);

    /// <summary>A maior fração do módulo e quando, ou (0, null) sem sombra.</summary>
    public (float Fraction, DateTime? When) Worst(int module)
    {
        var pior = 0f;
        DateTime? quando = null;
        for (var i = 0; i < Instants.Count; i++)
        {
            var f = Fraction(module, i);
            if (f <= pior) continue;
            pior = f;
            quando = Instants[i];
        }

        return (pior, quando);
    }

    /// <summary>
    /// O texto para colar no Excel (separado por tabulação): o cabeçalho e uma
    /// linha por módulo, dia e hora com sombra (a mesa, o módulo, a data, a
    /// hora, a porcentagem, arredondada como a janela mostra, e a causa).
    /// </summary>
    /// <param name="header">O cabeçalho, já no idioma (as colunas separadas por tabulação).</param>
    /// <param name="causeName">O nome da causa no idioma.</param>
    /// <param name="table">O letreiro da mesa.</param>
    public string ToTsv(string header, Func<ShadowCause, string> causeName, string table = "")
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(causeName);

        var texto = new StringBuilder(header).Append("\r\n");
        foreach (var m in Modules)
        {
            for (var i = 0; i < Instants.Count; i++)
            {
                if (m.Fractions[i] <= 0) continue;
                texto.Append(table).Append('\t').Append(m.Label).Append('\t')
                    .Append(Instants[i].ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Instants[i].ToString("HH:mm", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Math.Round(m.Fractions[i] * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(causeName(m.Causes[i])).Append("\r\n");
            }
        }

        return texto.ToString();
    }
}

/// <summary>
/// Os piores módulos (02/11/2026, Renan: "Me mostre os X piores modulo que
/// bate sombra ... o sistema pinta os modulos mais fudidos com ordem. Ou
/// então ... me mostre os modulos pegam sombra em x dias por ano"), pelas
/// contas que o cálculo do período guarda em cada módulo.
/// </summary>
public static class ShadowRanking
{
    // Do vinho (o pior) ao amarelo (o último), passando pelo vermelho e pelo laranja.
    private static readonly RgbColor[] Escala = [new(128, 0, 38), new(227, 26, 28), new(253, 141, 60), new(254, 217, 118)];

    /// <summary>Se o módulo tem as contas do período (o registro de antes de 02/11/2026 não tem).</summary>
    public static bool HasTally(ModuleShadow module) => module.Days is not null && module.Hours is not null;

    /// <summary>
    /// Os <paramref name="count"/> piores: mais horas de sombra cheia; no
    /// empate, mais dias, depois a maior fração. Só os com as contas.
    /// </summary>
    public static IReadOnlyList<ModuleShadow> Worst(IEnumerable<ModuleShadow> modules, int count)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Pelo menos 1 módulo.");

        return Ordenar(modules.Where(HasTally)).Take(count).ToList();
    }

    /// <summary>Os com sombra em pelo menos <paramref name="days"/> dias do período, dos mais sombreados aos menos.</summary>
    public static IReadOnlyList<ModuleShadow> WithAtLeastDays(IEnumerable<ModuleShadow> modules, int days)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (days < 1) throw new ArgumentOutOfRangeException(nameof(days), "Pelo menos 1 dia.");

        return Ordenar(modules.Where(m => HasTally(m) && m.Days >= days)).ToList();
    }

    /// <summary>A cor do lugar <paramref name="index"/> (0 é o pior) entre <paramref name="total"/>: do vinho ao amarelo.</summary>
    public static RgbColor RankColor(int index, int total)
    {
        if (total <= 1) return Escala[0];

        var t = Math.Clamp((double)index / (total - 1), 0, 1) * (Escala.Length - 1);
        var k = Math.Min((int)t, Escala.Length - 2);
        var u = t - k;
        byte Mistura(byte a, byte b) => (byte)Math.Round(a + (b - a) * u);

        return new RgbColor(Mistura(Escala[k].R, Escala[k + 1].R), Mistura(Escala[k].G, Escala[k + 1].G), Mistura(Escala[k].B, Escala[k + 1].B));
    }

    private static IOrderedEnumerable<ModuleShadow> Ordenar(IEnumerable<ModuleShadow> modules) =>
        modules
            .OrderByDescending(m => m.Hours)
            .ThenByDescending(m => m.Days)
            .ThenByDescending(m => m.Fraction)
            .ThenBy(m => m.Handle, StringComparer.OrdinalIgnoreCase);
}
