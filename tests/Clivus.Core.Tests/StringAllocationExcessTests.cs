namespace Clivus.Core.Tests;

/// <summary>Excesso de capacidade do inversor (elétrica, 14.4).</summary>
public class StringAllocationExcessTests
{
    private static readonly InverterModel Modelo26 = new(Guid.NewGuid(), "Huawei 250", 13, 2, ElectricalDefaults.InverterSize);

    [Theory]
    [Trait("Etapa", "14")]
    [InlineData(0, 0)]
    [InlineData(25, 0)]
    [InlineData(26, 0)]
    [InlineData(27, 1)]
    [InlineData(28, 2)]
    public void ExcessoEOQuePassaDoTotalDeEntradas(int strings, int excesso)
    {
        Assert.Equal(26, Modelo26.TotalInputs);
        Assert.Equal(excesso, StringAllocation.Excess(strings, Modelo26));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OAvisoDizInversorStringsEntradasEOQuantoPassou()
    {
        var inversor = new Inverter(Guid.NewGuid(), Modelo26.Id, "Inversor 3", Guid.Empty);

        Assert.Null(StringAllocation.ExcessWarning(inversor, Modelo26, 26));

        var aviso = StringAllocation.ExcessWarning(inversor, Modelo26, 28);
        Assert.NotNull(aviso);
        Assert.Contains("Inversor 3", aviso);
        Assert.Contains("28", aviso);
        Assert.Contains("26", aviso);
        Assert.Contains("2 a mais", aviso);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemModeloNaoHaComoDizerExcesso()
    {
        var inversor = new Inverter(Guid.NewGuid(), Guid.NewGuid(), "Inversor 1", Guid.Empty);

        Assert.Equal(0, StringAllocation.Excess(40, null));
        Assert.Null(StringAllocation.ExcessWarning(inversor, null, 40));
    }
}
