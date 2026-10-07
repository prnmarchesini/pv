namespace Clivus.Core.Tests;

/// <summary>Repartir as strings nos limites dos inversores pelo kW (Renan, 07/10/2026).</summary>
public class BalancedLimitsTests
{
    private static List<LimitShare> Iguais(int n, double kw = 250, int entradas = 24) =>
        Enumerable.Range(0, n).Select(_ => new LimitShare(Guid.NewGuid(), kw, entradas)).ToList();

    [Fact]
    [Trait("Etapa", "14")]
    public void KwIguaisDaoOMesmoNumeroOuUmAMais()
    {
        var inv = Iguais(20);

        var exato = BalancedLimits.Split(inv, 240);
        Assert.All(inv, i => Assert.Equal(12, exato.Limits[i.Inverter]));
        Assert.Equal(0, exato.Leftover);

        var quebrado = BalancedLimits.Split(inv, 278);
        Assert.Equal(278, quebrado.Limits.Values.Sum());
        Assert.Equal(18, quebrado.Limits.Values.Count(v => v == 14));
        Assert.Equal(2, quebrado.Limits.Values.Count(v => v == 13));

        // A sobra do arredondamento vai para os primeiros da tabela.
        Assert.Equal(14, quebrado.Limits[inv[0].Inverter]);
        Assert.Equal(13, quebrado.Limits[inv[^1].Inverter]);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ProporcionalAoKw()
    {
        var grande = new LimitShare(Guid.NewGuid(), 300, 30);
        var pequeno = new LimitShare(Guid.NewGuid(), 100, 30);

        var r = BalancedLimits.Split([grande, pequeno], 40);

        Assert.Equal(30, r.Limits[grande.Inverter]);
        Assert.Equal(10, r.Limits[pequeno.Inverter]);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void QuemBateNasEntradasPassaORestoParaOsOutros()
    {
        var grande = new LimitShare(Guid.NewGuid(), 300, 12);
        var pequeno = new LimitShare(Guid.NewGuid(), 100, 30);

        var r = BalancedLimits.Split([grande, pequeno], 40);

        Assert.Equal(12, r.Limits[grande.Inverter]);
        Assert.Equal(28, r.Limits[pequeno.Inverter]);
        Assert.Equal(0, r.Leftover);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void MaisStringsQueEntradasSobra()
    {
        var inv = Iguais(2, entradas: 10);
        var r = BalancedLimits.Split(inv, 25);

        Assert.All(inv, i => Assert.Equal(10, r.Limits[i.Inverter]));
        Assert.Equal(5, r.Leftover);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemKwTodosPesamIgual()
    {
        var inv = Iguais(3, kw: 0);
        var r = BalancedLimits.Split(inv, 9);
        Assert.All(inv, i => Assert.Equal(3, r.Limits[i.Inverter]));
    }
}
