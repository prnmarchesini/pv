namespace Clivus.Core.Tests;

/// <summary>Os carports do desenho (35.1): registro próprio, ao lado do das mesas.</summary>
public class DrawingCarportsTests
{
    private static DrawingCarport Carport(string nome) => new(CarportProfile.Reference with { Name = nome }, DrawingCarports.DefaultColor);

    private static DrawingTable Mesa(string nome) => new(
        new TableProfile(nome,
            new TableLayout(new SolarModule("Risen", "X", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.1, 0.1),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
            20 * Math.PI / 180),
        DrawingTables.Palette[0], Use: true);

    [Fact]
    [Trait("Etapa", "35")]
    public void OsCarportsVoltamIguais()
    {
        IReadOnlyList<DrawingCarport> lista = [Carport("Estacionamento A"), Carport("Estacionamento B") with { Color = new RgbColor(1, 2, 3) }];

        var voltou = DrawingCarports.Decode(DrawingCarports.Encode(lista), out var problemas);

        Assert.Empty(problemas);
        Assert.Equal(lista, voltou);
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void NenhumCampoPassaDoTamanhoDoPedaco()
    {
        Assert.All(DrawingCarports.Encode([Carport("A")]), c => Assert.True(c.Length <= 200));
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void RegistroVazioEListaVazia()
    {
        Assert.Empty(DrawingCarports.Decode(null, out var p1));
        Assert.Empty(p1);
        Assert.Empty(DrawingCarports.Decode([], out _));
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void OutraVersaoDizOMotivo()
    {
        Assert.Empty(DrawingCarports.Decode(["9", "0"], out var problemas));
        Assert.Single(problemas);
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void CarportIlegivelFicaDeForaEOsOutrosEntram()
    {
        var campos = DrawingCarports.Encode([Carport("A"), Carport("B")]).ToList();
        // Estraga o JSON do primeiro: o primeiro pedaço dele passa a não ser JSON.
        campos[4] = "x" + campos[4];

        var voltou = DrawingCarports.Decode(campos, out var problemas);

        Assert.Single(voltou);
        Assert.Equal("B", voltou[0].Name);
        Assert.Single(problemas);
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void NomeRepetidoOuIgualAoDeUmaMesaERecusado()
    {
        Assert.Null(DrawingCarports.WhyInvalid([Carport("A"), Carport("B")], [Mesa("Mesa 28")]));
        Assert.NotNull(DrawingCarports.WhyInvalid([Carport("A"), Carport(" a ")], []));
        Assert.NotNull(DrawingCarports.WhyInvalid([Carport("Mesa 28")], [Mesa("Mesa 28")]));
        Assert.NotNull(DrawingCarports.WhyInvalid([Carport("  ")], []));
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void ORegistroDasMesasNaoMuda()
    {
        // Regra 4 do plano: as mesas continuam com a chave e o formato delas.
        Assert.Equal("MESAS", DrawingTables.StorageKey);
        Assert.NotEqual(DrawingTables.StorageKey, DrawingCarports.StorageKey);
    }
}
