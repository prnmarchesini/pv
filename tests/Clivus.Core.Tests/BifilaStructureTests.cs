using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// As peças da bifila em 3D (passo 37.3): vigas, mãos francesas, terças e
/// tirantes, a partir da mesa assentada e dos pilares calculados.
///
/// O ouro: a mão francesa sai com o comprimento da peça de catálogo (é ela
/// que se compra), a viga com a tesoura, a terça com a mesa; e cada ponta
/// encosta onde deve (o pé da mão francesa no eixo do pilar, a cabeça no eixo
/// da viga).
/// </summary>
public class BifilaStructureTests
{
    private const double Grau = Math.PI / 180;

    private static SystemConfiguration Config() =>
        SystemConfiguration.Default with { MinEmbedment = 2.2, MaxEmbedment = 2.2, MaxLowEdge = 1.5 };

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));

        return new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);
    }

    private sealed record Montada(TableProfile Perfil, TableGeometry Geo, TablePillars Pilares, BifilaStructureResult Estrutura);

    private static Montada Montar(TableProfile p, double pb, double cotaInicial = 0, double cotaFinal = 0)
    {
        var geo = TableGeometry.Local(p.Layout, p.Frame.Pillars(p.Layout), p.Frame);
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };
        var layout = RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, 8, 0.5,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(p.TiltRadians)), Config().UpslopeAzimuthRadians);
        var cell = layout.Rows[1].Tables[1];
        var orientacao = RowOrientation.Resolve(cell, Config().UpslopeAzimuthRadians);

        var pilares = PillarCalculator.Compute(
            geo, cell, orientacao, p.TiltRadians,
            new SolvedTable("F2.2", 700 + pb + cotaInicial, 700 + pb + cotaFinal, 0, false, null),
            Plano((_, _) => 700), Config());

        return new Montada(p, geo, pilares, BifilaStructure.Build(p, geo, pilares));
    }

    private static Montada Gc800(double pb = 0.832) => Montar(BifilaReference.Profile(BifilaVariant.Gc800), pb);

    private static double Dist(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>A distância do ponto à reta de a a b.</summary>
    private static double DistanciaAReta(Point3 p, Point3 a, Point3 b)
    {
        var (dx, dy, dz) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var (px, py, pz) = (p.X - a.X, p.Y - a.Y, p.Z - a.Z);
        var (cx, cy, cz) = (py * dz - pz * dy, pz * dx - px * dz, px * dy - py * dx);

        return Math.Sqrt(cx * cx + cy * cy + cz * cz) / Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // ------------------------------------------------------------- contagem

    /// <summary>A 2P14: 6 vigas, 12 mãos francesas, 4 terças e 4 tirantes (X nos vãos 2 e 4).</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void A2P14TemAsPecasDoDesenho()
    {
        var m = Gc800();
        var pecas = m.Estrutura.Bars;

        Assert.Empty(m.Estrutura.Warnings);
        Assert.Equal(6, pecas.Count(b => b.Part == StructurePart.Beam));
        Assert.Equal(12, pecas.Count(b => b.Part == StructurePart.Brace));
        Assert.Equal(6, pecas.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
        Assert.Equal(4, pecas.Count(b => b.Part == StructurePart.Purlin));
        Assert.Equal(4, pecas.Count(b => b.Part == StructurePart.Rod));
        Assert.All(pecas.Where(b => b.Part == StructurePart.Rod), b => Assert.Equal(PillarLine.Rear, b.Line));
        Assert.Equal([2, 2, 4, 4], pecas.Where(b => b.Part == StructurePart.Rod).Select(b => b.Index).Order());
    }

    // ------------------------------------------------------------------ ouro

    [Fact]
    [Trait("Etapa", "37")]
    public void VigaTercaEMaoFrancesaSaemComOComprimentoDaPeca()
    {
        var m = Gc800();
        var pecas = m.Estrutura.Bars;

        Assert.All(pecas.Where(b => b.Part == StructurePart.Beam), b => Assert.Equal(4.030, b.Length, 9));
        Assert.All(pecas.Where(b => b.Part == StructurePart.Purlin), b => Assert.Equal(18.602, b.Length, 9));
        Assert.All(pecas.Where(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front), b => Assert.Equal(1.244, b.Length, 9));
        Assert.All(pecas.Where(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Rear), b => Assert.Equal(1.209, b.Length, 9));
    }

    /// <summary>
    /// O pé da mão francesa no eixo do pilar, abaixo do topo dele; a cabeça
    /// no eixo da viga do mesmo pórtico, do lado do outro pilar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMaoFrancesaPegaNoPilarENaViga()
    {
        var m = Gc800();
        var b = m.Perfil.Frame.Bifila!;
        var vigas = m.Estrutura.Bars.Where(x => x.Part == StructurePart.Beam).ToList();

        foreach (var mao in m.Estrutura.Bars.Where(x => x.Part == StructurePart.Brace))
        {
            var pilar = m.Pilares.Pillars[2 * mao.Index + (mao.Line == PillarLine.Front ? 0 : 1)];
            var abaixo = mao.Line == PillarLine.Front ? b.FrontBraceBelowTop : b.RearBraceBelowTop;

            Assert.Equal(pilar.X, mao.Start.X, 9);
            Assert.Equal(pilar.Y, mao.Start.Y, 9);
            Assert.Equal(pilar.TopZ - abaixo, mao.Start.Z, 9);

            var viga = vigas[mao.Index];
            Assert.True(DistanciaAReta(mao.End, viga.Start, viga.End) < 1e-9);

            // Do lado do outro pilar: a dianteira sobe para trás, a traseira desce para a frente.
            var outro = m.Pilares.Pillars[2 * mao.Index + (mao.Line == PillarLine.Front ? 1 : 0)];
            Assert.True(Dist(mao.End, new Point3(outro.X, outro.Y, mao.End.Z)) < Dist(mao.Start, new Point3(outro.X, outro.Y, mao.Start.Z)));
        }
    }

    /// <summary>
    /// A cabeça da mão francesa dianteira a ~1,08 m do pilar na horizontal: é
    /// onde ela encosta na viga no corte da Arctech (1,078; 3 cm de folga,
    /// porque o desenho prende o pé na face do pilar e o Clivus no eixo).
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMaoFrancesaDianteiraEncostaNaVigaOndeODesenhoMostra()
    {
        var m = Montar(BifilaReference.Profile(BifilaVariant.Gc1000), 1.032);
        var mao = m.Estrutura.Bars.First(x => x.Part == StructurePart.Brace && x.Line == PillarLine.Front);

        var horizontal = Math.Sqrt((mao.End.X - mao.Start.X) * (mao.End.X - mao.Start.X) + (mao.End.Y - mao.Start.Y) * (mao.End.Y - mao.Start.Y));

        Assert.InRange(horizontal, 1.05, 1.11);
    }

    /// <summary>
    /// A viga e a terça abaixo dos módulos: o eixo da terça a espessura do
    /// módulo + meia terça da face; o da viga, mais a terça inteira e meia viga.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void TercaEVigaFicamDebaixoDosModulos()
    {
        var m = Gc800();
        var normal = Normal(m.Pilares.Placement);
        var origem = m.Pilares.Placement.Apply(new Point3(0, 0, 0));

        double Abaixo(Point3 p) => -((p.X - origem.X) * normal.X + (p.Y - origem.Y) * normal.Y + (p.Z - origem.Z) * normal.Z);

        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Purlin), t => Assert.Equal(0.033 + 0.050, Abaixo(t.Start), 9));
        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Beam), v => Assert.Equal(0.033 + 0.100 + 0.040, Abaixo(v.Start), 9));

        // As terças a 0,492 da borda de cada fileira de módulos: 0,492 / 1,892 / 2,896 / 4,296 da ponta baixa.
        var local = m.Estrutura.Bars.Where(x => x.Part == StructurePart.Purlin)
            .Select(t => LocalY(m.Pilares.Placement, t.Start))
            .ToList();
        Assert.Equal([0.492, 1.892, 2.896, 4.296], local.Select(y => Math.Round(y, 9)));
        Assert.Equal([0, 1, 2, 3], m.Estrutura.Bars.Where(x => x.Part == StructurePart.Purlin).Select(t => t.Index));
    }

    /// <summary>No chão plano com a mesa nivelada, o tirante liga 0,20 de um pilar a 0,60 do outro: √(3,5² + 0,4²).</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OTiranteLigaOsPontosDosDoisPilares()
    {
        var m = Gc800();

        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Rod), t => Assert.Equal(Math.Sqrt(3.5 * 3.5 + 0.4 * 0.4), t.Length, 6));
    }

    // ---------------------------------------------------------------- avisos

    /// <summary>
    /// PB baixa demais para a GC800: o pilar dianteiro fica com 0,36 m acima do
    /// chão e a mão francesa pegaria 0,447 abaixo do topo, dentro da terra.
    /// Avisa, e não desenha a peça torta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void MaoFrancesaQuePegariaDentroDoChaoViraAviso()
    {
        var m = Gc800(pb: 0.33);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("chão"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
        Assert.Equal(6, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Rear));
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void MaoFrancesaQueNaoAlcancaAVigaViraAviso()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        p = p with { Frame = p.Frame with { Bifila = p.Frame.Bifila! with { FrontBraceLength = 0.30 } } };

        var m = Montar(p, 0.832);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("viga"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
    }

    /// <summary>Um aviso por motivo, e não um por pórtico: a janela de relatório não pode encher.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OAvisoNaoSeRepetePorPortico()
    {
        var m = Gc800(pb: 0.33);

        Assert.Single(m.Estrutura.Warnings);
    }

    private static TableProfile ComMaoDianteira(double comprimento, double abaixo)
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        return p with { Frame = p.Frame with { Bifila = p.Frame.Bifila! with { FrontBraceLength = comprimento, FrontBraceBelowTop = abaixo } } };
    }

    /// <summary>
    /// Peça curta que ainda alcança a viga, mas encosta colada no pilar
    /// (quase em pé): na revisão, 0,395 e 0,40 pegando 0,447 abaixo do topo
    /// saíam sem aviso, com a cabeça dentro da seção do pilar.
    /// </summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData(0.395)]
    [InlineData(0.40)]
    public void MaoFrancesaColadaNoPilarViraAviso(double comprimento)
    {
        var m = Montar(ComMaoDianteira(comprimento, 0.447), 0.832);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("colada") || a.Contains("não alcança"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
    }

    /// <summary>Peça comprida demais: passa do outro pilar (3,3 m) ou da ponta da viga (4,9 m), e o aviso manda diminuir.</summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData(3.3)]
    [InlineData(4.9)]
    public void MaoFrancesaCompridaDemaisViraAvisoDeDiminuir(double comprimento)
    {
        var m = Montar(ComMaoDianteira(comprimento, 0.447), 0.832);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("diminua"));
        Assert.DoesNotContain(m.Estrutura.Warnings, a => a.Contains("aumente"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
    }

    /// <summary>O pé acima do fundo da viga (0,02 abaixo do topo): a peça sairia deitada ao longo da viga.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void MaoFrancesaQuePegaAcimaDaVigaViraAviso()
    {
        var m = Montar(ComMaoDianteira(1.244, 0.02), 0.832);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("fundo da viga"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front));
    }

    /// <summary>A cabeça entre a face deste pilar e a do outro, em planta, em toda mão francesa que sai.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void ACabecaFicaEntreOsDoisPilares()
    {
        var m = Gc800();

        foreach (var mao in m.Estrutura.Bars.Where(x => x.Part == StructurePart.Brace))
        {
            var este = m.Pilares.Pillars[2 * mao.Index + (mao.Line == PillarLine.Front ? 0 : 1)];
            var outro = m.Pilares.Pillars[2 * mao.Index + (mao.Line == PillarLine.Front ? 1 : 0)];
            var (dx, dy) = (outro.X - este.X, outro.Y - este.Y);
            var vao = Math.Sqrt(dx * dx + dy * dy);
            var avanco = ((mao.End.X - este.X) * dx + (mao.End.Y - este.Y) * dy) / vao;

            Assert.InRange(avanco, 0.095 / 2, vao - 0.095 / 2);
        }
    }

    /// <summary>Pilares que não são os da geometria: nada sai, e isso é dito.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void PilaresQueNaoBatemComAGeometriaViramAviso()
    {
        var m = Gc800();
        var poucos = m.Pilares with { Pillars = m.Pilares.Pillars.Take(3).ToList() };

        var r = BifilaStructure.Build(m.Perfil, m.Geo, poucos);

        Assert.Empty(r.Bars);
        Assert.Single(r.Warnings);
    }

    /// <summary>Pilar sem terreno: as peças saem, mas o aviso diz que o chão não foi conferido.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void PilarSemTerrenoViraAviso()
    {
        var m = Gc800();
        var semChao = m.Pilares with { Pillars = m.Pilares.Pillars.Select(p => p with { GroundZ = null }).ToList() };

        var r = BifilaStructure.Build(m.Perfil, m.Geo, semChao);

        Assert.NotEmpty(r.Bars);
        Assert.Contains(r.Warnings, a => a.Contains("sem terreno"));
    }

    /// <summary>Tirante nas duas linhas com a PB baixa: some só a linha da frente, e o aviso diz qual.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OAvisoDoTiranteDizALinha()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        p = p with { Frame = p.Frame with { Bifila = p.Frame.Bifila! with { RodLine = BifilaRodLine.Both } } };

        var m = Montar(p, 0.5);

        Assert.Contains(m.Estrutura.Warnings, a => a.Contains("dianteira") && a.Contains("tirante"));
        Assert.Equal(0, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Rod && b.Line == PillarLine.Front));
        Assert.Equal(4, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Rod && b.Line == PillarLine.Rear));
    }

    // -------------------------------------------------------------- variações

    [Fact]
    [Trait("Etapa", "37")]
    public void SemMaoFrancesaESemTiranteSoSobramVigaETerca()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        p = p with
        {
            Frame = p.Frame with
            {
                Bifila = p.Frame.Bifila! with { FrontBraceLength = 0, RearBraceLength = 0, RodLine = BifilaRodLine.None, RodSpans = [] },
            },
        };

        var m = Montar(p, 0.832);

        Assert.Equal(10, m.Estrutura.Bars.Count);
        Assert.Empty(m.Estrutura.Warnings);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void TiranteNasDuasLinhasDobra()
    {
        var p = BifilaReference.Profile(BifilaVariant.Gc800);
        p = p with { Frame = p.Frame with { Bifila = p.Frame.Bifila! with { RodLine = BifilaRodLine.Both } } };

        var m = Montar(p, 0.832);

        Assert.Equal(8, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Rod));
        Assert.Equal(4, m.Estrutura.Bars.Count(b => b.Part == StructurePart.Rod && b.Line == PillarLine.Front));
    }

    /// <summary>A monoposte não tem estrutura bifila: nada sai, e nada é inventado.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMonoposteNaoTemPecasDeBifila()
    {
        var mesa = new TableLayout(new SolarModule("Risen", "RSM", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);
        var p = new TableProfile("mono", mesa, new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0), 10 * Grau);

        var m = Montar(p, 0.5);

        Assert.Empty(m.Estrutura.Bars);
        Assert.Empty(m.Estrutura.Warnings);
    }

    /// <summary>Mesa girada ao longo da fileira: a viga continua com 4,030 e a terça com o comprimento da mesa (peça rígida).</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void NaMesaGiradaAsPecasRigidasNaoMudam()
    {
        var m = Montar(BifilaReference.Profile(BifilaVariant.Gc800), 0.832, 0, 0.9);

        Assert.NotEqual(0, m.Pilares.LongitudinalTiltRadians);
        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Beam), b => Assert.Equal(4.030, b.Length, 9));
        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Purlin), b => Assert.Equal(18.602, b.Length, 9));
        Assert.All(m.Estrutura.Bars.Where(x => x.Part == StructurePart.Brace && x.Line == PillarLine.Front), b => Assert.Equal(1.244, b.Length, 9));
    }

    /// <summary>Toda barra com a direção da seção perpendicular ao eixo e unitária: é o que o sólido do CAD precisa.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void ADirecaoDaSecaoEPerpendicularAoEixo()
    {
        var m = Montar(BifilaReference.Profile(BifilaVariant.Gc800), 0.832, 0, 0.9);

        Assert.All(m.Estrutura.Bars, b =>
        {
            var (ex, ey, ez) = ((b.End.X - b.Start.X) / b.Length, (b.End.Y - b.Start.Y) / b.Length, (b.End.Z - b.Start.Z) / b.Length);
            var d = b.DepthDirection;
            Assert.Equal(1, Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z), 9);
            Assert.Equal(0, ex * d.X + ey * d.Y + ez * d.Z, 9);
        });
    }

    /// <summary>A coordenada local Y (na inclinação) de um ponto do mundo, pela transformação rígida.</summary>
    private static double LocalY(Transform t, Point3 p)
    {
        var o = t.Apply(new Point3(0, 0, 0));
        var y = t.Apply(new Point3(0, 1, 0));
        return (p.X - o.X) * (y.X - o.X) + (p.Y - o.Y) * (y.Y - o.Y) + (p.Z - o.Z) * (y.Z - o.Z);
    }

    private static Point3 Normal(Transform t)
    {
        var o = t.Apply(new Point3(0, 0, 0));
        var z = t.Apply(new Point3(0, 0, 1));
        return new Point3(z.X - o.X, z.Y - o.Y, z.Z - o.Z);
    }
}
