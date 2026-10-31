using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A sombra no chão em pedaços que acompanham o terreno (42.1, regra sagrada
/// 5): ladrilhos de até 8 m, cada um num plano ajustado ao TIN, 4 cm acima
/// dele no ponto mais alto e a poucos centímetros no resto.
/// </summary>
public class GroundPatchesTests
{
    private static IReadOnlyList<(double X, double Y)> Quadrado(double x, double y, double lado) =>
        [(x, y), (x + lado, y), (x + lado, y + lado), (x, y + lado)];

    /// <summary>A folga do plano acima do terreno em cada vértice dos contornos e numa grade de 0,25 m dentro de cada pedaço.</summary>
    private static List<double> Folgas(GroundPatch pedaco, Tin terreno)
    {
        var folgas = new List<double>();

        foreach (var anel in pedaco.Loops)
        {
            foreach (var (x, y) in anel)
                if (terreno.TryGetZ(x, y, out var z)) folgas.Add(pedaco.HeightAt(x, y) - z);

            var (x0, x1, y0, y1) = (anel.Min(p => p.X), anel.Max(p => p.X), anel.Min(p => p.Y), anel.Max(p => p.Y));
            for (var x = x0; x <= x1; x += 0.25)
                for (var y = y0; y <= y1; y += 0.25)
                    if (Polygons.Contains(anel.Select(p => new Point3(p.X, p.Y, 0)).ToList(), x, y) && terreno.TryGetZ(x, y, out var z))
                        folgas.Add(pedaco.HeightAt(x, y) - z);
        }

        return folgas;
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void NoTerrenoInclinadoCadaLadrilhoTemOPlanoDoTerrenoQuatroCentimetrosAcima()
    {
        // Um plano inclinado em x e em y, numa cota de verdade (UTM-ish).
        var terreno = TerrainShadowTests.Grade(-40, -40, 40, 40, 4, (x, y) => 700 + 0.10 * x - 0.05 * y);

        // Uma mancha de 20 × 20 m que não começa num múltiplo de 8.
        var pedacos = GroundPatches.Build([Quadrado(-9, -5, 20)], terreno);

        // De x −9 a 11 e de y −5 a 15: quatro colunas (−9 a −8, −8 a 0, 0 a 8,
        // 8 a 11) e três linhas (−5 a 0, 0 a 8, 8 a 15) de ladrilhos de 8 m.
        Assert.Equal(12, pedacos.Count);
        Assert.Equal(400, pedacos.Sum(p => p.Loops.Sum(ShadowUnion.Area)), 6);
        Assert.All(pedacos, p => Assert.Equal(GroundPatches.TileSize, p.Size));

        foreach (var p in pedacos)
        {
            // Cada pedaço dentro do ladrilho dele.
            var (x0, y0) = (p.TileX * GroundPatches.TileSize, p.TileY * GroundPatches.TileSize);
            Assert.All(p.Loops.SelectMany(a => a), q =>
            {
                Assert.InRange(q.X, x0 - 1e-6, x0 + GroundPatches.TileSize + 1e-6);
                Assert.InRange(q.Y, y0 - 1e-6, y0 + GroundPatches.TileSize + 1e-6);
            });

            // O plano é o do terreno, 4 cm acima, em todo ponto.
            Assert.Equal(0.10, p.SlopeX, 6);
            Assert.Equal(-0.05, p.SlopeY, 6);
            Assert.All(Folgas(p, terreno), f => Assert.Equal(GroundPatches.Clearance, f, 4));
            Assert.Equal(GroundPatches.Clearance, p.MinClearance, 6);
            Assert.Equal(GroundPatches.Clearance, p.MaxClearance, 4);
        }

        // A normal aponta para cima e é perpendicular ao plano.
        var n = pedacos[0].Normal;
        Assert.True(n.Z > 0);
        Assert.Equal(1, n.X * n.X + n.Y * n.Y + n.Z * n.Z, 9);
        Assert.Equal(-0.10, n.X / n.Z, 9);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void NaCristaOPedacoSeDivideAteFicarPertoDoTerreno()
    {
        // Um telhado: a crista em x = 3 (dentro de um ladrilho), 50% de cada lado.
        var terreno = TerrainShadowTests.Grade(-24, -24, 24, 24, 1, (x, y) => 100 - 0.5 * Math.Abs(x - 3));

        var pedacos = GroundPatches.Build([Quadrado(-8, 0, 24)], terreno);

        Assert.Equal(24 * 24, pedacos.Sum(p => p.Loops.Sum(ShadowUnion.Area)), 6);

        // O ladrilho da crista (x de 0 a 8) não cabe num plano: dividido.
        Assert.Contains(pedacos, p => p.TileX == 0 && p.Size < GroundPatches.TileSize);

        // Em todo pedaço, o plano fica de 4 cm acima do terreno até 4 + 5 cm (o
        // limite do pedaço de 1 m, que já cabe num plano aqui).
        foreach (var p in pedacos)
        {
            var folgas = Folgas(p, terreno);
            Assert.NotEmpty(folgas);
            Assert.All(folgas, f => Assert.InRange(f, GroundPatches.Clearance - 1e-6, GroundPatches.Clearance + GroundPatches.Spread + 1e-6));
            Assert.InRange(p.MaxClearance, GroundPatches.Clearance, GroundPatches.Clearance + GroundPatches.Spread + 1e-9);
        }

        // Longe da crista, o ladrilho inteiro.
        Assert.Contains(pedacos, p => p.TileX == -1 && p.Size == GroundPatches.TileSize);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OFuroDaManchaFicaFuroESemTerrenoNaoHaPedaco()
    {
        var terreno = TerrainShadowTests.Grade(0, 0, 16, 16, 2, (x, y) => 50);

        // Um quadrado de 6 m com um furo de 2 m no meio (o furo no sentido contrário).
        IReadOnlyList<(double X, double Y)> furo = [(2, 2), (2, 4), (4, 4), (4, 2)];
        var pedacos = GroundPatches.Build([Quadrado(0, 0, 6), furo], terreno);

        var pedaco = Assert.Single(pedacos);
        Assert.Equal(36 - 4, Math.Abs(pedaco.Loops.Sum(a => Area(a))), 6);
        Assert.Equal(50 + GroundPatches.Clearance, pedaco.HeightAt(1, 1), 9);

        // Fora do terreno: nada.
        Assert.Empty(GroundPatches.Build([Quadrado(100, 100, 5)], terreno));
        Assert.Empty(GroundPatches.Build([], terreno));

        static double Area(IReadOnlyList<(double X, double Y)> anel)
        {
            var s = 0.0;
            for (var i = 0; i < anel.Count; i++) s += anel[i].X * anel[(i + 1) % anel.Count].Y - anel[(i + 1) % anel.Count].X * anel[i].Y;
            return s / 2;
        }
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OLadrilhoEFixoNoDesenhoEABordaSoSegueAMancha()
    {
        Assert.Equal((0, 0), GroundPatches.TileOf(0, 7.999));
        Assert.Equal((-1, 0), GroundPatches.TileOf(-0.001, 0));
        Assert.Equal((37500, 875000), GroundPatches.TileOf(300_000.5, 7_000_000.5));

        // A borda de um quadrado de 20 m cortada nos ladrilhos: 80 m no total,
        // todos os pontos no contorno (os cortes dos ladrilhos não viram borda).
        var trechos = GroundPatches.Borders([Quadrado(1, 1, 20)]);

        var comprimento = trechos.Sum(t => t.Line.Zip(t.Line.Skip(1), (a, b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y))).Sum());
        Assert.Equal(80, comprimento, 6);
        Assert.All(trechos.SelectMany(t => t.Line), p =>
            Assert.True(Math.Abs(p.X - 1) < 1e-6 || Math.Abs(p.X - 21) < 1e-6 || Math.Abs(p.Y - 1) < 1e-6 || Math.Abs(p.Y - 21) < 1e-6, $"{p} fora do contorno"));
        Assert.All(trechos, t => Assert.All(t.Line, p =>
        {
            Assert.InRange(p.X, t.TileX * 8 - 1e-6, (t.TileX + 1) * 8 + 1e-6);
            Assert.InRange(p.Y, t.TileY * 8 - 1e-6, (t.TileY + 1) * 8 + 1e-6);
        }));
    }
}
