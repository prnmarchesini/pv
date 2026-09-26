namespace UFV.Geo.Tests;

/// <summary>
/// O polígono em planta: dentro ou fora, e onde uma reta o atravessa. É o que
/// a distribuição de mesas usa para saber onde a área começa e termina.
/// </summary>
public class PolygonTests
{
    private static Point3 P(double x, double y) => new(x, y, 0);

    /// <summary>Um retângulo de 100 por 50, com origem no canto.</summary>
    private static readonly Point3[] Retangulo = [P(0, 0), P(100, 0), P(100, 50), P(0, 50)];

    /// <summary>Um "L": o retângulo sem o quadrante superior direito.</summary>
    private static readonly Point3[] Ele = [P(0, 0), P(100, 0), P(100, 25), P(50, 25), P(50, 50), P(0, 50)];

    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(50, 25, true)]
    [InlineData(0.001, 0.001, true)]
    [InlineData(-1, 25, false)]
    [InlineData(101, 25, false)]
    [InlineData(50, 51, false)]
    [InlineData(50, 50.001, false)]
    public void DentroOuForaDoRetangulo(double x, double y, bool dentro)
    {
        Assert.Equal(dentro, Polygons.Contains(Retangulo, x, y));
    }

    /// <summary>
    /// A borda conta como dentro: o canto da mesa encostada na borda da área
    /// é o caso mais comum que existe. Vale para aresta e para vértice, em
    /// qualquer lado do polígono.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0, 25)]
    [InlineData(100, 25)]
    [InlineData(50, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    [InlineData(0, 50)]
    public void ABordaContaComoDentro(double x, double y)
    {
        Assert.True(Polygons.Contains(Retangulo, x, y));
        Assert.True(Polygons.Contains(Retangulo.Reverse().ToArray(), x, y));
    }

    /// <summary>O L é côncavo: o canto que falta está fora.</summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(25, 40, true)]
    [InlineData(75, 10, true)]
    [InlineData(75, 40, false)]
    public void DentroOuForaDoEle(double x, double y, bool dentro)
    {
        Assert.Equal(dentro, Polygons.Contains(Ele, x, y));
    }

    /// <summary>A ordem dos vértices (horária ou anti-horária) não muda o resultado.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OSentidoDosVerticesNaoImporta()
    {
        var invertido = Retangulo.Reverse().ToArray();

        Assert.True(Polygons.Contains(invertido, 50, 25));
        Assert.False(Polygons.Contains(invertido, 150, 25));
    }

    /// <summary>
    /// O último vértice repetindo o primeiro (polilinha fechada "à mão") não
    /// cria aresta degenerada nem muda a resposta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void VerticeFinalRepetidoNaoAtrapalha()
    {
        var fechado = Retangulo.Append(P(0, 0)).ToArray();

        Assert.True(Polygons.Contains(fechado, 50, 25));
        Assert.Equal(2, Polygons.Crossings(fechado, P(-10, 25), P(1, 0)).Count);
    }

    /// <summary>
    /// A reta horizontal y = 25 atravessa o retângulo em x = 0 e x = 100. Os
    /// parâmetros vêm em ordem crescente, medidos a partir da origem da reta
    /// no comprimento da direção.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ARetaAtravessaORetanguloEmDoisPontos()
    {
        var cruzamentos = Polygons.Crossings(Retangulo, P(-10, 25), P(1, 0));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(10, cruzamentos[0], 9);
        Assert.Equal(110, cruzamentos[1], 9);
    }

    /// <summary>
    /// A direção não precisa ser unitária: o parâmetro é em unidades da
    /// direção, então com direção (2, 0) os cruzamentos vêm pela metade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OParametroEEmUnidadesDaDirecao()
    {
        var cruzamentos = Polygons.Crossings(Retangulo, P(-10, 25), P(2, 0));

        Assert.Equal(5, cruzamentos[0], 9);
        Assert.Equal(55, cruzamentos[1], 9);
    }

    /// <summary>No L, a reta y = 40 só atravessa a perna esquerda: de 0 a 50.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NoEleARetaAltaSoPegaAPernaEsquerda()
    {
        var cruzamentos = Polygons.Crossings(Ele, P(0, 40), P(1, 0));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(0, cruzamentos[0], 9);
        Assert.Equal(50, cruzamentos[1], 9);
    }

    /// <summary>
    /// Uma reta que passa exatamente por um vértice não pode contar o vértice
    /// duas vezes (uma por aresta): senão o "dentro" vira "fora" dali para a
    /// frente. Na diagonal do retângulo, os cruzamentos são os dois cantos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RetaPeloVerticeNaoContaOVerticeDuasVezes()
    {
        var cruzamentos = Polygons.Crossings(Retangulo, P(0, 0), P(100, 50));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(0, cruzamentos[0], 9);
        Assert.Equal(1, cruzamentos[1], 9);
    }

    /// <summary>Uma reta que não toca o polígono não tem cruzamento.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RetaForaNaoCruza()
    {
        Assert.Empty(Polygons.Crossings(Retangulo, P(0, 60), P(1, 0)));
    }

    /// <summary>
    /// Uma reta por cima de uma aresta é tangente: o polígono fica inteiro de
    /// um lado dela, e ela não entra. Nenhum cruzamento, e não dois — a
    /// borda tem área zero, e uma fileira ali não teria mesa dentro. Vale
    /// com o polígono de qualquer lado da reta: a revisão do 5.1 pegou a
    /// versão anterior devolvendo [10, 110] para a aresta de cima e nada
    /// para a de baixo.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(50)]
    public void RetaSobreAArestaNaoEntraDeNenhumLado(double y)
    {
        Assert.Empty(Polygons.Crossings(Retangulo, P(-10, y), P(1, 0)));
        Assert.Empty(Polygons.Crossings(Retangulo, P(-10, y), P(-1, 0)));
        Assert.Empty(Polygons.Crossings(Retangulo.Reverse().ToArray(), P(-10, y), P(1, 0)));
    }

    /// <summary>A reta que só encosta num vértice por fora não entra.</summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(-10, 40, 1, 1)]
    [InlineData(90, -10, 1, 1)]
    [InlineData(110, 40, -1, 1)]
    public void RetaEncostandoNoVerticePorForaNaoEntra(double x, double y, double dx, double dy)
    {
        Assert.Empty(Polygons.Crossings(Retangulo, P(x, y), P(dx, dy)));
    }

    /// <summary>
    /// A reta que entra por uma aresta e sai por um vértice cruza duas vezes:
    /// uma na aresta, uma no vértice. No L, a diagonal por (40, 15) entra
    /// pela aresta de baixo em (25, 0) e sai pelo vértice côncavo (50, 25).
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RetaQueSaiPorUmVerticeCruzaUmaVezNele()
    {
        var cruzamentos = Polygons.Crossings(Ele, P(40, 15), P(1, 1));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(-15, cruzamentos[0], 9);
        Assert.Equal(10, cruzamentos[1], 9);
    }

    /// <summary>
    /// A reta que corre por uma aresta vindo de dentro: no L, a vertical
    /// x = 50 entra em (50, 0), e de (50, 25) a (50, 50) corre pela borda.
    /// A borda ainda é o polígono; o cruzamento de saída é na ponta da
    /// corrida, em (50, 50).
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RetaQueCorrePelaArestaVindoDeDentroSaiNaPonta()
    {
        var cruzamentos = Polygons.Crossings(Ele, P(50, -10), P(0, 1));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(10, cruzamentos[0], 9);
        Assert.Equal(60, cruzamentos[1], 9);
    }

    /// <summary>
    /// A reta que corre por uma aresta e depois entra no polígono: cruza uma
    /// vez, na ponta da aresta em que entra. No L, a horizontal y = 25 corre
    /// pela aresta (100,25)–(50,25) e entra na perna esquerda em x = 50,
    /// saindo em x = 0.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RetaQueCorrePelaArestaEEntraCruzaNaPonta()
    {
        var cruzamentos = Polygons.Crossings(Ele, P(200, 25), P(-1, 0));

        Assert.Equal(2, cruzamentos.Count);
        Assert.Equal(150, cruzamentos[0], 9);
        Assert.Equal(200, cruzamentos[1], 9);
    }

    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PoligonoComMenosDeTresVerticesERecusado(int quantos)
    {
        var poucos = Retangulo.Take(quantos).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => Polygons.Contains(poucos, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polygons.Crossings(poucos, P(0, 0), P(1, 0)));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void DirecaoNulaERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Polygons.Crossings(Retangulo, P(0, 0), P(0, 0)));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void PontoNaoFinitoERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Polygons.Contains(Retangulo, double.NaN, 1));
    }

    /// <summary>Cruzar é atravessar; encostar, tocar de ponta e correr junto não é.</summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0, 0, 10, 10, 0, 10, 10, 0, true)]
    [InlineData(0, 0, 10, 0, 5, 0, 5, 10, false)]
    [InlineData(0, 0, 10, 0, 10, 0, 20, 10, false)]
    [InlineData(0, 0, 10, 0, 5, 0, 15, 0, false)]
    [InlineData(0, 0, 10, 0, 0, 1, 10, 1, false)]
    [InlineData(0, 0, 10, 0, 5, -1, 5, 1, true)]
    public void SegmentosCruzamSoQuandoAtravessam(
        double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, bool cruzam)
    {
        Assert.Equal(cruzam, Polygons.SegmentsCross(P(ax, ay), P(bx, by), P(cx, cy), P(dx, dy)));
    }
}
