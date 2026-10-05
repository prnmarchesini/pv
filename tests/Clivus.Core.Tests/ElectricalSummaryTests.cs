namespace Clivus.Core.Tests;

/// <summary>O resumo do sistema elétrico (elétrica, 16.1): consolidado pela cadeia de vínculo, nunca pela posição.</summary>
public class ElectricalSummaryTests
{
    private static readonly EquipmentSize Caixa = new(2, 1, 2.2);

    /// <summary>
    /// C1 com o trafo TA; TB sem UC; modelo de 2x2 (4 entradas); inversores
    /// 1 e 2 no TA, 3 no TB, 4 sem trafo. Módulos de 550 W.
    /// </summary>
    private sealed class Cadeia
    {
        public readonly ConsumerUnit C1 = new(Guid.NewGuid(), "C1", "Medição norte", ConsumerUnitMode.Shared, Caixa);
        public readonly Transformer TA;
        public readonly Transformer TB;
        public readonly InverterModel Modelo = new(Guid.NewGuid(), "Teste 2x2", 2, 2, Caixa);
        public readonly Inverter I1;
        public readonly Inverter I2;
        public readonly Inverter I3;
        public readonly Inverter I4;
        public readonly List<ElectricalString> Strings = [];
        public readonly Dictionary<Guid, double?> Potencia = [];

        public Cadeia()
        {
            TA = new Transformer(Guid.NewGuid(), "Trafo seco", "TA", 800, 13800, 2500, 4, 6.5, "", Caixa, C1.Id);
            TB = new Transformer(Guid.NewGuid(), "Trafo seco", "TB", 800, 13800, 1000, 4, 6.5, "", Caixa, Guid.Empty);
            I1 = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", TA.Id);
            I2 = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 2", TA.Id);
            I3 = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 3", TB.Id);
            I4 = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 4", Guid.Empty);
        }

        public ElectricalString Str(Guid inversor, int modulos = 10, double? watts = 550, string tag = "")
        {
            var ids = Enumerable.Range(0, modulos).Select(_ => Guid.NewGuid()).ToList();
            foreach (var m in ids) Potencia[m] = watts;
            var s = new ElectricalString(Guid.NewGuid(), Guid.Empty, ids, inversor, tag);
            Strings.Add(s);
            return s;
        }

        public SystemSummary Resumo(double? reserva = null) =>
            ElectricalSummary.Build([C1], [TA, TB], [Modelo], [I1, I2, I3, I4], Strings, Potencia, reserva);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void ACadeiaConsolidaPorVinculo()
    {
        var c = new Cadeia();
        for (var i = 0; i < 5; i++) c.Str(c.I1.Id, tag: "x");
        for (var i = 0; i < 4; i++) c.Str(c.I2.Id, tag: "x");
        for (var i = 0; i < 3; i++) c.Str(c.I3.Id, tag: "x");
        for (var i = 0; i < 2; i++) c.Str(c.I4.Id, tag: "x");
        c.Str(Guid.Empty);
        c.Str(Guid.Empty, modulos: 14);

        var r = c.Resumo();

        // C1 -> TA -> inversores 1 e 2.
        var c1 = Assert.Single(r.Units);
        Assert.Equal(c.C1.Id, c1.Unit.Id);
        var ta = Assert.Single(c1.Transformers);
        Assert.Equal([c.I1.Id, c.I2.Id], ta.Inverters.Select(i => i.Inverter.Id));
        Assert.Equal((9, 90, 49.5), (ta.Strings, ta.Modules, Math.Round(ta.PowerKwp, 6)));
        Assert.Equal((9, 90, 49.5), (c1.Strings, c1.Modules, Math.Round(c1.PowerKwp, 6)));

        // TB sem UC; inversor 4 sem trafo.
        var tb = Assert.Single(r.TransformersWithoutUnit);
        Assert.Equal(c.TB.Id, tb.Transformer.Id);
        Assert.Equal(3, tb.Strings);
        var semTrafo = Assert.Single(r.InvertersWithoutTransformer);
        Assert.Equal(c.I4.Id, semTrafo.Inverter.Id);

        // Totais: as livres contam à parte.
        Assert.Equal((1, 2, 4), (r.UnitCount, r.TransformerCount, r.InverterCount));
        Assert.Equal((16, 14, 2), (r.StringCount, r.AllocatedStrings, r.FreeStrings));
        Assert.Equal(140, r.Modules);
        Assert.Equal(77.0, r.PowerKwp, 6);
        Assert.Equal(24, r.FreeModules);
        Assert.Equal(13.2, r.FreePowerKwp, 6);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void InversorAcimaDaCapacidadeEhPendencia()
    {
        var c = new Cadeia();
        for (var i = 0; i < 5; i++) c.Str(c.I1.Id);
        for (var i = 0; i < 4; i++) c.Str(c.I2.Id);

        var r = c.Resumo();
        var i1 = r.Units[0].Transformers[0].Inverters[0];
        var i2 = r.Units[0].Transformers[0].Inverters[1];

        Assert.Equal((5, 4, true), (i1.Strings, i1.Capacity, i1.OverCapacity));
        Assert.Equal((4, 4, false), (i2.Strings, i2.Capacity, i2.OverCapacity));
        Assert.Equal([c.I1.Id], r.OverCapacity.Select(i => i.Inverter.Id));
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void ElosQuebradosViramPendenciaENaoSomemDoTotal()
    {
        var c = new Cadeia();
        var fantasma = new Inverter(Guid.NewGuid(), Guid.NewGuid(), "Inversor 9", Guid.NewGuid());
        c.Str(fantasma.Id);                      // inversor que não está no cadastro
        var semPotencia = c.Str(c.I1.Id, watts: null);
        var sumida = c.Str(c.I1.Id);
        c.Potencia.Remove(sumida.Modules[0]);   // módulo que não está no desenho
        c.Str(c.I1.Id, tag: "T1.I1.S1");

        var r = ElectricalSummary.Build([c.C1], [c.TA with { ConsumerUnit = Guid.NewGuid() }], [], [c.I1, fantasma with { Id = Guid.NewGuid() }], c.Strings, c.Potencia, 500);

        Assert.Equal(1, r.StringsWithUnknownInverter);
        Assert.Equal(1, r.ModulesNotInDrawing);
        Assert.Equal(semPotencia.Modules.Count, r.ModulesWithFallbackPower);
        Assert.Equal(2, r.InvertersWithUnknownModel);   // o modelo não está no cadastro: capacidade desconhecida
        Assert.Equal(1, r.InvertersWithUnknownTransformer);
        Assert.Equal(1, r.TransformersWithUnknownUnit);
        Assert.Equal(2, r.AllocatedWithoutTag);

        // A potência: 10 × 500 (reserva) + 9 × 550 + 10 × 550 (o módulo sumido não conta).
        Assert.Equal((10 * 500 + 9 * 550 + 10 * 550) / 1000.0, r.PowerKwp, 6);
        Assert.Equal(3, r.AllocatedStrings);
        Assert.Empty(r.Units[0].Transformers);
        Assert.Single(r.TransformersWithoutUnit);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void StringCopiadaComOMesmoGuidFicaForaDosTotaisEAvisada()
    {
        var c = new Cadeia();
        var s = c.Str(c.I1.Id);
        c.Strings.Add(s with { Inverter = c.I2.Id });   // a cópia aponta para outro inversor
        c.Str(c.I2.Id);

        var r = c.Resumo();
        Assert.Equal(2, r.DuplicateStrings);
        Assert.Equal((3, 1), (r.StringCount, r.AllocatedStrings));
        Assert.Equal(0, r.Units[0].Transformers[0].Inverters[0].Strings);
        Assert.Contains(r.Pending(), p => p.Contains("repetida"));
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void ModuloEmDuasStringsEModuloSemMesaSaoPendencia()
    {
        var c = new Cadeia();
        var a = c.Str(c.I1.Id);
        c.Strings.Add(new ElectricalString(Guid.NewGuid(), Guid.Empty, [a.Modules[0], Guid.NewGuid()], c.I2.Id, ""));
        var orfao = c.Strings[^1].Modules[1];
        var livre = c.Str(Guid.Empty);
        c.Potencia.Remove(livre.Modules[0]);   // módulo sumido numa string livre: não é pendência dos totais

        var r = ElectricalSummary.Build([c.C1], [c.TA], [c.Modelo], [c.I1, c.I2], c.Strings, c.Potencia, null, new HashSet<Guid> { orfao });

        Assert.Equal(1, r.ModulesInMoreThanOneString);
        Assert.Equal(1, r.ModulesWithoutTable);
        Assert.Equal(0, r.ModulesNotInDrawing);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void SemReservaOModuloSemPotenciaNaoInventaNumero()
    {
        var c = new Cadeia();
        c.Str(c.I1.Id, watts: null);

        var r = c.Resumo(reserva: null);
        Assert.Equal(0, r.PowerKwp);
        Assert.Equal(10, r.ModulesWithoutPower);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void UsinaVaziaDaResumoVazioSemQuebrar()
    {
        var r = ElectricalSummary.Build([], [], [], [], [], new Dictionary<Guid, double?>(), null);
        Assert.Equal((0, 0, 0, 0), (r.UnitCount, r.TransformerCount, r.InverterCount, r.StringCount));
        Assert.NotEmpty(r.Rows());
        Assert.NotEmpty(r.Lines());
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void AsLinhasDaTabelaSeguemACadeia()
    {
        var c = new Cadeia();
        for (var i = 0; i < 5; i++) c.Str(c.I1.Id);
        c.Str(c.I3.Id);
        c.Str(c.I4.Id);
        c.Str(Guid.Empty);

        var linhas = c.Resumo().Rows();

        // Subestação, trafo, inversores; depois trafo sem UC, inversor sem trafo, livres e o total.
        Assert.Equal(
            [
                (SummaryRowKind.Unit, "C1"), (SummaryRowKind.Transformer, "TA"), (SummaryRowKind.Inverter, "Inversor 1"), (SummaryRowKind.Inverter, "Inversor 2"),
                (SummaryRowKind.Group, null), (SummaryRowKind.Transformer, "TB"), (SummaryRowKind.Inverter, "Inversor 3"),
                (SummaryRowKind.Group, null), (SummaryRowKind.Inverter, "Inversor 4"),
                (SummaryRowKind.Free, null), (SummaryRowKind.Total, null),
            ],
            linhas.Select(l => (l.Kind, l.Kind is SummaryRowKind.Group or SummaryRowKind.Free or SummaryRowKind.Total ? null : l.Name)));

        var i1 = linhas.First(l => l.Name == "Inversor 1");
        Assert.Equal((5, 50, 27.5), (i1.Strings, i1.Modules, Math.Round(i1.PowerKwp, 6)));
        Assert.Contains("5", i1.Note);
        Assert.True(i1.Warning);
    }
}
