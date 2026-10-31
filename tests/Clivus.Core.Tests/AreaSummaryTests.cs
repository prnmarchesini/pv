using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O resumo por área (etapa 41, Renan, 30/10/2026: "vou ter 7 áreas [...]
/// quero ter a opção do sistema listar o resumo de cada área [...] do resumo
/// sair por área"). A conta de cada área é a do Recontar
/// (<see cref="LayoutCensus.Count"/>); aqui se confere de que área é cada
/// mesa, a soma por tipo, os pilares, a ocupação e o total.
/// </summary>
public class AreaSummaryTests
{
    private const double Reserva = 550;

    private static IReadOnlyList<Point3> Retangulo(double x0, double y0, double x1, double y1, double z = 0) =>
        [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)];

    private static AreaInput Area(string nome, double x0, double y0, double x1, double y1, Guid? id = null, bool objetos = false) =>
        new(id ?? Guid.NewGuid(), nome, Retangulo(x0, y0, x1, y1), objetos);

    /// <summary>Uma mesa de 10 x 4 m em planta com o centro em (cx, cy), a cota z nos cantos.</summary>
    private static AreaTableInput Mesa(
        double cx, double cy, string? tipo = "Mesa 28", int modulos = 28, double? watts = 600, int pilares = 7, double comprimento = 3.0,
        double? face = 2.5, bool pendente = false, bool marcada = false, double z = 100, int contornos = 1)
    {
        var identidade = new TableIdentity(
            Guid.NewGuid(), "F1.1", z, z, 0.35, marcada, marcada ? "não cabe" : null, pendente, pendente ? "movida" : null, watts, ProfileName: tipo);

        var contada = new CountedTable(identidade, contornos, modulos, Enumerable.Repeat(comprimento, pilares).ToList(), 0);
        return new AreaTableInput(contada, Retangulo(cx - 5, cy - 2, cx + 5, cy + 2, z), face);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void ContaPorAreaEPorTipoEOTotalBateComORecontar()
    {
        var areas = new[] { Area("Norte", 0, 0, 100, 100), Area("Sul", 200, 0, 300, 100) };
        var mesas = new List<AreaTableInput>
        {
            Mesa(20, 20), Mesa(20, 40), Mesa(20, 60),
            Mesa(60, 20, tipo: "Mesa 14", modulos: 14, pilares: 4, comprimento: 2.5),
            Mesa(220, 20), Mesa(220, 40),
        };

        var resumo = AreaSummary.Build(areas, mesas, Reserva);

        var norte = resumo.Areas[0];
        Assert.Equal("Norte", norte.Name);
        Assert.Equal(4, norte.Census.Tables);
        Assert.Equal(98, norte.Census.Modules);
        Assert.Equal(58.8, norte.Census.PowerKwp, 6);
        Assert.Equal(25, norte.Census.Pillars);
        Assert.Equal(3 * 7 * 3.0 + 4 * 2.5, norte.PillarMeters, 6);
        Assert.Equal(["Mesa 28", "Mesa 14"], norte.Types.Select(t => t.Name));
        Assert.Equal((3, 84), (norte.Types[0].Tables, norte.Types[0].Modules));
        Assert.Equal(50.4, norte.Types[0].PowerKwp, 6);
        Assert.Equal((1, 14), (norte.Types[1].Tables, norte.Types[1].Modules));
        Assert.Equal("3 × Mesa 28, 1 × Mesa 14", norte.TypesText);

        var sul = resumo.Areas[1];
        Assert.Equal((2, 56, 14), (sul.Census.Tables, sul.Census.Modules, sul.Census.Pillars));
        Assert.Equal(33.6, sul.Census.PowerKwp, 6);
        Assert.Equal(42, sul.PillarMeters, 6);

        // O total é o Recontar do desenho inteiro, e as áreas somam ele.
        var recontar = LayoutCensus.Count(mesas.Select(m => m.Table).ToList(), Reserva);
        Assert.Null(resumo.Outside);
        Assert.Equal(AreaRowKind.Total, resumo.Total.Kind);
        Assert.Equal((recontar.Tables, recontar.Modules, recontar.Pillars), (resumo.Total.Census.Tables, resumo.Total.Census.Modules, resumo.Total.Census.Pillars));
        Assert.Equal(recontar.PowerKwp, resumo.Total.Census.PowerKwp, 9);
        Assert.Equal(resumo.Total.Census.Tables, resumo.Areas.Sum(a => a.Census.Tables));
        Assert.Equal(resumo.Total.PillarMeters, resumo.Areas.Sum(a => a.PillarMeters), 9);
        Assert.Equal(["Norte", "Sul", "Total"], resumo.Rows.Select(r => r.Name));
        Assert.Equal([0, 0, 0, 0, 1, 1], resumo.Assignment.Select(a => a ?? -1));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void OKwpUsaAPotenciaDeCadaMesaAReservaEASimulada()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100) };
        var mesas = new List<AreaTableInput> { Mesa(20, 20, watts: 600), Mesa(20, 40, watts: null) };

        var gravada = AreaSummary.Build(areas, mesas, Reserva).Areas[0];
        Assert.Equal((28 * 600 + 28 * 550) / 1000.0, gravada.Census.PowerKwp, 9);
        Assert.Equal(1, gravada.Census.TablesWithoutPower);

        var simulada = AreaSummary.Build(areas, mesas, Reserva, simulatedPowerWatts: 700).Areas[0];
        Assert.Equal(56 * 700 / 1000.0, simulada.Census.PowerKwp, 9);
        Assert.Equal(56 * 700 / 1000.0, simulada.Types.Sum(t => t.PowerKwp), 9);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AOcupacaoEAAreaDosModulosSobreAAreaEmPlanta()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100) };
        var mesas = Enumerable.Range(0, 5).Select(i => Mesa(20, 10 + i * 15)).Append(Mesa(70, 50, face: null)).ToList();

        var a = AreaSummary.Build(areas, mesas, Reserva).Areas[0];

        Assert.Equal(10_000, a.PlanArea!.Value, 6);
        Assert.Equal(1.0, a.Hectares!.Value, 9);
        Assert.Equal(5 * 28 * 2.5, a.ModuleArea, 9);
        Assert.Equal(0.035, a.Occupation!.Value, 9);

        // A mesa sem face no desenho fica fora da área dos módulos, e é contada.
        Assert.Equal(1, a.TablesWithoutFace);
        Assert.Equal(6, a.Census.Tables);
    }

    /// <summary>
    /// A decisão de 30/10/2026: a mesa que atravessa duas áreas conta uma vez
    /// só, na área do centro dela, e aparece como "na divisa" lá.
    /// </summary>
    [Fact]
    [Trait("Etapa", "41")]
    public void AMesaQueAtravessaDuasAreasContaUmaVezNaDoCentro()
    {
        var areas = new[] { Area("Oeste", 0, 0, 100, 100), Area("Leste", 100, 0, 200, 100) };
        var mesas = new List<AreaTableInput>
        {
            Mesa(98, 50),    // de 93 a 103: o centro no Oeste, dois cantos no Leste
            Mesa(103, 20),   // de 98 a 108: o centro no Leste, dois cantos no Oeste
            Mesa(150, 80),   // inteira no Leste
        };

        var resumo = AreaSummary.Build(areas, mesas, Reserva);

        Assert.Equal((1, 1), (resumo.Areas[0].Census.Tables, resumo.Areas[0].OnBorder));
        Assert.Equal((2, 1), (resumo.Areas[1].Census.Tables, resumo.Areas[1].OnBorder));
        Assert.Equal(3, resumo.Total.Census.Tables);
        Assert.Equal(2, resumo.Total.OnBorder);
        Assert.Null(resumo.Outside);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AMesaComOCentroNaDivisaContaUmaVezSo()
    {
        var areas = new[] { Area("Oeste", 0, 0, 100, 100), Area("Leste", 100, 0, 200, 100) };

        var resumo = AreaSummary.Build(areas, [Mesa(100, 50)], Reserva);

        Assert.Equal(1, resumo.Areas.Sum(a => a.Census.Tables));
        Assert.Equal(1, resumo.Total.Census.Tables);
        Assert.Null(resumo.Outside);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void DentroDeDuasAreasAMesaContaNaMenor()
    {
        var areas = new[] { Area("Grande", 0, 0, 300, 300), Area("Pequena", 100, 100, 200, 200) };
        var mesas = new List<AreaTableInput> { Mesa(150, 150), Mesa(50, 50) };

        var resumo = AreaSummary.Build(areas, mesas, Reserva);

        Assert.Equal(1, resumo.Areas[0].Census.Tables);
        Assert.Equal(1, resumo.Areas[1].Census.Tables);
        Assert.Equal<int?>([1, 0], resumo.Assignment);

        // A área do total é a união: a pequena está dentro da grande.
        Assert.Equal(90_000, resumo.Total.PlanArea!.Value, 3);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AMesaForaDeTodaAreaVaiParaForaDasAreasEOTotalContaEla()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100) };
        var mesas = new List<AreaTableInput> { Mesa(50, 50), Mesa(500, 500, tipo: "Mesa 14", modulos: 14) };

        var resumo = AreaSummary.Build(areas, mesas, Reserva);

        var fora = Assert.IsType<AreaSummaryRow>(resumo.Outside);
        Assert.Equal(AreaRowKind.Outside, fora.Kind);
        Assert.Null(fora.Id);
        Assert.Null(fora.PlanArea);
        Assert.Null(fora.Occupation);
        Assert.Equal((1, 14), (fora.Census.Tables, fora.Census.Modules));
        Assert.Equal("1 × Mesa 14", fora.TypesText);

        Assert.Equal(2, resumo.Total.Census.Tables);
        Assert.Equal(42, resumo.Total.Census.Modules);
        Assert.Equal(3, resumo.Rows.Count());
        Assert.Equal<int?>([0, null], resumo.Assignment);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AAreaVaziaTemTudoZeroEOcupacaoZero()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100), Area("Vazia", 200, 0, 250, 100) };

        var vazia = AreaSummary.Build(areas, [Mesa(50, 50)], Reserva).Areas[1];

        Assert.Equal("Vazia", vazia.Name);
        Assert.Equal((0, 0, 0, 0), (vazia.Census.Tables, vazia.Census.Modules, vazia.Census.Pillars, vazia.OnBorder));
        Assert.Equal(0, vazia.Census.PowerKwp);
        Assert.Equal(0, vazia.PillarMeters);
        Assert.Empty(vazia.Types);
        Assert.Equal(string.Empty, vazia.TypesText);
        Assert.Equal(5_000, vazia.PlanArea!.Value, 6);
        Assert.Equal(0, vazia.Occupation);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void SemAreaNenhumaAsMesasFicamForaEOTotalNaoTemArea()
    {
        var resumo = AreaSummary.Build([], [Mesa(0, 0), Mesa(50, 0)], Reserva);

        Assert.Empty(resumo.Areas);
        Assert.Equal(2, resumo.Outside!.Census.Tables);
        Assert.Equal(2, resumo.Total.Census.Tables);
        Assert.Null(resumo.Total.PlanArea);
        Assert.Null(resumo.Total.Occupation);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void PendentesENaoCabemSaoDaAreaDelas()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100), Area("B", 200, 0, 300, 100) };
        var mesas = new List<AreaTableInput> { Mesa(20, 20, pendente: true), Mesa(20, 40, marcada: true), Mesa(220, 20, pendente: true) };

        var resumo = AreaSummary.Build(areas, mesas, Reserva);

        Assert.Equal((1, 1), (resumo.Areas[0].Census.Dirty, resumo.Areas[0].Census.Marked));
        Assert.Equal((1, 0), (resumo.Areas[1].Census.Dirty, resumo.Areas[1].Census.Marked));
        Assert.Equal((2, 1), (resumo.Total.Census.Dirty, resumo.Total.Census.Marked));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AsPecasOrfasContamPeloPontoDelasComoTipoProprio()
    {
        var areas = new[] { Area("A", 0, 0, 100, 100) };
        var orfa = new AreaTableInput(new CountedTable(null, 0, 28, [2.5, 2.5], 0), [new Point3(30, 30, 100)]);

        var a = AreaSummary.Build(areas, [Mesa(60, 60), orfa], Reserva).Areas[0];

        Assert.Equal(1, a.Census.Tables);
        Assert.Equal(1, a.Census.Orphans);
        Assert.Equal(56, a.Census.Modules);
        Assert.Equal(9, a.Census.Pillars);
        Assert.Contains(a.Types, t => t.Name == "(peças órfãs)" && t.Tables == 0 && t.Modules == 28);
        Assert.Equal("1 × Mesa 28", a.TypesText);
        Assert.Equal(1, a.TablesWithoutFace);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AMesaCopiadaSemIdentidadePropriaContaPelosContornos()
    {
        var a = AreaSummary.Build([Area("A", 0, 0, 100, 100)], [Mesa(50, 50, contornos: 2)], Reserva).Areas[0];

        Assert.Equal(2, a.Census.Tables);
        Assert.Equal(1, a.Census.Duplicated);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AMesaSemNomeDeTipoVaiParaSemTipoEOsTiposVemDoMaiorParaOMenor()
    {
        var mesas = new List<AreaTableInput> { Mesa(10, 10, tipo: null), Mesa(10, 30, tipo: "B"), Mesa(10, 50, tipo: "B"), Mesa(10, 70, tipo: " b ") };

        var a = AreaSummary.Build([Area("A", 0, 0, 100, 100)], mesas, Reserva).Areas[0];

        // O nome do tipo sem espaço em volta e sem diferença de maiúscula (como DrawingTables.Find).
        Assert.Equal(["B", "(sem tipo)"], a.Types.Select(t => t.Name));
        Assert.Equal("3 × B, 1 × (sem tipo)", a.TypesText);
    }

    /// <summary>A polilinha da área copiada leva o GUID junto: cada cópia conta as mesas dela.</summary>
    [Fact]
    [Trait("Etapa", "41")]
    public void AAreaCopiadaComOMesmoGuidContaAsMesasDela()
    {
        var id = Guid.NewGuid();
        var areas = new[] { Area("A", 0, 0, 100, 100, id), Area("A", 500, 0, 600, 100, id) };

        var resumo = AreaSummary.Build(areas, [Mesa(50, 50), Mesa(550, 50), Mesa(560, 50)], Reserva);

        Assert.Equal(1, resumo.Areas[0].Census.Tables);
        Assert.Equal(2, resumo.Areas[1].Census.Tables);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AAreaQueSegueOsObjetosDizIsso()
    {
        var resumo = AreaSummary.Build([Area("A", 0, 0, 100, 100, objetos: true), Area("B", 200, 0, 300, 100)], [], Reserva);

        Assert.True(resumo.Areas[0].FollowsObjects);
        Assert.False(resumo.Areas[1].FollowsObjects);
        Assert.False(resumo.Total.FollowsObjects);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AUniaoDasAreasNaoContaDuasVezesOChaoSobreposto()
    {
        Assert.Equal(17_500, AreaSummary.UnionArea([Retangulo(0, 0, 100, 100), Retangulo(50, 50, 150, 150)]), 3);

        // Em UTM, a conta é a mesma (milímetro inteiro na Clipper2).
        const double x = 700_000, y = 7_400_000;
        Assert.Equal(17_500, AreaSummary.UnionArea([Retangulo(x, y, x + 100, y + 100), Retangulo(x + 50, y + 50, x + 150, y + 150)]), 3);

        // Áreas separadas somam; menos de três vértices não conta.
        Assert.Equal(20_000, AreaSummary.UnionArea([Retangulo(0, 0, 100, 100), Retangulo(200, 0, 300, 100), [new Point3(0, 0, 0), new Point3(1, 1, 0)]]), 3);
        Assert.Equal(0, AreaSummary.UnionArea([]));
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void AFaceDoModuloEmVerdadeiraGrandeza()
    {
        // Um módulo de 2,278 x 1,134 m inclinado 20 graus: a planta encolhe, a face não.
        var (sen, cos) = Math.SinCos(20 * Math.PI / 180);
        var a = new Point3(0, 0, 0);
        var b = new Point3(2.278, 0, 0);
        var c = new Point3(2.278, 1.134 * cos, 1.134 * sen);
        var d = new Point3(0, 1.134 * cos, 1.134 * sen);

        Assert.Equal(2.278 * 1.134, AreaSummary.QuadArea(a, b, c, d), 9);
        Assert.Equal(0.5, AreaSummary.QuadArea(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 1, 0)), 9);
    }

    [Fact]
    [Trait("Etapa", "41")]
    public void OCentroDaMesaEOMeioDoEnvelopeComOuSemOPontoDeFechamento()
    {
        var mesa = Retangulo(10, 20, 30, 24);

        Assert.Equal(new Point3(20, 22, 0), AreaSummary.Center(mesa));
        Assert.Equal(new Point3(20, 22, 0), AreaSummary.Center([.. mesa, mesa[0]]));
        Assert.Null(AreaSummary.Center([]));
        Assert.Null(AreaSummary.AreaOf([], [Area("A", 0, 0, 100, 100)]));
    }
}
