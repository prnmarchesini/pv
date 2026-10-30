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
/// motor (o mesmo <see cref="PillarCalculator"/> que desenha os pilares) e
/// aos números de catálogo da Arctech.
/// </summary>
public class VendorStructuresTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>A tolerância dos números de catálogo: o PDF dá 1,0589 e a conta, 1,0588.</summary>
    private const double Ouro = 0.002;

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
            Assert.False(string.IsNullOrWhiteSpace(e.Source));
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

    public static TheoryData<string> Modelos() => new() { "GC800", "GC1000" };

    /// <summary>
    /// Com a ponta baixa que dá a altura livre do fabricante, o P1 da janela é
    /// o pilar dianteiro do catálogo, e o P2 de catálogo é o da janela mais a
    /// folga do declive para o qual ele foi feito (3,00 m × 10 % = 0,300).
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [MemberData(nameof(Modelos))]
    public void OsPilaresDoCatalogoFechamComAAlturaBasica(string modelo)
    {
        var estrutura = VendorStructures.All.Single(e => e.Model.Contains(modelo + "MM", StringComparison.Ordinal));
        var basica = estrutura.Profile.PillarsAtBasicHeight(Projeto(estrutura.LowEdge));

        Assert.Equal(estrutura.CatalogP1, basica.P1, Ouro);
        Assert.Equal(estrutura.CatalogP2, basica.P2!.Value + estrutura.Profile.BifilaHorizontalSpacing!.Value * estrutura.CatalogSlope, Ouro);
        Assert.Equal(2.2, basica.Embedment, 12);
    }

    // ------------------------------------------------------- altura básica

    public static TheoryData<BifilaVariant> Variantes() => new() { BifilaVariant.Gc800, BifilaVariant.Gc1000 };

    /// <summary>A conta da janela dá os mesmos pilares que o motor no chão plano, na ponta baixa mínima.</summary>
    [Theory]
    [Trait("Etapa", "38")]
    [MemberData(nameof(Variantes))]
    public void NoBiposteOP1EOP2SaoOsDoMotorNoChaoPlano(BifilaVariant variante)
    {
        var perfil = BifilaReference.Profile(variante);
        var projeto = Projeto(0.8325);

        var basica = perfil.PillarsAtBasicHeight(projeto);
        var motor = PilaresDoMotor(perfil, projeto);

        Assert.Equal(0.8325, basica.LowEdge, 12);
        Assert.All(motor.Pillars.Where(p => p.Line == PillarLine.Front), p => Assert.Equal(p.Length!.Value, basica.P1, 3));
        Assert.All(motor.Pillars.Where(p => p.Line == PillarLine.Rear), p => Assert.Equal(p.Length!.Value, basica.P2!.Value, 3));
        Assert.True(basica.P2 > basica.P1);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void NaMonoposteSoHaOP1EEleEhODoMotor()
    {
        var modulo = new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033);
        var perfil = new TableProfile(
            "Mesa 28 módulos",
            new TableLayout(modulo, 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
            10 * Grau);
        var projeto = Projeto(0.5);

        var basica = perfil.PillarsAtBasicHeight(projeto);
        var motor = PilaresDoMotor(perfil, projeto);

        Assert.Null(basica.RearFree);
        Assert.Null(basica.P2);
        Assert.All(motor.Pillars, p => Assert.Equal(p.Length!.Value, basica.P1, 3));
    }

    /// <summary>Sem T3 na mesa, o enterro é o mínimo do projeto; com T3, o da mesa.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OEnterroEhOT3DaMesaOuOMinimoDoProjeto()
    {
        var comT3 = BifilaReference.Profile(BifilaVariant.Gc800);
        var semT3 = comT3 with { Frame = comT3.Frame with { MinEmbedment = null } };
        var projeto = Projeto(0.8325) with { MinEmbedment = 1.5, MaxEmbedment = 3.0 };

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
    }

    // ----------------------------------------------------------- genérico

    /// <summary>
    /// O genérico é para montar o seu: sem fabricante no nome, sem mão
    /// francesa, sem tirante e pelo vão-alvo, e ainda assim uma mesa que fecha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OBiposteGenericoNaoTemNadaDeFabricante()
    {
        var generico = BifilaReference.Generic();
        var b = generico.Frame.Bifila!;

        Assert.Null(generico.WhyInvalid);
        Assert.Equal("Biposte", generico.Name);
        Assert.All(VendorStructures.All, e => Assert.DoesNotContain(e.Manufacturer, generico.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, b.FrontBraceLength);
        Assert.Equal(0, b.RearBraceLength);
        Assert.Equal(BifilaRodLine.None, b.RodLine);
        Assert.Empty(b.RodSpans);
        Assert.Null(generico.Frame.PillarSpans);
        Assert.Contains(Tr.T("sem mão francesa"), b.Describe(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- apoio

    private static SystemConfiguration Projeto(double pontaBaixaMinima) =>
        SystemConfiguration.Default with
        {
            MinLowEdge = pontaBaixaMinima,
            MaxLowEdge = Math.Max(1.5, pontaBaixaMinima),
            MinEmbedment = 2.2,
            MaxEmbedment = 2.2,
        };

    /// <summary>Os pilares que o motor calcula com a mesa assentada no chão plano, na ponta baixa mínima do projeto.</summary>
    private static TablePillars PilaresDoMotor(TableProfile perfil, SystemConfiguration projeto)
    {
        var geo = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);
        var daMesa = projeto.WithEmbedmentOf(perfil.Frame);

        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };
        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, 8, 0.5,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(perfil.TiltRadians)), daMesa.UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[1];
        var orientacao = RowOrientation.Resolve(cell, daMesa.UpslopeAzimuthRadians);

        var cota = 700 + projeto.MinLowEdge;
        Point3 P(double x, double y) => new(x, y, 700);
        var plano = new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);

        var pilares = PillarCalculator.Compute(
            geo, cell, orientacao, perfil.TiltRadians, new SolvedTable("F2.2", cota, cota, 0, false, null), plano, daMesa);

        Assert.True(pilares.AllSound);
        return pilares;
    }
}
