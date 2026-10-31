using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A sombra do relevo (42.1, Renan, 31/10/2026: "o terreno gera sombra se
/// tiver barranco"), num barranco sintético: o chão plano na cota 0 ao sul de
/// y = 0, um talude de 2 m que sobe 5 m (2,5 m/m) e o platô na cota 5 ao
/// norte de y = 2. Com o sol ao norte a 20°, o raio de um ponto do pé do
/// barranco bate na crista enquanto 5 &gt; (2 + d)·tan 20°: a sombra vai de
/// y ≈ −11,74 até o talude (que olha para longe do sol e fica na sombra
/// dele mesmo).
/// </summary>
public class TerrainShadowTests
{
    private const double Altura = 5;

    /// <summary>O barranco: 0 ao sul de y = 0, 5 ao norte de y = 2, reto entre os dois.</summary>
    private static double? Degrau(double x, double y) => y <= 0 ? 0 : y >= 2 ? Altura : Altura * y / 2;

    private static SunPosition Sol(double azimute, double elevacao) => new(azimute, elevacao, 0, 0);

    /// <summary>A região do chão: x de −10 a 10, y de −30 a 20.</summary>
    private static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Regiao => [[(-10, -30), (10, -30), (10, 20), (-10, 20)]];

    /// <summary>A crista vista do pé: 5 = (2 + d)·tan 20°.</summary>
    private static readonly double FimDaSombra = -(Altura / Math.Tan(20 * Math.PI / 180) - 2);

    [Fact]
    [Trait("Etapa", "42")]
    public void OBarrancoFazSombraNoPeComOSolDoLadoDele()
    {
        var relevo = new TerrainShadow(Degrau, Regiao, [], margin: 40);
        relevo.Add(Sol(0, 20));

        Assert.Equal(1, relevo.Steps);
        Assert.Equal(1, relevo.SweptSteps);

        var anel = Assert.Single(relevo.Ground());
        var (yMin, yMax) = (anel.Min(p => p.Y), anel.Max(p => p.Y));

        // A célula é de 1 m: a borda fica a uma célula do exato.
        Assert.InRange(yMin, FimDaSombra - 1.5, FimDaSombra + 1.5);
        Assert.InRange(yMax, 0.5, 3);
        Assert.Equal(-10, anel.Min(p => p.X), 1);
        Assert.Equal(10, anel.Max(p => p.X), 1);

        // Pergunta ponto a ponto, na mesma varredura.
        Assert.True(relevo.Occludes(0, -5, 0.01));
        Assert.True(relevo.Occludes(0, -1, 0.5));
        Assert.False(relevo.Occludes(0, -15, 0.01));
        Assert.False(relevo.Occludes(0, 10, Altura + 0.01));   // no platô, o sol está à vista
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ComOSolDoOutroLadoOuAltoOBarrancoNaoFazSombra()
    {
        var doSul = new TerrainShadow(Degrau, Regiao, [], margin: 40);
        doSul.Add(Sol(180, 20));
        Assert.Empty(doSul.Ground());
        Assert.False(doSul.AnyShadow);

        // O sol mais alto que o talude (tan 70° = 2,75 > 2,5 m/m): sai cedo, sem varrer.
        var alto = new TerrainShadow(Degrau, Regiao, [], margin: 40);
        alto.Add(Sol(0, 70));
        Assert.Equal(1, alto.Steps);
        Assert.Equal(0, alto.SweptSteps);
        Assert.Empty(alto.Ground());
        Assert.False(alto.Prepare(Sol(0, 70)));

        // O sol abaixo do mínimo do cálculo não conta.
        var posto = new TerrainShadow(Degrau, Regiao, [], margin: 40);
        posto.Add(Sol(0, 1));
        Assert.Equal(0, posto.Steps);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AMesaNoPeDoBarrancoPegaSombraNoPlanoDelaEADoPlatoNao()
    {
        // Duas mesas planas a 1 m do chão: uma no pé (y de −6 a −2), uma no platô.
        IReadOnlyList<Point3> pe = [new(-5, -6, 1), new(5, -6, 1), new(5, -2, 1), new(-5, -2, 1)];
        IReadOnlyList<Point3> plato = [new(-5, 8, Altura + 1), new(5, 8, Altura + 1), new(5, 12, Altura + 1), new(-5, 12, Altura + 1)];

        var relevo = new TerrainShadow(Degrau, [], [pe, plato], margin: 40);
        relevo.Add(Sol(0, 20));

        Assert.Equal([0], relevo.ShadedTables);
        var anel = Assert.Single(relevo.OnTable(0));
        Assert.Equal(40, ShadowUnion.Area(anel), 0);   // a mesa inteira: o raio de y = −2, a 1 m, ainda bate na crista
        Assert.Empty(relevo.OnTable(1));
        Assert.Empty(relevo.Ground());                  // sem região, só as mesas
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OPeriodoJuntaOsPassosEAMesmaPosicaoNaoVarreDeNovo()
    {
        var relevo = new TerrainShadow(Degrau, Regiao, [], margin: 40);

        relevo.Add(Sol(0, 20));
        relevo.Add(Sol(0, 40));    // sombra mais curta: já estava dentro da de 20°
        relevo.Add(Sol(0, 40));    // a mesma posição: não varre de novo
        relevo.Add(Sol(180, 20));  // do sul: nada

        Assert.Equal(4, relevo.Steps);
        Assert.Equal(3, relevo.SweptSteps);
        var anel = Assert.Single(relevo.Ground());
        Assert.InRange(anel.Min(p => p.Y), FimDaSombra - 1.5, FimDaSombra + 1.5);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ODeclivePeloTinEOAlcance()
    {
        // O mesmo barranco num TIN de grade de 1 m.
        var tin = Grade(-20, -20, 20, 20, 1, (x, y) => Degrau(x, y)!.Value);

        Assert.Equal(2.5, TerrainShadow.MaxSlope(tin, -20, -20, 20, 20), 6);
        Assert.Equal(0, TerrainShadow.MaxSlope(tin, -20, -20, 20, -1), 6);   // só o chão plano do sul

        Assert.Equal(Altura / Math.Tan(20 * Math.PI / 180), TerrainShadow.Reach(Altura, 0, 20, 500), 6);
        Assert.Equal(30, TerrainShadow.Reach(Altura, 0, 1, 30));             // o sol abaixo do mínimo vale o mínimo, e o teto manda
        Assert.Equal(0, TerrainShadow.Reach(0, Altura, 20, 500));            // o morro mais baixo que quem recebe
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OModuloNoPeDoBarrancoPegaASombraDoTerrenoPeloModelo()
    {
        // Uma face de 4 × 2 a 1 m do chão no pé do barranco, e a mesma no platô.
        ShadowQuad Face(double y0, double z, int grupo) => new([new(-2, y0, z), new(2, y0, z), new(2, y0 + 2, z), new(-2, y0 + 2, z)], grupo);

        var faces = new[] { Face(-6, 1, 0), Face(8, Altura + 1, 1) };
        var relevo = new TerrainShadow(Degrau, [], faces.Select(f => f.Corners).ToList(), margin: 40);
        var modelo = new ShadingModel(faces, [], terrain: relevo);

        var baixo = modelo.At(Sol(0, 20));
        Assert.Equal(1, baixo.Fractions[0]);
        Assert.Equal(ShadowCause.Terrain, baixo.Causes[0]);
        Assert.Equal(0, baixo.Fractions[1]);

        Assert.Equal(0, modelo.At(Sol(0, 70)).Fractions[0]);
        Assert.Equal(0, modelo.At(Sol(180, 20)).Fractions[0]);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void SoAsFacesDoEscopoRecebem()
    {
        // A fileira da frente faz sombra na de trás com o sol baixo; a de trás
        // fora do escopo fica sem fração, mas a da frente continua fazendo sombra.
        ShadowQuad Mesa(double y0, int grupo) => new([new(0, y0, 0.5), new(10, y0, 0.5), new(10, y0 - 2, 2.5), new(0, y0 - 2, 2.5)], grupo);
        var faces = new[] { Mesa(0, 0), Mesa(-4, 1) };

        var todas = new ShadingModel(faces, []).At(Sol(0, 10));
        Assert.True(todas.Fractions[1] > 0.4);

        var soATras = new ShadingModel(faces, [], receivers: [false, true]).At(Sol(0, 10));
        Assert.True(soATras.Fractions[1] > 0.4);

        var soAFrente = new ShadingModel(faces, [], receivers: [true, false]).At(Sol(0, 10));
        Assert.Equal(0, soAFrente.Fractions[1]);
        Assert.Equal(ShadowCause.None, soAFrente.Causes[1]);
    }

    /// <summary>Um TIN de grade regular, dois triângulos por célula.</summary>
    internal static Tin Grade(double x0, double y0, double x1, double y1, double passo, Func<double, double, double> z)
    {
        var triangulos = new List<Triangle>();
        for (var x = x0; x < x1 - 1e-9; x += passo)
        {
            for (var y = y0; y < y1 - 1e-9; y += passo)
            {
                Point3 P(double a, double b) => new(a, b, z(a, b));
                var (a1, b1, c1, d1) = (P(x, y), P(x + passo, y), P(x + passo, y + passo), P(x, y + passo));
                triangulos.Add(new Triangle(a1, b1, c1));
                triangulos.Add(new Triangle(a1, c1, d1));
            }
        }

        return new Tin(triangulos);
    }
}
