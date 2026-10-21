using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>
/// Terrenos analíticos da drenagem (etapas 25 a 30): uma grade quadriculada,
/// duas faces por célula, com a cota de uma função conhecida. A resposta
/// certa de cada teste sai da função, não do motor.
/// </summary>
internal static class TerrenoDeDrenagem
{
    /// <summary>Grade de <paramref name="n"/> × <paramref name="n"/> células de <paramref name="passo"/> m, a partir de (x0, y0).</summary>
    internal static Tin Grade(Func<double, double, double> z, double x0, double y0, int n, double passo)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));

        var triangulos = new List<Triangle>(n * n * 2);

        for (var i = 0; i < n; i++)
        for (var j = 0; j < n; j++)
        {
            var x = x0 + i * passo;
            var y = y0 + j * passo;

            triangulos.Add(new Triangle(P(x, y), P(x + passo, y), P(x + passo, y + passo)));
            triangulos.Add(new Triangle(P(x, y), P(x + passo, y + passo), P(x, y + passo)));
        }

        return new Tin(triangulos);
    }

    /// <summary>Rampa de 5 % descendo para o leste: z = 100 − 0,05·x, de 0 a 200 m.</summary>
    internal static Tin Rampa() => Grade((x, _) => 100 - 0.05 * x, 0, 0, 20, 10);

    /// <summary>Plano horizontal a 50 m, de 0 a 200 m.</summary>
    internal static Tin Plano() => Grade((_, _) => 50, 0, 0, 20, 10);

    /// <summary>
    /// Vale em V com o talvegue no eixo x = 100: as encostas caem 10 % para o
    /// talvegue, e o talvegue cai 2 % para o norte. De 0 a 200 m.
    /// </summary>
    internal static Tin Vale() => Grade((x, y) => 100 + 0.10 * Math.Abs(x - 100) - 0.02 * y, 0, 0, 20, 10);

    /// <summary>
    /// Telhado de duas águas com a cumeeira em x = 100: cai 4 % para o oeste e
    /// 4 % para o leste. De 0 a 200 m.
    /// </summary>
    internal static Tin Telhado() => Grade((x, _) => 100 - 0.04 * Math.Abs(x - 100), 0, 0, 20, 10);

    /// <summary>Bacia fechada: um funil com o fundo em (100, 100), 3 % em todas as direções.</summary>
    internal static Tin Funil() => Grade((x, y) => 50 + 0.03 * Math.Sqrt((x - 100) * (x - 100) + (y - 100) * (y - 100)), 0, 0, 40, 5);

    /// <summary>Morro gaussiano no centro (100, 100), 20 m de altura, raio 40 m.</summary>
    internal static Tin Morro() => Grade((x, y) => 50 + 20 * Math.Exp(-((x - 100) * (x - 100) + (y - 100) * (y - 100)) / (2 * 40 * 40)), 0, 0, 40, 5);
}
