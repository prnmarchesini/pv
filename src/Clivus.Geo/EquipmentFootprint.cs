namespace Clivus.Geo;

/// <summary>
/// O retângulo de um equipamento elétrico em campo (subestação, trafo,
/// inversor; elétrica 12.3, 13.2 e 14.6): um símbolo, não o equipamento. O
/// centro é o ponto clicado em planta; a base flutua <see cref="FloatHeight"/>
/// acima da cota do TERRENO nesse ponto (regra sagrada 5: a cota do clique não
/// vale); a largura corre em X e o comprimento em Y.
/// </summary>
public static class EquipmentFootprint
{
    /// <summary>Quanto a base do retângulo fica acima do terreno, em metros.</summary>
    public const double FloatHeight = 0.80;

    /// <summary>Menor letra da tag, em metros: abaixo disso ninguém lê de cima.</summary>
    public const double MinTagHeight = 0.05;

    /// <summary>A cota da base, dada a cota do terreno no centro.</summary>
    public static double BaseElevation(double terrainZ)
    {
        if (!double.IsFinite(terrainZ)) throw new ArgumentOutOfRangeException(nameof(terrainZ), "cota do terreno inválida");
        return terrainZ + FloatHeight;
    }

    /// <summary>Os quatro cantos em planta, anti-horário a partir do de baixo à esquerda.</summary>
    public static IReadOnlyList<(double X, double Y)> Corners(double centerX, double centerY, double width, double length)
    {
        Validar(width, length);

        var w = width / 2;
        var l = length / 2;
        return [(centerX - w, centerY - l), (centerX + w, centerY - l), (centerX + w, centerY + l), (centerX - w, centerY + l)];
    }

    /// <summary>
    /// Quantos cantos têm o terreno acima da base (o retângulo entraria no
    /// chão ali: terreno muito inclinado sob um equipamento grande). Canto
    /// fora do terreno (NaN) não conta.
    /// </summary>
    public static int BuriedCorners(double baseElevation, IEnumerable<double> cornerTerrainZ)
    {
        ArgumentNullException.ThrowIfNull(cornerTerrainZ);
        return cornerTerrainZ.Count(z => double.IsFinite(z) && z > baseElevation);
    }

    /// <summary>
    /// A altura da letra da tag escrita no topo: cabe na largura (cada letra
    /// ocupa por volta de 0,9 da altura) e em um terço do comprimento.
    /// </summary>
    public static double TagHeight(int characters, double width, double length)
    {
        Validar(width, length);

        var n = Math.Max(1, characters);
        var cabeNaLargura = 0.9 * width / (0.9 * n);
        return Math.Max(MinTagHeight, Math.Min(cabeNaLargura, length / 3));
    }

    private static void Validar(double width, double length)
    {
        if (!(width > 0) || !(length > 0) || !double.IsFinite(width + length))
            throw new ArgumentOutOfRangeException(nameof(width), "largura e comprimento têm que ser maiores que zero");
    }
}
