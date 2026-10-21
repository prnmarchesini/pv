namespace Clivus.Geo;

/// <summary>Um ponto do perfil: a distância desde a montante, a posição e a cota do terreno.</summary>
/// <param name="IsStation">Se é uma estaca (múltiplo do espaçamento) e não um ponto de quebra do terreno.</param>
public readonly record struct ProfilePoint(double Distance, double X, double Y, double Z, bool IsStation);

/// <summary>
/// O perfil do terreno ao longo de uma linha (passo 27.2).
/// </summary>
/// <param name="Points">Os pontos em ordem de distância: as quebras do terreno (vértices e arestas cruzadas) e as estacas.</param>
/// <param name="Reversed">Se a linha foi lida do fim para o começo, porque o fim era a ponta mais alta (a montante).</param>
/// <param name="OutsideCount">Quantos pontos ficaram sem terreno embaixo.</param>
public sealed record TerrainProfile(IReadOnlyList<ProfilePoint> Points, bool Reversed, int OutsideCount)
{
    /// <summary>O comprimento em planta, em metros.</summary>
    public double Length => Points.Count == 0 ? 0 : Points[^1].Distance;

    /// <summary>A cota do terreno na distância dada, por interpolação linear entre os pontos (exata: entre duas quebras o terreno é reto).</summary>
    public double ZAt(double distance)
    {
        if (Points.Count == 0) return double.NaN;
        if (distance <= Points[0].Distance) return Points[0].Z;
        if (distance >= Points[^1].Distance) return Points[^1].Z;

        // Busca binária: perfil de canaleta comprida tem milhares de pontos.
        int lo = 0, hi = Points.Count - 1;
        while (hi - lo > 1)
        {
            var meio = (lo + hi) / 2;
            if (Points[meio].Distance <= distance) lo = meio; else hi = meio;
        }

        var a = Points[lo];
        var b = Points[hi];
        var t = b.Distance > a.Distance ? (distance - a.Distance) / (b.Distance - a.Distance) : 0;
        return a.Z + (b.Z - a.Z) * t;
    }
}

/// <summary>
/// A amostragem do perfil de uma linha sobre o terreno (passo 27.2): as
/// quebras do terreno (cada aresta de triângulo cruzada, pelo drapeamento)
/// mais as estacas a cada tantos metros. A distância conta da ponta mais
/// alta, a montante (regra 3 da drenagem: a água vai do alto para o baixo, e
/// a linha desenhada ao contrário não é revertida no desenho, só na leitura).
/// </summary>
public static class ProfileSampler
{
    /// <summary>O perfil da linha <paramref name="plan"/> (só X e Y contam) com estaca a cada <paramref name="stationSpacing"/> m.</summary>
    public static TerrainProfile Sample(Tin tin, IReadOnlyList<Point3> plan, double stationSpacing, bool keepDirection = false)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentNullException.ThrowIfNull(plan);

        if (!(stationSpacing > 0)) throw new ArgumentOutOfRangeException(nameof(stationSpacing));
        if (plan.Count < 2) return new TerrainProfile([], false, 0);

        var drapeada = Draping.Along(tin, plan);
        var vertices = drapeada.Vertices.ToList();
        var fora = new HashSet<int>(drapeada.OutsideIndices);

        // Montante e jusante pelas pontas que têm terreno: a cota de um ponto
        // fora dele não é do terreno e decidiria o sentido errado.
        var dentro = Enumerable.Range(0, vertices.Count).Where(i => !fora.Contains(i)).ToList();
        var invertida = !keepDirection && dentro.Count >= 2 && vertices[dentro[^1]].Z > vertices[dentro[0]].Z;
        if (invertida)
        {
            vertices.Reverse();
            fora = fora.Select(i => vertices.Count - 1 - i).ToHashSet();
        }

        // As quebras, com a distância acumulada em planta. O ponto sem terreno
        // embaixo fica de fora do perfil (a cota dele não é do terreno), mas
        // a distância conta o trecho: o gráfico mostra o buraco onde ele é.
        var quebras = new List<(ProfilePoint Ponto, int Indice)>(vertices.Count);
        var distancia = 0.0;

        for (var i = 0; i < vertices.Count; i++)
        {
            if (i > 0)
            {
                var (dx, dy) = (vertices[i].X - vertices[i - 1].X, vertices[i].Y - vertices[i - 1].Y);
                distancia += Math.Sqrt(dx * dx + dy * dy);
            }

            if (!fora.Contains(i)) quebras.Add((new ProfilePoint(distancia, vertices[i].X, vertices[i].Y, vertices[i].Z, false), i));
        }

        if (quebras.Count < 2) return new TerrainProfile([], invertida, fora.Count);

        // Começo sem terreno embaixo: o perfil conta do primeiro ponto com
        // terreno. Contar do zero inventaria um patamar plano até ele.
        var deslocamento = quebras[0].Ponto.Distance;
        if (deslocamento > 0)
        {
            for (var i = 0; i < quebras.Count; i++)
                quebras[i] = (quebras[i].Ponto with { Distance = quebras[i].Ponto.Distance - deslocamento }, quebras[i].Indice);
            distancia -= deslocamento;
        }

        // As estacas, interpoladas entre quebras vizinhas (o terreno é reto
        // entre elas); estaca que cai num trecho sem terreno não existe.
        var pontos = new List<ProfilePoint>(quebras.Count + (int)(distancia / stationSpacing) + 2);
        var proxima = 0.0;
        var j = 0;

        for (var i = 0; i < quebras.Count; i++)
        {
            var q = quebras[i].Ponto;

            while (proxima <= q.Distance + 1e-9)
            {
                if (Math.Abs(proxima - q.Distance) <= 1e-9)
                {
                    q = q with { IsStation = true };
                }
                else if (i > 0 && quebras[i].Indice - quebras[i - 1].Indice == 1)
                {
                    var a = quebras[i - 1].Ponto;
                    var t = (proxima - a.Distance) / (q.Distance - a.Distance);
                    pontos.Add(new ProfilePoint(proxima, a.X + (q.X - a.X) * t, a.Y + (q.Y - a.Y) * t, a.Z + (q.Z - a.Z) * t, true));
                }

                j++;
                proxima = j * stationSpacing;
            }

            pontos.Add(q);
        }

        // A última ponta é sempre estaca: é o fim da linha.
        pontos[^1] = pontos[^1] with { IsStation = true };

        return new TerrainProfile(pontos, invertida, fora.Count);
    }
}
