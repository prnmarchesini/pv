namespace Clivus.Core;

/// <summary>O que acontece com a água num trecho ruim do fundo.</summary>
public enum BottomProblem
{
    /// <summary>Desce, mas menos que a mínima: a água anda devagar e assoreia.</summary>
    Slow,

    /// <summary>Plano: a água fica parada.</summary>
    Flat,

    /// <summary>Sobe sem formar barriga: a água volta e sai pela ponta de cima.</summary>
    Backward,

    /// <summary>Barriga até a altura da parede: enche, fica água parada e o resto passa.</summary>
    Pond,

    /// <summary>Barriga mais funda que a parede: a água transborda antes de passar.</summary>
    Overflow,
}

/// <summary>
/// Um trecho ruim do fundo explicado (pedido do Renan de 21/10/2026: "tem
/// como especificar melhor, tipo, quanto tem que afundar o solo, ou se vai
/// transbordar?").
/// </summary>
/// <param name="PondDepth">Na barriga: do fundo mais baixo até a borda por onde a água passa, em m; senão, 0.</param>
/// <param name="Cut">Quanto o fundo tem de descer, no máximo, nesse trecho, para ficar na declividade mínima desde a ponta de cima, em m.</param>
public sealed record BottomAdvice(SlopeSegment Run, BottomProblem Problem, double PondDepth, double Cut)
{
    /// <summary>Abaixo disto (m) não vale escrever "aprofundar".</summary>
    public const double SmallestCut = 0.005;

    /// <summary>O que acontece, curto (primeira linha no gráfico).</summary>
    public string What(double minimumSlope, double wallHeight) => Problem switch
    {
        BottomProblem.Slow => Tr.F("{0:0.0}% < {1:0.0}%: água lenta", Run.Percent, minimumSlope * 100),
        BottomProblem.Flat => Tr.T("plano: água parada"),
        BottomProblem.Backward => Tr.T("sobe: a água volta"),
        BottomProblem.Pond => Tr.F("barriga de {0:0.00} m: enche e passa", PondDepth),
        _ => Tr.F("barriga de {0:0.00} m > parede {1:0.00} m: transborda", PondDepth, wallHeight),
    };

    /// <summary>O conserto (segunda linha no gráfico); vazio se não precisa cavar.</summary>
    public string Fix => Cut >= SmallestCut ? Tr.F("aprofundar {0:0.00} m", Cut) : string.Empty;
}

/// <summary>Os trechos ruins do fundo, um a um, com o que a água faz neles e quanto cavar.</summary>
public static class ChannelBottomAdvice
{
    /// <param name="wallHeight">A altura útil da seção: barriga mais funda transborda.</param>
    public static IReadOnlyList<BottomAdvice> Of(ChannelProfileResult perfil, double wallHeight)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var p = perfil.Points;
        var n = p.Count;
        if (n < 2) return [];

        // O fundo consertado: desce sempre a mínima desde a ponta de cima (o
        // modo "declividade mínima"); o que falta é quanto cavar.
        var cavar = new double[n];
        var consertado = p[0].Bottom;
        for (var i = 1; i < n; i++)
        {
            consertado = Math.Min(p[i].Bottom, consertado - perfil.MinimumSlopeRule * (p[i].Distance - p[i - 1].Distance));
            cavar[i] = p[i].Bottom - consertado;
        }

        // Até onde a água sobe parada em cada ponto, saindo pelas pontas.
        var antes = new double[n];
        var depois = new double[n];
        for (var i = 0; i < n; i++) antes[i] = i == 0 ? p[0].Bottom : Math.Max(antes[i - 1], p[i].Bottom);
        for (var i = n - 1; i >= 0; i--) depois[i] = i == n - 1 ? p[i].Bottom : Math.Max(depois[i + 1], p[i].Bottom);
        var lamina = new double[n];
        for (var i = 0; i < n; i++) lamina[i] = Math.Max(0, Math.Min(antes[i], depois[i]) - p[i].Bottom);

        int Indice(double d)
        {
            var k = 0;
            while (k < n - 1 && p[k].Distance < d - 1e-9) k++;
            return k;
        }

        var conselhos = new List<BottomAdvice>();

        void Juntar(SlopeSegment t, bool invertido)
        {
            var (i0, i1) = (Indice(t.From), Indice(t.To));

            if (!invertido)
            {
                conselhos.Add(new BottomAdvice(t, BottomProblem.Slow, 0, Max(cavar, i0, i1)));
                return;
            }

            // A barriga que encosta no trecho: os pontos com água parada em volta dele.
            var (a, b) = (i0, i1);
            if (lamina.Skip(i0).Take(i1 - i0 + 1).Any(l => l > 1e-6) || (i0 > 0 && lamina[i0 - 1] > 1e-6) || (i1 < n - 1 && lamina[i1 + 1] > 1e-6))
            {
                while (a > 0 && lamina[a - 1] > 1e-6) a--;
                while (b < n - 1 && lamina[b + 1] > 1e-6) b++;
            }

            var barriga = Max(lamina, a, b);
            var problema = barriga > wallHeight + 1e-9 ? BottomProblem.Overflow
                : barriga > ChannelProfileResult.RiseTolerance ? BottomProblem.Pond
                : -t.Drop < ChannelProfileResult.RiseTolerance ? BottomProblem.Flat
                : BottomProblem.Backward;

            conselhos.Add(new BottomAdvice(t, problema, problema is BottomProblem.Pond or BottomProblem.Overflow ? barriga : 0, Max(cavar, Math.Min(a, i0), Math.Max(b, i1))));
        }

        foreach (var t in perfil.LowSegments) Juntar(t, false);
        foreach (var t in perfil.InvertedSegments) Juntar(t, true);

        return [.. conselhos.OrderBy(c => c.Run.From)];
    }

    private static double Max(double[] valores, int de, int ate)
    {
        var m = 0.0;
        for (var i = Math.Max(0, de); i <= Math.Min(valores.Length - 1, ate); i++) m = Math.Max(m, valores[i]);
        return m;
    }
}
