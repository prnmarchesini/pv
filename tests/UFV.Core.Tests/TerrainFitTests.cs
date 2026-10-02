using UFV.Core.Invariants;
using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A troca da mesa comprida pela curta onde o morro enterra módulos
/// (Renan, 02/10/2026: "será que a de 14 não passaria aí?").
/// </summary>
public class TerrainFitTests
{
    private const double Tilt = 20 * Math.PI / 180;

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa(int modulos) => new(Risen(), modulos, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria(int modulos) =>
        TableGeometry.Local(Mesa(modulos), PillarTable.Distribute(Mesa(modulos).Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static readonly TableGeometry[] Geos = [Geometria(28), Geometria(14)];
    private static readonly TableFootprint[] Pegadas = Geos.Select(g => new TableFootprint(g.Length, g.Depth * Math.Cos(Tilt))).ToArray();
    private static readonly int[] Modulos = [28, 14];

    private static Tin Terreno(Func<double, double, double> z)
    {
        // Malha fina: o morro precisa aparecer entre os pilares.
        var triangulos = new List<Triangle>();

        for (var x = -20.0; x < 120; x += 1)
        {
            for (var y = -20.0; y < 60; y += 1)
            {
                Point3 P(double a, double b) => new(a, b, z(a, b));
                triangulos.Add(new Triangle(P(x, y), P(x + 1, y), P(x + 1, y + 1)));
                triangulos.Add(new Triangle(P(x, y), P(x + 1, y + 1), P(x, y + 1)));
            }
        }

        return new Tin(triangulos);
    }

    private static (PlanLayout Layout, Func<PlanRow, ProcessedRow> Resolver) Usina(Tin terreno, double largura = 40)
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(largura, 0, 0), new Point3(largura, 8, 0), new Point3(0, 8, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 8, 0) };
        var config = ProjectSettings.Default.Configuration;

        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, Pegadas, Modulos, config.UpslopeAzimuthRadians);

        ProcessedRow Resolver(PlanRow fileira) =>
            RowPipeline.ProcessRow(fileira, fileira.Tables.Select(t => Geos[t.Kind]).ToList(), Tilt, terreno, ProjectSettings.Default);

        return (layout, Resolver);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(false)]
    [InlineData(true)]
    public void AMesaEncurtadaFicaNoLugarDela(bool noFim)
    {
        var (layout, _) = Usina(Terreno((_, _) => 700));
        var longa = layout.Tables.First(t => t.Kind == 0);

        var curta = TerrainFit.Replace(longa, Pegadas[1], 1, noFim);

        Assert.Equal(1, curta.Kind);
        Assert.Equal(Pegadas[1].Length, curta.Length, 9);
        Assert.Equal(longa.DirectionRadians, curta.DirectionRadians, 9);

        // Dentro do lugar da comprida: o começo ou o fim coincidem.
        var inicio = noFim ? longa.Corners[1] : longa.Corners[0];
        var ponta = noFim ? curta.Corners[1] : curta.Corners[0];
        Assert.Equal(inicio.X, ponta.X, 6);
        Assert.Equal(inicio.Y, ponta.Y, 6);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void TerrenoPlanoNaoTrocaNada()
    {
        var (layout, resolver) = Usina(Terreno((x, y) => 700 + 0.02 * x));

        foreach (var fileira in layout.Rows)
        {
            var antes = resolver(fileira);
            var depois = TerrainFit.Improve(antes, Pegadas, Modulos, resolver);

            Assert.Same(antes, depois);
        }
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void OMorroLargoNoMeioTrocaALongaPelaCurta()
    {
        // Um planalto alto entre x = 2 e x = 15 (dentro do lugar da primeira
        // mesa de 28, que vai de 0 a 18,7): as pontas dela ficam fora do
        // morro e o meio enterra. A de 14 encostada no fim (9,3 a 18,7) pega
        // só a borda dele.
        double Morro(double x, double y) => 700 + (x is > 2 and < 15 ? 3.0 : 0.0);

        var (layout, resolver) = Usina(Terreno(Morro), largura: 19.5);
        var fileira = layout.Rows[0];

        var antes = resolver(fileira);
        var depois = TerrainFit.Improve(antes, Pegadas, Modulos, resolver);

        Assert.True(TerrainFit.UsefulModules(depois, Modulos) >= TerrainFit.UsefulModules(antes, Modulos));
        Assert.Empty(EqualTips.Check(depois, ProjectSettings.Default.Configuration));
    }

    /// <summary>
    /// Propriedade: em terreno sorteado (morros de altura e largura
    /// aleatórias), a troca nunca piora os módulos úteis, e a fileira
    /// trocada continua fechando as juntas (regra 6).
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void ATrocaNuncaPioraENuncaAbreJunta()
    {
        var sorteio = new Random(20261002);
        var trocou = 0;

        for (var caso = 0; caso < 12; caso++)
        {
            var centro = sorteio.NextDouble() * 40;
            var largura = 2 + sorteio.NextDouble() * 12;
            var altura = 0.5 + sorteio.NextDouble() * 4;

            double Morro(double x, double y) => 700 + altura * Math.Exp(-Math.Pow((x - centro) / largura, 2));

            var (layout, resolver) = Usina(Terreno(Morro));

            foreach (var fileira in layout.Rows)
            {
                var antes = resolver(fileira);
                var depois = TerrainFit.Improve(antes, Pegadas, Modulos, resolver);

                Assert.True(TerrainFit.UsefulModules(depois, Modulos) >= TerrainFit.UsefulModules(antes, Modulos));
                Assert.Empty(EqualTips.Check(depois, ProjectSettings.Default.Configuration));
                if (!ReferenceEquals(antes, depois)) trocou++;
            }
        }

        // O sorteio tem morros que enterram o meio de mesas de 28: alguma troca acontece.
        Assert.True(trocou > 0, "nenhuma troca em 12 terrenos com morro");
    }
}
