using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A rodada de tela de 10/10/2026 na rota de cabos (plano/melhorias-2026-10-10.md):
/// item 10 (uma entrada por mesa, pelo lado alto), 12 (resumo agrupado UC >
/// trafo > inversor com subtotais), 16 (contas por string), 18 (o resumo só
/// com o que está em campo) e 19 (o inversor automático libera a aba CC).
/// </summary>
public class CableRouteMelhoriasTests
{
    // ---- item 10: uma entrada por mesa, pelo lado alto

    // Mesa de 20 × 4 m ao longo de x (x = 30 a 50), com a borda 0-1 em y = 0 e a
    // 2-3 em y = 4. Valas norte-sul nas duas pontas, ligadas ao sul e ao norte.
    private static readonly Point3[] Oeste = [new(0, 60, 0), new(0, -50, 0)];
    private static readonly Point3[] Leste = [new(100, 60, 0), new(100, -50, 0)];
    private static readonly Point3[] Sul = [new(0, -50, 0), new(100, -50, 0)];
    private static readonly Point3 Inversor = new(60, -52, 100.8);
    private static readonly RouteSettings Config = RouteSettings.Default(CableRoute.DirectCurrent);

    private static double? Chao(double x, double y) => 100;

    /// <summary>A mesa com a borda 2-3 (y = 4) mais alta (<paramref name="altaEm4"/>) ou a 0-1 (y = 0).</summary>
    private static Point3[] Mesa(bool altaEm4, double x0 = 30) =>
        altaEm4
            ? [new(x0, 0, 101), new(x0 + 20, 0, 101), new(x0 + 20, 4, 102.4), new(x0, 4, 102.4)]
            : [new(x0, 0, 102.4), new(x0 + 20, 0, 102.4), new(x0 + 20, 4, 101), new(x0, 4, 101)];

    private static StringRouteInput Str(Guid mesa, Point3[] cantos, Point3 mais, Point3 menos, RowEnd? forcado = null) => new(
        Guid.NewGuid(), "S",
        new StringEndInput(mais, mesa, cantos, cantos),
        new StringEndInput(menos, mesa, cantos, cantos),
        new CableEnd(CableEndKind.Inverter, InversorId), "INV1", Inversor, forcado);

    private static readonly Guid InversorId = Guid.NewGuid();

    /// <summary>O ponto da saída de um lance: o primeiro ponto do percurso fora da mesa (em planta).</summary>
    private static Point3 Saida(PlannedRun l, Point3[] mesa) => l.Path.Skip(1).First(p => !Polygons.Contains(mesa, p.X, p.Y));

    /// <summary>
    /// A mesa com duas strings, uma na fileira de baixo dos módulos (y = 1) e
    /// outra na de cima (y = 3), como na imagem do Renan: antes cada uma saía
    /// pela borda mais perto (duas entradas). Agora as quatro pontas saem pela
    /// borda alta e todas passam por um ponto só.
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void MesaComDuasStringsTemUmaEntradaSo()
    {
        var mesa = Guid.NewGuid();
        var cantos = Mesa(altaEm4: true);
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var baixo = Str(mesa, cantos, new(31, 1, 101.4), new(32, 1, 101.4));
        var cima = Str(mesa, cantos, new(31, 3, 102), new(32, 3, 102));

        var r = CableRouter.Strings([baixo, cima], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Failures);
        Assert.Equal(4, r.Runs.Count);

        // Todas saem pela borda alta (y = 4 + 0,5).
        Assert.All(r.Runs, l => Assert.Equal(4.5, Saida(l, cantos).Y, 6));

        // E todas passam por um ponto comum fora da mesa (a entrada da mesa).
        var comuns = r.Runs.Select(l => l.Path.Select(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToHashSet())
            .Aggregate((a, b) => { a.IntersectWith(b); return a; });
        Assert.Contains(comuns, p => !Polygons.Contains(cantos, p.Item1, p.Item2) && Math.Abs(p.Item2 - 4.5) < 1e-6);

        // Nunca pelo miolo: tirando o módulo, nenhum ponto dentro da mesa.
        Assert.All(r.Runs, l => Assert.All(l.Path.Skip(1), p => Assert.False(Polygons.Contains(cantos, p.X, p.Y), $"{p} dentro da mesa")));
    }

    /// <summary>O lado alto ganha, mesmo com a ponta da string perto da borda baixa; trocando a altura, troca o lado.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void OLadoAltoGanha()
    {
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);

        var altaNoNorte = Mesa(altaEm4: true);
        var r1 = CableRouter.Strings([Str(Guid.NewGuid(), altaNoNorte, new(31, 0.5, 101), new(32, 0.5, 101))], CableRoute.DirectCurrent, rede, Config, Chao);
        Assert.All(r1.Runs, l => Assert.Equal(4.5, Saida(l, altaNoNorte).Y, 6));

        var altaNoSul = Mesa(altaEm4: false);
        var r2 = CableRouter.Strings([Str(Guid.NewGuid(), altaNoSul, new(31, 3.5, 101), new(32, 3.5, 101))], CableRoute.DirectCurrent, rede, Config, Chao);
        Assert.All(r2.Runs, l => Assert.Equal(-0.5, Saida(l, altaNoSul).Y, 6));
    }

    /// <summary>
    /// O padrão é da usina: a mesa que num terreno torto ficou com a borda
    /// baixa mais alta sai pelo mesmo lado das outras (o da maioria).
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void OPadraoEDaUsinaInteira()
    {
        var lado = ExitPattern.PlantSide([Mesa(true, 0), Mesa(true, 30), Mesa(false, 60)]);

        Assert.NotNull(lado);
        Assert.Equal(0, lado!.Value.X, 9);
        Assert.Equal(1, lado.Value.Y, 9);

        // Sem altura nenhuma (mesas planas), sem padrão: cada ponta pela borda mais perto, como era.
        Assert.Null(ExitPattern.PlantSide([[new(0, 0, 5), new(20, 0, 5), new(20, 4, 5), new(0, 4, 5)]]));

        var torta = Mesa(altaEm4: false);
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var s = Str(Guid.NewGuid(), torta, new(31, 1, 101), new(32, 1, 101));
        var r = CableRouter.Strings([s], CableRoute.DirectCurrent, rede, Config, Chao, ExitPattern.For([s], lado));
        Assert.All(r.Runs, l => Assert.Equal(4.5, Saida(l, torta).Y, 6));
    }

    /// <summary>Mesa plana (sem lado alto, nem padrão da usina): ainda um lado só por mesa, o mais perto do meio das pontas.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void MesaPlanaTambemTemUmLadoSo()
    {
        Point3[] plana = [new(30, 0, 101), new(50, 0, 101), new(50, 4, 101), new(30, 4, 101)];
        var mesa = Guid.NewGuid();
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var baixo = Str(mesa, plana, new(31, 0.5, 101), new(32, 0.5, 101));
        var cima = Str(mesa, plana, new(31, 3.5, 101), new(32, 3.5, 101));
        var maisEmCima = Str(mesa, plana, new(33, 3.5, 101), new(34, 3.5, 101));

        var r = CableRouter.Strings([baixo, cima, maisEmCima], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Failures);
        Assert.Single(r.Runs.Select(l => Math.Round(Saida(l, plana).Y, 6)).Distinct());
        Assert.All(r.Runs, l => Assert.Equal(4.5, Saida(l, plana).Y, 6));
    }

    /// <summary>As strings de uma mesa vão todas para a mesma ponta da fileira; o "Forçar lado" continua valendo por cima.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void AsStringsDaMesaVaoParaAMesmaPontaEOForcadoVale()
    {
        // Sem vala ao sul, a volta pelo norte: a ponta oeste da fileira é bem mais curta para as duas.
        var norte = new Point3[] { new(0, 60, 0), new(100, 60, 0) };
        var rede = new TrenchNetwork([Oeste, Leste, norte]);
        var mesa = Guid.NewGuid();
        var cantos = Mesa(altaEm4: true);
        var inv = new Point3(10, 62, 100.8);
        var a = Str(mesa, cantos, new(31, 1, 101), new(32, 1, 101)) with { DestinationPoint = inv };
        var b = Str(mesa, cantos, new(48, 3, 101), new(49, 3, 101)) with { DestinationPoint = inv };

        var r = CableRouter.Strings([a, b], CableRoute.DirectCurrent, rede, Config, Chao);
        Assert.Empty(r.Failures);
        Assert.All(r.Runs, l => Assert.Contains(l.Path, p => p.X < 0.01));
        Assert.All(r.Runs, l => Assert.DoesNotContain(l.Path, p => p.X > 99));

        var forcada = CableRouter.Strings([a, b with { ForcedEnd = RowEnd.End }], CableRoute.DirectCurrent, rede, Config, Chao);
        Assert.All(forcada.Runs.Where(l => l.Run.From.Id == b.String), l => Assert.Contains(l.Path, p => p.X > 99));
        Assert.All(forcada.Runs.Where(l => l.Run.From.Id == a.String), l => Assert.DoesNotContain(l.Path, p => p.X > 99));
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void OPontoDeJuntarEntraNoCaminho()
    {
        var cantos = Mesa(altaEm4: true);
        var (pontos, _) = RowExit.Plan(new Point3(35, 1, 0), cantos, cantos, RowEnd.Start, RowExit.Margin, new Point3(0, 1, 0), 32);

        Assert.Equal(3, pontos.Count);
        Assert.Equal(new Point3(35, 4.5, 0), pontos[0]);
        Assert.Equal(new Point3(32, 4.5, 0), pontos[1]);
        Assert.Equal(new Point3(29.5, 4.5, 0), pontos[2]);

        // O ponto de juntar atrás (a própria ponta é a mais adiantada) não entra.
        Assert.Equal(2, RowExit.Plan(new Point3(31, 1, 0), cantos, cantos, RowEnd.Start, RowExit.Margin, new Point3(0, 1, 0), 32).Points.Count);
    }

    // ---- item 19: o inversor automático libera a aba CC

    [Fact]
    [Trait("Etapa", "17")]
    public void InversorAutomaticoForaDeCampoLiberaACc()
    {
        var caixa = new EquipmentSize(2, 1, 2.2);
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, caixa);
        var t = new Transformer(Guid.NewGuid(), "Seco", "T1", 800, 13800, 2500, 4, 6.5, "", caixa, Guid.Empty);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "INV1", t.Id);
        var setup = new ElectricalSetup(transformers: [t], inverters: [i1], models: [modelo]);
        var nadaEmCampo = new HashSet<(EquipmentKind, Guid)>();

        var semAuto = CableRoutes.Drawing(setup, 10, nadaEmCampo);
        Assert.NotEmpty(CableRoutes.Missing(CableRoute.DirectCurrent, semAuto));

        var comAuto = CableRoutes.Drawing(setup, 10, nadaEmCampo, new HashSet<Guid> { i1.Id });
        Assert.Equal(1, comAuto.Inverters.Automatic);
        Assert.Empty(CableRoutes.Missing(CableRoute.DirectCurrent, comAuto));

        // O CA e a Combiner continuam pedindo o inversor em campo (o Gerar CC é quem o põe).
        Assert.Contains(CableRoutes.Missing(CableRoute.AlternatingCurrent, comAuto), f => f.Contains("inversor"));

        // Já em campo, não conta como automático pendente.
        var emCampo = CableRoutes.Drawing(setup, 10, new HashSet<(EquipmentKind, Guid)> { (EquipmentKind.Inverter, i1.Id) }, new HashSet<Guid> { i1.Id });
        Assert.Equal(0, emCampo.Inverters.Automatic);
    }

    // ---- itens 12 e 18: o resumo agrupado e só com o que está em campo

    private static readonly EquipmentSize Caixa = new(2, 1, 2.2);

    /// <summary>UC1 com dois trafos (T1 com dois inversores, T2 com um) e UC2 com T3.</summary>
    private static (ElectricalSetup Setup, Inverter[] Inv, Transformer[] Trafos) Usina()
    {
        var u1 = new ConsumerUnit(Guid.NewGuid(), "UC1", "Subestação Norte", ConsumerUnitMode.Unitary, Caixa);
        var u2 = new ConsumerUnit(Guid.NewGuid(), "UC2", "", ConsumerUnitMode.Unitary, Caixa);
        var t1 = new Transformer(Guid.NewGuid(), "Seco", "T1", 800, 13800, 2500, 4, 6.5, "", Caixa, u1.Id);
        var t2 = new Transformer(Guid.NewGuid(), "Seco", "T2", 800, 13800, 2500, 4, 6.5, "", Caixa, u1.Id);
        var t3 = new Transformer(Guid.NewGuid(), "Seco", "T3", 800, 13800, 2500, 4, 6.5, "", Caixa, u2.Id);
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, Caixa);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", t1.Id);
        var i2 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 2", t1.Id);
        var i10 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 10", t2.Id);
        var i3 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 3", t3.Id);
        return (new ElectricalSetup(transformers: [t1, t2, t3], inverters: [i1, i2, i10, i3], models: [modelo], units: [u1, u2]), [i1, i2, i10, i3], [t1, t2, t3]);
    }

    private static CableReport.CircuitRun Cc(Inverter inv, string tag, double comprimento, int vias = 1) =>
        new(CableRoute.DirectCurrent, new(CableEndKind.String, Guid.NewGuid()), new(CableEndKind.Inverter, inv.Id), tag, inv.Name, 2, comprimento, vias, null, "D");

    [Fact]
    [Trait("Etapa", "24")]
    public void OResumoCcAgrupaUcTrafoInversorESoma()
    {
        var (setup, inv, _) = Usina();
        var circuitos = new[]
        {
            Cc(inv[0], "T1-1S2", 100), Cc(inv[0], "T1-1S1", 50), Cc(inv[1], "T1-2S1", 30),
            Cc(inv[2], "T2-10S1", 20, vias: 2), Cc(inv[3], "T3-3S1", 40),
        };

        var grupos = CableReport.Group(circuitos, c => CableReport.GroupPath(setup, c));

        Assert.Equal(["UC1", "UC2"], grupos.Select(g => g.Name));
        var uc1 = grupos[0];
        Assert.Equal(["T1", "T2"], uc1.Children.Select(g => g.Name));
        Assert.Equal(["Inversor 1", "Inversor 2"], uc1.Children[0].Children.Select(g => g.Name));

        // A UC soma os dois trafos: cabos (lances × vias) e metros de cabo.
        Assert.Equal(2 + 2 + 2 + 4, uc1.Cables);
        Assert.Equal(100 + 50 + 30 + 40, uc1.CableLength, 9);
        Assert.Equal(4, uc1.CircuitCount);

        // O inversor tem as strings, em ordem natural.
        var inversor1 = uc1.Children[0].Children[0];
        Assert.Equal(["T1-1S1", "T1-1S2"], inversor1.Circuits.Select(c => c.FromName));
        Assert.Equal(150, inversor1.CableLength, 9);

        // A tabela do Excel: as linhas de grupo com o subtotal, os circuitos com a tag, o total da usina embaixo.
        var tabela = CableReport.GroupedCircuits("Resumo CC", grupos, dc: true, routeColumn: false);
        Assert.Equal("Tag da string", tabela.Header[1]);
        Assert.Equal("UC1", tabela.Rows[0][0]);
        var colunaTotal = tabela.Header.ToList().IndexOf("Total de cabo (m)");
        var colunaCabos = tabela.Header.ToList().IndexOf("Cabos");
        Assert.Equal(220.0, (double)tabela.Rows[0][colunaTotal]!, 9);
        Assert.Equal(10.0, tabela.Rows[0][colunaCabos]);
        Assert.Equal("    T1", tabela.Rows[1][0]);
        Assert.Equal("        Inversor 1", tabela.Rows[2][0]);
        Assert.Equal("T1-1S1", tabela.Rows[3][1]);
        // UC1, T1, Inversor 1 (2 strings), Inversor 2 (1), T2, Inversor 10 (1), UC2, T3, Inversor 3 (1).
        Assert.Equal(14, tabela.Rows.Count);
        Assert.Equal(260.0, (double)tabela.Total[colunaTotal]!, 9);
    }

    [Fact]
    [Trait("Etapa", "24")]
    public void OResumoCaAgrupaUcTrafoEOMtUc()
    {
        var (setup, inv, trafos) = Usina();
        var ca = inv.Select(i => new CableReport.CircuitRun(CableRoute.AlternatingCurrent, new(CableEndKind.Inverter, i.Id), new(CableEndKind.Transformer, i.Transformer), i.Name, "T", 1, 10, 3, null, "D"));
        var grupos = CableReport.Group(ca, c => CableReport.GroupPath(setup, c));
        Assert.Equal(["T1", "T2"], grupos[0].Children.Select(g => g.Name));
        Assert.Equal(2, grupos[0].Children[0].Circuits.Count);
        Assert.Empty(grupos[0].Children[0].Children);

        var mt = trafos.Select(t => new CableReport.CircuitRun(CableRoute.MediumVoltage, new(CableEndKind.Transformer, t.Id), new(CableEndKind.Substation, t.ConsumerUnit), t.Nickname, "UC", 1, 100, 3, null, "D"));
        var gruposMt = CableReport.Group(mt, c => CableReport.GroupPath(setup, c));
        Assert.Equal(["UC1", "UC2"], gruposMt.Select(g => g.Name));
        Assert.Equal(["T1", "T2"], gruposMt[0].Circuits.Select(c => c.FromName));
        Assert.Equal(600, gruposMt[0].CableLength, 9);
    }

    /// <summary>
    /// O erro grave do item 18: o inversor apagado do desenho e o resumo
    /// continuava com os cabos CC dele. O cabo cuja ponta saiu de campo não
    /// entra, e o aviso diz quem (todos sumidos: não dá para mostrar o resumo).
    /// </summary>
    [Fact]
    [Trait("Etapa", "24")]
    public void CaboDeInversorForaDeCampoNaoEntraNoResumo()
    {
        var (_, inv, _) = Usina();
        var circuitos = new[] { Cc(inv[0], "S1", 10), Cc(inv[0], "S2", 10), Cc(inv[1], "S3", 10) };
        string Nome(CableEnd p) => inv.First(i => i.Id == p.Id).Name;

        // Só o Inversor 1 saiu de campo.
        var (ficam, faltam) = CableReport.InField(circuitos, c => c.From, c => c.To, p => p.Kind == CableEndKind.String || p.Id != inv[0].Id);
        Assert.Equal(["S3"], ficam.Select(c => c.FromName));
        Assert.Equal([new CableEnd(CableEndKind.Inverter, inv[0].Id)], faltam);
        var parcial = CableReport.MissingMessage(faltam, Nome, ficam.Count == 0);
        Assert.Contains("Inversor 1", parcial);
        Assert.Contains("fora do resumo", parcial);

        // Todos os inversores saíram: nada fica e a aba diz que não dá para mostrar.
        var (nada, todos) = CableReport.InField(circuitos, c => c.From, c => c.To, p => p.Kind == CableEndKind.String);
        Assert.Empty(nada);
        var aviso = CableReport.MissingMessage(todos, Nome, nada.Count == 0);
        Assert.StartsWith("Inversor não está em campo: não é possível mostrar o resumo", aviso);
        Assert.Contains("Inversor 1, Inversor 2", aviso);

        Assert.Null(CableReport.MissingMessage([], Nome, false));
    }

    // ---- item 16: as contas por string

    private static readonly PanModule Modulo = new("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);

    [Fact]
    [Trait("Etapa", "23")]
    public void AsContasDaStringEOCriterioSimples()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc && c.SectionMm2 == 6);
        var metodo = cabo.Ampacity.Keys.First();
        var capacidade = cabo.Ampacity[metodo];

        var conta = StringCheck.For(Modulo, 28, -5, cabo, metodo)!;

        // Voc na mínima: 28 × (50 + (−0,125) × (−5 − 25)) = 28 × 53,75 = 1505 V.
        Assert.Equal(1505, conta.VocAtMin, 9);
        Assert.Equal(28 * 42, conta.Vmp, 9);
        Assert.Equal(18, conta.Isc, 9);
        Assert.Equal(17, conta.Imp, 9);
        Assert.Equal(capacidade, conta.Ampacity);
        Assert.Equal(22.5, conta.DesignCurrent, 9);
        Assert.Equal(22.5 <= capacidade, conta.Supports);

        // O critério: Isc × 1,25 ≤ capacidade.
        var justo = new StringCheck(1000, 800, 20, 19, 25);
        Assert.True(justo.Supports);
        Assert.False((justo with { Ampacity = 24.9 }).Supports);

        // Sem capacidade no método: vazio, nunca "Não". Sem PAN: sem conta.
        Assert.Null(StringCheck.For(Modulo, 28, -5, cabo, "XYZ")!.Supports);
        Assert.Null(StringCheck.For(null, 28, -5, cabo, metodo));
        Assert.Equal(StringCheck.Headers().Count, conta.Cells().Count);
        Assert.Equal(conta.Supports == true ? "Sim" : "Não", conta.Cells()[^1]);
    }
}
