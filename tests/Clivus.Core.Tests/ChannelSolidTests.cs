using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>Passo 29.1: os anéis da canaleta em 3D.</summary>
public class ChannelSolidTests
{
    private static readonly ChannelSection Trap = ChannelSection.Defaults[0];

    private static ChannelProfilePoint P(double d, double x, double y, double fundo) => new(d, x, y, fundo + 0.35, fundo);

    [Fact]
    [Trait("Etapa", "29")]
    public void CanaletaRetaComFundoRetoViraDoisAneis()
    {
        var pontos = Enumerable.Range(0, 21).Select(i => P(i * 5, i * 5, 0, 100 - 0.01 * i * 5)).ToList();

        var aneis = ChannelSolid.Rings(pontos, Trap);

        Assert.Equal(2, aneis.Count);
        Assert.Equal(8, aneis[0].Count);

        // O fundo interno na cota do fundo; a borda interna a y acima; o fundo externo e abaixo.
        Assert.Equal(100, aneis[0].Where((_, i) => i is 5 or 6).Max(p => p.Z), 9);
        Assert.Equal(100.35, aneis[0][4].Z, 9);
        Assert.Equal(100 - 0.10, aneis[0][1].Z, 9);
        Assert.Equal(99, aneis[1][5].Z, 9);

        // A largura na borda interna é B = 0,72, na perpendicular (eixo y) do traçado (eixo x).
        Assert.Equal(0.72, Math.Abs(aneis[0][4].Y - aneis[0][7].Y), 9);
        Assert.All(aneis[0], p => Assert.Equal(0, p.X, 9));
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void QuebraDoFundoAlemDaToleranciaEntraComoSecao()
    {
        // Desce 1 % até 50 m e 5 % depois: a quebra no meio precisa de seção.
        var pontos = Enumerable.Range(0, 21).Select(i =>
        {
            var d = i * 5.0;
            var fundo = d <= 50 ? 100 - 0.01 * d : 99.5 - 0.05 * (d - 50);
            return P(d, d, 0, fundo);
        }).ToList();

        var simples = ChannelSolid.Simplify(pontos);

        Assert.Equal([0.0, 50, 100], simples.Select(p => p.Distance));
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void NaQuinaEmPlantaOAnelFicaNaBissetrizEsticado()
    {
        // Leste 50 m e depois norte 50 m: quina de 90° em (50, 0).
        var pontos = new List<ChannelProfilePoint>
        {
            P(0, 0, 0, 100), P(50, 50, 0, 99.5), P(100, 50, 50, 99),
        };

        var aneis = ChannelSolid.Rings(pontos, Trap);
        var quina = aneis[1];

        // A borda interna esquerda e a direita na diagonal da bissetriz (−45°),
        // com a largura esticada por 1/cos(45°).
        var (e, d) = (quina[7], quina[4]);
        var largura = Math.Sqrt((e.X - d.X) * (e.X - d.X) + (e.Y - d.Y) * (e.Y - d.Y));
        Assert.Equal(0.72 / Math.Cos(Math.PI / 4), largura, 6);
        Assert.Equal(1, Math.Abs((e.Y - d.Y) / (e.X - d.X)), 6);
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void ASuperficieEFechadaComTampas()
    {
        var pontos = new List<ChannelProfilePoint> { P(0, 0, 0, 100), P(30, 30, 0, 99.7) };
        var aneis = ChannelSolid.Rings(pontos, Trap);
        var tri = ChannelSolid.Triangles(aneis);

        // 8 lados × 2 triângulos + 2 tampas × 3 quadriláteros × 2 triângulos.
        Assert.Equal(16 + 12, tri.Count);

        // Fechada: cada aresta aparece em exatamente dois triângulos.
        var arestas = new Dictionary<(Point3, Point3), int>();
        void Somar(Point3 a, Point3 b)
        {
            var chave = string.CompareOrdinal(a.ToString(), b.ToString()) < 0 ? (a, b) : (b, a);
            arestas[chave] = arestas.GetValueOrDefault(chave) + 1;
        }

        foreach (var (a, b, c) in tri) { Somar(a, b); Somar(b, c); Somar(c, a); }
        Assert.All(arestas.Values, v => Assert.Equal(2, v));
    }

    [Fact]
    [Trait("Etapa", "29")]
    public void MeiaCanaTemOArcoInteiro()
    {
        var mc = ChannelSection.Defaults[1];
        var u = ChannelSolid.UShape(mc, 12);

        Assert.Equal(26, u.Count);
        Assert.Equal(-mc.Thickness, u.Min(p => p.Y), 9);
        Assert.Equal(mc.Radius, u.Max(p => p.Y), 9);
    }
}
