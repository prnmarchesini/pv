namespace Clivus.Core.Tests;

/// <summary>
/// Quem leva a PB e a PA de cada estação (passo 38.3, revisão): um pilar por
/// estação, o primeiro sem problema.
/// </summary>
public class PillarStationsTests
{
    [Fact]
    [Trait("Etapa", "38")]
    public void NaMonoposteTodoPilarLeva()
    {
        var levam = PillarStations.EdgeCarriers([(0.5, true), (3.5, false), (6.5, true)]);

        Assert.Equal([0, 1, 2], levam.Order());
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void NaBifilaLevaODianteiroQuandoEleEstaBom()
    {
        var levam = PillarStations.EdgeCarriers([(0.851, true), (0.851, true), (4.051, true), (4.051, true)]);

        Assert.Equal([0, 2], levam.Order());
    }

    /// <summary>O dianteiro com problema e o traseiro bom: o traseiro leva, e o pórtico não fica sem PB.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void ComODianteiroComProblemaLevaOTraseiro()
    {
        var levam = PillarStations.EdgeCarriers([(0.851, false), (0.851, true), (4.051, true), (4.051, true)]);

        Assert.Equal([1, 2], levam.Order());
    }

    /// <summary>Os dois com problema: leva o primeiro (quem escreve decide se põe a cota).</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void ComOsDoisComProblemaLevaOPrimeiro()
    {
        var levam = PillarStations.EdgeCarriers([(0.851, false), (0.851, false)]);

        Assert.Equal([0], levam);
    }

    private static PillarResult Pilar(double estacao, double? pb, PillarLine linha) =>
        new(estacao, estacao, 0, pb is null ? null : 700, 701, 1, 2.2, 3.2, pb is null ? "sem terreno" : null, pb, pb, linha);

    /// <summary>A ponta da mesa pelo pilar que tem a PB: o dianteiro sem terreno não deixa a ponta sem PB.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void APontaUsaOPilarQueTemAPb()
    {
        List<PillarResult> pilares =
        [
            Pilar(0.851, null, PillarLine.Front), Pilar(0.851, 1.10, PillarLine.Rear),
            Pilar(17.751, 1.20, PillarLine.Front), Pilar(17.751, 1.20, PillarLine.Rear),
        ];

        Assert.Equal(1.10, PillarStations.EndCarrier(pilares, first: true).LowEdgeClearance);
        Assert.Equal(PillarLine.Front, PillarStations.EndCarrier(pilares, first: false).Line);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void SemPbNaPontaFicaOPrimeiroDaEstacao()
    {
        List<PillarResult> pilares = [Pilar(0.851, null, PillarLine.Front), Pilar(0.851, null, PillarLine.Rear)];

        Assert.Equal(PillarLine.Front, PillarStations.EndCarrier(pilares, first: true).Line);
        Assert.Throws<ArgumentException>(() => PillarStations.EndCarrier([], first: true));
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void ListaVaziaNaoLevaNinguem()
    {
        Assert.Empty(PillarStations.EdgeCarriers([]));
    }
}
