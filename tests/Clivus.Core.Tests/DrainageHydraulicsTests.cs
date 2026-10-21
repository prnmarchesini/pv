namespace Clivus.Core.Tests;

/// <summary>
/// Passos 28.1 e 31.1: a seção da canaleta e o motor hidráulico, contra os
/// números de ouro do memorial de Valparaiso (plano/drenagem/02-calculos-hidraulicos.md).
/// A tolerância é a do arredondamento do memorial; não se afrouxa.
/// </summary>
public class DrainageHydraulicsTests
{
    private static readonly ChannelSection Trap = ChannelSection.Defaults[0];

    // ---- Tabela 1: escoamento superficial ---------------------------------

    [Theory]
    [InlineData(179.85, 5.51, 3.06, 7.05, 4.07, 0.60)]
    [InlineData(166.23, 8.70, 5.23, 5.28, 3.12, 0.55)]
    [InlineData(166.54, 6.50, 3.90, 6.07, 3.50, 0.56)]
    [Trait("Etapa", "31")]
    public void Tabela1DoMemorial(double l, double dh, double s, double kerby, double kirpich, double v)
    {
        Assert.Equal(kerby, Rainfall.Kerby(l, s), 2);
        Assert.Equal(kirpich, Rainfall.Kirpich(l, dh), 2);

        var tc = Rainfall.AdoptedTc(Rainfall.Kirpich(l, dh), Rainfall.Kerby(l, s));
        Assert.Equal(5.00, tc, 2);
        Assert.Equal(v, Rainfall.SurfaceVelocity(l, tc), 2);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void TcAdotadoEOMenorDosDoisAcimaDoMinimo()
    {
        Assert.Equal(8.0, Rainfall.AdoptedTc(8.0, 12.0));
        Assert.Equal(5.0, Rainfall.AdoptedTc(3.0, 12.0));
        Assert.Equal(9.0, Rainfall.AdoptedTc(double.NaN, 9.0));
        Assert.Equal(5.0, Rainfall.AdoptedTc(double.NaN, double.NaN));
    }

    // ---- Tabela 3: intensidade --------------------------------------------

    [Theory]
    [InlineData(25, 5, 186.73)]
    [InlineData(15, 5, 173.67)]
    [InlineData(15, 60, 78.97)]
    [InlineData(50, 30, 130.23)]
    [InlineData(100, 1440, 12.01)]
    [Trait("Etapa", "31")]
    public void Tabela3DoMemorial(double t, double minutos, double esperado)
    {
        Assert.Equal(esperado, Rainfall.Intensity(1544.886, 0.142, 24.953, 0.756, t, minutos), 2);
    }

    // ---- Tabela 5: vazão ---------------------------------------------------

    [Theory]
    [InlineData(13289, 103.39)]
    [InlineData(30120, 234.35)]
    [Trait("Etapa", "31")]
    public void Tabela5DoMemorial(double area, double litros)
    {
        Assert.Equal(litros, Rainfall.RationalFlowLitersPerSecond(0.15, 186.73, area), 2);
    }

    // ---- Tabela 8: Manning -------------------------------------------------

    [Theory]
    [InlineData(0.1034, 0.0434, 0.207, 2.58)]
    [InlineData(0.2343, 0.0472, 0.327, 3.33)]
    [Trait("Etapa", "31")]
    public void Tabela8DoMemorial(double q, double imax, double hmax, double vmax)
    {
        var h = Hydraulics.NormalDepth(Trap, q, 0.005);
        Assert.Equal(hmax, h, 3);

        var hNaMaxima = Hydraulics.NormalDepth(Trap, q, imax);
        Assert.Equal(vmax, Hydraulics.Velocity(Trap, hNaMaxima, imax), 2);

        Assert.Equal(0.10, Hydraulics.Freeboard(q));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void LaminaSemDeclividadeNaoTemResposta()
    {
        Assert.True(double.IsNaN(Hydraulics.NormalDepth(Trap, 0.1, 0)));
        Assert.True(double.IsNaN(Hydraulics.NormalDepth(Trap, 0.1, -0.01)));
        Assert.Equal(0, Hydraulics.NormalDepth(Trap, 0, 0.01));
    }

    // ---- Tabela 9: dimensões ----------------------------------------------

    [Fact]
    [Trait("Etapa", "31")]
    public void Tabela9DoMemorial()
    {
        // Canaleta 1: y sugerido 0,35 (0,207 + 0,10 → 0,35 no passo de 0,05).
        Assert.Equal(0.35, Hydraulics.SuggestedHeight(Trap, 0.1034, 0.005, 0.05), 9);

        var c1 = Trap with { Height = 0.35 };
        Assert.Equal(0.21, c1.SideOpening, 9);
        Assert.Equal(0.72, c1.TopWidth, 9);

        // Canaleta 2: o memorial adotou 0,50 (x 0,30; B 0,90). No passo de
        // 0,05 a sugestão é 0,45; no de 0,10, 0,50 (decisão 1 do PROGRESSO).
        Assert.Equal(0.45, Hydraulics.SuggestedHeight(Trap, 0.2343, 0.005, 0.05), 9);
        Assert.Equal(0.50, Hydraulics.SuggestedHeight(Trap, 0.2343, 0.005, 0.10), 9);

        var c2 = Trap with { Height = 0.50 };
        Assert.Equal(0.30, c2.SideOpening, 9);
        Assert.Equal(0.90, c2.TopWidth, 9);
    }

    // ---- Tabela 11: dissipador; transposição -------------------------------

    [Theory]
    [InlineData(0.72, 2.16, 1.44, "DES 73-219")]
    [InlineData(0.90, 2.70, 1.80, "DES 90-270")]
    [Trait("Etapa", "31")]
    public void Tabela11DoMemorial(double b, double l2, double comprimento, string modelo)
    {
        var (l1, l2Calc, compCalc) = EnergyDissipator.Size(b);
        Assert.Equal(b, l1, 9);
        Assert.Equal(l2, l2Calc, 9);
        Assert.Equal(comprimento, compCalc, 9);
        Assert.Equal(modelo, EnergyDissipator.Model(b));
    }

    [Theory]
    [InlineData(0.72, "TSS 120")]
    [InlineData(0.90, "TSS 120")]
    [InlineData(0.92, "TSS 130")]
    [InlineData(1.80, null)]
    [Trait("Etapa", "31")]
    public void TransposicaoPelaAbertura(double b, string? modelo)
    {
        Assert.Equal(modelo, ChannelCrossing.Model(b));
    }

    // ---- Bordo livre (Tabela 36 do DNIT) -----------------------------------

    [Theory]
    [InlineData(0.25, 0.10)]
    [InlineData(0.26, 0.13)]
    [InlineData(0.56, 0.13)]
    [InlineData(0.84, 0.14)]
    [InlineData(1.40, 0.15)]
    [InlineData(2.80, 0.18)]
    [InlineData(3.00, 0.20)]
    [Trait("Etapa", "31")]
    public void BordoLivreDaTabela36(double q, double f)
    {
        Assert.Equal(f, Hydraulics.Freeboard(q));
    }

    // ---- Meia-cana ---------------------------------------------------------

    [Fact]
    [Trait("Etapa", "28")]
    public void MeiaCanaCheiaEMeioCirculo()
    {
        var mc = ChannelSection.Defaults[1] with { Radius = 0.2 };

        Assert.Equal(Math.PI * 0.04 / 2, mc.WetArea(0.2), 9);
        Assert.Equal(Math.PI * 0.2, mc.WetPerimeter(0.2), 9);
        Assert.Equal(0.4, mc.WaterWidth(0.2), 9);
        Assert.Equal(0.4, mc.TopWidth, 9);
        Assert.Equal(0.2, mc.UsefulHeight, 9);
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void MeiaCanaAfogadaNaoTemLamina()
    {
        var mc = ChannelSection.Defaults[1] with { Radius = 0.1 };
        var cheia = Hydraulics.Flow(mc, 0.1, 0.01);

        Assert.True(double.IsNaN(Hydraulics.NormalDepth(mc, cheia * 1.01, 0.01)));
        Assert.Equal(0.1, Hydraulics.NormalDepth(mc, cheia, 0.01), 6);
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void MeiaCanaSugereORaioQueCabeComBordoLivre()
    {
        var mc = ChannelSection.Defaults[1];
        var r = Hydraulics.SuggestedHeight(mc, 0.1, 0.01, 0.05);

        var h = Hydraulics.NormalDepth(mc with { Radius = r }, 0.1, 0.01);
        Assert.True(h + 0.10 <= r + 1e-9, $"r {r} não cabe h {h} + f");

        var menor = mc with { Radius = r - 0.05 };
        var hMenor = Hydraulics.NormalDepth(menor, 0.1, 0.01);
        Assert.True(double.IsNaN(hMenor) || hMenor + 0.10 > menor.Radius, "um raio menor também servia");
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void ParedeDaTrapezoidalBateComAConta()
    {
        // b 0,30; z 0,6; y 0,35; e 0,10. A parede externa afasta e na normal.
        var s = Trap;
        var raiz = Math.Sqrt(1 + 0.36);
        var bExt = 0.30 + 2 * 0.10 * (raiz - 0.6);
        var externa = (bExt + 0.6 * 0.45) * 0.45;
        var interna = (0.30 + 0.21) * 0.35;

        Assert.Equal(externa, s.ExternalArea, 9);
        Assert.Equal(externa - interna, s.WallArea, 9);
        Assert.True(s.WallArea > 0);
    }

    [Theory]
    [InlineData("Base", 0)]
    [InlineData("Height", -1)]
    [InlineData("Manning", 0)]
    [Trait("Etapa", "28")]
    public void SecaoComMedidaRuimDizPorque(string campo, double valor)
    {
        var s = campo switch
        {
            "Base" => Trap with { Base = valor },
            "Height" => Trap with { Height = valor },
            _ => Trap with { Manning = valor },
        };

        Assert.NotNull(s.Problem());
        Assert.Null(Trap.Problem());
    }
}
