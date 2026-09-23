using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A identidade da linha de alinhamento.
///
/// Ela carrega uma coisa que a identidade da área não tem: o lado. E o lado é
/// a informação mais fácil de perder e mais difícil de perceber perdida — a
/// usina inteira nasce do outro lado da linha, e o desenho fica perfeito.
/// </summary>
public class AlignmentIdentityTests
{
    private static readonly DateTime Agora = new(2026, 9, 23, 14, 30, 0);

    [Fact]
    [Trait("Etapa", "4")]
    public void UmAlinhamentoNovoTemIdentidadePropria()
    {
        var a = AlignmentIdentity.Create("Cerca norte", LineSide.Left, Agora);
        var b = AlignmentIdentity.Create("Cerca norte", LineSide.Left, Agora);

        Assert.True(a.IsValid);
        Assert.NotEqual(Guid.Empty, a.Id);

        // Dois alinhamentos com o mesmo nome continuam sendo dois.
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OLadoEGuardadoComAIdentidade()
    {
        Assert.Equal(LineSide.Left, AlignmentIdentity.Create("x", LineSide.Left, Agora).Side);
        Assert.Equal(LineSide.Right, AlignmentIdentity.Create("x", LineSide.Right, Agora).Side);
    }

    /// <summary>
    /// "Em cima da linha" não é lado: é o que sobra de o usuário ter clicado
    /// sobre a própria linha. Um alinhamento assim não diz onde pôr mesa
    /// nenhuma, e aceitar isso adiaria o erro para a etapa 5.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void AlinhamentoSemLadoNaoEValido()
    {
        var sem = AlignmentIdentity.Create("Cerca", LineSide.On, Agora);

        Assert.False(sem.IsValid);
    }

    /// <summary>
    /// GUID vazio é o que sobra de um XData truncado, e tratar isso como
    /// identidade faria dois alinhamentos diferentes parecerem o mesmo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void GuidVazioNaoEIdentidade()
    {
        Assert.False(new AlignmentIdentity(Guid.Empty, "x", LineSide.Left, Agora).IsValid);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemNomeOAlinhamentoAindaSeApresenta(string? nome)
    {
        var alinhamento = AlignmentIdentity.Create(nome, LineSide.Right, Agora);

        Assert.Equal(AlignmentIdentity.SemNome, alinhamento.DisplayName);
        Assert.True(alinhamento.IsValid);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void ONomeVemSemEspacoEmVolta()
    {
        Assert.Equal("Cerca norte", AlignmentIdentity.Create("  Cerca norte  ", LineSide.Left, Agora).DisplayName);
    }

    /// <summary>
    /// A descrição diz o lado em português, porque é o que o usuário precisa
    /// conferir ao reabrir o desenho: o nome ele reconhece, o lado não.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void ADescricaoDizOLadoEADataEmPortugues()
    {
        var texto = AlignmentIdentity.Create("Cerca norte", LineSide.Left, Agora).Describe();

        Assert.Contains("Cerca norte", texto);
        Assert.Contains("esquerda", texto);
        Assert.Contains("23/09/2026", texto);

        var direita = AlignmentIdentity.Create("Cerca sul", LineSide.Right, Agora).Describe();

        Assert.Contains("direita", direita);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void OTipoDistingueDeOutrasCoisasNossas()
    {
        Assert.Equal("Alinhamento", AlignmentIdentity.Tipo);
        Assert.NotEqual(AreaIdentity.Tipo, AlignmentIdentity.Tipo);
    }
}
