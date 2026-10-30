using Clivus.Core.Invariants;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// As regras que leem pilar, com o biposte (39.2): a junta das pontas iguais
/// compara os pilares que levam a PB, e não o último da lista (o traseiro).
/// </summary>
public class BifilaInvariantsTests
{
    private const double Tilt = 10 * Math.PI / 180;

    private static ProjectSettings Settings => ProjectSettings.Default with
    {
        Configuration = ProjectSettings.Default.Configuration with { MinLowEdge = 0.85, MaxLowEdge = 1.5 },
    };

    /// <summary>Uma fileira de bipostes GC800 no plano: as mesas encadeadas.</summary>
    private static ProcessedRow Fileira()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        var geo = TableGeometry.Local(p.Layout, p.Frame.Pillars(p.Layout), p.Frame);

        Point3 P(double x, double y) => new(x, y, 700);
        var terreno = new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);

        var config = Settings.Configuration;
        var area = new[] { new Point3(0, 0, 0), new Point3(60, 0, 0), new Point3(60, 6, 0), new Point3(0, 6, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 6, 0) };
        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, config.Pitch, config.TableGap, [new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt))], [28], config.UpslopeAzimuthRadians);

        var usina = PlantPipeline.ProcessAll(layout, [geo], Tilt, [28], [720], terreno, Settings);
        return usina.Rows.First(r => r.Tables.Count >= 2);
    }

    [Fact]
    [Trait("Etapa", "39")]
    public void AJuntaDoBipostePeloPilarQueLevaAPb()
    {
        var fileira = Fileira();
        var config = Settings.Configuration;

        Assert.Empty(EqualTips.Check(fileira, config));

        // No pórtico da ponta de cada mesa, o traseiro fica sem PB (sem
        // terreno) e o dianteiro de B sobe meio metro: a junta abriu, e só o
        // pilar que leva a PB mostra isso.
        var (a, b) = (fileira.Tables[0], fileira.Tables[1]);
        var comAFileira = a.Orientation.LengthRunsWithRow;
        double Ponta(ProcessedTable t, bool primeira) => primeira ? t.Pillars.Pillars.Min(p => p.Station) : t.Pillars.Pillars.Max(p => p.Station);

        ProcessedTable Mexer(ProcessedTable t, double estacao, double soma) => t with
        {
            Pillars = t.Pillars with
            {
                Pillars = [.. t.Pillars.Pillars.Select(p => Math.Abs(p.Station - estacao) > 1e-9 ? p
                    : p.Line == PillarLine.Rear ? p with { LowEdgeClearance = null, Problem = "sem terreno" }
                    : p with { LowEdgeClearance = p.LowEdgeClearance + soma })],
            },
        };

        var mexida = fileira with
        {
            Tables = [Mexer(a, Ponta(a, primeira: !comAFileira), 0), Mexer(b, Ponta(b, primeira: comAFileira), 0.5), .. fileira.Tables.Skip(2)],
        };

        var abertas = EqualTips.Check(mexida, config);

        Assert.Contains(abertas, j => j.Contains(a.Label) && j.Contains(b.Label));
    }
}
