namespace Clivus.Core.Tests;

/// <summary>A fila contínua do carport (34.3): N vãos, N + 1 pórticos, cada um no seu chão.</summary>
public class CarportRowTests
{
    private static CarportProfile Padrao => CarportProfile.Reference;
    private static double Vao => Padrao.Array.BayWidth;

    private static Func<double, double, double?> Plano(double z = 0) => (_, _) => z;

    private static CarportRowResult Fila(double comprimento, int? vaos = null, Func<double, double, double?>? chao = null, bool caiADireita = true)
    {
        var f = CarportRow.Build(Padrao, 100, 200, 100 + comprimento, 200, caiADireita, vaos, chao ?? Plano(), out var motivo);
        Assert.True(f is not null, motivo);
        return f!;
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void TresVaosTemQuatroPorticos()
    {
        var f = Fila(3 * Vao);

        Assert.Equal(4, f.Portals.Count);
        Assert.Equal(3, f.Bays.Count);
        Assert.Equal(45, f.Bays.Sum(b => b.Modules.Count));
        Assert.Equal(8, f.Portals.Sum(p => p.Bars.Count(b => b.Part == CarportPart.Pillar)));
        Assert.Equal(3 * Vao, f.Length, 9);
        Assert.Empty(f.Warnings);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OsPorticosFicamNasDivisasDosVaos()
    {
        var f = Fila(3 * Vao);

        for (var k = 0; k < 4; k++)
        {
            Assert.Equal(100 + k * Vao, f.Portals[k].Center.X, 9);
            Assert.Equal(200, f.Portals[k].Center.Y, 9);
        }

        // O vão começa e termina no eixo dos pórticos: o pórtico do meio é um só para os dois vãos.
        Assert.Equal(f.Portals[1].Center.X, f.Bays[0].Modules.Max(m => m.Corners.Max(c => c.X)), 6);
        Assert.Equal(f.Portals[1].Center.X, f.Bays[1].Modules.Min(m => m.Corners.Min(c => c.X)), 6);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void SemNumeroDeVaosEncheOEixo()
    {
        Assert.Equal(3, Fila(20).Bays.Count);          // 3 × 5,67 = 17,01; o quarto não cabe
        Assert.Equal(3, Fila(3 * Vao).Bays.Count);     // a medida exata
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void ComNumeroDeVaosOEixoSoDaOSentido()
    {
        var f = Fila(1, vaos: 2);

        Assert.Equal(2, f.Bays.Count);
        Assert.Equal(2 * Vao, f.Length, 9);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void EixoMaisCurtoQueUmVaoNaoSai()
    {
        Assert.Null(CarportRow.Build(Padrao, 0, 0, 4, 0, true, null, Plano(), out var motivo));
        Assert.Contains("mais curto que um vão", motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void ClicarDuasVezesNoMesmoLugarNaoSai()
    {
        Assert.Null(CarportRow.Build(Padrao, 5, 5, 5, 5, true, 3, Plano(), out var motivo));
        Assert.NotNull(motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void PorticoForaDoTerrenoDizQualE()
    {
        // O terreno acaba em X 110: o terceiro pórtico (X 111,34) cai fora.
        var f = CarportRow.Build(Padrao, 100, 200, 120, 200, true, null, (x, _) => x > 110 ? null : 0, out var motivo);

        Assert.Null(f);
        Assert.Contains("pórtico 3 de 4", motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void CadaPorticoAssentaNoSeuChao()
    {
        var f = Fila(3 * Vao, chao: (x, _) => 0.03 * (x - 100) + 500);

        for (var k = 0; k < 4; k++)
            Assert.Equal(Math.Max(f.Portals[k].HighBase.Z, f.Portals[k].LowBase.Z) + Padrao.ClearHeight, f.Portals[k].LowEdgeZ, 9);

        Assert.True(f.Portals[3].LowEdgeZ > f.Portals[0].LowEdgeZ + 0.4);
        Assert.Empty(f.Warnings);                      // 3 % não avisa
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void DesnivelGrandeAoLongoDaFilaAvisa()
    {
        var f = Fila(3 * Vao, chao: (x, _) => 0.08 * (x - 100));

        Assert.Equal(3, f.Warnings.Count);
        Assert.Contains("pórticos 1 e 2", f.Warnings[0]);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void InverterOEixoETrocarOLadoDaAguaDaAMesmaCobertura()
    {
        var ida = Fila(2 * Vao, caiADireita: true);
        var volta = CarportRow.Build(Padrao, 100 + 2 * Vao, 200, 100, 200, false, null, Plano(), out _)!;

        Assert.Equal(ida.Portals[0].LowBase.Y, volta.Portals[^1].LowBase.Y, 9);
        Assert.Equal(ida.Portals[0].HighBase.Y, volta.Portals[^1].HighBase.Y, 9);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void FilaCompridaDemaisERecusada()
    {
        Assert.Null(CarportRow.Build(Padrao, 0, 0, 1, 0, true, CarportRow.MaiorNumeroDeVaos + 1, Plano(), out var motivo));
        Assert.NotNull(motivo);
    }

    // ---------------------------------------------------------- revisão do 34

    [Fact]
    [Trait("Etapa", "34")]
    public void CincoPorCentoExatosNaoAvisam()
    {
        Assert.Empty(Fila(3 * Vao, chao: (x, _) => 0.05 * (x - 100)).Warnings);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void DesnivelGrandeEntreAsBasesAvisaComOComprimentoDoPilar()
    {
        // 30 % na descida: as bases ficam a ~0,73 m uma da outra em altura.
        var f = Fila(2 * Vao, chao: (_, y) => 0.30 * y);

        Assert.Equal(3, f.Warnings.Count);
        Assert.Contains("entre as bases", f.Warnings[0]);
    }
}
