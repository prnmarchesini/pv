namespace Clivus.Geo;

/// <summary>
/// O cabo em 3D (roteamento, 17.5 e regra 9): sai do ponto de origem, vai
/// pela superfície até a vala, desce à profundidade dela, segue a vala
/// acompanhando o terreno, sobe e vai pela superfície até o destino. O
/// comprimento que conta é o deste percurso 3D, descidas e subidas incluídas.
/// </summary>
public static class CablePath
{
    /// <summary>De quanto em quanto (m) o terreno é lido ao longo da vala e da superfície.</summary>
    public const double Step = 1.0;

    /// <summary>
    /// Monta o percurso. <paramref name="toTrench"/>: os pontos de planta do
    /// caminho de superfície depois da origem, terminando na entrada da vala;
    /// <paramref name="inTrench"/>: o caminho pela vala, da entrada à saída;
    /// <paramref name="fromTrench"/>: os pontos de superfície depois da saída,
    /// antes do destino. <paramref name="ground"/> dá a cota do terreno (null
    /// fora dele). Null, com o primeiro ponto sem terreno em
    /// <paramref name="outside"/>, se algum trecho fica fora do terreno.
    /// </summary>
    public static IReadOnlyList<Point3>? Build(
        Point3 start,
        IReadOnlyList<Point3> toTrench,
        IReadOnlyList<Point3> inTrench,
        IReadOnlyList<Point3> fromTrench,
        Point3 end,
        double depth,
        Func<double, double, double?> ground,
        out Point3? outside,
        bool groundFromStart = false)
    {
        ArgumentNullException.ThrowIfNull(toTrench);
        ArgumentNullException.ThrowIfNull(inTrench);
        ArgumentNullException.ThrowIfNull(fromTrench);
        ArgumentNullException.ThrowIfNull(ground);

        if (inTrench.Count == 0 || toTrench.Count == 0) throw new ArgumentException("Caminho sem vala.");
        if (!double.IsFinite(depth) || depth < 0) throw new ArgumentOutOfRangeException(nameof(depth));

        var pontos = new List<Point3> { start };
        Point3? fora = null;

        void NaCota(IEnumerable<Point3> planta, double abaixo)
        {
            foreach (var p in planta)
            {
                if (ground(p.X, p.Y) is not { } z || !double.IsFinite(z))
                {
                    fora ??= p;
                    continue;
                }

                Somar(pontos, new Point3(p.X, p.Y, z - abaixo));
            }
        }

        // Da saída da mesa (CC) o cabo desce reto até o primeiro ponto; do
        // equipamento (<paramref name="groundFromStart"/>) vai acompanhando o terreno.
        // Dali, pela superfície até a entrada da vala, na cota do terreno.
        NaCota(groundFromStart ? Densificar(start, toTrench) : Densificar(toTrench[0], toTrench), 0);

        // Desce e segue a vala.
        NaCota(Densificar(inTrench[0], inTrench).Prepend(inTrench[0]), depth);

        // Sobe na saída e vai até o destino pela superfície, acompanhando o terreno.
        NaCota([inTrench[^1]], 0);
        NaCota(Densificar(inTrench[^1], fromTrench), 0);
        NaCota(Densificar(fromTrench.Count > 0 ? fromTrench[^1] : inTrench[^1], [end]).SkipLast(1), 0);
        Somar(pontos, end);

        outside = fora;
        return fora is null ? pontos : null;
    }

    /// <summary>O comprimento 3D de uma sequência de pontos.</summary>
    public static double Length(IReadOnlyList<Point3> pontos)
    {
        ArgumentNullException.ThrowIfNull(pontos);

        var total = 0.0;
        for (var i = 0; i + 1 < pontos.Count; i++)
        {
            var dx = pontos[i + 1].X - pontos[i].X;
            var dy = pontos[i + 1].Y - pontos[i].Y;
            var dz = pontos[i + 1].Z - pontos[i].Z;
            total += Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        return total;
    }

    /// <summary>Os pontos depois de <paramref name="from"/>, com um ponto a cada <see cref="Step"/> em cada trecho.</summary>
    private static IEnumerable<Point3> Densificar(Point3 from, IReadOnlyList<Point3> pontos)
    {
        var anterior = from;
        foreach (var p in pontos)
        {
            var d = Math.Sqrt((p.X - anterior.X) * (p.X - anterior.X) + (p.Y - anterior.Y) * (p.Y - anterior.Y));
            var n = (int)Math.Ceiling(d / Step);
            for (var i = 1; i < n; i++)
                yield return new Point3(anterior.X + (p.X - anterior.X) * i / n, anterior.Y + (p.Y - anterior.Y) * i / n, 0);
            yield return p;
            anterior = p;
        }
    }

    /// <summary>Acrescenta sem repetir o ponto anterior.</summary>
    private static void Somar(List<Point3> pontos, Point3 p)
    {
        var u = pontos[^1];
        if (Math.Abs(u.X - p.X) < 1e-9 && Math.Abs(u.Y - p.Y) < 1e-9 && Math.Abs(u.Z - p.Z) < 1e-9) return;
        pontos.Add(p);
    }
}
