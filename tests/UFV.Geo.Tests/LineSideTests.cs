using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// De que lado da linha de alinhamento ficam as mesas.
///
/// Parece a conta mais boba do projeto, e é a que tem o pior modo de falha:
/// trocar o sinal põe a usina inteira do outro lado da linha. Em planta isso
/// até aparece — mas só se alguém souber de que lado deveria estar, e é
/// exatamente essa a informação que o desenho não carrega sozinho.
/// </summary>
public class LineSideTests
{
    /// <summary>
    /// A convenção, fixada de propósito: esquerda e direita de quem caminha do
    /// primeiro ponto para o segundo.
    ///
    /// Linha apontando para o leste: o norte fica à esquerda, o sul à direita.
    /// É a mesma noção de quem anda na rua, e é a única que não depende de
    /// para que lado a tela está virada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CaminhandoParaOLesteONorteFicaAEsquerda()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);

        Assert.Equal(LineSide.Left, LineSides.Of(a, b, new Point3(5, 3, 0)));
        Assert.Equal(LineSide.Right, LineSides.Of(a, b, new Point3(5, -3, 0)));
    }

    /// <summary>
    /// Desenhar a MESMA linha ao contrário troca os dois lados. É o motivo de
    /// o alinhamento guardar o sentido, e não só a reta: sem ele, reabrir o
    /// desenho poria as mesas do lado errado sem nada parecer estranho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void DesenharALinhaAoContrarioTrocaOsLados()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);
        var ponto = new Point3(5, 3, 0);

        Assert.Equal(LineSide.Left, LineSides.Of(a, b, ponto));
        Assert.Equal(LineSide.Right, LineSides.Of(b, a, ponto));
    }

    [Theory]
    [Trait("Etapa", "4")]
    // Linha para o norte: o leste fica à direita.
    [InlineData(0, 0, 0, 10, 3, 5, LineSide.Right)]
    [InlineData(0, 0, 0, 10, -3, 5, LineSide.Left)]
    // Diagonal, para o nordeste.
    [InlineData(0, 0, 10, 10, 0, 10, LineSide.Left)]
    [InlineData(0, 0, 10, 10, 10, 0, LineSide.Right)]
    public void OLadoSegueOSentidoDoTracado(
        double ax, double ay, double bx, double by, double px, double py, LineSide esperado)
    {
        var lado = LineSides.Of(new Point3(ax, ay, 0), new Point3(bx, by, 0), new Point3(px, py, 0));

        Assert.Equal(esperado, lado);
    }

    /// <summary>
    /// Ponto sobre a linha não é um lado: é "em cima". Responder esquerda ou
    /// direita aí seria inventar uma decisão que o usuário não tomou — ele
    /// clicou em cima da linha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void PontoSobreALinhaNaoTemLado()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);

        Assert.Equal(LineSide.On, LineSides.Of(a, b, new Point3(5, 0, 0)));

        // E fora do trecho, mas ainda sobre a reta, também.
        Assert.Equal(LineSide.On, LineSides.Of(a, b, new Point3(50, 0, 0)));
        Assert.Equal(LineSide.On, LineSides.Of(a, b, new Point3(-50, 0, 0)));
    }

    /// <summary>
    /// A tolerância é de um milímetro, medida em DISTÂNCIA até a reta.
    ///
    /// Sem dividir pelo comprimento, o valor cresceria com o tamanho da linha,
    /// e um milímetro significaria coisas diferentes numa linha de 10 m e numa
    /// de 500 m — a mesma mão trêmula seria "em cima" numa e "esquerda" na
    /// outra.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(10)]
    [InlineData(500)]
    [InlineData(0.5)]
    public void AToleranciaEUmMilimetroEmQualquerComprimentoDeLinha(double comprimento)
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(comprimento, 0, 0);
        var meio = comprimento / 2;

        Assert.Equal(LineSide.On, LineSides.Of(a, b, new Point3(meio, 0.0009, 0)));
        Assert.Equal(LineSide.Left, LineSides.Of(a, b, new Point3(meio, 0.0011, 0)));
    }

    /// <summary>
    /// A cota não participa: alinhamento é coisa de planta, e os pontos vêm do
    /// desenho com Z qualquer.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ACotaNaoMudaOLado()
    {
        var a = new Point3(0, 0, 812);
        var b = new Point3(10, 0, -33);

        Assert.Equal(LineSide.Left, LineSides.Of(a, b, new Point3(5, 3, 1000)));
    }

    /// <summary>
    /// Dois pontos no mesmo lugar não definem lado nenhum. Devolver "em cima"
    /// aqui esconderia o problema: o usuário clicou duas vezes no mesmo ponto,
    /// e o alinhamento que sairia disso não teria direção.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void LinhaDeComprimentoZeroERecusada()
    {
        var a = new Point3(5, 5, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => LineSides.Of(a, a, new Point3(9, 9, 0)));

        // E "mesmo lugar" vale a tolerância: meio milímetro também não serve.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LineSides.Of(a, new Point3(5.0005, 5, 0), new Point3(9, 9, 0)));
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(0, double.PositiveInfinity, 0)]
    [InlineData(0, 0, double.NaN)]
    public void PontoComCoordenadaInvalidaERecusado(double x, double y, double z)
    {
        var ruim = new Point3(x, y, z);
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => LineSides.Of(ruim, b, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineSides.Of(a, ruim, b));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineSides.Of(a, b, ruim));
    }

    // ------------------------------------------------ distância com sinal

    /// <summary>
    /// A distância com sinal usa o mesmo critério do lado, e é ela que serve
    /// para ordenar mesas pela proximidade do alinhamento.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ADistanciaComSinalConcordaComOLado()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);

        Assert.Equal(-3, LineSides.SignedDistance(a, b, new Point3(5, 3, 0)), 9);
        Assert.Equal(3, LineSides.SignedDistance(a, b, new Point3(5, -3, 0)), 9);
        Assert.Equal(0, LineSides.SignedDistance(a, b, new Point3(5, 0, 0)), 9);
    }

    /// <summary>
    /// Em metro, e não em unidade que depende do comprimento da linha: numa
    /// diagonal, a distância de um ponto a 5 m da reta é 5 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ADistanciaEEmMetro()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(100, 100, 0);

        // O ponto (10, 0) está a 10/√2 da reta y = x.
        Assert.Equal(10 / Math.Sqrt(2), LineSides.SignedDistance(a, b, new Point3(10, 0, 0)), 9);
    }

    // ----------------------------------------------------------- utilidades

    [Fact]
    [Trait("Etapa", "4")]
    public void OOpostoDeEsquerdaEDireitaEViceVersa()
    {
        Assert.Equal(LineSide.Right, LineSide.Left.Opposite());
        Assert.Equal(LineSide.Left, LineSide.Right.Opposite());
        Assert.Equal(LineSide.On, LineSide.On.Opposite());
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void CadaLadoTemNomeEmPortugues()
    {
        Assert.Equal("esquerda", LineSide.Left.Describe());
        Assert.Equal("direita", LineSide.Right.Describe());
        Assert.Equal("sobre a linha", LineSide.On.Describe());
    }

    // --------------------------------------------- o lado como texto gravado

    [Fact]
    [Trait("Etapa", "4")]
    public void OLadoViraNomeEVolta()
    {
        foreach (var lado in new[] { LineSide.Left, LineSide.Right, LineSide.On })
        {
            Assert.True(LineSides.TryParseName(lado.Name(), out var voltou));
            Assert.Equal(lado, voltou);
        }
    }

    /// <summary>
    /// O achado da revisão do 4.2, e é o modo de falha que o passo inteiro diz
    /// querer evitar.
    ///
    /// <c>Enum.TryParse&lt;LineSide&gt;("1")</c> devolve <c>Left</c> e <c>"2"</c>
    /// devolve <c>Right</c>: o número da enumeração passa como se fosse nome.
    /// Com isso, a defesa contra renumerar a enum não existia — um registro
    /// com "1" era aceito hoje, e no dia da renumeração viraria o outro lado
    /// em silêncio, com a usina inteira do lado errado.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("+1")]
    [InlineData(" 2 ")]
    [InlineData("3")]
    public void NumeroNaoEhNomeDeLado(string texto)
    {
        Assert.False(LineSides.TryParseName(texto, out _));
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Maiúscula e minúscula importam: o nome gravado é exato, e aceitar
    // variação abriria a porta para aceitar outras coisas.
    [InlineData("left")]
    [InlineData("LEFT")]
    [InlineData(" Left")]
    [InlineData("Esquerda")]
    public void TextoQueNaoEhNomeDeLadoNaoELido(string? texto)
    {
        Assert.False(LineSides.TryParseName(texto, out _));
    }

    /// <summary>
    /// O nome gravado é em inglês e o nome mostrado é em português. São coisas
    /// diferentes de propósito: um é formato de arquivo, o outro é texto de
    /// tela, e trocar um pelo outro quebraria todo alinhamento já gravado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ONomeGravadoNaoEONomeMostrado()
    {
        Assert.Equal("Left", LineSide.Left.Name());
        Assert.Equal("esquerda", LineSide.Left.Describe());
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ADistanciaDeUmPontoLongeDemaisERecusada()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(10, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => LineSides.SignedDistance(a, b, new Point3(1e308, 1e308, 0)));
    }
}
