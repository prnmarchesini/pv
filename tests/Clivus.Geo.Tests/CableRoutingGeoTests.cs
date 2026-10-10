using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>A rede de valas e o cabo em 3D (roteamento, 17.5).</summary>
public class TrenchNetworkTests
{
    // Um "T": vala principal de oeste a leste (y = 0, x de 0 a 100) e um ramal
    // que desce do norte (x = 50, y de 40 a 0,3: a ponta encosta sem tocar).
    private static readonly Point3[] Principal = [new(0, 0, 0), new(100, 0, 0)];
    private static readonly Point3[] Ramal = [new(50, 40, 0), new(50, 0.3, 0)];

    [Fact]
    [Trait("Etapa", "17")]
    public void PontaQueEncostaLigaAsValas()
    {
        var rede = new TrenchNetwork([Principal, Ramal]);

        var a = rede.Nearest(new Point3(50, 40, 0), 1)!.Value;
        var b = rede.Nearest(new Point3(100, 0, 0), 1)!.Value;
        var caminho = rede.Path(a, b);

        Assert.NotNull(caminho);
        Assert.Equal(40 + 50, TrenchNetwork.PlanLength(caminho!), 1);
    }

    // Retângulo do equipamento de x 98 a 102, y 6 a 10.
    private static readonly Point3[] Caixa = [new(98, 6, 0), new(102, 6, 0), new(102, 10, 0), new(98, 10, 0)];

    /// <summary>
    /// A vala que entra no retângulo do equipamento (o rabicho) é achada só em
    /// planta, com a cota do rabisco a centenas de metros do terreno (Renan,
    /// 10/10/2026); a principal, que só passa perto, não conta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "21")]
    public void ValaQueEntraNoEquipamentoEAchadaSoEmPlanta()
    {
        var rabicho = new Point3[] { new(90, 0, -300), new(99, 7, 412) };
        var rede = new TrenchNetwork([Principal.Select(p => p with { X = p.X * 2 }).ToArray(), rabicho]);

        var entram = rede.Entering(Caixa, new Point3(100, 8, 500));

        var ponto = Assert.Single(entram);
        Assert.Equal(99, ponto.At.X, 6);
        Assert.Equal(7, ponto.At.Y, 6);
    }

    /// <summary>Ponta que encosta na borda (até 0,5 m) conta; mais longe, não; nenhuma: vazio.</summary>
    [Fact]
    [Trait("Etapa", "21")]
    public void ValaQueEncostaNaBordaContaEALongeNao()
    {
        var encosta = new TrenchNetwork([[new(90, 0, 0), new(97.7, 8, 0)]]);
        var longe = new TrenchNetwork([[new(90, 0, 0), new(97, 8, 0)]]);

        var p = Assert.Single(encosta.Entering(Caixa, new Point3(100, 8, 0)));
        Assert.Equal(97.7, p.At.X, 6);
        Assert.Empty(longe.Entering(Caixa, new Point3(100, 8, 0)));
    }

    /// <summary>Vala que atravessa o retângulo: o ponto de dentro mais perto do equipamento.</summary>
    [Fact]
    [Trait("Etapa", "21")]
    public void ValaQueAtravessaDaOPontoDeDentroMaisPerto()
    {
        var rede = new TrenchNetwork([[new(80, 9, 0), new(120, 9, 0)]]);

        var p = Assert.Single(rede.Entering(Caixa, new Point3(110, 8, 0)));

        Assert.Equal(102, p.At.X, 6);
        Assert.Equal(9, p.At.Y, 6);
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ValasSoltasNaoTemCaminho()
    {
        var longe = new Point3[] { new(50, 40, 0), new(50, 5, 0) };
        var rede = new TrenchNetwork([Principal, longe]);

        var a = rede.Nearest(new Point3(50, 40, 0), 1)!.Value;
        var b = rede.Nearest(new Point3(0, 0, 0), 1)!.Value;

        Assert.Null(rede.Path(a, b));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void CaminhoMaisCurtoPelaRede()
    {
        // Um quadrado de 10 m: de um canto ao oposto há dois caminhos de 20 m; pela diagonal, 14,1.
        var quadrado = new Point3[] { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0), new(0, 0, 0) };
        var diagonal = new Point3[] { new(0, 0, 0), new(10, 10, 0) };
        var rede = new TrenchNetwork([quadrado, diagonal]);

        var caminho = rede.Path(rede.Nearest(new(0, 0, 0), 0.1)!.Value, rede.Nearest(new(10, 10, 0), 0.1)!.Value);

        Assert.Equal(Math.Sqrt(200), TrenchNetwork.PlanLength(caminho!), 6);
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ForaDoRaioNaoAcha()
    {
        var rede = new TrenchNetwork([Principal]);

        Assert.Null(rede.Nearest(new Point3(30, 10, 0), 9.99));
        Assert.Equal(10, rede.Nearest(new Point3(30, 10, 0), 10)!.Value.Distance, 9);
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void RetaBateNaPrimeiraVala()
    {
        var segunda = new Point3[] { new(0, -20, 0), new(100, -20, 0) };
        var rede = new TrenchNetwork([Principal, segunda]);

        var batida = rede.Ray(new Point3(30, 10, 0), new Point3(0, -1, 0), 100)!.Value;

        Assert.Equal(10, batida.Distance, 9);
        Assert.Equal(0, batida.At.Y, 9);
        Assert.Null(rede.Ray(new Point3(30, 10, 0), new Point3(0, 1, 0), 100));
        Assert.Null(rede.Ray(new Point3(30, 10, 0), new Point3(0, -1, 0), 9));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void CaboDesceAProfundidadeEContaEm3D()
    {
        // Terreno plano na cota 100. Origem a 2 m de altura (o módulo), vala de 10 m, 0,8 m de fundo.
        double? Plano(double x, double y) => 100;
        var cabo = CablePath.Build(
            new Point3(0, 5, 102), [new Point3(0, 0, 0)], [new Point3(0, 0, 0), new Point3(10, 0, 0)], [], new Point3(10, 0, 100.8), 0.8, Plano, out var fora);

        Assert.Null(fora);
        Assert.NotNull(cabo);
        Assert.Contains(cabo!, p => Math.Abs(p.Z - 99.2) < 1e-9);

        // Do módulo reto até a entrada da vala (5 m em planta, 2 m de queda), desce 0,8,
        // anda 10, sobe 0,8, sobe mais 0,8 até o equipamento.
        var esperado = Math.Sqrt(25 + 4) + 0.8 + 10 + 0.8 + 0.8;
        Assert.Equal(esperado, CablePath.Length(cabo), 6);
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ForaDoTerrenoNaoInventaCota()
    {
        double? SoAteX5(double x, double y) => x <= 5 ? 100 : null;

        var cabo = CablePath.Build(
            new Point3(0, 5, 102), [new Point3(0, 0, 0)], [new Point3(0, 0, 0), new Point3(10, 0, 0)], [], new Point3(10, 0, 100), 0.8, SoAteX5, out var fora);

        Assert.Null(cabo);
        Assert.NotNull(fora);
        Assert.True(fora!.Value.X > 5);
    }
}

/// <summary>A saída do cabo da string pela mesa (roteamento, 18.1 e regra 2: nunca pelo miolo).</summary>
public class RowExitTests
{
    // Mesa de 20 x 4 m ao longo de x: borda baixa em y = 0, alta em y = 4.
    private static readonly Point3[] Mesa = [new(0, 0, 0), new(20, 0, 0), new(20, 4, 0), new(0, 4, 0)];

    // Fileira de três mesas iguais, de x = 0 a x = 64 (vão de 2 m).
    private static readonly Point3[] Fileira =
        [.. Mesa, new(22, 0, 0), new(42, 0, 0), new(42, 4, 0), new(22, 4, 0), new(44, 0, 0), new(64, 0, 0), new(64, 4, 0), new(44, 4, 0)];

    [Theory]
    [InlineData(1.0, -0.5)]   // perto da borda baixa: sai por baixo
    [InlineData(3.2, 4.5)]    // perto da alta: sai por cima
    [Trait("Etapa", "18")]
    public void SaiPelaBordaMaisPertoEFicaForaDaMesa(double y, double yEsperado)
    {
        var (pontos, _) = RowExit.Plan(new Point3(5, y, 2), Mesa, Fileira, RowEnd.End);

        Assert.Equal(yEsperado, pontos[0].Y, 9);
        Assert.Equal(5, pontos[0].X, 9);
        Assert.All(pontos, p => Assert.False(p.Y > 0 && p.Y < 4, $"ponto {p} dentro da faixa das mesas"));
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void SegueAteAPontaDaFileiraEMaisAFolga()
    {
        var (fim, direcaoFim) = RowExit.Plan(new Point3(5, 1, 2), Mesa, Fileira, RowEnd.End);
        var (inicio, direcaoInicio) = RowExit.Plan(new Point3(5, 1, 2), Mesa, Fileira, RowEnd.Start);

        Assert.Equal(64 + RowExit.Margin, fim[1].X, 9);
        Assert.Equal(1, direcaoFim.X, 9);
        Assert.Equal(-RowExit.Margin, inicio[1].X, 9);
        Assert.Equal(-1, direcaoInicio.X, 9);
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void MesaGiradaSaiNaPerpendicular()
    {
        // A mesma mesa girada 90°: comprida em y, borda baixa em x = 0.
        Point3[] girada = [new(0, 0, 0), new(0, 20, 0), new(-4, 20, 0), new(-4, 0, 0)];

        var (pontos, direcao) = RowExit.Plan(new Point3(-1, 5, 2), girada, girada, RowEnd.End);

        Assert.Equal(0.5, pontos[0].X, 9);
        Assert.Equal(5, pontos[0].Y, 9);
        Assert.Equal(20.5, pontos[1].Y, 9);
        Assert.Equal(1, direcao.Y, 9);
    }
}

/// <summary>A revisão de 10/10/2026: a regra 2 com fileira de mesas desalinhadas, mais largas ou tortas, e a rede com cruzamento real.</summary>
public class CableRoutingReviewTests
{
    private static Point3[] Mesa(double x0, double y0, double largura = 4, double comprimento = 20) =>
        [new(x0, y0, 0), new(x0 + comprimento, y0, 0), new(x0 + comprimento, y0 + largura, 0), new(x0, y0 + largura, 0)];

    /// <summary>O segmento ab passa por dentro do polígono (amostrado a cada 5 cm)?</summary>
    private static bool Corta(Point3 a, Point3 b, IReadOnlyList<Point3> poligono)
    {
        var n = (int)Math.Ceiling(Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) / 0.05);
        for (var i = 1; i < n; i++)
        {
            var x = a.X + (b.X - a.X) * i / n;
            var y = a.Y + (b.Y - a.Y) * i / n;
            if (Polygons.Contains(poligono, x, y) && !NaBorda(poligono, x, y)) return true;
        }

        return false;
    }

    private static bool NaBorda(IReadOnlyList<Point3> p, double x, double y)
    {
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            var l = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (Math.Abs((b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X)) / l < 1e-6) return true;
        }

        return false;
    }

    [Theory]
    [InlineData(-1.9, 4)]   // vizinha deslocada 1,9 m para baixo
    [InlineData(0, 6)]      // vizinha mais larga (3P ao lado de 2P), alinhada pela borda baixa
    [Trait("Etapa", "18")]
    public void LinhaDeForaPassaAlemDeTodasAsMesasDaFileira(double deslocamento, double largura)
    {
        var a = Mesa(0, 0);
        var b = Mesa(22, deslocamento, largura);
        Point3[] fileira = [.. a, .. b];

        foreach (var (y, fim) in new[] { (1.0, RowEnd.End), (3.0, RowEnd.End), (1.0, RowEnd.Start) })
        {
            var (pontos, _) = RowExit.Plan(new Point3(5, y, 2), a, fileira, fim);
            Assert.False(Corta(pontos[0], pontos[1], b), $"y={y} {fim}: a linha corta a mesa vizinha");
            Assert.False(Corta(pontos[0], pontos[1], a), $"y={y} {fim}: a linha corta a própria mesa");
        }
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void MesaTortaNaoDeixaASaidaDentroDela()
    {
        // Borda alta torta: canto 2 em y = 5, canto 3 em y = 4.
        Point3[] torta = [new(0, 0, 0), new(20, 0, 0), new(20, 5, 0), new(0, 4, 0)];

        var (pontos, _) = RowExit.Plan(new Point3(18, 4.2, 2), torta, torta, RowEnd.End);

        Assert.False(Polygons.Contains(torta, pontos[0].X, pontos[0].Y));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ValasQueSeCruzamNoMeioSeLigam()
    {
        var rede = new TrenchNetwork([[new Point3(0, 0, 0), new Point3(20, 20, 0)], [new Point3(0, 20, 0), new Point3(20, 0, 0)]]);

        var caminho = rede.Path(rede.Nearest(new(0, 0, 0), 0.1)!.Value, rede.Nearest(new(0, 20, 0), 0.1)!.Value);

        Assert.NotNull(caminho);
        Assert.Equal(2 * Math.Sqrt(200), TrenchNetwork.PlanLength(caminho!), 6);
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void DentroDoRaioVemTodasDoMaisPerto()
    {
        var rede = new TrenchNetwork([[new Point3(0, 2, 0), new Point3(10, 2, 0)], [new Point3(0, -5, 0), new Point3(10, -5, 0)]]);

        var achadas = rede.Within(new Point3(5, 0, 0), 6);

        Assert.Equal(2, achadas.Count);
        Assert.Equal(2, achadas[0].Distance, 9);
        Assert.Equal(5, achadas[1].Distance, 9);
    }
}
