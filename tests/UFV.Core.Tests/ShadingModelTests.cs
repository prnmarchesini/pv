using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A sombra com todos os elementos do desenho (03/10/2026: "cadê o
/// sombreamento das próprias mesas? A análise tem que pegar todos
/// elementos do desenho"): mesas umas nas outras, relevo e árvores.
/// </summary>
public class ShadingModelTests
{
    /// <summary>Sol ao norte (o do Brasil), na elevação dada.</summary>
    private static SunPosition SolAoNorte(double elevacao) => new(0, elevacao, 0, 0);

    /// <summary>Uma mesa inclinada para o norte: borda baixa em y0 na cota z0, sobe 2 m em 2 m de fundo.</summary>
    private static ShadowQuad Mesa(double y0, double z0, int grupo) =>
        new([new(0, y0, z0), new(10, y0, z0), new(10, y0 - 2, z0 + 2), new(0, y0 - 2, z0 + 2)], grupo);

    /// <summary>
    /// Duas fileiras: a de trás (grupo 1, ao sul) fica a 4 m da da frente
    /// (grupo 0, ao norte). Com o sol baixo ao norte (10°), a da frente faz
    /// sombra na de trás; com o sol alto (60°), não. A da frente fica no sol.
    /// </summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void AFileiraDaFrenteSombreiaADeTrasComOSolBaixo()
    {
        var frente = Mesa(0, 0.5, 0);
        var tras = Mesa(-4, 0.5, 1);
        var modelo = new ShadingModel([frente, tras], []);

        var baixo = modelo.At(SolAoNorte(10));
        Assert.Equal(0, baixo.Fractions[0]);
        Assert.True(baixo.Fractions[1] > 0.4, $"fração {baixo.Fractions[1]}");
        Assert.Equal(ShadowCause.Table, baixo.Causes[1]);

        var alto = modelo.At(SolAoNorte(60));
        Assert.Equal(0, alto.Fractions[1]);
        Assert.Equal(ShadowCause.None, alto.Causes[1]);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AMesaNaoFazSombraNelaMesmaESemAsMesasNinguemSombreia()
    {
        var frente = Mesa(0, 0.5, 0);
        var mesmaMesa = Mesa(-4, 0.5, 0);

        Assert.Equal(0, new ShadingModel([frente, mesmaMesa], []).At(SolAoNorte(10)).Fractions[1]);
        Assert.Equal(0, new ShadingModel([frente, Mesa(-4, 0.5, 1)], [], tablesCastShadow: false).At(SolAoNorte(10)).Fractions[1]);
    }

    /// <summary>Um morro de 30 m a 20 m ao norte esconde o sol a 20° (horizonte de 55°) mas não a 60°; o sol do sul não é escondido.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void OMorroAoNorteEscondeOSolBaixo()
    {
        double? Terreno(double x, double y) => y >= 20 ? 30 : 0;

        var modelo = new ShadingModel([Mesa(0, 0.5, 0)], [], Terreno, groundMaxZ: 30);

        var baixo = modelo.At(SolAoNorte(20));
        Assert.Equal(1, baixo.Fractions[0]);
        Assert.Equal(ShadowCause.Terrain, baixo.Causes[0]);

        Assert.Equal(0, modelo.At(SolAoNorte(60)).Fractions[0]);
        Assert.Equal(0, modelo.At(new SunPosition(180, 20, 0, 0)).Fractions[0]);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AArvoreTemACausaDela()
    {
        var mesa = Mesa(0, 0.5, 0);
        var arvore = new TreeSpec(3, 0.4, 5, 6).Cylinders(5, 5, 0);

        var r = new ShadingModel([mesa], arvore).At(SolAoNorte(30));

        Assert.True(r.Fractions[0] > 0);
        Assert.Equal(ShadowCause.Object, r.Causes[0]);
    }

    /// <summary>No pior caso de um dia de inverno em Itatiba, a fileira de trás pega sombra da da frente cedo ou tarde, e a causa vai junto.</summary>
    [Fact]
    [Trait("Etapa", "9")]
    public void PiorCasoDoDiaComAsMesas()
    {
        var modelo = new ShadingModel([Mesa(0, 0.5, 0), Mesa(-4, 0.5, 1)], []);
        var dia = new DateOnly(2026, 6, 21);

        var r = modelo.Worst(-23, -46.8, -3, Shading.Instants(dia, dia, new TimeOnly(7, 0), new TimeOnly(17, 0), TimeSpan.FromMinutes(30)));

        Assert.True(r.Fractions[1] > 0);
        Assert.Equal(ShadowCause.Table, r.Causes![1]);
        Assert.NotNull(r.When[1]);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void PassoDeDias()
    {
        var a = new DateOnly(2026, 1, 1);
        var b = new DateOnly(2026, 12, 31);

        Assert.Equal(53 * 11, Shading.Instants(a, b, new TimeOnly(7, 0), new TimeOnly(17, 0), TimeSpan.FromHours(1), dayStep: 7).Count());
        Assert.Equal(53 * 11, Shading.CountInstants(a, b, new TimeOnly(7, 0), new TimeOnly(17, 0), TimeSpan.FromHours(1), dayStep: 7));
    }
}
