using Clivus.Geo;

namespace Clivus.Core;

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
/// Se a ponta baixa da mesa fica na borda da célula onde está a origem de
/// <see cref="PlacedTable"/> (canto 0 e 1). Quando não, a ponta baixa está
/// na borda de lá (cantos 3 e 2). Com o distribuidor de 26/09/2026, que põe
/// o fundo da célula no sentido do azimute, é sempre verdadeiro.
/// </param>
/// <param name="DivergenceRadians">
/// Quanto a subida escolhida difere da que a configuração pede, em radianos,
/// de 0 a 90°. Com o distribuidor atual é zero; ficou para uma célula que
/// venha de outro lugar (uma mesa movida à mão, etapa 7).
/// </param>
/// <param name="LengthRunsWithRow">
/// Se o comprimento local da mesa (+X, da estação zero à final) corre no
/// sentido da fileira. Quando não, a estação zero de cada mesa fica na
/// ponta de LÁ da célula, e a junta com a mesa seguinte na fileira é entre
/// o início local desta e o fim local daquela — quem encadeia cotas ao
/// longo da fileira (5.4) precisa saber. Na configuração padrão (fileira
/// para o leste, mesa olhando para o norte) é falso: +X local corre para
/// oeste.
/// </param>
public sealed record RowOrientation(
    double UpslopeAzimuthRadians,
    bool LowEdgeOnNearSide,
    double DivergenceRadians,
    bool LengthRunsWithRow)
{
    /// <summary>
    /// Resolve a orientação de uma mesa a partir da célula em planta e do
    /// azimute de subida que a configuração pede.
    ///
    /// A mesa fica alinhada com a fileira, sempre: a célula reservada pela
    /// distribuição tem exatamente o comprimento por o fundo, e girá-la
    /// invadiria a vizinha. A normal da célula é lida dos cantos (canto 3 −
    /// canto 0); o azimute da configuração decide se a subida é +normal ou
    /// −normal — e é aqui, num lugar só, que a direção matemática da fileira
    /// (do +X, anti-horária) vira azimute topográfico (do norte, horário).
    /// </summary>
    /// <param name="cell">A célula, como o distribuidor a colocou.</param>
    /// <param name="preferredUpslopeAzimuthRadians">O azimute de subida da configuração.</param>
    public static RowOrientation Resolve(PlacedTable cell, double preferredUpslopeAzimuthRadians)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (!double.IsFinite(cell.DirectionRadians))
            throw new ArgumentOutOfRangeException(nameof(cell), "A direção não é um número.");

        if (!double.IsFinite(preferredUpslopeAzimuthRadians))
            throw new ArgumentOutOfRangeException(nameof(preferredUpslopeAzimuthRadians), "O azimute não é um número.");

        if (cell.Corners is not { Count: 4 } || cell.PlanDepth <= 0)
            throw new ArgumentException("A célula não tem quatro cantos com fundo.", nameof(cell));

        var dx = Math.Cos(cell.DirectionRadians);
        var dy = Math.Sin(cell.DirectionRadians);

        // A normal da célula, dos cantos: o fundo, unitário.
        var nx = (cell.Corners[3].X - cell.Corners[0].X) / cell.PlanDepth;
        var ny = (cell.Corners[3].Y - cell.Corners[0].Y) / cell.PlanDepth;

        if (!double.IsFinite(nx) || !double.IsFinite(ny) || Math.Abs(nx * nx + ny * ny - 1) > 1e-6)
            throw new ArgumentException("O fundo da célula não fecha com o fundo declarado.", nameof(cell));

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

        // O +X local é a subida girada 90° no sentido horário: (uy, −ux).
        var comprimentoComAFileira = (uy * dx - ux * dy) > 0;

        return new RowOrientation(azimute, pontaBaixaDeCa, divergencia, comprimentoComAFileira);
    }
}
