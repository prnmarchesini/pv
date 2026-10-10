using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// As melhorias da aba Inversor de 10/10/2026 (itens 2 a 8, 17 e 19 do
/// Renan): a área em grade sem sobrepor, a vaga livre do último, o estado
/// coerente da linha, o nome da área, o Repartir pela distribuição de
/// verdade, a soma dos limites e a pré-tag.
/// </summary>
public class InverterMelhoriasTests
{
    private static bool Dentro(IReadOnlyList<Point3> poligono, Point3 centro, double w, double l) =>
        InverterSites.Dentro(poligono, EquipmentFootprint.Corners(centro.X, centro.Y, w, l));

    private static void SemSobrepor(IReadOnlyList<Point3?> centros, double w, double l)
    {
        var postos = centros.OfType<Point3>().ToList();
        for (var i = 0; i < postos.Count; i++)
            for (var j = i + 1; j < postos.Count; j++)
                Assert.False(Math.Abs(postos[i].X - postos[j].X) < w - 1e-6 && Math.Abs(postos[i].Y - postos[j].Y) < l - 1e-6,
                    $"as caixas {i} e {j} se sobrepõem ({postos[i].X:0.###};{postos[i].Y:0.###} e {postos[j].X:0.###};{postos[j].Y:0.###})");
    }

    /// <summary>
    /// Item 2 (image2): área em pé cujo PRIMEIRO lado é o curto. Antes o eixo
    /// das fileiras saía paralelo ao das vagas: 9 inversores numa coluna no
    /// meio, um em cima do outro em 3 vagas. Agora: grade de colunas e
    /// linhas, todos dentro, nenhum sobreposto.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void NoveInversoresNaAreaEmPeFicamEmGradeSemSobrepor()
    {
        // 4,2 m de largura e 6 m de altura: começa pelo lado de baixo (o curto).
        Point3[] sala = [new(100, 200, 0), new(104.2, 200, 0), new(104.2, 206, 0), new(100, 206, 0)];

        var centros = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 9).ToList());

        Assert.All(centros, c => Assert.NotNull(c));
        Assert.All(centros, c => Assert.True(Dentro(sala, c!.Value, 1.1, 0.7)));
        SemSobrepor(centros, 1.1, 0.7);

        // Grade: mais de uma coluna e mais de uma linha.
        Assert.True(centros.Select(c => Math.Round(c!.Value.X, 3)).Distinct().Count() >= 2);
        Assert.True(centros.Select(c => Math.Round(c!.Value.Y, 3)).Distinct().Count() >= 2);
    }

    /// <summary>A mesma área girada 30°, começando pelo lado curto: dentro e sem sobrepor.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AreaGiradaComecandoPeloLadoCurto()
    {
        var (c, s) = (Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        Point3 R(double x, double y) => new(500 + x * c - y * s, 800 + x * s + y * c, 0);
        Point3[] sala = [R(0, 0), R(6, 0), R(6, 14), R(0, 14)];

        var centros = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 9).ToList());

        Assert.All(centros, p => Assert.True(p is { } x && Dentro(sala, x, 1.1, 0.7)));
        SemSobrepor(centros, 1.1, 0.7);
    }

    /// <summary>Área pequena: os que cabem vão, sem sobrepor; os outros voltam null (o comando avisa "área pequena, couberam N").</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AreaPequenaPoeOsQueCabemEDevolveNullParaOResto()
    {
        Point3[] sala = [new(0, 0, 0), new(3.8, 0, 0), new(3.8, 4, 0), new(0, 4, 0)];

        var centros = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 9).ToList());

        var postos = centros.Count(c => c is not null);
        Assert.InRange(postos, 4, 8);
        Assert.Contains(centros, c => c is null);
        Assert.All(centros.OfType<Point3>(), p => Assert.True(Dentro(sala, p, 1.1, 0.7)));
        SemSobrepor(centros, 1.1, 0.7);
    }

    /// <summary>
    /// Item 3: 9 já na área, o último sozinho entra na vaga livre (os 9 são
    /// obstáculo); com a área cheia, não entra (null: o comando avisa).
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void OUltimoSozinhoEntraNaVagaLivre()
    {
        Point3[] sala = [new(0, 0, 0), new(4.2, 0, 0), new(4.2, 6, 0), new(0, 6, 0)];
        var nove = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 9).ToList()).Select(p => p!.Value).ToList();
        var caixas = nove.Select(p => (IReadOnlyList<Point3>)EquipmentFootprint.Corners(p.X, p.Y, 1.1, 0.7).Select(q => new Point3(q.X, q.Y, 0)).ToList()).ToList();
        bool Livre(IReadOnlyList<(double X, double Y)> cantos) => !caixas.Any(o => InverterSites.Overlaps(cantos, o));

        var decimo = InverterSites.InArea(sala, [(1.1, 0.7)], livre: Livre)[0];

        Assert.NotNull(decimo);
        Assert.True(Dentro(sala, decimo!.Value, 1.1, 0.7));
        SemSobrepor([.. nove.Select(p => (Point3?)p), decimo], 1.1, 0.7);

        // Cheia: quantos couberem ao todo, e nenhum a mais.
        var todos = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 40).ToList()).OfType<Point3>().ToList();
        var ocupadas = todos.Select(p => (IReadOnlyList<Point3>)EquipmentFootprint.Corners(p.X, p.Y, 1.1, 0.7).Select(q => new Point3(q.X, q.Y, 0)).ToList()).ToList();
        Assert.Null(InverterSites.InArea(sala, [(1.1, 0.7)], livre: cantos => !ocupadas.Any(o => InverterSites.Overlaps(cantos, o)))[0]);
    }

    /// <summary>Item 4 e 19: a coluna Local e o botão de campo saem da mesma conta.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void ALinhaEhCoerente()
    {
        var inv = Guid.NewGuid();
        var area = Guid.NewGuid();
        var naArea = new InverterPlacement(inv, InverterPlacementMode.Area, area);
        var auto = new InverterPlacement(inv, InverterPlacementMode.Automatic, Guid.Empty);

        // Na área e em campo: o nome da área, Mover e Ver em campo.
        Assert.Equal(new InverterSiteView(InverterPlacementMode.Area, area, InverterFieldButton.Move, true), InverterSiteView.Of(naArea, emCampo: true, areaExiste: true));

        // O erro do print: "Área" com "Pôr em campo". Fora do campo (não coube, foi apagado) ou sem a área: à mão.
        Assert.Equal(new InverterSiteView(null, Guid.Empty, InverterFieldButton.Place, false), InverterSiteView.Of(naArea, emCampo: false, areaExiste: true));
        Assert.Equal(new InverterSiteView(null, Guid.Empty, InverterFieldButton.Move, true), InverterSiteView.Of(naArea, emCampo: true, areaExiste: false));

        // Automático fora de campo: o texto no lugar do botão, sem Ver em campo.
        Assert.Equal(new InverterSiteView(InverterPlacementMode.Automatic, Guid.Empty, InverterFieldButton.Automatic, false), InverterSiteView.Of(auto, emCampo: false, areaExiste: false));

        // Automático já posto pela rota: Mover e Ver em campo (item 19, "eu quero ter a liberdade de mover o inversor"); a coluna continua Auto.
        Assert.Equal(new InverterSiteView(InverterPlacementMode.Automatic, Guid.Empty, InverterFieldButton.Move, true), InverterSiteView.Of(auto, emCampo: true, areaExiste: false));

        // À mão.
        Assert.Equal(InverterFieldButton.Place, InverterSiteView.Of(null, emCampo: false, areaExiste: false).Button);
        Assert.Equal(InverterFieldButton.Move, InverterSiteView.Of(null, emCampo: true, areaExiste: false).Button);
    }

    /// <summary>Item 6: o nome padrão e a conferência do nome da área.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void ONomeDaArea()
    {
        Assert.Equal(Tr.F("Área {0}", 1), SiteMark.NextDefaultName([]));
        Assert.Equal(Tr.F("Área {0}", 3), SiteMark.NextDefaultName(["Sala A", Tr.F("Área {0}", 2)]));

        Assert.Null(SiteMark.NameProblem("Skid norte", ["Sala A"]));
        Assert.NotNull(SiteMark.NameProblem("  ", []));
        Assert.NotNull(SiteMark.NameProblem("sala a", ["Sala A"]));
        Assert.NotNull(SiteMark.NameProblem(new string('x', SiteMark.MaxNameLength + 1), []));
        Assert.NotNull(SiteMark.NameProblem("a\tb", []));
    }

    /// <summary>Item 5: potências iguais e kW iguais dão a mesma conta por quantidade (no máximo 1 a mais).</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void RepartirPelaFilaComPotenciasIguais()
    {
        var inv = Enumerable.Range(0, 20).Select(_ => new LimitShare(Guid.NewGuid(), 250, 24)).ToList();

        var r = BalancedLimits.SplitInOrder(inv, Enumerable.Repeat(0.6, 278).ToList());

        Assert.Equal(278, r.Limits.Values.Sum());
        Assert.All(r.Limits.Values, v => Assert.InRange(v, 13, 14));
        Assert.Equal(0, r.Leftover);
    }

    /// <summary>
    /// Item 5: a fila tem strings de potências diferentes (uma mesa de módulos
    /// maiores no começo): o limite segue a potência, não a quantidade. Dois
    /// inversores iguais: o primeiro leva menos strings (as fortes) e os dois
    /// ficam com o kWp mais perto possível.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void RepartirPelaFilaSegueAPotencia()
    {
        var a = new LimitShare(Guid.NewGuid(), 250, 30);
        var b = new LimitShare(Guid.NewGuid(), 250, 30);
        var fila = Enumerable.Repeat(20.0, 10).Concat(Enumerable.Repeat(10.0, 20)).ToList();   // 400 kWp

        var r = BalancedLimits.SplitInOrder([a, b], fila);

        Assert.Equal(10, r.Limits[a.Inverter]);   // 200 kWp
        Assert.Equal(20, r.Limits[b.Inverter]);   // 200 kWp
        Assert.Equal(0, r.Leftover);

        // Pelo kW: um de 300 e um de 100, mesma fila de 30 iguais: 3 para 1.
        var grande = new LimitShare(Guid.NewGuid(), 300, 30);
        var pequeno = new LimitShare(Guid.NewGuid(), 100, 30);
        var p = BalancedLimits.SplitInOrder([grande, pequeno], Enumerable.Repeat(1.0, 40).ToList());
        Assert.Equal(30, p.Limits[grande.Inverter]);
        Assert.Equal(10, p.Limits[pequeno.Inverter]);
    }

    /// <summary>Item 5: sem passar das entradas, sem deixar para os de depois mais do que cabe neles; a sobra é contada.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void RepartirPelaFilaRespeitaAsEntradas()
    {
        var a = new LimitShare(Guid.NewGuid(), 250, 10);
        var b = new LimitShare(Guid.NewGuid(), 250, 10);

        // Fila com tudo forte no fim: a conta pela potência daria 15 ao primeiro; as entradas seguram em 10.
        var fila = Enumerable.Repeat(1.0, 15).Concat(Enumerable.Repeat(3.0, 5)).ToList();
        var r = BalancedLimits.SplitInOrder([a, b], fila);
        Assert.Equal(10, r.Limits[a.Inverter]);
        Assert.Equal(10, r.Limits[b.Inverter]);

        var sobra = BalancedLimits.SplitInOrder([a, b], Enumerable.Repeat(1.0, 25).ToList());
        Assert.Equal(20, sobra.Limits.Values.Sum());
        Assert.Equal(5, sobra.Leftover);
    }

    /// <summary>
    /// Item 5, ponta a ponta no Core: os limites do Repartir, gravados como
    /// meta, fazem o Distribuir (do zero, na mesma varredura) dar exatamente
    /// esses números a cada inversor.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void ODistribuirDaOsTrechosDoRepartir()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", [12], new EquipmentSize(1.1, 0.7, 0.6), 250);
        var inversores = Enumerable.Range(1, 3).Select(n => new Inverter(Guid.NewGuid(), modelo.Id, $"Inversor {n}", Guid.Empty)).ToList();
        var modulos = new Dictionary<Guid, ModuleSpot>();
        var strings = new List<ElectricalString>();
        var mesa = Guid.NewGuid();
        for (var k = 0; k < 30; k++)
        {
            var m = Guid.NewGuid();
            modulos[m] = new ModuleSpot(mesa, k % 6 * 10, 100 - k / 6 * 5);
            strings.Add(new ElectricalString(Guid.NewGuid(), Guid.Empty, [m], Guid.Empty, string.Empty));
        }

        var fila = StringAutoAllocation.Queue(strings, modulos, AllocationScan.Default);
        var potencia = fila.Select((_, i) => i < 10 ? 2.0 : 1.0).ToList();
        var r = BalancedLimits.SplitInOrder(inversores.Select(i => new LimitShare(i.Id, 250, 12)).ToList(), potencia);

        var comMeta = inversores.Select(i => i with { Target = r.Limits[i.Id] }).ToList();
        var d = StringAutoAllocation.Allocate(comMeta, [modelo], strings, modulos, AllocationScan.Default);

        Assert.All(inversores, i => Assert.Equal(r.Limits[i.Id], d.Added.GetValueOrDefault(i.Id)));
    }

    /// <summary>Item 5: a soma dos limites contra as strings úteis.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void ASomaDosLimites()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", [12, 12], new EquipmentSize(1.1, 0.7, 0.6), 250);
        Inverter[] inv =
        [
            new(Guid.NewGuid(), modelo.Id, "Inversor 1", Guid.Empty, Target: 20),
            new(Guid.NewGuid(), modelo.Id, "Inversor 2", Guid.Empty),               // sem limite: 24
            new(Guid.NewGuid(), Guid.NewGuid(), "Inversor 3", Guid.Empty, Target: 5), // sem modelo: 0
        ];

        Assert.Equal(44, BalancedLimits.Sum(inv, [modelo]));
        Assert.True(BalancedLimits.Describe(44, 44).Matches);
        Assert.Equal(Tr.F("Limites: {0} de {1} strings úteis", 44, 44), BalancedLimits.Describe(44, 44).Text);
        Assert.False(BalancedLimits.Describe(40, 44).Matches);
        Assert.False(BalancedLimits.Describe(48, 44).Matches);
    }

    /// <summary>Item 8: o nome curto do inversor e quais strings têm pré-tag.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void APreTag()
    {
        Assert.Equal("I3", StringPreTag.ShortName("Inversor 3", 7));
        Assert.Equal("I12", StringPreTag.ShortName("INV-012", 1));
        Assert.Equal("I2", StringPreTag.ShortName("Sala norte", 2));

        var i1 = new Inverter(Guid.NewGuid(), Guid.NewGuid(), "Inversor 1", Guid.Empty);
        var i2 = new Inverter(Guid.NewGuid(), Guid.NewGuid(), "Central", Guid.Empty);
        ElectricalString S(Guid inversor, string tag = "") => new(Guid.NewGuid(), Guid.Empty, [], inversor, tag);
        var livre = S(Guid.Empty);
        var doUm = S(i1.Id);
        var doDois = S(i2.Id);
        var comTag = S(i1.Id, "1.1.1");
        var orfa = S(Guid.NewGuid());

        var p = StringPreTag.Expected([i1, i2], [livre, doUm, doDois, comTag, orfa]);

        Assert.Equal(2, p.Count);
        Assert.Equal("I1", p[doUm.Id]);
        Assert.Equal("I2", p[doDois.Id]);

        var t = new StringPreTag(doUm.Id, "I1");
        Assert.Equal(t, StringPreTag.Parse(t.ToFields()));

        // O Apagar da Edição leva a pré-tag como leva a tag.
        Assert.Equal(CleanupTarget.StringTag, Cleanup.Classify(StringPreTag.Tipo, isText: true));
    }
}
