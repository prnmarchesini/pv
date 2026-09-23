namespace UFV.Core.Tests;

/// <summary>
/// A configuração do sistema: os limites que valem para o projeto inteiro.
///
/// É o objeto mais chato de testar e o mais caro de errar. Ele quase não
/// calcula — só guarda números —, e por isso o erro nunca aparece nele: aparece
/// três etapas adiante, numa mesa marcada que não devia ser, ou pior, numa que
/// devia e não foi.
///
/// A coerência entre os números é o que este arquivo trava: mínimo menor que
/// máximo, faixa possível, fração entre zero e um.
/// </summary>
public class SystemConfigurationTests
{
    private const double Grau = Math.PI / 180;

    private static SystemConfiguration Padrao() => SystemConfiguration.Default;

    // ------------------------------------------------- os números do Renan

    /// <summary>
    /// O que ele informou em 23/09/2026: ponta baixa de 0,30 a 0,80 m, enterro
    /// mínimo de 0,90 m, e mesa reprovada acima de 10° de declividade
    /// longitudinal.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoTrazOsNumerosQueORenanInformou()
    {
        var config = Padrao();

        Assert.True(config.IsValid, config.WhyInvalid);

        Assert.Equal(0.30, config.MinLowEdge, 9);
        Assert.Equal(0.80, config.MaxLowEdge, 9);
        Assert.Equal(0.90, config.MinEmbedment, 9);
        Assert.Equal(10, config.MaxLongitudinalSlopeDegrees!.Value, 9);
        Assert.Equal(10 * Grau, config.MaxLongitudinalSlope!.Value, 9);
    }

    /// <summary>
    /// O azimute é o rumo para onde a MESA OLHA, contado do norte no sentido
    /// horário. No Brasil o normal é zero — mesa olhando para o norte.
    ///
    /// A convenção está fixada em teste porque ela é a que espelha a usina
    /// inteira se for trocada, e em planta isso não aparece.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OAzimutePadraoEZeroQueEOlharParaONorte()
    {
        Assert.Equal(0, Padrao().FacingAzimuthRadians, 9);
    }

    // --------------------------------------------- o comprimento do pilar

    /// <summary>
    /// A missão do plugin, nas palavras do Renan: "vc vai considerar o mínimo
    /// enterrado, o que precisa para cima, e me dar o tamanho ideal".
    ///
    /// O comprimento é <b>saída</b>, não restrição. Não existe lista comercial
    /// travando nada — ele filtra depois, e o que ficar maior ou menor é
    /// problema dele.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    // A mesa 2V dele sobe 1,161 m no pilar. Com a ponta baixa no mínimo:
    [InlineData(1.461, 2.361)]
    // E no máximo da faixa:
    [InlineData(1.961, 2.861)]
    [InlineData(0, 0.9)]
    public void OComprimentoIdealEOQueFicaDeForaMaisOEnterroMinimo(double livre, double esperado)
    {
        Assert.Equal(esperado, Padrao().IdealPillarLength(livre), 9);
    }

    /// <summary>
    /// Sem teto configurado, comprimento nenhum é reprovado — é o padrão, e é
    /// o que o Renan pediu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void SemTetoNenhumComprimentoEReprovado()
    {
        var config = Padrao();

        Assert.Null(config.MaxPillarLength);
        Assert.Null(config.WhyPillarIsTooLong(2.861));
        Assert.Null(config.WhyPillarIsTooLong(9));
    }

    /// <summary>
    /// Quem quiser o teto, liga: aí o pilar que passa dele é marcado, como
    /// manda a regra sagrada 4 — marcado, nunca encurtado, porque encurtar
    /// mudaria a altura livre que o projetista pediu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ComTetoOPilarQuePassaEMarcado()
    {
        var config = Padrao() with { MaxPillarLength = 2.50 };

        Assert.Null(config.WhyPillarIsTooLong(2.361));

        var motivo = config.WhyPillarIsTooLong(2.861);

        Assert.NotNull(motivo);
        Assert.Contains("2,861", motivo!);
        Assert.Contains("2,5", motivo);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0)]
    [InlineData(-2.5)]
    [InlineData(99)]
    [InlineData(double.NaN)]
    public void TetoDePilarImpossivelERecusado(double teto)
    {
        var config = Padrao() with { MaxPillarLength = teto };

        Assert.False(config.IsValid);
        Assert.Contains("pilar", config.WhyInvalid!);
    }

    /// <summary>
    /// Teto menor que o enterro mínimo é projeto impossível: não sobraria nada
    /// acima do chão, que é a regra sagrada 1 ao contrário.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void TetoMenorQueOEnterroERecusado()
    {
        var config = Padrao() with { MaxPillarLength = 0.5 };

        Assert.False(config.IsValid);
        Assert.Contains("acima do chão", config.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ComprimentoIdealDeAlturaImpossivelERecusado(double livre)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Padrao().IdealPillarLength(livre));
    }

    // ----------------------------------------------- coerência entre limites

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0.80, 0.30)]
    [InlineData(-0.1, 0.80)]
    [InlineData(0.30, 99)]
    public void FaixaDaPontaBaixaIncoerenteERecusada(double minimo, double maximo)
    {
        var config = Padrao() with { MinLowEdge = minimo, MaxLowEdge = maximo };

        Assert.False(config.IsValid);
        Assert.Contains("ponta baixa", config.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(1.5, 0.9)]
    [InlineData(-0.1, 1.6)]
    [InlineData(0.9, 99)]
    public void FaixaDeEnterroIncoerenteERecusada(double minimo, double maximo)
    {
        var config = Padrao() with { MinEmbedment = minimo, MaxEmbedment = maximo };

        Assert.False(config.IsValid);
        Assert.Contains("enterro", config.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0.6, 0.2)]
    [InlineData(-0.1, 0.5)]
    public void FaixaDeDegrauIncoerenteERecusada(double minimo, double maximo)
    {
        var config = Padrao() with { MinStep = minimo, MaxStep = maximo };

        Assert.False(config.IsValid);
        Assert.Contains("degrau", config.WhyInvalid!);
    }

    // ------------------------------------------------------- outros limites

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(1000)]
    public void PitchImpossivelERecusado(double pitch)
    {
        Assert.False((Padrao() with { Pitch = pitch }).IsValid);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-1)]
    [InlineData(-0.001)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void ToleranciaDeInvasaoForaDeZeroAUmERecusada(double tolerancia)
    {
        var config = Padrao() with { BumpToleranceFraction = tolerancia };

        Assert.False(config.IsValid);
        Assert.Contains("invasão", config.WhyInvalid!);
    }

    /// <summary>
    /// A tolerância é fração da mesa, e não contagem: mesa de 28 módulos e
    /// mesa de 14 não podem ter a mesma permissão absoluta. Um inteiro só
    /// significaria coisas diferentes em cada mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void QuantosModulosPodemInvadirSaiDaFracao()
    {
        var config = Padrao() with { BumpToleranceFraction = 0.25 };

        // 25% de 28 módulos são 7.
        Assert.Equal(7, config.BumpToleranceFor(28));

        // E nunca arredonda para cima: 25% de 10 são 2,5, e vale 2.
        Assert.Equal(2, config.BumpToleranceFor(10));

        // Tolerância zero é o padrão: nenhum módulo pode invadir.
        Assert.Equal(0, Padrao().BumpToleranceFor(28));
    }

    /// <summary>
    /// O limite de declividade longitudinal é opcional: sem ele, a mesa
    /// acompanha o terreno sem reprovar por inclinação.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OLimiteDeDeclividadePodeNaoExistir()
    {
        var config = Padrao() with { MaxLongitudinalSlope = null };

        Assert.True(config.IsValid, config.WhyInvalid);
        Assert.Null(config.MaxLongitudinalSlope);
        Assert.Null(config.MaxLongitudinalSlopeDegrees);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(90)]
    [InlineData(double.NaN)]
    public void LimiteDeDeclividadeImpossivelERecusado(double graus)
    {
        var config = Padrao() with { MaxLongitudinalSlope = graus * Grau };

        Assert.False(config.IsValid);
        Assert.Contains("declividade", config.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(500)]
    public void LimiteDeEspacamentoImpossivelERecusado(double limite)
    {
        var config = Padrao() with { MaxGapBeforeBreak = limite };

        Assert.False(config.IsValid);
        Assert.Contains("espaçamento", config.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-0.1)]
    [InlineData(7)]
    [InlineData(double.NaN)]
    public void AzimuteForaDeUmaVoltaERecusado(double azimute)
    {
        var config = Padrao() with { FacingAzimuthRadians = azimute };

        Assert.False(config.IsValid);
        Assert.Contains("azimute", config.WhyInvalid!);
    }

    // ------------------------------------------------------------- texto

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoTrazOsNumerosComVirgula()
    {
        var texto = Padrao().Describe();

        Assert.Contains("0,3", texto);
        Assert.Contains("0,8", texto);
        Assert.Contains("0,9", texto);
        Assert.DoesNotContain("0.3", texto);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoDeUmaConfiguracaoQuebradaDizOMotivo()
    {
        var texto = (Padrao() with { MinLowEdge = 9 }).Describe();

        Assert.Contains("inválida", texto);
        Assert.Contains("ponta baixa", texto);
    }

    /// <summary>
    /// Cada motivo nomeia um campo só, como no resto do Core: a tela do 4.4
    /// tem uma dúzia de campos, e "configuração inválida" manda procurar no
    /// escuro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void TodaConfiguracaoInvalidaDizPorQue()
    {
        SystemConfiguration[] quebradas =
        [
            Padrao() with { MinLowEdge = 9 },
            Padrao() with { Pitch = -1 },
            Padrao() with { MinEmbedment = 5 },
            Padrao() with { MaxStep = -1 },
            Padrao() with { FacingAzimuthRadians = 9 },
            Padrao() with { MaxPillarLength = 0.2 },
        ];

        foreach (var config in quebradas)
        {
            Assert.False(config.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(config.WhyInvalid));
        }
    }

    // ------------------------------- o que a revisão do 4.1 mandou cobrir

    /// <summary>
    /// A pendência que a etapa 3 deixou aberta, agora fechada em código: o
    /// azimute de mira e o azimute do eixo que sobe a inclinação são opostos.
    ///
    /// O +Y local aponta da ponta baixa para a alta, ou seja, para o lado
    /// contrário ao que a mesa olha. Passar o azimute de mira direto para a
    /// rotação põe a usina inteira virada para o lado errado, com o desenho
    /// perfeito e a produção pela metade.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0, 180)]
    [InlineData(180, 0)]
    [InlineData(90, 270)]
    [InlineData(350, 170)]
    public void OEixoQueSobeApontaParaOLadoOpostoAoQueAMesaOlha(double mira, double subida)
    {
        var config = Padrao() with { FacingAzimuthRadians = mira * Grau };

        Assert.Equal(subida * Grau, config.UpslopeAzimuthRadians, 9);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OAzimuteEmGrausAcompanhaORadiano()
    {
        var config = Padrao() with { FacingAzimuthRadians = 45 * Grau };

        Assert.Equal(45, config.FacingAzimuthDegrees, 9);
    }

    /// <summary>
    /// O achado da revisão: um teto que não comporta nem a ponta baixa mínima
    /// faz TODA mesa da usina nascer marcada — e a configuração era aceita
    /// como coerente.
    ///
    /// Com enterro de 0,90 e ponta baixa mínima de 0,30, o teto precisa passar
    /// de 1,20 m só para existir uma mesa possível.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(1.0)]
    [InlineData(1.15)]
    [InlineData(1.20)]
    public void TetoQueNaoComportaNemAPontaBaixaMinimaERecusado(double teto)
    {
        var config = Padrao() with { MaxPillarLength = teto };

        Assert.False(config.IsValid);
        Assert.Contains("nenhuma mesa caberia", config.WhyInvalid!);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TetoQueComportaAPontaBaixaMinimaEAceito()
    {
        Assert.True((Padrao() with { MaxPillarLength = 1.21 }).IsValid);
    }

    /// <summary>
    /// Enterro mínimo de um bilionésimo de metro é pilar que flutua, e passava
    /// pela conferência de sinal.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void EnterroMinimoMenorQueUmMilimetroERecusado()
    {
        var config = Padrao() with { MinEmbedment = 1e-9 };

        Assert.False(config.IsValid);
        Assert.Contains("milímetro", config.WhyInvalid!);
    }

    /// <summary>
    /// O enterro máximo deixou de ser campo que ninguém lê: ele vale quando o
    /// comprimento do pilar vem imposto de fora.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OEnterroForaDaFaixaEDenunciado()
    {
        var config = Padrao();

        Assert.Null(config.WhyEmbedmentIsWrong(0.90));
        Assert.Null(config.WhyEmbedmentIsWrong(1.50));
        Assert.Null(config.WhyEmbedmentIsWrong(2.00));

        Assert.Contains("mínimo", config.WhyEmbedmentIsWrong(0.5)!);
        Assert.Contains("máximo", config.WhyEmbedmentIsWrong(2.5)!);
        Assert.NotNull(config.WhyEmbedmentIsWrong(double.NaN));
    }

    /// <summary>
    /// "Não sei medir" não pode virar "está bom": comprimento que não é número
    /// é marcado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ComprimentoQueNaoENumeroEMarcado()
    {
        Assert.NotNull(Padrao().WhyPillarIsTooLong(double.NaN));
        Assert.NotNull(Padrao().WhyPillarIsTooLong(double.PositiveInfinity));
    }

    /// <summary>
    /// A fronteira exata do teto: um pilar do tamanho do teto cabe. Sem este
    /// teste, trocar o menor-ou-igual por menor passaria impune.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OPilarDoTamanhoExatoDoTetoCabe()
    {
        var config = Padrao() with { MaxPillarLength = 2.50 };

        Assert.Null(config.WhyPillarIsTooLong(2.50));
        Assert.NotNull(config.WhyPillarIsTooLong(2.5001));
    }

    /// <summary>
    /// Configuração quebrada não responde número. Com a fração em NaN, o cast
    /// devolvia int.MinValue — uma tolerância de menos dois bilhões numa
    /// comparação adiante é estouro esperando acontecer.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoQuebradaNaoRespondeTolerancia()
    {
        Assert.Equal(0, (Padrao() with { BumpToleranceFraction = double.NaN }).BumpToleranceFor(28));
        Assert.Equal(0, (Padrao() with { BumpToleranceFraction = -0.5 }).BumpToleranceFor(28));
        Assert.Equal(0, Padrao().BumpToleranceFor(0));
        Assert.Equal(0, Padrao().BumpToleranceFor(-5));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoQuebradaNaoRespondeComprimentoDePilar()
    {
        var quebrada = Padrao() with { MinEmbedment = double.NaN };

        Assert.Throws<InvalidOperationException>(() => quebrada.IdealPillarLength(1.461));
    }

    /// <summary>
    /// Registrado como comportamento, não como acidente: numa mesa pequena uma
    /// fração pequena dá tolerância zero, e quem ligou a tolerância não é
    /// avisado. Está em PROGRESSO.md esperando a palavra do Renan.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void NumaMesaPequenaUmaFracaoPequenaDaToleranciaZero()
    {
        var config = Padrao() with { BumpToleranceFraction = 0.25 };

        Assert.Equal(0, config.BumpToleranceFor(3));
        Assert.Equal(1, config.BumpToleranceFor(4));
    }

    /// <summary>
    /// Tolerância um é legítimo e desliga a regra sagrada 4: quem puser isso
    /// está dizendo que aceita qualquer invasão.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ToleranciaUmAceitaTodosOsModulos()
    {
        var config = Padrao() with { BumpToleranceFraction = 1.0 };

        Assert.True(config.IsValid);
        Assert.Equal(28, config.BumpToleranceFor(28));
    }

    /// <summary>
    /// A rede de escala do comprimento ideal é a mesma do resto do Core, e não
    /// uma segunda: ela vem de PillarSizing.Length.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OComprimentoIdealHerdaARedeDeEscalaDoCore()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Padrao().IdealPillarLength(1e9));
    }
}
