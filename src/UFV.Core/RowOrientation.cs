using UFV.Geo;

namespace UFV.Core;

/// <summary>
/// Como uma mesa colocada numa fileira se orienta: para que lado sobe.
/// </summary>
/// <param name="UpslopeAzimuthRadians">
/// O azimute do eixo que sobe a inclinação da mesa, do norte e horário — o
/// que <see cref="Transform.Azimuth"/> e <see cref="SystemConfiguration.UpslopeAzimuthRadians"/>
/// falam. É um dos dois perpendiculares à fileira: o mais próximo do que a
/// configuração pede.
/// </param>
/// <param name="LowEdgeOnNearSide">
/// Se a ponta baixa da mesa fica na borda mais próxima da linha de
/// alinhamento (a origem de <see cref="PlacedTable"/>). Quando não, a ponta
/// baixa está na borda de lá, e a mesa olha para a linha.
/// </param>
/// <param name="DivergenceRadians">
/// Quanto a subida escolhida difere da que a configuração pede, em radianos,
/// de 0 a 90°. Zero quando a linha foi traçada perpendicular ao azimute;
/// perto de 90° quando ela foi traçada quase paralela a ele, e aí a mesa vai
/// olhar para um lado bem diferente do configurado — é para avisar.
/// </param>
public sealed record RowOrientation(
    double UpslopeAzimuthRadians,
    bool LowEdgeOnNearSide,
    double DivergenceRadians)
{
    /// <summary>
    /// Resolve a orientação de uma mesa a partir da direção da fileira e do
    /// azimute de subida que a configuração pede.
    ///
    /// A mesa fica alinhada com a fileira, sempre: a célula reservada pela
    /// distribuição tem exatamente o comprimento por o fundo, e girá-la
    /// invadiria a vizinha. O que o azimute da configuração decide é qual dos
    /// dois lados perpendiculares à fileira é a subida — e é aqui, num lugar
    /// só, que a direção matemática da fileira (do +X, anti-horária) vira
    /// azimute topográfico (do norte, horário).
    /// </summary>
    /// <param name="directionRadians">A direção da fileira, como em <see cref="PlacedTable.DirectionRadians"/>.</param>
    /// <param name="side">De que lado da linha ficam as mesas, como em <see cref="RowDistributor.Distribute"/>.</param>
    /// <param name="preferredUpslopeAzimuthRadians">O azimute de subida da configuração.</param>
    public static RowOrientation Resolve(double directionRadians, LineSide side, double preferredUpslopeAzimuthRadians)
    {
        if (!double.IsFinite(directionRadians))
            throw new ArgumentOutOfRangeException(nameof(directionRadians), "A direção não é um número.");

        if (!double.IsFinite(preferredUpslopeAzimuthRadians))
            throw new ArgumentOutOfRangeException(nameof(preferredUpslopeAzimuthRadians), "O azimute não é um número.");

        if (side == LineSide.On)
            throw new ArgumentException("\"Em cima da linha\" não é lado para as mesas.", nameof(side));

        var dx = Math.Cos(directionRadians);
        var dy = Math.Sin(directionRadians);

        // A normal para o lado das mesas, a mesma convenção do distribuidor:
        // a direita do sentido da linha é (dy, −dx).
        var (nx, ny) = side == LineSide.Right ? (dy, -dx) : (-dy, dx);

        // O vetor do azimute pedido, em (X = leste, Y = norte).
        var px = Math.Sin(preferredUpslopeAzimuthRadians);
        var py = Math.Cos(preferredUpslopeAzimuthRadians);

        // A subida é +normal (ponta baixa na borda de cá) ou −normal (na de
        // lá): o que fizer o menor ângulo com o pedido.
        var produto = nx * px + ny * py;
        var pontaBaixaDeCa = produto >= 0;

        var (ux, uy) = pontaBaixaDeCa ? (nx, ny) : (-nx, -ny);

        var azimute = Math.Atan2(ux, uy);
        if (azimute < 0) azimute += 2 * Math.PI;

        var divergencia = Math.Acos(Math.Clamp(Math.Abs(produto), 0, 1));

        return new RowOrientation(azimute, pontaBaixaDeCa, divergencia);
    }
}
