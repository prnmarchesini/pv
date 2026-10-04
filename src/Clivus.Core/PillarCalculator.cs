using System.Globalization;
using Clivus.Core.Invariants;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>Um pilar calculado: onde fura o chão, até onde sobe, e quanto mede.</summary>
/// <param name="Station">A estação ao longo da mesa, como em <see cref="PillarPiece.Station"/>.</param>
/// <param name="X">Onde o pilar fura o chão, em planta (a posição real, depois do giro).</param>
/// <param name="Y">Onde o pilar fura o chão, em planta.</param>
/// <param name="GroundZ">A cota do terreno no pé, ou null se o pé caiu fora do terreno.</param>
/// <param name="TopZ">A cota onde o pilar encosta na mesa.</param>
/// <param name="FreeHeight">O que fica acima do terreno (P3), ou null sem terreno.</param>
/// <param name="Embedment">
/// O que fica enterrado (P2): o enterro mínimo da configuração, sempre — é
/// o comprimento ideal que se entrega. Num pilar com problema o número está
/// aqui do mesmo jeito, e não é medida: é o mínimo que ele teria.
/// </param>
/// <param name="Length">O comprimento ideal (P1): altura livre mais enterro, ou null sem terreno ou quando não há pilar possível.</param>
/// <param name="Problem">
/// O que estourou, em português, ou null. É a regra sagrada 1 falando: pilar
/// que não tem altura livre (a mesa desce até o chão ali) ou que não tem
/// terreno. Marcado, nunca escondido.
/// </param>
/// <param name="LowEdgeClearance">
/// A altura livre da PONTA BAIXA da mesa na estação deste pilar (M1 do
/// desenho do Renan): a cota do plano dos módulos na borda baixa menos o
/// terreno ali. Null sem terreno. É a cota que o desenho mostra com um
/// risco vermelho na borda baixa.
/// </param>
/// <param name="HighEdgeClearance">A mesma coisa na PONTA ALTA. Null sem terreno.</param>
public sealed record PillarResult(
    double Station,
    double X,
    double Y,
    double? GroundZ,
    double TopZ,
    double? FreeHeight,
    double Embedment,
    double? Length,
    string? Problem,
    double? LowEdgeClearance = null,
    double? HighEdgeClearance = null)
{
    /// <summary>Se o pilar está de pé como deve: tem terreno, altura livre e enterro.</summary>
    public bool IsSound => Problem is null;
}

/// <summary>Os pilares de uma mesa resolvida.</summary>
/// <param name="Placement">A matriz que põe a mesa local no lugar, com a cota e o giro escolhidos.</param>
/// <param name="LongitudinalTiltRadians">O giro longitudinal aplicado, em radianos.</param>
/// <param name="Pillars">Um por pilar, na ordem da tabela.</param>
public sealed record TablePillars(
    Transform Placement,
    double LongitudinalTiltRadians,
    IReadOnlyList<PillarResult> Pillars)
{
    /// <summary>Quantos pilares estouraram.</summary>
    public int ProblemCount => Pillars.Count(p => !p.IsSound);

    /// <summary>Se há pilar e todo pilar está de pé como deve.</summary>
    public bool AllSound => Pillars.Count > 0 && ProblemCount == 0;

    /// <summary>O pilar mais comprido, ou null se nenhum tem comprimento.</summary>
    public double? LongestPillar
    {
        get
        {
            var comprimentos = Comprimentos();

            return comprimentos.Count == 0 ? null : comprimentos.Max();
        }
    }

    private List<double> Comprimentos() =>
        Pillars.Where(p => p.Length is not null).Select(p => p.Length!.Value).ToList();

    /// <summary>A linha que descreve os pilares para o usuário.</summary>
    public string Describe()
    {
        var comprimentos = Comprimentos();

        var faixa = comprimentos.Count == 0
            ? Tr.F("{0} pilar(es), sem comprimento", Pillars.Count)
            : Tr.F("{0} pilar(es), de {1:0.00} a {2:0.00} m", Pillars.Count, comprimentos.Min(), comprimentos.Max());

        return faixa + (ProblemCount > 0 ? Tr.F(", {0} com problema", ProblemCount) : string.Empty);
    }
}

/// <summary>
/// O cálculo dos pilares de uma mesa já resolvida: por pilar, a cota do
/// terreno no pé, a cota de topo, a altura livre, o enterro e o comprimento.
///
/// É o passo 5.5, com a divergência que o 4.1 já registrou: <b>não há
/// arredondamento para comprimento comercial</b>. O Renan foi explícito ("vc
/// deve calcular o pilar ideal apenas"): o comprimento é a altura livre mais
/// o enterro mínimo, e ele filtra. O enterro é sempre o mínimo, porque é o
/// comprimento ideal que se entrega; <see cref="SystemConfiguration.MaxEmbedment"/>
/// só morde quando o comprimento vem imposto de fora, o que não acontece aqui.
///
/// A mesa é colocada com a matriz de verdade — inclinação, giro longitudinal
/// escolhido pela fileira, azimute, cota — e o pé de cada pilar é
/// <b>reamostrado no terreno na posição real</b>: o giro desloca o pé em
/// planta por centímetros, e a amostra do 5.2 (sem giro) era só a primeira
/// aproximação. É a pendência que o 5.2 deixou, fechada aqui.
///
/// O que estoura é marcado, nunca escondido: pilar sem altura livre (a mesa
/// desce até o chão ali, ou abaixo) é a regra sagrada 1 sendo violada; pilar
/// alto demais para a escala do Core (mais de 20 m, um vértice espúrio da
/// superfície) é fora de escala; pé fora do terreno é sem terreno. Cada um
/// é problema escrito por pilar, nunca exceção. A mesa não é movida por
/// causa dele — a ponta baixa manda, o pilar é consequência.
///
/// A mesa que a fileira entregou com desnível maior que o comprimento (só
/// possível sem limite de declividade) não tem giro que a produza: sai com
/// todo pilar marcado e a matriz nivelada na cota inicial, e não com
/// exceção derrubando a fileira.
/// </summary>
public static class PillarCalculator
{
    /// <summary>
    /// Calcula os pilares.
    /// </summary>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="cell">A célula da mesa em planta (5.1).</param>
    /// <param name="orientation">Para que lado a mesa sobe (5.1).</param>
    /// <param name="tiltRadians">A inclinação transversal da mesa.</param>
    /// <param name="solved">As cotas escolhidas pela fileira (5.4).</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="configuration">De onde vem o enterro mínimo.</param>
    /// <exception cref="ArgumentOutOfRangeException">Cotas não finitas.</exception>
    /// <exception cref="ArgumentException">Célula de outro comprimento que a geometria.</exception>
    /// <exception cref="InvalidOperationException">Configuração que não fecha.</exception>
    public static TablePillars Compute(
        TableGeometry geometry,
        PlacedTable cell,
        RowOrientation orientation,
        double tiltRadians,
        SolvedTable solved,
        Tin terrain,
        SystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(solved);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.WhyInvalid is { } motivo)
            throw new InvalidOperationException(Tr.F("A configuração não fecha: {0}.", motivo));

        if (!double.IsFinite(solved.StartElevation) || !double.IsFinite(solved.EndElevation))
            throw new ArgumentOutOfRangeException(nameof(solved), "A cota da mesa não é um número.");

        // Desnível maior que o comprimento: não há mesa rígida assim. Marca,
        // não derruba.
        var desnivel = solved.EndElevation - solved.StartElevation;
        string? semGiro = Math.Abs(desnivel) >= geometry.Length - 1e-9
            ? Tr.F("o desnível entre as pontas ({0:0.##} m) é maior que o comprimento da mesa: não há giro que o produza", Math.Abs(desnivel))
            : null;

        var colocacao = semGiro is null
            ? TablePlacement.PlanSolved(
                cell, orientation, tiltRadians, solved.StartElevation, solved.EndElevation, geometry.Length, out var giroCalculado)
            : TablePlacement.Plan(cell, orientation, tiltRadians, solved.StartElevation);

        var giro = semGiro is null ? Math.Asin(desnivel / geometry.Length) : 0;

        var pilares = geometry.Pillars
            .Select(p =>
            {
                var topo = colocacao.Apply(p.Anchor);
                var enterro = configuration.MinEmbedment;

                if (semGiro is not null)
                    return new PillarResult(p.Station, topo.X, topo.Y, null, topo.Z, null, enterro, null, semGiro);

                if (!terrain.TryGetZ(topo.X, topo.Y, out var terreno))
                {
                    return new PillarResult(
                        p.Station, topo.X, topo.Y, null, topo.Z, null, enterro, null,
                        Tr.T("o pé do pilar caiu fora do terreno"));
                }

                var livre = topo.Z - terreno;

                // As pontas baixa e alta da mesa nesta estação: o plano dos
                // módulos em y = 0 e y = fundo, levados ao mundo e comparados
                // com o terreno debaixo de cada uma.
                var pontaBaixa = Altura(colocacao.Apply(new Point3(p.Station, 0, 0)), terrain);
                var pontaAlta = Altura(colocacao.Apply(new Point3(p.Station, geometry.Depth, 0)), terrain);

                // Regra sagrada 1: o pilar nunca flutua. Aqui o enterro é o
                // mínimo por construção; o que pode faltar é a altura livre,
                // quando a mesa desce até o chão naquele pé. E acima da
                // escala do Core é um vértice espúrio da superfície, não um
                // pilar: marcado, sem comprimento.
                var problema = FloatingPillar.Check(livre, enterro)
                    ?? (livre > PillarSizing.MaiorAlturaLivre
                        ? Tr.F("altura livre de {0:0.#} m: fora de escala, o terreno sob este pé não é confiável", livre)
                        : null);

                double? comprimento = problema is null ? PillarSizing.Length(livre, enterro) : null;

                return new PillarResult(
                    p.Station, topo.X, topo.Y, terreno, topo.Z, livre, enterro, comprimento, problema, pontaBaixa, pontaAlta);
            })
            .ToList();

        return new TablePillars(colocacao, giro, pilares);
    }

    /// <summary>A altura de um ponto sobre o terreno, ou null fora dele.</summary>
    private static double? Altura(Point3 ponto, Tin terrain) =>
        terrain.TryGetZ(ponto.X, ponto.Y, out var z) ? ponto.Z - z : null;
}
