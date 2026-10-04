using Clivus.Core.Invariants;
using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A costura dos passos 5.1 a 5.6 numa fileira: da célula ao relatório. O
/// que se trava é a ordem e a coerência entre os passos, em terrenos onde a
/// resposta de cada um já é conhecida.
/// </summary>
public class RowPipelineTests
{
    private const double Grau = Math.PI / 180;
    private const double Tilt = 20 * Grau;

    private static Tin Plano(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));

        return new Tin(
        [
            new Triangle(P(-100, -100), P(400, -100), P(400, 400)),
            new Triangle(P(-100, -100), P(400, 400), P(-100, 400)),
        ]);
    }

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static ProjectSettings Settings() => ProjectSettings.Default;

    /// <summary>
    /// Um retângulo de 200 × 60 com a linha de alinhamento norte-sul na borda
    /// oeste e as fileiras à direita dela: nascem em y = 0, 6, 12… e correm
    /// 200 m para o leste.
    /// </summary>
    private static PlanLayout Layout(TableGeometry geo, double? gap = null)
    {
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 60, 0), new Point3(0, 60, 0) };
        var alinhamento = new[] { new Point3(0, 0, 0), new Point3(0, 60, 0) };

        return RowDistributor.Distribute(
            area, alinhamento, LineSide.Right, Settings().Configuration.Pitch, gap ?? Settings().Configuration.TableGap,
            new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)), Settings().Configuration.UpslopeAzimuthRadians);
    }

    /// <summary>
    /// Terreno plano em 700: toda mesa cabe, nenhuma marcada, degrau zero,
    /// todo pilar de pé com o mesmo comprimento, nada pintado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NoPlanoTudoCabeENadaEPintado()
    {
        var geo = Geometria();
        var layout = Layout(geo);
        var fileira = RowPipeline.ProcessRow(layout.Rows[0], geo, Tilt, Plano((_, _) => 700), Settings());

        Assert.Equal(layout.Rows[0].Tables.Count, fileira.Tables.Count);
        Assert.Equal(0, fileira.MarkedCount);
        Assert.Equal(0, fileira.PillarProblemCount);
        Assert.Empty(EqualTips.Check(fileira, Settings().Configuration));
        Assert.All(fileira.Tables, t => Assert.Equal(fileira.Tables[0].Solved.StartElevation, t.Solved.EndElevation, 9));

        foreach (var mesa in fileira.Tables)
        {
            Assert.True(mesa.Samples.IsComplete);
            Assert.False(mesa.Viable.IsEmpty);
            Assert.False(mesa.Solved.Marked);
            Assert.True(mesa.Pillars.AllSound);
            Assert.Equal(mesa.Cell.Label, mesa.Label);

            // Nada pintado, a não ser a borda: a última célula da fileira sai
            // da área (200 m não é múltiplo de 19,2) e é pintada como tal.
            if (mesa.Cell.PartlyOutside)
                Assert.All(mesa.Report.Painted, v => Assert.Equal(AnalysisKind.EdgeTable, v.Kind));
            else
                Assert.Empty(mesa.Report.Painted);

            foreach (var pilar in mesa.Pillars.Pillars)
            {
                Assert.Equal(700, pilar.GroundZ!.Value, 9);
                Assert.Null(FloatingPillar.Check(pilar.FreeHeight!.Value, pilar.Embedment));
            }
        }

        // Todas as mesas na mesma cota e com o mesmo pilar: o plano não dá motivo para diferença.
        var comprimentos = fileira.Tables.SelectMany(t => t.Pillars.Pillars).Select(p => Math.Round(p.Length!.Value, 6)).Distinct();
        Assert.Single(comprimentos);
    }

    /// <summary>
    /// Terreno subindo 4 % para o norte (perpendicular à fileira): as mesas
    /// de uma fileira estão todas na mesma cota de terreno, e a fileira sai
    /// nivelada. A mesa olha para o norte e sobe para o sul, contra o
    /// terreno, então os pilares de trás são mais curtos que os da frente
    /// — não: todos os pilares estão na mesma linha (PillarRow), então têm o
    /// mesmo comprimento. O que muda entre fileiras é a cota.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaTransversalDaFileirasNiveladasEmCotasDiferentes()
    {
        var geo = Geometria();
        var layout = Layout(geo);
        var terreno = Plano((_, y) => 700 + 0.04 * y);

        var primeira = RowPipeline.ProcessRow(layout.Rows[0], geo, Tilt, terreno, Settings());
        var segunda = RowPipeline.ProcessRow(layout.Rows[1], geo, Tilt, terreno, Settings());

        Assert.Equal(0, primeira.MarkedCount + segunda.MarkedCount);
        Assert.Equal(0, primeira.PillarProblemCount + segunda.PillarProblemCount);
        Assert.Empty(EqualTips.Check(primeira, Settings().Configuration));
        Assert.Empty(EqualTips.Check(segunda, Settings().Configuration));
        Assert.All(primeira.Tables, t => Assert.Equal(primeira.Tables[0].Solved.StartElevation, t.Solved.EndElevation, 9));

        // A segunda fileira está um pitch (6 m) ao norte: 0,24 m mais alta.
        var cota1 = primeira.Tables[0].Solved.StartElevation;
        var cota2 = segunda.Tables[0].Solved.StartElevation;
        Assert.InRange(cota2 - cota1, 0.24 - 0.02, 0.24 + 0.02);
    }

    /// <summary>
    /// Terreno subindo 5 % ao longo da fileira: as mesas inclinam com ele,
    /// sem marcar, e a fileira sobe pelas juntas. Os pilares de cada mesa
    /// reamostram o terreno na posição real.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RampaLongitudinalAcompanhaSemMarcar()
    {
        var geo = Geometria();
        var layout = Layout(geo);
        var terreno = Plano((x, _) => 700 + 0.05 * x);

        var fileira = RowPipeline.ProcessRow(layout.Rows[0], geo, Tilt, terreno, Settings());

        Assert.True(fileira.MarkedCount == 0, string.Join("; ", fileira.Tables.Where(t => t.Solved.Marked)
            .Select(t => $"{t.Label}: {t.Solved.Reason}")));
        Assert.Equal(0, fileira.PillarProblemCount);
        Assert.All(fileira.Tables, m => Assert.NotEqual(m.Solved.StartElevation, m.Solved.EndElevation));
        Assert.Empty(EqualTips.Check(fileira, Settings().Configuration));

        // A mesa olha para o norte: o comprimento local corre para oeste, e
        // com o terreno subindo para o leste a cota FINAL local é a mais
        // baixa. É o que o encadeamento invertido tem que respeitar.
        Assert.All(fileira.Tables, m => Assert.True(m.Solved.EndElevation < m.Solved.StartElevation));

        // E as juntas em planta fecham: a cota inicial local de uma mesa
        // (ponta leste) e a cota final local da mesa seguinte a leste (ponta
        // oeste dela) diferem no máximo um degrau.
        for (var i = 0; i + 1 < fileira.Tables.Count; i++)
        {
            var junta = Math.Abs(fileira.Tables[i].Solved.StartElevation - fileira.Tables[i + 1].Solved.EndElevation);
            Assert.True(junta <= Settings().Configuration.MaxStep + 1e-9, $"junta de {junta} m entre {fileira.Tables[i].Label} e {fileira.Tables[i + 1].Label}");
        }

        foreach (var pilar in fileira.Tables.SelectMany(t => t.Pillars.Pillars))
            Assert.Equal(700 + 0.05 * pilar.X, pilar.GroundZ!.Value, 6);

        // Uma fileira de 200 m sobe cerca de 0,05 × 200 = 10 m: da ponta
        // oeste da primeira célula (fim local) à ponta leste da última
        // (início local).
        var subida = fileira.Tables[^1].Solved.StartElevation - fileira.Tables[0].Solved.EndElevation;
        Assert.InRange(subida, 8, 11);

        // O relatório usa o terreno reamostrado na posição final: a folga
        // de cada módulo no relatório é a cota dele menos o plano no X e Y
        // reais, que diferem da amostra sem giro.
        foreach (var mesa in fileira.Tables)
        {
            Assert.NotEqual(mesa.Samples, mesa.FinalSamples);

            // O terreno da ponta baixa é o mais alto ao longo da aresta
            // (5.2): num plano que sobe para o leste, o da ponta leste do
            // módulo, meia largura (0,65 m) a leste do meio.
            foreach (var m in mesa.FinalSamples.LowEdge)
                Assert.InRange(m.GroundZ!.Value, 700 + 0.05 * m.X - 1e-6, 700 + 0.05 * (m.X + 0.66));
        }

        // Com o espaçamento padrão (0,50 m) o giro de 2,9° não faz as pontas
        // altas se cruzarem.
        Assert.Empty(fileira.Warnings);
    }

    /// <summary>
    /// Sem espaçamento entre mesas e com rampa forte ao longo da fileira, o
    /// giro desloca a ponta alta ao longo da fileira e as mesas se cruzam
    /// pela borda alta: o resultado avisa, sem marcar. Com 0,50 m de vão o
    /// aviso some.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PontasAltasQueSeCruzamPeloGiroSaoAvisadas()
    {
        var geo = Geometria();
        var terreno = Plano((x, _) => 700 + 0.15 * x);

        var semVao = RowPipeline.ProcessRow(Layout(geo, 0).Rows[0], geo, Tilt, terreno, Settings());
        Assert.NotEmpty(semVao.Warnings);
        Assert.Contains("se cruzarem", semVao.Warnings[0]);

        var comVao = RowPipeline.ProcessRow(Layout(geo, 0.5).Rows[0], geo, Tilt, terreno, Settings());
        Assert.Empty(comVao.Warnings);
    }

    /// <summary>
    /// Terreno que acaba no meio da fileira: as mesas fora do terreno saem
    /// marcadas (sem terreno), as de dentro inteiras, e todo pilar fora tem
    /// o problema escrito. Nada sai com cota inventada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesasForaDoTerrenoSaemMarcadasENaoInventadas()
    {
        var geo = Geometria();
        var layout = Layout(geo);

        Point3 P(double x, double y) => new(x, y, 700);
        var metade = new Tin(
        [
            new Triangle(P(-100, -100), P(100, -100), P(100, 400)),
            new Triangle(P(-100, -100), P(100, 400), P(-100, 400)),
        ]);

        var fileira = RowPipeline.ProcessRow(layout.Rows[0], geo, Tilt, metade, Settings());

        var dentro = fileira.Tables.Where(t => t.Cell.Origin.X + geo.Length < 100).ToList();
        var fora = fileira.Tables.Where(t => t.Cell.Origin.X > 100).ToList();

        Assert.NotEmpty(dentro);
        Assert.NotEmpty(fora);
        Assert.All(dentro, t => Assert.False(t.Solved.Marked));
        Assert.All(fora, t =>
        {
            Assert.True(t.Solved.Marked);
            Assert.Contains("sem terreno", t.Solved.Reason!);
            Assert.All(t.Pillars.Pillars, p => Assert.Contains("fora do terreno", p.Problem!));
        });
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraVaziaEConfiguracaoQuebradaSaoRecusadas()
    {
        var geo = Geometria();
        var layout = Layout(geo);

        Assert.Throws<ArgumentException>(() => RowPipeline.ProcessRow(
            new PlanRow(1, []), geo, Tilt, Plano((_, _) => 700), Settings()));

        var quebrada = Settings() with { Configuration = Settings().Configuration with { MinLowEdge = 9 } };

        Assert.Throws<InvalidOperationException>(() => RowPipeline.ProcessRow(
            layout.Rows[0], geo, Tilt, Plano((_, _) => 700), quebrada));
    }
}
