namespace Clivus.Geo.Tests;

/// <summary>
/// A simplificação por colinearidade em planta: tira os vértices que o
/// drapeamento acrescentou e deixa os que o usuário traçou.
/// </summary>
public class PlanPathsTests
{
    private static Point3 P(double x, double y, double z = 0) => new(x, y, z);

    /// <summary>
    /// Um traçado de dois pontos drapejado ganha vértices ao longo da reta,
    /// com cotas diferentes: todos saem, e sobram os dois.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OsVerticesDoDrapeamentoSaem()
    {
        var drapejada = new[] { P(0, 0, 700), P(10, 0, 701), P(25, 0, 699.5), P(60, 0, 702), P(100, 0, 700) };

        var simples = PlanPaths.SimplifyCollinear(drapejada);

        Assert.Equal([P(0, 0, 700), P(100, 0, 700)], simples);
    }

    /// <summary>O vértice em que a linha muda de direção fica, com a cota que tem.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OVerticeDeMudancaDeDirecaoFica()
    {
        var em_v = new[] { P(0, 0, 700), P(20, 10, 701), P(40, 20, 702), P(60, 10, 701), P(80, 0, 700) };

        var simples = PlanPaths.SimplifyCollinear(em_v);

        Assert.Equal([P(0, 0, 700), P(40, 20, 702), P(80, 0, 700)], simples);
    }

    /// <summary>Um vértice a um centímetro da reta não é do drapeamento: fica.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void UmVerticeForaDaRetaFica()
    {
        var torta = new[] { P(0, 0), P(50, 0.01), P(100, 0) };

        Assert.Equal(3, PlanPaths.SimplifyCollinear(torta).Count);
        Assert.Equal(2, PlanPaths.SimplifyCollinear(torta, tolerance: 0.02).Count);
    }

    /// <summary>
    /// Polígono fechado com o último igual ao primeiro: as pontas ficam como
    /// vieram, e os vértices do meio das arestas saem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPoligonoDrapejadoVoltaAosCantos()
    {
        var quadrado = new[]
        {
            P(0, 0), P(30, 0), P(70, 0), P(100, 0), P(100, 40), P(100, 100), P(60, 100), P(0, 100), P(0, 50), P(0, 0),
        };

        var simples = PlanPaths.SimplifyCollinear(quadrado);

        Assert.Equal([P(0, 0), P(100, 0), P(100, 100), P(0, 100), P(0, 0)], simples);
    }

    /// <summary>
    /// Polígono fechado SEM o primeiro repetido, como a área do plugin é
    /// gravada: o último vértice (drapeamento na aresta de fechamento) e o
    /// primeiro (idem) saem pela volta, e sobram os quatro cantos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPoligonoFechadoSemRepeticaoVoltaAosCantos()
    {
        var quadrado = new[]
        {
            P(0, 30), P(0, 0), P(30, 0), P(100, 0), P(100, 40), P(100, 100), P(60, 100), P(0, 100), P(0, 60),
        };

        var simples = PlanPaths.SimplifyCollinear(quadrado, closed: true);

        Assert.Equal(4, simples.Count);
        Assert.Contains(P(0, 0), simples);
        Assert.Contains(P(100, 0), simples);
        Assert.Contains(P(100, 100), simples);
        Assert.Contains(P(0, 100), simples);

        // Um triângulo nunca perde vértice pela volta.
        Assert.Equal(3, PlanPaths.SimplifyCollinear([P(0, 0), P(10, 0), P(0, 10)], closed: true).Count);
    }

    /// <summary>
    /// A tolerância é em metro, também ao longo do trecho: um ponto um metro
    /// além da ponta de um trecho de um quilômetro não está "em cima".
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AToleranciaAoLongoDoTrechoEEmMetro()
    {
        var alem = new[] { P(0, 0), P(1001, 0), P(1000, 0) };

        // (1001, 0) está um metro além de (1000, 0) na reta de (0,0): muda de
        // direção (volta), fica.
        Assert.Equal(3, PlanPaths.SimplifyCollinear(alem, tolerance: 1e-3).Count);
    }

    /// <summary>Um vértice que volta por cima do trecho anterior muda de direção e fica.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VoltarPorCimaDoTrechoNaoEColinear()
    {
        var vaiEVolta = new[] { P(0, 0), P(100, 0), P(50, 0) };

        Assert.Equal(3, PlanPaths.SimplifyCollinear(vaiEVolta).Count);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void DoisPontosOuMenosVoltamComoVieram()
    {
        Assert.Equal(2, PlanPaths.SimplifyCollinear([P(0, 0), P(1, 1)]).Count);
        Assert.Empty(PlanPaths.SimplifyCollinear([]));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ToleranciaNegativaERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanPaths.SimplifyCollinear([P(0, 0), P(1, 0), P(2, 0)], -1));
    }
}
