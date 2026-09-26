using UFV.Geo;

namespace UFV.Core;

/// <summary>A cota do terreno no pé de um pilar.</summary>
/// <param name="Station">A estação do pilar ao longo da mesa, como em <see cref="PillarPiece.Station"/>.</param>
/// <param name="X">Onde o pilar fura o chão, em planta.</param>
/// <param name="Y">Onde o pilar fura o chão, em planta.</param>
/// <param name="GroundZ">A cota do terreno ali, ou null se o ponto caiu fora do terreno.</param>
public sealed record PillarSample(double Station, double X, double Y, double? GroundZ);

/// <summary>
/// A cota do terreno sob a ponta baixa de um módulo da fileira de baixo.
/// </summary>
/// <param name="Column">A coluna do módulo na mesa, como em <see cref="ModulePiece.Column"/>.</param>
/// <param name="Station">
/// A estação do meio da ponta baixa ao longo da mesa, em coordenadas locais
/// (a mesma régua de <see cref="PillarPiece.Station"/>). É por ela que a
/// cota da ponta baixa varia ao longo de uma mesa que inclina no sentido da
/// fileira.
/// </param>
/// <param name="X">O meio da ponta baixa, em planta.</param>
/// <param name="Y">O meio da ponta baixa, em planta.</param>
/// <param name="GroundZ">
/// A cota do terreno mais ALTA sob a ponta baixa inteira, de ponta a ponta
/// da aresta (<see cref="Tin.TryGetMaxZAlong"/>), ou null se qualquer parte
/// dela ficou sem terreno embaixo. A mais alta, porque é ela que decide a
/// altura livre: a regra sagrada 4 mede da ponta baixa até o terreno, e o
/// terreno que importa é o que chega mais perto — em qualquer ponto da
/// aresta, não só nos amostrados.
/// </param>
public sealed record LowEdgeSample(int Column, double Station, double X, double Y, double? GroundZ);

/// <summary>
/// O terreno amostrado sob uma mesa, uma vez só.
///
/// É tudo que o motor vai saber do terreno daqui para a frente: a cota no pé
/// de cada pilar e sob a ponta baixa de cada módulo da fileira de baixo. A
/// arquitetura manda ("terreno amostrado uma vez e guardado; o motor não
/// volta à superfície durante a otimização"), e o objeto é imutável por isso.
/// </summary>
/// <param name="Pillars">Um por pilar, na ordem da tabela de pilares.</param>
/// <param name="LowEdge">Um por coluna de módulo da fileira de baixo, na ordem das colunas.</param>
public sealed record TableSamples(
    IReadOnlyList<PillarSample> Pillars,
    IReadOnlyList<LowEdgeSample> LowEdge)
{
    /// <summary>Quantos pontos caíram fora do terreno.</summary>
    public int OutsideCount =>
        Pillars.Count(p => p.GroundZ is null) + LowEdge.Count(m => m.GroundZ is null);

    /// <summary>Se todo ponto tem cota. Sem isto a mesa não pode ser calculada.</summary>
    public bool IsComplete => OutsideCount == 0;

    /// <summary>A cota de terreno mais alta entre as amostras que existem, ou null se não há nenhuma.</summary>
    public double? HighestGround => CotasConhecidas().DefaultIfEmpty().Max(z => (double?)z) is { } z && CotasConhecidas().Any() ? z : null;

    /// <summary>A cota de terreno mais baixa entre as amostras que existem, ou null se não há nenhuma.</summary>
    public double? LowestGround => CotasConhecidas().Any() ? CotasConhecidas().Min() : null;

    private IEnumerable<double> CotasConhecidas() =>
        Pillars.Select(p => p.GroundZ).Concat(LowEdge.Select(m => m.GroundZ))
            .Where(z => z is not null).Select(z => z!.Value);
}

/// <summary>
/// Amostra o terreno sob uma mesa colocada em planta.
///
/// Só X e Y da mesa importam: a amostragem pergunta ao terreno "qual a cota
/// aqui?", e "aqui" não depende da cota que a mesa vai ter. Por isso a
/// colocação pode vir com cota zero, e a mesma amostra serve para toda cota
/// que a otimização experimentar — com a inclinação e o azimute fixos. Um
/// giro longitudinal da mesa (início e fim em cotas diferentes) move o pé
/// de cada pilar em planta por alguns centímetros; se o 5.3 for girar, é
/// aqui que se decide se isso vale uma reamostragem (anotado em PROGRESSO.md).
///
/// Ponto fora do terreno é null, nunca zero: cota zero é um número plausível
/// e passaria despercebido até virar altura de pilar.
/// </summary>
public static class TerrainSampler
{
    /// <summary>
    /// Amostra o terreno.
    /// </summary>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="placement">A colocação em planta, de <see cref="TablePlacement.Plan"/>.</param>
    /// <param name="terrain">O terreno.</param>
    public static TableSamples Sample(TableGeometry geometry, Transform placement, Tin terrain)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(terrain);

        if (!placement.IsRigid)
            throw new ArgumentException("A colocação da mesa não é uma transformação rígida.", nameof(placement));

        var pilares = geometry.Pillars
            .Select(p =>
            {
                var pe = placement.Apply(p.Anchor);

                return new PillarSample(p.Station, pe.X, pe.Y, Cota(terrain, pe));
            })
            .ToList();

        var pontaBaixa = geometry.Modules
            .Where(m => m.Row == 0)
            .OrderBy(m => m.Column)
            .Select(m =>
            {
                // A ponta baixa do módulo, em coordenadas locais, é a aresta
                // da face em y = 0: os dois primeiros cantos.
                var a = m.TopFace[0];
                var b = m.TopFace[1];
                var meio = new Point3((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

                var aNoMundo = placement.Apply(a);
                var bNoMundo = placement.Apply(b);
                var meioNoMundo = placement.Apply(meio);

                double? maisAlta = terrain.TryGetMaxZAlong(aNoMundo.X, aNoMundo.Y, bNoMundo.X, bNoMundo.Y, out var z)
                    ? z
                    : null;

                return new LowEdgeSample(m.Column, meio.X, meioNoMundo.X, meioNoMundo.Y, maisAlta);
            })
            .ToList();

        return new TableSamples(pilares, pontaBaixa);
    }

    private static double? Cota(Tin terreno, Point3 ponto) =>
        terreno.TryGetZ(ponto.X, ponto.Y, out var z) ? z : null;
}
