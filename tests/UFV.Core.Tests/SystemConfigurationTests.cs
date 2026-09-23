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
/// máximo, faixa possível, contagem não negativa.
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

    // --------------------------------------------- o comprimento do pilar

    /// <summary>
    /// A missão do plugin, nas palavras dele: "vc vai considerar o mínimo
    /// enterrado, o que precisa para cima, e me dar o tamanho ideal".
    ///
    /// O comprimento é <b>saída</b>, e só isso. Perguntei se os 2,5 m que ele
    /// compra eram teto, e a resposta foi "eu costumo comprar, volto a dizer,
    /// vc deve calcular o pilar ideal apenas".
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
    /// Não existe teto de comprimento de pilar, e o teste guarda essa ausência:
    /// um campo assim voltaria como "configuração inofensiva" e viraria um
    /// segundo dono de um número que é dele, não do plugin.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void NaoHaTetoDeComprimentoDePilar()
    {
        var campos = typeof(SystemConfiguration).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("MaxPillarLength", campos);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ComprimentoIdealDeAlturaImpossivelERecusado(double livre)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Padrao().IdealPillarLength(livre));
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

    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoQuebradaNaoRespondeComprimentoDePilar()
    {
        var quebrada = Padrao() with { MinEmbedment = double.NaN };

        Assert.Throws<InvalidOperationException>(() => quebrada.IdealPillarLength(1.461));
    }

    // ------------------------------------------------------------- azimute

    /// <summary>
    /// O azimute é o rumo para onde a MESA OLHA, contado do norte no sentido
    /// horário. No Brasil o normal é zero — mesa olhando para o norte.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OAzimutePadraoEZeroQueEOlharParaONorte()
    {
        Assert.Equal(0, Padrao().FacingAzimuthRadians, 9);
    }

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

    // ------------------------------------------- tolerância de invasão

    /// <summary>
    /// A tolerância é contagem de módulos, como a regra sagrada 4 manda ao pé
    /// da letra: "o usuário define quantos módulos por mesa podem estourar a
    /// ponta baixa (ex.: 5 em 20)".
    ///
    /// Eu tinha guardado fração, achando que contagem significaria coisas
    /// diferentes numa mesa de 28 e numa de 14. Perguntei, e ele respondeu
    /// "contagem" em 23/09/2026.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AToleranciaEContagemDeModulos()
    {
        var config = Padrao() with { BumpToleranceModules = 5 };

        Assert.True(config.IsValid, config.WhyInvalid);
        Assert.Equal(5, config.BumpToleranceFor(28));
        Assert.Equal(5, config.BumpToleranceFor(20));
    }

    /// <summary>
    /// Tolerância maior que a mesa vale a mesa inteira: cinco numa mesa de três
    /// são três. Sem o limite, a comparação adiante nunca marcaria mesa nenhuma.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ToleranciaMaiorQueAMesaValeAMesaInteira()
    {
        Assert.Equal(3, (Padrao() with { BumpToleranceModules = 5 }).BumpToleranceFor(3));
    }

    /// <summary>Zero é o padrão: nenhum módulo pode invadir.</summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoNaoDeixaNenhumModuloInvadir()
    {
        Assert.Equal(0, Padrao().BumpToleranceModules);
        Assert.Equal(0, Padrao().BumpToleranceFor(28));
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ToleranciaNegativaERecusada()
    {
        var config = Padrao() with { BumpToleranceModules = -1 };

        Assert.False(config.IsValid);
        Assert.Contains("invasão", config.WhyInvalid!);
    }

    /// <summary>
    /// Configuração quebrada não responde número: a tolerância seria usada
    /// numa comparação adiante, e responder por uma configuração que não fecha
    /// é dar um número que ninguém conferiu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoQuebradaNaoRespondeTolerancia()
    {
        var quebrada = Padrao() with { MinLowEdge = 9, BumpToleranceModules = 5 };

        Assert.Equal(0, quebrada.BumpToleranceFor(28));
        Assert.Equal(0, Padrao().BumpToleranceFor(0));
        Assert.Equal(0, Padrao().BumpToleranceFor(-5));
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
    /// O enterro máximo não é campo que ninguém lê: ele vale quando o
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
            Padrao() with { BumpToleranceModules = -3 },
        ];

        foreach (var config in quebradas)
        {
            Assert.False(config.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(config.WhyInvalid));
        }
    }
}
