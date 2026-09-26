using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A orientação da mesa na fileira: qual dos dois sentidos da linha de
/// alinhamento é a subida, e a conversão, num lugar só, da direção
/// matemática da fileira para o azimute topográfico que o resto do Core fala.
///
/// Desde 26/09/2026 a fileira é PERPENDICULAR à linha de alinhamento: a
/// linha é o eixo transversal, e o fundo da célula corre ao longo dela.
/// </summary>
public class RowOrientationTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>
    /// Linha de alinhamento para o norte, fileiras à direita dela (para o
    /// leste, direção 0). A configuração padrão pede subida para o sul (mesa
    /// olha para o norte): a subida é −eixo da linha, azimute 180°, ponta
    /// baixa na borda de lá, e a linha está exatamente paralela ao azimute:
    /// divergência zero.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraParaOLesteComMesaOlhandoParaONorte()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(0, LineSide.Right, subida);

        Assert.Equal(180 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.False(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 9);

        // Mesa olhando para o norte: o +X local corre para oeste, contra a
        // fileira que vai para o leste.
        Assert.False(orientacao.LengthRunsWithRow);
    }

    /// <summary>Com a mesa olhando para o sul (subida para o norte), o +X local corre para o leste, com a fileira.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComAMesaOlhandoParaOSulOComprimentoCorreComAFileira()
    {
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, 0);

        Assert.True(orientacao.LengthRunsWithRow);
        Assert.True(orientacao.LowEdgeOnNearSide);
    }

    /// <summary>
    /// Linha para o sul com as fileiras à esquerda (também para o leste): a
    /// célula cresce para o sul, a subida pedida (sul) é +eixo, e a ponta
    /// baixa fica de cá.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComALinhaAoContrarioAPontaBaixaFicaDeCa()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(0, LineSide.Left, subida);

        Assert.Equal(180 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.True(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 9);
    }

    /// <summary>
    /// Fileira a 30° do leste (linha traçada 30° fora do norte): a subida
    /// escolhida é um dos sentidos da linha, e diverge 30° do que a
    /// configuração pediu. É o aviso.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaTortaDivergeDoAzimutePedido()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(30 * Grau, LineSide.Right, subida);

        // A linha (esquerda da fileira a 30°) aponta para azimute 330°; o
        // pedido é 180°, mais perto do oposto (150°): a subida é −eixo.
        Assert.Equal(30 * Grau, orientacao.DivergenceRadians, 9);
        Assert.Equal(150 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.False(orientacao.LowEdgeOnNearSide);
    }

    /// <summary>
    /// Linha traçada leste-oeste com a usina olhando para o norte: as
    /// fileiras correm norte-sul e a mesa só pode olhar para leste ou oeste.
    /// A divergência é 90°, o pior caso, e é para avisar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaPerpendicularAoAzimuteDivergeNoventaGraus()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(90 * Grau, LineSide.Right, subida);

        Assert.Equal(90 * Grau, orientacao.DivergenceRadians, 9);
    }

    /// <summary>
    /// A conversão fecha com a matriz: o eixo +Y local, girado pelo azimute
    /// resolvido, aponta para o lado da subida — perpendicular à fileira,
    /// para o lado escolhido, que é o eixo da linha. Trocar o sinal da
    /// conversão espelha a usina.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0, 0)]
    [InlineData(30, 0)]
    [InlineData(-120, 90)]
    [InlineData(170, 300)]
    public void OAzimuteResolvidoFechaComATransform(double direcaoGraus, double pedidoGraus)
    {
        var direcao = direcaoGraus * Grau;

        foreach (var lado in new[] { LineSide.Left, LineSide.Right })
        {
            var orientacao = RowOrientation.Resolve(direcao, lado, pedidoGraus * Grau);

            var subida = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(0, 1, 0));

            // Perpendicular à fileira...
            var aoLongo = subida.X * Math.Cos(direcao) + subida.Y * Math.Sin(direcao);
            Assert.Equal(0, aoLongo, 9);

            // ...e para o lado certo: a normal da célula é o eixo da linha,
            // que está à esquerda da fileira (−dy, dx) quando as fileiras
            // vão para a direita da linha, e à direita (dy, −dx) quando vão
            // para a esquerda.
            var (nx, ny) = lado == LineSide.Right
                ? (-Math.Sin(direcao), Math.Cos(direcao))
                : (Math.Sin(direcao), -Math.Cos(direcao));

            var paraOLado = subida.X * nx + subida.Y * ny;

            Assert.Equal(orientacao.LowEdgeOnNearSide ? 1 : -1, paraOLado, 9);

            // E o comprimento da fileira (eixo +X local) é a própria direção,
            // num sentido ou no outro — e LengthRunsWithRow diz qual.
            var comprimento = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(1, 0, 0));
            var alinhado = comprimento.X * Math.Cos(direcao) + comprimento.Y * Math.Sin(direcao);

            Assert.Equal(orientacao.LengthRunsWithRow ? 1 : -1, alinhado, 9);
        }
    }

    /// <summary>
    /// Fecha com o distribuidor: a normal que a orientação assume é o eixo
    /// da célula que a distribuição de fato reservou (canto 3 − canto 0).
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(LineSide.Right)]
    [InlineData(LineSide.Left)]
    public void ANormalDaOrientacaoEOFundoDaCelulaDoDistribuidor(LineSide lado)
    {
        Point3[] area = [new(-100, -100, 0), new(100, -100, 0), new(100, 100, 0), new(-100, 100, 0)];
        Point3[] linha = [new(0, -50, 0), new(20, 40, 0)];

        var layout = RowDistributor.Distribute(area, linha, lado, 6, 0, new TableFootprint(20, 4));
        var mesa = layout.Rows[0].Tables[0];

        var fundoX = (mesa.Corners[3].X - mesa.Corners[0].X) / mesa.PlanDepth;
        var fundoY = (mesa.Corners[3].Y - mesa.Corners[0].Y) / mesa.PlanDepth;

        // Subida pedida igual ao fundo da célula: a ponta baixa tem que ficar
        // de cá, e o azimute resolvido tem que ser o do fundo.
        var pedido = Math.Atan2(fundoX, fundoY);
        var orientacao = RowOrientation.Resolve(mesa.DirectionRadians, lado, pedido);

        Assert.True(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 9);

        var subida = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(0, 1, 0));
        Assert.Equal(fundoX, subida.X, 9);
        Assert.Equal(fundoY, subida.Y, 9);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ADivergenciaNuncaPassaDeNoventaGraus()
    {
        for (var graus = -180; graus <= 180; graus += 15)
        {
            var orientacao = RowOrientation.Resolve(graus * Grau, LineSide.Left, 0);

            Assert.InRange(orientacao.DivergenceRadians, 0, Math.PI / 2 + 1e-9);
            Assert.InRange(orientacao.UpslopeAzimuthRadians, 0, 2 * Math.PI);
        }
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void EntradaImpossivelERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RowOrientation.Resolve(double.NaN, LineSide.Left, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RowOrientation.Resolve(0, LineSide.Left, double.NaN));
        Assert.Throws<ArgumentException>(() => RowOrientation.Resolve(0, LineSide.On, 0));
    }
}
