namespace Clivus.Core.Tests;

/// <summary>
/// Passo 8.2: a conta por trás da janela de vãos (P1-P2, P2-P3...), que a
/// janela só mostra.
/// </summary>
public class PillarSpanFormTests
{
    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(0, "P1-P2")]
    [InlineData(1, "P2-P3")]
    [InlineData(9, "P10-P11")]
    public void ORotuloDeCadaVao(int indice, string esperado)
    {
        Assert.Equal(esperado, PillarSpanForm.Label(indice));
    }

    /// <summary>
    /// Distribuir igual arredonda ao milímetro, que é o que se digita, e o
    /// último vão leva o resto para a soma fechar exata.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void DistribuirIgualFechaAoMilimetro()
    {
        var vaos = PillarSpanForm.Equal(18.702, 7);

        Assert.Equal(6, vaos.Count);
        Assert.Equal([3.117, 3.117, 3.117, 3.117, 3.117, 3.117], vaos);

        var torto = PillarSpanForm.Equal(10, 4);

        Assert.Equal([3.333, 3.333, 3.334], torto);
        Assert.Equal(10, torto.Sum(), 9);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1002)]
    public void QuantidadeDePilaresImpossivelERecusada(int pilares)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSpanForm.Equal(18.702, pilares));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CobrirNadaERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSpanForm.Equal(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSpanForm.Equal(double.NaN, 3));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void LeOsCamposComVirgula()
    {
        Assert.True(PillarSpanForm.TryRead(["3", "3,5", " 2.25 "], out var vaos, out var motivo));
        Assert.Equal([3, 3.5, 2.25], vaos);
        Assert.Equal(string.Empty, motivo);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(new[] { "3", "", "3" }, "P2-P3 está em branco")]
    [InlineData(new[] { "3", "3", "abc" }, "P3-P4")]
    [InlineData(new[] { "0", "3" }, "P1-P2")]
    [InlineData(new[] { "3", "-2" }, "P2-P3")]
    [InlineData(new string[] { }, "pelo menos um vão")]
    public void CampoRuimENomeadoPeloPar(string[] textos, string trecho)
    {
        Assert.False(PillarSpanForm.TryRead(textos, out _, out var motivo));
        Assert.Contains(trecho, motivo);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(18.702, 18.702, "fecha")]
    [InlineData(18.7024, 18.702, "fecha")]
    [InlineData(18.0, 18.702, "falta 0,702 m")]
    [InlineData(19.0, 18.702, "sobra 0,298 m")]
    public void AFraseDaSoma(double soma, double cobrir, string trecho)
    {
        var frase = PillarSpanForm.Summary(soma, cobrir);

        Assert.Contains(trecho, frase);
        Assert.Contains(cobrir.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), frase);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void FechaUsaAMesmaToleranciaDaEstrutura()
    {
        Assert.True(PillarSpanForm.Closes(18.7024, 18.702));
        Assert.False(PillarSpanForm.Closes(18.704, 18.702));
    }
}

/// <summary>Achados da revisão do 8.2.</summary>
public class PillarSpanFormRevisionTests
{
    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(18.702, 400)]
    [InlineData(18.702, 1001)]
    [InlineData(4.5, 7)]
    public void DistribuirIgualNuncaDaVaoNegativoEFecha(double cobrir, int pilares)
    {
        var vaos = PillarSpanForm.Equal(cobrir, pilares);

        Assert.All(vaos, v => Assert.True(v > 0));
        Assert.True(vaos.Max() - vaos.Min() <= 0.001 + 1e-9);
        Assert.True(PillarSpanForm.Closes(vaos.Sum(), cobrir));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void VaoMenorQueUmMilimetroERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarSpanForm.Equal(0.5, 1001));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(15)]
    [InlineData(0.0005)]
    public void T3EmCentimetroOuMicroscopicoERecusado(double t3)
    {
        var estrutura = new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0) { MinEmbedment = t3 };

        Assert.False(estrutura.IsValid);
        Assert.Contains("em metro", estrutura.WhyInvalid);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AvisaQuandoOT3PassaDoEnterroMaximo()
    {
        var estrutura = new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0);

        Assert.Null(ProjectSettings.Default.EmbedmentNote(estrutura));
        Assert.Null(ProjectSettings.Default.EmbedmentNote(estrutura with { MinEmbedment = 1.2 }));
        Assert.Contains("o máximo subiu", ProjectSettings.Default.EmbedmentNote(estrutura with { MinEmbedment = 2.5 }));
    }
}
