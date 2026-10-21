namespace Clivus.Core.Tests;

/// <summary>Passos 32.6 e 32.7: a caixa entre canaletas e o Exportar canaletas.</summary>
public class DrainageExportTests
{
    [Fact]
    [Trait("Etapa", "32")]
    public void OFundoDaCaixaFicaAbaixoDaCanaletaMaisFunda()
    {
        var b = new BoxSettings();

        Assert.Equal(98.9 - 0.10, DrainageExport.BoxFloor(b, 100, [99.2, 98.9]), 9);
        Assert.Equal(100 - 0.50, DrainageExport.BoxFloor(b, 100, []), 9);

        // Canaleta rasa: a profundidade mínima manda.
        Assert.Equal(100 - 0.50, DrainageExport.BoxFloor(b, 100, [99.8]), 9);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void OConcretoDaCaixaEParedeMaisFundo()
    {
        var cx = new BoxRecord("CX 1", 0.6, 0.6, 0.15, 100, 99);

        Assert.Equal(1, cx.Depth, 9);
        Assert.Equal(0.9 * 0.9 * 1.15 - 0.6 * 0.6 * 1, cx.Concrete, 9);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void OExcelTemOsMetrosDeCadaCanaletaEDeCadaTipo()
    {
        var trap = ChannelSection.Defaults[0];
        var mc = ChannelSection.Defaults[1];
        ChannelExportRow C(string nome, ChannelSection s, double metros, DissipatorEnd d = DissipatorEnd.None) =>
            new(nome, s, new ChannelQuantities(metros, metros + 1, s.WallArea * metros, s.ExternalArea * metros, 0), d);

        var tabelas = DrainageExport.Tables(
            [C("Canaleta 1", trap, 100), C("Canaleta 2", trap, 50, DissipatorEnd.Downstream), C("Canaleta 3", mc, 30)],
            [new BoxRecord("CX 1", 0.6, 0.6, 0.15, 100, 99)]);

        Assert.Equal(["Canaletas", "Por tipo", "Caixas", "Dissipadores"], tabelas.Select(t => t.Title));

        var canaletas = tabelas[0];
        Assert.Equal(3, canaletas.Rows.Count);
        Assert.Equal(180.0, (double)canaletas.Total[5]!, 9);

        var porTipo = tabelas[1];
        Assert.Equal(2, porTipo.Rows.Count);
        var trapezio = porTipo.Rows.Single(r => (string?)r[0] == "Trap 30/60");
        Assert.Equal(2, trapezio[3]);
        Assert.Equal(150.0, (double)trapezio[4]!, 9);

        Assert.Single(tabelas[2].Rows);
        var dissipador = Assert.Single(tabelas[3].Rows);
        Assert.Equal("DES 73-219", dissipador[2]);

        var planilha = new XlsxWriter();
        foreach (var t in tabelas) QuantityReport.Tabela(planilha, t);
        Assert.True(planilha.ToBytes().Length > 1000);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void AVazaoCresceAoLongoDaCanaleta()
    {
        // 100 células de 4 m² entrando uniformemente de 0 a 99 m, mais 10 sem estaca (ajuste manual): no fim.
        var estacas = Enumerable.Range(0, 100).Select(i => (double)i).Concat(Enumerable.Repeat(double.NaN, 10));
        var curva = BasinEntry.Accumulate(estacas, 4, 100);
        var bacia = new BasinEntry(Guid.NewGuid(), "C", 440, 50, 2, true) { Accumulation = curva };

        Assert.Equal(4, bacia.AreaUpTo(0), 9);
        Assert.Equal(51 * 4, bacia.AreaUpTo(50), 9);
        Assert.Equal(440, bacia.AreaUpTo(100), 9);

        var perfil = ChannelProfile.Build(new Clivus.Geo.TerrainProfile([new(0, 0, 0, 100, true), new(100, 100, 0, 99, true)], false, 0), 0.35, ChannelBottomMode.FollowTerrain, 0.005, 20);
        var calculo = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", ChannelSection.Defaults[0], perfil, bacia));

        var (area, q) = DrainageMemory.FlowAt(new HydrologyParameters(), calculo, 50);
        Assert.Equal(204, area, 9);
        Assert.Equal(0.15 * calculo.Intensity * 204 / 3600, q, 9);
        Assert.True(q < calculo.FlowLitersPerSecond);

        // A curva vai e volta no JSON das bacias gravadas.
        var volta = BasinReport.Parse(new BasinReport { Basins = [bacia] }.ToJson())!.Basins.Single();
        Assert.Equal(bacia.AreaUpTo(50), volta.AreaUpTo(50), 9);
    }
}
