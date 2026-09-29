using System.Globalization;

namespace UFV.Core.Invariants;

/// <summary>
/// Regra sagrada 6 (Renan, 29/09/2026): ponta com ponta.
///
/// "A primeira ponta de uma mesa e a última ponta da outra mesa têm que ter
/// a mesma altura." Onde duas mesas vizinhas da mesma fileira se encontram,
/// a PB do pilar da ponta de uma é a PB do pilar da ponta da outra. Cada
/// mesa pode ter PBs diferentes nas suas duas pontas (é o giro dela); o elo
/// com a vizinha não abre.
///
/// Não entram: vão maior que o que quebra a fileira (as mesas deixam de ser
/// vizinhas) e mesa posta fora da corrente (pontas à mão, sem terreno), que
/// sai marcada ou foi escolhida pelo usuário.
/// </summary>
public static class EqualTips
{
    /// <summary>
    /// Dois centímetros: a grade da corrente é de 1 cm, e a PB desenhada é
    /// medida onde o pilar está depois do giro (a iteração do pipeline
    /// converge abaixo de meio centímetro).
    /// </summary>
    public const double Tolerancia = 0.02;

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>As juntas que abriram, em português; vazia se toda junta fecha.</summary>
    /// <param name="row">A fileira processada, na ordem da fileira.</param>
    /// <param name="configuration">De onde vem o vão que quebra a fileira.</param>
    public static IReadOnlyList<string> Check(ProcessedRow row, SystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(configuration);

        var abertas = new List<string>();
        var mesas = row.Tables;

        for (var i = 0; i + 1 < mesas.Count; i++)
        {
            var a = mesas[i];
            var b = mesas[i + 1];

            if (a.Solved.Seated || b.Solved.Seated) continue;
            if (RowSolver.GapBetween(a.Cell, b.Cell) > configuration.MaxGapBeforeBreak + 1e-9) continue;
            if (a.Pillars.Pillars.Count == 0 || b.Pillars.Pillars.Count == 0) continue;

            // Com o comprimento correndo com a fileira, o fim local de A
            // encosta no início local de B; senão, o contrário.
            var comAFileira = a.Orientation.LengthRunsWithRow;
            var pontaA = comAFileira ? a.Pillars.Pillars[^1] : a.Pillars.Pillars[0];
            var pontaB = comAFileira ? b.Pillars.Pillars[0] : b.Pillars.Pillars[^1];

            if (pontaA.LowEdgeClearance is not { } pa || pontaB.LowEdgeClearance is not { } pb) continue;

            if (Math.Abs(pa - pb) > Tolerancia)
            {
                abertas.Add(
                    $"a junta {a.Label}|{b.Label} abriu: PB {pa.ToString("0.00", Brasil)} m de um lado e "
                    + $"{pb.ToString("0.00", Brasil)} m do outro");
            }
        }

        return abertas;
    }
}
