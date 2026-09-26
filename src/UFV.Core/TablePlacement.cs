using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// A matriz que leva a mesa local (deitada, origem na ponta baixa, X ao longo
/// do comprimento, Y subindo a inclinação) para a célula que a distribuição
/// reservou em planta.
///
/// A célula é um retângulo alinhado com a fileira; a mesa local, inclinada
/// e girada, precisa cair exatamente nele: o comprimento sobre a direção da
/// fileira, e o fundo em planta (fundo × cos da inclinação) sobre a normal.
/// O que decide é <see cref="RowOrientation"/>: para que lado a mesa sobe.
/// Com a ponta baixa na borda de cá, a origem local é um canto da borda de
/// cá; senão, um da borda de lá — e em cada caso o canto de onde o eixo +X
/// local parte, que é o que a matriz de azimute manda.
///
/// A cota da origem entra de fora. A amostragem (5.2) não precisa dela e
/// passa zero; quem coloca a mesa de verdade (5.4) passa a cota escolhida.
/// Um lugar só faz essa conta, e é testado contra os cantos da célula.
/// </summary>
public static class TablePlacement
{
    /// <summary>
    /// A colocação da mesa na célula.
    /// </summary>
    /// <param name="table">A célula em planta.</param>
    /// <param name="orientation">Para que lado a mesa sobe, resolvido a partir da fileira.</param>
    /// <param name="tiltRadians">A inclinação da mesa, de 0 a 90° exclusivo.</param>
    /// <param name="elevation">A cota da ponta baixa (origem local), em metro.</param>
    /// <exception cref="ArgumentOutOfRangeException">Inclinação fora de [0, 90°) ou cota não finita.</exception>
    /// <exception cref="ArgumentException">
    /// Célula sem quatro cantos ou curta demais, ou orientação que não é
    /// perpendicular à célula (resolvida a partir de outra fileira): a mesa
    /// sairia girada dentro da célula, amostrando terreno sob a vizinha.
    /// </exception>
    public static Transform Plan(PlacedTable table, RowOrientation orientation, double tiltRadians, double elevation)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(orientation);

        if (table.Corners.Count != 4 || !(table.Length >= RowDistributor.MenorMedida))
            throw new ArgumentException("A célula da mesa não tem quatro cantos ou é curta demais.", nameof(table));

        if (!double.IsFinite(tiltRadians) || tiltRadians < 0 || tiltRadians >= Math.PI / 2)
        {
            throw new ArgumentOutOfRangeException(nameof(tiltRadians), tiltRadians,
                "A inclinação precisa ficar entre 0 e 90 graus.");
        }

        if (!double.IsFinite(elevation))
            throw new ArgumentOutOfRangeException(nameof(elevation), elevation, "A cota não é um número.");

        var c = table.Corners;

        // Os eixos da célula, tirados dos cantos: d ao longo, n para o lado
        // de lá (o fundo).
        var dx = (c[1].X - c[0].X) / table.Length;
        var dy = (c[1].Y - c[0].Y) / table.Length;

        // A subida: +n com a ponta baixa de cá, −n com ela de lá.
        var ux = Math.Sin(orientation.UpslopeAzimuthRadians);
        var uy = Math.Cos(orientation.UpslopeAzimuthRadians);

        // A subida tem que ser perpendicular à célula. Não é: a orientação
        // foi resolvida com a direção de outra fileira, e a mesa sairia
        // girada dentro da célula.
        if (Math.Abs(ux * dx + uy * dy) > 1e-6)
        {
            throw new ArgumentException(
                "A orientação não é perpendicular à célula: foi resolvida para outra fileira.",
                nameof(orientation));
        }

        // O eixo +X local é a subida girada 90° no sentido horário (é o que
        // Transform.Azimuth faz com +X). Ele corre com d ou contra d.
        var xLocalComD = (uy * dx - ux * dy) > 0;

        // A borda da ponta baixa: de cá (c0→c1) ou de lá (c3→c2), as duas
        // no sentido de d. A origem é o canto de onde +X local parte.
        var origem = orientation.LowEdgeOnNearSide
            ? (xLocalComD ? c[0] : c[1])
            : (xLocalComD ? c[3] : c[2]);

        return Transform.Place(
            tiltRadians,
            orientation.UpslopeAzimuthRadians,
            new Point3(origem.X, origem.Y, elevation));
    }
}
