namespace Clivus.Core;

/// <summary>
/// Onde e de que tamanho vai a tag da string (Renan, 07/10/2026: "jamais
/// quero texto virado, se for o caso, diminua o tamanho da fonte"). A tag
/// segue o eixo comprido dos módulos da string (o da mesa), nunca o caminho
/// do primeiro ao último módulo: a string em U volta para o lado de onde
/// saiu, e esse caminho dava 90° com a mesa (a tag em pé). Se o texto passa
/// do comprimento da string, a fonte diminui.
/// </summary>
public static class StringTagLayout
{
    /// <summary>
    /// O eixo comprido dos centros dos módulos (a direção de maior espalhamento),
    /// legível (entre -90° e 90°, em radianos), e o comprimento que a string
    /// ocupa nele: do primeiro ao último centro mais um passo entre módulos.
    /// Um módulo só (ou todos no mesmo ponto): ângulo 0 e comprimento 0.
    /// </summary>
    public static (double Angle, double Span) Axis(IReadOnlyList<(double X, double Y)> centers)
    {
        ArgumentNullException.ThrowIfNull(centers);
        if (centers.Count < 2) return (0, 0);

        var mx = centers.Average(c => c.X);
        var my = centers.Average(c => c.Y);
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var (x, y) in centers)
        {
            sxx += (x - mx) * (x - mx);
            syy += (y - my) * (y - my);
            sxy += (x - mx) * (y - my);
        }

        if (sxx + syy < 1e-12) return (0, 0);

        var angulo = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        if (angulo > Math.PI / 2 - 1e-9) angulo -= Math.PI;
        else if (angulo <= -Math.PI / 2 + 1e-9) angulo += Math.PI;

        // Quase deitada é deitada: o ruído dos centros não entorta a tag.
        if (Math.Abs(angulo) < 1e-6) angulo = 0;

        var (cos, sin) = (Math.Cos(angulo), Math.Sin(angulo));
        var projecoes = centers.Select(c => (c.X - mx) * cos + (c.Y - my) * sin).OrderBy(v => v).ToList();

        // O passo: o menor salto entre projeções distintas (a largura de um módulo).
        var passo = 0.0;
        for (var i = 1; i < projecoes.Count; i++)
        {
            var salto = projecoes[i] - projecoes[i - 1];
            if (salto > 1e-3 && (passo == 0 || salto < passo)) passo = salto;
        }

        return (angulo, projecoes[^1] - projecoes[0] + passo);
    }

    /// <summary>
    /// A altura da fonte para o texto caber no comprimento: a mesma se cabe
    /// (ou se não há comprimento para medir); senão, na proporção.
    /// </summary>
    public static double FitHeight(double height, double textWidth, double span)
    {
        if (height <= 0 || textWidth <= 0 || span <= 0 || textWidth <= span) return height;
        return height * span / textWidth;
    }
}
