using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A orientação da mesa na fileira: se a subida é +normal ou −normal da
/// célula, e a conversão, num lugar só, da direção matemática da fileira
/// para o azimute topográfico que o resto do Core fala.
///
/// Desde 26/09/2026 o distribuidor põe o fundo da célula no sentido do
/// azimute da configuração; a orientação continua genérica, para uma
/// célula que venha de outro lugar (mesa movida à mão, etapa 7).
/// </summary>
public class RowOrientationTests
{
    private const double Grau = Math.PI / 180;

    private static Point3 P(double x, double y) => new(x, y, 0);

    /// <summary>Uma célula de 20 × 4 com a fileira no rumo dado e o fundo para a esquerda do rumo (ou a direita, se pedido).</summary>
    private static PlacedTable Celula(double rumoGraus, bool fundoAEsquerda = true)
    {
        var rumo = rumoGraus * Grau;
        var d = P(Math.Cos(rumo), Math.Sin(rumo));
        var n = fundoAEsquerda ? P(-d.Y, d.X) : P(d.Y, -d.X);

        Point3[] cantos =
        [
            P(0, 0),
            P(d.X * 20, d.Y * 20),
            P(d.X * 20 + n.X * 4, d.Y * 20 + n.Y * 4),
            P(n.X * 4, n.Y * 4),
        ];

        return new PlacedTable(1, 1, P(0, 0), rumo, 20, 4, cantos, false);
    }

    /// <summary>
    /// Fileira para o leste com o fundo para o norte, mesa olhando para o
    /// norte (subida para o sul, a configuração padrão): a subida é −normal,
    /// azimute 180°, ponta baixa na borda de lá, e divergência zero. O +X
    /// local corre para oeste, contra a fileira.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FileiraParaOLesteComMesaOlhandoParaONorte()
    {
        var orientacao = RowOrientation.Resolve(Celula(0), SystemConfiguration.Default.UpslopeAzimuthRadians);

        Assert.Equal(180 * Grau, orientacao.UpslopeAzimuthRadians, 9);
        Assert.False(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 9);
        Assert.False(orientacao.LengthRunsWithRow);
    }

    /// <summary>Com a mesa olhando para o sul (subida para o norte), a ponta baixa fica de cá e o +X local corre com a fileira.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComAMesaOlhandoParaOSulOComprimentoCorreComAFileira()
    {
        var orientacao = RowOrientation.Resolve(Celula(0), 0);

        Assert.True(orientacao.LengthRunsWithRow);
        Assert.True(orientacao.LowEdgeOnNearSide);
    }

    /// <summary>
    /// A célula que o distribuidor entrega tem o fundo no sentido do azimute:
    /// a ponta baixa fica sempre de cá e a divergência é zero, para qualquer
    /// azimute.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0)]
    [InlineData(180)]
    [InlineData(37)]
    [InlineData(250)]
    public void ACelulaDoDistribuidorTemAPontaBaixaDeCa(double azimuteGraus)
    {
        var azimute = azimuteGraus * Grau;
        Point3[] area = [P(-100, -100), P(100, -100), P(100, 100), P(-100, 100)];

        // Uma linha ao longo do azimute, atravessando as fileiras.
        Point3[] linha = [P(-80 * Math.Sin(azimute), -80 * Math.Cos(azimute)), P(80 * Math.Sin(azimute), 80 * Math.Cos(azimute))];

        var layout = RowDistributor.Distribute(area, linha, LineSide.Right, 6, 0, new TableFootprint(20, 4), azimute);
        var mesa = layout.Rows[0].Tables[0];

        var orientacao = RowOrientation.Resolve(mesa, azimute);

        Assert.True(orientacao.LowEdgeOnNearSide);
        Assert.Equal(0, orientacao.DivergenceRadians, 6);
        Assert.Equal(azimute % (2 * Math.PI), orientacao.UpslopeAzimuthRadians, 6);
    }

    /// <summary>Célula com o fundo a 90° do azimute pedido: divergência de 90°, o pior caso.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void FundoPerpendicularAoAzimuteDivergeNoventaGraus()
    {
        // Fundo para o norte, pedido para o leste.
        var orientacao = RowOrientation.Resolve(Celula(0), 90 * Grau);

        Assert.Equal(90 * Grau, orientacao.DivergenceRadians, 9);
    }

    /// <summary>
    /// A conversão fecha com a matriz: o eixo +Y local, girado pelo azimute
    /// resolvido, aponta para o lado da subida — perpendicular à fileira,
    /// no sentido do fundo ou contra ele. Trocar o sinal da conversão
    /// espelha a usina.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0, 0, true)]
    [InlineData(30, 0, true)]
    [InlineData(-120, 90, false)]
    [InlineData(170, 300, true)]
    [InlineData(45, 225, false)]
    public void OAzimuteResolvidoFechaComATransform(double direcaoGraus, double pedidoGraus, bool fundoAEsquerda)
    {
        var direcao = direcaoGraus * Grau;
        var celula = Celula(direcaoGraus, fundoAEsquerda);
        var orientacao = RowOrientation.Resolve(celula, pedidoGraus * Grau);

        var subida = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(0, 1, 0));

        // Perpendicular à fileira...
        var aoLongo = subida.X * Math.Cos(direcao) + subida.Y * Math.Sin(direcao);
        Assert.Equal(0, aoLongo, 9);

        // ...e no sentido do fundo quando a ponta baixa fica de cá.
        var nx = (celula.Corners[3].X - celula.Corners[0].X) / 4;
        var ny = (celula.Corners[3].Y - celula.Corners[0].Y) / 4;
        var paraOFundo = subida.X * nx + subida.Y * ny;

        Assert.Equal(orientacao.LowEdgeOnNearSide ? 1 : -1, paraOFundo, 9);

        // E o comprimento da fileira (eixo +X local) é a própria direção,
        // num sentido ou no outro — e LengthRunsWithRow diz qual.
        var comprimento = Transform.Azimuth(orientacao.UpslopeAzimuthRadians).Apply(new Point3(1, 0, 0));
        var alinhado = comprimento.X * Math.Cos(direcao) + comprimento.Y * Math.Sin(direcao);

        Assert.Equal(orientacao.LengthRunsWithRow ? 1 : -1, alinhado, 9);
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ADivergenciaNuncaPassaDeNoventaGraus()
    {
        for (var graus = -180; graus <= 180; graus += 15)
        {
            var orientacao = RowOrientation.Resolve(Celula(graus), 0);

            Assert.InRange(orientacao.DivergenceRadians, 0, Math.PI / 2 + 1e-9);
            Assert.InRange(orientacao.UpslopeAzimuthRadians, 0, 2 * Math.PI);
        }
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void EntradaImpossivelERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RowOrientation.Resolve(Celula(0), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => RowOrientation.Resolve(Celula(0) with { DirectionRadians = double.NaN }, 0));

        var torta = Celula(0) with { Corners = [P(0, 0), P(20, 0), P(20, 4), P(0, 9)] };
        Assert.Throws<ArgumentException>(() => RowOrientation.Resolve(torta, 0));
    }
}
