using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Passos 27.3 e 28.3/28.4: trechos de declividade, fundo da canaleta e o gráfico do perfil.</summary>
public class DrainageProfileTests
{
    /// <summary>Um perfil de terreno a partir de (distância, cota).</summary>
    private static TerrainProfile Perfil(params (double D, double Z)[] pontos) =>
        new(pontos.Select(p => new ProfilePoint(p.D, p.D, 0, p.Z, true)).ToList(), false, 0);

    [Fact]
    [Trait("Etapa", "27")]
    public void TrechosDe20MetrosComORestoCurtoJuntoAoAnterior()
    {
        var trechos = ProfileSegments.Split(d => 100 - 0.05 * d, 65, 20);

        // 0–20, 20–40, 40–65 (o resto de 5 m entra no último).
        Assert.Equal(3, trechos.Count);
        Assert.Equal(40, trechos[^1].From);
        Assert.Equal(65, trechos[^1].To);
        Assert.All(trechos, t => Assert.Equal(0.05, t.Slope, 9));
        Assert.All(trechos, t => Assert.Equal(5, t.Percent, 9));
        Assert.All(trechos, t => Assert.Equal(Math.Atan(0.05) * 180 / Math.PI, t.Degrees, 9));
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void TrechoQueSobeTemDeclividadeNegativa()
    {
        var t = new SlopeSegment(0, 10, 50, 50.3);

        Assert.Equal(-0.03, t.Slope, 9);
        Assert.True(t.Degrees < 0);
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void BordaNoTerrenoCopiaOTerrenoEAvisaOTrechoBaixoEOInvertido()
    {
        // Desce 2 %, fica plano, sobe 1 %, desce 0,3 %.
        var terreno = Perfil((0, 100), (20, 99.6), (40, 99.6), (60, 99.8), (80, 99.74));
        var canaleta = ChannelProfile.Build(terreno, 0.35, ChannelBottomMode.FollowTerrain, 0.005, 20);

        Assert.All(canaleta.Points, p => Assert.Equal(0.35, p.Depth, 9));
        Assert.Equal(4, canaleta.Segments.Count);
        Assert.Equal(0.02, canaleta.MaxSlope, 9);
        Assert.Equal(-0.01, canaleta.MinSlope, 9);
        Assert.Single(canaleta.LowSegments);
        Assert.Equal(2, canaleta.InvertedSegments.Count());
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void ModoDeclividadeMinimaAprofundaENuncaSobe()
    {
        var terreno = Perfil((0, 100), (20, 99.6), (40, 99.6), (60, 99.8), (80, 99.74));
        var canaleta = ChannelProfile.Build(terreno, 0.35, ChannelBottomMode.MinimumSlope, 0.005, 20);

        for (var i = 1; i < canaleta.Points.Count; i++)
        {
            var (a, b) = (canaleta.Points[i - 1], canaleta.Points[i]);
            Assert.True(a.Bottom - b.Bottom >= 0.005 * (b.Distance - a.Distance) - 1e-9, $"fundo desce menos que a mínima entre {a.Distance} e {b.Distance}");
            Assert.True(b.Depth >= 0.35 - 1e-9, "a canaleta ficou mais rasa que a altura");
        }

        Assert.Empty(canaleta.LowSegments);
        Assert.Empty(canaleta.InvertedSegments);
        Assert.True(canaleta.MaxDepth > 0.35);

        // Onde o terreno desce mais que a mínima, o fundo volta a acompanhar.
        Assert.Equal(0.35, canaleta.Points[1].Depth, 9);
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void ExageroAutomaticoEPassosBonitos()
    {
        Assert.Equal(10, ProfileChart.AutoExaggeration(200, 5));
        Assert.Equal(1, ProfileChart.AutoExaggeration(50, 40));
        Assert.Equal(50, ProfileChart.AutoExaggeration(1000, 0.1));

        Assert.Equal(1, ProfileChart.NiceStep(0.8));
        Assert.Equal(25, ProfileChart.NiceStep(21));
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void OGraficoTemMolduraSeriesBandaETitulo()
    {
        var entrada = new ChartInput
        {
            Title = "Linha 1",
            Terrain = [(0, 100), (50, 97.5), (100, 95)],
            Segments = ProfileSegments.Split(d => 100 - 0.05 * d, 100, 20),
            VerticalExaggeration = 5,
        };

        var g = ProfileChart.Build(entrada);

        Assert.Equal(5, g.Exaggeration);
        Assert.Equal(100, g.Width);
        Assert.Single(g.Lines, l => l.Kind == ChartLineKind.Frame);
        Assert.Single(g.Lines, l => l.Kind == ChartLineKind.Terrain);
        Assert.DoesNotContain(g.Lines, l => l.Kind == ChartLineKind.Bottom);

        // O terreno fica dentro da moldura.
        var terreno = g.Lines.Single(l => l.Kind == ChartLineKind.Terrain);
        Assert.All(terreno.Points, p => Assert.InRange(p.Y, 0, g.Height));

        // Cinco trechos, cada um com "5,0% | 2,9°" (cultura do Tr) e uma seta de dois traços.
        var rotulos = g.Texts.Where(t => t.Text.Contains('|')).ToList();
        Assert.Equal(5, rotulos.Count);
        Assert.Equal(10, g.Lines.Count(l => l.Kind == ChartLineKind.Arrow));

        Assert.Contains(g.Texts, t => t.Text == "Linha 1");
        Assert.Contains(g.Texts, t => t.Text.Contains("exagero vertical 5", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "28")]
    public void NoGraficoDaCanaletaOTrechoBaixoGanhaAlerta()
    {
        var terreno = Perfil((0, 100), (20, 99.6), (40, 99.6), (60, 99.8), (80, 99.74));
        var canaleta = ChannelProfile.Build(terreno, 0.35, ChannelBottomMode.FollowTerrain, 0.005, 20);

        var g = ProfileChart.Build(new ChartInput
        {
            Title = "Canaleta 1",
            Terrain = canaleta.Points.Select(p => (p.Distance, p.Terrain)).ToList(),
            Bottom = canaleta.Points.Select(p => (p.Distance, p.Bottom)).ToList(),
            WallHeight = 0.35,
            Segments = canaleta.Segments,
            MinimumSlope = 0.005,
        });

        Assert.Single(g.Lines, l => l.Kind == ChartLineKind.Bottom);
        Assert.Single(g.Lines, l => l.Kind == ChartLineKind.WallTop);
        Assert.Equal(3, g.Lines.Count(l => l.Kind == ChartLineKind.Alert));
        Assert.Contains(g.Texts, t => t.Text == "Fundo (m)");
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void TodoTextoDoGraficoEstaNaHorizontalECabeNaFolha()
    {
        // O ChartText não tem rotação: o tipo garante a regra de 07/10/2026.
        Assert.DoesNotContain(typeof(ChartText).GetProperties(), p => p.Name.Contains("Rotation", StringComparison.OrdinalIgnoreCase));
    }
}
