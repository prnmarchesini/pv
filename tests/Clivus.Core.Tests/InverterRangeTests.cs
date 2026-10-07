namespace Clivus.Core.Tests;

/// <summary>O trafo em lote da aba Inversor (07/10/2026): as linhas "de ... a ...".</summary>
public class InverterRangeTests
{
    [Fact]
    [Trait("Etapa", "14")]
    public void AsLinhasDoIntervaloContadasDeUm()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5], InverterTable.Range(20, "1", "6").Rows);
        Assert.Equal([6, 7, 8], InverterTable.Range(20, " 9 ", "7").Rows);
        Assert.Equal([19], InverterTable.Range(20, "20", "").Rows);
        Assert.Null(InverterTable.Range(20, "7", "12").Problem);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ForaDaTabelaOuSemNumeroERecusado()
    {
        Assert.NotNull(InverterTable.Range(20, "0", "3").Problem);
        Assert.NotNull(InverterTable.Range(20, "15", "21").Problem);
        Assert.NotNull(InverterTable.Range(20, "", "3").Problem);
        Assert.NotNull(InverterTable.Range(20, "1", "x").Problem);
        Assert.NotNull(InverterTable.Range(0, "1", "1").Problem);
        Assert.Empty(InverterTable.Range(20, "15", "21").Rows);
    }
}
