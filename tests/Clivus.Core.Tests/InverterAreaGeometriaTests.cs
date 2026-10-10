using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A segunda rodada de 10/10/2026 na aba Inversor (itens 1, 2, 4 e 5): a
/// área cheia que recebe os inversores novos (reincidência do item 2, com a
/// geometria do Itatiba do Renan), o local pela geometria e não pelo caminho,
/// o "À mão" da coluna Local, o total dos limites e as opções da pré-tag.
/// </summary>
public class InverterAreaGeometriaTests
{
    // A Área 1 do "Curvas Itatiba.dwg" do Renan (10/10/2026, 19:24), lida da
    // cópia no Core Console: a Polyline3d drapeada, com os vértices do TIN.
    private static readonly Point3[] Area1 =
    [
        new(314026.5987, 7456107.8598, 721.0194), new(314029.95, 7456107.8598, 722), new(314030.3704, 7456107.8598, 722.1188),
        new(314030.3704, 7456102.706, 722.3446), new(314029.1507, 7456102.706, 722), new(314026.5987, 7456102.706, 721.2518),
    ];

    // As caixas (1,10 × 0,70 m) dos Inversores 1 a 8 que o Escolher área pôs nela (ROTA_EQUIP da mesma leitura).
    private static readonly (double MinX, double MinY)[] OsOito =
    [
        (314026.609, 7456102.716), (314026.609, 7456103.916), (314026.609, 7456105.116), (314026.609, 7456106.316),
        (314028.209, 7456102.716), (314028.209, 7456103.916), (314028.209, 7456105.116), (314028.209, 7456106.316),
    ];

    private static IReadOnlyList<Point3> Caixa(double minX, double minY) =>
        [new(minX, minY, 0), new(minX + 1.1, minY, 0), new(minX + 1.1, minY + 0.7, 0), new(minX, minY + 0.7, 0)];

    private static IReadOnlyList<Point3> CaixaNoCentro(Point3 c) => Caixa(c.X - 0.55, c.Y - 0.35);

    /// <summary>A distância entre duas caixas alinhadas aos eixos (0 se se tocam ou sobrepõem).</summary>
    private static double Distancia(IReadOnlyList<Point3> a, IReadOnlyList<Point3> b)
    {
        var dx = Math.Max(0, Math.Max(a.Min(p => p.X) - b.Max(p => p.X), b.Min(p => p.X) - a.Max(p => p.X)));
        var dy = Math.Max(0, Math.Max(a.Min(p => p.Y) - b.Max(p => p.Y), b.Min(p => p.Y) - a.Max(p => p.Y)));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool Dentro(IReadOnlyList<Point3> poligono, Point3 c, double w, double l) =>
        InverterSites.Dentro(poligono, EquipmentFootprint.Corners(c.X, c.Y, w, l));

    /// <summary>
    /// A causa raiz do item 2, no desenho do Renan: com os 8 na área, a grade
    /// de 0,50 m não tem vaga (o "0 de 2" que só foi para a linha de comando).
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void NoItatibaAGradeDeMeioMetroNaoTinhaVagaParaO9EO10()
    {
        var oito = OsOito.Select(c => Caixa(c.MinX, c.MinY)).ToList();
        var grade = InverterSites.InArea(Area1, [(1.1, 0.7), (1.1, 0.7)], livre: cantos => !oito.Any(o => InverterSites.Overlaps(cantos, o)));
        Assert.All(grade, c => Assert.Null(c));

        var (comprido, curto) = InverterSites.Measures(Area1);
        Assert.Equal(5.154, comprido, 3);
        Assert.Equal(3.772, curto, 3);
    }

    /// <summary>
    /// A correção: o Encher põe o 9 e o 10 na faixa livre de cima, dentro da
    /// área, sem tocar nos 8 nem um no outro, com a maior folga que cabe
    /// (0,10 m: a faixa tem 0,84 m e a caixa 0,70 m).
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void NoItatibaO9EO10EntramNaAreaComOs8()
    {
        var oito = OsOito.Select(c => Caixa(c.MinX, c.MinY)).ToList();

        var r = InverterSites.Fill(Area1, [(1.1, 0.7), (1.1, 0.7)], oito);

        Assert.Equal(2, r.Placed);
        var novos = r.Centers.Select(c => c!.Value).ToList();
        Assert.All(novos, c => Assert.True(Dentro(Area1, c, 1.1, 0.7)));
        var caixas = novos.Select(CaixaNoCentro).ToList();
        foreach (var n in caixas)
            Assert.All(oito, o => Assert.True(Distancia(n, o) >= InverterSites.MinGap - 1e-6, "encostou num dos 8"));
        Assert.True(Distancia(caixas[0], caixas[1]) >= InverterSites.MinGap - 1e-6);
        Assert.Equal(0.1, r.SmallestGap!.Value, 6);
    }

    /// <summary>Os 10 de uma vez na área vazia também entram (8 na grade, 2 na faixa de cima).</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void NoItatibaOs10DeUmaVezEntram()
    {
        var r = InverterSites.Fill(Area1, Enumerable.Repeat((1.1, 0.7), 10).ToList(), []);

        Assert.Equal(10, r.Placed);
        var caixas = r.Centers.Select(c => CaixaNoCentro(c!.Value)).ToList();
        Assert.All(r.Centers, c => Assert.True(Dentro(Area1, c!.Value, 1.1, 0.7)));
        for (var i = 0; i < caixas.Count; i++)
            for (var j = i + 1; j < caixas.Count; j++)
                Assert.True(Distancia(caixas[i], caixas[j]) >= InverterSites.MinGap - 1e-6, $"{i} e {j} sem folga");

        // Os 8 da grade com a folga cheia; só os 2 da faixa com a reduzida.
        Assert.Equal(8, r.Gaps.Count(g => g == InverterSites.Gap));
    }

    /// <summary>Área cheia de verdade: quem não cabe nem com 0,10 m volta null (o comando diz quantos e por quê).</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AreaCheiaDeVerdadeDevolveNull()
    {
        var r = InverterSites.Fill(Area1, Enumerable.Repeat((1.1, 0.7), 14).ToList(), []);
        Assert.InRange(r.Placed, 10, 13);
        Assert.Contains(r.Centers, c => c is null);

        // Um obstáculo cobrindo a área inteira: nada entra.
        Point3[] tudo = [new(314020, 7456100, 0), new(314040, 7456100, 0), new(314040, 7456110, 0), new(314020, 7456110, 0)];
        Assert.Equal(0, InverterSites.Fill(Area1, [(1.1, 0.7)], [tudo]).Placed);
    }

    /// <summary>A área girada 30°: quem passa da grade também entra inteiro e sem encostar.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AreaGiradaRecebeOsQuePassamDaGrade()
    {
        var (c, s) = (Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        Point3 R(double x, double y) => new(500 + x * c - y * s, 800 + x * s + y * c, 0);
        Point3[] sala = [R(0, 0), R(6, 0), R(6, 8), R(0, 8)];

        var grade = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 30).ToList()).Count(p => p is not null);
        var r = InverterSites.Fill(sala, Enumerable.Repeat((1.1, 0.7), 30).ToList(), []);

        Assert.True(r.Placed >= grade);
        var postos = r.Centers.OfType<Point3>().ToList();
        Assert.All(postos, p => Assert.True(Dentro(sala, p, 1.1, 0.7)));
        for (var i = 0; i < postos.Count; i++)
            for (var j = i + 1; j < postos.Count; j++)
                Assert.True(Distancia(CaixaNoCentro(postos[i]), CaixaNoCentro(postos[j])) >= InverterSites.MinGap - 1e-6);
    }

    /// <summary>
    /// A regra única "o inversor é da área": o CENTRO dentro do contorno. O
    /// 9 que o Renan pôs à mão (a caixa passa 6,5 cm da borda de cima) é da
    /// Área 1; fora dela, de nenhuma; numa área dentro de outra, da menor.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void OCentroDentroDoContornoEhDaArea()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var areas = new List<(Guid, IReadOnlyList<Point3>)> { (a1, Area1) };

        Assert.Equal(a1, InverterSites.AreaOf(314027.154, 7456107.575, areas));    // o Inversor 9 do print
        Assert.Equal(a1, InverterSites.AreaOf(314029.042, 7456107.601, areas));    // o Inversor 10
        Assert.Null(InverterSites.AreaOf(314027.154, 7456108.2, areas));            // acima da borda
        Assert.Null(InverterSites.AreaOf(313989.109, 7456189.25, areas));           // o automático ao lado da vala

        Point3[] grande = [new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), new(0, 100, 0)];
        Point3[] dentro = [new(10, 10, 0), new(20, 10, 0), new(20, 20, 0), new(10, 20, 0)];
        var aninhadas = new List<(Guid, IReadOnlyList<Point3>)> { (a1, grande), (a2, dentro) };
        Assert.Equal(a2, InverterSites.AreaOf(15, 15, aninhadas));
        Assert.Equal(a1, InverterSites.AreaOf(50, 50, aninhadas));
    }

    /// <summary>
    /// O registro pela geometria, não pelo caminho: posto à mão dentro da
    /// área é dela; movido para fora, deixa de ser (à mão); o automático em
    /// campo dentro da área é da área, fora continua automático; fora de
    /// campo, o registro de área cai e o automático fica.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void ORegistroSegueAGeometria()
    {
        var area = Guid.NewGuid();
        var areas = new List<(Guid, IReadOnlyList<Point3>)> { (area, Area1) };
        var (maoDentro, maoFora, areaFora, autoDentro, autoFora, autoForaDeCampo, areaForaDeCampo, outro) =
            (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        List<InverterPlacement> registro =
        [
            new(areaFora, InverterPlacementMode.Area, area),
            new(autoDentro, InverterPlacementMode.Automatic, Guid.Empty),
            new(autoFora, InverterPlacementMode.Automatic, Guid.Empty),
            new(autoForaDeCampo, InverterPlacementMode.Automatic, Guid.Empty),
            new(areaForaDeCampo, InverterPlacementMode.Area, area),
            new(outro, InverterPlacementMode.Area, area),
        ];
        var dentro = (314027.154, 7456107.575);
        var fora = (313989.109, 7456189.25);
        var emCampo = new Dictionary<Guid, (double, double)>
        {
            [maoDentro] = dentro, [maoFora] = fora, [areaFora] = fora, [autoDentro] = dentro, [autoFora] = fora,
        };

        var novo = InverterSites.Reconcile(registro, [maoDentro, maoFora, areaFora, autoDentro, autoFora, autoForaDeCampo, areaForaDeCampo], emCampo, areas)
            .ToDictionary(p => p.Inverter);

        Assert.Equal(new InverterPlacement(maoDentro, InverterPlacementMode.Area, area), novo[maoDentro]);
        Assert.False(novo.ContainsKey(maoFora));
        Assert.False(novo.ContainsKey(areaFora));
        Assert.Equal(InverterPlacementMode.Area, novo[autoDentro].Mode);
        Assert.Equal(InverterPlacementMode.Automatic, novo[autoFora].Mode);
        Assert.Equal(InverterPlacementMode.Automatic, novo[autoForaDeCampo].Mode);
        Assert.False(novo.ContainsKey(areaForaDeCampo));
        Assert.Equal(InverterPlacementMode.Area, novo[outro].Mode);    // de outro cadastro: fica como estava

        // Sem mudança, a mesma lista (o registro não é regravado à toa).
        var deNovo = InverterSites.Reconcile([.. novo.Values], [maoDentro, maoFora, areaFora, autoDentro, autoFora, autoForaDeCampo, areaForaDeCampo], emCampo, areas);
        Assert.True(InverterSites.SamePlacements(deNovo, [.. novo.Values]));
    }

    /// <summary>Item 4: a coluna Local diz "À mão" para o posto à mão em campo; vazia fora de campo.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AColunaLocalDizAMao()
    {
        var inv = Guid.NewGuid();
        var area = Guid.NewGuid();
        Assert.Equal("À mão", InverterSiteView.Of(null, emCampo: true, areaExiste: false).LocalText(null));
        Assert.Equal(string.Empty, InverterSiteView.Of(null, emCampo: false, areaExiste: false).LocalText(null));
        Assert.Equal("Sala 1", InverterSiteView.Of(new InverterPlacement(inv, InverterPlacementMode.Area, area), emCampo: true, areaExiste: true).LocalText("Sala 1"));
        Assert.Equal("Auto", InverterSiteView.Of(new InverterPlacement(inv, InverterPlacementMode.Automatic, Guid.Empty), emCampo: false, areaExiste: false).LocalText(null));
        Assert.Equal("Auto", InverterSiteView.Of(new InverterPlacement(inv, InverterPlacementMode.Automatic, Guid.Empty), emCampo: true, areaExiste: false).LocalText(null));
    }

    /// <summary>Item 1: o total da coluna Limite.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void OTotalDoLimite()
    {
        Assert.Equal(("278", true), BalancedLimits.TotalCell(278, 278));
        Assert.Equal(("278/480 úteis", false), BalancedLimits.TotalCell(278, 480));
    }

    /// <summary>Item 5: as opções da pré-tag, tudo sim por padrão, ida e volta do registro.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AsOpcoesDaPreTag()
    {
        Assert.Equal(new PreTagOptions(true, true, true), PreTagOptions.Default);
        var o = new PreTagOptions(false, true, false);
        Assert.Equal(o, PreTagOptions.Parse(o.ToFields()));
        Assert.Null(PreTagOptions.Parse(["1", "x", "0"]));
        Assert.Null(PreTagOptions.Parse(["1", "1"]));
    }
}
