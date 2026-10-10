using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Segunda rodada de 10/10/2026, item 6 (plano/melhorias-2026-10-10.md): a
/// vala que chega DENTRO da área dos inversores (ou da caixa do inversor) tem
/// prioridade absoluta, mesmo que o raio de 10 m dê, por outra vala, um
/// trajeto menor. Reproduz o desenho de Itatiba: a área com os inversores, a
/// vala que entra nela pelo sul, outra vala ao norte a menos de 10 m da área
/// e as strings ao norte. Os inversores do alto da área (o 9 e o 10 do
/// desenho) estavam sem o registro da área: o contorno era a caixa, nenhuma
/// vala entrava nela, e o raio os levava pela vala do norte.
/// </summary>
public class CableRouteAreaAccessTests
{
    // A área (sala dos inversores): 10 × 12 m.
    private static readonly Point3[] Area = [new(0, 0, 100), new(10, 0, 100), new(10, 12, 100), new(0, 12, 100)];

    // A vala que entra na área pelo sul (até y = 2) e a rede: ao sul, a leste até
    // o campo e subindo. A do norte sai da vala do leste e para a 4 m da área.
    private static readonly Point3[] EntraPeloSul = [new(5, 2, 0), new(5, -30, 0)];
    private static readonly Point3[] Sul = [new(5, -30, 0), new(40, -30, 0)];
    private static readonly Point3[] Leste = [new(40, -30, 0), new(40, 60, 0)];
    private static readonly Point3[] Norte = [new(40, 16, 0), new(5, 16, 0)];

    private static readonly RouteSettings Config = RouteSettings.Default(CableRoute.DirectCurrent);

    private static double? Chao(double x, double y) => 100;

    // Os inversores na área (o centro): os de cima a 7 m da vala do norte, mais perto que a do sul pelo caminho.
    private static readonly (Guid Id, Point3 Ponto)[] Inversores =
    [
        (Guid.NewGuid(), new Point3(3, 3, 100.8)),
        (Guid.NewGuid(), new Point3(7, 3, 100.8)),
        (Guid.NewGuid(), new Point3(3, 9, 100.8)),
        (Guid.NewGuid(), new Point3(7, 9, 100.8)),
    ];

    private static Point3[] Caixa(Point3 c) => [new(c.X - 0.55, c.Y - 0.35, 0), new(c.X + 0.55, c.Y - 0.35, 0), new(c.X + 0.55, c.Y + 0.35, 0), new(c.X - 0.55, c.Y + 0.35, 0)];

    // A mesa ao norte (x = 10 a 30, y = 40 a 44), plana: as strings saem pela borda de cima e vão para a vala do leste.
    private static readonly Point3[] Mesa = [new(10, 40, 101), new(30, 40, 101), new(30, 44, 101), new(10, 44, 101)];

    private static List<StringRouteInput> Strings(Func<Guid, Point3, IReadOnlyList<Point3>?> contorno)
    {
        var mesa = Guid.NewGuid();
        return Inversores.Select((inv, i) => new StringRouteInput(
                Guid.NewGuid(), $"S{i + 1}",
                new StringEndInput(new Point3(12 + i * 4, 43, 101), mesa, Mesa, Mesa),
                new StringEndInput(new Point3(13 + i * 4, 43, 101), mesa, Mesa, Mesa),
                new CableEnd(CableEndKind.Inverter, inv.Id), $"INV{i + 1}", inv.Ponto, null, contorno(inv.Id, inv.Ponto)))
            .ToList();
    }

    private static bool NaValaDoSulDentroDaArea(Point3 p) => Math.Abs(p.X - 5) < 1e-6 && p.Y > 0 && p.Y <= 2 + 1e-6;

    private static bool NaValaDoNorte(Point3 p) => Math.Abs(p.Y - 16) < 1e-6 && p.X < 40 - 1e-6;

    /// <summary>Todo cabo entra na área pela vala que entra nela e dali vai ao inversor por dentro da área; nenhum usa a vala do norte.</summary>
    private static void TodosPelaValaQueEntra(RouteResult r, int lances)
    {
        Assert.Empty(r.Failures);
        Assert.Equal(lances, r.Runs.Count);
        foreach (var l in r.Runs)
        {
            Assert.DoesNotContain(l.Path, NaValaDoNorte);
            var ultimo = l.Path.ToList().FindLastIndex(NaValaDoSulDentroDaArea);
            Assert.True(ultimo >= 0, "o cabo não passou pela vala que entra na área");
            Assert.All(l.Path.Skip(ultimo), p => Assert.True(Polygons.Contains(Area, p.X, p.Y), $"{p} fora da área depois da vala que entra nela"));
        }
    }

    /// <summary>
    /// O caso do desenho: nenhum inversor tem o registro da área (como o 9 e o
    /// 10), só a caixa. O contorno de acesso é a área onde eles estão em planta,
    /// e todos os cabos chegam pela vala que entra nela.
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ValaQueEntraNaAreaGanhaDoRaioMesmoSemORegistro()
    {
        var rede = new TrenchNetwork([EntraPeloSul, Sul, Leste, Norte]);
        var strings = Strings((_, p) => CableRouter.AccessOutline(p, Caixa(p), [Area]));

        TodosPelaValaQueEntra(CableRouter.Strings(strings, CableRoute.DirectCurrent, rede, Config, Chao), 8);
    }

    /// <summary>A vala do norte está mesmo no raio dos de cima, e por ela o caminho é menor: só a caixa (o erro) leva os cabos para lá.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void SoComACaixaORaioLevavaParaAValaDoNorte()
    {
        var rede = new TrenchNetwork([EntraPeloSul, Sul, Leste, Norte]);
        var (_, alto) = Inversores[3];
        Assert.Contains(rede.Within(alto, Config.Radius), t => Math.Abs(t.At.Y - 16) < 1e-6);

        var soCaixa = CableRouter.Strings(Strings((_, p) => Caixa(p)), CableRoute.DirectCurrent, rede, Config, Chao);
        Assert.Contains(soCaixa.Runs, l => l.Path.Any(NaValaDoNorte));
    }

    /// <summary>Com o registro da área (o caminho que já existia), o mesmo resultado; o Z dos pontos não conta (só XY).</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ComORegistroEComZDaValaNoTerrenoTambem()
    {
        // As valas assentadas no TIN (cota de centenas de metros) e a área com Z: o teste de "entra" é só em planta.
        Point3[] ComZ(Point3[] v) => v.Select(p => p with { Z = 742.3 }).ToArray();
        var rede = new TrenchNetwork([ComZ(EntraPeloSul), ComZ(Sul), ComZ(Leste), ComZ(Norte)]);
        var areaComZ = Area.Select(p => p with { Z = 735 }).ToArray();
        var strings = Strings((_, p) => CableRouter.AccessOutline(p, Caixa(p), [], registeredArea: areaComZ));

        TodosPelaValaQueEntra(CableRouter.Strings(strings, CableRoute.DirectCurrent, rede, Config, Chao), 8);
    }

    /// <summary>A vala que entra na área solta da rede das strings: aí sim vale o raio (cabo é melhor que falha).</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ValaQueEntraSoltaCaiNoRaio()
    {
        Point3[] solta = [new(5, 2, 0), new(5, -10, 0)];
        var rede = new TrenchNetwork([solta, Leste, Norte]);
        var strings = Strings((_, p) => CableRouter.AccessOutline(p, Caixa(p), [Area]));

        var r = CableRouter.Strings(strings, CableRoute.DirectCurrent, rede, Config, Chao);

        // Os de cima têm a vala do norte no raio: vão por ela; os de baixo não alcançam nenhuma que se ligue.
        Assert.All(r.Runs, l => Assert.Contains(l.Path, NaValaDoNorte));
        Assert.Equal(4, r.Runs.Count);
        Assert.Equal(2, r.Failures.Count);
    }

    /// <summary>A caixa com vala entrando nela, fora de toda área, continua valendo; fora de área e sem registro, a caixa.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void OContornoDeAcessoEAAreaOndeEleEsta()
    {
        var dentro = new Point3(7, 9, 0);
        var fora = new Point3(70, 9, 0);
        Point3[] outra = [new(60, 0, 0), new(80, 0, 0), new(80, 20, 0), new(60, 20, 0)];

        Assert.Same(Area, CableRouter.AccessOutline(dentro, Caixa(dentro), [outra, Area]));
        Assert.Same(Area, CableRouter.AccessOutline(dentro, Caixa(dentro), [Area], registeredArea: outra));
        Assert.Same(outra, CableRouter.AccessOutline(new Point3(200, 0, 0), null, [Area], registeredArea: outra));
        var caixa = Caixa(fora);
        Assert.Same(caixa, CableRouter.AccessOutline(new Point3(200, 0, 0), caixa, [Area]));
    }

    /// <summary>O CA do inversor da área também sai pela vala que entra nela.</summary>
    [Fact]
    [Trait("Etapa", "20")]
    public void CaDoInversorDaAreaSaiPelaValaQueEntra()
    {
        var rede = new TrenchNetwork([EntraPeloSul, Sul, Leste, Norte]);
        var (id, alto) = Inversores[3];
        var trafo = new Point3(42, 20, 100.8);
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Inverter, id), "INV4", alto, new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", trafo,
            CableRouter.AccessOutline(alto, Caixa(alto), [Area]), null);

        var r = CableRouter.Equipment([t], CableRoute.AlternatingCurrent, rede, RouteSettings.Default(CableRoute.AlternatingCurrent), Chao);

        var l = Assert.Single(r.Runs);
        Assert.Contains(l.Path, NaValaDoSulDentroDaArea);
        Assert.DoesNotContain(l.Path, NaValaDoNorte);
    }
}
