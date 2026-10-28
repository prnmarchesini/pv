namespace Clivus.Core.Tests;

/// <summary>
/// O "Atualizar estruturas" do carport (28/10/2026): a fila refeita onde os
/// vãos estão agora, depois de um MOVE, ROTATE ou MIRROR no desenho.
/// </summary>
public class CarportFollowTests
{
    private static readonly CarportProfile P = CarportProfile.Reference;

    private static CarportRowRecord Fila(int vaos = 3, bool caiADireita = true) =>
        new(Guid.NewGuid(), "Carport", 100, 200, 100 + P.Array.BayWidth * vaos, 200, caiADireita, vaos);

    /// <summary>Os cantos em planta de cada vão, como o contorno é desenhado (chão plano).</summary>
    private static List<(double X, double Y)[]> Cantos(CarportRowRecord fila)
    {
        var montada = fila.Build(P, (_, _) => 0, out var motivo);
        Assert.True(montada is not null, motivo);
        return CarportReturn.Tables(fila.Id, fila.Carport, P, montada!).Select(t => t.Corners.Select(c => (c.E, c.N)).ToArray()).ToList();
    }

    /// <summary>Gira em volta de (cx, cy), espelha no eixo X local se pedido, e desloca.</summary>
    private static Func<(double X, double Y), (double X, double Y)> Movimento(double graus, double dx, double dy, bool espelhar = false, double cx = 100, double cy = 200)
    {
        var a = graus * Math.PI / 180;
        return p =>
        {
            var x = p.X - cx;
            var y = p.Y - cy;
            if (espelhar) y = -y;
            return (cx + x * Math.Cos(a) - y * Math.Sin(a) + dx, cy + x * Math.Sin(a) + y * Math.Cos(a) + dy);
        };
    }

    private static CarportBayNow Agora((double X, double Y)[] c, Func<(double X, double Y), (double X, double Y)> mover)
    {
        var v0 = mover(c[0]);
        var v1 = mover(c[1]);
        var v2 = mover(c[2]);
        return new CarportBayNow(v0.X, v0.Y, v1.X, v1.Y, v2.X, v2.Y);
    }

    /// <summary>A fila refeita tem os vãos exatamente onde o usuário os deixou (o mesmo contorno, na mesma ordem).</summary>
    private static void VaosOnde(CarportRowRecord nova, IEnumerable<(double X, double Y)[]> esperados)
    {
        var refeitos = Cantos(nova);
        var lista = esperados.ToList();
        Assert.Equal(lista.Count, refeitos.Count);
        for (var v = 0; v < lista.Count; v++)
            for (var k = 0; k < 4; k++)
            {
                Assert.Equal(lista[v][k].X, refeitos[v][k].X, 6);
                Assert.Equal(lista[v][k].Y, refeitos[v][k].Y, 6);
            }
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void FilaParadaFicaComoEstava()
    {
        var fila = Fila();
        var cantos = Cantos(fila);
        var agora = cantos.Select((c, v) => (v, Agora(c, p => p))).ToDictionary(x => x.v, x => x.Item2);

        var novas = CarportFollow.Rows(fila, P, agora, () => throw new InvalidOperationException("não era para nascer fila"));

        // A mesma fila, sem nenhum arredondamento: o Atualizar repetido não faz a fila andar.
        Assert.Same(fila, Assert.Single(novas));
    }

    [Theory]
    [Trait("Etapa", "36")]
    [InlineData(0, 250, -1300)]
    [InlineData(37, -15, 8)]
    [InlineData(180, 3, 4)]
    public void FilaMovidaEGiradaVaiJunto(double graus, double dx, double dy)
    {
        var fila = Fila();
        var mover = Movimento(graus, dx, dy);
        var cantos = Cantos(fila);
        var agora = cantos.Select((c, v) => (v, Agora(c, mover))).ToDictionary(x => x.v, x => x.Item2);

        var nova = Assert.Single(CarportFollow.Rows(fila, P, agora, Guid.NewGuid));

        Assert.Equal(fila.Id, nova.Id);
        Assert.Equal(fila.Bays, nova.Bays);
        Assert.Equal(fila.FallsRight, nova.FallsRight);
        VaosOnde(nova, cantos.Select(c => c.Select(mover).ToArray()));
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void FilaEspelhadaTrocaOLadoDaDescida()
    {
        var fila = Fila(caiADireita: true);
        var mover = Movimento(20, 5, 5, espelhar: true);
        var cantos = Cantos(fila);
        var agora = cantos.Select((c, v) => (v, Agora(c, mover))).ToDictionary(x => x.v, x => x.Item2);

        var nova = Assert.Single(CarportFollow.Rows(fila, P, agora, Guid.NewGuid));

        Assert.False(nova.FallsRight);
        VaosOnde(nova, cantos.Select(c => c.Select(mover).ToArray()));
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void VaoMovidoSozinhoViraFilaPropria()
    {
        var fila = Fila(3);
        var cantos = Cantos(fila);
        var mover = Movimento(90, 40, 0);
        var agora = new Dictionary<int, CarportBayNow>
        {
            [0] = Agora(cantos[0], p => p),
            [1] = Agora(cantos[1], mover),
            [2] = Agora(cantos[2], p => p),
        };

        var novas = CarportFollow.Rows(fila, P, agora, Guid.NewGuid);

        Assert.Equal(3, novas.Count);
        Assert.All(novas, n => Assert.Equal(1, n.Bays));
        Assert.DoesNotContain(novas, n => n.Id == fila.Id);
        Assert.Equal(3, novas.Select(n => n.Id).Distinct().Count());
        VaosOnde(novas[0], [cantos[0]]);
        VaosOnde(novas[1], [cantos[1].Select(mover).ToArray()]);
        VaosOnde(novas[2], [cantos[2]]);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void VaoApagadoPartaAFila()
    {
        var fila = Fila(4);
        var cantos = Cantos(fila);
        var agora = new Dictionary<int, CarportBayNow>
        {
            [0] = Agora(cantos[0], p => p),
            [1] = Agora(cantos[1], p => p),
            [3] = Agora(cantos[3], p => p),
        };

        var novas = CarportFollow.Rows(fila, P, agora, Guid.NewGuid);

        Assert.Equal([2, 1], novas.Select(n => n.Bays));
        VaosOnde(novas[0], [cantos[0], cantos[1]]);
        VaosOnde(novas[1], [cantos[3]]);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void AtualizarDuasVezesNaoAndaAFila()
    {
        // Gira e move; a fila refeita, lida de novo como está, fica onde está.
        var fila = Fila();
        var mover = Movimento(15, 3, -12);
        var agora = Cantos(fila).Select((c, v) => (v, Agora(c, mover))).ToDictionary(x => x.v, x => x.Item2);
        var nova = Assert.Single(CarportFollow.Rows(fila, P, agora, Guid.NewGuid));

        var deNovo = Cantos(nova).Select((c, v) => (v, Agora(c, p => p))).ToDictionary(x => x.v, x => x.Item2);
        Assert.Same(nova, Assert.Single(CarportFollow.Rows(nova, P, deNovo, Guid.NewGuid)));
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void VaoCopiadoViraFilaOndeEsta()
    {
        // COPY de um vão, girado e espelhado: a fila nova desenha o vão em cima da cópia.
        var fila = Fila(3);
        var cantos = Cantos(fila);
        var mover = Movimento(-25, 30, 40, espelhar: true);

        var nova = CarportFollow.FromBay(P, "Carport", Agora(cantos[1], mover), Guid.NewGuid());

        Assert.NotNull(nova);
        Assert.Equal(1, nova!.Bays);
        VaosOnde(nova, [cantos[1].Select(mover).ToArray()]);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void VaosCopiadosLadoALadoVoltamAUmaFila()
    {
        // COPY da fila inteira: cada vão vira fila de um vão, e as três se juntam numa.
        var fila = Fila(3);
        var cantos = Cantos(fila);
        var mover = Movimento(10, 50, -20);
        var uma = cantos.Select(c => CarportFollow.FromBay(P, "Carport", Agora(c, mover), Guid.NewGuid())!).Reverse().ToList();

        var juntas = CarportFollow.Merge(uma);

        var nova = Assert.Single(juntas);
        Assert.Equal(3, nova.Bays);
        VaosOnde(nova, cantos.Select(c => c.Select(mover).ToArray()));
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void FilasSeparadasNaoSeJuntam()
    {
        var fila = Fila(3);
        var cantos = Cantos(fila);
        var a = CarportFollow.FromBay(P, "Carport", Agora(cantos[0], p => p), Guid.NewGuid())!;
        var c = CarportFollow.FromBay(P, "Carport", Agora(cantos[2], p => p), Guid.NewGuid())!;
        var outroCarport = CarportFollow.FromBay(P, "Outro", Agora(cantos[1], p => p), Guid.NewGuid())!;

        Assert.Equal(3, CarportFollow.Merge([a, c, outroCarport]).Count);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void SemVaoNenhumAFilaSai()
    {
        Assert.Empty(CarportFollow.Rows(Fila(), P, new Dictionary<int, CarportBayNow>(), Guid.NewGuid));
    }
}
