namespace Clivus.Core.Tests;

/// <summary>
/// Rota de cabos (roteamento, 17.1): cada aba só fica habilitada quando os
/// dois lados do trecho existem no desenho, e a aba indisponível diz o que falta.
/// </summary>
public class CableRouteAvailabilityTests
{
    private static readonly EquipmentCount Nenhum = new(0, 0);
    private static readonly EquipmentCount EmCampo = new(2, 2);
    private static readonly EquipmentCount SoCadastrado = new(3, 0);

    /// <summary>Uma usina com strings, inversores, trafos e subestação em campo; sem combiner.</summary>
    private static readonly CableRouteDrawing Completa = new(Strings: 40, Combiners: Nenhum, Inverters: EmCampo, Transformers: EmCampo, Substations: EmCampo);

    [Fact]
    [Trait("Etapa", "17")]
    public void AsQuatroAbasNaOrdemDoPlano()
    {
        Assert.Equal([CableRoute.DirectCurrent, CableRoute.Combiner, CableRoute.AlternatingCurrent, CableRoute.MediumVoltage], CableRoutes.All);
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void UsinaCompletaSemCombinerLiberaCcCaEMt()
    {
        Assert.Empty(CableRoutes.Missing(CableRoute.DirectCurrent, Completa));
        Assert.Empty(CableRoutes.Missing(CableRoute.AlternatingCurrent, Completa));
        Assert.Empty(CableRoutes.Missing(CableRoute.MediumVoltage, Completa));
        Assert.NotEmpty(CableRoutes.Missing(CableRoute.Combiner, Completa));
    }

    /// <summary>A validação do Renan: desenho sem trafo, a aba CA avisa o que falta.</summary>
    [Fact]
    [Trait("Etapa", "17")]
    public void SemTrafoACaDizQueFaltaOTrafo()
    {
        var semTrafo = Completa with { Transformers = Nenhum };

        var falta = Assert.Single(CableRoutes.Missing(CableRoute.AlternatingCurrent, semTrafo));
        Assert.Contains("transformador", falta);
        Assert.Empty(CableRoutes.Missing(CableRoute.DirectCurrent, semTrafo));
        Assert.Single(CableRoutes.Missing(CableRoute.MediumVoltage, semTrafo));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void SemNadaCadaAbaListaOsDoisLados()
    {
        var vazio = new CableRouteDrawing(0, Nenhum, Nenhum, Nenhum, Nenhum);

        Assert.Equal(2, CableRoutes.Missing(CableRoute.DirectCurrent, vazio).Count);
        Assert.Equal(3, CableRoutes.Missing(CableRoute.Combiner, vazio).Count);
        Assert.Equal(2, CableRoutes.Missing(CableRoute.AlternatingCurrent, vazio).Count);
        Assert.Equal(2, CableRoutes.Missing(CableRoute.MediumVoltage, vazio).Count);
    }

    /// <summary>
    /// A rota precisa do ponto físico: equipamento só cadastrado não libera a
    /// aba, mas o recado diz que falta só pôr em campo (e quantos há no cadastro).
    /// </summary>
    [Fact]
    [Trait("Etapa", "17")]
    public void CadastradoSemCampoNaoLiberaEORecadoDizPorEmCampo()
    {
        var semCampo = Completa with { Inverters = SoCadastrado };

        var falta = Assert.Single(CableRoutes.Missing(CableRoute.AlternatingCurrent, semCampo));
        Assert.Contains("em campo", falta);
        Assert.Contains("3", falta);
        Assert.NotEqual(falta, Assert.Single(CableRoutes.Missing(CableRoute.AlternatingCurrent, Completa with { Inverters = Nenhum })));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void CcComCombinerEmCampoNaoPrecisaDeInversor()
    {
        var soCombiner = Completa with { Combiners = EmCampo, Inverters = Nenhum };

        Assert.Empty(CableRoutes.Missing(CableRoute.DirectCurrent, soCombiner));
        Assert.Single(CableRoutes.Missing(CableRoute.Combiner, soCombiner));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void SemStringACcEACombinerAvisam()
    {
        var semString = Completa with { Strings = 0, Combiners = EmCampo };

        Assert.Contains("string", Assert.Single(CableRoutes.Missing(CableRoute.DirectCurrent, semString)));
        Assert.Contains("string", Assert.Single(CableRoutes.Missing(CableRoute.Combiner, semString)));
        Assert.Empty(CableRoutes.Missing(CableRoute.AlternatingCurrent, semString));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void ComTudoACombinerLibera()
    {
        Assert.Empty(CableRoutes.Missing(CableRoute.Combiner, Completa with { Combiners = EmCampo }));
    }

    [Fact]
    [Trait("Etapa", "17")]
    public void MtSemSubestacaoEmCampoAvisa()
    {
        var falta = Assert.Single(CableRoutes.Missing(CableRoute.MediumVoltage, Completa with { Substations = SoCadastrado }));
        Assert.Contains("subestação", falta);
    }

    private static readonly EquipmentSize Caixa = new(2, 1, 2.2);

    /// <summary>
    /// Contagem pelo cadastro (revisão do 17.1): a subestação compartilhada é
    /// UM equipamento (o bloco), por mais UCs que tenha; bloco em campo sem
    /// cadastro (COPY, UNDO, desenho copiado) não libera a aba.
    /// </summary>
    [Fact]
    [Trait("Etapa", "17")]
    public void ContaPeloCadastroOBlocoCompartilhadoEUmEOOrfaoNaoConta()
    {
        var bloco = new Substation(Guid.NewGuid(), "Cubículo", Caixa);
        var ucs = Enumerable.Range(1, 3).Select(n => new ConsumerUnit(Guid.NewGuid(), $"C{n}", "", ConsumerUnitMode.Shared, Caixa, bloco.Id)).ToList();
        var modelo = new InverterModel(Guid.NewGuid(), "Teste 2x2", 2, 2, Caixa);
        var trafo = new Transformer(Guid.NewGuid(), "Trafo seco", "TA", 800, 13800, 2500, 4, 6.5, "", Caixa, ucs[0].Id);
        var inversor = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", trafo.Id);
        var setup = new ElectricalSetup(transformers: [trafo], inverters: [inversor], units: ucs, models: [modelo], substations: [bloco]);

        var orfao = (EquipmentKind.Transformer, Guid.NewGuid());
        var desenho = CableRoutes.Drawing(setup, strings: 5, inField: new HashSet<(EquipmentKind, Guid)> { orfao, (EquipmentKind.Inverter, inversor.Id) });

        Assert.Equal(new EquipmentCount(1, 0), desenho.Substations);
        Assert.Equal(new EquipmentCount(1, 0), desenho.Transformers);
        Assert.Equal(new EquipmentCount(1, 1), desenho.Inverters);
        Assert.Equal(5, desenho.Strings);
        Assert.Contains("1 no cadastro", Assert.Single(CableRoutes.Missing(CableRoute.AlternatingCurrent, desenho)));
    }
}
