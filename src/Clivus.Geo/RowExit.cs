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
/// Desde o item 10 (10/10/2026), quem roteia passa o lado da usina (o alto
/// das mesas) e o ponto de juntar da mesa: uma entrada só por mesa.
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
    /// <remarks>
    /// <paramref name="side"/> (Renan, 10/10/2026, item 10: "cada mesa com uma
    /// única entrada de cabo"): a direção em planta do lado por onde a usina
    /// sai; a borda comprida da mesa que olha para ela é a da saída, para
    /// todas as strings. Null (ou perpendicular à mesa): a borda mais perto do
    /// módulo, como era. <paramref name="gather"/>: a coordenada, ao longo da
    /// mesa, do ponto de juntar da mesa (a ponta de string mais adiantada para
    /// <paramref name="end"/>); quando ela fica entre a saída deste módulo e a
    /// ponta da fileira, o ponto entra no percurso, e todas as strings da mesa
    /// passam por ele.
    /// </remarks>
    public static (IReadOnlyList<Point3> Points, Point3 Direction) Plan(
        Point3 from, IReadOnlyList<Point3> corners, IEnumerable<Point3> rowCorners, RowEnd end, double margin = Margin, Point3? side = null, double? gather = null)
    {
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentNullException.ThrowIfNull(rowCorners);
        if (corners.Count < 4) throw new ArgumentException("A mesa precisa dos 4 cantos.", nameof(corners));

        var (ux, uy) = Unit(corners[1].X - corners[0].X, corners[1].Y - corners[0].Y);
        var fileira = rowCorners.Concat(corners).ToList();

        // A coordenada lateral (na normal esquerda n = (-uy, ux)) de um ponto.
        double Lado(Point3 p) => -p.X * uy + p.Y * ux;

        // Sai pela borda comprida que olha para o lado da usina; sem ele, pela
        // mais perto do módulo: a baixa (0-1) ou a alta (3-2).
        var aBaixa = Lado(from) - (Lado(corners[0]) + Lado(corners[1])) / 2;
        var aAlta = Lado(from) - (Lado(corners[2]) + Lado(corners[3])) / 2;
        var (aEsta, aOutra) = Math.Abs(aBaixa) <= Math.Abs(aAlta) ? (aBaixa, aAlta) : (aAlta, aBaixa);
        var fora = -Math.Sign(aEsta - aOutra);
        if (fora == 0) fora = -1;
        if (side is { } s && Math.Abs(-s.X * uy + s.Y * ux) > 1e-6) fora = Math.Sign(-s.X * uy + s.Y * ux);

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

        // O ponto de juntar da mesa, se ele fica no caminho (entre a saída e a ponta da fileira).
        if (gather is { } g)
        {
            var aqui = saida.X * ux + saida.Y * uy;
            var adiante = (g - aqui) * (end == RowEnd.End ? 1 : -1);
            if (adiante > 1e-6 && adiante < Math.Abs(andar) - 1e-6)
                return ([saida, new Point3(saida.X + ux * (g - aqui), saida.Y + uy * (g - aqui), 0), ponta], direcao);
        }

        return ([saida, ponta], direcao);
    }

    /// <summary>
    /// O lado alto da mesa (item 10, 10/10/2026): a direção em planta, para
    /// fora da mesa, da borda comprida mais alta (pela cota real dos cantos:
    /// na estrutura fixa, a borda de cima do módulo). Null se a mesa não tem
    /// os 4 cantos ou se as duas bordas estão na mesma cota (até 1 mm).
    /// </summary>
    public static Point3? HighSide(IReadOnlyList<Point3> corners)
    {
        ArgumentNullException.ThrowIfNull(corners);
        if (corners.Count < 4) return null;
        var dx = corners[1].X - corners[0].X;
        var dy = corners[1].Y - corners[0].Y;
        var n = Math.Sqrt(dx * dx + dy * dy);
        if (n < 1e-9) return null;
        var (ux, uy) = (dx / n, dy / n);

        var baixa = (corners[0].Z + corners[1].Z) / 2;
        var alta = (corners[2].Z + corners[3].Z) / 2;
        if (Math.Abs(alta - baixa) < 1e-3) return null;

        // A normal esquerda aponta da borda 0-1 para a 2-3 quando os cantos giram no sentido anti-horário.
        double Lado(Point3 p) => -p.X * uy + p.Y * ux;
        var paraAAlta = Math.Sign((Lado(corners[2]) + Lado(corners[3])) / 2 - (Lado(corners[0]) + Lado(corners[1])) / 2);
        if (paraAAlta == 0) return null;
        var sinal = alta > baixa ? paraAAlta : -paraAAlta;
        return new Point3(-uy * sinal, ux * sinal, 0);
    }

    private static (double X, double Y) Unit(double x, double y)
    {
        var n = Math.Sqrt(x * x + y * y);
        if (n < 1e-9) throw new ArgumentException("A mesa não tem direção (cantos 0 e 1 iguais).");
        return (x / n, y / n);
    }
}
