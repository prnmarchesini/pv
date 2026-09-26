using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A orientação da mesa na fileira: qual dos dois lados perpendiculares é a
/// subida, e a conversão, num lugar só, da direção matemática da fileira
/// para o azimute topográfico que o resto do Core fala.
/// </summary>
public class RowOrientationTests
{
    private const double Grau = Math.PI / 180;

    /// <summary>
    /// Fileira para o leste (direção 0), mesas à esquerda (norte). A
    /// configuração padrão pede subida para o sul (mesa olha para o norte):
    /// a subida é −normal, azimute 180°, ponta baixa na borda de lá, e a
    /// linha está exatamente perpendicular ao azimute: divergência zero.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraParaOLesteComMesaOlhandoParaONorte()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(0, LineSide.Left, subida);

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
        var orientacao = RowOrientation.Resolve(0, LineSide.Left, 0);

        Assert.True(orientacao.LengthRunsWithRow);
        Assert.True(orientacao.LowEdgeOnNearSide);
    }

    /// <summary>Mesmas fileiras, mesas à direita (sul): a subida é +normal, e a ponta baixa fica de cá.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComAsMesasDoOutroLadoAPontaBaixaFicaDeCa()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(0, LineSide.Right, subida);

        Assert.Equal(180 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.True(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 9);
    }

    /// <summary>
    /// Linha traçada a 30° do leste: a subida escolhida é perpendicular a
    /// ela, e diverge 30° do que a configuração pediu. É o aviso.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaTortaDivergeDoAzimutePedido()
    {
        var subida = SystemConfiguration.Default.UpslopeAzimuthRadians;

        var orientacao = RowOrientation.Resolve(30 * Grau, LineSide.Left, subida);

        // A normal esquerda da linha a 30° aponta para azimute 330°; o pedido
        // é 180°, mais perto do oposto (150°): a subida é −normal.
        Assert.Equal(30 * Grau, orientacao.DivergenceRadians, 9);
        Assert.Equal(150 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.False(orientacao.LowEdgeOnNearSide);
    }

    /// <summary>
    /// A conversão fecha com a matriz: o eixo +Y local, girado pelo azimute
    /// resolvido, aponta para o lado da subida — perpendicular à fileira,
    /// para o lado escolhido. Trocar o sinal da conversão espelha a usina.
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

            // ...e para o lado certo: a normal do distribuidor é a esquerda
            // (−dy, dx) ou a direita (dy, −dx).
            var (nx, ny) = lado == LineSide.Right
                ? (Math.Sin(direcao), -Math.Cos(direcao))
                : (-Math.Sin(direcao), Math.Cos(direcao));

            var paraOLado = subida.X * nx + subida.Y * ny;

            Assert.Equal(orientacao.LowEdgeOnNearSide ? 1 : -1, paraOLado, 9);

            // E o comprimento da fileira (eixo +X local) é a própria direção,
            // num sentido ou no outro — e LengthRunsWithRow diz qual.
            var comprimento = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(1, 0, 0));
            var alinhado = comprimento.X * Math.Cos(direcao) + comprimento.Y * Math.Sin(direcao);

            Assert.Equal(orientacao.LengthRunsWithRow ? 1 : -1, alinhado, 9);
        }
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
