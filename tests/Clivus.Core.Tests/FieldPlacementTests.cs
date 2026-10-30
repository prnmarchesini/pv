using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A mesa do Clivus em cada objeto do cliente (40.2): o tipo que casa, o lado
/// da ponta baixa contra o projeto, e o centro no lugar depois das duas
/// passadas do motor.
/// </summary>
public class FieldPlacementTests
{
    private const double Grau = Math.PI / 180;

    private static TableProfile Gc800 => BifilaReference.Profile(BifilaVariant.Gc800);

    /// <summary>A mesma mesa da GC800 (2P14 a 10°), com estrutura monoposte.</summary>
    private static TableProfile Monoposte2P14 => Gc800 with
    {
        Name = "2P14 monoposte",
        Frame = new TableFrame(3.00, 1.50, 0.15, 0.07, 3.50, 0.851),
    };

    /// <summary>A usina olhando para o sul (os objetos do cliente) e a faixa de PB que a bifila pede.</summary>
    private static ProjectSettings ParaOSul => ProjectSettings.Default with
    {
        Configuration = ProjectSettings.Default.Configuration with { FacingAzimuthRadians = Math.PI, MinLowEdge = 0.85, MaxLowEdge = 1.6 },
    };

    private static Tin Terreno(Func<double, double, double> z)
    {
        Point3 P(double x, double y) => new(x, y, z(x, y));
        return new Tin([new Triangle(P(-100, -100), P(400, -100), P(400, 400)), new Triangle(P(-100, -100), P(400, 400), P(-100, 400))]);
    }

    /// <summary>
    /// A área dos módulos de uma 2P14 com o canto de trás-esquerda em (x0, y0),
    /// a fileira para o leste subindo <paramref name="rampa"/> e a ponta baixa
    /// para o sul, a 10°: como o cliente desenha.
    /// </summary>
    private static FieldObject Objeto(string id, double x0, double y0, double rampa = 0, double comprimento = 18.502, double largura = 4.788)
    {
        var g = Math.Atan(rampa);
        var t = 10 * Grau;
        var u = new Point3(comprimento * Math.Cos(g), 0, comprimento * Math.Sin(g));
        var v = new Point3(largura * Math.Sin(t) * Math.Sin(g), -largura * Math.Cos(t), -largura * Math.Sin(t) * Math.Cos(g));
        var z0 = rampa * x0;

        return new FieldObject(id, "CELDAS",
        [
            new(x0, y0, z0),
            new(x0 + u.X, y0 + u.Y, z0 + u.Z),
            new(x0 + u.X + v.X, y0 + u.Y + v.Y, z0 + u.Z + v.Z),
            new(x0 + v.X, y0 + v.Y, z0 + v.Z),
        ]);
    }

    /// <summary>Duas fileiras de três, 0,35 m entre as mesas, 6,8 m entre as fileiras.</summary>
    private static List<FieldObject> DuasFileiras(double rampa = 0)
    {
        var passo = 18.502 * Math.Cos(Math.Atan(rampa)) + 0.35;
        return
        [
            Objeto("A1", 0, 0, rampa), Objeto("A2", passo, 0, rampa), Objeto("A3", 2 * passo, 0, rampa),
            Objeto("B1", 0, -6.8, rampa), Objeto("B2", passo, -6.8, rampa), Objeto("B3", 2 * passo, -6.8, rampa),
        ];
    }

    /// <summary>O centro da área dos módulos de cada mesa processada, em planta.</summary>
    private static Dictionary<string, (double X, double Y)> CentrosDosModulos(FieldPlant usina)
    {
        var centros = new Dictionary<string, (double, double)>();

        foreach (var mesa in usina.Plant.Rows.SelectMany(r => r.Tables))
        {
            var g = usina.Geometries[mesa.Cell.Kind];
            var cantos = g.Modules.SelectMany(m => m.TopFace).Select(mesa.Pillars.Placement.Apply).ToList();
            centros[mesa.Cell.Label] = ((cantos.Min(p => p.X) + cantos.Max(p => p.X)) / 2, (cantos.Min(p => p.Y) + cantos.Max(p => p.Y)) / 2);
        }

        return centros;
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void AGc800CasaComOObjetoDoClienteSemAsSobras()
    {
        var f = FieldObjects.Footprint(Objeto("A", 0, 0), out _)!;

        var casadas = FieldPlacement.Match([f], [Monoposte2P14 with { TiltRadians = 20 * Grau }, Gc800], out var problemas);

        Assert.Empty(problemas);
        var c = Assert.Single(casadas);
        Assert.Equal(1, c.Kind);
        Assert.True(c.ModulesOnly);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void QuandoDuasCasamGanhaAPrimeiraDaLista()
    {
        var f = FieldObjects.Footprint(Objeto("A", 0, 0), out _)!;

        Assert.Equal(0, Assert.Single(FieldPlacement.Match([f], [Monoposte2P14, Gc800], out _)).Kind);
        Assert.Equal(0, Assert.Single(FieldPlacement.Match([f], [Gc800, Monoposte2P14], out _)).Kind);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ObjetoSemMesaDoTamanhoNaoViraMesa()
    {
        var f = FieldObjects.Footprint(Objeto("A", 0, 0, comprimento: 9.3), out _)!;

        Assert.Empty(FieldPlacement.Match([f], [Gc800], out var problemas));
        var p = Assert.Single(problemas);
        Assert.Equal(FieldIssueKind.NoProfile, p.Kind);
        Assert.True(p.Skips);
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ProjetoOlhandoParaONorteComObjetosParaOSulEhRecusado()
    {
        var levantamento = FieldObjects.Survey(DuasFileiras());

        var problema = FieldPlacement.FacingIssue(levantamento, ProjectSettings.Default.Configuration);

        Assert.NotNull(problema);
        Assert.Equal(FieldIssueKind.ProjectFacing, problema.Kind);
        Assert.Contains("o sul", problema.Message);
        Assert.Contains("o norte", problema.Message);
        Assert.Null(FieldPlacement.FacingIssue(levantamento, ParaOSul.Configuration));
    }

    /// <summary>No plano, a mesa cai no objeto, e cada objeto vira a mesa da fileira e da posição dele.</summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void NoPlanoCadaMesaCaiNoSeuObjeto()
    {
        var objetos = DuasFileiras();
        var levantamento = FieldObjects.Survey(objetos, profile: Gc800);
        var usina = FieldPlacement.Process(levantamento, [Gc800], Terreno((_, _) => 700), ParaOSul);

        Assert.Empty(usina.Issues);
        Assert.Equal(6, usina.Plant.Rows.Sum(r => r.Tables.Count));
        Assert.Equal("F1.1", usina.LabelByObject["A1"]);
        Assert.Equal("F1.3", usina.LabelByObject["A3"]);
        Assert.Equal("F2.2", usina.LabelByObject["B2"]);

        var centros = CentrosDosModulos(usina);
        foreach (var o in objetos)
        {
            var f = FieldObjects.Footprint(o, out _)!;
            var (x, y) = centros[usina.LabelByObject[o.Id]];
            Assert.True(Math.Abs(x - f.Center.X) < 0.005 && Math.Abs(y - f.Center.Y) < 0.005, $"{o.Id}: centro em ({x:0.000}; {y:0.000}), objeto em ({f.Center.X:0.000}; {f.Center.Y:0.000})");
        }
    }

    /// <summary>
    /// Terreno subindo 5 % na fileira e objetos desenhados planos: a primeira
    /// passada erra pela rampa (o motor encurta a planta), e a segunda corrige.
    /// </summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void NaRampaASegundaPassadaPoeOCentroNoLugar()
    {
        var objetos = DuasFileiras(rampa: 0);
        var levantamento = FieldObjects.Survey(objetos, profile: Gc800);
        var usina = FieldPlacement.Process(levantamento, [Gc800], Terreno((x, _) => 700 + 0.05 * x), ParaOSul);

        Assert.All(usina.DeviationByObject.Values, d => Assert.True(d < 0.005, $"desvio de {d:0.000} m"));
        Assert.DoesNotContain(usina.Issues, i => i.Kind == FieldIssueKind.Deviation);
        Assert.All(usina.Plant.Rows.SelectMany(r => r.Tables), t => Assert.True(Math.Abs(t.Pillars.LongitudinalTiltRadians) > 1 * Grau, "o motor tinha que girar a mesa com a rampa"));
    }

    /// <summary>
    /// O lugar das mesas não depende da estrutura (39.2): os mesmos objetos
    /// com a monoposte e com o biposte dão os módulos no mesmo lugar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "40")]
    public void MonoEBipostePoemOsModulosNoMesmoLugar()
    {
        var objetos = DuasFileiras(rampa: 0.04);
        var terreno = Terreno((x, y) => 700 + 0.04 * x + 0.02 * y);

        var bi = FieldPlacement.Process(FieldObjects.Survey(objetos), [Gc800], terreno, ParaOSul);
        var mono = FieldPlacement.Process(FieldObjects.Survey(objetos), [Monoposte2P14], terreno, ParaOSul);

        var (cb, cm) = (CentrosDosModulos(bi), CentrosDosModulos(mono));
        Assert.Equal(cb.Keys.Order(), cm.Keys.Order());
        foreach (var rotulo in cb.Keys)
            Assert.True(Math.Abs(cb[rotulo].X - cm[rotulo].X) < 0.01 && Math.Abs(cb[rotulo].Y - cm[rotulo].Y) < 0.01, $"{rotulo}: biposte ({cb[rotulo].X:0.000}; {cb[rotulo].Y:0.000}), monoposte ({cm[rotulo].X:0.000}; {cm[rotulo].Y:0.000})");

        Assert.Equal(12 * 6, bi.Plant.Rows.SelectMany(r => r.Tables).Sum(t => t.Pillars.Pillars.Count));
        Assert.Equal(6 * 6, mono.Plant.Rows.SelectMany(r => r.Tables).Sum(t => t.Pillars.Pillars.Count));
    }

    [Fact]
    [Trait("Etapa", "40")]
    public void ObjetoRepetidoNaoViraDuasMesas()
    {
        var objetos = DuasFileiras();
        objetos.Add(objetos[0] with { Id = "A1-copia" });

        var levantamento = FieldObjects.Survey(objetos, profile: Gc800);
        var usina = FieldPlacement.Process(levantamento, [Gc800], Terreno((_, _) => 700), ParaOSul);

        Assert.Equal(6, usina.Plant.Rows.Sum(r => r.Tables.Count));
        Assert.False(usina.LabelByObject.ContainsKey("A1-copia"));
    }
}
