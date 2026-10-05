namespace Clivus.Core.Tests;

/// <summary>
/// A cor de cada inversor (pedido do Renan em 05/10/2026): as strings dele
/// são pintadas com ela. Automática e distinta ao criar, legível no fundo
/// escuro e no claro, sem as cores da sombra (lilás, violeta, roxo) nem as
/// de aviso (magenta, vermelho). O formato 1 (sem cor) é lido com a cor
/// automática.
/// </summary>
public class InverterColorTests
{
    private static readonly InverterModel Modelo = new(Guid.NewGuid(), "Huawei 250", 5, 4, ElectricalDefaults.InverterSize);

    private static double Luminancia(RgbColor c)
    {
        static double F(byte v)
        {
            var x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * F(c.R) + 0.7152 * F(c.G) + 0.0722 * F(c.B);
    }

    private static double Contraste(RgbColor a, RgbColor b)
    {
        var (la, lb) = (Luminancia(a), Luminancia(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Matiz(RgbColor c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        if (d == 0) return -1;
        var h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60 + 360) % 360;
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void APaletaELegivelNoEscuroENoClaroESemAsCoresDaSombraNemDeAviso()
    {
        var escuro = new RgbColor(33, 40, 48);    // o fundo do modelo no Civil 3D
        var claro = new RgbColor(255, 255, 255);

        Assert.True(InverterColors.Palette.Count >= 8);
        Assert.Equal(InverterColors.Palette.Count, InverterColors.Palette.Select(p => p.Color).Distinct().Count());

        foreach (var (nome, cor) in InverterColors.Palette)
        {
            Assert.True(Contraste(cor, escuro) >= 2.4, $"{nome} some no fundo escuro");
            Assert.True(Contraste(cor, claro) >= 2.4, $"{nome} some no fundo claro");

            // Vermelho e magenta (aviso) e lilás, violeta e roxo (sombra) ficam de fora: matiz de 15° a 215°.
            var h = Matiz(cor);
            Assert.True(h is >= 15 and <= 215, $"{nome} (matiz {h:0}°) é vermelho, magenta ou roxo");
        }
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void InversoresCriadosGanhamCoresDistintas()
    {
        var setup = new ElectricalSetup(models: [Modelo]);

        var novos = setup.AddInverters(Modelo.Id, InverterColors.Palette.Count);

        Assert.All(novos, i => Assert.NotNull(i.Color));
        Assert.Equal(novos.Count, novos.Select(i => i.Color).Distinct().Count());

        // Depois de usar a paleta toda, ela recomeça pela primeira (todas empatadas em uso).
        var mais = setup.AddInverters(Modelo.Id, 1)[0];
        Assert.Equal(InverterColors.Palette[0].Color, mais.Color);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ACorDoInversorNovoEAMenosUsadaEntreOsQueJaHa()
    {
        var azul = InverterColors.Palette[1].Color;
        var laranja = InverterColors.Palette[0].Color;
        var setup = new ElectricalSetup(inverters: [new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty, laranja)], models: [Modelo]);

        Assert.Equal(azul, setup.AddInverters(Modelo.Id, 1)[0].Color);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ACorVaiEVoltaNoFormato2()
    {
        var i = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty, new RgbColor(12, 200, 34));
        var campos = i.ToFields();

        Assert.Equal(Inverter.FieldCount, campos.Count);
        Assert.Equal("#0CC822", campos[4]);
        Assert.Equal(i, Inverter.Parse(campos));
        Assert.Null(Inverter.Parse([i.Id.ToString("D"), Modelo.Id.ToString("D"), "I1", "", "#zz0000"]));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OFormato1SemCorELidoComACorAutomaticaEstavel()
    {
        var a = Inverter.Parse([Guid.NewGuid().ToString("D"), Modelo.Id.ToString("D"), "Inversor 1", ""]);
        var b = Inverter.Parse([Guid.NewGuid().ToString("D"), Modelo.Id.ToString("D"), "Inversor 2", ""]);
        Assert.NotNull(a);
        Assert.Null(a!.Color);

        var setup = new ElectricalSetup(inverters: [a, b!], models: [Modelo]);
        var deNovo = new ElectricalSetup(inverters: [a, b!], models: [Modelo]);

        Assert.Equal(2, setup.ColoredInverters);
        Assert.Equal(InverterColors.Palette[0].Color, setup.Inverters[0].Color);
        Assert.Equal(InverterColors.Palette[1].Color, setup.Inverters[1].Color);
        Assert.Equal(setup.Inverters.Select(i => i.Color), deNovo.Inverters.Select(i => i.Color));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void TrocarACorSoTrocaACor()
    {
        var setup = new ElectricalSetup(models: [Modelo]);
        var i = setup.AddInverters(Modelo.Id, 1)[0];
        var cor = new RgbColor(1, 2, 3);

        Assert.True(setup.SetInverterColor(i.Id, cor));
        Assert.Equal(i with { Color = cor }, setup.FindInverter(i.Id));
        Assert.False(setup.SetInverterColor(Guid.NewGuid(), cor));
    }
}
