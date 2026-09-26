using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A célula reconstruída do contorno desenhado (7.3): ida e volta com o
/// distribuidor e a colocação, inclusive com giro longitudinal (quando o
/// contorno em planta é um paralelogramo); letreiro lido; contorno que não
/// descreve uma mesa recusado.
/// </summary>
public class TableCellsTests
{
    private const double Tilt = 20 * Math.PI / 180;
    private const double Comprimento = 20;
    private const double FundoEmPlanta = 4;
    private static readonly double Fundo = FundoEmPlanta / Math.Cos(Tilt);

    private static (PlacedTable Original, double Azimute) Original(double azimuteGraus)
    {
        var azimute = azimuteGraus * Math.PI / 180;
        Point3[] area = [new(-100, -100, 0), new(100, -100, 0), new(100, 100, 0), new(-100, 100, 0)];
        Point3[] linha = [new(-80 * Math.Sin(azimute), -80 * Math.Cos(azimute), 0), new(80 * Math.Sin(azimute), 80 * Math.Cos(azimute), 0)];

        var layout = RowDistributor.Distribute(area, linha, LineSide.Right, 6, 0.5, new TableFootprint(Comprimento, FundoEmPlanta), azimute);

        return (layout.Rows[1].Tables[2], azimute);
    }

    /// <summary>
    /// A célula como desenhada: mede dos cantos, sem perfil; com o perfil
    /// trocado (mesa mais curta) ela ainda sai, com as medidas do desenho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "7")]
    public void ComoDesenhadaMedeDosCantosSemPerfil()
    {
        var (original, azimute) = Original(180);
        var orientacao = RowOrientation.Resolve(original, azimute);
        var cantos = CantosDesenhados(TablePlacement.Plan(original, orientacao, Tilt, 700));

        var celula = TableCells.FromDrawnCorners(cantos, original.Label);

        Assert.Equal(original.Row, celula.Row);
        Assert.Equal(original.Number, celula.Number);
        Assert.Equal(Comprimento, celula.Length, 6);
        Assert.Equal(FundoEmPlanta, celula.PlanDepth, 6);
        Assert.Equal(original.Origin.X, celula.Origin.X, 6);
        Assert.Equal(original.Origin.Y, celula.Origin.Y, 6);

        Assert.Throws<ArgumentException>(() => TableCells.FromDrawnCorners(cantos, "mesa 3"));
        Assert.Throws<ArgumentException>(() => TableCells.FromDrawnCorners(cantos.Take(3).ToList(), "F1.1"));
    }

    /// <summary>Os cantos como o LayoutDrawer grava: (0,0), (L,0), (L,D), (0,D) locais, pela colocação.</summary>
    private static List<Point3> CantosDesenhados(Transform colocacao) =>
        new Point3[] { new(0, 0, 0), new(Comprimento, 0, 0), new(Comprimento, Fundo, 0), new(0, Fundo, 0) }
            .Select(colocacao.Apply).ToList();

    /// <summary>
    /// Mesa nivelada: distribui, coloca, pega os cantos e reconstrói. A
    /// célula volta igual (cantos, direção, medidas), a orientação tem a
    /// ponta baixa de cá, e colocar a célula reconstruída põe a mesa no
    /// mesmo lugar.
    /// </summary>
    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(180.0)]
    [InlineData(0.0)]
    [InlineData(37.0)]
    public void IdaEVoltaNaMesaNivelada(double azimuteGraus)
    {
        var (original, azimute) = Original(azimuteGraus);
        var orientacao = RowOrientation.Resolve(original, azimute);
        var colocacao = TablePlacement.Plan(original, orientacao, Tilt, 700);

        var volta = TableCells.FromCorners(CantosDesenhados(colocacao), original.Label, Comprimento, FundoEmPlanta);

        Assert.Equal(original.Row, volta.Row);
        Assert.Equal(original.Number, volta.Number);
        Assert.Equal(Comprimento, volta.Length, 9);
        Assert.Equal(FundoEmPlanta, volta.PlanDepth, 9);

        foreach (var c in original.Corners)
            Assert.Contains(volta.Corners, v => Math.Abs(v.X - c.X) < 1e-6 && Math.Abs(v.Y - c.Y) < 1e-6);

        var deVolta = RowOrientation.Resolve(volta, azimute);
        Assert.True(deVolta.LowEdgeOnNearSide);
        Assert.True(deVolta.LengthRunsWithRow);
        Assert.Equal(0, deVolta.DivergenceRadians, 6);

        MesmoLugar(colocacao, TablePlacement.Plan(volta, deVolta, Tilt, 700));
    }

    /// <summary>
    /// Mesa com giro longitudinal (ponta final 0,80 m mais alta): o contorno
    /// em planta é um paralelogramo com a borda baixa encurtada; a célula
    /// reconstruída é a nominal, e colocá-la com as mesmas cotas põe a mesa
    /// exatamente onde estava.
    /// </summary>
    [Theory]
    [Trait("Etapa", "7")]
    [InlineData(180.0)]
    [InlineData(250.0)]
    public void IdaEVoltaComGiroLongitudinal(double azimuteGraus)
    {
        var (original, azimute) = Original(azimuteGraus);
        var orientacao = RowOrientation.Resolve(original, azimute);
        var colocacao = TablePlacement.PlanSolved(original, orientacao, Tilt, 700, 700.8, Comprimento, out var giro);

        Assert.True(Math.Abs(giro) > 0.01);

        var cantos = CantosDesenhados(colocacao);

        // A borda baixa desenhada é mais curta que a mesa: é o giro.
        var bordaBaixa = Math.Sqrt(Math.Pow(cantos[1].X - cantos[0].X, 2) + Math.Pow(cantos[1].Y - cantos[0].Y, 2));
        Assert.True(bordaBaixa < Comprimento - 1e-3);

        var volta = TableCells.FromCorners(cantos, original.Label, Comprimento, FundoEmPlanta);
        var deVolta = RowOrientation.Resolve(volta, azimute);

        Assert.True(deVolta.LowEdgeOnNearSide);
        Assert.Equal(Comprimento, volta.Length, 9);

        var colocacao2 = TablePlacement.PlanSolved(volta, deVolta, Tilt, 700, 700.8, Comprimento, out _);
        MesmoLugar(colocacao, colocacao2);
    }

    private static void MesmoLugar(Transform a, Transform b)
    {
        foreach (var local in new Point3[] { new(0, 0, 0), new(Comprimento, 0, 0), new(Comprimento, Fundo, 0), new(0, Fundo, 0), new(7, 1, 0) })
        {
            var p = a.Apply(local);
            var q = b.Apply(local);
            Assert.Equal(p.X, q.X, 6);
            Assert.Equal(p.Y, q.Y, 6);
            Assert.Equal(p.Z, q.Z, 6);
        }
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void OLetreiroELido()
    {
        Assert.True(TableCells.TryParseLabel("F12.7", out var f, out var m));
        Assert.Equal(12, f);
        Assert.Equal(7, m);
        Assert.False(TableCells.TryParseLabel("F0.1", out _, out _));
        Assert.False(TableCells.TryParseLabel("12.7", out _, out _));
        Assert.False(TableCells.TryParseLabel("F1", out _, out _));
        Assert.False(TableCells.TryParseLabel("(sobreposta)", out _, out _));
        Assert.False(TableCells.TryParseLabel(null, out _, out _));
    }

    [Fact]
    [Trait("Etapa", "7")]
    public void ContornoQueNaoDescreveUmaMesaERecusado()
    {
        Point3[] bom = [new(0, 0, 0), new(20, 0, 0), new(20, 4, 0), new(0, 4, 0)];

        // Trocou de mesa: perfil de 30 m para um contorno de 20.
        var erro = Assert.Throws<ArgumentException>(() => TableCells.FromCorners(bom, "F1.1", 30, 4));
        Assert.Contains("Trocou de mesa", erro.Message);

        // Contorno esticado além do perfil.
        Assert.Throws<ArgumentException>(() => TableCells.FromCorners(bom, "F1.1", 18, 4));

        Point3[] tres = [new(0, 0, 0), new(20, 0, 0), new(20, 4, 0)];
        Assert.Throws<ArgumentException>(() => TableCells.FromCorners(tres, "F1.1", 20, 4));

        Point3[] achatado = [new(0, 0, 0), new(20, 0, 0), new(20, 0, 0), new(0, 0, 0)];
        Assert.Throws<ArgumentException>(() => TableCells.FromCorners(achatado, "F1.1", 20, 4));

        Assert.Throws<ArgumentException>(() => TableCells.FromCorners(bom, "mesa", 20, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => TableCells.FromCorners(bom, "F1.1", double.NaN, 4));
    }
}
