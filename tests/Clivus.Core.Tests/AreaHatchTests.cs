using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A hachura das áreas (etapa 41): uma cor por área, a cota do hatch (regra
/// sagrada 5: a mais alta do terreno dentro da área e das mesas dela, nunca a
/// de um clique), onde vai o nome e de qual área é cada hachura.
/// </summary>
public class AreaHatchTests
{
    private const double Folga = 0.30;

    private static IReadOnlyList<Point3> Retangulo(double x0, double y0, double x1, double y1, double z = 0) =>
        [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)];

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));
        return new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);
    }

    /// <summary>Uma malha regular de passo <paramref name="passo"/> sobre o retângulo, com a cota da função nos vértices.</summary>
    private static Tin Grade(Func<double, double, double> z, double x0, double y0, double x1, double y1, double passo)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));
        var triangulos = new List<Triangle>();

        for (var x = x0; x < x1 - 1e-9; x += passo)
        {
            for (var y = y0; y < y1 - 1e-9; y += passo)
            {
                triangulos.Add(new Triangle(P(x, y), P(x + passo, y), P(x + passo, y + passo)));
                triangulos.Add(new Triangle(P(x, y), P(x + passo, y + passo), P(x, y + passo)));
            }
        }

        return new Tin(triangulos);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void CadaAreaGanhaUmaCorDiferente()
    {
        var sete = Enumerable.Range(0, 7).Select(AreaHatch.ColorOf).ToList();

        Assert.Equal(7, sete.Distinct().Count());
        Assert.Equal(AreaHatch.ColorOf(0), AreaHatch.ColorOf(InverterColors.Palette.Count));
        Assert.Equal(AreaHatch.ColorOf(InverterColors.Palette.Count - 1), AreaHatch.ColorOf(-1));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void ACotaEAMaisAltaDoTerrenoNaArea()
    {
        // Rampa subindo para leste: o ponto mais alto da área (0..50) está na borda x = 50.
        var terreno = Plano((x, _) => 100 + 0.1 * x);

        Assert.Equal(105 + Folga, AreaHatch.Elevation(terreno, Retangulo(0, 0, 50, 50), [], Folga)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void OMorroNoMeioDaAreaLevantaAHachura()
    {
        // Um morro de 20 m no meio da área: a borda fica em ~100 m, o topo em 120 m.
        var terreno = Grade((x, y) => 100 + 20 * Math.Exp(-((x - 50) * (x - 50) + (y - 50) * (y - 50)) / 400), -50, -50, 150, 150, 10);

        Assert.Equal(120 + Folga, AreaHatch.Elevation(terreno, Retangulo(0, 0, 100, 100), [], Folga)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void OPontoMaisAltoNaBordaEntreVerticesDoTinConta()
    {
        // Um vértice alto (120 m) logo fora da área, em (60; 10): dentro dela
        // o único vértice do TIN está a 100 m e os cantos da área, no máximo a
        // 110 m; o mais alto da área é onde a borda x = 58 cruza a aresta do
        // TIN que sobe para o vértice alto (116 m). Amostrar os cantos
        // deixaria a hachura enterrada.
        var terreno = Grade((x, y) => x == 60 && y == 10 ? 120 : 100, 0, 0, 100, 30, 10);
        var area = Retangulo(42, 5, 58, 15);

        var cantos = area.Select(p => terreno.TryGetZ(p.X, p.Y, out var z) ? z : double.NaN).Max();
        Assert.Equal(110, cantos, 6);

        Assert.Equal(116, AreaHatch.Elevation(terreno, area, [], 0)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AsMesasDaAreaLevantamAHachuraAcimaDelasEAsDeForaNao()
    {
        var terreno = Plano((_, _) => 100);
        var area = Retangulo(0, 0, 100, 100);

        var dentro = Retangulo(10, 10, 20, 14, 103);
        var naDivisa = Retangulo(95, 50, 105, 54, 104);
        var fora = Retangulo(300, 300, 310, 304, 200);

        Assert.Equal(103 + Folga, AreaHatch.Elevation(terreno, area, [dentro, fora], Folga)!.Value, 6);
        Assert.Equal(104 + Folga, AreaHatch.Elevation(terreno, area, [dentro, naDivisa, fora], Folga)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void SemTerrenoACotaVemDosVerticesDaArea()
    {
        IReadOnlyList<Point3> area = [new(0, 0, 98), new(100, 0, 99), new(100, 100, 101), new(0, 100, 100)];

        Assert.Equal(101 + Folga, AreaHatch.Elevation(null, area, [], Folga)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AAreaForaDoTerrenoUsaOsVerticesDela()
    {
        var terreno = Grade((_, _) => 300, 0, 0, 10, 10, 5);

        Assert.Equal(50 + Folga, AreaHatch.Elevation(terreno, Retangulo(100, 100, 200, 200, 50), [], Folga)!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AAreaSemTresVerticesNaoTemCota()
    {
        Assert.Null(AreaHatch.Elevation(null, [new(0, 0, 1), new(1, 1, 1)], [], Folga));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void ONomeFicaDentroDaAreaMesmoNaAreaEmU()
    {
        Assert.Equal(new Point3(50, 25, 0), AreaHatch.LabelPoint(Retangulo(0, 0, 100, 50)));

        // O U: o centroide cai no vão do meio, fora da área.
        IReadOnlyList<Point3> u = [new(0, 0, 0), new(30, 0, 0), new(30, 30, 0), new(20, 30, 0), new(20, 10, 0), new(10, 10, 0), new(10, 30, 0), new(0, 30, 0)];
        var p = AreaHatch.LabelPoint(u);

        Assert.True(Polygons.Contains(u, p.X, p.Y), $"o nome ficou fora da área, em ({p.X}; {p.Y})");
        Assert.False(p.X > 10 && p.X < 20 && p.Y > 10, "o nome ficou no vão do U");
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AAlturaDoNomeAcompanhaOTamanhoDaArea()
    {
        Assert.Equal(100 / 30.0, AreaHatch.LabelHeight(10_000), 9);
        Assert.Equal(15, AreaHatch.LabelHeight(1_000_000));
        Assert.Equal(2, AreaHatch.LabelHeight(100));
        Assert.Equal(2, AreaHatch.LabelHeight(double.NaN));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void ONomeEscapaOsCodigosDoMtext()
    {
        Assert.Equal("Área 1", AreaHatch.LabelText("Área 1"));
        Assert.Equal("A\\{1\\}\\\\B", AreaHatch.LabelText("A{1}\\B"));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AHachuraEDaAreaPelaPolilinhaEPeloGuidQuandoAPolilinhaSumiu()
    {
        var g = Guid.NewGuid();
        var outra = Guid.NewGuid();

        // A polilinha certa (o handle sem diferença de maiúscula).
        Assert.True(new AreaHatchMark(g, "2a").Belongs(g, "2A", [(g, "2A")]));

        // A cópia da polilinha (mesmo GUID, outro handle) não leva a hachura da original.
        Assert.False(new AreaHatchMark(g, "2A").Belongs(g, "3B", [(g, "2A"), (g, "3B")]));

        // Handle que não está mais no desenho (copiado para outro): vale o GUID.
        Assert.True(new AreaHatchMark(g, "99").Belongs(g, "3B", [(g, "3B")]));

        // Outra área, nunca.
        Assert.False(new AreaHatchMark(outra, "2A").Belongs(g, "2A", [(g, "2A")]));
        Assert.False(new AreaHatchMark(Guid.Empty, "2A").IsValid);
    }
}
