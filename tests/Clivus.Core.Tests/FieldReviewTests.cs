using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O que a revisão do 40.2 achou nas mesas conforme os objetos: o eixo perto
/// de 180°, a cerca que tomava o lugar das mesas, a sobreposição pequena, o
/// objeto 2D desenhado em planta e a fileira fora do rumo da maioria.
/// </summary>
public class FieldReviewTests
{
    private const double Grau = Math.PI / 180;

    private static TableProfile Gc800 => BifilaReference.Profile(BifilaVariant.Gc800);

    private static ProjectSettings ParaOSul => ProjectSettings.Default with
    {
        Configuration = ProjectSettings.Default.Configuration with { FacingAzimuthRadians = Math.PI, MinLowEdge = 0.85, MaxLowEdge = 1.6 },
    };

    /// <summary>A área dos módulos de uma 2P14, ponta baixa para o sul, o eixo girado <paramref name="graus"/> em torno do canto.</summary>
    private static FieldObject Mesa(string id, double x0, double y0, double graus = 0, double comprimento = 18.502, double largura = 4.788)
    {
        var (ux, uy) = (Math.Cos(graus * Grau), Math.Sin(graus * Grau));
        var (rx, ry) = (uy, -ux);
        var (dh, dz) = (largura * Math.Cos(10 * Grau), -largura * Math.Sin(10 * Grau));

        return new FieldObject(id, "CELDAS",
        [
            new(x0, y0, 0),
            new(x0 + ux * comprimento, y0 + uy * comprimento, 0),
            new(x0 + ux * comprimento + rx * dh, y0 + uy * comprimento + ry * dh, dz),
            new(x0 + rx * dh, y0 + ry * dh, dz),
        ]);
    }

    /// <summary>O mesmo objeto com os vértices começando do outro lado: o eixo sai perto de 180°.</summary>
    private static FieldObject Virado(FieldObject o) => o with { Vertices = [o.Vertices[2], o.Vertices[3], o.Vertices[0], o.Vertices[1]] };

    [Fact]
    [Trait("Etapa", "40")]
    public void OEixoPertoDe180NaoViraAMesa()
    {
        // A1 e A3 com o eixo a -0,02° (sai 179,98°, desenhados ao contrário), A2 a 0°.
        var a1 = Virado(Mesa("A1", 0, 0, graus: -0.02));
        var a2 = Mesa("A2", 18.852, 0);
        var a3 = Virado(Mesa("A3", 37.704, 0, graus: -0.02));

        var levantamento = FieldObjects.Survey([a1, a2, a3]);

        Assert.Empty(levantamento.Issues);
        var fileira = Assert.Single(levantamento.Rows);
        Assert.Equal(["A1", "A2", "A3"], fileira.Tables.Select(t => t.Id));

        // E o motor encadeia as três na ordem, com a junta fechada.
        Point3 P(double x, double y) => new(x, y, 700 + 0.03 * x);
        var terreno = new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);
        var usina = FieldPlacement.Process(levantamento, [Gc800], terreno, ParaOSul);

        Assert.Equal(["F1.1", "F1.2", "F1.3"], new[] { "A1", "A2", "A3" }.Select(id => usina.LabelByObject[id]));
        Assert.All(usina.DeviationByObject.Values, d => Assert.True(d < 0.01, $"desvio de {d:0.000} m"));
        Assert.Empty(Invariants.EqualTips.Check(usina.Plant.Rows[0], ParaOSul.Configuration));
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ACercaNaoTiraOLugarDasMesas()
    {
        // A cerca (um retângulo grande, fechado, de 4 vértices) vem antes na lista.
        var cerca = new FieldObject("CERCA", "CERCA", [new(-2, 2, 0), new(60, 2, 0), new(60, -8, 0), new(-2, -8, 0)]);

        var levantamento = FieldObjects.Survey([cerca, Mesa("A1", 0, 0), Mesa("A2", 18.852, 0), Mesa("A3", 37.704, 0)]);

        Assert.Equal(3, levantamento.TableCount);
        Assert.Contains(levantamento.Issues, i => i.Id == "CERCA" && i.Kind == FieldIssueKind.Overlap);
        Assert.DoesNotContain(levantamento.Issues, i => i.Id.StartsWith('A') && i.Skips);
    }

    /// <summary>Centros a 16 m numa mesa de 18,5 m: 2,5 m em cima da outra.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void ASobreposicaoPequenaNaMesmaFileiraEVista()
    {
        var levantamento = FieldObjects.Survey([Mesa("A", 0, 0), Mesa("B", 16, 0)]);

        Assert.Contains(levantamento.Issues, i => i.Id == "B" && i.Kind == FieldIssueKind.Overlap);
        Assert.Equal(1, levantamento.TableCount);
    }

    /// <summary>O retângulo plano com a largura em planta (4,788·cos 10° = 4,715 m) casa com a GC800.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void OObjeto2DEmPlantaCasaComAMesa()
    {
        var largura = 4.788 * Math.Cos(10 * Grau);
        var plano = new FieldObject("P", "2D", [new(0, 0, 0), new(18.502, 0, 0), new(18.502, -largura, 0), new(0, -largura, 0)]);
        var f = FieldObjects.Footprint(plano, out _)!;

        var casada = Assert.Single(FieldPlacement.Match([f], [Gc800], out var problemas));

        Assert.Empty(problemas);
        Assert.True(casada.ModulesOnly);
    }

    /// <summary>Uma fileira de quatro a 3° da maioria continua uma fileira de quatro.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void AFileiraForaDoRumoDaMaioriaContinuaFileira()
    {
        var passo = 18.502 + 0.35;
        var (cx, cy) = (Math.Cos(3 * Grau) * passo, Math.Sin(3 * Grau) * passo);
        List<FieldObject> objetos =
        [
            Mesa("A1", 0, 0), Mesa("A2", passo, 0), Mesa("A3", 2 * passo, 0), Mesa("A4", 3 * passo, 0), Mesa("A5", 4 * passo, 0),
            Mesa("B1", 0, -20, 3), Mesa("B2", cx, -20 + cy, 3), Mesa("B3", 2 * cx, -20 + 2 * cy, 3), Mesa("B4", 3 * cx, -20 + 3 * cy, 3),
        ];

        var levantamento = FieldObjects.Survey(objetos);

        Assert.Equal([5, 4], levantamento.Rows.Select(r => r.Tables.Count));
        Assert.Equal(["B1", "B2", "B3", "B4"], levantamento.Rows[1].Tables.Select(t => t.Id));
        Assert.Equal(4, levantamento.Issues.Count(i => i.Kind == FieldIssueKind.Axis));
    }
}
