namespace Clivus.Core.Tests;

/// <summary>
/// O bloco físico da subestação compartilhada (reprovação de 05/10/2026: "era
/// para ter uma listinha para eu colocar as UCs que fazem parte daquela
/// subestação"). Um cubículo em campo com as UCs C1, C2... dentro; a unitária
/// continua sendo bloco e UC ao mesmo tempo. Desenho do formato antigo
/// continua sendo lido.
/// </summary>
public class SubstationBlockTests
{
    private static readonly EquipmentSize Caixa = new(4, 3, 3);

    [Fact]
    [Trait("Etapa", "12")]
    public void OBlocoIdaEVoltaPeloRegistro()
    {
        var b = new Substation(Guid.NewGuid(), "Cubículo norte", new EquipmentSize(6, 3, 2.8));

        Assert.Equal(Substation.FieldCount, b.ToFields().Count);
        Assert.Equal(b, Substation.Parse(b.ToFields()));
        Assert.Null(Substation.Parse([b.Id.ToString(), " ", "1", "1", "1"]));
        Assert.Null(Substation.Parse([b.Id.ToString(), "X", "0", "1", "1"]));
        Assert.Null(Substation.Parse(["nao-guid", "X", "1", "1", "1"]));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void AUcGuardaOBlocoEOFormatoAntigoContinuaSendoLido()
    {
        var bloco = Guid.NewGuid();
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "Medição 1", ConsumerUnitMode.Shared, Caixa, bloco);

        Assert.Equal(ConsumerUnit.FieldCount, c1.ToFields().Count);
        Assert.Equal(c1, ConsumerUnit.Parse(c1.ToFields()));

        // Formato 1: 7 campos, sem bloco.
        var antigo = c1.ToFields().Take(ConsumerUnit.LegacyFieldCount).ToList();
        Assert.Equal(c1 with { Substation = Guid.Empty }, ConsumerUnit.Parse(antigo));

        // A unitária nunca tem bloco, nem se o campo vier preenchido.
        var u1 = new ConsumerUnit(Guid.NewGuid(), "U1", "Posto 1", ConsumerUnitMode.Unitary, Caixa);
        var comBloco = u1.ToFields().ToList();
        comBloco[7] = bloco.ToString();
        Assert.Equal(Guid.Empty, ConsumerUnit.Parse(comBloco)!.Substation);
        Assert.Equal(string.Empty, u1.ToFields()[7]);
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void AVersaoDoCabecalhoEscolheOLeitor()
    {
        Assert.Equal(1, RecordTable.VersionOf(["FORMATO", "1", "QUANTIDADE", "0"]));
        Assert.Equal(2, RecordTable.VersionOf(RecordTable.Write(2, 1, ["a"], x => [x])));
        Assert.Null(RecordTable.VersionOf(null));
        Assert.Null(RecordTable.VersionOf(["OUTRO", "1", "QUANTIDADE", "0"]));
        Assert.Null(RecordTable.VersionOf(["FORMATO"]));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ACompartilhadaNasceDentroDoBlocoUnicoDaUsina()
    {
        var setup = new ElectricalSetup();

        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();

        var bloco = Assert.Single(setup.Substations);
        Assert.Equal(bloco.Id, c1.Substation);
        Assert.Equal(bloco.Id, c2.Substation);
        Assert.Equal(["C1", "C2"], setup.UnitsOf(bloco.Id).Select(u => u.Code));
        Assert.Equal("Subestação compartilhada", bloco.Name);
        Assert.Equal(ElectricalDefaults.ConsumerUnitSize, bloco.Size);

        // Pedir a compartilhada de novo devolve a que existe: uma por usina.
        var (mesmo, criou) = setup.EnsureSharedSubstation();
        Assert.False(criou);
        Assert.Equal(bloco.Id, mesmo.Id);
        Assert.Single(setup.Substations);
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void CadaUcDoBlocoTemNomeETrafosProprios()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        var t3 = setup.AddTransformer();

        Assert.Null(setup.EditUnit(c1.Id, "Medição Norte", c1.Size));
        Assert.Null(setup.LinkTransformer(c1.Id, t1.Id));
        Assert.Null(setup.LinkTransformer(c1.Id, t2.Id));
        Assert.Null(setup.LinkTransformer(c2.Id, t3.Id));

        Assert.Equal("Medição Norte", setup.FindUnit(c1.Id)!.Name);
        Assert.Equal(["T1", "T2"], setup.TransformersOf(c1.Id).Select(t => t.Nickname));
        Assert.Equal(["T3"], setup.TransformersOf(c2.Id).Select(t => t.Nickname));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void OBlocoEOEquipamentoEmCampoENaoAUcCompartilhada()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var u1 = setup.AddUnitaryUnits(1)[0];
        var bloco = setup.Substations.Single();

        Assert.Null(setup.EditSubstation(bloco.Id, " Cubículo ", new EquipmentSize(6, 3, 2.8)));

        var noCampo = setup.FindEquipment(EquipmentKind.ConsumerUnit, bloco.Id)!;
        Assert.Equal("Cubículo", noCampo.Tag);
        Assert.Equal(new EquipmentSize(6, 3, 2.8), noCampo.Size);
        Assert.Null(setup.FindEquipment(EquipmentKind.ConsumerUnit, c1.Id));
        Assert.NotNull(setup.FindEquipment(EquipmentKind.ConsumerUnit, u1.Id));

        // O código ou o nome de uma UC do bloco acha o bloco (o comando de campo aceita "C1").
        Assert.Equal(bloco.Id, setup.FindEquipment("c1").Single().Id);
        Assert.Equal(bloco.Id, setup.FindEquipment(" subestação C1 ").Single().Id);
        Assert.Equal(bloco.Id, setup.FindEquipment("cubículo").Single().Id);
        Assert.Equal(u1.Id, setup.FindEquipment("U1").Single().Id);
        Assert.Equal(2, setup.Equipment().Count());
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void EditarOBlocoRecusaNomeVazioEMedidaZero()
    {
        var setup = new ElectricalSetup();
        setup.AddSharedUnit();
        var bloco = setup.Substations.Single();

        Assert.NotNull(setup.EditSubstation(bloco.Id, " ", bloco.Size));
        Assert.NotNull(setup.EditSubstation(bloco.Id, "X", new EquipmentSize(1, 0, 1)));
        Assert.NotNull(setup.EditSubstation(Guid.NewGuid(), "X", bloco.Size));
        Assert.NotNull(setup.EditSubstation(bloco.Id, new string('x', ElectricalDefaults.MaxNameLength + 1), bloco.Size));
        Assert.Equal(bloco, setup.Substations.Single());
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ApagarOBlocoTiraAsUcsDeleESoltaOsTrafosSemApagarOsTrafos()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        var u1 = setup.AddUnitaryUnits(1)[0];
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        var t3 = setup.AddTransformer();
        setup.LinkTransformer(c1.Id, t1.Id);
        setup.LinkTransformer(c2.Id, t2.Id);
        setup.LinkTransformer(u1.Id, t3.Id);
        var bloco = setup.Substations.Single();

        Assert.Equal((2, 2), setup.RemoveSubstation(bloco.Id));
        Assert.Null(setup.RemoveSubstation(bloco.Id));

        Assert.Empty(setup.Substations);
        Assert.Equal([u1.Id], setup.Units.Select(u => u.Id));
        Assert.Equal(3, setup.Transformers.Count);
        Assert.Equal(u1.Id, setup.FindTransformer(t3.Id)!.ConsumerUnit);
        Assert.Equal(Guid.Empty, setup.FindTransformer(t1.Id)!.ConsumerUnit);

        // Depois de apagado, a próxima compartilhada cria um bloco novo (o
        // código segue o maior que existe, a regra de sempre).
        var c3 = setup.AddSharedUnit();
        Assert.Equal("C1", c3.Code);
        Assert.NotEqual(bloco.Id, c3.Substation);
        Assert.Single(setup.Substations);
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ApagarUmaUcDoBlocoDeixaOBloco()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();

        Assert.Equal(0, setup.RemoveUnit(c1.Id));

        Assert.Single(setup.Substations);
        Assert.Empty(setup.UnitsOf(setup.Substations[0].Id));
        Assert.Equal(setup.Substations[0].Id, setup.AddSharedUnit(setup.Substations[0].Id).Substation);
        Assert.Throws<InvalidOperationException>(() => setup.AddSharedUnit(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void DesenhoAntigoPoeAsCompartilhadasNumBlocoNaLeituraComIdentidadeEstavel()
    {
        // Formato 1: C1 e C2 compartilhadas sem bloco, U1 unitária.
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "Medição 1", ConsumerUnitMode.Shared, new EquipmentSize(5, 4, 3));
        var c2 = new ConsumerUnit(Guid.NewGuid(), "C2", "Medição 2", ConsumerUnitMode.Shared, Caixa);
        var u1 = new ConsumerUnit(Guid.NewGuid(), "U1", "Posto", ConsumerUnitMode.Unitary, Caixa);

        var setup = new ElectricalSetup(units: [c1, c2, u1]);

        var bloco = Assert.Single(setup.Substations);
        Assert.Equal(2, setup.MigratedUnits);
        Assert.Equal("Subestação compartilhada", bloco.Name);
        Assert.Equal(c1.Size, bloco.Size);   // o tamanho do primeiro bloquinho antigo
        Assert.Equal(["C1", "C2"], setup.UnitsOf(bloco.Id).Select(u => u.Code));
        Assert.Equal(Guid.Empty, setup.FindUnit(u1.Id)!.Substation);

        // Ler de novo (antes de gravar) dá o mesmo bloco: o "Alocar em campo"
        // do bloco recém-migrado acha o mesmo GUID no comando.
        var deNovo = new ElectricalSetup(units: [c1, c2, u1]);
        Assert.Equal(bloco.Id, deNovo.Substations.Single().Id);

        // Já gravado no formato novo: nada a migrar.
        var gravado = new ElectricalSetup(units: setup.Units, substations: setup.Substations);
        Assert.Equal(0, gravado.MigratedUnits);
        Assert.Equal(bloco, gravado.Substations.Single());
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void UcQueApontaParaBlocoSumidoCaiNoBlocoQueExiste()
    {
        var bloco = new Substation(Guid.NewGuid(), "Cubículo", Caixa);
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "M1", ConsumerUnitMode.Shared, Caixa, bloco.Id);
        var c2 = new ConsumerUnit(Guid.NewGuid(), "C2", "M2", ConsumerUnitMode.Shared, Caixa, Guid.NewGuid());

        var setup = new ElectricalSetup(units: [c1, c2], substations: [bloco]);

        Assert.Equal(1, setup.MigratedUnits);
        Assert.Equal(bloco, setup.Substations.Single());
        Assert.Equal(bloco.Id, setup.FindUnit(c2.Id)!.Substation);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void OResumoMostraOBlocoComAsUcsDentroEAUnitariaComoAntes()
    {
        var bloco = new Substation(Guid.NewGuid(), "Cubículo", Caixa);
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "Medição 1", ConsumerUnitMode.Shared, Caixa, bloco.Id);
        var c2 = new ConsumerUnit(Guid.NewGuid(), "C2", "Medição 2", ConsumerUnitMode.Shared, Caixa, bloco.Id);
        var u1 = new ConsumerUnit(Guid.NewGuid(), "U1", "Posto", ConsumerUnitMode.Unitary, Caixa);
        var t1 = new Transformer(Guid.NewGuid(), "Trafo", "T1", 800, 13800, 2500, 1, 6, "", Caixa, c1.Id);
        var t2 = new Transformer(Guid.NewGuid(), "Trafo", "T2", 800, 13800, 2500, 1, 6, "", Caixa, u1.Id);
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, Caixa);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", t1.Id);
        var i2 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 2", t2.Id);
        var potencia = new Dictionary<Guid, double?>();
        ElectricalString Str(Guid inversor)
        {
            var m = Guid.NewGuid();
            potencia[m] = 500;
            return new ElectricalString(Guid.NewGuid(), Guid.Empty, [m], inversor, "x");
        }

        var r = ElectricalSummary.Build([c1, c2, u1], [t1, t2], [modelo], [i1, i2], [Str(i1.Id), Str(i1.Id), Str(i2.Id)], potencia, null, substations: [bloco]);

        var linhas = r.Rows();
        var b = linhas[0];
        Assert.Equal(SummaryRowKind.Substation, b.Kind);
        Assert.Equal(0, b.Level);
        Assert.Equal("Cubículo", b.Name);
        Assert.Equal(2, b.Strings);
        Assert.Equal((SummaryRowKind.Unit, 1, "C1"), (linhas[1].Kind, linhas[1].Level, linhas[1].Name));
        Assert.Equal((SummaryRowKind.Transformer, 2, "T1"), (linhas[2].Kind, linhas[2].Level, linhas[2].Name));
        Assert.Equal((SummaryRowKind.Inverter, 3, "Inversor 1"), (linhas[3].Kind, linhas[3].Level, linhas[3].Name));
        Assert.Equal((SummaryRowKind.Unit, 1, "C2"), (linhas[4].Kind, linhas[4].Level, linhas[4].Name));
        Assert.Equal((SummaryRowKind.Unit, 0, "U1"), (linhas[5].Kind, linhas[5].Level, linhas[5].Name));
        Assert.Equal((SummaryRowKind.Transformer, 1, "T2"), (linhas[6].Kind, linhas[6].Level, linhas[6].Name));
        Assert.Equal(3, r.UnitCount);
        Assert.Equal(3, r.AllocatedStrings);
        Assert.Equal(["Inversor 1", "Inversor 2"], r.AllInverters.Select(i => i.Inverter.Name));
        Assert.Contains(r.Lines(), l => l.Contains("Cubículo", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void SemBlocoOResumoFicaComoEra()
    {
        var c1 = new ConsumerUnit(Guid.NewGuid(), "C1", "Medição 1", ConsumerUnitMode.Shared, Caixa);
        var r = ElectricalSummary.Build([c1], [], [], [], [], new Dictionary<Guid, double?>(), null);

        Assert.Equal((SummaryRowKind.Unit, 0), (r.Rows()[0].Kind, r.Rows()[0].Level));
    }
}
