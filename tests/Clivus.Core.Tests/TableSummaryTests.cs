using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O resumo da mesa (42.9, Renan, 31/10/2026: "eu queria ver a inclinação de
/// uma mesa, o tilt, e não sabia como ver"): o tilt e o rumo pelo plano dos
/// módulos desenhados, o azimute do PVsyst pelo hemisfério, o giro ao longo
/// da mesa, o terreno sob ela e os números da mesa.
/// </summary>
public class TableSummaryTests
{
    /// <summary>
    /// Uma mesa 14 × 2 de faces como o desenho grava: o comprimento no rumo
    /// <paramref name="rumo"/> (bússola), com o giro subindo, e os módulos
    /// subindo a rampa à esquerda do comprimento, inclinados <paramref name="tilt"/>.
    /// </summary>
    private static List<PvModuleFace> Mesa(double tilt, double rumo, double giro = 0)
    {
        var r = rumo * Math.PI / 180;
        var g = giro * Math.PI / 180;
        var t = tilt * Math.PI / 180;
        var u = (X: Math.Sin(r) * Math.Cos(g), Y: Math.Cos(r) * Math.Cos(g), Z: Math.Sin(g));
        var esquerda = (X: -Math.Cos(r), Y: Math.Sin(r));
        var v = (X: esquerda.X * Math.Cos(t), Y: esquerda.Y * Math.Cos(t), Z: Math.Sin(t));
        var vu = v.X * u.X + v.Y * u.Y + v.Z * u.Z;
        v = (v.X - vu * u.X, v.Y - vu * u.Y, v.Z - vu * u.Z);
        var tam = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        v = (v.X / tam, v.Y / tam, v.Z / tam);

        Point3 Em(double s, double q) => new(500 + s * u.X + q * v.X, 800 + s * u.Y + q * v.Y, 100 + s * u.Z + q * v.Z);

        var faces = new List<PvModuleFace>();
        for (var c = 0; c < 14; c++)
            for (var f = 0; f < 2; f++)
            {
                var s0 = c * 1.323;
                var q0 = f * 2.404;
                faces.Add(new PvModuleFace(c, f, [Em(s0, q0), Em(s0 + 1.303, q0), Em(s0 + 1.303, q0 + 2.384), Em(s0, q0 + 2.384)]));
            }

        return faces;
    }

    private static TableSummaryInput Entrada(List<PvModuleFace> faces, double? latitude = 9, Func<double, double, double?>? terreno = null,
        IReadOnlyList<PillarIdentity>? pilares = null, double? potencia = null) => new(
        Guid.NewGuid(), "F16.4", "Biposte 2P14 (0,80 m livre)", "Biposte", new SolarModule("JA", "JAM66D46-720", 720, 2.384, 1.303, 0.035), potencia,
        faces, 15 * Math.PI / 180, pilares ?? [], false, null, false, null, latitude, terreno);

    /// <summary>
    /// A mesa com o comprimento para o leste e os módulos subindo para o norte
    /// olha para o sul, com o tilt dela; no PVsyst, 0° no norte (Panamá) e
    /// 180° no sul (Brasil).
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OTiltEORumoSaemDoPlanoDosModulos()
    {
        var norte = TableSummaries.Build(Entrada(Mesa(15, 90)));

        Assert.Equal(15, norte.TiltDegrees!.Value, 6);
        Assert.Equal(180, norte.FacingDegrees!.Value, 6);
        Assert.Equal(0, norte.PvsystAzimuthDegrees!.Value, 6);
        Assert.Equal(15, norte.DesignTiltDegrees, 6);
        Assert.Equal(0, norte.AxisSlopePercent!.Value, 6);
        Assert.Equal((14, 2, 28), (norte.Columns, norte.Rows, norte.Modules));
        Assert.Equal(14 * 1.303 + 13 * 0.02, norte.Length!.Value, 6);

        var sul = TableSummaries.Build(Entrada(Mesa(15, 90), latitude: -23));
        Assert.Equal(180, sul.PvsystAzimuthDegrees!.Value, 6);
    }

    /// <summary>A mesa que sobe 3° para o leste ao longo dela desce 5,24 % para o oeste.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OGiroAoLongoDaMesaEParaQueLadoDesce()
    {
        var r = TableSummaries.Build(Entrada(Mesa(15, 90, giro: 3)));

        Assert.Equal(Math.Tan(3 * Math.PI / 180) * 100, r.AxisSlopePercent!.Value, 6);
        Assert.Equal(270, r.AxisDownhillDegrees!.Value, 6);
        Assert.Equal("oeste", TableSummaries.Cardinal(r.AxisDownhillDegrees.Value));
    }

    /// <summary>
    /// O terreno que sobe 10 % para o leste: 10 % de inclinação descendo para
    /// o oeste, 10 % ao longo da mesa (que corre para o leste) e plano na largura.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OTerrenoSobAMesaEOPlanoDosPontosDele()
    {
        var r = TableSummaries.Build(Entrada(Mesa(15, 90), terreno: (x, y) => 98 + 0.1 * (x - 500)));

        Assert.Equal(10, r.TerrainSlopePercent!.Value, 6);
        Assert.Equal(270, r.TerrainDownhillDegrees!.Value, 6);
        Assert.Equal(10, r.TerrainAlongPercent!.Value, 6);
        Assert.Equal(270, r.TerrainAlongDownhillDegrees!.Value, 6);
        Assert.Equal(0, r.TerrainAcrossRisePercent!.Value, 6);

        // Fora do terreno (nenhum ponto), não há conta.
        Assert.Null(TableSummaries.Build(Entrada(Mesa(15, 90), terreno: (_, _) => null)).TerrainSlopePercent);
    }

    /// <summary>Os pilares: quantos, da frente e de trás, e o menor e o maior de cada medida.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OsPilaresVaoEmFaixas()
    {
        var mesa = Guid.NewGuid();
        PillarIdentity Pilar(int n, double livre, double comprimento, PillarLine linha, string? problema = null) =>
            new(Guid.NewGuid(), mesa, n, n * 3.0, comprimento, 2.2, livre, problema, 100, 0.8, 2.4, linha);

        var r = TableSummaries.Build(Entrada(Mesa(15, 90), pilares:
        [
            Pilar(1, 1.2, 3.4, PillarLine.Front), Pilar(2, 1.5, 3.7, PillarLine.Front),
            Pilar(3, 2.6, 4.8, PillarLine.Rear), Pilar(4, 2.9, 5.1, PillarLine.Rear, "passa do máximo"),
        ]));

        Assert.Equal((4, 2, 2, 1), (r.Pillars, r.FrontPillars, r.RearPillars, r.PillarProblems));
        Assert.Equal(new Span(1.2, 2.9), r.FreeHeight);
        Assert.Equal(new Span(3.4, 5.1), r.PillarLength);
        Assert.Equal(new Span(2.2, 2.2), r.Embedment);
    }

    /// <summary>A potência é módulos × a da mesa (a gravada, se houver; senão a do módulo).</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void APotenciaEModulosVezesADaMesa()
    {
        Assert.Equal(20.16, TableSummaries.Build(Entrada(Mesa(15, 90))).PowerKwp!.Value, 6);
        Assert.Equal(18.2, TableSummaries.Build(Entrada(Mesa(15, 90), potencia: 650)).PowerKwp!.Value, 6);
    }

    /// <summary>O azimute do PVsyst pelo help dele: no norte 0 é o sul; no sul 0 é o norte; + para o oeste.</summary>
    [Theory]
    [Trait("Etapa", "42")]
    [InlineData(180, false, 0)]
    [InlineData(270, false, 90)]
    [InlineData(90, false, -90)]
    [InlineData(0, false, 180)]
    [InlineData(0, true, 0)]
    [InlineData(270, true, 90)]
    [InlineData(90, true, -90)]
    [InlineData(180, true, 180)]
    public void OAzimuteDoPVsyst(double rumo, bool sul, double esperado) =>
        Assert.Equal(esperado, TableSummaries.PvsystAzimuth(rumo, sul), 9);

    /// <summary>
    /// A mesa voltada para o norte no Brasil (o comprimento para o oeste, os
    /// módulos subindo para o sul): "norte (0°)", nunca "360°", e 0,0° no
    /// PVsyst, nunca "-0,0" (revisão do 42.9); mesa nivelada e terreno plano
    /// dizem isso, e a pendente diz "PENDENTE" como o resto do Clivus.
    /// </summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void ANorteNoBrasilENivelada()
    {
        var entrada = Entrada(Mesa(15, 270), latitude: -23, terreno: (_, _) => 100) with { Dirty = true, DirtyReason = "movida" };
        var r = TableSummaries.Build(entrada);

        Assert.Equal(0, TableSummaries.PvsystAzimuth(r.FacingDegrees!.Value, true), 6);
        var linhas = r.Sections().SelectMany(s => s.Lines).ToList();
        string Valor(string rotulo, int qual = 0) => linhas.Where(l => l.Label == rotulo).ElementAt(qual).Value;

        Assert.Contains("(0°)", Valor("Voltada para"));
        Assert.DoesNotContain("360", Valor("Voltada para"));
        Assert.DoesNotContain("-0", Valor("Azimute no PVsyst"));
        Assert.Equal("nivelada", Valor("Ao longo da mesa"));
        Assert.Equal("plano", Valor("Inclinação"));
        Assert.Contains("PENDENTE", Valor("Estado"));
    }

    /// <summary>O terreno que sobe para o norte sob a mesa voltada para o sul sobe para a ponta alta dos módulos.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void NaLarguraOTerrenoSobeParaAPontaAlta()
    {
        var r = TableSummaries.Build(Entrada(Mesa(15, 90), terreno: (_, y) => 100 + 0.1 * (y - 800)));

        Assert.Equal(10, r.TerrainAcrossRisePercent!.Value, 6);
        Assert.StartsWith("sobe 10", r.Sections().SelectMany(s => s.Lines).Single(l => l.Label == "Na largura").Value);
    }

    /// <summary>O texto do resumo tem o letreiro e as seções, com a orientação primeiro.</summary>
    [Fact]
    [Trait("Etapa", "42")]
    public void OTextoTemAsSecoesComAOrientacaoPrimeiro()
    {
        var r = TableSummaries.Build(Entrada(Mesa(15, 90)));

        Assert.Equal(["Orientação", "Terreno sob a mesa", "Mesa", "Módulos", "Pilares"], r.Sections().Select(s => s.Title));
        Assert.Contains("F16.4", r.Describe());
        Assert.Contains("Tilt dos módulos", r.Describe());
    }
}
