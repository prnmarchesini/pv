namespace UFV.Core.Tests;

/// <summary>
/// A estrutura que segura os módulos, com os nomes do desenho do Renan:
/// T1 é a tesoura, T2 é onde o pilar encosta nela.
///
/// Objeto pequeno, e por isso mesmo fácil de deixar sem teste — foi o que
/// aconteceu até a revisão do 3.4 reparar. O ramo que recusa pilar fora da
/// tesoura nunca tinha sido exercitado.
/// </summary>
public class TableFrameTests
{
    /// <summary>A estrutura do Renan: tesoura de 3 m, pilar a 2,5 m, seção 0,15 × 0,07.</summary>
    private static TableFrame Padrao() => new(3.00, 2.50, 0.15, 0.07);

    [Fact]
    [Trait("Etapa", "3")]
    public void AEstruturaDoRenanFecha()
    {
        var estrutura = Padrao();

        Assert.True(estrutura.IsValid);
        Assert.Null(estrutura.WhyInvalid);
    }

    /// <summary>
    /// O pilar pode encostar na ponta baixa da tesoura — é o T2 = 0 do exemplo
    /// do plano — e na ponta alta. Fora dela, não: seria peça pendurada no ar,
    /// e passaria em qualquer conferência de sinal.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, true)]
    [InlineData(1.5, true)]
    [InlineData(3.0, true)]
    [InlineData(3.001, false)]
    [InlineData(9, false)]
    [InlineData(-0.1, false)]
    public void OPilarPrecisaEstarSobreATesoura(double t2, bool serve)
    {
        var estrutura = new TableFrame(3.00, t2, 0.15, 0.07);

        Assert.Equal(serve, estrutura.IsValid);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void OMotivoDizOsDoisNumerosQuandoOPilarSaiDaTesoura()
    {
        var motivo = new TableFrame(3.00, 4.20, 0.15, 0.07).WhyInvalid;

        Assert.NotNull(motivo);
        Assert.Contains("4,2", motivo!);
        Assert.Contains("3", motivo);
        Assert.Contains("fora dela", motivo);
    }

    /// <summary>
    /// Rede para erro de escala, como nas outras classes: quem digitou 150
    /// achando que a seção era em milímetro.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, 2.5, 0.15, 0.07)]
    [InlineData(-3, 2.5, 0.15, 0.07)]
    [InlineData(300, 2.5, 0.15, 0.07)]
    [InlineData(3, 2.5, 0, 0.07)]
    [InlineData(3, 2.5, 150, 0.07)]
    [InlineData(3, 2.5, 0.15, -0.07)]
    [InlineData(double.NaN, 2.5, 0.15, 0.07)]
    [InlineData(3, double.PositiveInfinity, 0.15, 0.07)]
    public void MedidaImpossivelNaoEhEstrutura(double t1, double t2, double largura, double profundidade)
    {
        Assert.False(new TableFrame(t1, t2, largura, profundidade).IsValid);
    }

    /// <summary>
    /// Cada motivo nomeia um campo só. Sem isso, a janela do 3.7 diria
    /// "inválida" e o usuário procuraria no escuro qual dos quatro campos
    /// errou.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-3, 2.5, 0.15, 0.07, "comprimento da tesoura")]
    [InlineData(3, 2.5, -0.15, 0.07, "largura do pilar")]
    [InlineData(3, 2.5, 0.15, -0.07, "profundidade do pilar")]
    [InlineData(3, -2.5, 0.15, 0.07, "posição do pilar")]
    public void CadaMotivoNomeiaOSeuCampo(
        double t1, double t2, double largura, double profundidade, string campo)
    {
        var estrutura = new TableFrame(t1, t2, largura, profundidade);

        Assert.False(estrutura.IsValid);
        Assert.Contains(campo, estrutura.WhyInvalid!);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoTrazOsNumerosComVirgula()
    {
        var texto = Padrao().Describe();

        Assert.Contains("3", texto);
        Assert.Contains("2,5", texto);
        Assert.Contains("0,15", texto);
        Assert.Contains("0,07", texto);
        Assert.DoesNotContain("0.15", texto);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoDeUmaEstruturaQuebradaDizOMotivo()
    {
        var texto = new TableFrame(3.00, 4.20, 0.15, 0.07).Describe();

        Assert.Contains("inválida", texto);
        Assert.Contains("fora dela", texto);
    }
}
