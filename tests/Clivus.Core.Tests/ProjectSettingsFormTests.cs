namespace Clivus.Core.Tests;

/// <summary>
/// O formulário da tela de configuração: a conversão entre o que o
/// projetista digita (graus, centímetros, vírgula) e o que o motor usa
/// (radiano, metro).
///
/// É a única conversão de unidade entre tela e motor, e a revisão do 4.4
/// apontou que ela não tinha teste: trocar /100 por /10 passava em tudo.
/// </summary>
public class ProjectSettingsFormTests
{
    private const double Grau = Math.PI / 180;

    private static ProjectSettingsForm Padrao() => ProjectSettingsForm.From(ProjectSettings.Default);

    // ------------------------------------------------------- o que se vê

    /// <summary>
    /// Os números do Renan como ele os diz: ponta baixa de 30 a 80 cm,
    /// enterro de 90 cm, declividade de 10 graus, pitch de 6 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoApareceEmCentimetrosGrausEMetros()
    {
        var form = Padrao();

        Assert.Equal("30", form.MinLowEdgeCm);
        Assert.Equal("80", form.MaxLowEdgeCm);
        Assert.Equal("90", form.MinEmbedmentCm);
        Assert.Equal("200", form.MaxEmbedmentCm);
        Assert.Equal("0", form.MinStepCm);
        Assert.Equal("50", form.MaxStepCm);
        Assert.Equal("50", form.TableGapCm);
        Assert.Equal("500", form.BreakGapCm);
        Assert.Equal("6", form.Pitch);
        Assert.Equal("0", form.AzimuthDegrees);
        Assert.True(form.LimitSlope);
        Assert.Equal("10", form.MaxSlopeDegrees);
        Assert.False(form.PaintPillars);
        Assert.Equal("0", form.BumpModules);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OsNumerosSaemComVirgula()
    {
        var form = ProjectSettingsForm.From(ProjectSettings.SampleAllDifferent());

        Assert.Contains(",", form.Pitch);
        Assert.DoesNotContain(".", form.Pitch);
        Assert.DoesNotContain(".", form.AzimuthDegrees);
    }

    // ------------------------------------------------------- ida e volta

    [Fact]
    [Trait("Etapa", "4")]
    public void OPadraoVaiParaATelaEVoltaIgual()
    {
        var lido = Padrao().TryParse(out var motivo);

        Assert.NotNull(lido);
        Assert.Equal(string.Empty, motivo);
        Assert.Equal(ProjectSettings.Default, lido);
    }

    /// <summary>
    /// Todo campo diferente do padrão, pela tela, volta igual — inclusive a
    /// cor "abaixo" das análises que não têm mínimo, que a tela não mostra
    /// mas não pode trocar em silêncio.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void TudoDiferenteVaiParaATelaEVoltaIgual()
    {
        var original = ProjectSettings.SampleAllDifferent();

        var lido = ProjectSettingsForm.From(original).TryParse(out var motivo);

        Assert.NotNull(lido);
        Assert.Equal(string.Empty, motivo);
        Assert.Equal(original, lido);
    }

    /// <summary>
    /// Abrir a tela e salvar sem tocar em nada regrava o que estava, e não
    /// uma versão arredondada a quatro casas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ValorComMuitasCasasSobreviveAIdaEVolta()
    {
        var fino = ProjectSettings.Default with
        {
            Configuration = ProjectSettings.Default.Configuration with
            {
                FacingAzimuthRadians = 12.34567 * Grau,
                MinLowEdge = 0.31234,
                Pitch = 6.123456,
            },
        };

        var lido = ProjectSettingsForm.From(fino).TryParse(out _)!;

        Assert.Equal(12.34567 * Grau, lido.Configuration.FacingAzimuthRadians, 12);
        Assert.Equal(0.31234, lido.Configuration.MinLowEdge, 12);
        Assert.Equal(6.123456, lido.Configuration.Pitch, 12);

        // E a segunda volta é idêntica à primeira: nada se perde a cada salvar.
        var segunda = ProjectSettingsForm.From(lido).TryParse(out _)!;

        Assert.Equal(lido, segunda);
    }

    // --------------------------------------------------------- unidades

    /// <summary>Centímetro é centímetro: 35 na tela é 0,35 m no motor.</summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("35", 0.35)]
    [InlineData("35,5", 0.355)]
    [InlineData("35.5", 0.355)]
    public void CentimetrosViramMetros(string texto, double metros)
    {
        var lido = (Padrao() with { MinLowEdgeCm = texto }).TryParse(out var motivo);

        Assert.NotNull(lido);
        Assert.Equal(metros, lido.Configuration.MinLowEdge, 9);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("15", 15)]
    [InlineData("12,5", 12.5)]
    public void GrausViramRadianos(string texto, double graus)
    {
        var lido = (Padrao() with { AzimuthDegrees = texto, MaxSlopeDegrees = texto }).TryParse(out _);

        Assert.NotNull(lido);
        Assert.Equal(graus * Grau, lido.Configuration.FacingAzimuthRadians, 12);
        Assert.Equal(graus * Grau, lido.Configuration.MaxLongitudinalSlope!.Value, 12);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void PitchEPilarFicamEmMetro()
    {
        var lido = (Padrao() with { Pitch = "7,5", PaintPillars = true, PillarLongerThan = "2,75" })
            .TryParse(out _);

        Assert.NotNull(lido);
        Assert.Equal(7.5, lido.Configuration.Pitch, 9);
        Assert.Equal(2.75, lido.Analyses.PaintPillarsLongerThan!.Value, 9);
    }

    // ---------------------------------------------------------- opcionais

    /// <summary>
    /// Com a caixa desmarcada, o texto ao lado não é lido — nem para
    /// reclamar. "Sem limite" com "abc" no campo continua sendo sem limite.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CaixaDesmarcadaIgnoraOTextoAoLado()
    {
        var form = Padrao() with
        {
            LimitSlope = false,
            MaxSlopeDegrees = "abc",
            PaintPillars = false,
            PillarLongerThan = "",
        };

        var lido = form.TryParse(out var motivo);

        Assert.NotNull(lido);
        Assert.Null(lido.Configuration.MaxLongitudinalSlope);
        Assert.Null(lido.Analyses.PaintPillarsLongerThan);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void CaixaMarcadaExigeONumeroAoLado()
    {
        var semDeclividade = (Padrao() with { LimitSlope = true, MaxSlopeDegrees = "" }).TryParse(out var motivo1);

        Assert.Null(semDeclividade);
        Assert.Contains("Declividade máxima", motivo1);
        Assert.Contains("em branco", motivo1);

        var semPilar = (Padrao() with { PaintPillars = true, PillarLongerThan = "x" }).TryParse(out var motivo2);

        Assert.Null(semPilar);
        Assert.Contains("Pintar pilar", motivo2);
    }

    // ----------------------------------------------------------- erros

    /// <summary>Cada campo em branco é nomeado, um por um.</summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CampoEmBrancoENomeado()
    {
        static ProjectSettingsForm Apagar(ProjectSettingsForm f, string campo) => campo switch
        {
            nameof(ProjectSettingsForm.AzimuthDegrees) => f with { AzimuthDegrees = "   " },
            nameof(ProjectSettingsForm.Pitch) => f with { Pitch = "   " },
            nameof(ProjectSettingsForm.MinStepCm) => f with { MinStepCm = "   " },
            nameof(ProjectSettingsForm.MaxStepCm) => f with { MaxStepCm = "   " },
            nameof(ProjectSettingsForm.TableGapCm) => f with { TableGapCm = "   " },
            nameof(ProjectSettingsForm.BreakGapCm) => f with { BreakGapCm = "   " },
            nameof(ProjectSettingsForm.MinLowEdgeCm) => f with { MinLowEdgeCm = "   " },
            nameof(ProjectSettingsForm.MaxLowEdgeCm) => f with { MaxLowEdgeCm = "   " },
            nameof(ProjectSettingsForm.BumpModules) => f with { BumpModules = "   " },
            nameof(ProjectSettingsForm.MinEmbedmentCm) => f with { MinEmbedmentCm = "   " },
            nameof(ProjectSettingsForm.MaxEmbedmentCm) => f with { MaxEmbedmentCm = "   " },
            nameof(ProjectSettingsForm.MaxSlopeDegrees) => f with { MaxSlopeDegrees = "   " },
            nameof(ProjectSettingsForm.PillarLongerThan) => f with { PillarLongerThan = "   " },
            _ => throw new ArgumentException(campo),
        };

        foreach (var (campo, nome) in ProjectSettingsForm.Labels)
        {
            var form = Padrao() with { LimitSlope = true, PaintPillars = true };
            var vazio = Apagar(form, campo);

            var lido = vazio.TryParse(out var motivo);

            Assert.Null(lido);
            Assert.Contains(nome, motivo);
            Assert.Contains("em branco", motivo);
        }
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void LomboPrecisaSerInteiro()
    {
        var lido = (Padrao() with { BumpModules = "2,5" }).TryParse(out var motivo);

        Assert.Null(lido);
        Assert.Contains("inteiro", motivo);
    }

    /// <summary>
    /// Número que lê mas não fecha volta com o motivo da configuração, que
    /// nomeia a faixa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void FaixaInvertidaVoltaComOMotivoDaConfiguracao()
    {
        var lido = (Padrao() with { MinLowEdgeCm = "90", MaxLowEdgeCm = "30" }).TryParse(out var motivo);

        Assert.Null(lido);
        Assert.Contains("ponta baixa", motivo);
        Assert.Contains("invertida", motivo);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void RegraAusenteNoFormularioENomeada()
    {
        var semBorda = (Padrao() with { Edge = null! }).TryParse(out var motivo);

        Assert.Null(semBorda);
        Assert.Contains("borda", motivo);

        var semRegras = (Padrao() with { Rules = new Dictionary<AnalysisKind, AnalysisRule>() })
            .TryParse(out var motivo2);

        Assert.Null(semRegras);
        Assert.Contains("ausente", motivo2);
    }
}
