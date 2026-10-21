using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Um trecho de declividade do perfil: de onde a onde (distância desde a
/// montante), a cota nas duas pontas e a declividade, positiva quando desce
/// no sentido da água.
/// </summary>
public readonly record struct SlopeSegment(double From, double To, double ZFrom, double ZTo)
{
    public double Length => To - From;

    /// <summary>Quanto desce no trecho, em m (negativo se sobe).</summary>
    public double Drop => ZFrom - ZTo;

    /// <summary>Declividade em m/m, positiva descendo.</summary>
    public double Slope => Length > 0 ? Drop / Length : 0;

    public double Percent => Slope * 100;

    /// <summary>Declividade em graus, com o mesmo sinal.</summary>
    public double Degrees => Math.Atan(Slope) * 180 / Math.PI;
}

/// <summary>Os trechos de declividade de um perfil (27.3): o "4,3 % | 2,5°" escrito sobre cada um.</summary>
public static class ProfileSegments
{
    /// <summary>
    /// Corta o perfil em trechos de <paramref name="segmentLength"/> m a partir da
    /// montante. Um resto curto no fim (menos da metade do trecho) entra no
    /// trecho anterior, para não sair um "% | grau" de dois metros.
    /// </summary>
    public static IReadOnlyList<SlopeSegment> Split(Func<double, double> zAt, double length, double segmentLength)
    {
        ArgumentNullException.ThrowIfNull(zAt);

        if (!(length > 0)) return [];
        if (!(segmentLength > 0)) segmentLength = length;

        var cortes = new List<double> { 0 };
        for (var d = segmentLength; d < length - 1e-9; d += segmentLength) cortes.Add(d);
        cortes.Add(length);

        if (cortes.Count > 2 && cortes[^1] - cortes[^2] < segmentLength / 2) cortes.RemoveAt(cortes.Count - 2);

        var trechos = new List<SlopeSegment>(cortes.Count - 1);
        for (var i = 1; i < cortes.Count; i++)
            trechos.Add(new SlopeSegment(cortes[i - 1], cortes[i], zAt(cortes[i - 1]), zAt(cortes[i])));

        return trechos;
    }
}

/// <summary>Como o fundo da canaleta é assentado (28.2, 28.4).</summary>
public enum ChannelBottomMode
{
    /// <summary>A borda no terreno: fundo = terreno − altura. Trecho abaixo da mínima é avisado e pintado.</summary>
    FollowTerrain,

    /// <summary>O fundo desce sempre pelo menos a declividade mínima; onde o terreno não deixa, a canaleta aprofunda. Nunca sobe.</summary>
    MinimumSlope,
}

/// <summary>Um ponto do perfil da canaleta: terreno, fundo e topo da parede.</summary>
public readonly record struct ChannelProfilePoint(double Distance, double X, double Y, double Terrain, double Bottom)
{
    /// <summary>Quanto o fundo está abaixo do terreno, em m.</summary>
    public double Depth => Terrain - Bottom;
}

/// <summary>O perfil de uma canaleta (28.3): pontos, trechos e o que está fora da regra.</summary>
public sealed record ChannelProfileResult(
    IReadOnlyList<ChannelProfilePoint> Points,
    IReadOnlyList<SlopeSegment> Segments,
    double MinimumSlopeRule)
{
    public double Length => Points.Count == 0 ? 0 : Points[^1].Distance;

    /// <summary>A menor declividade de trecho do fundo (m/m), a que dá a lâmina mais alta.</summary>
    public double MinSlope => Segments.Count == 0 ? 0 : Segments.Min(s => s.Slope);

    /// <summary>A maior declividade de trecho do fundo (m/m), a que dá a velocidade mais alta.</summary>
    public double MaxSlope => Segments.Count == 0 ? 0 : Segments.Max(s => s.Slope);

    /// <summary>Menor que isto (m) não é contrapendente: é o arredondamento da cota.</summary>
    public const double RiseTolerance = 0.005;

    /// <summary>Trecho baixo mais curto que isto (m) não é avisado: é o vértice de uma aresta do terreno.</summary>
    public const double ShortestLowRun = 2;

    /// <summary>
    /// Os trechos que descem menos que a mínima, mas descem, ponto a ponto do
    /// fundo (não pela média do trecho de 20 m: uma contrapendente curta
    /// dentro de um trecho que desce na média passaria calada, regra 4).
    /// </summary>
    public IReadOnlyList<SlopeSegment> LowSegments => Corridas(false);

    /// <summary>Os trechos planos ou que sobem, ponto a ponto: a água para ali.</summary>
    public IReadOnlyList<SlopeSegment> InvertedSegments => Corridas(true);

    /// <summary>
    /// As corridas de intervalos vizinhos do fundo na mesma situação: subindo
    /// ou plano (<paramref name="invertidas"/>), ou descendo menos que a mínima.
    /// Corrida invertida que sobe menos que <see cref="RiseTolerance"/> no
    /// total e é curta, ou corrida baixa mais curta que
    /// <see cref="ShortestLowRun"/>, não conta.
    /// </summary>
    private List<SlopeSegment> Corridas(bool invertidas)
    {
        var corridas = new List<SlopeSegment>();
        int? inicio = null;

        bool Ruim(int i)
        {
            var (a, b) = (Points[i - 1], Points[i]);
            var dl = b.Distance - a.Distance;
            if (dl <= 1e-9) return false;
            var s = (a.Bottom - b.Bottom) / dl;
            return invertidas ? s <= 0 : s > 0 && s < MinimumSlopeRule - 1e-9;
        }

        void Fechar(int fim)
        {
            if (inicio is not { } i0) return;
            var (a, b) = (Points[i0 - 1], Points[fim]);
            var corrida = new SlopeSegment(a.Distance, b.Distance, a.Bottom, b.Bottom);

            var conta = invertidas
                ? -corrida.Drop >= RiseTolerance || corrida.Length >= ShortestLowRun
                : corrida.Length >= ShortestLowRun;

            if (conta) corridas.Add(corrida);
            inicio = null;
        }

        for (var i = 1; i < Points.Count; i++)
        {
            if (Ruim(i)) inicio ??= i;
            else Fechar(i - 1);
        }

        Fechar(Points.Count - 1);
        return corridas;
    }

    /// <summary>Declividade média do fundo, da montante à jusante (m/m).</summary>
    public double AverageSlope => Length > 0 ? (Points[0].Bottom - Points[^1].Bottom) / Length : 0;

    /// <summary>A maior profundidade do fundo abaixo do terreno, em m.</summary>
    public double MaxDepth => Points.Count == 0 ? 0 : Points.Max(p => p.Depth);

    /// <summary>A cota do fundo na distância dada.</summary>
    public double BottomAt(double distance) => Interpolar(distance, p => p.Bottom);

    /// <summary>A cota do terreno na distância dada.</summary>
    public double TerrainAt(double distance) => Interpolar(distance, p => p.Terrain);

    private double Interpolar(double distance, Func<ChannelProfilePoint, double> valor)
    {
        if (Points.Count == 0) return double.NaN;
        if (distance <= Points[0].Distance) return valor(Points[0]);
        if (distance >= Points[^1].Distance) return valor(Points[^1]);

        int lo = 0, hi = Points.Count - 1;
        while (hi - lo > 1)
        {
            var meio = (lo + hi) / 2;
            if (Points[meio].Distance <= distance) lo = meio; else hi = meio;
        }

        var (a, b) = (Points[lo], Points[hi]);
        var t = b.Distance > a.Distance ? (distance - a.Distance) / (b.Distance - a.Distance) : 0;
        return valor(a) + (valor(b) - valor(a)) * t;
    }
}

/// <summary>O fundo da canaleta a partir do perfil do terreno (28.3, 28.4).</summary>
public static class ChannelProfile
{
    /// <summary>
    /// O perfil da canaleta: fundo = terreno − <paramref name="height"/>, ou, no
    /// modo de declividade mínima, o fundo nunca desce menos que
    /// <paramref name="minimumSlope"/> entre dois pontos (aprofunda onde o
    /// terreno não deixa; nunca sobe acima de terreno − altura).
    /// </summary>
    public static ChannelProfileResult Build(TerrainProfile terrain, double height, ChannelBottomMode mode, double minimumSlope, double segmentLength)
    {
        ArgumentNullException.ThrowIfNull(terrain);

        var pontos = new List<ChannelProfilePoint>(terrain.Points.Count);

        foreach (var p in terrain.Points)
        {
            var fundo = p.Z - height;

            if (mode == ChannelBottomMode.MinimumSlope && pontos.Count > 0)
            {
                var anterior = pontos[^1];
                fundo = Math.Min(fundo, anterior.Bottom - minimumSlope * (p.Distance - anterior.Distance));
            }

            pontos.Add(new ChannelProfilePoint(p.Distance, p.X, p.Y, p.Z, fundo));
        }

        var parcial = new ChannelProfileResult(pontos, [], minimumSlope);
        var trechos = ProfileSegments.Split(parcial.BottomAt, parcial.Length, segmentLength);

        return parcial with { Segments = trechos };
    }
}
