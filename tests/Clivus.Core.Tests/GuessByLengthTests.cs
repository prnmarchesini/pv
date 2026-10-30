namespace Clivus.Core.Tests;

/// <summary>
/// O palpite do perfil de uma mesa desenhada pelo comprimento (revisão do
/// 39.2): sabendo o tipo da mesa, só um perfil do mesmo tipo serve. A 2P14
/// monoposte e a GC800 têm o mesmo comprimento, e sem o tipo o palpite
/// trocava a estrutura da mesa no Recalcular, nas Pontas e no Regerar fileira.
/// </summary>
public class GuessByLengthTests
{
    private static TableProfile Gc800 => BifilaReference.Profile(BifilaVariant.Gc800);

    private static TableProfile Mono2P14 => Gc800 with { Name = "2P14 monoposte", Frame = new TableFrame(3.00, 1.50, 0.15, 0.07, 3.50, 0.851) };

    [Fact]
    [Trait("Etapa", "39")]
    public void ComOTipoSoServeOPerfilDoMesmoTipo()
    {
        var comprimento = Gc800.Layout.Length;
        Assert.Equal(comprimento, Mono2P14.Layout.Length, 9);

        Assert.Equal("2P14 monoposte", DrawingTables.GuessByLength([Mono2P14, Gc800], comprimento, bifila: false)!.Name);
        Assert.True(DrawingTables.GuessByLength([Mono2P14, Gc800], comprimento, bifila: true)!.IsBifila);
        Assert.Null(DrawingTables.GuessByLength([Mono2P14], comprimento, bifila: true));
        Assert.Null(DrawingTables.GuessByLength([Gc800], comprimento, bifila: false));
    }

    [Fact]
    [Trait("Etapa", "39")]
    public void SemOTipoServeOPrimeiroDoComprimento()
    {
        var comprimento = Gc800.Layout.Length;

        Assert.Equal("2P14 monoposte", DrawingTables.GuessByLength([Mono2P14, Gc800], comprimento, bifila: null)!.Name);
        Assert.Null(DrawingTables.GuessByLength([Mono2P14, Gc800], comprimento + 0.06, bifila: null));
        Assert.NotNull(DrawingTables.GuessByLength([Gc800], comprimento + 0.049, bifila: null));
    }
}
