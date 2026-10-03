using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>Árvore (9.4), posição do sol (9.5), sombra num instante (9.6) e num período (9.8).</summary>
public class SolarAndShadingTests
{
    // ------------------------------------------------------------- árvore

    [Fact]
    [Trait("Etapa", "9")]
    public void ArvoreEPirulitoDeDoisCilindros()
    {
        var a = new TreeSpec(3, 0.4, 5, 6);

        Assert.Null(a.WhyInvalid());
        Assert.Equal(8, a.TotalHeight);

        var c = a.Cylinders(100, 200, 700);
        Assert.Equal(new ShadowCylinder(100, 200, 0.2, 700, 703), c[0]);
        Assert.Equal(new ShadowCylinder(100, 200, 3, 703, 708), c[1]);
        Assert.Equal("tronco 3 × 0,4 m, copa 5 × 6 m (altura × largura)", a.Describe());
    }

    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(0, 0.4, 5, 6)]
    [InlineData(3, -1, 5, 6)]
    [InlineData(3, 0.4, double.NaN, 6)]
    [InlineData(3, 0.4, 5, 0)]
    [InlineData(300, 0.4, 5, 6)]
    public void ArvoreComMedidaRuimDizOQue(double ht, double lt, double hc, double lc)
    {
        Assert.NotNull(new TreeSpec(ht, lt, hc, lc).WhyInvalid());
        Assert.False(new TreeIdentity(Guid.NewGuid(), new TreeSpec(ht, lt, hc, lc)).IsValid);
    }

    // ---------------------------------------------------------------- sol

    /// <summary>Declinação nos solstícios e no equinócio de 2026 (20/03 14:46 UTC).</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void DeclinacaoNosSolsticiosENoEquinocio()
    {
        Assert.Equal(23.44, SolarCalculator.Compute(0, 0, new DateTime(2026, 6, 21, 12, 0, 0), 0).DeclinationDegrees, 1);
        Assert.Equal(-23.44, SolarCalculator.Compute(0, 0, new DateTime(2026, 12, 21, 12, 0, 0), 0).DeclinationDegrees, 1);
        Assert.InRange(SolarCalculator.Compute(0, 0, new DateTime(2026, 3, 20, 14, 46, 0), 0).DeclinationDegrees, -0.02, 0.02);
    }

    /// <summary>A equação do tempo nos extremos do ano: cerca de −14,2 min em 11/02 e +16,4 min em 03/11.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void EquacaoDoTempoNosExtremos()
    {
        Assert.InRange(SolarCalculator.Compute(0, 0, new DateTime(2026, 2, 11, 12, 0, 0), 0).EquationOfTimeMinutes, -14.5, -13.9);
        Assert.InRange(SolarCalculator.Compute(0, 0, new DateTime(2026, 11, 3, 12, 0, 0), 0).EquationOfTimeMinutes, 16.1, 16.7);
    }

    /// <summary>
    /// No meio-dia solar, a elevação é 90° − |latitude − declinação|. Em
    /// Itatiba (23° S) no inverno o sol fica ao norte (azimute perto de 0°);
    /// a 50° N no verão, ao sul (perto de 180°).
    /// </summary>
    [Theory]
    [Trait("Etapa", "9")]
    [InlineData(-23.0, -46.8, -3.0, 6, 21)]
    [InlineData(-23.0, -46.8, -3.0, 12, 21)]
    [InlineData(50.0, 8.0, 2.0, 6, 21)]
    [InlineData(0.0, 0.0, 0.0, 3, 20)]
    public void MeioDiaSolar(double lat, double lon, double fuso, int mes, int dia)
    {
        SunPosition? maior = null;
        DateTime quando = default;

        for (var m = 9 * 60; m <= 15 * 60; m++)
        {
            var t = new DateTime(2026, mes, dia).AddMinutes(m);
            var s = SolarCalculator.Compute(lat, lon, t, fuso);
            if (maior is null || s.ElevationDegrees > maior.ElevationDegrees) (maior, quando) = (s, t);
        }

        var esperada = 90 - Math.Abs(lat - maior!.DeclinationDegrees);
        Assert.Equal(esperada, maior.ElevationDegrees, 0.1);

        if (lat - maior.DeclinationDegrees < -1) Assert.True(maior.AzimuthDegrees < 5 || maior.AzimuthDegrees > 355, $"azimute {maior.AzimuthDegrees:0.0} às {quando:HH:mm}");
        if (lat - maior.DeclinationDegrees > 1) Assert.InRange(maior.AzimuthDegrees, 175, 185);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ManhaALesteTardeAOesteNoiteAbaixoDoHorizonte()
    {
        var manha = SolarCalculator.Compute(-23, -46.8, new DateTime(2026, 9, 23, 8, 0, 0), -3);
        var tarde = SolarCalculator.Compute(-23, -46.8, new DateTime(2026, 9, 23, 16, 0, 0), -3);
        var noite = SolarCalculator.Compute(-23, -46.8, new DateTime(2026, 9, 23, 23, 0, 0), -3);

        Assert.InRange(manha.AzimuthDegrees, 45, 135);
        Assert.InRange(tarde.AzimuthDegrees, 225, 315);
        Assert.True(manha.IsUp && tarde.IsUp);
        Assert.False(noite.IsUp);
    }

    /// <summary>O mesmo instante em fusos diferentes dá o mesmo sol.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void OFusoSoMudaORelogio()
    {
        var a = SolarCalculator.Compute(-23, -46.8, new DateTime(2026, 1, 15, 10, 0, 0), -3);
        var b = SolarCalculator.Compute(-23, -46.8, new DateTime(2026, 1, 15, 13, 0, 0), 0);

        Assert.Equal(a.AzimuthDegrees, b.AzimuthDegrees, 9);
        Assert.Equal(a.ElevationDegrees, b.ElevationDegrees, 9);
    }

    // ------------------------------------------------------------- sombra

    /// <summary>Sol ao sul a 45°: a sombra cai ao norte, do comprimento da altura.</summary>
    private static readonly (double X, double Y, double Z) SolAoSul45 = (0, -Math.Sqrt(0.5), Math.Sqrt(0.5));

    private static readonly ShadowCylinder Poste = new(0, 0, 1, 0, 10);

    [Fact]
    [Trait("Etapa", "9")]
    public void OCilindroBloqueiaQuemEstaNaSombraDele()
    {
        Assert.True(Shading.Blocks(Poste, new Point3(0, 5, 0), SolAoSul45));
        Assert.True(Shading.Blocks(Poste, new Point3(0.9, 10.3, 0), SolAoSul45));   // a sombra vai até 10 + √(1 − 0,81) = 10,44
        Assert.False(Shading.Blocks(Poste, new Point3(0.9, 10.6, 0), SolAoSul45));
        Assert.False(Shading.Blocks(Poste, new Point3(0, 12, 0), SolAoSul45));
        Assert.False(Shading.Blocks(Poste, new Point3(0, -5, 0), SolAoSul45));
        Assert.False(Shading.Blocks(Poste, new Point3(2.5, 5, 0), SolAoSul45));

        // Mais alto que o topo na direção do sol: livre.
        Assert.False(Shading.Blocks(Poste, new Point3(0, 5, 6), SolAoSul45));

        // Copa de 3 a 8 m, raio 3: com o sol a 45°, ela faz sombra entre 3 e
        // 8 m ao norte do centro (mais o raio); bem longe, não.
        var copa = new ShadowCylinder(0, 0, 3, 3, 8);
        Assert.True(Shading.Blocks(copa, new Point3(0, 8, 0), SolAoSul45));
        Assert.False(Shading.Blocks(copa, new Point3(0, 20, 0), SolAoSul45));

        // Sol a pino: só o que está debaixo do cilindro.
        Assert.True(Shading.Blocks(copa, new Point3(1, 1, 0), (0, 0, 1)));
        Assert.False(Shading.Blocks(copa, new Point3(4, 0, 0), (0, 0, 1)));
        Assert.False(Shading.Blocks(copa, new Point3(1, 1, 9), (0, 0, 1)));
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void FracaoDaFace()
    {
        IReadOnlyList<Point3> Quadrado(double x0, double y0, double lado) =>
            [new(x0, y0, 0), new(x0 + lado, y0, 0), new(x0 + lado, y0 + lado, 0), new(x0, y0 + lado, 0)];

        Assert.Equal(1.0, Shading.FaceFraction(Quadrado(-0.5, 4, 1), [Poste], SolAoSul45));
        Assert.Equal(0.0, Shading.FaceFraction(Quadrado(5, 4, 1), [Poste], SolAoSul45));

        // Metade da face para fora da sombra (x de 0 a 2, a sombra vai até x = 1).
        Assert.Equal(0.5, Shading.FaceFraction(Quadrado(0, 4, 2), [Poste], SolAoSul45), 2);

        // Sol posto: nada.
        Assert.Equal(0.0, Shading.FaceFraction(Quadrado(-0.5, 4, 1), [Poste], (0, -1, 0)));

        var fracoes = Shading.Fractions([Quadrado(-0.5, 4, 1), Quadrado(50, 50, 1), Quadrado(0, 4, 2)], [Poste], SolAoSul45);
        Assert.Equal(1.0, fracoes[0]);
        Assert.Equal(0.0, fracoes[1]);
        Assert.Equal(0.5, fracoes[2], 2);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ContornoDaSombraNoChaoPlano()
    {
        var contorno = Shading.ShadowOutline(Poste, SolAoSul45, (_, _) => 0.0);

        Assert.InRange(contorno.Max(p => p.Y), 10.95, 11.0);
        Assert.InRange(contorno.Min(p => p.Y), -1.0, -0.95);
        Assert.InRange(contorno.Max(p => p.X), 0.95, 1.0);
        Assert.All(contorno, p => Assert.Equal(0, p.Z));
    }

    /// <summary>No terreno que sobe para o norte (10%), a sombra encurta e cada vértice fica na cota do terreno.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void ContornoDaSombraAcompanhaOTerreno()
    {
        double? Rampa(double x, double y) => 0.1 * y;

        var contorno = Shading.ShadowOutline(Poste, SolAoSul45, Rampa);

        // A borda norte do topo (y = 1, z = 10) chega ao chão em 1 + s, com
        // 10 − s = 0,1 (1 + s): s = 9, y = 10.
        Assert.InRange(contorno.Max(p => p.Y), 9.95, 10.01);
        Assert.All(contorno, p => Assert.Equal(0.1 * p.Y, p.Z, 6));
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void SolBaixoNaoDesenhaSombra() =>
        Assert.Empty(Shading.ShadowOutline(Poste, (0, -1, 0.01), (_, _) => 0.0));

    // ------------------------------------------------------------ período

    [Fact]
    [Trait("Etapa", "9")]
    public void InstantesDoPeriodo()
    {
        var dia = new DateOnly(2026, 6, 21);

        Assert.Equal(25, Shading.Instants(dia, dia, new TimeOnly(6, 0), new TimeOnly(18, 0), TimeSpan.FromMinutes(30)).Count());
        Assert.Single(Shading.Instants(dia, dia, new TimeOnly(9, 0), new TimeOnly(9, 0), TimeSpan.FromMinutes(30)));
        Assert.Equal(30, Shading.Instants(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), new TimeOnly(9, 0), new TimeOnly(9, 0), TimeSpan.FromHours(1)).Count());
        Assert.Throws<ArgumentException>(() => Shading.Instants(dia, dia.AddDays(-1), new TimeOnly(9, 0), new TimeOnly(9, 0), TimeSpan.FromHours(1)));
    }

    /// <summary>
    /// Uma árvore de 8 m em Itatiba e uma face a 6 m ao sul dela: no
    /// inverno o sol fica ao norte e baixo, a sombra cai ao sul e pega a
    /// face; no pior caso do dia a face fica sombreada, e quando é dito. A
    /// face longe nunca pega.
    /// </summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void PiorCasoDoDia()
    {
        var arvore = new TreeSpec(3, 0.4, 5, 4).Cylinders(0, 0, 0);
        IReadOnlyList<Point3> perto = [new(-1, -7, 0.5), new(1, -7, 0.5), new(1, -6, 1.0), new(-1, -6, 1.0)];
        IReadOnlyList<Point3> longe = [new(100, -7, 0.5), new(102, -7, 0.5), new(102, -6, 1.0), new(100, -6, 1.0)];

        var dia = new DateOnly(2026, 6, 21);
        var r = Shading.Worst([perto, longe], arvore, -23.0, -46.8, -3, Shading.Instants(dia, dia, new TimeOnly(6, 0), new TimeOnly(18, 0), TimeSpan.FromMinutes(15)));

        Assert.True(r.Fractions[0] > 0.5, $"fração {r.Fractions[0]:0.00}");
        Assert.NotNull(r.When[0]);
        Assert.Equal(0, r.Fractions[1]);
        Assert.Null(r.When[1]);
        Assert.Equal(1, r.ShadedCount);
        Assert.NotNull(r.WorstInstant);
        Assert.Equal(49, r.Instants);
        Assert.InRange(r.InstantsWithSun, 30, 45);
    }

    /// <summary>No verão o sol do meio-dia em Itatiba fica quase a pino e ao sul: a mesma face, ao sul da árvore, fica livre ao meio-dia.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void NoVeraoAoMeioDiaAFaceAoSulFicaLivre()
    {
        var arvore = new TreeSpec(3, 0.4, 5, 4).Cylinders(0, 0, 0);
        IReadOnlyList<Point3> perto = [new(-1, -7, 0.5), new(1, -7, 0.5), new(1, -6, 1.0), new(-1, -6, 1.0)];

        var sol = SolarCalculator.Compute(-23.0, -46.8, new DateTime(2026, 12, 21, 12, 10, 0), -3);
        Assert.Equal(0.0, Shading.FaceFraction(perto, arvore, sol.Direction));
    }
}
