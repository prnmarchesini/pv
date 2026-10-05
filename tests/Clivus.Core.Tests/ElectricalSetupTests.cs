namespace Clivus.Core.Tests;

/// <summary>A configuração elétrica (etapas 12 a 14): cadastros, nomes, vínculos e travas.</summary>
public class ElectricalSetupTests
{
    // ------------------------------------------------------------ 13.1

    [Fact]
    [Trait("Etapa", "13")]
    public void TrafoNovoGanhaNomeEApelidoEmSequencia()
    {
        var setup = new ElectricalSetup();

        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();

        Assert.Equal(["T1", "T2"], setup.Transformers.Select(t => t.Nickname));
        Assert.Equal(["Trafo 1", "Trafo 2"], setup.Transformers.Select(t => t.Name));
        Assert.NotEqual(t1.Id, t2.Id);
        Assert.True(t1.IsValid);
        Assert.Equal(Guid.Empty, t1.ConsumerUnit);
        Assert.Equal(0, t1.PowerKva);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void ApelidoQueSaiuNaoVoltaEApelidoDoUsuarioNaoConta()
    {
        var setup = new ElectricalSetup();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        setup.AddTransformer();

        Assert.Equal(0, setup.RemoveTransformer(t2.Id));
        Assert.Null(setup.EditTransformer(setup.FindTransformer(t1.Id)! with { Nickname = "Skid Norte" }));

        Assert.Equal("T4", setup.AddTransformer().Nickname);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void TrafoDoPadraoVemComOsCamposDele()
    {
        var padrao = ElectricalDefaults.TransformerTemplates.Single(p => p.PowerKva == 2500 && p.OutputVoltage == 13800);
        var setup = new ElectricalSetup();

        var t = setup.AddTransformer(padrao);

        Assert.Equal(800, t.InputVoltage);
        Assert.Equal(13800, t.OutputVoltage);
        Assert.Equal(2500, t.PowerKva);
        Assert.Equal(6.0, t.ImpedancePercent);
        Assert.Equal(padrao.Size, t.Size);
        Assert.Equal("T1", t.Nickname);
        Assert.Equal(t, Transformer.Parse(t.ToFields()));
        Assert.Equal("2.500 kVA — 800 V / 13.800 V — Z 6,0 %", padrao.Describe());
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void OsPadroesSaoTodosValidos()
    {
        Assert.NotEmpty(ElectricalDefaults.TransformerTemplates);
        Assert.All(ElectricalDefaults.TransformerTemplates, p =>
        {
            Assert.True(p.Size.IsValid);
            Assert.True(p.InputVoltage > 0 && p.OutputVoltage > p.InputVoltage && p.PowerKva > 0 && p.ImpedancePercent > 0);
        });
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void EditarRecusaApelidoVazioRepetidoNumeroNegativoEMedidaZero()
    {
        var setup = new ElectricalSetup();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();

        Assert.NotNull(setup.EditTransformer(t2 with { Nickname = "  " }));
        Assert.NotNull(setup.EditTransformer(t2 with { Nickname = "t1" }));
        Assert.NotNull(setup.EditTransformer(t2 with { PowerKva = -1 }));
        Assert.NotNull(setup.EditTransformer(t2 with { ImpedancePercent = double.NaN }));
        Assert.NotNull(setup.EditTransformer(t2 with { Size = new EquipmentSize(0, 1, 1) }));
        Assert.NotNull(setup.EditTransformer(t2 with { Name = new string('x', 101) }));
        Assert.NotNull(setup.EditTransformer(t2 with { Id = Guid.NewGuid() }));
        Assert.Equal(t2, setup.FindTransformer(t2.Id));

        Assert.Null(setup.EditTransformer(t1 with { Nickname = " TR-A ", Name = "Seco 13,8", PowerKva = 2500, KFactor = 4, Notes = "para-raios 12 kV" }));
        var editado = setup.FindTransformer(t1.Id)!;
        Assert.Equal("TR-A", editado.Nickname);
        Assert.Equal(2500, editado.PowerKva);
        Assert.Equal("para-raios 12 kV", editado.Notes);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void EditarNaoMexeNaUcDoTrafo()
    {
        var uc = Guid.NewGuid();
        var t = new Transformer(Guid.NewGuid(), "Trafo 1", "T1", 800, 13800, 2500, 1, 6, "", ElectricalDefaults.TransformerSize, uc);
        var setup = new ElectricalSetup([t]);

        Assert.Null(setup.EditTransformer(t with { ConsumerUnit = Guid.Empty, PowerKva = 3150 }));

        Assert.Equal(uc, setup.FindTransformer(t.Id)!.ConsumerUnit);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void ApagarTrafoSoltaOsInversoresDoSkidSemApagarOsInversores()
    {
        var setup = new ElectricalSetup();
        var t = setup.AddTransformer();
        var modelo = Guid.NewGuid();
        var comSkid = new Inverter(Guid.NewGuid(), modelo, "Inversor 1", t.Id);
        var semSkid = new Inverter(Guid.NewGuid(), modelo, "Inversor 2", Guid.Empty);
        setup = new ElectricalSetup(setup.Transformers, [comSkid, semSkid]);

        Assert.Equal(1, setup.RemoveTransformer(t.Id));
        Assert.Null(setup.RemoveTransformer(t.Id));

        Assert.Empty(setup.Transformers);
        Assert.Equal(2, setup.Inverters.Count);
        Assert.All(setup.Inverters, i => Assert.Equal(Guid.Empty, i.Transformer));
    }

    // ------------------------------------------------------------ 12.1

    [Fact]
    [Trait("Etapa", "12")]
    public void SubestacaoCompartilhadaGanhaCodigoEmSequencia()
    {
        var setup = new ElectricalSetup();

        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        setup.AddSharedUnit();
        setup.RemoveUnit(c2.Id);
        var c4 = setup.AddSharedUnit();

        Assert.Equal("C1", c1.Code);
        Assert.Equal("Subestação C1", c1.Name);
        Assert.Equal(ConsumerUnitMode.Shared, c1.Mode);
        Assert.Equal("C4", c4.Code);   // o C2 saiu e não volta
        Assert.True(c1.IsValid);
        Assert.Equal(c1, ConsumerUnit.Parse(c1.ToFields()));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void UmaSubestacaoCompartilhadaRecebeUmOuMaisTrafos()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        var t3 = setup.AddTransformer();

        Assert.Null(setup.LinkTransformer(c1.Id, t1.Id));
        Assert.Null(setup.LinkTransformer(c1.Id, t2.Id));
        Assert.Null(setup.LinkTransformer(c2.Id, t3.Id));
        Assert.Null(setup.LinkTransformer(c1.Id, t1.Id));   // já era dela: nada muda

        Assert.Equal(["T1", "T2"], setup.TransformersOf(c1.Id).Select(t => t.Nickname));
        Assert.Equal(["T3"], setup.TransformersOf(c2.Id).Select(t => t.Nickname));
        Assert.Equal(c1.Id, setup.FindTransformer(t1.Id)!.ConsumerUnit);
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void TrafoDeUmaSubestacaoFicaTravadoAteSerSolto()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        var t1 = setup.AddTransformer();
        setup.LinkTransformer(c1.Id, t1.Id);

        var porque = setup.LinkTransformer(c2.Id, t1.Id);

        Assert.NotNull(porque);
        Assert.Contains("C1", porque);
        Assert.Equal(c1.Id, setup.FindTransformer(t1.Id)!.ConsumerUnit);

        Assert.True(setup.UnlinkTransformer(t1.Id));
        Assert.False(setup.UnlinkTransformer(t1.Id));
        Assert.Null(setup.LinkTransformer(c2.Id, t1.Id));
        Assert.Equal(c2.Id, setup.FindTransformer(t1.Id)!.ConsumerUnit);
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void VinculoComSubestacaoQueNaoExisteMaisNaoTrava()
    {
        var t = new Transformer(Guid.NewGuid(), "Trafo 1", "T1", 0, 0, 0, 0, 0, "", ElectricalDefaults.TransformerSize, Guid.NewGuid());
        var setup = new ElectricalSetup([t]);
        var c1 = setup.AddSharedUnit();

        Assert.Null(setup.LinkTransformer(c1.Id, t.Id));
        Assert.NotNull(setup.LinkTransformer(Guid.NewGuid(), t.Id));
        Assert.NotNull(setup.LinkTransformer(c1.Id, Guid.NewGuid()));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ApagarSubestacaoSoltaOsTrafosSemApagarOsTrafos()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        setup.LinkTransformer(c1.Id, t1.Id);
        setup.LinkTransformer(c1.Id, t2.Id);

        Assert.Equal(2, setup.RemoveUnit(c1.Id));
        Assert.Null(setup.RemoveUnit(c1.Id));

        Assert.Equal(2, setup.Transformers.Count);
        Assert.All(setup.Transformers, t => Assert.Equal(Guid.Empty, t.ConsumerUnit));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void EditarSubestacaoTrocaNomeETamanhoENaoOCodigo()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();

        Assert.Null(setup.EditUnit(c1.Id, " Medição Norte ", new EquipmentSize(6, 3, 2.8)));
        Assert.NotNull(setup.EditUnit(c1.Id, " ", new EquipmentSize(6, 3, 2.8)));
        Assert.NotNull(setup.EditUnit(c1.Id, "X", new EquipmentSize(6, 0, 2.8)));
        Assert.NotNull(setup.EditUnit(Guid.NewGuid(), "X", new EquipmentSize(6, 3, 2.8)));

        var editada = setup.FindUnit(c1.Id)!;
        Assert.Equal("Medição Norte", editada.Name);
        Assert.Equal("C1", editada.Code);
        Assert.Equal(6, editada.Size.Width);
    }

    // ------------------------------------------------------------ 12.2

    [Fact]
    [Trait("Etapa", "12")]
    public void UnitariasSaoBloquinhosIndependentesEmSequencia()
    {
        var setup = new ElectricalSetup();
        setup.AddSharedUnit();

        var unitarias = setup.AddUnitaryUnits(3);
        var mais = setup.AddUnitaryUnits(1);

        Assert.Equal(["U1", "U2", "U3"], unitarias.Select(u => u.Code));
        Assert.Equal("U4", mais.Single().Code);
        Assert.All(unitarias, u => Assert.Equal(ConsumerUnitMode.Unitary, u.Mode));
        Assert.Equal(3, unitarias.Select(u => u.Id).Distinct().Count());
        Assert.Equal("C2", setup.AddSharedUnit().Code);   // a compartilhada tem a sequência dela
        Assert.Equal(6, setup.Units.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => setup.AddUnitaryUnits(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => setup.AddUnitaryUnits(ElectricalDefaults.MaxAtOnce + 1));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void CadaUnitariaTemUmTrafoSo()
    {
        var setup = new ElectricalSetup();
        var u = setup.AddUnitaryUnits(2);
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();

        Assert.Null(setup.LinkTransformer(u[0].Id, t1.Id));
        var porque = setup.LinkTransformer(u[0].Id, t2.Id);
        Assert.NotNull(porque);
        Assert.Contains("T1", porque);
        Assert.Null(setup.LinkTransformer(u[1].Id, t2.Id));
        Assert.Null(setup.LinkTransformer(u[0].Id, t1.Id));   // o mesmo de novo não é segundo trafo

        Assert.Equal(["T1"], setup.TransformersOf(u[0].Id).Select(t => t.Nickname));
        Assert.Equal(["T2"], setup.TransformersOf(u[1].Id).Select(t => t.Nickname));

        setup.UnlinkTransformer(t1.Id);
        Assert.NotNull(setup.LinkTransformer(u[0].Id, t2.Id));   // T2 é da U2: travado
        Assert.Null(setup.LinkTransformer(u[0].Id, setup.AddTransformer().Id));
    }

    // ------------------------------------------------------------ 12.3

    [Fact]
    [Trait("Etapa", "12")]
    public void OEquipamentoTemATagEADimensaoDoCadastro()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var u1 = setup.AddUnitaryUnits(1)[0];
        var t1 = setup.AddTransformer();
        setup.EditUnit(c1.Id, "Medição Norte", new EquipmentSize(6, 3, 2.8));

        var uc = setup.FindEquipment(EquipmentKind.ConsumerUnit, c1.Id)!;
        Assert.Equal("Medição Norte", uc.Tag);
        Assert.Equal(new EquipmentSize(6, 3, 2.8), uc.Size);
        Assert.Equal("Subestação U1", setup.FindEquipment(EquipmentKind.ConsumerUnit, u1.Id)!.Tag);
        Assert.Equal("T1", setup.FindEquipment(EquipmentKind.Transformer, t1.Id)!.Tag);
        Assert.Null(setup.FindEquipment(EquipmentKind.Transformer, c1.Id));
        Assert.Null(setup.FindEquipment(EquipmentKind.ConsumerUnit, Guid.NewGuid()));
        Assert.Equal(3, setup.Equipment().Count());
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void OEquipamentoEAchadoPeloGuidPelaTagOuPeloCodigo()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var t1 = setup.AddTransformer();

        Assert.Equal(c1.Id, setup.FindEquipment("c1").Single().Id);
        Assert.Equal(c1.Id, setup.FindEquipment(" subestação C1 ").Single().Id);
        Assert.Equal(t1.Id, setup.FindEquipment("t1").Single().Id);
        Assert.Equal(t1.Id, setup.FindEquipment(t1.Id.ToString()).Single().Id);
        Assert.Empty(setup.FindEquipment("T9"));
        Assert.Empty(setup.FindEquipment(""));

        // Tag repetida entre tipos: os dois voltam, quem chama recusa.
        setup.EditUnit(c1.Id, "T1", c1.Size);
        Assert.Equal(2, setup.FindEquipment("T1").Count);
    }

    // ------------------------------------------------------------ 13.2

    [Fact]
    [Trait("Etapa", "13")]
    public void ATagDoTrafoEmCampoEOApelidoEditavel()
    {
        var setup = new ElectricalSetup();
        var t1 = setup.AddTransformer(ElectricalDefaults.TransformerTemplates[2]);

        var antes = setup.FindEquipment(EquipmentKind.Transformer, t1.Id)!;
        Assert.Equal("T1", antes.Tag);
        Assert.Equal(ElectricalDefaults.TransformerTemplates[2].Size, antes.Size);

        Assert.Null(setup.EditTransformer(t1 with { Nickname = "TR-A", Size = new EquipmentSize(4, 3, 2.6) }));

        var depois = setup.FindEquipment(EquipmentKind.Transformer, t1.Id)!;
        Assert.Equal("TR-A", depois.Tag);
        Assert.Equal(new EquipmentSize(4, 3, 2.6), depois.Size);
        Assert.Equal(t1.Id, setup.FindEquipment("tr-a").Single().Id);
        Assert.Empty(setup.FindEquipment("T1"));
    }

    // ------------------------------------------------------------ 14.1

    [Fact]
    [Trait("Etapa", "14")]
    public void ModeloNovoEGenericoEOTotalDeEntradasEDerivado()
    {
        var setup = new ElectricalSetup();

        var generico = setup.AddModel();
        var huawei = setup.AddModel();
        Assert.Null(setup.EditModel(huawei with { Name = "Huawei 250", Mppts = 5, InputsPerMppt = 4 }));

        Assert.Equal("Modelo de inversor 1", generico.Name);
        Assert.Equal(1, generico.TotalInputs);
        Assert.True(generico.IsValid);
        var lido = setup.FindModel(huawei.Id)!;
        Assert.Equal("Huawei 250", lido.Name);
        Assert.Equal(20, lido.TotalInputs);
        Assert.Equal(lido, InverterModel.Parse(lido.ToFields()));
        Assert.Equal("Modelo de inversor 2", setup.AddModel().Name);   // "Huawei 250" não entra na sequência
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ModeloRecusaNomeRepetidoZeroEntradaEMedidaZero()
    {
        var setup = new ElectricalSetup();
        var a = setup.AddModel();
        var b = setup.AddModel();
        setup.EditModel(a with { Name = "Huawei 250" });

        Assert.NotNull(setup.EditModel(b with { Name = "huawei 250" }));
        Assert.NotNull(setup.EditModel(b with { Name = " " }));
        Assert.NotNull(setup.EditModel(b with { Mppts = 0 }));
        Assert.NotNull(setup.EditModel(b with { InputsPerMppt = -1 }));
        Assert.NotNull(setup.EditModel(b with { Mppts = ElectricalDefaults.MaxMppts + 1 }));
        Assert.NotNull(setup.EditModel(b with { Size = new EquipmentSize(1, 1, 0) }));
        Assert.NotNull(setup.EditModel(b with { Id = Guid.NewGuid() }));
        Assert.Equal(b, setup.FindModel(b.Id));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ModeloComInversorNaoSaiDoCadastro()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "Huawei 250", 5, 4, ElectricalDefaults.InverterSize);
        var livre = new InverterModel(Guid.NewGuid(), "Sungrow", 6, 2, ElectricalDefaults.InverterSize);
        var inversor = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", Guid.Empty);
        var setup = new ElectricalSetup(inverters: [inversor], models: [modelo, livre]);

        Assert.NotNull(setup.RemoveModel(modelo.Id));
        Assert.Null(setup.RemoveModel(livre.Id));
        Assert.NotNull(setup.RemoveModel(livre.Id));
        Assert.Equal(["Huawei 250"], setup.Models.Select(m => m.Name));
    }

    // ------------------------------------------------------------ 14.2

    [Fact]
    [Trait("Etapa", "14")]
    public void InversoresDeVariosModelosComNomesContinuando()
    {
        var setup = new ElectricalSetup();
        var huawei = setup.AddModel();
        var sungrow = setup.AddModel();

        var quatro = setup.AddInverters(huawei.Id, 4);
        var dois = setup.AddInverters(sungrow.Id, 2);

        Assert.Equal(["Inversor 1", "Inversor 2", "Inversor 3", "Inversor 4"], quatro.Select(i => i.Name));
        Assert.Equal(["Inversor 5", "Inversor 6"], dois.Select(i => i.Name));
        Assert.All(quatro, i => Assert.Equal(huawei.Id, i.Model));
        Assert.All(dois, i => Assert.Equal(sungrow.Id, i.Model));
        Assert.All(setup.Inverters, i => Assert.True(i.IsValid));
        Assert.All(setup.Inverters, i => Assert.Equal(Guid.Empty, i.Transformer));
        Assert.Equal(6, setup.Inverters.Select(i => i.Id).Distinct().Count());
        Assert.Equal(setup.Inverters[0], Inverter.Parse(setup.Inverters[0].ToFields()));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void InversorPedeModeloQueExisteEQuantidadeValida()
    {
        var setup = new ElectricalSetup();
        var m = setup.AddModel();

        Assert.Throws<InvalidOperationException>(() => setup.AddInverters(Guid.NewGuid(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => setup.AddInverters(m.Id, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => setup.AddInverters(m.Id, ElectricalDefaults.MaxAtOnce + 1));
        Assert.Empty(setup.Inverters);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void CadaInversorContaAsStringsQueApontamParaEle()
    {
        var i1 = Guid.NewGuid();
        var i2 = Guid.NewGuid();
        ElectricalString S(Guid inversor) => new(Guid.NewGuid(), Guid.Empty, [Guid.NewGuid()], inversor, "");

        var contagem = StringAllocation.CountByInverter([S(i1), S(i1), S(i2), S(Guid.Empty)]);

        Assert.Equal(2, contagem[i1]);
        Assert.Equal(1, contagem[i2]);
        Assert.False(contagem.ContainsKey(Guid.Empty));
        Assert.Empty(StringAllocation.CountByInverter([]));
    }
}
