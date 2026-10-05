namespace Clivus.Core.Tests;

/// <summary>
/// O formulário do trafo (reprovação de 05/10/2026: "não mostra a potência
/// depois de salvo"). O texto da caixa vira número pelo MESMO caminho na
/// janela e no nível 2; o que a janela mostra volta igual pela leitura; e o
/// vínculo com a UC sai do formulário com a mesma trava da aba Subestação.
/// </summary>
public class TransformerFormTests
{
    private static TransformerFormTexts Textos(
        string entrada = "800", string saida = "13.800", string kva = "2500", string k = "1", string z = "6,5",
        string nome = "Trafo seco", string apelido = "T1", string notas = "", string w = "3", string l = "2,5", string h = "2,5") =>
        new(nome, apelido, entrada, saida, kva, k, z, notas, w, l, h);

    private static Transformer Branco() =>
        new(Guid.NewGuid(), "Trafo 1", "T1", 0, 0, 0, 0, 0, string.Empty, ElectricalDefaults.TransformerSize, Guid.Empty);

    [Theory]
    [Trait("Etapa", "13")]
    [InlineData("2500", 2500)]
    [InlineData("2.500", 2500)]
    [InlineData("2.500,5", 2500.5)]
    [InlineData("2500 kVA", 2500)]
    [InlineData("2,5 MVA", 2500)]
    [InlineData("1.250 MVA", 1250)]
    [InlineData("1.25 MVA", 1250)]
    [InlineData("", 0)]
    public void APotenciaDigitadaEhLidaNaCulturaBrasileira(string texto, double esperado)
    {
        var t = TransformerForm.Read(Branco(), Textos(kva: texto), out var problema);

        Assert.Null(problema);
        Assert.Equal(esperado, t!.PowerKva, 6);
    }

    [Theory]
    [Trait("Etapa", "13")]
    [InlineData("13.800", 13800)]
    [InlineData("13800", 13800)]
    [InlineData("800", 800)]
    [InlineData("13,8 kV", 13800)]
    [InlineData("34.5kV", 34500)]
    [InlineData("800 V", 800)]
    [InlineData("0.380 kV", 380)]
    [InlineData("13.8 kV", 13800)]
    public void ATensaoDigitadaEhLidaComPontoDeMilharEKv(string texto, double esperado)
    {
        var t = TransformerForm.Read(Branco(), Textos(saida: texto), out var problema);

        Assert.Null(problema);
        Assert.Equal(esperado, t!.OutputVoltage, 6);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void OsCincoNumerosSaemDoFormulario()
    {
        var t = TransformerForm.Read(Branco(), Textos(entrada: "800", saida: "13.800", kva: "2500", k: "4", z: "6,5"), out var problema)!;

        Assert.Null(problema);
        Assert.Equal(800, t.InputVoltage);
        Assert.Equal(13800, t.OutputVoltage);
        Assert.Equal(2500, t.PowerKva);
        Assert.Equal(4, t.KFactor);
        Assert.Equal(6.5, t.ImpedancePercent);
        Assert.Equal(new EquipmentSize(3, 2.5, 2.5), t.Size);
        Assert.Equal("Trafo seco", t.Name);
    }

    [Theory]
    [Trait("Etapa", "13")]
    [InlineData("abc", "kva", "potência")]
    [InlineData("1.2.3", "saida", "tensão de saída")]
    [InlineData("x", "z", "impedância")]
    [InlineData("0", "w", "largura")]
    public void NumeroQueNaoDaParaLerDizQualCampo(string texto, string campo, string nomeDoCampo)
    {
        var textos = campo switch
        {
            "kva" => Textos(kva: texto),
            "saida" => Textos(saida: texto),
            "z" => Textos(z: texto),
            _ => Textos(w: texto),
        };

        var t = TransformerForm.Read(Branco(), textos, out var problema);

        Assert.Null(t);
        Assert.NotNull(problema);
        Assert.Contains(nomeDoCampo, problema, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Etapa", "13")]
    [InlineData("pt")]
    [InlineData("en")]
    [InlineData("es")]
    public void OQueAJanelaMostraVoltaIgualPelaLeitura(string idioma)
    {
        var original = new Transformer(Guid.NewGuid(), "Seco", "T7", 800, 13800, 2500, 4.25, 5.875, "obs", new EquipmentSize(3.215, 2.6, 2.7), Guid.Empty);

        using (Tr.Use(Tr.Resolve(idioma, null)))
        {
            var textos = TransformerForm.Texts(original);
            var relido = TransformerForm.Read(original, textos, out var problema);

            Assert.Null(problema);
            Assert.Equal(original, relido);
        }
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void TrafoEmBrancoEditadoPeloFormularioGravaERelePelosCampos()
    {
        var setup = new ElectricalSetup();
        var t = setup.AddTransformer();

        var editado = TransformerForm.Read(t, Textos(apelido: "T1"), out var problema)!;
        Assert.Null(problema);
        Assert.Null(setup.SaveTransformer(editado, Guid.Empty));

        // O que vai para o desenho e volta (ToFields/Parse) mantém os números.
        var relido = Transformer.Parse(setup.FindTransformer(t.Id)!.ToFields())!;
        Assert.Equal(2500, relido.PowerKva);
        Assert.Equal(13800, relido.OutputVoltage);
        Assert.Equal(800, relido.InputVoltage);
        Assert.Equal(6.5, relido.ImpedancePercent);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void TrafoDoPadraoMostraOsNumerosDoPadraoNoFormulario()
    {
        var setup = new ElectricalSetup();
        var t = setup.AddTransformer(ElectricalDefaults.TransformerTemplates[1]);

        using (Tr.Use(UiLanguage.Portuguese))
        {
            var textos = TransformerForm.Texts(t);
            Assert.Equal("2500", textos.PowerKva);
            Assert.Equal("13800", textos.OutputVoltage);
            Assert.Equal("800", textos.InputVoltage);
            Assert.Equal("6", textos.ImpedancePercent);
        }
    }

    // ------------------------------------------- a UC escolhida no trafo

    [Fact]
    [Trait("Etapa", "13")]
    public void OFormularioDoTrafoLigaESoltaAUc()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var t = setup.AddTransformer();

        Assert.Null(setup.SaveTransformer(t with { PowerKva = 2500 }, c1.Id));
        Assert.Equal(c1.Id, setup.FindTransformer(t.Id)!.ConsumerUnit);
        Assert.Equal(2500, setup.FindTransformer(t.Id)!.PowerKva);

        Assert.Null(setup.SaveTransformer(setup.FindTransformer(t.Id)!, Guid.Empty));
        Assert.Equal(Guid.Empty, setup.FindTransformer(t.Id)!.ConsumerUnit);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void TrafoDeOutraUcFicaTravadoTambemPeloFormularioENadaEGravado()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var c2 = setup.AddSharedUnit();
        var t = setup.AddTransformer();
        setup.LinkTransformer(c1.Id, t.Id);

        var porque = setup.SaveTransformer(setup.FindTransformer(t.Id)! with { PowerKva = 999 }, c2.Id);

        Assert.NotNull(porque);
        Assert.Contains("C1", porque);
        Assert.Equal(c1.Id, setup.FindTransformer(t.Id)!.ConsumerUnit);
        Assert.Equal(0, setup.FindTransformer(t.Id)!.PowerKva);   // tudo ou nada
        Assert.Equal(c1.Id, setup.UnitChoices(t.Id).Single(o => o.Unit.Id == c1.Id).Unit.Id);
        Assert.False(setup.UnitChoices(t.Id).Single(o => o.Unit.Id == c2.Id).Allowed);
        Assert.True(setup.UnitChoices(t.Id).Single(o => o.Unit.Id == c1.Id).Allowed);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void UnitariaCheiaNaoAceitaOutroTrafoPeloFormulario()
    {
        var setup = new ElectricalSetup();
        var u1 = setup.AddUnitaryUnits(1)[0];
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        Assert.Null(setup.SaveTransformer(t1, u1.Id));

        var porque = setup.SaveTransformer(t2, u1.Id);

        Assert.NotNull(porque);
        Assert.Contains("T1", porque);
        Assert.Equal(Guid.Empty, setup.FindTransformer(t2.Id)!.ConsumerUnit);
        Assert.False(setup.UnitChoices(t2.Id).Single().Allowed);
        Assert.True(setup.UnitChoices(t1.Id).Single().Allowed);
    }

    [Fact]
    [Trait("Etapa", "13")]
    public void EdicaoRecusadaNaoMexeNoVinculo()
    {
        var setup = new ElectricalSetup();
        var c1 = setup.AddSharedUnit();
        var t = setup.AddTransformer();

        Assert.NotNull(setup.SaveTransformer(t with { Nickname = " " }, c1.Id));
        Assert.Equal(Guid.Empty, setup.FindTransformer(t.Id)!.ConsumerUnit);
    }
}
