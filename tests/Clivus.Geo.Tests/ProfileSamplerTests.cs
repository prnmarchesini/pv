using Clivus.Geo;

namespace Clivus.Geo.Tests;

/// <summary>Passo 27.2: o perfil do terreno ao longo da linha, com estacas e quebras, contado da montante.</summary>
public class ProfileSamplerTests
{
    [Fact]
    [Trait("Etapa", "27")]
    public void NaRampaOPerfilEUmaRetaDe5PorCento()
    {
        // Linha de oeste para leste: começa no alto (a rampa desce para o leste).
        var perfil = ProfileSampler.Sample(TerrenoDeDrenagem.Rampa(), [new Point3(10, 55, 0), new Point3(110, 55, 0)], 5);

        Assert.False(perfil.Reversed);
        Assert.Equal(100, perfil.Length, 9);
        Assert.Equal(0, perfil.OutsideCount);

        foreach (var p in perfil.Points)
            Assert.Equal(100 - 0.05 * (10 + p.Distance), p.Z, 9);

        // Estacas a cada 5 m, de 0 a 100: 21.
        Assert.Equal(21, perfil.Points.Count(p => p.IsStation));
        Assert.Equal(Enumerable.Range(0, 21).Select(i => 5.0 * i), perfil.Points.Where(p => p.IsStation).Select(p => Math.Round(p.Distance, 9)));
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void LinhaDesenhadaDeBaixoParaCimaEContadaDaMontante()
    {
        var perfil = ProfileSampler.Sample(TerrenoDeDrenagem.Rampa(), [new Point3(110, 55, 0), new Point3(10, 55, 0)], 5);

        Assert.True(perfil.Reversed);
        Assert.Equal(10, perfil.Points[0].X, 9);
        Assert.True(perfil.Points[0].Z > perfil.Points[^1].Z);
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void ComDirecaoTravadaNaoInverte()
    {
        var perfil = ProfileSampler.Sample(TerrenoDeDrenagem.Rampa(), [new Point3(110, 55, 0), new Point3(10, 55, 0)], 5, keepDirection: true);

        Assert.False(perfil.Reversed);
        Assert.Equal(110, perfil.Points[0].X, 9);
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void NoValeAQuebraDoTalvegueEntraNoPerfil()
    {
        // Linha atravessando o vale em y = 100: desce até x = 100 e sobe.
        var perfil = ProfileSampler.Sample(TerrenoDeDrenagem.Vale(), [new Point3(55, 100, 0), new Point3(145, 100, 0)], 7);

        var fundo = perfil.Points.MinBy(p => p.Z);
        Assert.Equal(100, fundo.X, 6);
        Assert.Equal(100 - 0.02 * 100, fundo.Z, 6);

        // Entre dois pontos, a cota interpolada é a do terreno (o terreno é reto entre quebras).
        var tin = TerrenoDeDrenagem.Vale();
        foreach (var d in new[] { 3.3, 21.7, 44.9, 46.2, 80.1 })
        {
            var a = perfil.Points.Last(p => p.Distance <= d);
            var b = perfil.Points.First(p => p.Distance >= d);
            var t = b.Distance > a.Distance ? (d - a.Distance) / (b.Distance - a.Distance) : 0;
            Assert.True(tin.TryGetZ(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, out var z));
            Assert.Equal(z, perfil.ZAt(d), 6);
        }
    }

    [Fact]
    [Trait("Etapa", "27")]
    public void PontoForaDoTerrenoEContadoENaoEntraNoPerfil()
    {
        // De x = 150 (dentro) a 230 (fora, o terreno vai a 200), desenhada do baixo para o alto.
        var perfil = ProfileSampler.Sample(TerrenoDeDrenagem.Rampa(), [new Point3(230, 55, 0), new Point3(150, 55, 0)], 5);

        Assert.True(perfil.OutsideCount > 0);

        // Nenhuma cota inventada (o ponto de fora tinha Z = 0) e nenhuma estaca além da borda.
        Assert.All(perfil.Points, p => Assert.Equal(100 - 0.05 * p.X, p.Z, 9));
        Assert.All(perfil.Points, p => Assert.True(p.X <= 200 + 1e-6));

        // O sentido sai das pontas com terreno: começa em x = 150 (o alto).
        Assert.True(perfil.Reversed);
        Assert.Equal(150, perfil.Points[0].X, 6);
    }
}
