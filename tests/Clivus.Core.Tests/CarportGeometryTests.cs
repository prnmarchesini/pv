using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A geometria do carport (34.2). O teste de ouro monta o pórtico padrão em
/// chão plano no mesmo lugar do <c>carport-biposte.glb</c> e confere com as
/// medidas do modelo (<c>plano/carport/01-modelo-de-referencia.md</c>):
/// 1 cm nos módulos, 3 cm no pórtico, 8 cm nas terças e mãos francesas (o
/// padrão usa a média do modelo, que não é simétrico; ver PROGRESSO).
/// </summary>
public class CarportGeometryTests
{
    private static CarportProfile Padrao => CarportProfile.Reference;

    // O pórtico de X = 2,75 do .glb: eixo da fila em +X, a borda baixa em Y 5,088
    // (a água cai para +Y, à esquerda de quem anda em +X), o meio da cobertura em Y 1,6865.
    private const double XDoPortico = 2.75;
    private const double YDoMeio = 1.6865;

    private static Func<double, double, double?> Plano(double z = 0) => (_, _) => z;

    private static CarportPortal PorticoDoModelo(Func<double, double, double?>? chao = null, CarportProfile? perfil = null, double x = XDoPortico)
    {
        var p = CarportGeometry.Portal(perfil ?? Padrao, x, YDoMeio, 1, 0, caiADireita: false, chao ?? Plano(), out var motivo);
        Assert.True(p is not null, motivo);
        return p!;
    }

    private static void Perto(double esperado, double obtido, double tolerancia, string oque) =>
        Assert.True(Math.Abs(esperado - obtido) <= tolerancia, $"{oque}: esperado {esperado:0.###}, saiu {obtido:0.###} (tolerância {tolerancia})");

    private static CarportBar Barra(CarportPortal p, CarportPart parte, int qual) => p.Bars.Where(b => b.Part == parte).ElementAt(qual);

    // ---------------------------------------------------------- o pórtico de ouro

    [Fact]
    [Trait("Etapa", "34")]
    public void ABordaBaixaFicaNaAlturaLivre()
    {
        Assert.Equal(2.345, PorticoDoModelo().LowEdgeZ, 9);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void AsBasesFicamOndeEstaoNoModelo()
    {
        var p = PorticoDoModelo();

        Perto(0.497, p.HighBase.Y, 0.03, "base do pilar alto, Y");
        Perto(2.922, p.LowBase.Y, 0.03, "base do pilar baixo, Y");
        Assert.Equal(XDoPortico, p.HighBase.X, 9);
        Assert.Equal(0, p.HighBase.Z, 9);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OsPilaresAbremEmVEPegamAVigaPorBaixo()
    {
        var p = PorticoDoModelo();
        var alto = Barra(p, CarportPart.Pillar, 0);
        var baixo = Barra(p, CarportPart.Pillar, 1);

        // O pilar começa em cima do bloco e da chapa.
        Assert.Equal(0.098, alto.Start.Z, 9);

        // Topo: onde o eixo do pilar encontra a face de baixo da viga no .glb.
        Perto(-0.198, alto.End.Y, 0.03, "topo do pilar alto, Y");
        Perto(2.766, alto.End.Z, 0.03, "topo do pilar alto, Z");
        Perto(3.494, baixo.End.Y, 0.03, "topo do pilar baixo, Y");
        Perto(2.320, baixo.End.Z, 0.03, "topo do pilar baixo, Z");

        // Abertura de 15°: o alto vai para -Y (o lado alto), o baixo para +Y.
        Assert.True(alto.End.Y < alto.Start.Y);
        Assert.True(baixo.End.Y > baixo.Start.Y);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void AVigaVaiDeUmaPontaAOutraComoNoModelo()
    {
        var viga = Barra(PorticoDoModelo(), CarportPart.Beam, 0);

        Perto(4.543, viga.Start.Y, 0.03, "ponta baixa da viga, Y");
        Perto(2.250, viga.Start.Z, 0.03, "ponta baixa da viga, Z");
        Perto(-1.215, viga.End.Y, 0.03, "ponta alta da viga, Y");
        Perto(2.950, viga.End.Z, 0.03, "ponta alta da viga, Z");
        Perto(5.80, viga.Length, 0.03, "comprimento da viga");
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void AsMaosFrancesasLigamPilarEViga()
    {
        var p = PorticoDoModelo();
        var alta = Barra(p, CarportPart.Brace, 0);
        var baixa = Barra(p, CarportPart.Brace, 1);

        // No pilar: o ponto no EIXO do pilar (no .glb a peça encosta na face, 6,5 cm para dentro).
        Perto(-0.033, alta.Start.Y, 0.08, "mão francesa alta no pilar, Y");
        Perto(2.176, alta.Start.Z, 0.08, "mão francesa alta no pilar, Z");
        Perto(0.633, alta.End.Y, 0.08, "mão francesa alta na viga, Y");
        Perto(2.682, alta.End.Z, 0.08, "mão francesa alta na viga, Z");

        Perto(3.334, baixa.Start.Y, 0.08, "mão francesa baixa no pilar, Y");
        Perto(1.721, baixa.Start.Z, 0.08, "mão francesa baixa no pilar, Z");
        Perto(2.793, baixa.End.Y, 0.08, "mão francesa baixa na viga, Y");
        Perto(2.416, baixa.End.Z, 0.08, "mão francesa baixa na viga, Z");
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OPontoMaisBaixoEAMaoFrancesaDoPilarBaixo()
    {
        Perto(1.70, PorticoDoModelo().LowestAboveGround, 0.08, "ponto mais baixo");
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void CadaPilarTemBlocoEChapa()
    {
        var p = PorticoDoModelo();

        Assert.Equal(2, p.Blocks.Count(b => b.Part == CarportPart.Pedestal));
        Assert.Equal(2, p.Blocks.Count(b => b.Part == CarportPart.BasePlate));
        Assert.All(p.Blocks.Where(b => b.Part == CarportPart.BasePlate), b => Assert.Equal(0.092, b.BottomCenter.Z, 9));
    }

    // ---------------------------------------------------------- o vão de ouro

    private static CarportBay VaoDoModelo(out CarportPortal de, out CarportPortal ate, Func<double, double, double?>? chao = null)
    {
        var meio = Padrao.Array.BayWidth / 2;
        de = PorticoDoModelo(chao, x: -meio);
        ate = PorticoDoModelo(chao, x: meio);
        return CarportGeometry.Bay(Padrao, 0, de, ate);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OVaoTemOsQuinzeModulosDoModelo()
    {
        var vao = VaoDoModelo(out _, out _);

        Assert.Equal(15, vao.Modules.Count);
        var todos = vao.Modules.SelectMany(m => m.Corners).ToList();

        // A cobertura do .glb: Y -1,716 a 5,088; de cima do quadro, a borda alta a 3,21.
        Perto(5.088, todos.Max(c => c.Y), 0.01, "borda baixa, Y");
        Perto(-1.716, todos.Min(c => c.Y), 0.01, "borda alta, Y");
        Perto(3.21, todos.Max(c => c.Z), 0.01, "borda alta, Z");
        Perto(Padrao.Array.BayWidth, todos.Max(c => c.X) - todos.Min(c => c.X), 0.001, "largura do vão");
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OsCantosDoModuloVemNaOrdemDaMesaFixa()
    {
        var m = VaoDoModelo(out _, out _).Modules[0];
        var c = m.Corners;

        // 1→2 é a borda baixa no sentido da fila; 3→4 a alta, voltando.
        Assert.True(c[1].X > c[0].X);
        Assert.Equal(c[0].Z, c[1].Z, 9);
        Assert.True(c[2].Z > c[1].Z);
        Assert.True(c[3].X < c[2].X);
        Perto(Padrao.Array.ModuleAcross, CarportGeometry.Distancia(c[0], c[1]), 1e-9, "largura do módulo");
        Perto(Padrao.Array.ModuleDown, CarportGeometry.Distancia(c[1], c[2]), 1e-9, "altura do módulo");
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void AsTercasFicamOndeEstaoNoModelo()
    {
        var vao = VaoDoModelo(out _, out _);
        var ys = vao.Purlins.Select(t => t.Start.Y).OrderByDescending(y => y).ToList();
        double[] doModelo = [4.459, 3.336, 2.261, 1.138, 0.015, -1.109];

        Assert.Equal(6, ys.Count);
        for (var i = 0; i < 6; i++) Perto(doModelo[i], ys[i], 0.08, $"terça {i + 1}, Y");
        Assert.All(vao.Purlins, t => Perto(Padrao.Array.BayWidth, t.Length, 1e-9, "comprimento da terça"));
    }

    // ---------------------------------------------------------- terreno

    [Fact]
    [Trait("Etapa", "34")]
    public void NoDesnivelOPilarMaisAltoMandaNaAlturaLivre()
    {
        // O chão sobe 10 % para o lado alto (-Y): o pilar alto está mais em cima.
        var plano = PorticoDoModelo();
        var p = PorticoDoModelo(chao: (_, y) => -0.10 * y);
        var desnivel = p.HighBase.Z - p.LowBase.Z;

        Assert.Equal(p.HighBase.Z + 2.345, p.LowEdgeZ, 9);
        Assert.True(desnivel > 0.2);

        // O pilar alto fica do tamanho de sempre; o baixo cresce o desnível (ao longo do pilar inclinado).
        Perto(Barra(plano, CarportPart.Pillar, 0).Length, Barra(p, CarportPart.Pillar, 0).Length, 0.03, "pilar alto");
        Assert.True(Barra(p, CarportPart.Pillar, 1).Length > Barra(plano, CarportPart.Pillar, 1).Length + desnivel * 0.9,
            "o pilar do lado de chão mais baixo sai mais comprido");
        Assert.True(p.LowestAboveGround > 1.5);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void TudoSobeComOChao()
    {
        var plano = PorticoDoModelo();
        var alto = PorticoDoModelo(chao: Plano(812.4));

        Assert.Equal(plano.LowEdgeZ + 812.4, alto.LowEdgeZ, 9);
        Assert.Equal(Barra(plano, CarportPart.Beam, 0).End.Z + 812.4, Barra(alto, CarportPart.Beam, 0).End.Z, 6);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void NoDesnivelAoLongoDaFilaACoberturaDoVaoContinuaPlana()
    {
        var vao = VaoDoModelo(out var de, out var ate, chao: (x, _) => 0.04 * x);

        Assert.True(ate.LowEdgeZ > de.LowEdgeZ);

        // Todos os cantos de cima no mesmo plano: a distância ao plano dos três primeiros.
        var c = vao.Modules.SelectMany(m => m.Corners).ToList();
        var (a, b, d) = (c[0], c[1], c[^1]);
        var u = (X: b.X - a.X, Y: b.Y - a.Y, Z: b.Z - a.Z);
        var v = (X: d.X - a.X, Y: d.Y - a.Y, Z: d.Z - a.Z);
        var n = (X: u.Y * v.Z - u.Z * v.Y, Y: u.Z * v.X - u.X * v.Z, Z: u.X * v.Y - u.Y * v.X);
        var norma = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
        Assert.All(c, p => Assert.True(Math.Abs(((p.X - a.X) * n.X + (p.Y - a.Y) * n.Y + (p.Z - a.Z) * n.Z) / norma) < 1e-6));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OSentidoDaFilaEOLadoDaAguaGiramJunto()
    {
        // A mesma fila girada 90°: a fila em +Y e a água para a direita (+X).
        var p = CarportGeometry.Portal(Padrao, 0, 0, 0, 1, caiADireita: true, Plano(), out var motivo)!;
        Assert.NotNull(p);

        Assert.True(p.LowBase.X > p.HighBase.X);
        Assert.Equal(p.HighBase.Y, p.LowBase.Y, 9);
        Perto(Padrao.Frame.PillarBaseSpacing, p.LowBase.X - p.HighBase.X, 1e-9, "bases");
    }

    // ---------------------------------------------------------- recusas

    [Fact]
    [Trait("Etapa", "34")]
    public void PilarForaDoTerrenoNaoSai()
    {
        var p = CarportGeometry.Portal(Padrao, 0, 0, 1, 0, false, (_, y) => y > 0 ? null : 0, out var motivo);

        Assert.Null(p);
        Assert.Contains("fora do terreno", motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void VigaQueNaoAlcancaOPilarNaoSai()
    {
        var curta = Padrao with { Frame = Padrao.Frame with { BeamHighInset = 3.0 } };
        Assert.True(curta.IsValid, curta.WhyInvalid);

        var p = CarportGeometry.Portal(curta, 0, 0, 1, 0, false, Plano(), out var motivo);

        Assert.Null(p);
        Assert.Contains("não alcança", motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void SemSentidoDaFilaNaoSai()
    {
        Assert.Null(CarportGeometry.Portal(Padrao, 0, 0, 0, 0, false, Plano(), out var motivo));
        Assert.NotNull(motivo);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void UmaTercaPorModuloVaiNoMeio()
    {
        var uma = Padrao with { Frame = Padrao.Frame with { PurlinsPerModule = 1 } };

        var s = CarportGeometry.PurlinStations(uma);

        Assert.Equal(3, s.Count);
        Assert.Equal(Padrao.Array.ModuleDown / 2, s[0], 9);
    }
}
