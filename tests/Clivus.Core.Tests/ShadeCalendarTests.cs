namespace Clivus.Core.Tests;

/// <summary>
/// O resumo das sombras de uma mesa e os piores módulos (Renan, 02/11/2026:
/// "uma lista estruturadas dos dias que bate sombra em cada modulo e quantos
/// % em cada hora"; "Me mostre os X piores modulo que bate sombra"; "me
/// mostre os modulos pegam sombra em x dias por ano").
/// </summary>
public class ShadeCalendarTests
{
    // Dois dias calculados (um por semana), de meia em meia hora; no segundo, o sol das 7h não conta (abaixo da altura).
    private static readonly DateTime[] Instantes =
    [
        new(2026, 6, 21, 7, 0, 0), new(2026, 6, 21, 7, 30, 0), new(2026, 6, 21, 8, 0, 0),
        new(2026, 6, 28, 7, 30, 0), new(2026, 6, 28, 8, 0, 0),
    ];

    private static ShadeSeries Serie(string rotulo, int coluna, params float[] fracoes) =>
        new("H" + rotulo, rotulo, coluna, 0, fracoes, fracoes.Select(f => f > 0 ? ShadowCause.Table : ShadowCause.None).ToList());

    // Meia hora por instante; cada dia calculado vale 7.
    private static ShadeCalendar Calendario(IEnumerable<DateOnly>? dias = null) => new(Instantes, 0.5, 7,
    [
        Serie("col 1 fil 1", 0, 1f, 0.5f, 0f, 0f, 0f),
        Serie("col 2 fil 1", 1, 0f, 0f, 0f, 0.25f, 0f),
        Serie("col 3 fil 1", 2, 0f, 0f, 0f, 0f, 0f),
    ], dias);

    [Fact]
    [Trait("Etapa", "42")]
    public void OCalendarioTemOsDiasEAsHorasEOQueNaoFoiCalculadoFicaVazio()
    {
        var c = Calendario();

        Assert.Equal([new DateOnly(2026, 6, 21), new DateOnly(2026, 6, 28)], c.Days);
        Assert.Equal([new TimeOnly(7, 0), new TimeOnly(7, 30), new TimeOnly(8, 0)], c.Times);

        Assert.Equal(1f, c.At(0, 0, 0));
        Assert.Equal(0.5f, c.At(0, 0, 1));
        Assert.Null(c.At(0, 1, 0));   // 28/06 às 7h: não calculado
        Assert.Equal(0.25f, c.At(1, 1, 1));
        Assert.Equal(-1, c.InstantAt(1, 0));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ODiaCalculadoSemSolAcimaDaAlturaEntraVazio()
    {
        // 05/07 foi calculado, mas o sol não passou da altura: nenhum instante, e o dia aparece (cinza).
        var c = Calendario(ShadeEstimate.SampledDays(new DateOnly(2026, 6, 21), new DateOnly(2026, 7, 5), 7));

        Assert.Equal([new DateOnly(2026, 6, 21), new DateOnly(2026, 6, 28), new DateOnly(2026, 7, 5)], c.Days);
        Assert.All(Enumerable.Range(0, c.Times.Count), h => Assert.Null(c.At(0, 2, h)));
        Assert.Equal([0], c.ShadedDays(0));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OsDiasComSombraAsHorasDeSombraCheiaEOPiorDeCadaModulo()
    {
        var c = Calendario();

        Assert.Equal([0], c.ShadedDays(0));
        Assert.Equal([1], c.ShadedDays(1));
        Assert.Empty(c.ShadedDays(2));

        // Cada dia calculado vale 7: 1 dia com sombra é 7 no período.
        Assert.Equal(7, c.DaysWithShade(0));
        Assert.Equal(0, c.DaysWithShade(2));

        // (1 + 0,5) × meia hora × 7 dias = 5,25 h; 0,25 × 0,5 × 7 = 0,875 h.
        Assert.Equal(5.25, c.Hours(0), 9);
        Assert.Equal(0.875, c.Hours(1), 9);

        Assert.Equal((1f, Instantes[0]), c.Worst(0));
        Assert.Equal((0f, (DateTime?)null), c.Worst(2));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OPesoDoDiaEOsDiasDoPeriodoDivididosPelosCalculados()
    {
        // 21 a 30/06, de 7 em 7: 21 e 28 calculados, 10 dias no período; cada um vale 5.
        var peso = ShadeEstimate.DayWeight(new DateOnly(2026, 6, 21), new DateOnly(2026, 6, 30), 7);
        Assert.Equal(5, peso, 9);

        var c = new ShadeCalendar(Instantes, 0.5, peso, [Serie("x", 0, 1f, 0f, 0f, 1f, 0f)]);
        Assert.Equal(10, c.DaysWithShade(0));   // os 2 dias calculados com sombra são o período inteiro
        Assert.Equal(5, c.Hours(0), 9);         // (1 + 1) × 0,5 h × 5

        // O ano de 30 em 30 dias: 13 dias calculados valem 365 (antes eram 390).
        var ano = ShadeEstimate.DayWeight(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 30);
        Assert.Equal(365.0 / 13, ano, 9);
        Assert.Equal(365, ShadeEstimate.Of(new ShadeTally(13 * 9, 13, 13 * 4.5), ano, 1).Days);
        Assert.Equal(13 * 4.5 * ano, ShadeEstimate.Of(new ShadeTally(13 * 9, 13, 13 * 4.5), ano, 1).Hours, 9);

        // Passo de 1 dia ou um dia só: cada dia vale 1.
        Assert.Equal(1, ShadeEstimate.DayWeight(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1), 9);
        Assert.Equal(1, ShadeEstimate.DayWeight(new DateOnly(2026, 6, 21), new DateOnly(2026, 6, 21), 7), 9);

        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), new DateOnly(2026, 1, 15)],
            ShadeEstimate.SampledDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), 7));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void UmaHoraSoPorDiaNaoSomaHoras()
    {
        // Ao meio-dia, todo dia: cada instante não "dura" o passo (o passo não é o intervalo entre eles).
        Assert.Equal(0, ShadeEstimate.StepHours(new TimeOnly(12, 0), new TimeOnly(12, 0), 60));
        Assert.Equal(0.5, ShadeEstimate.StepHours(new TimeOnly(6, 0), new TimeOnly(18, 0), 30), 9);

        var c = new ShadeCalendar([new DateTime(2026, 6, 21, 12, 0, 0)], 0, 1, [Serie("x", 0, 1f)]);
        Assert.Equal(0, c.Hours(0));
        Assert.Equal(1, c.DaysWithShade(0));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AMesaInteiraEAMediaDosModulos()
    {
        var c = Calendario();

        Assert.Equal(1f / 3, c.Fraction(ShadeCalendar.WholeTable, 0), 6);
        Assert.Equal(0.25f / 3, c.At(ShadeCalendar.WholeTable, 1, 1)!.Value, 6);
        Assert.Equal([0, 1], c.ShadedDays(ShadeCalendar.WholeTable));
        Assert.Equal((5.25 + 0.875) / 3, c.Hours(ShadeCalendar.WholeTable), 6);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OTextoParaOExcelTemUmaLinhaPorModuloDiaEHoraComSombra()
    {
        var tsv = Calendario().ToTsv("Mesa\tMódulo\tData\tHora\t%\tCausa", c => c == ShadowCause.Table ? "mesa" : "-", "M1");
        var linhas = tsv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, linhas.Length);
        Assert.Equal("Mesa\tMódulo\tData\tHora\t%\tCausa", linhas[0]);
        Assert.Equal("M1\tcol 1 fil 1\t21/06/2026\t07:00\t100\tmesa", linhas[1]);
        Assert.Equal("M1\tcol 1 fil 1\t21/06/2026\t07:30\t50\tmesa", linhas[2]);
        Assert.Equal("M1\tcol 2 fil 1\t28/06/2026\t07:30\t25\tmesa", linhas[3]);

        // 12,5% vira 13, como a janela mostra (e não 12, o arredondamento para o par).
        var meio = new ShadeCalendar([new DateTime(2026, 6, 21, 12, 0, 0)], 0.5, 1, [Serie("x", 0, 0.125f)]);
        Assert.EndsWith("\t13\tmesa\r\n", meio.ToTsv("h", _ => "mesa", "M1"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OCalendarioRecusaSerieDeTamanhoErradoEPassoInvalido()
    {
        Assert.Throws<ArgumentException>(() => new ShadeCalendar(Instantes, 0.5, 1, [Serie("x", 0, 1f)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShadeCalendar(Instantes, -0.5, 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShadeCalendar(Instantes, double.NaN, 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShadeCalendar(Instantes, 0.5, 0.5, []));
    }

    private static ModuleShadow M(string handle, double horas, int dias, double fracao = 0.5) =>
        new(handle, Guid.Empty, fracao, ShadowCause.Table, null, "?", dias, horas);

    [Fact]
    [Trait("Etapa", "42")]
    public void OsPioresSaoOsDeMaisHorasDeSombraEONoEmpateMaisDias()
    {
        var modulos = new[]
        {
            M("A", 10, 20), M("B", 30, 5), M("C", 10, 40), M("D", 0.5, 1),
            new ModuleShadow("E", Guid.Empty, 1, ShadowCause.Object, null, "?"),   // registro antigo: sem as contas
        };

        Assert.Equal(["B", "C", "A"], ShadowRanking.Worst(modulos, 3).Select(m => m.Handle));
        Assert.Equal(["B", "C", "A", "D"], ShadowRanking.Worst(modulos, 10).Select(m => m.Handle));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowRanking.Worst(modulos, 0));

        // Em pelo menos 20 dias: C (40) e A (20), na ordem dos piores (A tem menos horas que C, empate desfeito pelos dias).
        Assert.Equal(["C", "A"], ShadowRanking.WithAtLeastDays(modulos, 20).Select(m => m.Handle));
        Assert.Empty(ShadowRanking.WithAtLeastDays(modulos, 41));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ACorDoLugarVaiDoVinhoAoAmarelo()
    {
        Assert.Equal(new RgbColor(128, 0, 38), ShadowRanking.RankColor(0, 10));
        Assert.Equal(new RgbColor(254, 217, 118), ShadowRanking.RankColor(9, 10));
        Assert.Equal(new RgbColor(128, 0, 38), ShadowRanking.RankColor(0, 1));

        // Do pior ao último, o verde cresce sempre (fica mais claro).
        var verdes = Enumerable.Range(0, 10).Select(i => (int)ShadowRanking.RankColor(i, 10).G).ToList();
        Assert.Equal(verdes.Order(), verdes);
    }
}
