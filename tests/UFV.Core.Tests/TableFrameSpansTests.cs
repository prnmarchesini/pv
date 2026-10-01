namespace UFV.Core.Tests;

/// <summary>
/// Passo 8.1: os vãos escritos par a par (P1-P2, P2-P3...) e o enterro
/// mínimo do pilar (T3) na estrutura.
///
/// Origem: Melhorias.docx, 01/10/2026. O Renan quer digitar cada vão, ver a
/// soma e saber se ela bate com o que os pilares precisam cobrir, que o
/// sistema já sabe pela largura dos módulos, pelo espaçamento e pelas sobras.
/// </summary>
public class TableFrameSpansTests
{
    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    /// <summary>
    /// 14 colunas × 1,303 + 13 × 0,02 + 0,10 + 0,10 = 18,702 m.
    /// </summary>
    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableFrame Padrao(double balanco = 0) => new(3.00, 2.50, 0.15, 0.07, 3.00, balanco);

    [Fact]
    [Trait("Etapa", "8")]
    public void SemVaosEscritosOsPilaresSaemDoVaoAlvoComoAntes()
    {
        var estrutura = Padrao(0.5);

        Assert.Null(estrutura.PillarSpans);
        Assert.Equal(PillarTable.Distribute(Mesa().Length, 3.00, 0.5), estrutura.Pillars(Mesa()));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ComVaosEscritosATabelaEAListaDoUsuario()
    {
        var estrutura = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702] };

        var tabela = estrutura.Pillars(Mesa());

        Assert.Equal([3, 3, 3, 3, 3, 3.702], tabela.Spans);
        Assert.Equal(7, tabela.PillarCount);
        Assert.Null(estrutura.WhyDoesNotFit(Mesa()));
    }

    /// <summary>
    /// O que os pilares cobrem é a mesa inteira (módulos, espaçamentos e as
    /// duas sobras) menos o balanço das duas pontas.
    /// </summary>
    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(0, 18.702)]
    [InlineData(0.5, 17.702)]
    public void OQueOsPilaresPrecisamCobrir(double balanco, double esperado)
    {
        Assert.Equal(esperado, Padrao(balanco).PillarCoverage(Mesa()), 9);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADiferencaDizQuantoSobraOuFalta()
    {
        var curta = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3] };
        var longa = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 4] };

        Assert.Equal(-0.702, curta.SpanDifference(Mesa())!.Value, 9);
        Assert.Equal(0.298, longa.SpanDifference(Mesa())!.Value, 9);
        Assert.Null(Padrao().SpanDifference(Mesa()));

        Assert.Contains("falta 0,702 m", curta.WhyDoesNotFit(Mesa()));
        Assert.Contains("sobra 0,298 m", longa.WhyDoesNotFit(Mesa()));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void MenosDeUmMilimetroEFechado()
    {
        var quase = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.7024] };

        Assert.Equal(0, quase.SpanDifference(Mesa()));
        Assert.Null(quase.WhyDoesNotFit(Mesa()));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OBalancoEntraNaConta()
    {
        var estrutura = Padrao(0.5) with { PillarSpans = [3, 3, 3, 3, 3, 2.702] };

        Assert.Null(estrutura.WhyDoesNotFit(Mesa()));
        Assert.Equal(0.5, estrutura.Pillars(Mesa()).Cantilever);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(new double[] { }, "pelo menos um vão")]
    [InlineData(new double[] { 3, 0, 3 }, "vão 2")]
    [InlineData(new double[] { 3, 3, -1 }, "vão 3")]
    [InlineData(new double[] { 60 }, "vão 1")]
    [InlineData(new double[] { 3, 25 }, "vão 2")]
    [InlineData(new double[] { 3, double.NaN }, "vão 2")]
    public void VaoImpossivelERecusadoPeloNumero(double[] vaos, string trecho)
    {
        var estrutura = Padrao() with { PillarSpans = vaos };

        Assert.False(estrutura.IsValid);
        Assert.Contains(trecho, estrutura.WhyInvalid);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AListaECopiadaNaEntrada()
    {
        var vaos = new double[] { 3, 3, 3, 3, 3, 3.702 };
        var estrutura = Padrao() with { PillarSpans = vaos };

        vaos[0] = -1;

        Assert.True(estrutura.IsValid);
        Assert.Equal(3, estrutura.PillarSpans![0]);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void DuasEstruturasComOsMesmosVaosSaoIguais()
    {
        var a = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702], MinEmbedment = 1.1 };
        var b = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702], MinEmbedment = 1.1 };
        var c = Padrao() with { PillarSpans = [3, 3, 3, 3, 3.702, 3], MinEmbedment = 1.1 };
        var d = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702], MinEmbedment = 1.2 };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.NotEqual(a, Padrao());
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(1.1, true)]
    [InlineData(0.01, true)]
    [InlineData(0, false)]
    [InlineData(-0.5, false)]
    [InlineData(double.NaN, false)]
    [InlineData(25, false)]
    public void OEnterroMinimoPrecisaSerUmaMedida(double t3, bool serve)
    {
        var estrutura = Padrao() with { MinEmbedment = t3 };

        Assert.Equal(serve, estrutura.IsValid);
        if (!serve) Assert.Contains("enterro mínimo", estrutura.WhyInvalid);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ADescricaoFalaDosVaosEDoT3()
    {
        var estrutura = Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702], MinEmbedment = 1.1 };

        var texto = estrutura.Describe();

        Assert.Contains("vãos escritos", texto);
        Assert.Contains("enterro mínimo (T3) de 1,1 m", texto);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OPerfilGuardaOsVaosEOT3()
    {
        var perfil = new TableProfile(
            "Risen 2V28",
            Mesa(),
            Padrao() with { PillarSpans = [3, 3, 3, 3, 3, 3.702], MinEmbedment = 1.1 },
            15 * Math.PI / 180);

        var volta = TableProfile.Parse(perfil.ToJson());

        Assert.Equal(perfil.Frame, volta.Frame);
        Assert.Equal([3, 3, 3, 3, 3, 3.702], volta.Frame.PillarSpans);
        Assert.Equal(1.1, volta.Frame.MinEmbedment);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void PerfilAntigoSemOsCamposNovosAindaAbre()
    {
        var antigo = new TableProfile("Antigo", Mesa(), Padrao(), 15 * Math.PI / 180).ToJson();

        Assert.DoesNotContain("pillarSpans", antigo);
        Assert.DoesNotContain("minEmbedment", antigo);

        var volta = TableProfile.Parse(antigo);

        Assert.Null(volta.Frame.PillarSpans);
        Assert.Null(volta.Frame.MinEmbedment);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void PerfilComVaoQueNaoFechaERecusado()
    {
        var perfil = new TableProfile(
            "Errado",
            Mesa(),
            Padrao() with { PillarSpans = [3, 3, 3] },
            15 * Math.PI / 180);

        Assert.False(perfil.IsValid);
        Assert.Contains("falta", perfil.WhyInvalid);
    }
}

/// <summary>Passo 8.2: o T3 da estrutura chega ao motor pelas configurações.</summary>
public class SettingsForTableTests
{
    private static TableFrame Padrao() => new(3.00, 2.50, 0.15, 0.07, 3.00, 0);

    [Fact]
    [Trait("Etapa", "8")]
    public void SemT3ValeODaConfiguracao()
    {
        Assert.Same(ProjectSettings.Default, ProjectSettings.Default.ForTable(Padrao()));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ComT3EleMandaNoEnterroMinimo()
    {
        var ajustada = ProjectSettings.Default.ForTable(Padrao() with { MinEmbedment = 1.3 });

        Assert.Equal(1.3, ajustada.Configuration.MinEmbedment);
        Assert.Equal(ProjectSettings.Default.Configuration.MaxEmbedment, ajustada.Configuration.MaxEmbedment);
        Assert.True(ajustada.Configuration.IsValid);
        Assert.Equal(ProjectSettings.Default.Analyses, ajustada.Analyses);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void T3AcimaDoMaximoLevaOMaximoJunto()
    {
        var ajustada = ProjectSettings.Default.ForTable(Padrao() with { MinEmbedment = 2.5 });

        Assert.Equal(2.5, ajustada.Configuration.MinEmbedment);
        Assert.Equal(2.5, ajustada.Configuration.MaxEmbedment);
        Assert.True(ajustada.Configuration.IsValid);
    }
}
