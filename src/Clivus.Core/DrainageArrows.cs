namespace Clivus.Core;

/// <summary>Uma faixa de declividade das flechas: até quantos %, a cor e o rótulo da legenda.</summary>
/// <param name="UpToPercent">Limite de cima da faixa, em %; a última faixa vai ao infinito.</param>
public sealed record SlopeBand(double UpToPercent, RgbColor Color, string Label);

/// <summary>A forma de uma flecha em planta: cauda, ponta e as duas abas.</summary>
public readonly record struct ArrowShape((double X, double Y) Tail, (double X, double Y) Tip, (double X, double Y) WingLeft, (double X, double Y) WingRight);

/// <summary>
/// As flechas do sentido da água (passo 26, Análise › Flechas). Pedido do
/// Renan, 21/10/2026: "o AutoCAD Civil pega o terreno e coloca as flechinhas
/// indicando o sentido da água, teria como replicar?", sem configurar estilo.
/// Referência visual: Figura 3 do memorial de Valparaiso.
/// </summary>
public static class DrainageArrows
{
    /// <summary>
    /// Abaixo disto (em %) o ponto é plano: vira um pontinho, não flecha. O
    /// mesmo limiar do caminho da água (<c>FlowOptions.FlatRatio</c>, 0,0005 m/m):
    /// flecha e bacia concordam sobre onde a água para.
    /// </summary>
    public const double FlatPercent = 0.05;

    /// <summary>As cores da Figura 3 do memorial, uma por faixa: verde, laranja, vermelho, roxo, azul-escuro.</summary>
    public static readonly IReadOnlyList<RgbColor> BandColors =
    [
        new(0, 200, 0),
        new(237, 125, 49),
        new(255, 0, 0),
        new(112, 48, 160),
        new(0, 32, 96),
    ];

    /// <summary>Os limites padrão das faixas, em %: 0–5, 5–10, 10–15, 15–20, acima de 20.</summary>
    public static readonly IReadOnlyList<double> DefaultLimits = [5, 10, 15, 20];

    /// <summary>
    /// As faixas pelos limites (crescentes, em %). Os limites que não sobem são
    /// recusados: uma faixa de largura zero ou negativa some da legenda e
    /// engana quem lê.
    /// </summary>
    public static IReadOnlyList<SlopeBand> Bands(IReadOnlyList<double> limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (limits.Count != BandColors.Count - 1)
            throw new ArgumentException(Tr.F("As faixas precisam de {0} limites.", BandColors.Count - 1));

        for (var i = 0; i < limits.Count; i++)
        {
            if (!double.IsFinite(limits[i]) || limits[i] <= 0 || (i > 0 && limits[i] <= limits[i - 1]))
                throw new ArgumentException(Tr.T("Os limites das faixas precisam ser positivos e crescentes."));
        }

        var faixas = new List<SlopeBand>();
        var de = 0.0;

        for (var i = 0; i < BandColors.Count; i++)
        {
            if (i < limits.Count)
            {
                faixas.Add(new SlopeBand(limits[i], BandColors[i], Tr.F("{0:0.#} – {1:0.#}%", de, limits[i])));
                de = limits[i];
            }
            else
            {
                faixas.Add(new SlopeBand(double.PositiveInfinity, BandColors[i], Tr.F("> {0:0.#}%", de)));
            }
        }

        return faixas;
    }

    /// <summary>Em qual faixa cai a declividade (em %). O limite é da faixa de baixo: 5 % é 0–5.</summary>
    public static int BandOf(double percent, IReadOnlyList<SlopeBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        for (var i = 0; i < bands.Count; i++)
        {
            if (percent <= bands[i].UpToPercent) return i;
        }

        return bands.Count - 1;
    }

    /// <summary>
    /// Os pontos da grade (centros das células de <paramref name="spacing"/> m)
    /// dentro do retângulo e aceitos por <paramref name="inside"/>. A grade
    /// é alinhada em múltiplos do espaçamento, para gerar de novo dar os
    /// mesmos pontos.
    /// </summary>
    public static IEnumerable<(double X, double Y)> GridPoints(double minX, double minY, double maxX, double maxY, double spacing, Func<double, double, bool> inside)
    {
        ArgumentNullException.ThrowIfNull(inside);

        if (!(spacing > 0) || !double.IsFinite(spacing)) throw new ArgumentException(Tr.T("O espaçamento das flechas precisa ser maior que zero."));

        var i0 = (long)Math.Floor(minX / spacing);
        var i1 = (long)Math.Floor(maxX / spacing);
        var j0 = (long)Math.Floor(minY / spacing);
        var j1 = (long)Math.Floor(maxY / spacing);

        for (var j = j0; j <= j1; j++)
        for (var i = i0; i <= i1; i++)
        {
            var x = (i + 0.5) * spacing;
            var y = (j + 0.5) * spacing;

            if (x < minX || x > maxX || y < minY || y > maxY) continue;
            if (inside(x, y)) yield return (x, y);
        }
    }

    /// <summary>
    /// A flecha centrada em (x, y) apontando para (dirX, dirY): comprimento de
    /// 70 % do espaçamento (sobra folga entre vizinhas), abas de um quarto do
    /// comprimento abertas 25°.
    /// </summary>
    public static ArrowShape Shape(double x, double y, double dirX, double dirY, double spacing)
    {
        var norma = Math.Sqrt(dirX * dirX + dirY * dirY);
        if (norma < 1e-12) throw new ArgumentException(Tr.T("Flecha sem direção."));

        var (ux, uy) = (dirX / norma, dirY / norma);
        var meio = 0.35 * spacing;

        var cauda = (x - ux * meio, y - uy * meio);
        var ponta = (x + ux * meio, y + uy * meio);

        var aba = 0.25 * 2 * meio;
        var angulo = 25 * Math.PI / 180;
        var (c, s) = (Math.Cos(angulo), Math.Sin(angulo));

        // As abas: a direção de volta (−u) girada ±25°.
        var esquerda = (ponta.Item1 + aba * (-ux * c - uy * s), ponta.Item2 + aba * (-uy * c + ux * s));
        var direita = (ponta.Item1 + aba * (-ux * c + uy * s), ponta.Item2 + aba * (-uy * c - ux * s));

        return new ArrowShape(cauda, ponta, esquerda, direita);
    }
}
