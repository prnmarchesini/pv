namespace UFV.Core.Tests;

/// <summary>
/// As regras de análise: o que se pinta, de que cor, e em que camada.
///
/// Análise não trava nada. A regra sagrada 4 continua mandando: a ponta baixa
/// manda, o pilar estoura se tiver que estourar. O que este arquivo trava é
/// que a análise <b>diga a verdade</b>: fora do limite pinta, dentro não
/// pinta, e "não sei medir" nunca vira "está bom".
///
/// Os limites das análises que já existem na configuração (ponta baixa,
/// enterro, declividade) vêm DELA, e não de uma segunda cópia aqui. Dois
/// donos do mesmo número é como eles começam a divergir — a revisão do 3.5 e
/// a do 4.1 pagaram por isso.
/// </summary>
public class AnalysisRulesTests
{
    private const double Grau = Math.PI / 180;

    private static AnalysisRules Padrao() => AnalysisRules.Default;

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    // ------------------------------------------------- cores do Renan

    /// <summary>
    /// Decisão do Renan em 25/09/2026: vermelho para valor abaixo do limite,
    /// azul para valor acima. Não perguntar de novo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoEVermelhoAbaixoEAzulAcima()
    {
        var regras = Padrao();

        Assert.True(regras.IsValid, regras.WhyInvalid);

        foreach (var kind in AnalysisRules.RangedKinds)
        {
            var regra = regras.Rule(kind);

            Assert.Equal(RgbColor.Red, regra.BelowColor);
            Assert.Equal(RgbColor.Blue, regra.AboveColor);
        }
    }

    /// <summary>
    /// A mesa na borda "é mantida e pintada inteira com uma cor própria, para
    /// o engenheiro decidir" (plano de requisitos). Não é abaixo nem acima de
    /// nada: uma cor só.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AMesaNaBordaTemUmaCorSo()
    {
        var regra = Padrao().EdgeRule;

        Assert.True(regra.Enabled);
        Assert.NotEqual(RgbColor.Red, regra.Color);
        Assert.NotEqual(RgbColor.Blue, regra.Color);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TodasAsAnalisesNascemLigadas()
    {
        var regras = Padrao();

        foreach (var kind in AnalysisRules.RangedKinds)
            Assert.True(regras.Rule(kind).Enabled, kind.ToString());

        Assert.True(regras.EdgeRule.Enabled);
    }

    // ------------------------------------------------- camadas

    /// <summary>
    /// "Cada análise vai em camada própria, para ligar e desligar e enxergar
    /// o padrão no terreno." Duas análises na mesma camada não se separam.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CadaAnaliseTemCamadaPropria()
    {
        var camadas = Padrao().Layers.ToList();

        Assert.Equal(5, camadas.Count);
        Assert.Equal(camadas.Count, camadas.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(camadas, camada => Assert.StartsWith(PluginInfo.PrefixoDeDados, camada));
    }

    /// <summary>
    /// O AutoCAD compara nome de camada sem diferenciar maiúscula: duas
    /// análises em "UFV_X" e "ufv_x" seriam a mesma camada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void DuasAnalisesNaMesmaCamadaSaoRecusadas()
    {
        var regras = Padrao().With(AnalysisKind.Embedment,
            Padrao().Rule(AnalysisKind.Embedment) with
            {
                Layer = Padrao().Rule(AnalysisKind.LowEdge).Layer.ToLowerInvariant(),
            });

        Assert.False(regras.IsValid);
        Assert.Contains("camada", regras.WhyInvalid!);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UFV/ANALISE")]
    [InlineData("UFV<1>")]
    [InlineData("UFV:X")]
    [InlineData("UFV\"X")]
    [InlineData("UFV*")]
    [InlineData("UFV|X")]
    [InlineData("UFV=X")]
    [InlineData(" UFV_X")]
    public void NomeDeCamadaQueOAutoCadRecusaERecusadoAqui(string nome)
    {
        Assert.NotNull(LayerName.WhyInvalid(nome));

        var regras = Padrao().With(AnalysisKind.LowEdge,
            Padrao().Rule(AnalysisKind.LowEdge) with { Layer = nome });

        Assert.False(regras.IsValid);
        Assert.Contains("camada", regras.WhyInvalid!);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void NomeDeCamadaLongoDemaisERecusado()
    {
        Assert.NotNull(LayerName.WhyInvalid(new string('A', 256)));
        Assert.Null(LayerName.WhyInvalid(new string('A', 255)));
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("UFV_ANALISE_PONTA_BAIXA")]
    [InlineData("Análise ponta baixa")]
    [InlineData("A-B.C$D")]
    public void NomeDeCamadaComumEAceito(string nome)
    {
        Assert.Null(LayerName.WhyInvalid(nome));
    }

    // ------------------------------------------------- ponta baixa

    /// <summary>
    /// A faixa vem da configuração: 0,30 a 0,80 m. Dentro não pinta.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0.30)]
    [InlineData(0.55)]
    [InlineData(0.80)]
    public void PontaBaixaDentroDaFaixaNaoPinta(double valor)
    {
        var veredito = Padrao().Evaluate(AnalysisKind.LowEdge, valor, Config());

        Assert.Equal(AnalysisOutcome.Inside, veredito.Outcome);
        Assert.Null(veredito.Color);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void PontaBaixaAbaixoDoMinimoPintaDeVermelho()
    {
        var veredito = Padrao().Evaluate(AnalysisKind.LowEdge, 0.25, Config());

        Assert.Equal(AnalysisOutcome.Below, veredito.Outcome);
        Assert.Equal(RgbColor.Red, veredito.Color);
        Assert.Equal(Padrao().Rule(AnalysisKind.LowEdge).Layer, veredito.Layer);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void PontaBaixaAcimaDoMaximoPintaDeAzul()
    {
        var veredito = Padrao().Evaluate(AnalysisKind.LowEdge, 0.85, Config());

        Assert.Equal(AnalysisOutcome.Above, veredito.Outcome);
        Assert.Equal(RgbColor.Blue, veredito.Color);
    }

    /// <summary>
    /// A cor é a que o usuário escolheu, não a fixa: trocar a cor da regra
    /// troca a cor do veredito.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ACorDoVereditoEAQueOUsuarioEscolheu()
    {
        var amarelo = new RgbColor(255, 255, 0);
        var regras = Padrao().With(AnalysisKind.LowEdge,
            Padrao().Rule(AnalysisKind.LowEdge) with { BelowColor = amarelo });

        Assert.Equal(amarelo, regras.Evaluate(AnalysisKind.LowEdge, 0.1, Config()).Color);
    }

    /// <summary>
    /// A faixa é a da configuração: mudar lá muda aqui, sem segunda cópia.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OLimiteDaPontaBaixaVemDaConfiguracao()
    {
        var config = Config() with { MinLowEdge = 0.50, MaxLowEdge = 0.60 };

        Assert.Equal(AnalysisOutcome.Below, Padrao().Evaluate(AnalysisKind.LowEdge, 0.45, config).Outcome);
        Assert.Equal(AnalysisOutcome.Above, Padrao().Evaluate(AnalysisKind.LowEdge, 0.65, config).Outcome);
        Assert.Equal(AnalysisOutcome.Inside, Padrao().Evaluate(AnalysisKind.LowEdge, 0.55, config).Outcome);
    }

    /// <summary>
    /// 0,30 calculado por um seno pode sair 0,2999999. Isso é dentro, não
    /// abaixo: a tolerância geométrica da arquitetura é 1e-6.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void UmNanometroAbaixoDoLimiteAindaEDentro()
    {
        Assert.Equal(AnalysisOutcome.Inside,
            Padrao().Evaluate(AnalysisKind.LowEdge, 0.30 - 1e-9, Config()).Outcome);
        Assert.Equal(AnalysisOutcome.Inside,
            Padrao().Evaluate(AnalysisKind.LowEdge, 0.80 + 1e-9, Config()).Outcome);
    }

    /// <summary>
    /// A folga é de um micrômetro, e não mais: dez micrômetros já é fora.
    /// Sem este teste, uma tolerância de um centímetro passaria a suíte.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void DezMicrometrosAlemDoLimiteJaEFora()
    {
        Assert.Equal(AnalysisOutcome.Below,
            Padrao().Evaluate(AnalysisKind.LowEdge, 0.30 - 1e-5, Config()).Outcome);
        Assert.Equal(AnalysisOutcome.Above,
            Padrao().Evaluate(AnalysisKind.LowEdge, 0.80 + 1e-5, Config()).Outcome);
        Assert.Equal(AnalysisOutcome.Above,
            Padrao().Evaluate(AnalysisKind.LongitudinalSlope, 10 * Grau + 1e-5, Config()).Outcome);
    }

    // ------------------------------------------------- enterro

    /// <summary>
    /// "Também se analisa embutimento abaixo do mínimo." A faixa de enterro
    /// é a da configuração: 0,90 a 2,00 m.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0.50, AnalysisOutcome.Below)]
    [InlineData(0.90, AnalysisOutcome.Inside)]
    [InlineData(1.50, AnalysisOutcome.Inside)]
    [InlineData(2.50, AnalysisOutcome.Above)]
    public void OEnterroEAnalisadoContraAFaixaDaConfiguracao(double valor, AnalysisOutcome esperado)
    {
        Assert.Equal(esperado, Padrao().Evaluate(AnalysisKind.Embedment, valor, Config()).Outcome);
    }

    // ------------------------------------------------- altura do pilar

    /// <summary>
    /// O exemplo do plano: "pintar tudo acima de 2,50 m, porque compra pilar
    /// de 2,20, 2,50 e 3,00 m". É análise, não trava: a configuração continua
    /// sem teto de pilar (teste do 4.1 guarda isso).
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AlturaDePilarAcimaDoLimiteDaAnalisePintaDeAzul()
    {
        var regras = Padrao() with { PaintPillarsLongerThan = 2.50 };

        Assert.True(regras.IsValid, regras.WhyInvalid);
        Assert.Equal(AnalysisOutcome.Above, regras.Evaluate(AnalysisKind.PillarLength, 2.6, Config()).Outcome);
        Assert.Equal(AnalysisOutcome.Inside, regras.Evaluate(AnalysisKind.PillarLength, 2.5, Config()).Outcome);
        Assert.Equal(AnalysisOutcome.Inside, regras.Evaluate(AnalysisKind.PillarLength, 0.95, Config()).Outcome);
    }

    /// <summary>
    /// O limite de altura de pilar é meu de não existir por padrão: o Renan
    /// disse que só quer o comprimento ideal e filtra ele mesmo. Sem limite,
    /// a análise não pinta ninguém, e diz isso.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void SemLimiteDeAlturaDePilarAAnaliseFicaDesligada()
    {
        Assert.Null(Padrao().PaintPillarsLongerThan);

        var veredito = Padrao().Evaluate(AnalysisKind.PillarLength, 9.0, Config());

        Assert.Equal(AnalysisOutcome.Off, veredito.Outcome);
        Assert.Null(veredito.Color);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(500)]
    public void LimiteDeAlturaDePilarImpossivelERecusado(double limite)
    {
        var regras = Padrao() with { PaintPillarsLongerThan = limite };

        Assert.False(regras.IsValid);
        Assert.Contains("pilar", regras.WhyInvalid!);
    }

    /// <summary>
    /// O 4.1 apagou <c>MaxPillarLength</c> da configuração por palavra do
    /// Renan, e o limite da análise não pode voltar com esse nome nem com
    /// nenhum nome que a configuração use: na tela do 4.4 os dois objetos
    /// ficam lado a lado, e campo homônimo é campo confundido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void NenhumCampoDasRegrasTemONomeDeUmCampoDaConfiguracao()
    {
        // Os campos de dado são os parâmetros do construtor do record; as
        // propriedades calculadas (IsValid, WhyInvalid) se chamam igual nos
        // dois de propósito, por convenção do Core.
        static IEnumerable<string> Campos(Type tipo) =>
            tipo.GetConstructors().Single().GetParameters().Select(p => p.Name!);

        var configuracao = Campos(typeof(SystemConfiguration)).ToHashSet();
        var regras = Campos(typeof(AnalysisRules)).ToList();

        Assert.Empty(regras.Intersect(configuracao));
        Assert.DoesNotContain("MaxPillarLength", regras);
        Assert.Contains(nameof(AnalysisRules.PaintPillarsLongerThan), regras);
    }

    /// <summary>
    /// Só ponta baixa e enterro têm mínimo; nas outras duas a cor "abaixo"
    /// nunca é usada, e a tela do 4.4 precisa saber para escondê-la.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(AnalysisKind.LowEdge, true)]
    [InlineData(AnalysisKind.Embedment, true)]
    [InlineData(AnalysisKind.PillarLength, false)]
    [InlineData(AnalysisKind.LongitudinalSlope, false)]
    public void SoPontaBaixaEEnterroTemMinimo(AnalysisKind kind, bool temMinimo)
    {
        Assert.Equal(temMinimo, AnalysisRules.HasMinimum(kind));

        if (!temMinimo)
        {
            var regras = Padrao() with { PaintPillarsLongerThan = 2.5 };

            Assert.NotEqual(AnalysisOutcome.Below,
                regras.Evaluate(kind, 0, Config()).Outcome);
        }
    }

    // ------------------------------------------------- declividade

    /// <summary>
    /// "Se o usuário tiver definido um limite na configuração, o que passar é
    /// pintado." O limite é o da configuração (10°), em radianos.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(5, AnalysisOutcome.Inside)]
    [InlineData(10, AnalysisOutcome.Inside)]
    [InlineData(12, AnalysisOutcome.Above)]
    public void ADeclividadeEAnalisadaContraOLimiteDaConfiguracao(double graus, AnalysisOutcome esperado)
    {
        Assert.Equal(esperado,
            Padrao().Evaluate(AnalysisKind.LongitudinalSlope, graus * Grau, Config()).Outcome);
    }

    /// <summary>
    /// O sentido da fileira é arbitrário: a mesa que desce 12° para o leste é
    /// a mesma que sobe 12° para o oeste. Vale o módulo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void DeclividadeNegativaValePeloModulo()
    {
        Assert.Equal(AnalysisOutcome.Above,
            Padrao().Evaluate(AnalysisKind.LongitudinalSlope, -12 * Grau, Config()).Outcome);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void SemLimiteDeDeclividadeNaConfiguracaoAAnaliseFicaDesligada()
    {
        var config = Config() with { MaxLongitudinalSlope = null };

        Assert.Equal(AnalysisOutcome.Off,
            Padrao().Evaluate(AnalysisKind.LongitudinalSlope, 45 * Grau, config).Outcome);
    }

    // ------------------------------------------------- mesa na borda

    [Fact]
    [Trait("Etapa", "4")]
    public void MesaParcialmenteForaDaAreaEPintadaComACorDaBorda()
    {
        var veredito = Padrao().EvaluateEdge(partlyOutside: true);

        Assert.Equal(AnalysisOutcome.Above, veredito.Outcome);
        Assert.Equal(Padrao().EdgeRule.Color, veredito.Color);
        Assert.Equal(Padrao().EdgeRule.Layer, veredito.Layer);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void MesaDentroDaAreaNaoEPintada()
    {
        var veredito = Padrao().EvaluateEdge(partlyOutside: false);

        Assert.Equal(AnalysisOutcome.Inside, veredito.Outcome);
        Assert.Null(veredito.Color);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void BordaDesligadaDevolveOffMesmoParaMesaDentro()
    {
        var regras = Padrao() with { EdgeRule = Padrao().EdgeRule with { Enabled = false } };

        Assert.Equal(AnalysisOutcome.Off, regras.EvaluateEdge(partlyOutside: false).Outcome);
    }

    // ------------------------------------------------- desligar

    /// <summary>
    /// Análise desligada não pinta e não finge que mediu: o veredito é Off,
    /// não Inside. Quem lê "dentro" numa análise desligada acha que conferiu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AnaliseDesligadaDevolveOffENaoDentro()
    {
        var regras = Padrao().With(AnalysisKind.LowEdge,
            Padrao().Rule(AnalysisKind.LowEdge) with { Enabled = false });

        var veredito = regras.Evaluate(AnalysisKind.LowEdge, 0.1, Config());

        Assert.Equal(AnalysisOutcome.Off, veredito.Outcome);
        Assert.Null(veredito.Color);
        Assert.Null(veredito.Layer);

        var borda = (regras with { EdgeRule = regras.EdgeRule with { Enabled = false } })
            .EvaluateEdge(partlyOutside: true);

        Assert.Equal(AnalysisOutcome.Off, borda.Outcome);
    }

    // ------------------------------------------------- o que não pode

    /// <summary>
    /// "Não sei medir" nunca vira "está bom". NaN e infinito são recusados,
    /// como no resto do Core.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ValorQueNaoEMedidaERecusado(double valor)
    {
        foreach (var kind in AnalysisRules.RangedKinds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Padrao().Evaluate(kind, valor, Config()));
        }
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void RegrasQuebradasNaoAvaliam()
    {
        var quebradas = Padrao() with { PaintPillarsLongerThan = -1 };

        Assert.Throws<InvalidOperationException>(
            () => quebradas.Evaluate(AnalysisKind.LowEdge, 0.5, Config()));
        Assert.Throws<InvalidOperationException>(() => quebradas.EvaluateEdge(true));
    }

    /// <summary>
    /// Os limites vêm da configuração; configuração quebrada é limite que
    /// ninguém conferiu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoQuebradaNaoAvalia()
    {
        var config = Config() with { MinLowEdge = 9 };

        Assert.Throws<InvalidOperationException>(
            () => Padrao().Evaluate(AnalysisKind.LowEdge, 0.5, config));
    }

    /// <summary>
    /// Regra ausente (campo faltando num arquivo, quando o 4.4 ler isto de
    /// disco) é motivo nomeado, não exceção de referência nula.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void RegraAusenteEMotivoNomeadoENaoExcecao()
    {
        AnalysisRules[] faltando =
        [
            Padrao() with { LowEdge = null! },
            Padrao() with { PillarLength = null! },
            Padrao() with { Embedment = null! },
            Padrao() with { LongitudinalSlope = null! },
            Padrao() with { EdgeRule = null! },
        ];

        foreach (var regras in faltando)
        {
            Assert.False(regras.IsValid);
            Assert.Contains("ausente", regras.WhyInvalid!);
            Assert.Equal(5, regras.Layers.Count());
            Assert.Contains("inválidas", regras.Describe());
            Assert.Throws<InvalidOperationException>(
                () => regras.Evaluate(AnalysisKind.LowEdge, 0.5, Config()));
        }

        Assert.Throws<InvalidOperationException>(
            () => (Padrao() with { Embedment = null! }).Rule(AnalysisKind.Embedment));
    }

    /// <summary>
    /// O motivo nomeia a análise certa, não só "camada": a tela do 4.4 tem
    /// cinco campos de camada.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(AnalysisKind.LowEdge, "ponta baixa")]
    [InlineData(AnalysisKind.PillarLength, "pilar")]
    [InlineData(AnalysisKind.Embedment, "enterro")]
    [InlineData(AnalysisKind.LongitudinalSlope, "declividade")]
    public void OMotivoDeCamadaInvalidaNomeiaAAnalise(AnalysisKind kind, string nome)
    {
        var regras = Padrao().With(kind, Padrao().Rule(kind) with { Layer = "a*b" });

        Assert.Contains(nome, regras.WhyInvalid!);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OMotivoDeCamadaInvalidaDaBordaNomeiaABorda()
    {
        var regras = Padrao() with { EdgeRule = Padrao().EdgeRule with { Layer = "a*b" } };

        Assert.Contains("borda", regras.WhyInvalid!);
    }

    /// <summary>A ordem é a de RangedKinds, com a borda por último.</summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AsCamadasSaemNaOrdemDasAnalisesComABordaPorUltimo()
    {
        var esperado = AnalysisRules.RangedKinds
            .Select(kind => Padrao().Rule(kind).Layer)
            .Append(Padrao().EdgeRule.Layer);

        Assert.Equal(esperado, Padrao().Layers);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData((AnalysisKind)99)]
    [InlineData((AnalysisKind)(-1))]
    public void AnaliseQueNaoExisteERecusada(AnalysisKind kind)
    {
        Assert.Throws<ArgumentException>(() => Padrao().Rule(kind));
        Assert.Throws<ArgumentException>(() => Padrao().With(kind, Padrao().LowEdge));
        Assert.Throws<ArgumentException>(() => Padrao().Evaluate(kind, 1.0, Config()));
    }

    /// <summary>
    /// A borda não é uma análise de faixa: pedir Evaluate dela com número é
    /// engano de quem chama, e não deve ser respondido com um chute.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ABordaNaoSeAvaliaComNumero()
    {
        Assert.Throws<ArgumentException>(
            () => Padrao().Evaluate(AnalysisKind.EdgeTable, 1.0, Config()));
        Assert.Throws<ArgumentException>(() => Padrao().Rule(AnalysisKind.EdgeTable));
        Assert.DoesNotContain(AnalysisKind.EdgeTable, AnalysisRules.RangedKinds);
    }

    // ------------------------------------------------- texto

    [Fact]
    [Trait("Etapa", "4")]
    public void TodaRegraInvalidaDizPorQue()
    {
        AnalysisRules[] quebradas =
        [
            Padrao() with { PaintPillarsLongerThan = 0 },
            Padrao().With(AnalysisKind.LowEdge, Padrao().Rule(AnalysisKind.LowEdge) with { Layer = "" }),
            Padrao() with { EdgeRule = Padrao().EdgeRule with { Layer = "a*b" } },
            Padrao() with { EdgeRule = Padrao().EdgeRule with { Layer = Padrao().Rule(AnalysisKind.Embedment).Layer } },
        ];

        foreach (var regras in quebradas)
        {
            Assert.False(regras.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(regras.WhyInvalid));
        }
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoDizQuantasAnalisesEstaoLigadas()
    {
        Assert.Contains("5 análises ligadas", Padrao().Describe());

        var uma = Padrao() with { EdgeRule = Padrao().EdgeRule with { Enabled = false } };

        Assert.Contains("4 análises ligadas", uma.Describe());
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoTrazOLimiteDePilarComVirgula()
    {
        var texto = (Padrao() with { PaintPillarsLongerThan = 2.5 }).Describe();

        Assert.Contains("2,5 m", texto);
        Assert.DoesNotContain("2.5", texto);
        Assert.Contains("sem limite de pilar", Padrao().Describe());
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoDeRegrasQuebradasDizOMotivo()
    {
        var texto = (Padrao() with { PaintPillarsLongerThan = -1 }).Describe();

        Assert.Contains("inválidas", texto);
        Assert.Contains("pilar", texto);
    }

    // ------------------------------------------------- cor

    [Fact]
    [Trait("Etapa", "4")]
    public void ACorSeDescreveEmHexadecimal()
    {
        Assert.Equal("#FF0000", RgbColor.Red.ToHex());
        Assert.Equal("#0000FF", RgbColor.Blue.ToHex());
        Assert.Equal("#0A0B0C", new RgbColor(10, 11, 12).ToHex());
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("ff0000", 255, 0, 0)]
    [InlineData(" #0a0B0c ", 10, 11, 12)]
    public void ACorSeLeDeHexadecimal(string texto, int r, int g, int b)
    {
        Assert.True(RgbColor.TryParseHex(texto, out var cor));
        Assert.Equal(new RgbColor((byte)r, (byte)g, (byte)b), cor);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GG0000")]
    [InlineData("#FF00001")]
    [InlineData("#FF 000")]
    [InlineData("#F F000")]
    [InlineData("vermelho")]
    public void TextoQueNaoECorERecusado(string texto)
    {
        Assert.False(RgbColor.TryParseHex(texto, out _));
    }
}
