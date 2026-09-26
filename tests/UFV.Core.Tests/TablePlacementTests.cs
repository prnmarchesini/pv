using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A matriz que põe a mesa local na célula da planta. O que se trava: os
/// quatro cantos da mesa inclinada caem, em planta, nos quatro cantos da
/// célula; a ponta baixa fica na borda certa; e a cota sobe com a inclinação.
/// </summary>
public class TablePlacementTests
{
    private const double Grau = Math.PI / 180;

    private static Point3 P(double x, double y) => new(x, y, 0);

    /// <summary>
    /// Uma célula de 20 × 4 (fundo em planta), tirada do distribuidor, com a
    /// fileira correndo no rumo pedido: a linha de alinhamento é traçada
    /// perpendicular a ele, para o lado que faz a fileira sair no rumo.
    /// </summary>
    private static PlacedTable Celula(LineSide lado, double rumoGraus)
    {
        var rumo = rumoGraus * Grau;
        var linha = lado == LineSide.Right ? rumo + Math.PI / 2 : rumo - Math.PI / 2;
        var area = new[] { P(-500, -500), P(500, -500), P(500, 500), P(-500, 500) };
        var alinhamento = new[] { P(0, 0), P(100 * Math.Cos(linha), 100 * Math.Sin(linha)) };

        return RowDistributor.Distribute(area, alinhamento, lado, 6, 0, new TableFootprint(20, 4))
            .Rows[0].Tables[0];
    }

    /// <summary>
    /// Os cantos da mesa local (comprimento 20, fundo 4/cos 20° na
    /// inclinação), levados pela matriz, caem em planta nos cantos da célula
    /// — em todo lado, rumo e azimute pedido.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(LineSide.Left, 0, 180, 20)]
    [InlineData(LineSide.Right, 0, 180, 20)]
    [InlineData(LineSide.Left, 0, 0, 20)]
    [InlineData(LineSide.Left, 30, 180, 20)]
    [InlineData(LineSide.Right, -60, 90, 20)]
    [InlineData(LineSide.Left, 200, 270, 20)]
    [InlineData(LineSide.Left, 0, 180, 0)]
    public void OsCantosDaMesaCaemNosCantosDaCelula(LineSide lado, double rumoGraus, double azimutePedido, double tiltGraus)
    {
        var celula = Celula(lado, rumoGraus);
        var tilt = tiltGraus * Grau;
        var fundo = 4 / Math.Cos(tilt);

        var orientacao = RowOrientation.Resolve(celula.DirectionRadians, lado, azimutePedido * Grau);
        var matriz = TablePlacement.Plan(celula, orientacao, tilt, elevation: 700);

        Point3[] locais = [new(0, 0, 0), new(20, 0, 0), new(20, fundo, 0), new(0, fundo, 0)];
        var noMundo = locais.Select(matriz.Apply).ToList();

        foreach (var canto in celula.Corners)
        {
            Assert.Contains(noMundo, p => Math.Abs(p.X - canto.X) < 1e-6 && Math.Abs(p.Y - canto.Y) < 1e-6);
        }

        // A ponta baixa fica na cota dada; a alta sobe fundo × sen(tilt).
        Assert.Equal(700, noMundo[0].Z, 9);
        Assert.Equal(700, noMundo[1].Z, 9);
        Assert.Equal(700 + fundo * Math.Sin(tilt), noMundo[2].Z, 9);
        Assert.Equal(700 + fundo * Math.Sin(tilt), noMundo[3].Z, 9);
    }

    /// <summary>
    /// Fileira para o leste (linha norte-sul, fileiras à direita), célula
    /// crescendo para o norte, mesa olhando para o norte (a configuração
    /// padrão): a subida é para o sul, então a ponta baixa fica na borda de
    /// LÁ (y = 4), e a alta na de cá (y = 0).
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComAMesaOlhandoParaONorteAPontaBaixaFicaNaBordaDeLa()
    {
        var celula = Celula(LineSide.Right, 0);
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, SystemConfiguration.Default.UpslopeAzimuthRadians);
        var matriz = TablePlacement.Plan(celula, orientacao, 20 * Grau, 0);

        var pontaBaixa = matriz.Apply(new Point3(10, 0, 0));
        var pontaAlta = matriz.Apply(new Point3(10, 4 / Math.Cos(20 * Grau), 0));

        Assert.Equal(4, pontaBaixa.Y, 6);
        Assert.Equal(0, pontaAlta.Y, 6);
        Assert.True(pontaAlta.Z > pontaBaixa.Z);
    }

    /// <summary>Com a mesa olhando para o sul, inverte: ponta baixa de cá.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComAMesaOlhandoParaOSulAPontaBaixaFicaNaBordaDeCa()
    {
        var celula = Celula(LineSide.Right, 0);
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, 0);
        var matriz = TablePlacement.Plan(celula, orientacao, 20 * Grau, 0);

        Assert.Equal(0, matriz.Apply(new Point3(10, 0, 0)).Y, 6);
    }

    /// <summary>A matriz é rígida: a mesa não estica nem espelha ao ser colocada.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AColocacaoERigida()
    {
        var celula = Celula(LineSide.Right, 45);
        var orientacao = RowOrientation.Resolve(celula.DirectionRadians, LineSide.Right, 1.0);

        Assert.True(TablePlacement.Plan(celula, orientacao, 15 * Grau, 12.5).IsRigid);
    }

    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(-1)]
    [InlineData(90)]
    [InlineData(double.NaN)]
    public void InclinacaoImpossivelERecusada(double graus)
    {
        var celula = Celula(LineSide.Right, 0);
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => TablePlacement.Plan(celula, orientacao, graus * Grau, 0));
    }

    /// <summary>
    /// Orientação resolvida para outra fileira (30° de diferença): a mesa
    /// sairia girada dentro da célula, amostrando terreno sob a vizinha. É
    /// recusada, e não aceita como matriz rígida com cara de certa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OrientacaoDeOutraFileiraERecusada()
    {
        var celula = Celula(LineSide.Right, 0);
        var deOutra = RowOrientation.Resolve(30 * Grau, LineSide.Right, Math.PI);

        Assert.Throws<ArgumentException>(() => TablePlacement.Plan(celula, deOutra, 20 * Grau, 0));
    }

    /// <summary>Célula sem quatro cantos, ou curta demais, é recusada com mensagem.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CelulaDegeneradaERecusada()
    {
        var boa = Celula(LineSide.Right, 0);
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, 0);

        var semCantos = boa with { Corners = [boa.Corners[0], boa.Corners[1]] };
        var curta = boa with { Length = 0 };

        Assert.Throws<ArgumentException>(() => TablePlacement.Plan(semCantos, orientacao, 0.1, 0));
        Assert.Throws<ArgumentException>(() => TablePlacement.Plan(curta, orientacao, 0.1, 0));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void CotaNaoFinitaERecusada()
    {
        var celula = Celula(LineSide.Right, 0);
        var orientacao = RowOrientation.Resolve(0, LineSide.Right, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => TablePlacement.Plan(celula, orientacao, 0.1, double.NaN));
    }
}
