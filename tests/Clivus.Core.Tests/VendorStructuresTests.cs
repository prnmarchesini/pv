using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A aba Fabricantes, o biposte genérico e a altura básica (passo 38.7).
///
/// Renan, 30/10/2026: "se for um biposte específico do fabricante, crie uma
/// aba de fabricantes e aí um genérico, para a pessoa montar o biposte dela"
/// e "cadê a altura básica das duas pernas da mesa? ... P1 que é o pilar
/// menor e P2 que é o pilar maior".
///
/// O P1 e o P2 da janela são uma conta só dela; estes testes os amarram ao
/// motor inteiro (<see cref="RowPipeline.ProcessRow"/> num terreno plano, que
/// escolhe a ponta baixa na grade de 1 cm) e aos números de catálogo da
/// Arctech. A revisão do 38.7 achou a primeira versão montando a cota à mão:
/// ela provava só a fórmula, e deixava passar a grade.
/// </summary>
public class VendorStructuresTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>A tolerância dos números de catálogo: o PDF dá 1,0589 e a conta, 1,0588.</summary>
    private const double Ouro = 0.002;

    /// <summary>A tolerância contra o motor: a mesma conta, só o ponto flutuante de diferença.</summary>
    private const double Motor = 1e-6;

    // ----------------------------------------------------------- catálogo

    [Fact]
    [Trait("Etapa", "38")]
    public void AsEstruturasDeFabricanteSaoMesasValidasComONomeDoFabricante()
    {
        Assert.Equal(2, VendorStructures.All.Count);
        Assert.Equal(VendorStructures.All.Count, VendorStructures.All.Select(e => e.Profile.Name).Distinct().Count());

        Assert.All(VendorStructures.All, e =>
        {
            Assert.Null(e.Profile.WhyInvalid);
            Assert.True(e.Profile.IsBifila);
            Assert.StartsWith(e.Manufacturer + " ", e.Name, StringComparison.Ordinal);
            Assert.StartsWith(e.Manufacturer + " ", e.Profile.Name, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(e.Describe()));
            Assert.Contains(e.Manufacturer, e.DescribeSource(), StringComparison.Ordinal);
            Assert.Contains(e.Revision, e.DescribeSource(), StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void AEstruturaDeFabricanteEhAMesaDeReferenciaComOutroNome()
    {
        var gc800 = VendorStructures.All.Single(e => e.Model.Contains("GC800", StringComparison.Ordinal));
        var referencia = BifilaReference.Profile(BifilaVariant.Gc800);

        Assert.Equal(referencia with { Name = gc800.Profile.Name }, gc800.Profile);
        Assert.Equal(0.800 + 0.033 * Math.Cos(10 * Grau), gc800.LowEdge, 9);
    }

    /// <summary>
    /// "Usar esta estrutura" mantém o módulo da janela: a ponta baixa que dá a
    /// altura livre do fabricante é com a espessura e a inclinação da mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void APontaBaixaDoFabricanteUsaOModuloEAInclinacaoDaMesa()
    {
        var gc800 = VendorStructures.All[0];
        var outra = gc800.Profile with
        {
            Layout = gc800.Profile.Layout with { Module = gc800.Profile.Layout.Module with { Thickness = 0.040 } },
            TiltRadians = 15 * Grau,
        };

        Assert.Equal(0.800 + 0.040 * Math.Cos(15 * Grau), gc800.LowEdgeFor(outra), 12);
        Assert.Equal(gc800.LowEdge, gc800.LowEdgeFor(gc800.Profile), 12);
    }

    public static TheoryData<string> Modelos() => new() { "GC800", "GC1000" };

    /// <summary>
    /// Com a altura livre exata do fabricante (sem a grade do motor), o P1 é o
    /// pilar dianteiro do catálogo, e o P2 de catálogo é o do chão plano mais
    /// a folga do declive para o qual ele foi feito (3,00 m × 10 % = 0,300).
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [MemberData(nameof(Modelos))]
    public void OsPilaresDoCatalogoFechamComAAlturaLivreDoFabricante(string modelo)
    {
        var estrutura = VendorStructures.All.Single(e => e.Model.Contains(modelo + "MM", StringComparison.Ordinal));
        var noPlano = estrutura.Profile.PillarsOnFlatGround(estrutura.LowEdge, estrutura.Profile.Frame.MinEmbedment!.Value);

        Assert.Equal(estrutura.CatalogP1, noPlano.P1, Ouro);
        Assert.Equal(estrutura.CatalogP2, noPlano.P2!.Value + estrutura.Profile.BifilaHorizontalSpacing!.Value * estrutura.CatalogSlope, Ouro);
        Assert.Equal(2.2, noPlano.Embedment, 12);
    }

    // ------------------------------------------------------- altura básica

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData(0.8325, 0.84)]
    [InlineData(0.832, 0.84)]
    [InlineData(0.84, 0.84)]
    [InlineData(0.83, 0.83)]
    [InlineData(0.30, 0.30)]
    [InlineData(0.305, 0.31)]
    [InlineData(1.0325, 1.04)]
    public void NoChaoPlanoAPontaBaixaSobeParaAGradeDoMotor(double minima, double noPlano)
    {
        Assert.Equal(noPlano, ViableElevations.FlatGroundLowEdge(Projeto(minima)), 9);
    }

    public static TheoryData<string, double> Bipostes() => new()
    {
        // a mesa, a ponta baixa mínima do projeto (fora e dentro da grade)
        { "GC800", 0.8325 },
        { "GC800", 0.84 },
        { "GC1000", 1.0325 },
        { "Genérico", 0.305 },
    };

    /// <summary>
    /// O P1 e o P2 da janela são os pilares que o motor inteiro desenha no
    /// chão plano, com a ponta baixa fora da grade inclusive.
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [MemberData(nameof(Bipostes))]
    public void NoBiposteOP1EOP2SaoOsDoMotorNoChaoPlano(string mesa, double minima)
    {
        var perfil = mesa == "Genérico"
            ? BifilaReference.Generic()
            : VendorStructures.All.Single(e => e.Model.Contains(mesa + "MM", StringComparison.Ordinal)).Profile;
        var projeto = Projeto(minima);

        var basica = perfil.PillarsAtBasicHeight(projeto);
        var (pontasBaixas, pilares) = PeloMotor(perfil, projeto);

        Assert.All(pontasBaixas, pb => Assert.Equal(basica.LowEdge, pb, 6));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Front), p => Assert.Equal(basica.P1, p.Length!.Value, Motor));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Rear), p => Assert.Equal(basica.P2!.Value, p.Length!.Value, Motor));
        Assert.Contains(pilares, p => p.Line == PillarLine.Rear);
        Assert.True(basica.P2 > basica.P1);
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData(0.30, null)]
    [InlineData(0.355, null)]
    [InlineData(0.355, 1.5)]
    public void NaMonoposteSoHaOP1EEleEhODoMotor(double minima, double? t3)
    {
        var modulo = new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033);
        var perfil = new TableProfile(
            "Mesa 28 módulos",
            new TableLayout(modulo, 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0) { MinEmbedment = t3 },
            20 * Grau);
        var projeto = Projeto(minima);

        var basica = perfil.PillarsAtBasicHeight(projeto);
        var (_, pilares) = PeloMotor(perfil, projeto);

        Assert.Null(basica.RearFree);
        Assert.Null(basica.P2);
        Assert.Equal(t3 ?? projeto.MinEmbedment, basica.Embedment, 12);
        Assert.All(pilares, p => Assert.Equal(basica.P1, p.Length!.Value, Motor));
    }

    /// <summary>Sem T3 na mesa, o enterro é o mínimo do projeto; com T3, o da mesa.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OEnterroEhOT3DaMesaOuOMinimoDoProjeto()
    {
        var comT3 = BifilaReference.Profile(BifilaVariant.Gc800);
        var semT3 = comT3 with { Frame = comT3.Frame with { MinEmbedment = null } };
        var projeto = Projeto(0.84) with { MinEmbedment = 1.5, MaxEmbedment = 3.0 };

        Assert.Equal(2.2, comT3.PillarsAtBasicHeight(projeto).Embedment, 12);
        Assert.Equal(1.5, semT3.PillarsAtBasicHeight(projeto).Embedment, 12);
        Assert.Equal(comT3.PillarsAtBasicHeight(projeto).P1 - 0.7, semT3.PillarsAtBasicHeight(projeto).P1, 9);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void PerfilInvalidoNaoTemAlturaBasica()
    {
        var perfil = BifilaReference.Profile(BifilaVariant.Gc800);
        var quebrado = perfil with { Frame = perfil.Frame with { Bifila = perfil.Frame.Bifila! with { PurlinMaxBar = 0.1 } } };

        Assert.NotNull(quebrado.WhyInvalid);
        Assert.Throws<InvalidOperationException>(() => quebrado.PillarsAtBasicHeight(Projeto(0.8)));
        Assert.Throws<InvalidOperationException>(() => quebrado.PillarsOnFlatGround(0.8, 2.2));
    }

    // ----------------------------------------------------------- genérico

    /// <summary>
    /// O genérico é para montar o seu: sem fabricante no nome, sem mão
    /// francesa, sem tirante, pelo vão-alvo e sem T3 (o enterro do projeto,
    /// que sabe o solo; a revisão do 38.7 o achou com os 2,20 m da Arctech).
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OBiposteGenericoNaoTemNadaDeFabricante()
    {
        var generico = BifilaReference.Generic();
        var b = generico.Frame.Bifila!;

        Assert.Null(generico.WhyInvalid);
        Assert.Equal("Biposte", generico.Name);
        Assert.Equal(0, b.FrontBraceLength);
        Assert.Equal(0, b.RearBraceLength);
        Assert.Equal(BifilaRodLine.None, b.RodLine);
        Assert.Empty(b.RodSpans);
        Assert.Null(generico.Frame.PillarSpans);
        Assert.Null(generico.Frame.MinEmbedment);
        Assert.Contains(Tr.T("sem mão francesa"), b.Describe(), StringComparison.Ordinal);

        // Com o enterro de 0,90 do projeto, o pilar não leva os 2,20 da Arctech.
        Assert.Equal(0.9, generico.PillarsAtBasicHeight(Projeto(0.30) with { MinEmbedment = 0.9 }).Embedment, 12);
    }

    // ------------------------------------------------------------- apoio

    private static SystemConfiguration Projeto(double pontaBaixaMinima) =>
        SystemConfiguration.Default with
        {
            MinLowEdge = pontaBaixaMinima,
            MaxLowEdge = Math.Max(1.5, pontaBaixaMinima),
            MinEmbedment = 2.2,
            MaxEmbedment = 2.5,
        };

    /// <summary>
    /// A fileira inteira pelo motor (<see cref="RowPipeline.ProcessRow"/>) num
    /// terreno plano na cota 700: as pontas baixas que ele escolheu, acima do
    /// chão, e os pilares que ele calculou.
    /// </summary>
    private static (IReadOnlyList<double> PontasBaixas, IReadOnlyList<PillarResult> Pilares) PeloMotor(TableProfile perfil, SystemConfiguration projeto)
    {
        var geo = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);

        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };
        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, 8, 0.5,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(perfil.TiltRadians)), projeto.UpslopeAzimuthRadians);

        Point3 P(double x, double y) => new(x, y, 700);
        var plano = new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);

        var fila = RowPipeline.ProcessRow(layout.Rows[1], geo, perfil.TiltRadians, plano, ProjectSettings.Default with { Configuration = projeto });

        Assert.NotEmpty(fila.Tables);

        var pontasBaixas = fila.Tables.SelectMany(t => new[] { t.Solved.StartElevation - 700, t.Solved.EndElevation - 700 }).ToList();
        var pilares = fila.Tables.SelectMany(t => t.Pillars.Pillars).ToList();

        Assert.NotEmpty(pilares);
        return (pontasBaixas, pilares);
    }
}
