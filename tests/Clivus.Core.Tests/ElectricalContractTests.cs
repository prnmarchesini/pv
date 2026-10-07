namespace Clivus.Core.Tests;

/// <summary>O contrato da parte elétrica: cada registro vai e volta dos campos do desenho, e campo estragado não vira registro.</summary>
public class ElectricalContractTests
{
    private static readonly EquipmentSize Caixa = new(2.4, 1.2, 2.1);

    [Fact]
    [Trait("Etapa", "11")]
    public void AStringVaiEVoltaComOVinculo()
    {
        var modulos = Enumerable.Range(0, 28).Select(_ => Guid.NewGuid()).ToList();
        var livre = new ElectricalString(Guid.NewGuid(), Guid.Empty, modulos, Guid.Empty, string.Empty);
        var alocada = livre with { Type = Guid.NewGuid(), Inverter = Guid.NewGuid(), Tag = "T1.I2.S3" };

        Assert.Equal(livre, ElectricalString.Parse(livre.ToFields()));
        Assert.Equal(alocada, ElectricalString.Parse(alocada.ToFields()));
        Assert.False(livre.IsAllocated);
        Assert.True(alocada.IsAllocated);
        Assert.Equal(ElectricalString.FixedFieldCount + 28, livre.ToFields().Count);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void StringEstragadaOuRepetindoModuloNaoVale()
    {
        var m = Guid.NewGuid();
        var campos = new ElectricalString(Guid.NewGuid(), Guid.Empty, [m, Guid.NewGuid()], Guid.Empty, "").ToFields().ToList();

        var faltando = campos.Take(campos.Count - 1).ToList();
        Assert.Null(ElectricalString.Parse(faltando));

        var repetida = campos.ToList();
        repetida[^1] = m.ToString("D");
        Assert.Null(ElectricalString.Parse(repetida));

        var semModulo = new ElectricalString(Guid.NewGuid(), Guid.Empty, [], Guid.Empty, "").ToFields();
        Assert.Null(ElectricalString.Parse(semModulo));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OsCadastrosVaoEVoltam()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "Huawei 250", 6, 4, Caixa);
        var inversor = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", Guid.Empty);
        var trafo = new Transformer(Guid.NewGuid(), "Trafo seco", "T1", 800, 13800, 2500, 4, 6.5, "nota | com separador", Caixa, Guid.NewGuid());
        var uc = new ConsumerUnit(Guid.NewGuid(), "UC1", "Medição norte", ConsumerUnitMode.Shared, Caixa);
        var lugar = new EquipmentPlacement(EquipmentKind.Transformer, trafo.Id);

        Assert.Equal(24, modelo.TotalInputs);
        Assert.Equal(modelo, InverterModel.Parse(modelo.ToFields()));
        Assert.Equal(inversor, Inverter.Parse(inversor.ToFields()));
        Assert.Equal(trafo, Transformer.Parse(trafo.ToFields()));
        Assert.Equal(uc, ConsumerUnit.Parse(uc.ToFields()));
        Assert.Equal(uc with { Mode = ConsumerUnitMode.Unitary }, ConsumerUnit.Parse((uc with { Mode = ConsumerUnitMode.Unitary }).ToFields()));
        Assert.Equal(lugar, EquipmentPlacement.Parse(lugar.ToFields()));

        Assert.Equal(InverterModel.FieldCount, modelo.ToFields().Count);
        Assert.Equal(Inverter.FieldCount, inversor.ToFields().Count);
        Assert.Equal(Transformer.FieldCount, trafo.ToFields().Count);
        Assert.Equal(ConsumerUnit.FieldCount, uc.ToFields().Count);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void CadastroInvalidoNaoVoltaDoDesenho()
    {
        Assert.Null(InverterModel.Parse(new InverterModel(Guid.NewGuid(), "X", 0, 4, Caixa).ToFields()));
        Assert.Null(InverterModel.Parse(new InverterModel(Guid.NewGuid(), "X", 2, 4, new EquipmentSize(0, 1, 1)).ToFields()));
        Assert.Null(Inverter.Parse(new Inverter(Guid.NewGuid(), Guid.Empty, "I1", Guid.Empty).ToFields()));
        Assert.Null(ConsumerUnit.Parse(["6f9619ff-8b86-d011-b42d-00cf4fc964ff", "UC1", "", "X", "1", "1", "1"]));
        Assert.Null(EquipmentPlacement.Parse(["Bomba", Guid.NewGuid().ToString()]));
        Assert.Null(Transformer.Parse(new Transformer(Guid.NewGuid(), "", "  ", 1, 1, 1, 1, 1, "", Caixa, Guid.Empty).ToFields()));
    }
}
