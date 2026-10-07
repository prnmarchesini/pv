namespace Clivus.Core.Tests;

/// <summary>
/// A meta de strings por inversor (Renan, 07/10/2026: "o sistema distribui
/// para TODAS entradas disponíveis do inversor, quero uma opção de eu falar
/// QUANTAS strings eu quero por inversor ... em lote também").
/// </summary>
public class InverterTargetTests
{
    private static readonly InverterModel Modelo = new(Guid.NewGuid(), "Inv Top", [4, 4, 4, 4, 4, 4], new EquipmentSize(1.1, 0.7, 0.6), 250);

    [Fact]
    [Trait("Etapa", "14")]
    public void AMetaVaiEVoltaEOFormatoAntigoContinuaSendoLido()
    {
        var comMeta = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty, new RgbColor(10, 20, 30), 18);
        Assert.Equal(comMeta, Inverter.Parse(comMeta.ToFields()));

        var semMeta = comMeta with { Target = null };
        Assert.Equal(semMeta, Inverter.Parse(semMeta.ToFields()));

        // Formato 2 (5 campos, sem a meta).
        Assert.Equal(semMeta, Inverter.Parse(semMeta.ToFields().Take(Inverter.ColorFieldCount).ToList()));

        Assert.Null(Inverter.Parse([.. semMeta.ToFields().Take(5), "0"]));
        Assert.Null(Inverter.Parse([.. semMeta.ToFields().Take(5), "x"]));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AMetaLimitaSemPassarDasEntradas()
    {
        var i = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty);
        Assert.Equal(24, i.Limit(24));
        Assert.Equal(18, (i with { Target = 18 }).Limit(24));
        Assert.Equal(24, (i with { Target = 30 }).Limit(24));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AMetaEmLoteRecusaOQuePassaDasEntradas()
    {
        var setup = new ElectricalSetup(models: [Modelo]);
        var inversores = setup.AddInverters(Modelo.Id, 3);
        var ids = inversores.Select(i => i.Id).ToList();

        Assert.Equal((3, (string?)null), setup.SetTarget(ids, 18));
        Assert.All(setup.Inverters, i => Assert.Equal(18, i.Target));

        Assert.NotNull(setup.SetTarget(ids, 25).Problem);
        Assert.All(setup.Inverters, i => Assert.Equal(18, i.Target));
        Assert.NotNull(setup.SetTarget(ids, 0).Problem);

        Assert.Equal(1, setup.SetTarget([ids[0]], null).Changed);
        Assert.Null(setup.FindInverter(ids[0])!.Target);
        Assert.Equal(0, setup.SetTarget(ids.Skip(1), 18).Changed);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AMetaEscritaNaCaixa()
    {
        Assert.Equal((null, null), InverterTable.ParseTarget("  "));
        Assert.Equal(((int?)18, (string?)null), InverterTable.ParseTarget(" 18 "));
        Assert.NotNull(InverterTable.ParseTarget("0").Problem);
        Assert.NotNull(InverterTable.ParseTarget("-3").Problem);
        Assert.NotNull(InverterTable.ParseTarget("1,5").Problem);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ODistribuirParaNaMetaDeCadaInversor()
    {
        var a = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty, Target: 2);
        var b = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 2", Guid.Empty, Target: 3);
        var modulos = new Dictionary<Guid, ModuleSpot>();
        var strings = new List<ElectricalString>();
        for (var k = 0; k < 10; k++)
        {
            var m = Guid.NewGuid();
            modulos[m] = new ModuleSpot(Guid.NewGuid(), k * 10, 0);
            strings.Add(new ElectricalString(Guid.NewGuid(), Guid.Empty, [m], Guid.Empty, string.Empty));
        }

        var r = StringAutoAllocation.Allocate([a, b], [Modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal(2, r.Added[a.Id]);
        Assert.Equal(3, r.Added[b.Id]);
        Assert.Equal(5, r.Leftover);
    }
}
