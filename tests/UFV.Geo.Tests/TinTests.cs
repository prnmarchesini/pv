using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// A consulta de cota é a pergunta mais feita do projeto: cada pilar e cada
/// ponta baixa de módulo é uma dessas. Se ela erra, erra tudo depois, e erra
/// em silêncio — o número sai plausível.
///
/// Por isso os testes usam terreno analítico, onde a resposta certa é
/// calculável à mão, e não um terreno de arquivo.
/// </summary>
public class TinTests
{
    /// <summary>Plano inclinado de referência: z = 2x + 3y + 10.</summary>
    private static double ZDoPlano(double x, double y) => 2 * x + 3 * y + 10;

    /// <summary>
    /// Um quadrado de 100 m de lado sobre o plano inclinado, partido em dois
    /// triângulos pela diagonal. Dois triângulos já exercitam o essencial:
    /// interpolação, borda e a aresta compartilhada.
    /// </summary>
    private static Tin PlanoInclinado()
    {
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));

        return new Tin(
        [
            new Triangle(P(0, 0), P(100, 0), P(100, 100)),
            new Triangle(P(0, 0), P(100, 100), P(0, 100)),
        ]);
    }

    [Theory]
    [InlineData(50, 50)]     // meio do terreno, sobre a diagonal
    [InlineData(25, 10)]     // dentro do primeiro triângulo
    [InlineData(10, 25)]     // dentro do segundo
    [InlineData(99.9, 0.1)]
    [Trait("Etapa", "1")]
    public void CotaDentroDoTerrenoSaiExata(double x, double y)
    {
        var achou = PlanoInclinado().TryGetZ(x, y, out var z);

        Assert.True(achou, $"({x}, {y}) devia estar dentro do terreno.");

        // Interpolação linear sobre um plano dá o valor exato do plano; a
        // folga aqui é só o arredondamento do double.
        Assert.Equal(ZDoPlano(x, y), z, 9);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(100, 100)]
    [InlineData(0, 100)]
    [Trait("Etapa", "1")]
    public void VerticeDoTerrenoTemCota(double x, double y)
    {
        // Vértice é o caso em que dois pesos baricêntricos são zero: o ponto
        // está na borda de tudo ao mesmo tempo.
        var achou = PlanoInclinado().TryGetZ(x, y, out var z);

        Assert.True(achou, $"O vértice ({x}, {y}) devia pertencer ao terreno.");
        Assert.Equal(ZDoPlano(x, y), z, 9);
    }

    [Theory]
    [InlineData(50, 0)]      // aresta de baixo
    [InlineData(100, 50)]    // aresta da direita
    [InlineData(0, 50)]      // aresta da esquerda
    [InlineData(50, 100)]    // aresta de cima
    [InlineData(30, 30)]     // sobre a diagonal, compartilhada pelos dois
    [Trait("Etapa", "1")]
    public void PontoNaArestaTemCota(double x, double y)
    {
        // A aresta entre dois triângulos não pode virar um buraco de espessura
        // zero no meio do terreno. Nos dois triângulos a cota interpolada é a
        // mesma, então aceitar em ambos não traz ambiguidade.
        var achou = PlanoInclinado().TryGetZ(x, y, out var z);

        Assert.True(achou, $"A aresta em ({x}, {y}) devia pertencer ao terreno.");
        Assert.Equal(ZDoPlano(x, y), z, 9);
    }

    [Theory]
    [InlineData(-0.5, 50)]     // à esquerda
    [InlineData(100.5, 50)]    // à direita
    [InlineData(50, -0.5)]     // abaixo
    [InlineData(50, 100.5)]    // acima
    [InlineData(-1000, -1000)] // bem longe
    [Trait("Etapa", "1")]
    public void ForaDoTerrenoNaoTemCota(double x, double y)
    {
        // Fora do terreno não é cota zero: é ausência de resposta. Devolver 0
        // aqui poria um pilar no nível do mar sem ninguém perceber.
        var achou = PlanoInclinado().TryGetZ(x, y, out var z);

        Assert.False(achou, $"({x}, {y}) está fora do terreno e não devia ter cota.");
        Assert.Equal(0, z);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TerrenoVazioNaoRespondeNada()
    {
        var tin = new Tin([]);

        Assert.Equal(0, tin.TriangleCount);
        Assert.False(tin.TryGetZ(0, 0, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void UmTrianguloSoJaResponde()
    {
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));
        var tin = new Tin([new Triangle(P(0, 0), P(10, 0), P(0, 10))]);

        Assert.True(tin.TryGetZ(2, 3, out var z));
        Assert.Equal(ZDoPlano(2, 3), z, 9);

        // Do outro lado da hipotenusa não há terreno.
        Assert.False(tin.TryGetZ(8, 8, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void BuracoNaTriangulacaoNaoInventaCota()
    {
        // Terreno com uma falha no meio: é assim que se descobre que a
        // triangulação tem buraco, e é o que o botão Obter Coordenada do passo
        // 1.7 precisa conseguir avisar.
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(10, 0), P(10, 10)),
            // falta o triângulo (0,0)-(10,10)-(0,10)
        ]);

        Assert.True(tin.TryGetZ(8, 2, out _));
        Assert.False(tin.TryGetZ(2, 8, out _));
    }

    [Theory]
    [InlineData(double.NaN, 50)]
    [InlineData(50, double.NaN)]
    [Trait("Etapa", "1")]
    public void CoordenadaInvalidaNaoViraCota(double x, double y)
    {
        // Comparação com NaN é sempre falsa, então um NaN passaria pelo teste
        // de dentro/fora sem disparar nada e sairia como cota NaN, que
        // contamina toda conta seguinte.
        Assert.False(PlanoInclinado().TryGetZ(x, y, out var z));
        Assert.Equal(0, z);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void FacetaVerticalNaoRespondeCota()
    {
        // Uma parede de talude aparece na malha como triângulo sem área em
        // planta. Sobre ela não existe "a cota", existem infinitas. Quem
        // responde é o triângulo vizinho.
        var parede = new Triangle(
            new Point3(0, 0, 0),
            new Point3(10, 0, 0),
            new Point3(5, 0, 8));

        Assert.True(parede.IsDegenerate2D);
        Assert.False(new Tin([parede]).TryGetZ(5, 0, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void VerticeRepetidoNaoQuebra()
    {
        var agulha = new Triangle(
            new Point3(0, 0, 10),
            new Point3(0, 0, 10),
            new Point3(10, 10, 20));

        Assert.True(agulha.IsDegenerate2D);
        Assert.False(new Tin([agulha]).TryGetZ(5, 5, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TerrenoPlanoDevolveSempreAMesmaCota()
    {
        // Terreno plano é o caso em que o erro passa despercebido: qualquer
        // conta errada de peso ainda devolve a mesma cota.
        var tin = new Tin(
        [
            new Triangle(new Point3(0, 0, 7), new Point3(10, 0, 7), new Point3(10, 10, 7)),
            new Triangle(new Point3(0, 0, 7), new Point3(10, 10, 7), new Point3(0, 10, 7)),
        ]);

        foreach (var (x, y) in new[] { (0.0, 0.0), (5.0, 5.0), (9.9, 0.1), (10.0, 10.0) })
        {
            Assert.True(tin.TryGetZ(x, y, out var z));
            Assert.Equal(7, z, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void CoordenadaGrandeNaoPerdePrecisao()
    {
        // Terreno real vem em UTM: x na casa de 500 000, y de 7 000 000. É aí
        // que uma tolerância absoluta mal escolhida começa a mentir.
        const double x0 = 512_345.678;
        const double y0 = 7_456_789.012;

        Point3 P(double dx, double dy) => new(x0 + dx, y0 + dy, 100 + 0.05 * dx + 0.02 * dy);

        var tin = new Tin(
        [
            new Triangle(P(0, 0), P(50, 0), P(50, 50)),
            new Triangle(P(0, 0), P(50, 50), P(0, 50)),
        ]);

        Assert.True(tin.TryGetZ(x0 + 20, y0 + 30, out var z));

        // 1 mm de tolerância, que é o limite dos verificadores de regra sagrada.
        Assert.Equal(100 + 0.05 * 20 + 0.02 * 30, z, 3);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaAleatoriaResponde()
    {
        // Propriedade com semente fixa (04-testes.md): a malha é um
        // quadriculado sobre o mesmo plano, e toda consulta dentro dela tem
        // que bater com a fórmula.
        var triangulos = new List<Triangle>();
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));

        for (var i = 0; i < 10; i++)
        for (var j = 0; j < 10; j++)
        {
            double x = i * 10, y = j * 10;
            triangulos.Add(new Triangle(P(x, y), P(x + 10, y), P(x + 10, y + 10)));
            triangulos.Add(new Triangle(P(x, y), P(x + 10, y + 10), P(x, y + 10)));
        }

        var tin = new Tin(triangulos);
        var sorteio = new Random(20260922);

        for (var n = 0; n < 2_000; n++)
        {
            var x = sorteio.NextDouble() * 100;
            var y = sorteio.NextDouble() * 100;

            Assert.True(tin.TryGetZ(x, y, out var z), $"({x}, {y}) devia estar dentro.");
            Assert.Equal(ZDoPlano(x, y), z, 9);
        }
    }
}
