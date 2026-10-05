namespace Clivus.Geo.Tests;

/// <summary>O traçado da string acompanhando a declividade de cada módulo (elétrica, 11.7; regra elétrica 5).</summary>
public class StringPathTests
{
    /// <summary>
    /// Uma fileira de módulos de 1 × 2 m (vão de 2 cm), começando em x0, no
    /// plano z = z0 + gx·x + gy·y.
    /// </summary>
    private static List<IReadOnlyList<Point3>> Modulos(double x0, int quantos, double z0, double gx, double gy, double y0 = 0)
    {
        Point3 P(double x, double y) => new(x, y, z0 + gx * x + gy * y);

        var lista = new List<IReadOnlyList<Point3>>();
        for (var i = 0; i < quantos; i++)
        {
            var a = x0 + i * 1.0 + 0.01;
            var b = x0 + (i + 1) * 1.0 - 0.01;
            lista.Add([P(a, y0), P(b, y0), P(b, y0 + 2), P(a, y0 + 2)]);
        }

        return lista;
    }

    private static double PlanoZ(IReadOnlyList<Point3> f, double x, double y)
    {
        // As faces de teste são planos z = z0 + gx·x + gy·y: três cantos bastam.
        var (a, b, c) = (f[0], f[1], f[3]);
        var gx = (b.Z - a.Z) / (b.X - a.X);
        var gy = (c.Z - a.Z) / (c.Y - a.Y);
        return a.Z + gx * (x - a.X) + gy * (y - a.Y);
    }

    private static bool Dentro(IReadOnlyList<Point3> f, double x, double y) =>
        x >= f.Min(p => p.X) - 1e-6 && x <= f.Max(p => p.X) + 1e-6 && y >= f.Min(p => p.Y) - 1e-6 && y <= f.Max(p => p.Y) + 1e-6;

    /// <summary>
    /// A conferência do nível 2, aqui em amostras: ao longo de todo o
    /// traçado (vértices e 50 pontos por trecho), onde há módulo embaixo, a
    /// linha fica entre o plano dele e o plano mais <paramref name="lift"/> (1 mm de folga).
    /// </summary>
    private static void ConferirAltura(IReadOnlyList<Point3> traco, IReadOnlyList<IReadOnlyList<Point3>> faces, double lift)
    {
        for (var i = 0; i + 1 < traco.Count; i++)
        {
            for (var k = 0; k <= 50; k++)
            {
                var t = k / 50.0;
                var x = traco[i].X + (traco[i + 1].X - traco[i].X) * t;
                var y = traco[i].Y + (traco[i + 1].Y - traco[i].Y) * t;
                var z = traco[i].Z + (traco[i + 1].Z - traco[i].Z) * t;

                foreach (var f in faces.Where(f => Dentro(f, x, y)))
                {
                    var acima = z - PlanoZ(f, x, y);
                    Assert.True(acima >= -1e-3 && acima <= lift + 1e-3, $"a {acima:0.000} m do plano do módulo em ({x:0.00}, {y:0.00})");
                }
            }
        }
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void NumPlanoSoOsCentrosDosModulosEOTracadoFicaNaAlturaDoPlano()
    {
        var faces = Modulos(0, 6, 100, 0.03, 0.4);

        var traco = StringPath.Build(faces, faces);

        Assert.Equal(6, traco.Count);
        Assert.Equal(0.5, traco[0].X, 6);
        Assert.Equal(1.0, traco[0].Y, 6);
        Assert.Equal(100 + 0.03 * 0.5 + 0.4 * 1 + StringPath.DefaultLift, traco[0].Z, 6);
        ConferirAltura(traco, faces, StringPath.DefaultLift);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void EntreMesasDeInclinacoesDiferentesHaVerticeNaBordaDeCadaUma()
    {
        // Mesa 1 subindo para leste, mesa 2 (a 0,5 m) descendo e 1 m mais alta.
        var mesa1 = Modulos(0, 3, 100, 0.10, 0.4);
        var mesa2 = Modulos(3.5, 3, 101, -0.10, 0.4);
        var todas = mesa1.Concat(mesa2).ToList();

        var traco = StringPath.Build([mesa1[2], mesa2[0]], todas);

        // O centro do último da mesa 1, a saída dele, a entrada na mesa 2, o centro do primeiro.
        Assert.Equal(4, traco.Count);
        Assert.Equal(2.99, traco[1].X, 6);
        Assert.Equal(3.51, traco[2].X, 6);
        ConferirAltura(traco, todas, StringPath.DefaultLift);

        // Uma reta direta entre os dois centros enterraria/flutuaria: confere que a conta pegaria isso.
        var reta = new List<Point3> { traco[0], traco[^1] };
        Assert.ThrowsAny<Exception>(() => ConferirAltura(reta, todas, StringPath.DefaultLift));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OLeapfrogPassaPorCimaDoModuloPuladoNoPlanoDaMesa()
    {
        var faces = Modulos(0, 5, 50, 0.2, 0.5);

        var traco = StringPath.Build([faces[0], faces[2], faces[4], faces[3], faces[1]], faces);

        Assert.Equal(5, traco.Count);
        ConferirAltura(traco, faces, StringPath.DefaultLift);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void SubirParaAFileiraDeCimaEVoltarFicaNoPlano()
    {
        var baixo = Modulos(0, 4, 10, 0.05, 0.6);
        var cima = Modulos(0, 4, 10, 0.05, 0.6, y0: 2.02);
        var todas = baixo.Concat(cima).ToList();

        var caminho = new List<IReadOnlyList<Point3>> { baixo[0], baixo[1], baixo[2], baixo[3], cima[3], cima[2], cima[1], cima[0] };
        var traco = StringPath.Build(caminho, todas);

        Assert.Equal(8, traco.Count);
        ConferirAltura(traco, todas, StringPath.DefaultLift);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ACotaNoMeioDoCaminhoEADaFaceEmbaixoENullForaDeTudo()
    {
        var faces = Modulos(0, 2, 100, 0, 0.5);

        Assert.Equal(100 + 0.5 * 1 + 0.05, StringPath.Cota(faces, 0.5, 1)!.Value, 9);
        Assert.Null(StringPath.Cota(faces, 5, 5));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void FaceEmPeOuCaminhoVazioSaoRecusados()
    {
        var emPe = new List<Point3> { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) };

        Assert.Throws<ArgumentException>(() => StringPath.Build([], []));
        Assert.Throws<ArgumentException>(() => StringPath.Build([emPe], []));
        Assert.Throws<ArgumentException>(() => StringPath.Build([[new Point3(0, 0, 0)]], []));
    }
}
