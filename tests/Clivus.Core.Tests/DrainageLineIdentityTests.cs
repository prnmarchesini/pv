namespace Clivus.Core.Tests;

/// <summary>Passos 27.1 e 28.2: a identidade da linha e da canaleta no XData.</summary>
public class DrainageLineIdentityTests
{
    [Fact]
    [Trait("Etapa", "27")]
    public void LinhaDeAnaliseVaiEVolta()
    {
        var linha = new DrainageLineIdentity { Name = "Linha 1" };
        var volta = DrainageLineIdentity.FromFields(linha.ToFields());

        Assert.NotNull(volta);
        Assert.Equal(linha.Id, volta.Id);
        Assert.Equal("Linha 1", volta.Name);
        Assert.False(volta.IsChannel);
        Assert.Null(volta.Chart);
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void CanaletaVaiEVoltaComSecaoModoEGrafico()
    {
        var canaleta = new DrainageLineIdentity
        {
            Name = "Canaleta 2",
            Section = ChannelSection.Defaults[0] with { Height = 0.5 },
            Mode = ChannelBottomMode.MinimumSlope,
            Chart = (123456.789, 7400000.125),
        };

        var volta = DrainageLineIdentity.FromFields(canaleta.ToFields());

        Assert.NotNull(volta);
        Assert.True(volta.IsChannel);
        Assert.Equal(canaleta.Section, volta.Section);
        Assert.Equal(ChannelBottomMode.MinimumSlope, volta.Mode);
        Assert.Equal(canaleta.Chart, volta.Chart);
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void CampoTortoNaoViraLinha()
    {
        var bons = new DrainageLineIdentity { Name = "C", Section = ChannelSection.Defaults[0] }.ToFields();

        var semGuid = (string[])bons.Clone();
        semGuid[0] = "nao-e-guid";
        Assert.Null(DrainageLineIdentity.FromFields(semGuid));

        var baseTorta = (string[])bons.Clone();
        baseTorta[8] = "abc";
        Assert.Null(DrainageLineIdentity.FromFields(baseTorta));

        Assert.Null(DrainageLineIdentity.FromFields(bons.Take(5).ToArray()));
        Assert.NotNull(DrainageLineIdentity.FromFields(bons));
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void TodoCampoCabeNoLimiteDoXData()
    {
        var longa = new DrainageLineIdentity
        {
            Name = new string('n', 400),
            Section = ChannelSection.Defaults[0] with { Name = new string('s', 400), Lining = new string('r', 400), Base = 1.0 / 3 },
            Chart = (-1234567.123456789, 7654321.987654321),
        };

        Assert.All(longa.ToFields(), c => Assert.True(System.Text.Encoding.UTF8.GetByteCount(c) <= 255, $"campo de {c.Length} caracteres"));
        Assert.Equal(DrainageLineIdentity.FieldCount + 2, longa.ToFields().Length);
    }

    [Fact]
    [Trait("Etapa", "32")]
    public void GraficoApagadoGuardaOLugarEVoltaDosCampos()
    {
        var linha = new DrainageLineIdentity { Name = "Linha 1", Chart = (10, 20), ChartHidden = true };

        var lida = DrainageLineIdentity.FromFields(linha.ToFields())!;
        Assert.True(lida.ChartHidden);
        Assert.Equal((10.0, 20.0), lida.Chart);

        // XData de antes (17 campos, sem o 18º): gráfico à vista.
        var antiga = DrainageLineIdentity.FromFields(linha.ToFields().Take(DrainageLineIdentity.FieldCount + 1).ToArray())!;
        Assert.False(antiga.ChartHidden);
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void ProximoNomeUsaOMaiorNumero()
    {
        Assert.Equal("Linha 1", DrainageLineIdentity.NextName("Linha", []));
        Assert.Equal("Linha 4", DrainageLineIdentity.NextName("Linha", ["Linha 1", "Linha 3", "Canaleta 9", "Linha x"]));
    }
}
