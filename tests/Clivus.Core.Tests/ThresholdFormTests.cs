namespace Clivus.Core.Tests;

/// <summary>
/// 42.6 (revisão): as caixas da seção Cores montadas numa regra, como a
/// janela de análises faz. Uma coluna fora do biposte; uma por linha de
/// pilar nas análises de pilar de um desenho com biposte.
/// </summary>
public class ThresholdFormTests
{
    private static readonly RgbColor Vermelho = new(255, 0, 0);
    private static readonly RgbColor Azul = new(0, 0, 255);

    private static readonly PillarLine[] Biposte = [PillarLine.Front, PillarLine.Rear];

    private static ThresholdRule? Montar(
        IReadOnlyList<PillarLine> colunas, bool abaixo, bool acima, ThresholdRule gravada, out string? erro,
        params (PillarLine Linha, string Abaixo, string Acima)[] textos) =>
        ThresholdForm.Build(colunas, textos.ToDictionary(t => t.Linha, t => (t.Abaixo, t.Acima)), abaixo, acima, Vermelho, Azul, pecas: false, gravada, out erro);

    [Fact]
    [Trait("Etapa", "42")]
    public void AsColunasSoNasAnalisesDePilarComBiposte()
    {
        var mono = new HashSet<PillarLine> { PillarLine.Single };
        var bi = new HashSet<PillarLine> { PillarLine.Front, PillarLine.Rear };
        var misturado = new HashSet<PillarLine> { PillarLine.Single, PillarLine.Front, PillarLine.Rear };

        Assert.Equal([PillarLine.Single], ThresholdForm.Columns(IndependentKind.PillarAbove, mono));
        Assert.Equal([PillarLine.Front, PillarLine.Rear], ThresholdForm.Columns(IndependentKind.PillarBuried, bi));
        Assert.Equal([PillarLine.Single, PillarLine.Front, PillarLine.Rear], ThresholdForm.Columns(IndependentKind.PillarLength, misturado));
        Assert.Equal([PillarLine.Single], ThresholdForm.Columns(IndependentKind.LowEdge, bi));
        Assert.Equal([PillarLine.Single], ThresholdForm.Columns(IndependentKind.PillarAbove, new HashSet<PillarLine>()));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void UmaColunaMarcadaEVaziaEErroComoSempreFoi()
    {
        var gravada = IndependentAnalysis.Default(IndependentKind.LowEdge);

        Assert.Null(Montar([PillarLine.Single], abaixo: true, acima: false, gravada, out var erro, (PillarLine.Single, "", "")));
        Assert.Equal("Não consigo ler o limite de baixo.", erro);

        var regra = Montar([PillarLine.Single], abaixo: true, acima: true, gravada, out erro, (PillarLine.Single, "0,3", "1,2"));
        Assert.Null(erro);
        Assert.Equal(new ThresholdRule(0.3, Vermelho, 1.2, Azul, false), regra);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ComColunasAVaziaEALinhaSemLimite()
    {
        var gravada = IndependentAnalysis.Default(IndependentKind.PillarAbove);

        var regra = Montar(Biposte, abaixo: false, acima: true, gravada, out var erro, (PillarLine.Front, "", ""), (PillarLine.Rear, "", "2,5"));

        Assert.Null(erro);
        Assert.NotNull(regra);
        Assert.Equal(new LineLimits(null, null), regra.Front);
        Assert.Equal(new LineLimits(null, 2.5), regra.Rear);
        Assert.Equal(Band.Inside, regra.ForLine(PillarLine.Front).Classify(99));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ComColunasPrecisaDeUmLimiteEOErroDizALinha()
    {
        var gravada = IndependentAnalysis.Default(IndependentKind.PillarAbove);

        Assert.Null(Montar(Biposte, abaixo: true, acima: true, gravada, out var erro, (PillarLine.Front, "", ""), (PillarLine.Rear, "", "")));
        Assert.Equal("Preencha pelo menos um limite (de uma das colunas).", erro);

        Assert.Null(Montar(Biposte, abaixo: false, acima: true, gravada, out erro, (PillarLine.Front, "", "x"), (PillarLine.Rear, "", "2")));
        Assert.Equal("Pilar da frente: não consigo ler o limite de cima.", erro);

        Assert.Null(Montar(Biposte, abaixo: true, acima: true, gravada, out erro, (PillarLine.Front, "1", "2"), (PillarLine.Rear, "3", "2")));
        Assert.StartsWith("Pilar de trás:", erro, StringComparison.Ordinal);
    }

    /// <summary>A monoposte sem coluna (desenho só de biposte) fica com o limite gravado, e o lado desmarcado sai dela também.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void ALinhaSemColunaGuardaOGravadoMenosOLadoDesmarcado()
    {
        var gravada = new ThresholdRule(0.5, Vermelho, 3.0, Azul, false);

        var regra = Montar(Biposte, abaixo: false, acima: true, gravada, out _, (PillarLine.Front, "", "1,5"), (PillarLine.Rear, "", "2,5"));

        Assert.NotNull(regra);
        Assert.Null(regra.Below);
        Assert.Equal(3.0, regra.Above);
    }

    /// <summary>Uma coluna só num desenho que tem as linhas gravadas: desmarcar um lado desliga esse lado nelas também.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void UmaColunaComLinhasGravadasDesligaOLadoNelas()
    {
        var gravada = new ThresholdRule(null, Vermelho, 3.0, Azul, false) { Front = new LineLimits(1.0, 1.5), Rear = new LineLimits(null, 2.5) };

        var regra = Montar([PillarLine.Single], abaixo: false, acima: true, gravada, out _, (PillarLine.Single, "", "3"));

        Assert.NotNull(regra);
        Assert.Equal(new LineLimits(null, 1.5), regra.Front);
        Assert.Equal(new LineLimits(null, 2.5), regra.Rear);
    }
}
