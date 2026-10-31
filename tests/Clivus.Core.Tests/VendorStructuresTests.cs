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

    /// <summary>A estrutura de fabricante é a mesa de referência com outro nome e, desde o 38.9, o P1 e o P2 do catálogo.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void AEstruturaDeFabricanteEhAMesaDeReferenciaComOutroNomeEOsPilaresDoCatalogo()
    {
        var gc800 = VendorStructures.All.Single(e => e.Model.Contains("GC800", StringComparison.Ordinal));
        var referencia = BifilaReference.Profile(BifilaVariant.Gc800);
        var comPilares = referencia.Frame with { Bifila = referencia.Frame.Bifila! with { FrontPillar = 3.059, RearPillar = 3.888 } };

        Assert.Equal(referencia with { Name = gc800.Profile.Name, Frame = comPilares }, gc800.Profile);
        Assert.Equal(3.059, gc800.CatalogP1, 12);
        Assert.Equal(3.888, gc800.CatalogP2, 12);
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

    /// <summary>A mesa sem o P1 e o P2 (a altura da faixa do projeto, como nos perfis de antes do 38.9).</summary>
    private static TableProfile SemPilares(TableProfile perfil) =>
        perfil with { Frame = perfil.Frame with { Bifila = perfil.Frame.Bifila! with { FrontPillar = null, RearPillar = null } } };

    /// <summary>
    /// Sem o P1 e o P2, os pilares da janela são os que o motor inteiro
    /// desenha no chão plano, com a ponta baixa fora da grade inclusive.
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [MemberData(nameof(Bipostes))]
    public void SemP1OP1EOP2SaoOsDoMotorNoChaoPlano(string mesa, double minima)
    {
        var perfil = SemPilares(mesa == "Genérico"
            ? BifilaReference.Generic()
            : VendorStructures.All.Single(e => e.Model.Contains(mesa + "MM", StringComparison.Ordinal)).Profile);
        var projeto = Projeto(minima);

        var basica = perfil.PillarsAtBasicHeight(projeto);
        var (pontasBaixas, pilares) = PeloMotor(perfil, projeto);

        Assert.All(pontasBaixas, pb => Assert.Equal(basica.LowEdge, pb, 6));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Front), p => Assert.Equal(basica.P1, p.Length!.Value, Motor));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Rear), p => Assert.Equal(basica.P2!.Value, p.Length!.Value, Motor));
        Assert.Contains(pilares, p => p.Line == PillarLine.Rear);
        Assert.True(basica.P2 > basica.P1);
    }

    /// <summary>
    /// Com o P1 (38.9), a altura é da estrutura, qualquer que seja a faixa do
    /// projeto: o motor inteiro assenta a mesa no chão plano na ponta baixa
    /// que o P1 dá (subida para a grade de 1 cm, nunca abaixo), e os pilares
    /// saem o P1 e o P2 de chão plano, com menos de 1 cm a mais. Com a faixa de
    /// 0,30 a 0,80 (abaixo do P1), a mesa não fica sem folga.
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("GC800", 0.30, 0.80)]
    [InlineData("GC800", 0.84, 1.60)]
    [InlineData("GC1000", 0.30, 0.80)]
    [InlineData("Genérico", 1.50, 2.00)]
    public void ComP1OMotorAssentaAMesaNaAlturaDaEstrutura(string mesa, double minima, double maxima)
    {
        var perfil = mesa == "Genérico"
            ? BifilaReference.Generic()
            : VendorStructures.All.Single(e => e.Model.Contains(mesa + "MM", StringComparison.Ordinal)).Profile;
        var projeto = Projeto(minima) with { MaxLowEdge = maxima };

        var daEstrutura = perfil.StructureLowEdge!.Value;
        var basica = perfil.PillarsAtBasicHeight(projeto);
        var (pontasBaixas, pilares) = PeloMotor(perfil, projeto);

        Assert.Equal(perfil.Frame.Bifila!.FrontPillar!.Value, basica.P1, Motor);
        Assert.Equal(daEstrutura, basica.LowEdge, Motor);
        Assert.All(pontasBaixas, pb => Assert.InRange(pb, daEstrutura - 1e-9, daEstrutura + 0.01));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Front), p => Assert.InRange(p.Length!.Value, basica.P1 - 1e-9, basica.P1 + 0.01));
        Assert.All(pilares.Where(p => p.Line == PillarLine.Rear), p => Assert.InRange(p.Length!.Value, basica.P2!.Value - 1e-9, basica.P2!.Value + 0.01));
    }

    /// <summary>
    /// O P1 de catálogo dá a altura livre do fabricante, e a folga do P2 de
    /// catálogo é o declive dele: 10 % na GC800 e 20 % na GC1000.
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("GC800", 0.800, 0.10)]
    [InlineData("GC1000", 1.000, 0.20)]
    public void OP1DaAAlturaLivreEOP2ODecliveDoCatalogo(string modelo, double livre, double declive)
    {
        var estrutura = VendorStructures.All.Single(e => e.Model.Contains(modelo + "MM", StringComparison.Ordinal));
        var perfil = estrutura.Profile;
        var geometria = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);

        Assert.Equal(livre + perfil.Layout.Module.Thickness * Math.Cos(perfil.TiltRadians), perfil.StructureLowEdge!.Value, Ouro);
        Assert.Equal(declive, StructureHeight.RearSlopeAllowance(geometria, perfil.TiltRadians)!.Value, 0.001);

        // A volta: o P1 que a ponta baixa dele dá é ele mesmo, e o P2 de chão
        // plano é o da conta da janela.
        var noPlano = perfil.PillarsOnFlatGround(perfil.StructureLowEdge!.Value, perfil.Frame.MinEmbedment!.Value);
        Assert.Equal(estrutura.CatalogP1, noPlano.P1, Motor);
        Assert.Equal(StructureHeight.FlatRearPillar(geometria, perfil.TiltRadians)!.Value, noPlano.P2!.Value, Motor);
    }

    /// <summary>
    /// A faixa da ponta baixa de uma mesa com P1: a mínima é a do P1; a máxima,
    /// a do projeto, mas nunca com menos folga que a faixa do projeto tem. Sem
    /// P1, a do projeto (com o T3 da mesa).
    /// </summary>
    [Theory]
    [Trait("Etapa", "38")]
    [InlineData(0.30, 0.80, 0.50)]
    [InlineData(0.30, 1.00, 0.70)]
    [InlineData(0.84, 1.60, -1)]
    [InlineData(1.20, 1.30, -1)]
    public void AFaixaDaMesaComP1(double minima, double maxima, double folga)
    {
        var perfil = VendorStructures.All[0].Profile;
        var geometria = TableGeometry.Local(perfil.Layout, perfil.Frame.Pillars(perfil.Layout), perfil.Frame);
        var projeto = Projeto(minima) with { MaxLowEdge = maxima };

        var daMesa = projeto.ForStructure(geometria, perfil.TiltRadians);
        var p1 = perfil.StructureLowEdge!.Value;

        Assert.Equal(p1, daMesa.MinLowEdge, 12);
        Assert.Equal(folga < 0 ? maxima : p1 + folga, daMesa.MaxLowEdge, 9);
        Assert.Null(daMesa.WhyInvalid);

        var semP1 = SemPilares(perfil);
        var geometriaSemP1 = TableGeometry.Local(semP1.Layout, semP1.Frame.Pillars(semP1.Layout), semP1.Frame);
        Assert.Equal(projeto.WithEmbedmentOf(semP1.Frame), projeto.ForStructure(geometriaSemP1, semP1.TiltRadians));
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
    /// O genérico é para montar o seu (38.9: "é PILAR P1, Mão francesa MF1,
    /// pilar P2, Mão francesa MF2, aí tem as terças, as tesouras"): sem
    /// fabricante no nome, com as peças do pórtico de referência (P1 e P2 de
    /// chão plano, sem folga de declive, o enterro e as duas mãos francesas),
    /// sem tirante e pelo vão-alvo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OBiposteGenericoTemAsPecasDoPorticoESemFabricante()
    {
        var generico = BifilaReference.Generic();
        var b = generico.Frame.Bifila!;
        var geometria = TableGeometry.Local(generico.Layout, generico.Frame.Pillars(generico.Layout), generico.Frame);

        Assert.Null(generico.WhyInvalid);
        Assert.Equal("Biposte", generico.Name);
        Assert.DoesNotContain(VendorStructures.All, e => generico.Name.Contains(e.Manufacturer, StringComparison.Ordinal));
        Assert.Equal(3.059, b.FrontPillar);
        Assert.Equal(3.588, b.RearPillar);
        Assert.Equal(2.2, generico.Frame.MinEmbedment);
        Assert.Equal(1.244, b.FrontBraceLength);
        Assert.Equal(1.209, b.RearBraceLength);
        Assert.Equal(BifilaRodLine.None, b.RodLine);
        Assert.Empty(b.RodSpans);
        Assert.Null(generico.Frame.PillarSpans);
        Assert.Equal(0, StructureHeight.RearSlopeAllowance(geometria, generico.TiltRadians)!.Value, 3);
    }

    /// <summary>
    /// O P1 e o P2 que não fecham: um sem o outro, o P2 menor que o P1, o P1
    /// sem o T3, o P1 que encosta o módulo no chão e o P2 que não alcança a
    /// viga. Cada um com o motivo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OP1EOP2QueNaoFechamSaoRecusadosComOMotivo()
    {
        var gc800 = VendorStructures.All[0].Profile;
        TableProfile Com(double? p1, double? p2) =>
            gc800 with { Frame = gc800.Frame with { Bifila = gc800.Frame.Bifila! with { FrontPillar = p1, RearPillar = p2 } } };

        Assert.Contains("juntos", Com(3.059, null).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("juntos", Com(null, 3.888).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("maior que o P1", Com(3.059, 3.0).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("T3", (Com(3.059, 3.888) with { Frame = Com(3.059, 3.888).Frame with { MinEmbedment = null } }).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("encosta no chão", Com(2.0, 3.0).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("não alcança", Com(3.059, 3.5).WhyInvalid, StringComparison.Ordinal);
        Assert.Null(Com(3.059, 3.588).WhyInvalid);
        Assert.Null(Com(3.2, 4.5).WhyInvalid);
    }

    /// <summary>O perfil com o P1 e o P2 sai na versão 5 e volta igual; sem eles, na 4, como antes.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OPerfilComP1EP2VaiNaVersao5EVoltaIgual()
    {
        var comPilares = VendorStructures.All[0].Profile;
        var semPilares = SemPilares(comPilares);

        var json = comPilares.ToJson();
        Assert.Contains("\"formatVersion\": 5", json, StringComparison.Ordinal);
        Assert.Contains("\"frontPillar\": 3.059", json, StringComparison.Ordinal);
        Assert.Contains("\"rearPillar\": 3.888", json, StringComparison.Ordinal);
        Assert.Equal(comPilares, TableProfile.Parse(json));

        var antigo = semPilares.ToJson();
        Assert.Contains("\"formatVersion\": 4", antigo, StringComparison.Ordinal);
        Assert.DoesNotContain("frontPillar", antigo, StringComparison.Ordinal);
        Assert.Equal(semPilares, TableProfile.Parse(antigo));

        // Mexido à mão: a 4 com os pilares, a 5 sem eles.
        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json.Replace("\"formatVersion\": 5", "\"formatVersion\": 4", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(antigo.Replace("\"formatVersion\": 4", "\"formatVersion\": 5", StringComparison.Ordinal)));
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
