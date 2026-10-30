using Clivus.Core.Invariants;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Dois pilares por pórtico (passo 37.2): a geometria monta o dianteiro e o
/// traseiro de cada estação, e o cálculo desce o topo dos dois pelo "topo
/// abaixo da face dos módulos".
///
/// Os números de ouro são os pilares de catálogo da Arctech
/// (plano/bifila/01-modelo-de-referencia.md): 3,259 e 3,788 no plano para a
/// GC1000, e o traseiro de 4,388 com o chão caindo 20 % para trás.
/// </summary>
public class BifilaPillarsTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>A tolerância dos números de ouro: o PDF dá 1,0589 e a conta, 1,0588.</summary>
    private const double Ouro = 0.002;

    private static TableProfile Perfil(BifilaVariant v) => BifilaReference.Profile(v);

    private static TableGeometry Geometria(TableProfile p) =>
        TableGeometry.Local(p.Layout, p.Frame.Pillars(p.Layout), p.Frame);

    private static SystemConfiguration Config() =>
        SystemConfiguration.Default with { MinEmbedment = 2.2, MaxEmbedment = 2.2, MaxLowEdge = 1.5 };

    /// <summary>A PB do Clivus (face de cima do módulo) para a altura livre que a Arctech cota no fundo.</summary>
    private static double PbDaArctech(double livreNoFundo) => livreNoFundo + 0.033 * Math.Cos(10 * Grau);

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));

        return new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);
    }

    private static (PlacedTable Cell, RowOrientation Orientation) Celula(TableGeometry geo)
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };

        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, 8, 0.5,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(10 * Grau)), Config().UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[1];

        return (cell, RowOrientation.Resolve(cell, Config().UpslopeAzimuthRadians));
    }

    private static TablePillars Calcular(TableProfile p, double pb, Func<double, double, double>? chao = null)
    {
        var geo = Geometria(p);
        var (cell, orientacao) = Celula(geo);
        var cota = 700 + pb;

        return PillarCalculator.Compute(
            geo, cell, orientacao, p.TiltRadians, new SolvedTable("F2.2", cota, cota, 0, false, null),
            Plano(chao ?? ((_, _) => 700)), Config());
    }

    // ------------------------------------------------------------ geometria

    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaTemDoisPilaresPorPortico()
    {
        var p = Perfil(BifilaVariant.Gc800);
        var geo = Geometria(p);

        Assert.Equal(12, geo.Pillars.Count);

        // Ordem estação → frente → trás: Pillars[0] e Pillars[^1] continuam
        // sendo a primeira e a última estação, como na monoposte.
        var estacoes = p.Frame.Pillars(p.Layout).Positions;
        for (var i = 0; i < estacoes.Count; i++)
        {
            Assert.Equal(estacoes[i], geo.Pillars[2 * i].Station, 12);
            Assert.Equal(estacoes[i], geo.Pillars[2 * i + 1].Station, 12);
            Assert.Equal(PillarLine.Front, geo.Pillars[2 * i].Line);
            Assert.Equal(PillarLine.Rear, geo.Pillars[2 * i + 1].Line);
        }
    }

    /// <summary>
    /// A fileira de cada pilar, medida da ponta baixa do módulo: sobra
    /// (0,379) + T2. É o ponto do plano em cima do eixo do pilar, o que o
    /// corte da Arctech dá (0,877 e 3,923).
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void CadaPilarFicaNaSuaFileira()
    {
        var geo = Geometria(Perfil(BifilaVariant.Gc800));

        Assert.Equal(0.877, geo.PillarRow, 9);
        Assert.Equal(3.923, geo.RearPillarRow!.Value, 9);
        Assert.Equal(0.126, geo.PillarTopDrop, 12);

        Assert.All(geo.Pillars.Where(p => p.Line == PillarLine.Front), p => Assert.Equal(0.877, p.Anchor.Y, 9));
        Assert.All(geo.Pillars.Where(p => p.Line == PillarLine.Rear), p => Assert.Equal(3.923, p.Anchor.Y, 9));

        // A pegada centrada no eixo de cada um.
        Assert.All(geo.Pillars, p => Assert.Equal(p.Anchor.Y, p.Footprint.Average(q => q.Y), 9));
    }

    /// <summary>A regra sagrada 2 (mesa plana) vale com os âncoras dos dois pilares.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaContinuaUmaMesaRigida()
    {
        var geo = Geometria(Perfil(BifilaVariant.Gc1000));

        Assert.Equal(28 * 4 + 12, geo.PlanePoints().Count());
        Assert.Null(RigidTable.Check(geo));

        var levada = geo.Transformed(Transform.Place(10 * Grau, 0.3, new Point3(10, 20, 700)));
        Assert.Equal(geo.Pillars.Select(p => p.Line), levada.Pillars.Select(p => p.Line));
        Assert.Equal(geo.RearPillarRow, levada.RearPillarRow);
        Assert.Equal(geo.PillarTopDrop, levada.PillarTopDrop);
        Assert.Null(RigidTable.Check(levada));
    }

    /// <summary>A regressão do passo: a monoposte continua com um pilar por estação, na fileira de sempre.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMonoposteContinuaComUmPilarPorEstacao()
    {
        var mesa = new TableLayout(new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);
        var estrutura = new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0);
        var geo = TableGeometry.Local(mesa, estrutura.Pillars(mesa), estrutura);

        Assert.Equal(estrutura.Pillars(mesa).Positions.Count, geo.Pillars.Count);
        Assert.All(geo.Pillars, p => Assert.Equal(PillarLine.Single, p.Line));
        Assert.Null(geo.RearPillarRow);
        Assert.Equal(0, geo.PillarTopDrop);
    }

    // ------------------------------------------------------ números de ouro

    public static TheoryData<BifilaVariant, double, double, double> NoPlano() => new()
    {
        // variante, altura livre no fundo do módulo, pilar da frente, pilar de trás
        { BifilaVariant.Gc1000, 1.000, 3.259, 3.788 },
        { BifilaVariant.Gc800, 0.800, 3.059, 3.588 },
    };

    [Theory]
    [Trait("Etapa", "37")]
    [MemberData(nameof(NoPlano))]
    public void NoChaoPlanoOsPilaresSaemComOsDoCatalogo(BifilaVariant variante, double livre, double frente, double tras)
    {
        var pilares = Calcular(Perfil(variante), PbDaArctech(livre));

        Assert.Equal(12, pilares.Pillars.Count);
        Assert.True(pilares.AllSound);

        Assert.All(pilares.Pillars.Where(p => p.Line == PillarLine.Front), p =>
        {
            Assert.Equal(frente, p.Length!.Value, Ouro);
            Assert.Equal(frente - 2.2, p.FreeHeight!.Value, Ouro);
            Assert.Equal(2.2, p.Embedment, 12);
        });
        Assert.All(pilares.Pillars.Where(p => p.Line == PillarLine.Rear), p => Assert.Equal(tras, p.Length!.Value, Ouro));

        // O traseiro sobe 3,00 × tg 10° = 0,529 a mais que o dianteiro.
        Assert.Equal(3.0 * Math.Tan(10 * Grau), pilares.Pillars[1].Length!.Value - pilares.Pillars[0].Length!.Value, 3);
    }

    public static TheoryData<BifilaVariant, double, double, double, double> NoDeclive() => new()
    {
        // variante, altura livre, declive para trás, pilar da frente, pilar de trás (o do catálogo)
        { BifilaVariant.Gc1000, 1.000, 0.20, 3.259, 4.388 },
        { BifilaVariant.Gc800, 0.800, 0.10, 3.059, 3.888 },
    };

    /// <summary>
    /// O chão caindo para trás, girando no pé do pilar da frente: é o caso
    /// para o qual a Arctech dimensiona o pilar traseiro de catálogo.
    /// </summary>
    [Theory]
    [Trait("Etapa", "37")]
    [MemberData(nameof(NoDeclive))]
    public void ComOChaoCaindoParaTrasOTraseiroDaODoCatalogo(BifilaVariant variante, double livre, double declive, double frente, double tras)
    {
        var p = Perfil(variante);

        // No plano, para achar o pé do dianteiro e o sentido "para trás".
        var plano = Calcular(p, PbDaArctech(livre));
        var f = plano.Pillars[0];
        var t = plano.Pillars[1];
        var dx = t.X - f.X;
        var dy = t.Y - f.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);

        Assert.Equal(3.000, d, 3);

        double Chao(double x, double y) => 700 - declive * ((x - f.X) * dx + (y - f.Y) * dy) / d;

        var pilares = Calcular(p, PbDaArctech(livre), Chao);

        Assert.Equal(frente, pilares.Pillars[0].Length!.Value, Ouro);
        Assert.Equal(tras, pilares.Pillars[1].Length!.Value, Ouro);
        Assert.Equal(PillarLine.Front, pilares.Pillars[0].Line);
        Assert.Equal(PillarLine.Rear, pilares.Pillars[1].Line);
    }

    /// <summary>
    /// A PB e a PA são da estação, e não do pilar: os dois pilares do
    /// pórtico trazem as mesmas. É o que deixa o verificador de juntas e as
    /// pontas à mão certos com a bifila.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OsDoisPilaresDoPorticoTemAMesmaPbEPa()
    {
        var pilares = Calcular(Perfil(BifilaVariant.Gc800), PbDaArctech(0.8));

        for (var i = 0; i < pilares.Pillars.Count; i += 2)
        {
            Assert.Equal(pilares.Pillars[i].LowEdgeClearance!.Value, pilares.Pillars[i + 1].LowEdgeClearance!.Value, 12);
            Assert.Equal(pilares.Pillars[i].HighEdgeClearance!.Value, pilares.Pillars[i + 1].HighEdgeClearance!.Value, 12);
        }

        Assert.Equal(PbDaArctech(0.8), pilares.Pillars[0].LowEdgeClearance!.Value, 9);
    }

    /// <summary>
    /// Regra sagrada 1 no pilar traseiro: o chão subindo para trás até o topo
    /// dele marca o traseiro, e não o dianteiro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OTraseiroQueNaoTemAlturaLivreEMarcado()
    {
        var p = Perfil(BifilaVariant.Gc800);
        var plano = Calcular(p, PbDaArctech(0.8));
        var f = plano.Pillars[0];
        var t = plano.Pillars[1];
        var dx = t.X - f.X;
        var dy = t.Y - f.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);

        // Subindo 60 %: em 3 m o chão sobe 1,80, acima do topo do traseiro (1,388).
        double Chao(double x, double y) => 700 + 0.60 * ((x - f.X) * dx + (y - f.Y) * dy) / d;

        var pilares = Calcular(p, PbDaArctech(0.8), Chao);

        Assert.All(pilares.Pillars.Where(q => q.Line == PillarLine.Rear), q => Assert.NotNull(q.Problem));
        Assert.Contains(pilares.Pillars, q => q.Line == PillarLine.Front && q.Problem is null);
        Assert.Null(FloatingPillar.Check(pilares.Pillars.First(q => q.Line == PillarLine.Front).FreeHeight!.Value, 2.2));
    }

    /// <summary>Na monoposte o topo do pilar continua no plano dos módulos: a conta do 5.x não muda.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void NaMonoposteOTopoContinuaNoPlano()
    {
        var mesa = new TableLayout(new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);
        var estrutura = new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0);
        var p = new TableProfile("mono", mesa, estrutura, 10 * Grau);

        var pilares = Calcular(p, 0.5);

        Assert.All(pilares.Pillars, q =>
        {
            Assert.Equal(PillarLine.Single, q.Line);
            Assert.Equal(0.5 + TableGeometry.Local(mesa, estrutura.Pillars(mesa), estrutura).PillarRow * Math.Sin(10 * Grau), q.FreeHeight!.Value, 9);
        });
    }
}
