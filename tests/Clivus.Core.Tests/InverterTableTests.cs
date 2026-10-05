namespace Clivus.Core.Tests;

/// <summary>
/// A aba Inversor de 05/10/2026 (pedidos do Renan): a potência do modelo
/// ("falta o campo para inserir a potência"), o trafo de cada inversor direto
/// na tabela, um ou vários de uma vez ("esse inversor, esse e esse é deste
/// trafo"), e as colunas de strings, kWp, kW e CC/CA.
/// </summary>
public class InverterTableTests
{
    private static readonly EquipmentSize Caixa = ElectricalDefaults.InverterSize;

    // ------------------------------------------------------- a potência

    [Fact]
    [Trait("Etapa", "14")]
    public void OFormato3VaiEVoltaComAPotencia()
    {
        var m = new InverterModel(Guid.NewGuid(), "Huawei 330", [4, 4, 4, 5, 5], Caixa, 300.5);
        var campos = m.ToFields();

        Assert.Equal(8, InverterModel.FieldCount);
        Assert.Equal(InverterModel.FieldCount, campos.Count);
        Assert.Equal("300.5", campos[7]);
        Assert.Equal(m, InverterModel.Parse(campos));
        Assert.NotEqual(m, m with { PowerKw = 300 });
        Assert.True(m.HasPower);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void PotenciaNaoInformadaVaiVaziaEVoltaZero()
    {
        var m = new InverterModel(Guid.NewGuid(), "Generico", 1, 2, Caixa);

        Assert.Equal(0, m.PowerKw);
        Assert.False(m.HasPower);
        Assert.Equal(string.Empty, m.ToFields()[7]);
        Assert.Equal(m, InverterModel.Parse(m.ToFields()));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OFormato2SemPotenciaContinuaSendoLidoComZero()
    {
        var id = Guid.NewGuid();
        string[] formato2 = [id.ToString("D"), "Huawei 330", "5", "4;4;4;5;5", "1.1", "0.7", "0.6"];

        Assert.Equal(InverterModel.LegacyFieldCount, formato2.Length);
        var lido = InverterModel.Parse(formato2);

        Assert.NotNull(lido);
        Assert.Equal(0, lido!.PowerKw);
        Assert.Equal(22, lido.TotalInputs);

        // O formato 1 também fica com a potência 0.
        Assert.Equal(0, InverterModel.ParseLegacy([id.ToString("D"), "Antigo", "5", "4", "1.1", "0.7", "0.6"])!.PowerKw);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void PotenciaMuitoPequenaVoltaSemNotacaoCientifica()
    {
        var m = new InverterModel(Guid.NewGuid(), "Micro", 1, 1, Caixa, 0.00001);

        Assert.DoesNotContain("E", m.ToFields()[7], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(m, InverterModel.Parse(m.ToFields()));
        Assert.Equal(0.00001, InverterModel.Parse([.. m.ToFields().Take(7), "1E-05"])!.PowerKw);
    }

    [Theory]
    [Trait("Etapa", "14")]
    [InlineData("x")]
    [InlineData("-5")]
    [InlineData("1e9")]
    [InlineData("200000")]
    public void PotenciaEstragadaNaoVoltaDoDesenho(string potencia)
    {
        Assert.Null(InverterModel.Parse([Guid.NewGuid().ToString("D"), "X", "1", "2", "1", "1", "1", potencia]));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OCadastroGravaAPotenciaERecusaAInvalida()
    {
        var setup = new ElectricalSetup();
        var m = setup.AddModel();

        Assert.Null(setup.EditModel(m with { PowerKw = 250 }));
        Assert.Equal(250, setup.FindModel(m.Id)!.PowerKw);

        Assert.NotNull(setup.EditModel(m with { PowerKw = -1 }));
        Assert.NotNull(setup.EditModel(m with { PowerKw = double.NaN }));
        Assert.NotNull(setup.EditModel(m with { PowerKw = ElectricalDefaults.MaxInverterPowerKw + 1 }));
        Assert.Equal(250, setup.FindModel(m.Id)!.PowerKw);

        Assert.Null(setup.EditModel(m with { PowerKw = 0 }));
        Assert.False(setup.FindModel(m.Id)!.HasPower);
    }

    // ------------------------------------------------- o trafo na tabela

    private static (ElectricalSetup Setup, Transformer T1, Transformer T2, IReadOnlyList<Inverter> Inversores) Usina()
    {
        var setup = new ElectricalSetup();
        var t1 = setup.AddTransformer();
        var t2 = setup.AddTransformer();
        var m = setup.AddModel();
        return (setup, t1, t2, setup.AddInverters(m.Id, 6));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void VariosDeUmaVezNoTrafo()
    {
        var (setup, t1, t2, inv) = Usina();

        var a = setup.SetTransformer(inv.Take(3).Select(i => i.Id), t1.Id);
        var b = setup.SetTransformer(inv.Skip(3).Select(i => i.Id), t2.Id);

        Assert.Equal(3, a.Changed);
        Assert.Equal(3, b.Changed);
        Assert.Null(a.Problem);
        Assert.Equal(["Inversor 1", "Inversor 2", "Inversor 3"], setup.InvertersOf(t1.Id).Select(i => i.Name));
        Assert.Equal(["Inversor 4", "Inversor 5", "Inversor 6"], setup.InvertersOf(t2.Id).Select(i => i.Name));

        // De novo no mesmo trafo: nada muda, conta como já dele.
        var denovo = setup.SetTransformer([inv[0].Id, inv[0].Id], t1.Id);
        Assert.Equal(0, denovo.Changed);
        Assert.Equal(1, denovo.AlreadyThere);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void NaTabelaOInversorDeOutroTrafoMudaSemTrava()
    {
        var (setup, t1, t2, inv) = Usina();
        setup.Group(t1.Id, "Skid Norte", inv.Take(2).Select(i => i.Id));

        // A seleção em campo trava; a escolha na tabela é ato explícito e muda.
        Assert.Equal(2, setup.Group(t2.Id, "", inv.Take(2).Select(i => i.Id)).Refused.Count);
        var r = setup.SetTransformer([inv[0].Id], t2.Id);

        Assert.Equal(1, r.Changed);
        Assert.Equal(t2.Id, setup.FindInverter(inv[0].Id)!.Transformer);
        Assert.Equal("Skid Norte", setup.FindSkid(t1.Id)!.Name);   // o T1 ainda tem o Inversor 2

        // O último sai do T1: o skid dele deixa de existir, como no Tirar do skid.
        setup.SetTransformer([inv[1].Id], t2.Id);
        Assert.Null(setup.FindSkid(t1.Id));
        Assert.Equal(2, setup.InvertersOf(t2.Id).Count);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemTrafoSoltaENomeDoSkidDeDestinoFica()
    {
        var (setup, t1, t2, inv) = Usina();
        setup.Group(t2.Id, "Skid Sul", [inv[5].Id]);
        setup.SetTransformer([inv[0].Id, inv[1].Id], t1.Id);

        Assert.Equal(1, setup.SetTransformer([inv[0].Id], t2.Id).Changed);
        Assert.Equal("Skid Sul", setup.FindSkid(t2.Id)!.Name);

        var r = setup.SetTransformer([inv[0].Id, inv[1].Id, Guid.NewGuid()], Guid.Empty);
        Assert.Equal(2, r.Changed);
        Assert.Equal(1, r.Missing);
        Assert.Equal(Guid.Empty, setup.FindInverter(inv[0].Id)!.Transformer);
        Assert.Empty(setup.InvertersOf(t1.Id));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void TrafoQueNaoExisteNaoMudaNada()
    {
        var (setup, t1, _, inv) = Usina();
        setup.SetTransformer([inv[0].Id], t1.Id);

        var r = setup.SetTransformer([inv[0].Id], Guid.NewGuid());

        Assert.NotNull(r.Problem);
        Assert.Equal(0, r.Changed);
        Assert.Equal(t1.Id, setup.FindInverter(inv[0].Id)!.Transformer);
    }

    // ---------------------------------------------- strings, kWp, kW, CC/CA

    [Fact]
    [Trait("Etapa", "14")]
    public void OKwpDeCadaInversorEODoResumoEletrico()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M 100", 2, 2, Caixa, 100);
        var semPotencia = new InverterModel(Guid.NewGuid(), "M sem", 2, 2, Caixa);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", Guid.Empty);
        var i2 = new Inverter(Guid.NewGuid(), semPotencia.Id, "Inversor 2", Guid.Empty);
        var i3 = new Inverter(Guid.NewGuid(), Guid.NewGuid(), "Inversor 3", Guid.Empty);   // modelo apagado

        var potencia = new Dictionary<Guid, double?>();
        var strings = new List<ElectricalString>();
        void Str(Guid inversor, int modulos)
        {
            var ids = Enumerable.Range(0, modulos).Select(_ => Guid.NewGuid()).ToList();
            foreach (var m in ids) potencia[m] = 550;
            strings.Add(new ElectricalString(Guid.NewGuid(), Guid.Empty, ids, inversor, ""));
        }

        // Inversor 1: 5 strings de 26 módulos de 550 W = 71,5 kWp (acima das 4 entradas).
        for (var k = 0; k < 5; k++) Str(i1.Id, 26);
        Str(i2.Id, 20);
        Str(Guid.Empty, 26);   // livre: não entra em inversor nenhum

        Inverter[] inversores = [i1, i2, i3];
        var resumo = ElectricalSummary.Build([], [], [modelo, semPotencia], inversores, strings, potencia, null);
        var contagem = StringAllocation.CountByInverter(strings);

        var linhas = InverterTable.Rows(inversores, [modelo, semPotencia], contagem, resumo.AllInverters);

        Assert.Equal(["Inversor 1", "Inversor 2", "Inversor 3"], linhas.Select(l => l.Inverter.Name));
        Assert.Equal(5, linhas[0].Strings);
        Assert.Equal(4, linhas[0].Capacity);
        Assert.True(linhas[0].OverCapacity);
        Assert.Equal(71.5, linhas[0].PowerKwp!.Value, 6);
        Assert.Equal(100, linhas[0].PowerKw);
        Assert.Equal(0.715, linhas[0].DcAcRatio!.Value, 6);

        Assert.Equal(11, linhas[1].PowerKwp!.Value, 6);
        Assert.Equal(0, linhas[1].PowerKw);
        Assert.Null(linhas[1].DcAcRatio);   // sem potência no modelo, sem razão

        Assert.Null(linhas[2].Model);
        Assert.Equal(0, linhas[2].Capacity);
        Assert.Equal(0, linhas[2].PowerKwp);

        // As linhas somam o mesmo que o resumo da usina.
        var total = InverterTable.Total(linhas);
        Assert.Equal(3, total.Inverters);
        Assert.Equal(6, total.Strings);
        Assert.Equal(8, total.Capacity);
        Assert.Equal(resumo.PowerKwp, total.PowerKwp!.Value, 6);
        Assert.Equal(100, total.PowerKw);
        // O Inversor 2 (sem kW) fica fora dos dois lados do CC/CA da usina.
        Assert.Equal(71.5 / 100, total.DcAcRatio!.Value, 6);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemResumoOKwpFicaEmBrancoEORestoContinua()
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", 1, 4, Caixa, 50);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", Guid.Empty);

        var linhas = InverterTable.Rows([i1], [modelo], new Dictionary<Guid, int> { [i1.Id] = 3 }, null);

        Assert.Equal(3, linhas[0].Strings);
        Assert.Null(linhas[0].PowerKwp);
        Assert.Null(linhas[0].DcAcRatio);
        Assert.Null(InverterTable.Total(linhas).PowerKwp);
        Assert.Equal(50, InverterTable.Total(linhas).PowerKw);
    }

    // ------------------------------------- a edição na linha (05/10/2026)

    [Fact]
    [Trait("Etapa", "14")]
    public void RenomearNaLinhaTrocaSoONomeComAsMesmasRegras()
    {
        var setup = new ElectricalSetup();
        var m = setup.AddModel();
        var inv = setup.AddInverters(m.Id, 2);

        Assert.Null(setup.RenameInverter(inv[0].Id, "  INV-NORTE "));
        Assert.Equal("INV-NORTE", setup.FindInverter(inv[0].Id)!.Name);
        Assert.Equal(m.Id, setup.FindInverter(inv[0].Id)!.Model);

        // O mesmo nome nele mesmo (outra caixa) serve; noutro, não.
        Assert.Null(setup.RenameInverter(inv[0].Id, "inv-norte"));
        Assert.NotNull(setup.RenameInverter(inv[1].Id, "Inv-Norte"));
        Assert.NotNull(setup.RenameInverter(inv[1].Id, "   "));
        Assert.NotNull(setup.RenameInverter(inv[1].Id, null));
        Assert.NotNull(setup.RenameInverter(inv[1].Id, Guid.NewGuid().ToString()));
        Assert.NotNull(setup.RenameInverter(inv[1].Id, new string('x', ElectricalDefaults.MaxNameLength + 1)));
        Assert.NotNull(setup.RenameInverter(Guid.NewGuid(), "Outro"));
        Assert.Equal(inv[1], setup.FindInverter(inv[1].Id));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void RenomearServeAteParaInversorDeModeloQueSumiu()
    {
        var setup = new ElectricalSetup();
        var m = setup.AddModel();
        var inv = setup.AddInverters(m.Id, 1)[0];
        var semModelo = new Inverter(inv.Id, Guid.NewGuid(), inv.Name, Guid.Empty, inv.Color);
        var outro = new ElectricalSetup(inverters: [semModelo], models: [m]);

        Assert.Null(outro.RenameInverter(inv.Id, "Antigo"));
        Assert.Equal("Antigo", outro.FindInverter(inv.Id)!.Name);
        Assert.Equal(semModelo.Model, outro.FindInverter(inv.Id)!.Model);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void TrocarOModeloNaLinhaRecusaOQueNaoComportaAsStrings()
    {
        var setup = new ElectricalSetup();
        var grande = setup.AddModel();
        Assert.Null(setup.EditModel(grande with { Name = "Grande", InputsByMppt = [4, 4, 4, 4, 4] }));
        var pequeno = setup.AddModel();
        Assert.Null(setup.EditModel(pequeno with { Name = "Pequeno", InputsByMppt = [2, 2] }));
        var inv = setup.AddInverters(grande.Id, 1)[0];

        // 5 strings alocadas: o Pequeno (4 entradas) não serve; nada muda.
        var porque = setup.ChangeInverterModel(inv.Id, pequeno.Id, 5);
        Assert.NotNull(porque);
        Assert.Contains("Pequeno", porque);
        Assert.Contains("4", porque);
        Assert.Equal(grande.Id, setup.FindInverter(inv.Id)!.Model);

        // Com 4 cabe (no limite), e volta ao Grande com qualquer número que caiba.
        Assert.Null(setup.ChangeInverterModel(inv.Id, pequeno.Id, 4));
        Assert.Equal(pequeno.Id, setup.FindInverter(inv.Id)!.Model);
        Assert.Null(setup.ChangeInverterModel(inv.Id, grande.Id, 4));
        Assert.Equal(grande.Id, setup.FindInverter(inv.Id)!.Model);

        // O mesmo modelo não é troca (mesmo em excesso, que já existia: nada a recusar).
        Assert.Null(setup.ChangeInverterModel(inv.Id, grande.Id, 99));

        // Modelo ou inversor que não existe: recusado.
        Assert.NotNull(setup.ChangeInverterModel(inv.Id, Guid.NewGuid(), 0));
        Assert.NotNull(setup.ChangeInverterModel(Guid.NewGuid(), grande.Id, 0));
        Assert.Equal(inv.Name, setup.FindInverter(inv.Id)!.Name);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ApagarVariasLinhasDeUmaVez()
    {
        var (setup, t1, _, inv) = Usina();
        setup.SetTransformer([inv[0].Id, inv[1].Id], t1.Id);
        setup.Group(t1.Id, "Skid Norte", []);
        Assert.NotNull(setup.FindSkid(t1.Id));

        var sairam = setup.RemoveInverters([inv[1].Id, inv[0].Id, inv[4].Id, Guid.NewGuid(), inv[4].Id]);

        // Na ordem do cadastro, sem repetir, o que não existia não conta.
        Assert.Equal(["Inversor 1", "Inversor 2", "Inversor 5"], sairam.Select(i => i.Name));
        Assert.Equal(["Inversor 3", "Inversor 4", "Inversor 6"], setup.Inverters.Select(i => i.Name));

        // O skid do T1, sem inversor, deixa de existir (como apagar um por um).
        Assert.Null(setup.FindSkid(t1.Id));
        Assert.Empty(setup.RemoveInverters([]));
    }
}
