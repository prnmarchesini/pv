namespace Clivus.Geo;

/// <summary>Para que ponta da fileira o cabo da string vai (18.2 e 18.3).</summary>
public enum RowEnd
{
    /// <summary>A ponta do começo da fileira (contra a direção da mesa: do canto 1 para o 0).</summary>
    Start,

    /// <summary>A ponta do fim da fileira (a favor da direção da mesa: do canto 0 para o 1).</summary>
    End,
}

/// <summary>
/// A saída do cabo de uma ponta de string (roteamento, 18.1 e regra 2): do
/// módulo, reto para fora da mesa pela borda comprida mais perto (é o
/// caminho mais curto para fora; nunca ao longo do miolo), depois por fora,
/// paralelo à fileira, até passar da ponta dela. Dali o cabo segue reto na
/// mesma direção até bater na vala (<see cref="TrenchNetwork.Ray"/>).
/// </summary>
public static class RowExit
{
    /// <summary>A folga (m) entre o cabo e a borda da mesa, e além da ponta da fileira.</summary>
    public const double Margin = 0.5;

    /// <summary>
    /// Os pontos de planta depois do módulo <paramref name="from"/>: a saída
    /// da mesa e a ponta da fileira; e a direção (unitária, em planta) em que
    /// o cabo segue para a vala. <paramref name="corners"/>: os 4 cantos da
    /// mesa (0 e 1 a borda baixa, do começo ao fim; 2 e 3 a borda alta).
    /// <paramref name="rowCorners"/>: os cantos de todas as mesas da fileira
    /// (esta inclusive), para achar as pontas dela.
    /// </summary>
    public static (IReadOnlyList<Point3> Points, Point3 Direction) Plan(
        Point3 from, IReadOnlyList<Point3> corners, IEnumerable<Point3> rowCorners, RowEnd end, double margin = Margin)
    {
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentNullException.ThrowIfNull(rowCorners);
        if (corners.Count < 4) throw new ArgumentException("A mesa precisa dos 4 cantos.", nameof(corners));

        var (ux, uy) = Unit(corners[1].X - corners[0].X, corners[1].Y - corners[0].Y);
        var fileira = rowCorners.Concat(corners).ToList();

        // A coordenada lateral (na normal esquerda n = (-uy, ux)) de um ponto.
        double Lado(Point3 p) => -p.X * uy + p.Y * ux;

        // Sai pela borda comprida mais perto do módulo: a baixa (0-1) ou a alta (3-2).
        var aBaixa = Lado(from) - (Lado(corners[0]) + Lado(corners[1])) / 2;
        var aAlta = Lado(from) - (Lado(corners[2]) + Lado(corners[3])) / 2;
        var (aEsta, aOutra) = Math.Abs(aBaixa) <= Math.Abs(aAlta) ? (aBaixa, aAlta) : (aAlta, aBaixa);
        var fora = -Math.Sign(aEsta - aOutra);
        if (fora == 0) fora = -1;

        // A linha de fora passa além de TODAS as mesas da fileira desse lado (mesa
        // vizinha deslocada, mais larga ou torta não fica embaixo do cabo).
        var laterais = fileira.Select(Lado).ToList();
        var alvoLateral = fora > 0 ? laterais.Max() + margin : laterais.Min() - margin;
        var avanco = alvoLateral - Lado(from);
        var saida = new Point3(from.X - uy * avanco, from.Y + ux * avanco, 0);

        // As pontas da fileira ao longo da direção da mesa.
        var longo = fileira.Select(c => c.X * ux + c.Y * uy).ToList();
        var alvo = end == RowEnd.End ? longo.Max() + margin : longo.Min() - margin;
        var andar = alvo - (saida.X * ux + saida.Y * uy);
        var ponta = new Point3(saida.X + ux * andar, saida.Y + uy * andar, 0);

        var direcao = end == RowEnd.End ? new Point3(ux, uy, 0) : new Point3(-ux, -uy, 0);
        return ([saida, ponta], direcao);
    }

    private static (double X, double Y) Unit(double x, double y)
    {
        var n = Math.Sqrt(x * x + y * y);
        if (n < 1e-9) throw new ArgumentException("A mesa não tem direção (cantos 0 e 1 iguais).");
        return (x / n, y / n);
    }
}
