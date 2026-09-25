using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// O lado de uma linha de vários trechos: decidido pelo trecho mais próximo
/// do clique. Um "L" é o caso que separa isto da linha reta: o mesmo ponto
/// está à esquerda de um trecho e à direita do outro, e a resposta certa é a
/// do trecho ao lado do clique.
/// </summary>
public class PathSideTests
{
    // Um "L": leste por 10 m, depois norte por 10 m.
    private static readonly Point3[] L =
    [
        new(0, 0, 0),
        new(10, 0, 0),
        new(10, 10, 0),
    ];

    [Fact]
    [Trait("Etapa", "4")]
    public void OLadoEDoTrechoMaisProximo()
    {
        // Ao norte do primeiro trecho (leste): esquerda.
        Assert.Equal(LineSide.Left, PathSides.Of(L, new Point3(5, 2, 0)));

        // Ao sul do primeiro trecho: direita.
        Assert.Equal(LineSide.Right, PathSides.Of(L, new Point3(5, -2, 0)));

        // A leste do segundo trecho (norte): direita, mesmo estando "ao norte"
        // do primeiro trecho, que fica longe.
        Assert.Equal(LineSide.Right, PathSides.Of(L, new Point3(12, 5, 0)));

        // A oeste do segundo trecho: esquerda.
        Assert.Equal(LineSide.Left, PathSides.Of(L, new Point3(8, 5, 0)));
    }

    /// <summary>
    /// A distância é até o SEGMENTO, não até a reta que o contém.
    ///
    /// O ponto (30, 1) está a 1 m da RETA do primeiro trecho (y = 0), e pela
    /// reta ele ganharia. Mas o segmento acaba em (10, 0), a 20,02 m; e o
    /// segundo trecho, em (10, 1), está a 20 m. Ganha o segundo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ADistanciaEAteOSegmentoENaoAteAReta()
    {
        Assert.Equal(1, PathSides.NearestSegment(L, new Point3(30, 1, 0)));

        // E o contrário: (-5, 5) está a 5 m da reta do primeiro trecho e a
        // 7,07 m do segmento (ponta em (0, 0)); do segundo trecho, 15 m.
        Assert.Equal(0, PathSides.NearestSegment(L, new Point3(-5, 5, 0)));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TracadoAoContrarioTrocaOsLados()
    {
        var invertido = L.Reverse().ToArray();

        Assert.Equal(LineSide.Left, PathSides.Of(L, new Point3(5, 2, 0)));
        Assert.Equal(LineSide.Right, PathSides.Of(invertido, new Point3(5, 2, 0)));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ACotaNaoParticipa()
    {
        var comCotas = new Point3[] { new(0, 0, 700), new(10, 0, 0), new(10, 10, 9999) };

        Assert.Equal(LineSide.Left, PathSides.Of(comCotas, new Point3(5, 2, -50)));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TrechoDeComprimentoZeroEPulado()
    {
        var comRepetido = new Point3[] { new(0, 0, 0), new(0, 0, 0), new(10, 0, 0) };

        Assert.Equal(LineSide.Left, PathSides.Of(comRepetido, new Point3(5, 2, 0)));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void LinhaSemTrechoComComprimentoNaoTemLado()
    {
        var parada = new Point3[] { new(0, 0, 0), new(0, 0, 0) };

        Assert.Throws<ArgumentOutOfRangeException>(() => PathSides.Of(parada, new Point3(5, 2, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PathSides.Of([], new Point3(5, 2, 0)));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ComprimentoEmPlantaSomaOsTrechos()
    {
        Assert.Equal(20, PathSides.PlanLength(L), 9);
        Assert.Equal(0, PathSides.PlanLength([new Point3(1, 1, 1)]), 9);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void CurtaDemaisTemMotivo()
    {
        Assert.Null(PathSides.WhyTooShort(L));
        Assert.NotNull(PathSides.WhyTooShort([new Point3(0, 0, 0)]));
        Assert.NotNull(PathSides.WhyTooShort([new Point3(0, 0, 0), new Point3(0.05, 0, 0)]));
        Assert.NotNull(PathSides.WhyTooShort([new Point3(0, 0, 0), new Point3(double.NaN, 0, 0)]));
    }
}
