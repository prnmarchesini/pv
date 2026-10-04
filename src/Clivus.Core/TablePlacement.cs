using Clivus.Geo;

namespace Clivus.Core;

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
    public static Transform Plan(PlacedTable table, RowOrientation orientation, double tiltRadians, double elevation) =>
        PlanSolved(table, orientation, tiltRadians, elevation, elevation, table.Length, out _);

    /// <summary>
    /// A colocação da mesa resolvida: com a cota da ponta baixa no início e
    /// no fim escolhidas pela fileira (5.4). O giro longitudinal sai do
    /// desnível sobre o comprimento LOCAL da mesa (seno, e não tangente):
    /// assim a cota da ponta baixa na estação local s é exatamente
    /// início + (fim − início)·s/L, a reta que o 5.3 usou para contar
    /// módulos fora da faixa. O que o 5.3 NÃO tem é a posição em planta com
    /// giro: a estação s fica em s·cos(giro) ao longo da fileira, até 28 cm
    /// antes de onde o terreno foi amostrado a 10°; por isso o relatório
    /// (5.6) reamostra a ponta baixa na posição final. O comprimento em
    /// planta encurta para L·cos(giro), que é o preço de a mesa ser rígida.
    /// </summary>
    /// <param name="table">A célula em planta.</param>
    /// <param name="orientation">Para que lado a mesa sobe.</param>
    /// <param name="tiltRadians">A inclinação transversal.</param>
    /// <param name="startElevation">A cota da ponta baixa na estação zero.</param>
    /// <param name="endElevation">A cota da ponta baixa na estação final.</param>
    /// <param name="length">O comprimento local da mesa (o da geometria, não o da célula).</param>
    /// <param name="longitudinalTilt">O giro aplicado, em radianos.</param>
    /// <exception cref="ArgumentOutOfRangeException">Cotas não finitas, ou desnível maior que o comprimento.</exception>
    public static Transform PlanSolved(
        PlacedTable table, RowOrientation orientation, double tiltRadians,
        double startElevation, double endElevation, double length, out double longitudinalTilt)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(orientation);

        if (table.Corners.Count != 4 || !(table.Length >= RowDistributor.MenorMedida))
            throw new ArgumentException("A célula da mesa não tem quatro cantos ou é curta demais.", nameof(table));

        if (!double.IsFinite(startElevation))
            throw new ArgumentOutOfRangeException(nameof(startElevation), startElevation, "A cota não é um número.");

        if (!double.IsFinite(endElevation))
            throw new ArgumentOutOfRangeException(nameof(endElevation), endElevation, "A cota não é um número.");

        if (!double.IsFinite(length) || length < RowDistributor.MenorMedida)
            throw new ArgumentOutOfRangeException(nameof(length), length, "O comprimento da mesa não é uma medida válida.");

        if (Math.Abs(length - table.Length) > 1e-6)
        {
            throw new ArgumentException(
                $"A célula tem {table.Length:0.###} m e a mesa {length:0.###} m: a célula é de outra mesa.",
                nameof(length));
        }

        var desnivel = endElevation - startElevation;

        // Igual ao comprimento seria a mesa em pé, com comprimento em planta
        // zero; recusado com folga.
        if (Math.Abs(desnivel) >= length - 1e-9)
        {
            throw new ArgumentOutOfRangeException(nameof(endElevation), desnivel,
                "O desnível entre as pontas é maior que o comprimento da mesa: não há giro que o produza.");
        }

        longitudinalTilt = Math.Asin(desnivel / length);

        return Montar(table, orientation, tiltRadians, startElevation, longitudinalTilt);
    }

    private static Transform Montar(PlacedTable table, RowOrientation orientation, double tiltRadians, double elevation, double giro)
    {
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

        return Transform.PlaceSolved(
            tiltRadians,
            giro,
            orientation.UpslopeAzimuthRadians,
            new Point3(origem.X, origem.Y, elevation));
    }
}
