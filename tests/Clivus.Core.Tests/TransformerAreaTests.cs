namespace Clivus.Core.Tests;

/// <summary>A área do trafo: o contorno das pegadas dos módulos das strings dele (05/10/2026).</summary>
public class TransformerAreaTests
{
    private static IReadOnlyList<(double X, double Y)> Retangulo(double x, double y, double largura, double altura) =>
        [(x, y), (x + largura, y), (x + largura, y + altura), (x, y + altura)];

    /// <summary>Uma fileira de módulos 1 x 2 m, com 2 cm entre eles, começando em (x, y).</summary>
    private static List<IReadOnlyList<(double X, double Y)>> Fileira(double x, double y, int quantos) =>
        Enumerable.Range(0, quantos).Select(i => Retangulo(x + i * 1.02, y, 1, 2)).ToList();

    private static bool Dentro(IReadOnlyList<(double X, double Y)> anel, double px, double py)
    {
        var dentro = false;
        for (int i = 0, j = anel.Count - 1; i < anel.Count; j = i++)
        {
            var (a, b) = (anel[i], anel[j]);
            if ((a.Y > py) != (b.Y > py) && px < (b.X - a.X) * (py - a.Y) / (b.Y - a.Y) + a.X) dentro = !dentro;
        }

        return dentro;
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void TresFileirasComCorredorViramUmaAreaSemBuraco()
    {
        // Três fileiras de 10 módulos (10,18 x 2 m), corredor de 5 m entre elas.
        var pegadas = Fileira(0, 0, 10).Concat(Fileira(0, 7, 10)).Concat(Fileira(0, 14, 10)).ToList();

        var ilha = Assert.Single(TransformerArea.Outline(pegadas));

        Assert.Empty(ilha.Holes);
        Assert.Equal(30, ilha.Members.Count);

        // O retângulo que envolve tudo, mais a folga de 0,5 m em volta.
        var esperado = (10.18 + 1) * (16 + 1);
        Assert.InRange(ilha.Area, esperado - 0.5, esperado + 0.01);

        // Todo canto de módulo fica dentro, e o meio do corredor também.
        foreach (var p in pegadas)
            foreach (var (x, y) in p)
                Assert.True(Dentro(ilha.Outer, x, y), $"o canto ({x}; {y}) ficou de fora");
        Assert.True(Dentro(ilha.Outer, 5, 4.5));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void FileirasGiradasEDesencontradasDaoContornoLimpo()
    {
        // Azimute de 20 graus e cada fileira deslocada 3 m: a borda é a
        // escada das pontas, sem farpa das frestas de 2 cm entre módulos.
        var (sen, cos) = Math.SinCos(20 * Math.PI / 180);
        var pegadas = Fileira(0, 0, 12).Concat(Fileira(3, 7, 12)).Concat(Fileira(6, 14, 12))
            .Select(p => (IReadOnlyList<(double X, double Y)>)p.Select(v => (v.X * cos - v.Y * sen + 500, v.X * sen + v.Y * cos + 300)).ToList())
            .ToList();

        var ilha = Assert.Single(TransformerArea.Outline(pegadas));

        Assert.Empty(ilha.Holes);
        Assert.Equal(36, ilha.Members.Count);
        Assert.True(ilha.Outer.Count <= 16, $"{ilha.Outer.Count} vértices: o contorno tem farpas");
        foreach (var p in pegadas)
            foreach (var (x, y) in p)
                Assert.True(Dentro(ilha.Outer, x, y), $"o canto ({x}; {y}) ficou de fora");
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void BlocosLongeUmDoOutroDaoDuasIlhas()
    {
        var pegadas = Fileira(0, 0, 5).Concat(Fileira(100, 0, 3)).ToList();

        var ilhas = TransformerArea.Outline(pegadas);

        Assert.Equal(2, ilhas.Count);
        Assert.Equal(5, ilhas[0].Members.Count);   // a maior primeiro
        Assert.Equal([5, 6, 7], ilhas[1].Members.OrderBy(m => m));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void NaoInvadeOsModulosDeOutroTrafo()
    {
        // O trafo tem as fileiras de baixo e de cima; a do meio é de outro.
        var proprias = Fileira(0, 0, 10).Concat(Fileira(0, 14, 10)).ToList();
        var alheias = Fileira(0, 7, 10);

        var ilhas = TransformerArea.Outline(proprias, alheias);

        Assert.Equal(2, ilhas.Count);
        foreach (var p in alheias)
        {
            var (cx, cy) = (p.Average(v => v.X), p.Average(v => v.Y));
            Assert.DoesNotContain(ilhas, i => Dentro(i.Outer, cx, cy));
        }

        Assert.Equal(20, ilhas.Sum(i => i.Members.Count));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void BuracoGrandeFicaEBuracoMinusculoSome()
    {
        // Um anel de módulos em volta de um pátio de 30 x 30 m: o pátio é buraco de verdade.
        var pegadas = new List<IReadOnlyList<(double X, double Y)>>
        {
            Retangulo(0, 0, 40, 5), Retangulo(0, 35, 40, 5), Retangulo(0, 5, 5, 30), Retangulo(35, 5, 5, 30),
        };

        var ilha = Assert.Single(TransformerArea.Outline(pegadas));
        var buraco = Assert.Single(ilha.Holes);
        Assert.InRange(ShadowUnion.Area(buraco), 25 * 25, 30 * 30);

        // Sem fechamento e sem folga, um vão de 30 cm entre quatro módulos deixa
        // um furo de 0,09 m²: não vira buraco no hatch.
        var furinho = new List<IReadOnlyList<(double X, double Y)>>
        {
            Retangulo(0, 0, 2, 2), Retangulo(2.3, 0, 2, 2), Retangulo(0, 2.3, 2, 2), Retangulo(2.3, 2.3, 2, 2),
            Retangulo(2, 0, 0.3, 2), Retangulo(2, 2.3, 0.3, 2), Retangulo(0, 2, 2, 0.3), Retangulo(2.3, 2, 2, 0.3),
        };

        var semFuro = Assert.Single(TransformerArea.Outline(furinho, bridge: 0, margin: 0));
        Assert.Empty(semFuro.Holes);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemPegadaNaoDaIlhaENegativoERecusado()
    {
        Assert.Empty(TransformerArea.Outline([]));
        Assert.Empty(TransformerArea.Outline([[(0, 0), (1, 1)]]));
        Assert.Empty(TransformerArea.Outline([[(0, 0), (1, 0), (2, 0)]]));
        Assert.Throws<ArgumentOutOfRangeException>(() => TransformerArea.Outline(Fileira(0, 0, 2), bridge: -1));
    }
}
