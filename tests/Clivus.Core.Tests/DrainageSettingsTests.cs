namespace Clivus.Core.Tests;

/// <summary>Passos 26.1, 28.1 e 31.2: o que a drenagem grava no desenho volta igual, e o desenho antigo abre com o padrão.</summary>
public class DrainageSettingsTests
{
    [Fact]
    [Trait("Etapa", "26")]
    public void IdaEVoltaPeloJson()
    {
        var original = new DrainageSettings
        {
            Arrows = new ArrowSettings { Spacing = 7.5, ShowValue = true, Limits = [2, 4, 8, 16] },
            Profile = new ProfileSettings { StationSpacing = 10, SegmentLength = 25, VerticalExaggeration = 5 },
            Hydrology = new HydrologyParameters { ReturnPeriod = 50, RunoffC = 0.3, RoadLayers = "VIAS;ESTRADA" },
            Sections = [ChannelSection.Defaults[1] with { Name = "MC 40", Radius = 0.2 }],
            Basins = new BasinSettings { CellSize = 3 },
        };

        var volta = DrainageSettings.Parse(original.ToJson(), out var problema);

        Assert.Null(problema);
        Assert.Equal(original.Arrows.Spacing, volta.Arrows.Spacing);
        Assert.True(volta.Arrows.ShowValue);
        Assert.Equal([2.0, 4, 8, 16], volta.Arrows.Limits);
        Assert.Equal(original.Profile, volta.Profile);
        Assert.Equal(original.Hydrology, volta.Hydrology);
        Assert.Equal(original.Sections.Single(), volta.Sections.Single());
        Assert.Equal(ChannelKind.HalfPipe, volta.Sections.Single().Kind);
        Assert.Equal(3, volta.Basins.CellSize);
    }

    [Fact]
    [Trait("Etapa", "26")]
    public void JsonVazioOuQuebradoVoltaOPadrao()
    {
        Assert.Equal(5, DrainageSettings.Parse(null, out _).Arrows.Spacing);

        var quebrado = DrainageSettings.Parse("{nao e json", out var problema);
        Assert.NotNull(problema);
        Assert.Equal(2, quebrado.Sections.Count);

        // Desenho de versão anterior: só as flechas gravadas, o resto padrão.
        var parcial = DrainageSettings.Parse("{\"Arrows\":{\"Spacing\":3}}", out problema);
        Assert.Null(problema);
        Assert.Equal(3, parcial.Arrows.Spacing);
        Assert.Equal(25, parcial.Hydrology.ReturnPeriod);
        Assert.Equal("Trap 30/60", parcial.Sections[0].Name);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void OPadraoEOMemorialDeValparaiso()
    {
        var p = new HydrologyParameters();

        Assert.Null(p.Problem());
        Assert.Equal(186.73, p.Intensity(5), 2);
        Assert.Equal(0.15, p.RunoffC);
        Assert.Equal(0.005, p.MinimumSlope);
        Assert.Equal(1.0, p.MinimumVelocity);
    }

    [Fact]
    [Trait("Etapa", "26")]
    public void PedacosRemontamOTexto()
    {
        var texto = new DrainageSettings().ToJson();
        var pedacos = DrainageSettings.Chunks(texto, 100);

        Assert.All(pedacos, p => Assert.True(p.Length <= 100));
        Assert.Equal(texto, string.Concat(pedacos));
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void SecaoPeloNomeIgnoraMaiusculas()
    {
        var s = new DrainageSettings();

        Assert.Equal("Meia-cana 30", s.Section(" meia-CANA 30 ")?.Name);
        Assert.Null(s.Section("não existe"));
    }

    [Theory]
    [InlineData("{\"Arrows\":{\"Limits\":null}}")]
    [InlineData("{\"Arrows\":{\"Limits\":[1,2]}}")]
    [InlineData("{\"Sections\":[null]}")]
    [Trait("Etapa", "26")]
    public void ListaTortaNoJsonVoltaOPadraoComOMotivo(string json)
    {
        // Revisão da etapa 26: com isso a janela das flechas não abria mais.
        var lidas = DrainageSettings.Parse(json, out var problema);

        Assert.NotNull(problema);
        Assert.Equal(4, lidas.Arrows.Limits.Count);
        Assert.NotEmpty(lidas.Sections);
        Assert.All(lidas.Sections, s => Assert.NotNull(s));
    }
}
