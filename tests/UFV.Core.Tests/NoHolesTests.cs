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

    /// <summary>
    /// Com o teste do terreno (Renan, 02/10/2026: "deveria testar com o
    /// módulo de 14 quando a primeira opção, que é 28, não couber; se a
    /// segunda não couber, volta à de 28 e pinta de outra cor"): em
    /// terrenos com morro sorteado, cada mesa é a primeira da lista que
    /// fica boa no terreno; a que não ficou boa com nenhuma é a de 28,
    /// marcada como "tentou todas"; e a fileira não tem buraco: cada mesa
    /// começa logo depois da anterior.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void TentaASegundaOndeAPrimeiraNaoFicaBoaSemBuraco()
    {
        var sorteio = new Random(20261003);
        var config = ProjectSettings.Default.Configuration;
        int trocadas = 0, tentouTodas = 0;

        for (var caso = 0; caso < 12; caso++)
        {
            var centro = sorteio.NextDouble() * 40;
            var largura = 2 + sorteio.NextDouble() * 12;
            var altura = 0.5 + sorteio.NextDouble() * 4;

            double Morro(double x, double y) => 700 + altura * Math.Exp(-Math.Pow((x - centro) / largura, 2));

            var terreno = Terreno(Morro);
            var fica = PlantPipeline.FitsOnTerrain(Geos, Tilt, terreno, ProjectSettings.Default);

            var area = new[] { new Point3(0, 0, 0), new Point3(40, 0, 0), new Point3(40, 8, 0), new Point3(0, 8, 0) };
            var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 8, 0) };
            var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, Pegadas, Modulos, config.UpslopeAzimuthRadians, fica);

            foreach (var fileira in layout.Rows)
            {
                for (var t = 0; t < fileira.Tables.Count; t++)
                {
                    var mesa = fileira.Tables[t];

                    if (mesa.TriedAll)
                    {
                        // Ficou a primeira da lista, e nenhuma ficava boa ali.
                        Assert.Equal(0, mesa.Kind);
                        Assert.False(fica(mesa));
                        Assert.False(fica(TerrainSwap(mesa, 1)));
                        tentouTodas++;
                    }
                    else if (mesa.Kind == 1 && fica(mesa) && t + 1 < fileira.Tables.Count)
                    {
                        // A de 14 no meio da fileira: a de 28 não ficava boa ali.
                        Assert.False(fica(TerrainSwap(mesa, 0)));
                        trocadas++;
                    }

                    // Sem buraco: a próxima começa no fim desta mais o espaçamento.
                    if (t + 1 < fileira.Tables.Count)
                    {
                        var proxima = fileira.Tables[t + 1];
                        var fimDesta = mesa.Corners[1];
                        var vao = Math.Sqrt(Math.Pow(proxima.Origin.X - fimDesta.X, 2) + Math.Pow(proxima.Origin.Y - fimDesta.Y, 2));
                        Assert.Equal(config.TableGap, vao, 6);
                    }
                }
            }
        }

        Assert.True(trocadas > 0, "nenhuma 28 virou 14 por causa do terreno: o teste não prova nada");
        Assert.True(tentouTodas > 0, "nenhum lugar ficou sem mesa boa: o teste não prova a volta à primeira");
    }

    /// <summary>A mesma mesa, no mesmo começo, com outro tipo.</summary>
    private static PlacedTable TerrainSwap(PlacedTable mesa, int tipo)
    {
        var c = mesa.Corners;
        var dx = (c[1].X - c[0].X) / mesa.Length;
        var dy = (c[1].Y - c[0].Y) / mesa.Length;
        var nx = (c[3].X - c[0].X) / mesa.PlanDepth;
        var ny = (c[3].Y - c[0].Y) / mesa.PlanDepth;
        var comprimento = Pegadas[tipo].Length;
        var fundo = Pegadas[tipo].PlanDepth;
        var fim = new Point3(c[0].X + dx * comprimento, c[0].Y + dy * comprimento, 0);

        return mesa with
        {
            Kind = tipo,
            Length = comprimento,
            PlanDepth = fundo,
            TriedAll = false,
            Corners = [c[0], fim, new Point3(fim.X + nx * fundo, fim.Y + ny * fundo, 0), new Point3(c[0].X + nx * fundo, c[0].Y + ny * fundo, 0)],
        };
    }
}
