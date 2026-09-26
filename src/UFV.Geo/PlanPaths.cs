namespace UFV.Geo;

/// <summary>
/// Operações sobre linhas em planta que ignoram a cota.
/// </summary>
public static class PlanPaths
{
    /// <summary>
    /// Tira os vértices que estão em cima da reta entre os vizinhos, em
    /// planta. O que sobra são os vértices em que a linha muda de direção.
    ///
    /// Existe por causa do drapeamento: a linha de alinhamento e a área
    /// gravadas no desenho são as drapejadas, com um vértice em cada aresta
    /// do terreno que cruzam — dezenas por trecho traçado. Para a
    /// distribuição em planta, cada vértice a mais é um "trecho" a mais, e
    /// um trecho de 2 m com o mesmo rumo do vizinho virava uma família de
    /// fileiras com sobreposição. Os vértices drapejados estão exatamente na
    /// reta do traçado em planta (só a cota muda), e é isso que se usa para
    /// reconhecê-los.
    /// </summary>
    /// <param name="vertices">A linha. Numa linha aberta as pontas ficam sempre.</param>
    /// <param name="tolerance">Quanto um vértice pode se afastar da reta e ainda contar como em cima dela, em metro.</param>
    /// <param name="closed">
    /// Se a linha é um polígono fechado SEM o primeiro vértice repetido no
    /// fim (como a área do plugin é gravada): aí o último e o primeiro
    /// também são conferidos contra os vizinhos pela volta, e saem se
    /// estiverem na reta.
    /// </param>
    public static IReadOnlyList<Point3> SimplifyCollinear(IReadOnlyList<Point3> vertices, double tolerance = 1e-6, bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (!double.IsFinite(tolerance) || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "A tolerância não é uma medida.");

        if (vertices.Count < 3) return vertices;

        var mantidos = new List<Point3> { vertices[0] };

        for (var i = 1; i + 1 < vertices.Count; i++)
        {
            var anterior = mantidos[^1];
            var atual = vertices[i];
            var proximo = vertices[i + 1];

            if (!EmCimaDaReta(anterior, proximo, atual, tolerance)) mantidos.Add(atual);
        }

        mantidos.Add(vertices[^1]);

        if (!closed) return mantidos;

        // A volta: o último contra (penúltimo, primeiro) e o primeiro contra
        // (último, segundo). Enquanto houver três vértices.
        if (mantidos.Count > 3 && EmCimaDaReta(mantidos[^2], mantidos[0], mantidos[^1], tolerance))
            mantidos.RemoveAt(mantidos.Count - 1);

        if (mantidos.Count > 3 && EmCimaDaReta(mantidos[^1], mantidos[1], mantidos[0], tolerance))
            mantidos.RemoveAt(0);

        return mantidos;
    }

    /// <summary>
    /// Se o ponto está em cima do segmento a–b em planta, dentro da
    /// tolerância, e ENTRE as pontas (não na reta prolongada: um vértice que
    /// volta por cima do trecho anterior muda de direção e fica).
    /// </summary>
    private static bool EmCimaDaReta(Point3 a, Point3 b, Point3 p, double tolerancia)
    {
        var ex = b.X - a.X;
        var ey = b.Y - a.Y;
        var comprimento2 = ex * ex + ey * ey;

        if (comprimento2 <= tolerancia * tolerancia) return Math.Abs(p.X - a.X) <= tolerancia && Math.Abs(p.Y - a.Y) <= tolerancia;

        var comprimento = Math.Sqrt(comprimento2);
        var s = ((p.X - a.X) * ex + (p.Y - a.Y) * ey) / comprimento2;

        // A folga é em metro, e s é adimensional: comparar s·L, não s. Com s
        // cru, um ponto podia estar tolerância × L além da ponta (um metro
        // num trecho de um quilômetro) e ainda contar como em cima.
        if (s * comprimento < -tolerancia || s * comprimento > comprimento + tolerancia) return false;

        var px = a.X + s * ex - p.X;
        var py = a.Y + s * ey - p.Y;

        return px * px + py * py <= tolerancia * tolerancia;
    }
}
