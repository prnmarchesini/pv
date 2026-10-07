namespace Clivus.Core.Tests;

/// <summary>A tag nunca em pé (Renan, 07/10/2026: "jamais quero texto virado, se for o caso, diminua o tamanho da fonte").</summary>
public class StringTagLayoutTests
{
    [Fact]
    [Trait("Etapa", "15")]
    public void AStringEmUNaMesaDeitadaDaTagDeitada()
    {
        // Vai pela fileira de cima e volta pela de baixo: o primeiro e o
        // último módulo ficam um em cima do outro.
        var centros = new List<(double X, double Y)>();
        for (var c = 0; c < 12; c++) centros.Add((c * 1.1, 1.0));
        for (var c = 11; c >= 0; c--) centros.Add((c * 1.1, -1.0));

        var (angulo, comprimento) = StringTagLayout.Axis(centros);

        Assert.Equal(0, angulo, 6);
        Assert.Equal(12 * 1.1, comprimento, 6);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void AMesaInclinadaDaOAnguloDelaLegivel()
    {
        var a = 30 * Math.PI / 180;
        var centros = Enumerable.Range(0, 10).Select(i => (Math.Cos(a) * i * 2.0, Math.Sin(a) * i * 2.0)).ToList();
        Assert.Equal(a, StringTagLayout.Axis(centros).Angle, 6);

        // Andando para o outro lado, o mesmo ângulo (nunca de cabeça para baixo).
        centros.Reverse();
        Assert.Equal(a, StringTagLayout.Axis(centros).Angle, 6);

        var b = 150 * Math.PI / 180;
        var outra = Enumerable.Range(0, 10).Select(i => (Math.Cos(b) * i * 2.0, Math.Sin(b) * i * 2.0)).ToList();
        Assert.Equal(b - Math.PI, StringTagLayout.Axis(outra).Angle, 6);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void UmModuloSoNaoGira()
    {
        Assert.Equal((0.0, 0.0), StringTagLayout.Axis([(5, 5)]));
        Assert.Equal((0.0, 0.0), StringTagLayout.Axis([(5, 5), (5, 5)]));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void AFonteDiminuiSoQuandoNaoCabe()
    {
        Assert.Equal(0.5, StringTagLayout.FitHeight(0.5, 3, 13.2));
        Assert.Equal(0.25, StringTagLayout.FitHeight(0.5, 8, 4), 9);
        Assert.Equal(0.5, StringTagLayout.FitHeight(0.5, 8, 0));
    }
}
