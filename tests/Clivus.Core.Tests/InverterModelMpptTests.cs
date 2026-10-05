namespace Clivus.Core.Tests;

/// <summary>
/// O modelo de inversor com a lista de entradas de cada MPPT (pedido do Renan
/// em 05/10/2026: "cada MPPT tem uma quantidade de entrada"). O total é a
/// soma; o formato 1 (MPPT x entradas por MPPT) continua sendo lido.
/// </summary>
public class InverterModelMpptTests
{
    private static readonly EquipmentSize Caixa = ElectricalDefaults.InverterSize;

    [Fact]
    [Trait("Etapa", "14")]
    public void CadaMpptTemAsSuasEntradasEOTotalEASoma()
    {
        var m = new InverterModel(Guid.NewGuid(), "Huawei 330", [4, 4, 4, 5, 5], Caixa);

        Assert.Equal(5, m.Mppts);
        Assert.Equal(22, m.TotalInputs);
        Assert.False(m.IsUniform);
        Assert.True(m.IsValid);
        Assert.True(new InverterModel(Guid.NewGuid(), "U", 3, 4, Caixa).IsUniform);
        Assert.Equal([4, 4, 4], new InverterModel(Guid.NewGuid(), "U", 3, 4, Caixa).InputsByMppt);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OFormato2VaiEVoltaComALista()
    {
        var m = new InverterModel(Guid.NewGuid(), "Huawei 330", [4, 4, 4, 5, 5], new EquipmentSize(1.2, 0.8, 0.5));
        var campos = m.ToFields();

        Assert.Equal(InverterModel.FieldCount, campos.Count);
        Assert.Equal("5", campos[2]);
        Assert.Equal("4;4;4;5;5", campos[3]);
        Assert.Equal(m, InverterModel.Parse(campos));
        Assert.NotEqual(m, m with { InputsByMppt = [4, 4, 4, 5, 6] });
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OFormato1ViraAListaComOMesmoValorRepetido()
    {
        var id = Guid.NewGuid();
        string[] antigo = [id.ToString("D"), "Huawei 250", "5", "4", "1.1", "0.7", "0.6"];

        var lido = InverterModel.ParseLegacy(antigo);

        Assert.NotNull(lido);
        Assert.Equal([4, 4, 4, 4, 4], lido!.InputsByMppt);
        Assert.Equal(20, lido.TotalInputs);
        Assert.Equal(id, lido.Id);

        // O 1 lido pelo leitor do 2 não passa: a lista "4" não tem 5 MPPTs.
        Assert.Null(InverterModel.Parse(antigo));
        Assert.Null(InverterModel.ParseLegacy([id.ToString("D"), "X", "0", "4", "1", "1", "1"]));
    }

    [Theory]
    [Trait("Etapa", "14")]
    [InlineData("3", "4;4")]        // a contagem não bate com a lista
    [InlineData("2", "4;0")]        // MPPT sem entrada
    [InlineData("2", "4;x")]
    [InlineData("2", "4;-1")]
    [InlineData("0", "")]
    public void ListaQueNaoBateNaoVoltaDoDesenho(string mppts, string lista)
    {
        Assert.Null(InverterModel.Parse([Guid.NewGuid().ToString("D"), "X", mppts, lista, "1", "1", "1"]));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void MudarONumeroDeMpptsCresceRepetindoOUltimoEEncolheDoFim()
    {
        Assert.Equal([4, 4, 4, 5, 5, 5, 5], InverterModel.Resize([4, 4, 4, 5, 5], 7));
        Assert.Equal([4, 4], InverterModel.Resize([4, 4, 4, 5, 5], 2));
        Assert.Equal([1, 1], InverterModel.Resize([], 2));
        Assert.Empty(InverterModel.Resize([4], 0));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AListaDigitadaAceitaPontoEVirgulaVirgulaEEspaco()
    {
        Assert.Equal([4, 4, 5], InverterModel.ParseInputs("4;4;5"));
        Assert.Equal([4, 4, 5], InverterModel.ParseInputs(" 4, 4 ,5 "));
        Assert.Equal([4, 4, 5], InverterModel.ParseInputs("4 4 5"));
        Assert.Null(InverterModel.ParseInputs("4;a"));
        Assert.Null(InverterModel.ParseInputs("4;-1"));
        Assert.Empty(InverterModel.ParseInputs("")!);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void EditarGravaAListaERecusaMpptSemEntradaOuDemais()
    {
        var setup = new ElectricalSetup();
        var m = setup.AddModel();

        Assert.Null(setup.EditModel(m with { Name = "Huawei 330", InputsByMppt = [4, 4, 4, 5, 5] }));
        Assert.Equal(22, setup.FindModel(m.Id)!.TotalInputs);

        Assert.NotNull(setup.EditModel(m with { InputsByMppt = [4, 0, 4] }));
        Assert.NotNull(setup.EditModel(m with { InputsByMppt = [4, ElectricalDefaults.MaxInputsPerMppt + 1] }));
        Assert.NotNull(setup.EditModel(m with { InputsByMppt = [] }));
        Assert.Equal([4, 4, 4, 5, 5], setup.FindModel(m.Id)!.InputsByMppt);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ACapacidadeDoExcessoEASomaDasEntradas()
    {
        var m = new InverterModel(Guid.NewGuid(), "Huawei 330", [4, 4, 4, 5, 5], Caixa);

        Assert.Equal(0, StringAllocation.Excess(22, m));
        Assert.Equal(1, StringAllocation.Excess(23, m));
    }
}
