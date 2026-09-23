using UFV.Core.Invariants;

namespace UFV.Core.Tests;

/// <summary>
/// A conta que vira altura de pilar.
///
/// É a conta mais barata do projeto e a mais cara de errar: ela não tem nada
/// que possa dar errado de forma visível. Um seno trocado por cosseno, um grau
/// no lugar de um radiano, ou a sobra da tesoura esquecida — e a usina inteira
/// sai com o mesmo erro, constante e plausível. Nada fica estranho; tudo fica
/// errado.
/// </summary>
public class PillarSizingTests
{
    private const double Grau = Math.PI / 180;

    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() =>
        new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    /// <summary>
    /// O exemplo trabalhado do plano, passo 3.5: "tesoura 4 m a 10°:
    /// 0,30 / 0,647 / 0,995 m nas posições 0, 2 e 4 m".
    ///
    /// Os números foram conferidos à mão: 0,30 + 2 × sen 10° = 0,6472, e
    /// 0,30 + 4 × sen 10° = 0,9946.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, 0.300)]
    [InlineData(2, 0.647)]
    [InlineData(4, 0.995)]
    public void OExemploDoPlanoFecha(double distancia, double esperado)
    {
        Assert.Equal(esperado, PillarSizing.FreeHeight(0.30, distancia, 10 * Grau), 3);
    }

    /// <summary>
    /// Em mesa deitada o pilar tem a altura livre e nada mais: sem inclinação,
    /// a distância não levanta nada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void SemInclinacaoADistanciaNaoLevantaNada()
    {
        Assert.Equal(0.30, PillarSizing.FreeHeight(0.30, 40, 0), 9);
    }

    /// <summary>
    /// É seno, e não tangente nem cosseno. A 30° a diferença entre seno e
    /// tangente já é de 15 cm por metro, e nenhuma das duas parece absurda no
    /// desenho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AContaEComSenoENaoComTangente()
    {
        var livre = PillarSizing.FreeHeight(0, 1, 30 * Grau);

        Assert.Equal(0.5, livre, 9);
        Assert.NotEqual(Math.Tan(30 * Grau), livre, 3);
    }

    /// <summary>
    /// A entrada é em radianos, como manda a arquitetura. Passar 20 achando
    /// que são graus daria sen(20 rad) — e 20 radianos são mais de três voltas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AInclinacaoEEmRadianos()
    {
        Assert.Equal(
            0.30 + 2.5 * Math.Sin(20 * Grau),
            PillarSizing.FreeHeight(0.30, 2.5, 20 * Grau),
            9);

        // E 20 "graus" digitados como radianos são recusados, em vez de
        // devolverem um número plausível.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarSizing.FreeHeight(0.30, 2.5, 20));
    }

    // ------------------------------------------------ a mesa 2V do Renan

    /// <summary>
    /// A ligação que faltava: a distância não é digitada à mão, ela vem da
    /// geometria da mesa — que é quem sabe da sobra da tesoura.
    ///
    /// sobra = (4,788 − 3,000) / 2 = 0,894
    /// P3 = M1 + (0,894 + 2,500) × sen 20° = M1 + 1,161
    ///
    /// Sem este teste, os dois lados nunca se encontravam: dava para apagar a
    /// sobra da geometria e a fórmula continuaria "certa" sozinha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ADistanciaVemDaGeometriaDaMesaEJaTrazASobraDaTesoura()
    {
        var mesa = Mesa();
        var geo = TableGeometry.Local(
            mesa, PillarTable.Distribute(mesa.Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07));

        Assert.Equal(0.894, geo.RafterOffset, 3);
        Assert.Equal(3.394, geo.PillarRow, 3);

        var subida = PillarSizing.FreeHeight(0, geo.PillarRow, 20 * Grau);

        Assert.Equal(1.161, subida, 3);
        Assert.Equal(1.461, PillarSizing.FreeHeight(0.30, geo.PillarRow, 20 * Grau), 3);
    }

    /// <summary>
    /// O pilar inteiro é a altura livre mais o enterro. Com a ponta baixa no
    /// mínimo da faixa do Renan: 0,30 + 1,161 + 0,90 = 2,361 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OPilarInteiroSomaAAlturaLivreEOEnterro()
    {
        var livre = PillarSizing.FreeHeight(0.30, 3.394, 20 * Grau);

        Assert.Equal(2.361, PillarSizing.Length(livre, 0.90), 3);
    }

    // --------------------------------------------------- entradas recusadas

    /// <summary>
    /// Inclinação negativa devolvia altura livre negativa, e o pilar de
    /// comprimento negativo seguia adiante como se coubesse. Acima de 90° a
    /// mesa está virada, e 120° devolvia 3,24 m em silêncio.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-0.01)]
    [InlineData(-30 * Grau)]
    [InlineData(120 * Grau)]
    [InlineData(Math.PI)]
    [InlineData(double.NaN)]
    public void InclinacaoForaDoQuartoDeVoltaERecusada(double tilt)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarSizing.FreeHeight(0.30, 3.394, tilt));
    }

    /// <summary>
    /// Noventa graus é mesa em pé — extremo, mas geometricamente válido, e o
    /// limite tem que ser inclusivo para a faixa ser previsível.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void NoventaGrausEOLimiteEEhAceito()
    {
        Assert.Equal(0.30 + 3.394, PillarSizing.FreeHeight(0.30, 3.394, PillarSizing.MaiorInclinacao), 9);
    }

    [Theory]
    [Trait("Etapa", "3")]
    // Ponta baixa negativa dava pilar de comprimento negativo, sem um aviso.
    [InlineData(-0.01, 3.394)]
    [InlineData(-5, 3.394)]
    // Rede para erro de escala: quem digitou 300 achando centímetro.
    [InlineData(300, 3.394)]
    [InlineData(0.30, -1)]
    [InlineData(0.30, 300)]
    [InlineData(double.NaN, 3.394)]
    [InlineData(0.30, double.PositiveInfinity)]
    public void MedidaImpossivelERecusada(double m1, double distancia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarSizing.FreeHeight(m1, distancia, 20 * Grau));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ComprimentoComMedidaImpossivelERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSizing.Length(-1, 0.90));
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSizing.Length(1.46, -0.90));
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSizing.Length(double.NaN, 0.90));
    }
}

/// <summary>
/// O verificador da regra sagrada 1, que este passo torna possível pela
/// primeira vez: antes dele a altura livre e o embutimento eram uma frase; a
/// partir dele são uma conta que pode dar errado.
/// </summary>
public class FloatingPillarTests
{
    [Fact]
    [Trait("Etapa", "3")]
    public void UmPilarNormalPassa()
    {
        Assert.Null(FloatingPillar.Check(1.461, 0.90));
    }

    /// <summary>
    /// Altura livre zero é mesa pousada no chão, com os módulos raspando o
    /// terreno. Passava antes: a conta dava zero, nada era negativo, e nenhuma
    /// conferência de sinal reclamava.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaPousadaNoChaoERecusada()
    {
        var motivo = FloatingPillar.Check(0, 0.90);

        Assert.NotNull(motivo);
        Assert.Contains("altura livre", motivo!);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void PilarEnterradoAteAMesaERecusado()
    {
        var motivo = FloatingPillar.Check(-0.5, 0.90);

        Assert.NotNull(motivo);
        Assert.Contains("enterrado até a mesa", motivo!);
    }

    /// <summary>
    /// Embutimento zero é pilar apoiado na superfície, que nem pilar é: vira
    /// na primeira ventania.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PilarApoiadoNaSuperficieERecusado()
    {
        var motivo = FloatingPillar.Check(1.461, 0);

        Assert.NotNull(motivo);
        Assert.Contains("não está enterrado", motivo!);
    }

    /// <summary>
    /// Embutimento negativo é o pilar que não chega ao chão — literalmente
    /// flutuando, que é o nome da regra.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PilarQueNaoChegaAoTerrenoERecusado()
    {
        var motivo = FloatingPillar.Check(1.461, -0.2);

        Assert.NotNull(motivo);
        Assert.Contains("não chega ao terreno", motivo!);
    }

    /// <summary>
    /// A tolerância é a de regra, um milímetro. Meio milímetro acima do chão é
    /// mesa pousada para qualquer efeito de obra; aprovar isso seria cumprir a
    /// letra da regra contra o que ela quer dizer.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0.0011, 0.90, true)]
    [InlineData(0.0009, 0.90, false)]
    [InlineData(1.461, 0.0011, true)]
    [InlineData(1.461, 0.0009, false)]
    public void AToleranciaEDeUmMilimetro(double livre, double enterro, bool passa)
    {
        var motivo = FloatingPillar.Check(livre, enterro);

        Assert.Equal(passa, motivo is null);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void MedidaNaoFinitaERecusada()
    {
        Assert.NotNull(FloatingPillar.Check(double.NaN, 0.90));
        Assert.NotNull(FloatingPillar.Check(1.461, double.PositiveInfinity));
    }
}
