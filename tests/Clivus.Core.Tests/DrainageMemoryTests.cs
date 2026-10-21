using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Passo 31.1 a 31.3: a memória inteira contra o memorial de Valparaiso (canaletas 1 e 2).</summary>
public class DrainageMemoryTests
{
    /// <summary>
    /// Um fundo de canaleta com <paramref name="comprimento"/> m que desce
    /// <paramref name="imax"/> até 100 m e <paramref name="imin"/> depois:
    /// nos trechos de 20 m, a mínima e a máxima do memorial.
    /// </summary>
    private static ChannelProfileResult Perfil(double comprimento, double imin, double imax, double altura)
    {
        var pontos = new List<ProfilePoint>();
        for (var d = 0.0; d < comprimento; d += 5) pontos.Add(new ProfilePoint(d, d, 0, Z(d), true));
        pontos.Add(new ProfilePoint(comprimento, comprimento, 0, Z(comprimento), true));
        return ChannelProfile.Build(new TerrainProfile(pontos, false, 0), altura, ChannelBottomMode.FollowTerrain, 0.005, 20);

        double Z(double d) => 100 - imax * Math.Min(d, 100) - imin * Math.Max(0, d - 100);
    }

    private static readonly ChannelSection Trap = ChannelSection.Defaults[0];

    [Fact]
    [Trait("Etapa", "31")]
    public void Canaleta1DoMemorial()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "Canaleta 1", 13289, 179.85, 5.51, false);
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "Canaleta 1", Trap, Perfil(269.35, 0.005, 0.0434, 0.35), bacia));

        // Tabela 1.
        Assert.Equal(7.05, c.KerbyTc, 2);
        Assert.Equal(4.07, c.KirpichTc, 2);
        Assert.Equal(5.00, c.Tc, 2);
        Assert.Equal(0.60, c.SurfaceVelocity, 2);

        // Tabela 5.
        Assert.Equal(186.73, c.Intensity, 2);
        Assert.Equal(103.39, c.FlowLitersPerSecond, 2);

        // Tabela 8.
        Assert.Equal(0.005, c.MinSlope, 9);
        Assert.Equal(0.0434, c.MaxSlope, 9);
        Assert.Equal(0.207, c.MaxDepth, 3);
        Assert.Equal(2.58, c.MaxVelocity, 2);
        Assert.Equal(0.10, c.Freeboard);

        // Tabela 9 e 11.
        Assert.Equal(0.35, c.SuggestedHeight, 9);
        Assert.Equal(0.72, c.Dissipator.L1, 9);
        Assert.Equal("DES 73-219", c.DissipatorModel);

        // A 0,35 m adotada cabe: nada fora da regra.
        Assert.Empty(c.Alerts);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void Canaleta2DoMemorialComAAlturaQueEleAdotou()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "Canaleta 2", 30120, 166.23, 8.70, false);
        var secao = Trap with { Height = 0.50 };
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "Canaleta 2", secao, Perfil(273.38, 0.005, 0.0472, 0.50), bacia));

        Assert.Equal(234.35, c.FlowLitersPerSecond, 2);
        Assert.Equal(0.327, c.MaxDepth, 3);
        Assert.Equal(3.33, c.MaxVelocity, 2);
        Assert.Equal(0.45, c.SuggestedHeight, 9);
        Assert.Equal(0.90, c.Dissipator.L1, 9);
        Assert.Empty(c.Alerts);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void SecaoPequenaDemaisEAvisadaComASugestao()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "C", 30120, 166.23, 8.70, false);
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", Trap with { Height = 0.30 }, Perfil(273.38, 0.005, 0.0472, 0.30), bacia));

        Assert.Contains(c.Alerts, a => a.Contains("passa da altura da seção", StringComparison.Ordinal) && a.Contains("0,45", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void SemBaciaFicaSemVazaoEAvisa()
    {
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", Trap, Perfil(100, 0.01, 0.02, 0.35), null));

        Assert.Equal(0, c.Flow);
        Assert.Contains(c.Alerts, a => a.Contains("sem bacia", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void FundoInvertidoUsaAMinimaDaNormaEAvisa()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "C", 13289, 179.85, 5.51, false);
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", Trap, Perfil(200, -0.01, 0.02, 0.35), bacia));

        Assert.Equal(0.005, c.SlopeForDepth, 9);
        Assert.Equal(0.207, c.MaxDepth, 3);
        Assert.Contains(c.Alerts, a => a.Contains("plano ou subindo", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void VelocidadeAltaEBaixaSaoAvisadas()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "C", 60000, 200, 30, false);
        var c = DrainageMemory.Calculate(new HydrologyParameters(), new ChannelInput(Guid.NewGuid(), "C", Trap with { Height = 1 }, Perfil(300, 0.0001, 0.30, 1), bacia));

        Assert.Contains(c.Alerts, a => a.Contains("passa da do revestimento", StringComparison.Ordinal));
        Assert.Contains(c.Alerts, a => a.Contains("autolimpeza", StringComparison.Ordinal));
        Assert.Contains(c.Alerts, a => a.Contains("abaixo da norma", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void AsTabelasTemAsDoMemorialEOsTotais()
    {
        var bacia = new BasinEntry(Guid.NewGuid(), "Canaleta 1", 13289, 179.85, 5.51, false);
        var memoria = DrainageMemory.Build(new HydrologyParameters(),
            [new ChannelInput(Guid.NewGuid(), "Canaleta 1", Trap, Perfil(269.35, 0.005, 0.0434, 0.35), bacia, RoadCrossings: 1)]);

        var tabelas = DrainageMemory.Tables(memoria);
        Assert.Equal(["Parâmetros", "Bacias", "Chuva", "Vazão", "Canaletas", "Dimensões", "Dissipadores", "Transposições", "Quantitativos", "Alertas"], tabelas.Select(t => t.Title));

        // Tabela 3 inteira: T 25, 5 min = 186,73.
        var chuva = tabelas.Single(t => t.Title == "Chuva");
        Assert.Equal(186.73, (double)chuva.Rows[1][1]!, 2);
        Assert.Equal(12.01, (double)chuva.Rows[3][10]!, 2);

        var transposicao = tabelas.Single(t => t.Title == "Transposições");
        Assert.Equal("TSS 120", Assert.Single(transposicao.Rows)[3]);

        // O Excel monta com as tabelas.
        var planilha = new XlsxWriter();
        foreach (var t in tabelas) QuantityReport.Tabela(planilha, t);
        Assert.True(planilha.ToBytes().Length > 1000);
    }

    [Fact]
    [Trait("Etapa", "31")]
    public void ParametroRuimERecusado()
    {
        Assert.Throws<ArgumentException>(() => DrainageMemory.Build(new HydrologyParameters { RunoffC = 0 }, []));
    }
}
