using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// Os testes de <see cref="TinTests"/> rodam sobre um plano inclinado, onde a
/// resposta certa é calculável à mão. Isso é bom para conferir a interpolação,
/// e cego para duas coisas:
///
/// 1. Qual triângulo respondeu. Num plano, o triângulo errado devolve a mesma
///    cota que o certo, e o erro de seleção não aparece.
/// 2. O tamanho das tolerâncias. Num terreno bem-comportado, afrouxar a folga
///    em seis ordens de grandeza não muda resultado nenhum.
///
/// Aqui o terreno tem quina, fatia e coordenada de verdade.
/// </summary>
public class TinRobustezTests
{
    /// <summary>
    /// Duas faces com inclinações diferentes encostadas numa aresta comum —
    /// a quina de um calombo. Do lado esquerdo o terreno sobe 1 m a cada 10 m;
    /// do lado direito, desce na mesma taxa.
    ///
    ///   z = 10 + x/10        para x &lt;= 50
    ///   z = 10 + (100-x)/10  para x &gt;= 50
    /// </summary>
    private static Tin Calombo()
    {
        Point3 E(double x, double y) => new(x, y, 10 + x / 10.0);
        Point3 D(double x, double y) => new(x, y, 10 + (100 - x) / 10.0);

        return new Tin(
        [
            // face oeste, subindo
            new Triangle(E(0, 0), E(50, 0), E(50, 100)),
            new Triangle(E(0, 0), E(50, 100), E(0, 100)),
            // face leste, descendo
            new Triangle(D(50, 0), D(100, 0), D(100, 100)),
            new Triangle(D(50, 0), D(100, 100), D(50, 100)),
        ]);
    }

    [Theory]
    [InlineData(49.0, 50.0, 14.9)]   // um metro antes da quina, subindo
    [InlineData(50.0, 50.0, 15.0)]   // em cima da quina
    [InlineData(51.0, 50.0, 14.9)]   // um metro depois, descendo
    [InlineData(10.0, 20.0, 11.0)]
    [InlineData(90.0, 20.0, 11.0)]
    [Trait("Etapa", "1")]
    public void QuinaDeCalomboRespondeAFaceCerta(double x, double y, double esperado)
    {
        // Num terreno plano este teste não diria nada: as duas faces dariam a
        // mesma resposta. Aqui, escolher a face errada erra por metros.
        Assert.True(Calombo().TryGetZ(x, y, out var z));
        Assert.Equal(esperado, z, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void PertoDaQuinaNaoPegaAFaceDeLaPorUmCentimetro()
    {
        var tin = Calombo();

        // 1 cm de cada lado da aresta compartilhada. A diferença de cota entre
        // as faces nessa distância é 2 mm — abaixo disso, tanto faz; acima,
        // é erro de seleção de triângulo.
        Assert.True(tin.TryGetZ(49.99, 50, out var oeste));
        Assert.True(tin.TryGetZ(50.01, 50, out var leste));

        Assert.Equal(10 + 49.99 / 10.0, oeste, 9);
        Assert.Equal(10 + (100 - 50.01) / 10.0, leste, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void FatiaEmCoordenadaUtmNaoRespondeCota()
    {
        // O caso que levou a barreira de degenerado a ser refeita. Um
        // triângulo de 25 cm de lado com a espessura do menor passo que o
        // double representa em UTM passava como triângulo bom e devolvia cota
        // errada em dezenas de metros, com a consulta dizendo que deu certo.
        const double x0 = 512_345.678;
        const double y0 = 7_456_789.012;

        var espessura = Math.BitIncrement(y0) - y0;   // 1 ulp, ~1 nanômetro

        var fatia = new Triangle(
            new Point3(x0, y0, 100),
            new Point3(x0 + 0.25, y0, 100),
            new Point3(x0 + 0.125, y0 + espessura, 140));

        Assert.True(fatia.IsDegenerate2D, "Uma fatia de 1 nanômetro não pode passar por triângulo bom.");

        var tin = new Tin([fatia]);
        Assert.Equal(0, tin.TriangleCount);
        Assert.Equal(1, tin.DiscardedTriangleCount);
        Assert.False(tin.TryGetZ(x0 + 0.125, y0, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TrianguloPequenoEmCoordenadaUtmContinuaValendo()
    {
        // O outro lado do mesmo ajuste: a barreira não pode ficar tão severa a
        // ponto de recusar triângulo legítimo. 25 cm é a densidade máxima de
        // levantamento prevista no plano de requisitos.
        const double x0 = 512_345.678;
        const double y0 = 7_456_789.012;

        var bom = new Triangle(
            new Point3(x0, y0, 100.00),
            new Point3(x0 + 0.25, y0, 100.01),
            new Point3(x0 + 0.125, y0 + 0.216, 100.02));

        Assert.False(bom.IsDegenerate2D, "Um triângulo de 25 cm é terreno normal, não degenerado.");

        var tin = new Tin([bom]);
        Assert.Equal(1, tin.TriangleCount);
        Assert.True(tin.TryGetZ(x0 + 0.125, y0 + 0.05, out var z));
        Assert.InRange(z, 100.0, 100.02);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AFolgaDaBordaNaoPodeSerLargaDemais()
    {
        // Prende a ordem de grandeza da tolerância baricêntrica. Sem isto, ela
        // podia ser afrouxada em um milhão de vezes sem nenhum teste reclamar,
        // e o terreno passaria a responder cota metros além da sua borda.
        Point3 P(double x, double y) => new(x, y, 10 + x / 10.0);

        var tin = new Tin([new Triangle(P(0, 0), P(100, 0), P(0, 100))]);

        // 1 mm fora da hipotenusa: é a tolerância dos verificadores de regra
        // sagrada, e nesse ponto o terreno já acabou.
        Assert.False(
            tin.TryGetZ(50 + 0.001, 50 + 0.001, out _),
            "Um ponto a 1 mm fora da borda não pode ganhar cota.");

        // E 1 mm para dentro continua sendo terreno.
        Assert.True(tin.TryGetZ(50 - 0.001, 50 - 0.001, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void VerticeComNaoNumeroNaoViraCota()
    {
        // Um levantamento com ponto defeituoso vira uma TinSurface com vértice
        // NaN. NaN não dispara nada sozinho: toda comparação com ele é falsa,
        // então ele atravessa o teste de dentro/fora e sai como cota.
        var comNaN = new Triangle(
            new Point3(0, 0, 0),
            new Point3(10, 0, double.NaN),
            new Point3(0, 10, 0));

        var comInfinito = new Triangle(
            new Point3(0, 0, 0),
            new Point3(10, 0, double.PositiveInfinity),
            new Point3(0, 10, 0));

        var xInvalido = new Triangle(
            new Point3(0, 0, 0),
            new Point3(double.NaN, 0, 5),
            new Point3(0, 10, 0));

        var tin = new Tin([comNaN, comInfinito, xInvalido]);

        Assert.Equal(0, tin.TriangleCount);
        Assert.Equal(3, tin.DiscardedTriangleCount);
        Assert.False(tin.TryGetZ(2, 2, out var z));
        Assert.Equal(0, z);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 50)]
    [InlineData(double.NegativeInfinity, 50)]
    [InlineData(50, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, double.PositiveInfinity)]
    [Trait("Etapa", "1")]
    public void ConsultaInfinitaNaoViraCota(double x, double y)
    {
        // Infinito não é NaN, então a guarda antiga deixava passar: os pesos
        // viravam ∞ - ∞ = NaN e o ponto era aceito com cota NaN.
        Point3 P(double px, double py) => new(px, py, 10 + px / 10.0);
        var tin = new Tin([new Triangle(P(0, 0), P(100, 0), P(0, 100))]);

        Assert.False(tin.TryGetZ(x, y, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaBoaNaoDescartaNada()
    {
        Point3 P(double x, double y) => new(x, y, 10 + x / 10.0);

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(10, 0), P(10, 10)),
            new Triangle(P(0, 0), P(10, 10), P(0, 10)),
        ]);

        Assert.Equal(2, tin.TriangleCount);
        Assert.Equal(0, tin.DiscardedTriangleCount);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaNulaERecusadaNaConstrucao()
    {
        Assert.Throws<ArgumentNullException>(() => new Tin(null!));
    }
}
