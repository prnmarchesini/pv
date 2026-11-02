using System.Globalization;
using System.Text;

namespace Clivus.Core;

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
    /// <param name="step">O passo do dia.</param>
    /// <param name="dayStep">O passo de dias (7: um dia por semana; cada dia calculado vale 7).</param>
    /// <param name="modules">A série de cada módulo, com um valor por instante.</param>
    /// <param name="periodDays">Quantos dias o período tem: os dias com sombra estimados (os calculados vezes o passo) não passam disso, como no registro do Gerar sombras.</param>
    public ShadeCalendar(IReadOnlyList<DateTime> instants, TimeSpan step, int dayStep, IReadOnlyList<ShadeSeries> modules, int? periodDays = null)
    {
        PeriodDays = periodDays;
        ArgumentNullException.ThrowIfNull(instants);
        ArgumentNullException.ThrowIfNull(modules);
        if (step <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step), "O passo precisa ser positivo.");
        if (dayStep < 1) throw new ArgumentOutOfRangeException(nameof(dayStep), "O passo de dias precisa ser 1 ou mais.");
        if (modules.Any(m => m.Fractions.Count != instants.Count || m.Causes.Count != instants.Count))
            throw new ArgumentException("Cada módulo precisa de um valor por instante.", nameof(modules));

        Instants = instants;
        Step = step;
        DayStep = dayStep;
        Modules = modules;
        Days = instants.Select(DateOnly.FromDateTime).Distinct().ToList();
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

    /// <summary>O passo do dia.</summary>
    public TimeSpan Step { get; }

    /// <summary>O passo de dias.</summary>
    public int DayStep { get; }

    /// <summary>Quantos dias o período tem, ou null sem limite.</summary>
    public int? PeriodDays { get; }

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

    /// <summary>As horas de sombra cheia no período: a soma das frações vezes o passo, vezes o passo de dias.</summary>
    public double Hours(int module)
    {
        var soma = 0.0;
        for (var i = 0; i < Instants.Count; i++) soma += Fraction(module, i);
        return soma * Step.TotalHours * DayStep;
    }

    /// <summary>
    /// Em quantos dias do período ele pegou sombra: os dias calculados com
    /// sombra vezes o passo de dias, sem passar dos dias do período (13 dias
    /// de 30 em 30 no ano dão 390, e o ano tem 365).
    /// </summary>
    public int DaysWithShade(int module) => Math.Min(ShadedDays(module).Count * DayStep, PeriodDays ?? int.MaxValue);

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
    /// linha por módulo, dia e hora com sombra (o módulo, a data, a hora, a
    /// porcentagem e a causa).
    /// </summary>
    /// <param name="header">O cabeçalho, já no idioma (as colunas separadas por tabulação).</param>
    /// <param name="causeName">O nome da causa no idioma.</param>
    public string ToTsv(string header, Func<ShadowCause, string> causeName)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(causeName);

        var texto = new StringBuilder(header).Append("\r\n");
        foreach (var m in Modules)
        {
            for (var i = 0; i < Instants.Count; i++)
            {
                if (m.Fractions[i] <= 0) continue;
                texto.Append(m.Label).Append('\t')
                    .Append(Instants[i].ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Instants[i].ToString("HH:mm", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Math.Round(m.Fractions[i] * 100).ToString(CultureInfo.InvariantCulture)).Append('\t')
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
