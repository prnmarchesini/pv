namespace UFV.Core.Tests;

/// <summary>
/// As configurações do projeto: a configuração do sistema (4.1) e as regras de
/// análise (4.3) juntas, no formato em que vão para o desenho e voltam.
///
/// O que este arquivo trava é a ida e volta: tudo que a tela do 4.4 mostra
/// tem que sobreviver a salvar o desenho, fechar e reabrir. A validação do
/// Renan para o passo é exatamente essa ("configura um projeto real e confere
/// que salvar e reabrir preserva tudo"), e o teste de nível 1 é a metade que
/// não precisa do CAD.
/// </summary>
public class ProjectSettingsTests
{
    private const double Grau = Math.PI / 180;

    private static ProjectSettings Padrao() => ProjectSettings.Default;

    /// <summary>
    /// A amostra do Core em que todo campo difere do padrão. Se a ida e volta
    /// perder um campo e ele voltar com o padrão, este é o objeto que denuncia.
    /// </summary>
    private static ProjectSettings TudoDiferente() => ProjectSettings.SampleAllDifferent();

    /// <summary>
    /// A amostra é mesmo toda diferente: campo a campo, contra o padrão. Sem
    /// isto, o teste de ida e volta e o de nível 2 provariam preservação só
    /// dos campos que por acaso mudaram.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AAmostraDifereDoPadraoEmTodoCampo()
    {
        var padrao = Padrao().ToFields().ToDictionary(c => c.Key, c => c.Value);
        var amostra = TudoDiferente().ToFields();

        Assert.True(TudoDiferente().IsValid, TudoDiferente().WhyInvalid);
        Assert.Equal(padrao.Count, amostra.Count);

        foreach (var (chave, valor) in amostra)
        {
            if (chave == "FORMATO") continue;

            Assert.True(padrao[chave] != valor, $"o campo {chave} da amostra é igual ao padrão ({valor})");
        }
    }

    /// <summary>
    /// O número de campos é fixo e conhecido: 1 de versão, 13 da configuração,
    /// 4 por análise de faixa e 3 da borda. O teste de nível 2 exige o mesmo
    /// número; um campo a menos aqui tem que doer aqui também.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void SaoTrintaETresCampos()
    {
        Assert.Equal(33, Padrao().ToFields().Count);
    }

    // ------------------------------------------------------------- padrão

    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoJuntaOsDoisPadroes()
    {
        var padrao = Padrao();

        Assert.True(padrao.IsValid, padrao.WhyInvalid);
        Assert.Equal(SystemConfiguration.Default, padrao.Configuration);
        Assert.Equal(AnalysisRules.Default, padrao.Analyses);
    }

    // -------------------------------------------------------- ida e volta

    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoVaiEVoltaIgual()
    {
        var lido = ProjectSettings.Parse(Padrao().ToFields());

        Assert.Null(lido.Problem);
        Assert.Equal(Padrao(), lido.Settings);
    }

    /// <summary>
    /// O teste que importa: todo campo diferente do padrão, e todos voltam.
    /// Um campo esquecido na gravação voltaria com o padrão, e a igualdade
    /// do record acusa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void TudoDiferenteDoPadraoVaiEVoltaIgual()
    {
        var original = TudoDiferente();

        Assert.True(original.IsValid, original.WhyInvalid);

        var lido = ProjectSettings.Parse(original.ToFields());

        Assert.Null(lido.Problem);
        Assert.Equal(original, lido.Settings);
    }

    /// <summary>
    /// Os dois opcionais em branco também voltam em branco, e não como zero:
    /// "sem limite" e "limite zero" são coisas diferentes, e zero nem é aceito.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OsLimitesOpcionaisVoltamEmBranco()
    {
        var semLimites = Padrao() with
        {
            Configuration = Padrao().Configuration with { MaxLongitudinalSlope = null },
            Analyses = Padrao().Analyses with { PaintPillarsLongerThan = null },
        };

        var lido = ProjectSettings.Parse(semLimites.ToFields());

        Assert.Null(lido.Problem);
        Assert.Null(lido.Settings!.Configuration.MaxLongitudinalSlope);
        Assert.Null(lido.Settings.Analyses.PaintPillarsLongerThan);
    }

    /// <summary>
    /// Números vão com ponto e em formato redondo, independentemente da
    /// cultura da máquina: o desenho pode ser aberto noutra máquina, e
    /// "0,3" lido com cultura invariante vira 3.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OsNumerosVaoComPontoEVoltamExatos()
    {
        var campos = Padrao().ToFields();

        foreach (var (chave, valor) in campos)
        {
            Assert.DoesNotContain(",", valor);
        }

        var pitch = campos.Single(c => c.Key == "PITCH").Value;

        Assert.Equal("6", pitch);

        var original = TudoDiferente();
        var lido = ProjectSettings.Parse(original.ToFields()).Settings!;

        Assert.Equal(original.Configuration.FacingAzimuthRadians, lido.Configuration.FacingAzimuthRadians);
        Assert.Equal(original.Configuration.MaxLongitudinalSlope, lido.Configuration.MaxLongitudinalSlope);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void AsCoresVaoEmHexadecimal()
    {
        var campos = TudoDiferente().ToFields();

        Assert.Contains(campos, c => c.Key == "PONTA_BAIXA_ABAIXO_COR" && c.Value == "#FFC800");
        Assert.Contains(campos, c => c.Key == "BORDA_COR" && c.Value == "#000000");
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OPrimeiroCampoEAVersaoDoFormato()
    {
        var campos = Padrao().ToFields();

        Assert.Equal("FORMATO", campos[0].Key);
        Assert.Equal("1", campos[0].Value);
    }

    /// <summary>
    /// Configuração que não fecha não vai para o desenho: gravar um estado
    /// inválido faria a próxima abertura recusar o que o próprio plugin
    /// escreveu.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ConfiguracaoInvalidaNaoEGravada()
    {
        var quebrada = Padrao() with
        {
            Configuration = Padrao().Configuration with { MinLowEdge = 9 },
        };

        Assert.False(quebrada.IsValid);
        Assert.Contains("ponta baixa", quebrada.WhyInvalid!);
        Assert.Throws<InvalidOperationException>(() => quebrada.ToFields());
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void RegrasInvalidasTambemNaoSaoGravadas()
    {
        var quebrada = Padrao() with
        {
            Analyses = Padrao().Analyses with { PaintPillarsLongerThan = -1 },
        };

        Assert.False(quebrada.IsValid);
        Assert.Contains("pilar", quebrada.WhyInvalid!);
        Assert.Throws<InvalidOperationException>(() => quebrada.ToFields());
    }

    // ------------------------------------------------------------ leitura

    [Fact]
    [Trait("Etapa", "4")]
    public void NadaGravadoEAusencia()
    {
        var lido = ProjectSettings.Parse(null);

        Assert.Null(lido.Settings);
        Assert.Null(lido.Problem);

        var vazio = ProjectSettings.Parse([]);

        Assert.Null(vazio.Settings);
        Assert.Null(vazio.Problem);
    }

    /// <summary>
    /// Versão diferente é recusada com o motivo, não lida pela metade: um
    /// formato futuro que mude a unidade de um campo faria a leitura antiga
    /// entender errado o que está escrito.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OutraVersaoDoFormatoERecusadaComOMotivo()
    {
        var campos = Padrao().ToFields()
            .Select(c => c.Key == "FORMATO" ? new KeyValuePair<string, string>(c.Key, "2") : c)
            .ToList();

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Settings);
        Assert.Contains("versão", lido.Problem!);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void SemVersaoNaoSeLe()
    {
        var campos = Padrao().ToFields().Where(c => c.Key != "FORMATO").ToList();

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Settings);
        Assert.NotNull(lido.Problem);
    }

    /// <summary>
    /// Campo faltando é motivo que nomeia o campo. Preencher com o padrão em
    /// silêncio faria o projetista perder um número que digitou e não saber.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CampoFaltandoENomeado()
    {
        foreach (var chave in Padrao().ToFields().Select(c => c.Key).Where(k => k != "FORMATO"))
        {
            var campos = Padrao().ToFields().Where(c => c.Key != chave).ToList();

            var lido = ProjectSettings.Parse(campos);

            Assert.Null(lido.Settings);
            Assert.Contains(chave, lido.Problem!);
        }
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("PITCH", "seis")]
    [InlineData("PITCH", "6,0")]
    [InlineData("LOMBO_MODULOS", "2.5")]
    [InlineData("PONTA_BAIXA_ABAIXO_COR", "vermelho")]
    [InlineData("PONTA_BAIXA_LIGADA", "talvez")]
    public void CampoIlegivelENomeado(string chave, string valor)
    {
        var campos = Padrao().ToFields()
            .Select(c => c.Key == chave ? new KeyValuePair<string, string>(c.Key, valor) : c)
            .ToList();

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Settings);
        Assert.Contains(chave, lido.Problem!);
    }

    /// <summary>
    /// Números gravados que não fecham entre si voltam como problema, com o
    /// motivo da configuração — alguém editou o registro, ou um plugin de
    /// outra versão gravou. Nunca como configuração "meio válida".
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void NumerosQueNaoFechamVoltamComoProblema()
    {
        var campos = Padrao().ToFields()
            .Select(c => c.Key == "PONTA_BAIXA_MIN" ? new KeyValuePair<string, string>(c.Key, "5") : c)
            .ToList();

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Settings);
        Assert.Contains("ponta baixa", lido.Problem!);
    }

    /// <summary>
    /// Campo desconhecido é ignorado: uma versão futura que acrescente campo
    /// e mantenha a versão do formato não pode tornar o registro ilegível
    /// para esta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CampoDesconhecidoEIgnorado()
    {
        var campos = Padrao().ToFields().ToList();
        campos.Add(new KeyValuePair<string, string>("CAMPO_DO_FUTURO", "42"));

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Problem);
        Assert.Equal(Padrao(), lido.Settings);
    }

    /// <summary>A chave não distingue caixa, como o resto dos registros do plugin.</summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AChaveNaoDistingueCaixa()
    {
        var campos = Padrao().ToFields()
            .Select(c => new KeyValuePair<string, string>(c.Key.ToLowerInvariant(), c.Value))
            .ToList();

        var lido = ProjectSettings.Parse(campos);

        Assert.Null(lido.Problem);
        Assert.Equal(Padrao(), lido.Settings);
    }

    // -------------------------------------------------------------- texto

    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoJuntaAsDuasPartes()
    {
        var texto = Padrao().Describe();

        Assert.Contains("ponta baixa", texto);
        Assert.Contains("análises ligadas", texto);
    }
}
