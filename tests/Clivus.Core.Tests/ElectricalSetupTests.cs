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
}
