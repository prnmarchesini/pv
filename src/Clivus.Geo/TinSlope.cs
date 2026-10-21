namespace Clivus.Geo;

/// <summary>
/// A declividade do terreno num ponto: o gradiente do plano do triângulo que
/// contém o ponto (passo 25.3 da drenagem).
///
/// Dentro de um triângulo o terreno é um plano, então a declividade é a
/// mesma em todo ele; muda de um triângulo para o outro. A água corre na
/// direção contrária ao gradiente.
/// </summary>
/// <param name="DzDx">Quanto a cota sobe por metro para o leste (m/m).</param>
/// <param name="DzDy">Quanto a cota sobe por metro para o norte (m/m).</param>
public readonly record struct SurfaceSlope(double DzDx, double DzDy)
{
    /// <summary>Declividade máxima, em m/m (tangente).</summary>
    public double Ratio => Math.Sqrt(DzDx * DzDx + DzDy * DzDy);

    /// <summary>Declividade máxima, em %.</summary>
    public double Percent => Ratio * 100;

    /// <summary>Declividade máxima, em graus.</summary>
    public double Degrees => Math.Atan(Ratio) * 180 / Math.PI;

    /// <summary>Componente leste da direção de descida, em planta, unitária. Zero no plano.</summary>
    public double DownhillX => Ratio > 0 ? -DzDx / Ratio : 0;

    /// <summary>Componente norte da direção de descida, em planta, unitária. Zero no plano.</summary>
    public double DownhillY => Ratio > 0 ? -DzDy / Ratio : 0;

    /// <summary>Se a declividade é menor que <paramref name="limite"/> (m/m): a água não tem para onde correr.</summary>
    public bool IsFlat(double limite) => Ratio < limite;

    /// <summary>O gradiente do plano que passa pelos três vértices. Zero se o triângulo não tem área em planta.</summary>
    public static SurfaceSlope Of(in Triangle t)
    {
        var (ax, ay, az) = (t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z);
        var (bx, by, bz) = (t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z);

        var denominador = ax * by - bx * ay;
        if (Math.Abs(denominador) < 1e-15) return default;

        return new SurfaceSlope(
            (az * by - bz * ay) / denominador,
            (ax * bz - bx * az) / denominador);
    }
}

/// <summary>A declividade como pergunta ao terreno.</summary>
public static class TinSlope
{
    /// <summary>
    /// A declividade do terreno em (x, y). Falso fora do terreno: fora não é
    /// plano, é ausência de resposta.
    /// </summary>
    public static bool TryGetSlope(this Tin tin, double x, double y, out SurfaceSlope slope)
    {
        ArgumentNullException.ThrowIfNull(tin);

        slope = default;
        if (!tin.TryFindTriangle(x, y, out var i)) return false;

        slope = SurfaceSlope.Of(tin.Triangles[i]);
        return true;
    }
}
