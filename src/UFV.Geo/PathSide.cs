namespace UFV.Geo;

/// <summary>
/// O lado de uma linha de vários trechos (a polilinha do alinhamento), em
/// planta.
///
/// Existe porque o Renan, em 25/09/2026, disse que o alinhamento não precisa
/// ser reto: "eu posso fazer vários pontos". Uma linha quebrada não tem UM
/// lado: o mesmo ponto pode estar à esquerda de um trecho e à direita de
/// outro. O que ela tem é um lado <em>local</em>: o do trecho mais próximo do
/// ponto. É o que o usuário quer dizer quando clica "as mesas ficam aqui": ele
/// está apontando para o trecho ao lado do clique.
///
/// A convenção é a de <see cref="LineSides"/>: esquerda e direita de quem
/// caminha no sentido do traçado, trecho a trecho. Desenhar a mesma linha ao
/// contrário troca os lados de todos os trechos de uma vez, então guardar o
/// sentido continua bastando.
/// </summary>
public static class PathSides
{
    /// <summary>
    /// De que lado da linha o ponto está, decidido pelo trecho mais próximo
    /// dele em planta.
    ///
    /// Trechos de comprimento zero (dois cliques no mesmo lugar) são pulados:
    /// não têm direção, e portanto não têm lado.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se a linha não tem nenhum trecho com comprimento, ou se algum ponto
    /// não for finito.
    /// </exception>
    public static LineSide Of(IReadOnlyList<Point3> vertices, Point3 ponto)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        var trecho = NearestSegment(vertices, ponto);

        if (trecho is null)
        {
            throw new ArgumentOutOfRangeException(nameof(vertices), vertices.Count,
                "A linha não tem nenhum trecho com comprimento em planta: não há lado a decidir.");
        }

        return LineSides.Of(vertices[trecho.Value], vertices[trecho.Value + 1], ponto);
    }

    /// <summary>
    /// O índice do primeiro vértice do trecho mais próximo do ponto, em
    /// planta, ou null se nenhum trecho tem comprimento.
    /// </summary>
    public static int? NearestSegment(IReadOnlyList<Point3> vertices, Point3 ponto)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (!ponto.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(ponto), ponto, "O ponto tem coordenada inválida.");
        }

        int? melhor = null;
        var menor = double.PositiveInfinity;

        for (var i = 0; i + 1 < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[i + 1];

            if (!a.IsFinite || !b.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(vertices), i,
                    "A linha tem vértice com coordenada inválida.");
            }

            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var comprimento2 = dx * dx + dy * dy;

            if (comprimento2 <= LineSides.Tolerancia * LineSides.Tolerancia) continue;

            // Projeção do ponto sobre o trecho, presa entre as duas pontas: a
            // distância é até o SEGMENTO, e não até a reta que o contém. Sem
            // prender, um trecho curto e distante, cuja reta passa perto do
            // ponto, ganharia de um trecho longo que está de fato ao lado.
            var t = ((ponto.X - a.X) * dx + (ponto.Y - a.Y) * dy) / comprimento2;
            t = Math.Clamp(t, 0, 1);

            var px = a.X + dx * t - ponto.X;
            var py = a.Y + dy * t - ponto.Y;
            var distancia2 = px * px + py * py;

            if (distancia2 < menor)
            {
                menor = distancia2;
                melhor = i;
            }
        }

        return melhor;
    }

    /// <summary>O comprimento da linha em planta, somando os trechos.</summary>
    public static double PlanLength(IReadOnlyList<Point3> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        var total = 0.0;

        for (var i = 0; i + 1 < vertices.Count; i++)
        {
            var dx = vertices[i + 1].X - vertices[i].X;
            var dy = vertices[i + 1].Y - vertices[i].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }

        return total;
    }

    /// <summary>
    /// Por que esta linha é curta demais para servir de referência, ou null
    /// se ela serve. O critério é o de <see cref="LineSides.WhyTooShort"/>,
    /// aplicado ao comprimento total em planta.
    /// </summary>
    public static string? WhyTooShort(IReadOnlyList<Point3> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < 2) return "a linha precisa de pelo menos dois pontos";

        if (vertices.Any(v => !v.IsFinite)) return "a linha tem ponto com coordenada inválida";

        var comprimento = PlanLength(vertices);

        if (comprimento < LineSides.ComprimentoMinimo)
        {
            return $"a linha tem {comprimento:0.###} m em planta, e o mínimo para servir de "
                + $"referência é {LineSides.ComprimentoMinimo:0.##} m";
        }

        return null;
    }
}
