using UFV.Core.Invariants;
using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// Sem buracos nas fileiras (Renan, 02/10/2026, com print das fileiras
/// esburacadas pela troca de 28 por 14: "isso não pode acontecer, buracos;
/// é melhor colocar a mesa e deixar ela enterrada e aí eu vejo o que faço").
/// A troca olhando o terreno saiu; a mesa que não dá fica marcada.
/// </summary>
public class NoHolesTests
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

    /// <summary>
    /// Propriedade: em terreno sorteado (morros de altura e largura
    /// aleatórias), a usina resolvida tem exatamente as mesas que a
    /// distribuição planejou, cada uma no lugar e do tipo dela, e as juntas
    /// fechadas (regra 6). Os morros enterram mesas: alguma fica marcada,
    /// e nenhuma some.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void TodaMesaPlanejadaFicaNoLugarMesmoEnterrada()
    {
        var sorteio = new Random(20261002);
        var marcadas = 0;

        for (var caso = 0; caso < 12; caso++)
        {
            var centro = sorteio.NextDouble() * 40;
            var largura = 2 + sorteio.NextDouble() * 12;
            var altura = 0.5 + sorteio.NextDouble() * 4;

            double Morro(double x, double y) => 700 + altura * Math.Exp(-Math.Pow((x - centro) / largura, 2));

            var (layout, _) = Usina(Terreno(Morro));
            var usina = PlantPipeline.ProcessAll(layout, Geos, Tilt, Modulos, [720, 720], Terreno(Morro), ProjectSettings.Default);

            Assert.Equal(layout.Rows.Count, usina.Rows.Count);

            for (var r = 0; r < layout.Rows.Count; r++)
            {
                var planejadas = layout.Rows[r].Tables;
                var resolvidas = usina.Rows[r].Tables;

                Assert.Equal(planejadas.Count, resolvidas.Count);

                for (var t = 0; t < planejadas.Count; t++)
                {
                    Assert.Equal(planejadas[t].Kind, resolvidas[t].Cell.Kind);
                    Assert.Equal(planejadas[t].Origin.X, resolvidas[t].Cell.Origin.X, 6);
                    Assert.Equal(planejadas[t].Origin.Y, resolvidas[t].Cell.Origin.Y, 6);
                    Assert.Equal(planejadas[t].Length, resolvidas[t].Cell.Length, 6);
                }

                Assert.Empty(EqualTips.Check(usina.Rows[r], ProjectSettings.Default.Configuration));
                marcadas += usina.Rows[r].MarkedCount;
            }
        }

        Assert.True(marcadas > 0, "os morros sorteados não enterraram mesa nenhuma: o teste não prova nada");
    }
}
