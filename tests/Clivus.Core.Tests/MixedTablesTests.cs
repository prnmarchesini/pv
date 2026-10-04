using Clivus.Core.Invariants;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Passo 8.6: a usina com mais de um tipo de mesa (28 e 14 módulos). Cada
/// trecho de fileira recebe a combinação que põe mais módulos; o resto do
/// motor trata cada mesa com a geometria dela, e as regras sagradas valem.
/// </summary>
public class MixedTablesTests
{
    private const double Grau = Math.PI / 180;
    private const double Tilt = 20 * Grau;

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa(int modulos) => new(Risen(), modulos, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria(int modulos) =>
        TableGeometry.Local(Mesa(modulos), PillarTable.Distribute(Mesa(modulos).Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static TableFootprint Pegada(TableGeometry g) => new(g.Length, g.Depth * Math.Cos(Tilt));

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));
        return new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);
    }

    // 28 módulos 2V: 14 × 1,303 + 13 × 0,02 + 0,2 = 18,702 m; 14 módulos: 7 × 1,303 + 6 × 0,02 + 0,2 = 9,441 m.
    private static readonly TableFootprint Longa = new(18.702, 4.5);
    private static readonly TableFootprint Curta = new(9.441, 4.5);

    [Fact]
    [Trait("Etapa", "8")]
    public void NumTrechoDe30MetrosCabeUmaLongaEUmaCurta()
    {
        var combinacao = RowDistributor.Combinacao(30, [Longa, Curta], [28, 14], 0.5);

        Assert.Equal([0, 1], combinacao);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void APrimeiraDaListaTemPrioridade()
    {
        // Renan, 02/10/2026: "sempre vai tentar encaixar o primeiro, se não
        // der, aí o segundo". 40 m com a longa primeiro: duas longas; com a
        // curta primeiro: quatro curtas.
        Assert.Equal([0, 0], RowDistributor.Combinacao(40, [Longa, Curta], [28, 14], 0.5));
        Assert.Equal([0, 0, 0, 0], RowDistributor.Combinacao(40, [Curta, Longa], [14, 28], 0.5));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ASegundaSoEntraNoQueSobra()
    {
        // 30 m: uma longa (19,2 com o espaço) e, nos 11,3 que sobram, uma curta.
        Assert.Equal([0, 1], RowDistributor.Combinacao(30, [Longa, Curta], [28, 14], 0.5));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(9.0, new int[] { })]
    [InlineData(9.441, new[] { 1 })]
    [InlineData(18.702, new[] { 0 })]
    [InlineData(19.0, new[] { 0 })]
    [InlineData(28.643, new[] { 0, 1 })]
    public void OTrechoNuncaEstoura(double comprimento, int[] esperado)
    {
        var combinacao = RowDistributor.Combinacao(comprimento, [Longa, Curta], [28, 14], 0.5);

        Assert.Equal(esperado, combinacao);

        var ocupado = combinacao.Sum(t => (t == 0 ? Longa : Curta).Length) + Math.Max(0, combinacao.Count - 1) * 0.5;
        Assert.True(ocupado <= comprimento + 1e-3);
    }

    private static (PlanLayout Layout, TableGeometry[] Geos) Usina(double largura, double fundo)
    {
        var geos = new[] { Geometria(28), Geometria(14) };
        var area = new[] { new Point3(0, 0, 0), new Point3(largura, 0, 0), new Point3(largura, fundo, 0), new Point3(0, fundo, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, fundo, 0) };
        var config = ProjectSettings.Default.Configuration;

        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, config.Pitch, config.TableGap,
            [Pegada(geos[0]), Pegada(geos[1])], [28, 14], config.UpslopeAzimuthRadians);

        return (layout, geos);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AUsinaMistaPoeMaisModulosQueSoAMesaLonga()
    {
        // 70 m de largura: com só a longa cabem 3 (56,1 m + folgas); com a
        // curta, 3 longas e 1 curta.
        var (mista, geos) = Usina(70, 30);
        var config = ProjectSettings.Default.Configuration;
        var area = new[] { new Point3(0, 0, 0), new Point3(70, 0, 0), new Point3(70, 30, 0), new Point3(0, 30, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 30, 0) };
        var so = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, Pegada(geos[0]), config.UpslopeAzimuthRadians);

        var modulosMista = mista.Tables.Sum(t => t.Kind == 0 ? 28 : 14);
        var modulosSo = so.Tables.Count * 28;

        Assert.True(modulosMista > modulosSo, $"mista {modulosMista}, só longa {modulosSo}");
        Assert.Contains(mista.Tables, t => t.Kind == 1);
        Assert.All(mista.Tables, t => Assert.Equal(t.Kind == 0 ? geos[0].Length : geos[1].Length, t.Length, 6));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AsMesasDaFileiraNaoSeSobrepoem()
    {
        var (layout, _) = Usina(70, 30);
        var gap = ProjectSettings.Default.Configuration.TableGap;

        foreach (var fileira in layout.Rows)
        {
            for (var i = 0; i + 1 < fileira.Tables.Count; i++)
                Assert.True(RowSolver.GapBetween(fileira.Tables[i], fileira.Tables[i + 1]) >= gap - 1e-6);
        }
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OMotorTrataCadaMesaComAGeometriaDelaEAJuntaFecha()
    {
        var (layout, geos) = Usina(70, 30);

        // Rampa ao longo da fileira e da subida: as juntas têm trabalho.
        var usina = PlantPipeline.ProcessAll(
            layout, geos, Tilt, [28, 14], [720, 720], Plano((x, y) => 700 + 0.05 * x + 0.03 * y), ProjectSettings.Default);

        Assert.Equal(layout.Tables.Sum(t => t.Kind == 0 ? 28 : 14), usina.ModuleCount);
        Assert.Equal(usina.ModuleCount * 0.72, usina.PowerKwp, 9);
        Assert.Equal(layout.Tables.Count(t => t.Kind == 1), usina.TablesByKind[1]);

        // Cada mesa com os pilares da geometria dela; regra sagrada 6 em toda fileira.
        foreach (var fileira in usina.Rows)
        {
            Assert.Empty(EqualTips.Check(fileira, ProjectSettings.Default.Configuration));

            foreach (var mesa in fileira.Tables)
                Assert.Equal(geos[mesa.Cell.Kind].Pillars.Count, mesa.Pillars.Pillars.Count);
        }
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void TipoQueNaoVeioERecusado()
    {
        var (layout, geos) = Usina(70, 30);

        Assert.Throws<ArgumentException>(() =>
            PlantPipeline.ProcessAll(layout, [geos[0]], Tilt, [28], [720], Plano((_, _) => 700), ProjectSettings.Default));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ComUmTipoSoADistribuicaoEAMesmaDeSempre()
    {
        var geo = Geometria(28);
        var config = ProjectSettings.Default.Configuration;
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 60, 0), new Point3(0, 60, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 60, 0) };

        var antiga = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, Pegada(geo), config.UpslopeAzimuthRadians);
        var lista = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, [Pegada(geo)], [28], config.UpslopeAzimuthRadians);

        Assert.Equal(antiga.Tables.Count, lista.Tables.Count);
        Assert.Equal(antiga.DroppedOutside, lista.DroppedOutside);
        Assert.All(antiga.Tables.Zip(lista.Tables), par =>
        {
            Assert.Equal(par.First with { Corners = [] }, par.Second with { Corners = [] });
            Assert.Equal(par.First.Corners, par.Second.Corners);
        });
    }
}
