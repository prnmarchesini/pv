using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>O contrato do roteamento (17.2, 17.3, 17.6): camadas, vala, lance, configuração de aba, lado forçado.</summary>
public class CableRoutingContractTests
{
    [Fact]
    [Trait("Etapa", "17")]
    public void CadaRotaTemCamadasECoresProprias()
    {
        var camadas = CableLayers.All().ToList();

        Assert.Equal(camadas.Count, camadas.Select(c => c.Name).Distinct().Count());
        Assert.Equal(4, CableRoutes.All.Select(CableLayers.TrenchColor).Distinct().Count());
        Assert.NotEqual(CableLayers.Cable(CableRoute.DirectCurrent, CablePolarity.Positive), CableLayers.Cable(CableRoute.DirectCurrent, CablePolarity.Negative));
        Assert.Equal(1, CableLayers.CableColor(CableRoute.DirectCurrent, CablePolarity.Positive));
        Assert.All(camadas, c => Assert.StartsWith("CLIVUS_", c.Name));
        Assert.All(camadas, c => Assert.Null(LayerName.WhyInvalid(c.Name)));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void LanceValaELadoVoltamIguais()
    {
        var lance = new CableRun(Guid.NewGuid(), CableRoute.DirectCurrent, CablePolarity.Negative,
            new CableEnd(CableEndKind.String, Guid.NewGuid()), new CableEnd(CableEndKind.Inverter, Guid.NewGuid()));
        Assert.Equal(lance, CableRun.Parse(lance.ToFields()));
        Assert.Null(CableRun.Parse([.. lance.ToFields().Take(6)]));
        Assert.Null(CableRun.Parse([Guid.Empty.ToString(), .. lance.ToFields().Skip(1)]));

        Assert.Equal(new TrenchMark(CableRoute.MediumVoltage), TrenchMark.Parse(new TrenchMark(CableRoute.MediumVoltage).ToFields()));
        Assert.Null(TrenchMark.Parse(["Xyz"]));

        var lado = new StringSide(Guid.NewGuid(), RowEnd.Start);
        Assert.Equal(lado, StringSide.Parse(lado.ToFields()));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ConfiguracaoDaAbaVoltaIgualComESemCabo()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc);
        var com = RouteSettings.Default(CableRoute.DirectCurrent) with { Depth = 0.7, Cable = cabo, Method = "C" };
        var sem = RouteSettings.Default(CableRoute.MediumVoltage);

        Assert.Equal(RouteSettings.FieldCount, com.ToFields().Count);
        Assert.Equal(com, RouteSettings.Parse(com.ToFields()));
        Assert.Equal(sem, RouteSettings.Parse(sem.ToFields()));
        Assert.Null(RouteSettings.Parse((sem with { Depth = 0 }).ToFields()));
    }

    /// <summary>
    /// As vias (10/10/2026) entram no fim do registro (formato 2); o formato 1
    /// é lido com as vias da formação do cabo (3x1x25 = 3) ou 1.
    /// </summary>
    [Fact]
    [Trait("Etapa", "24")]
    public void ViasGravamEOFormatoAntigoLeDaFormacao()
    {
        var mt = CableLibrary.Default().First(c => c.Type == CableType.Mv) with { Formation = "3x1x25" };
        var com = RouteSettings.Default(CableRoute.MediumVoltage) with { Cable = mt, Wires = 6 };

        Assert.Equal(6, RouteSettings.Parse(com.ToFields())!.Wires);
        Assert.Null(RouteSettings.Parse((com with { Wires = 0 }).ToFields()));

        var v1 = com.ToFields().Take(RouteSettings.FieldCountV1).ToList();
        Assert.Equal(3, RouteSettings.ParseV1(v1)!.Wires);
        Assert.Equal(1, RouteSettings.ParseV1(RouteSettings.Default(CableRoute.DirectCurrent).ToFields().Take(RouteSettings.FieldCountV1).ToList())!.Wires);
        Assert.Null(RouteSettings.Parse(v1));

        Assert.Equal(3, CableLibrary.WiresFromFormation("3x1x95"));
        Assert.Equal(3, CableLibrary.WiresFromFormation(" 3 × 1 × 95"));
        Assert.Equal(1, CableLibrary.WiresFromFormation("1x6"));
        Assert.Equal(1, CableLibrary.WiresFromFormation("1x(3x95)"));
        Assert.Equal(1, CableLibrary.WiresFromFormation("4x16"));
        Assert.Null(CableLibrary.WiresFromFormation("tripolar"));

        var troca = new CircuitWires(CableRoute.MediumVoltage, new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), new CableEnd(CableEndKind.Substation, Guid.NewGuid()), 4);
        Assert.Equal(troca, CircuitWires.Parse(troca.ToFields()));
        Assert.Null(CircuitWires.Parse((troca with { Wires = 0 }).ToFields()));
    }

    /// <summary>Cada aba com o seu valor (17.3): as profundidades de partida são independentes.</summary>
    [Fact]
    [Trait("Etapa", "17")]
    public void ProfundidadeDePartidaPorAba()
    {
        Assert.Equal(0.6, RouteSettings.Default(CableRoute.DirectCurrent).Depth);
        Assert.Equal(0.8, RouteSettings.Default(CableRoute.AlternatingCurrent).Depth);
        Assert.Equal(1.0, RouteSettings.Default(CableRoute.MediumVoltage).Depth);
    }
}

/// <summary>A biblioteca de cabos (22.1) e o arquivo PAN (22.3).</summary>
public class CableLibraryAndPanTests
{
    [Fact]
    [Trait("Etapa", "22")]
    public void ABibliotecaDePartidaEValidaComOsTresTipos()
    {
        var cabos = CableLibrary.Default();

        Assert.All(cabos, c => Assert.True(c.IsValid, c.Name));
        Assert.Equal(cabos.Count, cabos.Select(c => c.Id).Distinct().Count());
        Assert.Equal(cabos.Count, cabos.Select(c => c.Name).Distinct().Count());
        foreach (var tipo in Enum.GetValues<CableType>()) Assert.Contains(cabos, c => c.Type == tipo);
        Assert.All(cabos, c => Assert.True(c.ResistanceOperating > c.Resistance20, c.Name));
        Assert.Equal(cabos.Select(c => c.Id), CableLibrary.Default().Select(c => c.Id));
    }

    [Fact]
    [Trait("Etapa", "22")]
    public void ABibliotecaGravaELeIgual()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clivus-cabos-" + Guid.NewGuid().ToString("N"));
        var arquivo = Path.Combine(pasta, CableLibrary.FileName);

        try
        {
            Assert.Equal(CableLibrary.Default(), CableLibrary.Load(arquivo, out var semArquivo));
            Assert.Null(semArquivo);

            var editado = CableLibrary.Default().Take(3).Select((c, i) => i == 0 ? c with { Name = "Editado", Resistance20 = 9 } : c).ToList();
            CableLibrary.Save(arquivo, editado);

            Assert.Equal(editado, CableLibrary.Load(arquivo, out var problema));
            Assert.Null(problema);

            File.WriteAllText(arquivo, "{ estragado");
            Assert.Equal(CableLibrary.Default(), CableLibrary.Load(arquivo, out var estragado));
            Assert.NotNull(estragado);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    [Trait("Etapa", "22")]
    public void ACapacidadeLeEscreveEProcuraPeloMetodo()
    {
        var mapa = Cable.ParseAmpacity("b1=66; D=58,5", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))!;
        Assert.Equal(66, mapa["B1"]);
        Assert.Equal(58.5, mapa["D"]);
        Assert.Null(Cable.ParseAmpacity("B1"));
        Assert.Null(Cable.ParseAmpacity("B1=-3"));

        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Ac);
        Assert.NotNull(cabo.AmpacityFor("b1"));
        Assert.Null(cabo.AmpacityFor("A1"));
        Assert.Equal(cabo, Cable.Parse(cabo.ToFields()));
    }

    // O começo de um .PAN do PVsyst (as linhas que importam e algumas que não).
    private const string Pan = """
        PVObject_=pvModule
          Version=7.2.8
          Flags=$00508043
          PVObject_Commercial=pvCommercial
            Comment=www.exemplo.com
            Flags=$0041
            Manufacturer=Risen Energy
            Model=RSM132-8-720BHDG
            Width=1.303
          End of PVObject pvCommercial
          Technol=mtSiMono
          GRef=1000
          TRef=25.0
          PNom=720.0
          Isc=18.420
          Voc=50.20
          Imp=17.280
          Vmp=41.70
          muISC=7.37
          muVocSpec=-125.5
          muPmpReq=-0.340
        End of PVObject pvModule
        """;

    [Fact]
    [Trait("Etapa", "22")]
    public void OPanELido()
    {
        var m = PanModule.Parse(Pan, out var faltam);

        Assert.Empty(faltam);
        Assert.NotNull(m);
        Assert.Equal("RSM132-8-720BHDG", m!.Model);
        Assert.Equal("Risen Energy", m.Manufacturer);
        Assert.Equal(50.20, m.Voc);
        Assert.Equal(17.280, m.Imp);
        Assert.Equal(-0.1255, m.VocCoefficient, 9);
        Assert.Equal(0.00737, m.IscCoefficient, 9);
        Assert.Null(m.Noct);
        Assert.Equal(m, PanModule.Parse(m.ToFields()));
    }

    /// <summary>PAN incompleto: diz qual campo faltou, nunca inventa valor.</summary>
    [Fact]
    [Trait("Etapa", "22")]
    public void PanIncompletoDizOQueFaltou()
    {
        var semVoc = Pan.Replace("  Voc=50.20\n", string.Empty).Replace("  Voc=50.20\r\n", string.Empty);
        var semMu = semVoc.Replace("muVocSpec=-125.5", "muVocSpec=abc");

        Assert.Null(PanModule.Parse(semVoc, out var faltaVoc));
        Assert.Equal(["Voc"], faltaVoc);
        Assert.Null(PanModule.Parse(semMu, out var faltam));
        Assert.Contains("muVocSpec", faltam);
        Assert.Null(PanModule.Parse("lixo", out var tudo));
        Assert.Contains("PNom", tudo);
    }
}

/// <summary>As contas do memorial (23.1 a 23.5), conferidas à mão.</summary>
public class CableCalcTests
{
    private static readonly PanModule Modulo = new("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);

    [Fact]
    [Trait("Etapa", "23")]
    public void VocNoFrioSobeETensaoNoCalorDesce()
    {
        // 28 módulos, mínima 5 °C: 28 × (50 − 0,125 × (5 − 25)) = 28 × 52,5 = 1470 V.
        Assert.Equal(1470, CableCalc.OpenCircuitAtMin(Modulo, 28, 5), 9);
        Assert.True(CableCalc.OpenCircuitAtMin(Modulo, 28, 5) > 28 * Modulo.Voc);

        // Máxima 65 °C: 28 × 42 × (1 − 0,125/50 × 40) = 28 × 42 × 0,9 = 1058,4 V.
        Assert.Equal(1058.4, CableCalc.OperatingAtMax(Modulo, 28, 65), 9);
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void QuedaNoCcEIdaEVolta()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc) with { ResistanceOperating = 4 };

        // 17 A × 4 Ω/km × (100 + 80) m / 1000 = 12,24 V.
        var queda = CableCalc.DcDrop(17, cabo, 100, 80);
        Assert.Equal(12.24, queda, 9);
        Assert.Equal(12.24 / 1058.4 * 100, CableCalc.Percent(queda, 1058.4), 9);
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void CorrenteEQuedaTrifasicas()
    {
        // 300 kW em 800 V, FP 1: 300000 / (√3 × 800) = 216,5 A.
        var i = CableCalc.ThreePhaseCurrent(300, 800, 1);
        Assert.Equal(216.506, i, 3);

        // FP 1: só a resistência. √3 × 216,5 × 0,2 km × 0,2 Ω/km = 15 V.
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Ac) with { ResistanceOperating = 0.2, Reactance = 0.1 };
        Assert.Equal(Math.Sqrt(3) * i * 0.2 * 0.2, CableCalc.ThreePhaseDrop(i, cabo, 200, 1), 9);

        // FP 0,8: R × 0,8 + X × 0,6.
        Assert.Equal(Math.Sqrt(3) * 100 * 0.2 * (0.2 * 0.8 + 0.1 * 0.6), CableCalc.ThreePhaseDrop(100, cabo, 200, 0.8), 9);

        // 2500 kVA em 13,8 kV: 104,6 A.
        Assert.Equal(104.59, CableCalc.ThreePhaseCurrentKva(2500, 13800), 2);
    }
}

/// <summary>O roteador (18.2 a 18.7, 20.1 a 20.3, 21.1 a 21.3): menor percurso, lado forçado, nunca calado.</summary>
public class CableRouterTests
{
    // Mesa de 20 × 4 m ao longo de x, de x = 30 a 50 (fileira só dela). Vala
    // norte-sul em x = 0 (oeste) e em x = 100 (leste), ligadas por uma vala
    // leste-oeste em y = -50, e o inversor em (60, -50).
    private static readonly Point3[] Mesa = [new(30, 0, 0), new(50, 0, 0), new(50, 4, 0), new(30, 4, 0)];
    private static readonly Point3[] Oeste = [new(0, 20, 0), new(0, -50, 0)];
    private static readonly Point3[] Leste = [new(100, 20, 0), new(100, -50, 0)];
    private static readonly Point3[] Sul = [new(0, -50, 0), new(100, -50, 0)];
    private static readonly Point3 Inversor = new(60, -52, 100.8);

    private static double? Chao(double x, double y) => 100;

    private static readonly RouteSettings Config = RouteSettings.Default(CableRoute.DirectCurrent);

    private static StringRouteInput Str(double xPos, double xNeg, RowEnd? forcado = null, Guid? inversor = null, Point3? ondeInv = null) => new(
        Guid.NewGuid(), "S1",
        new StringEndInput(new Point3(xPos, 1, 102), Guid.Empty, Mesa, Mesa),
        new StringEndInput(new Point3(xNeg, 1, 102), Guid.Empty, Mesa, Mesa),
        new CableEnd(CableEndKind.Inverter, inversor ?? Guid.NewGuid()), "INV1", ondeInv ?? Inversor, forcado);

    [Fact]
    [Trait("Etapa", "18")]
    public void DoisLancesPorStringPeloLadoMaisCurto()
    {
        // Pontas em x = 32 e 33, perto do começo da fileira: pelo oeste ≈ 285 m, pelo leste ≈ 315 m.
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var r = CableRouter.Strings([Str(32, 33)], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Failures);
        Assert.Equal(2, r.Runs.Count);
        Assert.Contains(r.Runs, l => l.Run.Polarity == CablePolarity.Positive);
        Assert.Contains(r.Runs, l => l.Run.Polarity == CablePolarity.Negative);

        var oeste = CableRouter.Strings([Str(32, 33, RowEnd.Start)], CableRoute.DirectCurrent, rede, Config, Chao).Runs.Sum(l => l.Length);
        var leste = CableRouter.Strings([Str(32, 33, RowEnd.End)], CableRoute.DirectCurrent, rede, Config, Chao).Runs.Sum(l => l.Length);
        Assert.Equal(Math.Min(oeste, leste), r.Runs.Sum(l => l.Length), 6);
        Assert.True(oeste + 10 < leste, $"oeste {oeste:0.#}, leste {leste:0.#}");
    }

    /// <summary>A mesa de 100 m com vala dos dois lados e a string no metro 40: vai pelos 40 (18.2).</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void MesaDeCemMetrosStringNoMetroQuarenta()
    {
        Point3[] mesa = [new(0, 0, 0), new(100, 0, 0), new(100, 4, 0), new(0, 4, 0)];
        Point3[] oeste = [new(-5, 20, 0), new(-5, -60, 0)];
        Point3[] leste = [new(105, 20, 0), new(105, -60, 0)];
        Point3[] sul = [new(-5, -60, 0), new(105, -60, 0)];
        var rede = new TrenchNetwork([oeste, leste, sul]);

        // O inversor no meio, para a volta pela vala sul ser igual dos dois lados.
        var s = new StringRouteInput(Guid.NewGuid(), "S", new StringEndInput(new(40, 1, 102), Guid.Empty, mesa, mesa),
            new StringEndInput(new(40.5, 1, 102), Guid.Empty, mesa, mesa), new CableEnd(CableEndKind.Inverter, Guid.NewGuid()), "INV", new Point3(50, -62, 100.8), null);

        var r = CableRouter.Strings([s], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Failures);
        Assert.All(r.Runs, l => Assert.Contains(l.Path, p => p.X < 0));
        Assert.All(r.Runs, l => Assert.DoesNotContain(l.Path, p => p.X > 100));
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void LadoForcadoERespeitado()
    {
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);

        var r = CableRouter.Strings([Str(35, 36, RowEnd.End)], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Failures);
        Assert.All(r.Runs, l => Assert.Contains(l.Path, p => p.X > 99));
    }

    /// <summary>Nenhuma vala cruza a reta do fim da fileira: avisa, pinta a mesa e não chuta (18.5).</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ValaQueNaoPassaDaMesaAvisaEPintaAMesa()
    {
        // Só a vala sul: as retas do fim da fileira (y = 4,5 ou −0,5) não cruzam.
        var rede = new TrenchNetwork([Sul]);
        var mesa = Guid.NewGuid();
        var s = Str(35, 45) with
        {
            Positive = new StringEndInput(new Point3(35, 1, 102), mesa, Mesa, Mesa),
            Negative = new StringEndInput(new Point3(45, 1, 102), mesa, Mesa, Mesa),
        };

        var r = CableRouter.Strings([s], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Empty(r.Runs);
        var falha = Assert.Single(r.Failures);
        Assert.Contains("referência da vala", falha.Reason);
        Assert.Equal([mesa], falha.PaintTables);
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void StringSemInversorOuInversorForaDeCampoAvisa()
    {
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);

        var semInversor = CableRouter.Strings([Str(35, 45, inversor: Guid.Empty)], CableRoute.DirectCurrent, rede, Config, Chao);
        var foraDeCampo = CableRouter.Strings([Str(35, 45) with { DestinationPoint = null }], CableRoute.DirectCurrent, rede, Config, Chao);

        Assert.Contains("alocada", Assert.Single(semInversor.Failures).Reason);
        Assert.Contains("em campo", Assert.Single(foraDeCampo.Failures).Reason);
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void CaboNuncaPassaPeloMeioDaMesa()
    {
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var r = CableRouter.Strings([Str(35, 45), Str(40, 41)], CableRoute.DirectCurrent, rede, Config, Chao);

        // Tirando o primeiro ponto (o módulo, dentro da mesa), nenhum ponto cai dentro da mesa.
        Assert.All(r.Runs, l => Assert.All(l.Path.Skip(1), p => Assert.False(Polygons.Contains(Mesa, p.X, p.Y), $"{p} dentro da mesa")));
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void OCaboDesceAProfundidadeDaAba()
    {
        var rede = new TrenchNetwork([Oeste, Leste, Sul]);
        var r = CableRouter.Strings([Str(35, 45)], CableRoute.DirectCurrent, rede, Config with { Depth = 0.9 }, Chao);

        Assert.All(r.Runs, l => Assert.Contains(l.Path, p => Math.Abs(p.Z - 99.1) < 1e-9));
    }

    // ---- CA e MT

    private static readonly Point3[] ValaCa = [new(0, 0, 0), new(100, 0, 0)];

    [Fact]
    [Trait("Etapa", "20")]
    public void CaVaiPelaValaMaisPertoDentroDoRaio()
    {
        var rede = new TrenchNetwork([ValaCa]);
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Inverter, Guid.NewGuid()), "INV1", new Point3(10, 3, 100.8),
            new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", new Point3(90, -4, 100.8));

        var r = CableRouter.Equipment([t], CableRoute.AlternatingCurrent, rede, RouteSettings.Default(CableRoute.AlternatingCurrent), Chao);

        var lance = Assert.Single(r.Runs);
        Assert.Empty(r.Failures);

        // Em planta: 3 até a vala, 80 por ela, 4 até o trafo; mais as descidas e subidas.
        var planta = 3 + 80 + 4;
        Assert.InRange(lance.Length, planta, planta + 4);
        Assert.Equal(CableEndKind.Transformer, lance.Run.To.Kind);
    }

    /// <summary>Vala fora do raio: avisa e pinta o equipamento sem rota (20.3, 21.3).</summary>
    [Fact]
    [Trait("Etapa", "20")]
    public void ValaForaDoRaioPintaOEquipamento()
    {
        var rede = new TrenchNetwork([ValaCa]);
        var trafo = new CableEnd(CableEndKind.Transformer, Guid.NewGuid());
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Inverter, Guid.NewGuid()), "INV1", new Point3(10, 3, 100.8), trafo, "T1", new Point3(90, -40, 100.8));

        var r = CableRouter.Equipment([t], CableRoute.AlternatingCurrent, rede, RouteSettings.Default(CableRoute.AlternatingCurrent), Chao);

        Assert.Empty(r.Runs);
        var falha = Assert.Single(r.Failures);
        Assert.Equal([trafo], falha.Paint);
        Assert.Contains("T1", falha.Reason);
    }

    /// <summary>
    /// O rabicho (Renan, 10/10/2026): a vala principal desce ao lado do trafo
    /// e ele desenhou um ramal que sai dela e entra no retângulo do trafo. Pelo
    /// raio, o cabo cortava na diagonal até um vértice da principal mais ao
    /// sul (mais curto); com o ramal entrando no trafo, o cabo vai por ele (o
    /// raio só vale quando nenhuma vala entra no equipamento).
    /// </summary>
    [Fact]
    [Trait("Etapa", "21")]
    public void MtVaiPeloRabichoQueEntraNoTrafo()
    {
        var principal = new Point3[] { new(0, 50, 0), new(0, 5, 0), new(0, -100, 0) };
        var rabicho = new Point3[] { new(0, 10, 0), new(7, 10, 0) };
        var rede = new TrenchNetwork([principal, rabicho]);
        Point3[] caixa = [new(6, 8, 0), new(10, 8, 0), new(10, 12, 0), new(6, 12, 0)];

        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", new Point3(8, 10, 100.8),
            new CableEnd(CableEndKind.Substation, Guid.NewGuid()), "UC1", new Point3(5, -90, 100.8), FromOutline: caixa);
        static bool Em(Point3 p, double x, double y) => Math.Abs(p.X - x) < 1e-6 && Math.Abs(p.Y - y) < 1e-6;

        var lance = Assert.Single(CableRouter.Equipment([t], CableRoute.MediumVoltage, rede, RouteSettings.Default(CableRoute.MediumVoltage), Chao).Runs);

        Assert.Contains(lance.Path, p => Em(p, 7, 10));
        Assert.Contains(lance.Path, p => Em(p, 0, 10));

        // Rabicho solto (a ponta a 0,8 m da principal, mais que o encosto de 0,5): ele não se
        // liga a nada, e o cabo cai na busca pelo raio em vez de falhar (revisão de 10/10/2026).
        var solto = new TrenchNetwork([principal, [new(0.8, 10, 0), new(7, 10, 0)]]);
        var pelaPrincipal = CableRouter.Equipment([t], CableRoute.MediumVoltage, solto, RouteSettings.Default(CableRoute.MediumVoltage), Chao);
        Assert.Empty(pelaPrincipal.Failures);
        Assert.DoesNotContain(Assert.Single(pelaPrincipal.Runs).Path, p => Em(p, 7, 10));

        // Sem o contorno (como era), o raio acha o vértice (0; 5) a 9,4 m e o cabo corta na diagonal.
        var semContorno = Assert.Single(CableRouter.Equipment([t with { FromOutline = null }], CableRoute.MediumVoltage, rede, RouteSettings.Default(CableRoute.MediumVoltage), Chao).Runs);
        Assert.DoesNotContain(semContorno.Path, p => Em(p, 7, 10));
    }

    [Fact]
    [Trait("Etapa", "21")]
    public void SemVinculoNaCadeiaAvisa()
    {
        var rede = new TrenchNetwork([ValaCa]);
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", new Point3(10, 3, 100.8),
            new CableEnd(CableEndKind.Substation, Guid.Empty), "-", null);

        var r = CableRouter.Equipment([t], CableRoute.MediumVoltage, rede, RouteSettings.Default(CableRoute.MediumVoltage), Chao);

        Assert.Contains("vínculo", Assert.Single(r.Failures).Reason);
    }
}

/// <summary>As tabelas do memorial (23.4, 23.6, 23.7, 23.8) e o resumo e a lista de material (24.1, 24.2).</summary>
public class CableReportTests
{
    private static readonly PanModule Modulo = new("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);
    private static readonly Cable Cabo6 = CableLibrary.Default().First(c => c.Type == CableType.Dc && c.SectionMm2 == 6);
    private static readonly Cable Cabo10 = CableLibrary.Default().First(c => c.Type == CableType.Dc && c.SectionMm2 == 10);

    [Fact]
    [Trait("Etapa", "23")]
    public void TabelaDoCcTemUmaLinhaPorStringETotais()
    {
        var t = CableReport.Dc("CC", [new(Guid.NewGuid(), "S2", 28, 100, 80), new(Guid.NewGuid(), "S1", 28, 50, 40)], Cabo6, Modulo, 5, 65);

        Assert.Equal(2, t.Rows.Count);
        Assert.Equal("S1", t.Rows[0][0]);
        Assert.Equal(90.0, t.Rows[0][4]);
        Assert.Equal(1470, (double)t.Rows[0][6]!, 9);
        Assert.Equal(1058.4, (double)t.Rows[0][7]!, 9);
        Assert.Equal(17 * Cabo6.ResistanceOperating * 90 / 1000, (double)t.Rows[0][10]!, 9);
        Assert.Equal(270.0, t.Total[4]);
        Assert.Empty(t.Notes);
    }

    /// <summary>Trocar a bitola (23.7): todas as colunas calculadas mudam e os comprimentos não.</summary>
    [Fact]
    [Trait("Etapa", "23")]
    public void TrocarABitolaMudaAQuedaENaoOComprimento()
    {
        DcStringRun[] s = [new(Guid.NewGuid(), "S1", 28, 100, 80)];
        var com6 = CableReport.Dc("CC", s, Cabo6, Modulo, 5, 65);
        var com10 = CableReport.Dc("CC", s, Cabo10, Modulo, 5, 65);

        Assert.Equal(com6.Rows[0][4], com10.Rows[0][4]);
        Assert.True((double)com10.Rows[0][10]! < (double)com6.Rows[0][10]!);
        Assert.NotEqual(com6.Rows[0][5], com10.Rows[0][5]);
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void SemPanOuSemCaboFicaEmBrancoEDiz()
    {
        var t = CableReport.Dc("CC", [new(Guid.NewGuid(), "S1", 28, 100, null)], null, null, 5, 65);

        Assert.Null(t.Rows[0][6]);
        Assert.Null(t.Rows[0][10]);
        Assert.Equal(3, t.Notes.Count);
    }

    /// <summary>CA/MT (23.5, 23.6): a corrente calculada e a admissível lado a lado, sem veredito.</summary>
    [Fact]
    [Trait("Etapa", "23")]
    public void TabelaDeCaMostraAsDuasCorrentes()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Ac);
        var t = CableReport.Equipment("CA", [new("INV1", "T1", 120, 216.5, 800, null), new("INV2", "T1", 80, null, null, "potência do inversor não informada")], cabo, "B1", 1);

        Assert.Equal(216.5, t.Rows[0][4]);
        Assert.Equal(cabo.AmpacityFor("B1"), t.Rows[0][5]);
        Assert.NotNull(t.Rows[0][6]);
        Assert.Null(t.Rows[1][6]);
        Assert.Equal(200.0, t.Total[2]);
        Assert.Contains(t.Notes, n => n.Contains("INV2"));
        Assert.DoesNotContain(t.Header, h => h.Contains("prova", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void CsvSaiComSeparadorEVirgulaDaCultura()
    {
        var t = CableReport.Equipment("CA", [new("INV;1", "T1", 120.5, null, null, null)], null, "D", 1);
        var csv = t.ToCsv(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        var linhas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, linhas.Length);
        Assert.StartsWith("\"INV;1\";T1;120,5;", linhas[1]);
    }

    [Fact]
    [Trait("Etapa", "24")]
    public void ResumoEMaterialSomamPorCaboComAFolgaDoUsuario()
    {
        CableReport.MeasuredRun[] l =
        [
            new(CableRoute.DirectCurrent, CablePolarity.Positive, "6", 100),
            new(CableRoute.DirectCurrent, CablePolarity.Negative, "6", 80),
            new(CableRoute.AlternatingCurrent, CablePolarity.None, "95", 50),
        ];

        var resumo = CableReport.Summary(l);
        Assert.Equal(3, resumo.Rows.Count);
        Assert.Equal(230.0, resumo.Total[4]);

        var material = CableReport.Material(l, 5);
        Assert.Equal(2, material.Rows.Count);
        Assert.Equal(180 * 1.05, (double)material.Rows.Single(r => (string)r[0]! == "6")[2]!, 9);
        Assert.Equal(230.0, (double)CableReport.Material(l, 0).Total[2]!, 9);

        // Com vias, os metros de cabo são o traçado vezes as vias.
        CableReport.MeasuredRun[] mt = [new(CableRoute.MediumVoltage, CablePolarity.None, "25", 200, 3)];
        Assert.Equal(600.0, (double)CableReport.Material(mt, 0).Total[1]!, 9);
    }

    /// <summary>
    /// O resumo por tipo (Renan, 10/10/2026): cada circuito uma linha, com De
    /// → Para, a especificação do cabo, o método, os lances, as vias, os cabos
    /// e os metros de cabo; o total soma os cabos e os metros com as vias.
    /// </summary>
    [Fact]
    [Trait("Etapa", "24")]
    public void ResumoPorCircuitoComViasEDePara()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Mv);
        CableEnd T() => new(CableEndKind.Transformer, Guid.NewGuid());
        CableEnd U() => new(CableEndKind.Substation, Guid.NewGuid());

        var tabela = CableReport.Circuits("MT",
        [
            new(CableRoute.MediumVoltage, T(), U(), "T2", "UC2", 1, 150, 3, cabo, "D"),
            new(CableRoute.MediumVoltage, T(), U(), "T1", "UC1", 1, 255.22, 1, cabo, "D"),
        ]);

        Assert.Equal(2, tabela.Rows.Count);
        Assert.Equal("T1 → UC1", tabela.Rows[0][0]);
        Assert.Equal(cabo.Name, tabela.Rows[0][1]);
        Assert.Equal(cabo.SectionMm2, tabela.Rows[0][3]);
        Assert.Equal(cabo.Conductor, tabela.Rows[0][4]);
        Assert.Equal("D", tabela.Rows[0][6]);
        Assert.Equal(3.0, tabela.Rows[1][9]);
        Assert.Equal(3.0, tabela.Rows[1][10]);
        Assert.Equal(450.0, (double)tabela.Rows[1][11]!, 9);

        Assert.Equal(405.22, (double)tabela.Total[8]!, 9);
        Assert.Equal(4.0, tabela.Total[10]);
        Assert.Equal(255.22 + 450, (double)tabela.Total[11]!, 9);
    }

    /// <summary>No CC o circuito tem dois lances (+ e −): os cabos são 2 vezes as vias.</summary>
    [Fact]
    [Trait("Etapa", "24")]
    public void CircuitoCcTemDoisLances()
    {
        var c = new CableReport.CircuitRun(CableRoute.DirectCurrent, new(CableEndKind.String, Guid.NewGuid()), new(CableEndKind.Inverter, Guid.NewGuid()),
            "S1", "Inversor 1", 2, 180, 1, null, "D");

        Assert.Equal(2, c.Cables);
        Assert.Equal(180, c.CableLength, 9);
        Assert.Contains(CableReport.Circuits("CC", [c]).Notes, n => n.Contains("sem cabo"));
    }

    [Fact]
    [Trait("Etapa", "24")]
    public void NomesEmOrdemNatural()
    {
        string[] nomes = ["S10", "S2", "Inversor 10", "Inversor 2", "S1"];
        Assert.Equal(["Inversor 2", "Inversor 10", "S1", "S2", "S10"], nomes.Order(NaturalStringComparer.Instance));
    }
}

/// <summary>A cadeia das rotas (20, 21.2) e a fileira de uma mesa (18.1).</summary>
public class CableChainTests
{
    private static readonly EquipmentSize Caixa = new(2, 1, 2.2);

    [Fact]
    [Trait("Etapa", "21")]
    public void MtVaiParaASubestacaoVinculadaNaoAMaisPerto()
    {
        var bloco = new Substation(Guid.NewGuid(), "Cubículo", Caixa);
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "", ConsumerUnitMode.Shared, Caixa, bloco.Id);
        var u2 = new ConsumerUnit(Guid.NewGuid(), "U2", "Unitária 2", ConsumerUnitMode.Unitary, Caixa);
        var t1 = new Transformer(Guid.NewGuid(), "Seco", "T1", 800, 13800, 2500, 4, 6.5, "", Caixa, c1.Id);
        var t2 = new Transformer(Guid.NewGuid(), "Seco", "T2", 800, 13800, 2500, 4, 6.5, "", Caixa, u2.Id);
        var t3 = new Transformer(Guid.NewGuid(), "Seco", "T3", 800, 13800, 2500, 4, 6.5, "", Caixa, Guid.Empty);
        var setup = new ElectricalSetup(transformers: [t1, t2, t3], units: [c1, u2], substations: [bloco]);

        var mt = CableChain.MediumVoltage(setup);

        Assert.Equal(bloco.Id, mt.Single(l => l.FromName == "T1").To.Id);
        Assert.Equal(u2.Id, mt.Single(l => l.FromName == "T2").To.Id);
        Assert.Equal(Guid.Empty, mt.Single(l => l.FromName == "T3").To.Id);
    }

    [Fact]
    [Trait("Etapa", "20")]
    public void CaLigaCadaInversorAoTrafoDele()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, Caixa);
        var t = new Transformer(Guid.NewGuid(), "Seco", "T1", 800, 13800, 2500, 4, 6.5, "", Caixa, Guid.Empty);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "INV1", t.Id);
        var i2 = new Inverter(Guid.NewGuid(), modelo.Id, "INV2", Guid.Empty);

        var ca = CableChain.AlternatingCurrent(new ElectricalSetup(transformers: [t], inverters: [i1, i2], models: [modelo]));

        Assert.Equal(t.Id, ca.Single(l => l.FromName == "INV1").To.Id);
        Assert.Equal(Guid.Empty, ca.Single(l => l.FromName == "INV2").To.Id);
    }

    private static RowTable Mesa(string rotulo, double x0, double y0 = 0) =>
        new(Guid.NewGuid(), rotulo, [new(x0, y0, 0), new(x0 + 20, y0, 0), new(x0 + 20, y0 + 4, 0), new(x0, y0 + 4, 0)]);

    [Fact]
    [Trait("Etapa", "18")]
    public void AFileiraSaoAsMesasDoMesmoNumeroAlinhadas()
    {
        var a = Mesa("F1.1", 0);
        var b = Mesa("F1.2", 22);
        var outraFileira = Mesa("F2.1", 44);
        var mesmoNumeroLonge = Mesa("F1.3", 44, 30);

        var cantos = CableRows.Corners(a, [a, b, outraFileira, mesmoNumeroLonge]);

        Assert.Equal(8, cantos.Count);
        Assert.Equal(42, cantos.Max(c => c.X));
    }
}

/// <summary>A combiner (19.1 a 19.3): cadastro, alocação com as regras do inversor e o alimentador.</summary>
public class CombinerTests
{
    private static readonly EquipmentSize Caixa = new(0.8, 0.3, 0.8);

    [Fact]
    [Trait("Etapa", "19")]
    public void ACombinerVaiEVoltaEEntraNoCadastroDeCampo()
    {
        var cb = new Combiner(Guid.NewGuid(), "CB1", 16, Caixa, Guid.NewGuid());
        Assert.Equal(cb, Combiner.Parse(cb.ToFields()));
        Assert.Null(Combiner.Parse((cb with { Inputs = 0 }).ToFields()));

        var setup = new ElectricalSetup(combiners: [cb]);
        Assert.Equal(EquipmentKind.Combiner, Assert.Single(setup.Equipment()).Kind);
        Assert.Single(setup.FindEquipment("cb1"));
    }

    [Fact]
    [Trait("Etapa", "19")]
    public void CadastroDaCombinerConfereNomeEInversor()
    {
        var setup = new ElectricalSetup();
        var a = setup.AddCombiner();
        var b = setup.AddCombiner();

        Assert.Equal("CB1", a.Name);
        Assert.Equal("CB2", b.Name);
        Assert.NotNull(setup.EditCombiner(b with { Name = "cb1" }));
        Assert.NotNull(setup.EditCombiner(b with { Inverter = Guid.NewGuid() }));
        Assert.Null(setup.EditCombiner(b with { Name = "Norte", Inputs = 24 }));
        Assert.Equal(24, setup.FindCombiner(b.Id)!.Inputs);
    }

    [Fact]
    [Trait("Etapa", "19")]
    public void TirarOInversorSoltaACombinerDele()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, new EquipmentSize(1, 1, 1));
        var inv = new Inverter(Guid.NewGuid(), modelo.Id, "INV1", Guid.Empty);
        var setup = new ElectricalSetup(inverters: [inv], models: [modelo], combiners: [new Combiner(Guid.NewGuid(), "CB1", 8, Caixa, inv.Id)]);

        setup.RemoveInverter(inv.Id);

        Assert.Equal(Guid.Empty, setup.Combiners[0].Inverter);
    }

    /// <summary>As mesmas regras da alocação no inversor (19.2): trava da string de outra combiner e aviso de excesso.</summary>
    [Fact]
    [Trait("Etapa", "19")]
    public void AlocacaoTravaStringDeOutraCombinerEContaOExcesso()
    {
        var cb1 = new Combiner(Guid.NewGuid(), "CB1", 2, Caixa, Guid.Empty);
        var cb2 = new Combiner(Guid.NewGuid(), "CB2", 2, Caixa, Guid.Empty);
        var s = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();

        var r1 = CombinerAllocation.Allocate(cb1, [s[0], s[1], s[2]], []);
        Assert.Equal(3, r1.Added);
        Assert.Equal(1, r1.Excess);

        var r2 = CombinerAllocation.Allocate(cb2, [s[0], s[3]], r1.Allocation);
        Assert.Equal(1, r2.Added);
        Assert.Equal(1, r2.Refused);
        Assert.Equal(cb1.Id, r2.Allocation.Single(x => x.String == s[0]).Combiner);

        Assert.DoesNotContain(CombinerAllocation.Release(cb1.Id, r2.Allocation), x => x.Combiner == cb1.Id);
    }

    [Fact]
    [Trait("Etapa", "19")]
    public void AlimentadorSomaAsStringsEQuedaIdaEVolta()
    {
        var m = new PanModule("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc) with { ResistanceOperating = 1 };

        var t = CableReport.Feeders("F", [new DcFeederRun("CB1", "INV1", 50, 4, 28, m)], cabo, 65);

        Assert.Equal(68.0, t.Rows[0][5]);
        Assert.Equal(68 * 1 * 100 / 1000.0, (double)t.Rows[0][7]!, 9);
    }
}

/// <summary>O menu Exportar (10/10/2026): o usuário escolhe as abas; o padrão continua o Excel de antes.</summary>
public class ExportMenuTests
{
    private static IReadOnlyList<string> Abas(XlsxWriter x)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(x.ToBytes()));
        var livro = System.Xml.Linq.XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
        return livro.Descendants().Where(e => e.Name.LocalName == "sheet").Select(e => (string)e.Attribute("name")!).ToList();
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void SoAsAbasEscolhidasEAsTabelasNoFim()
    {
        var cabos = CableReport.Summary([new(CableRoute.DirectCurrent, CablePolarity.Positive, "6", 10)]);

        Assert.Equal(["Resumo", "Análises", "Pilares", "Compra de pilares"], Abas(QuantityReport.Build(1, 2, 3, [], [])));
        Assert.Equal(["Pilares", "Resumo de cabos"], Abas(QuantityReport.Build(1, 2, 3, [], [], ExportSheets.Pillars, [cabos])));
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void TituloComCaractereProibidoViraNomeDeAbaValido()
    {
        var t = new CableTable("Cabos: CC/CA [teste] com um nome comprido demais", ["A"], [["x"]], [], ["nota"]);
        var planilha = new XlsxWriter();

        QuantityReport.Tabela(planilha, t);

        var nome = Assert.Single(Abas(planilha));
        Assert.True(nome.Length <= 31);
        Assert.DoesNotContain(nome, c => ":\\/?*[]".Contains(c));
    }
}

/// <summary>A revisão de 10/10/2026: vala mais perto solta, PAN com CRLF, comprimento exato, monofásico e o coeficiente da Vmp.</summary>
public class CableRoutingReviewCoreTests
{
    private static double? Chao(double x, double y) => 100;

    /// <summary>A vala mais perto do equipamento está solta; outra dentro do raio se liga: vale a que liga (regra 3, sem falha falsa).</summary>
    [Fact]
    [Trait("Etapa", "20")]
    public void ValaSoltaMaisPertoNaoImpedeARota()
    {
        Point3[] solta = [new(0, 2, 0), new(20, 2, 0)];
        Point3[] principal = [new(0, -5, 0), new(100, -5, 0)];
        var rede = new TrenchNetwork([solta, principal]);
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Inverter, Guid.NewGuid()), "INV1", new Point3(10, 0, 100.8),
            new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", new Point3(90, -8, 100.8));

        var r = CableRouter.Equipment([t], CableRoute.AlternatingCurrent, rede, RouteSettings.Default(CableRoute.AlternatingCurrent), Chao);

        Assert.Empty(r.Failures);
        Assert.Single(r.Runs);
    }

    /// <summary>O comprimento do CA, exato: subida/descida e o percurso em planta.</summary>
    [Fact]
    [Trait("Etapa", "20")]
    public void ComprimentoDoCaExato()
    {
        var rede = new TrenchNetwork([[new Point3(0, 0, 0), new Point3(100, 0, 0)]]);
        var t = new EquipmentRouteInput(new CableEnd(CableEndKind.Inverter, Guid.NewGuid()), "INV1", new Point3(10, 3, 100.8),
            new CableEnd(CableEndKind.Transformer, Guid.NewGuid()), "T1", new Point3(90, -4, 100.8));

        var lance = CableRouter.Equipment([t], CableRoute.AlternatingCurrent, rede, RouteSettings.Default(CableRoute.AlternatingCurrent), Chao).Runs.Single();

        // Do inversor (0,8 m acima) ao chão no primeiro metro lido, mais 2 m no chão, desce 0,8,
        // 80 m pela vala, sobe 0,8, 3 m no chão e sobe 0,8 no trafo.
        var esperado = Math.Sqrt(1 + 0.64) + 2 + 0.8 + 80 + 0.8 + 3 + Math.Sqrt(1 + 0.64);
        Assert.Equal(esperado, lance.Length, 6);
    }

    [Fact]
    [Trait("Etapa", "22")]
    public void PanComCrlfETabulacaoELido()
    {
        var texto = "PVObject_=pvModule\r\n\tModel=X1\r\n  PNom=700\r\n\tVoc=50\r\n Isc=18\r\nVmp=42\r\nImp=17\r\nmuISC=7.0\r\nmuVocSpec=-130\r\nmuPmpReq=-0.35\r\n";

        var m = PanModule.Parse(texto, out var faltam);

        Assert.Empty(faltam);
        Assert.Equal("X1", m!.Model);
        Assert.Equal(-0.35, m.PowerCoefficient);
    }

    /// <summary>Com o coeficiente da potência, a Vmp no calor cai mais que pelo da Voc (o da Voc era otimista).</summary>
    [Fact]
    [Trait("Etapa", "23")]
    public void VmpNoCalorUsaOCoeficienteDaPotencia()
    {
        var semGama = new PanModule("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);
        var comGama = semGama with { PowerCoefficient = -0.35 };

        // β_Vmp = −0,35 % − 0,007/18 = −0,003889 /°C; a 65 °C: 28 × 42 × (1 − 0,003889 × 40).
        Assert.Equal(28 * 42 * (1 + (-0.0035 - 0.007 / 18) * 40), CableCalc.OperatingAtMax(comGama, 28, 65), 9);
        Assert.True(CableCalc.OperatingAtMax(comGama, 28, 65) < CableCalc.OperatingAtMax(semGama, 28, 65));
    }

    [Fact]
    [Trait("Etapa", "23")]
    public void MonofasicoUsaIdaEVolta()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Ac) with { ResistanceOperating = 0.5, Reactance = 0 };

        Assert.Equal(10000 / 230.0, CableCalc.SinglePhaseCurrent(10, 230, 1), 9);
        Assert.Equal(2 * 40 * 0.1 * 0.5, CableCalc.SinglePhaseDrop(40, cabo, 100, 1), 9);
        Assert.Equal(RouteSettings.Default(CableRoute.AlternatingCurrent) with { ThreePhase = false },
            RouteSettings.Parse((RouteSettings.Default(CableRoute.AlternatingCurrent) with { ThreePhase = false }).ToFields()));
    }
}
