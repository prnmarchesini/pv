namespace Clivus.Core.Tests;

/// <summary>
/// A ordem da lista de inversores (Renan, 10/10/2026: "não tem o inversor 1;
/// eu criei, ele foi criado como Inversor 21, eu renomeei para Inversor 1 e
/// não consigo arrastar ele na lista. Quero ter opções de reordenação: por
/// Trafo, por Nome do inversor, Trafo em seguida nome").
/// </summary>
public class InverterOrderTests
{
    private static readonly InverterModel Modelo = new(Guid.NewGuid(), "Inv Top", [4, 4], new EquipmentSize(1.1, 0.7, 0.6), 250);

    private static List<string> Nomes(ElectricalSetup s) => s.Inverters.Select(i => i.Name).ToList();

    [Fact]
    [Trait("Etapa", "14")]
    public void AOrdemNaturalComparaOsNumerosPeloValor()
    {
        var c = NaturalStringComparer.Instance;
        Assert.True(c.Compare("Inversor 2", "Inversor 10") < 0);
        Assert.True(c.Compare("Inversor 10", "Inversor 9") > 0);
        Assert.True(c.Compare("T9", "T10") < 0);
        Assert.True(c.Compare("T1", "t2") < 0);
        Assert.Equal(0, c.Compare("inversor 3", "INVERSOR 3"));
        Assert.Equal(0, c.Compare("  Inversor 3 ", "Inversor 3"));
        Assert.Equal(0, c.Compare("Inversor 007", "Inversor 7"));
        Assert.True(c.Compare("Inversor", "Inversor 1") < 0);
        Assert.True(c.Compare("Inversor 1", "Inversor 1A") < 0);
        Assert.True(c.Compare("1", "A") < 0);
        Assert.True(c.Compare(null, "A") < 0);
        Assert.True(c.Compare("Inversor 99999999999999999999", "Inversor 100000000000000000000") < 0);

        // O hífen conta como caractere (a ordem não depende da cultura da máquina).
        Assert.True(c.Compare("INV-B", "INVA") < 0);
        Assert.True(c.Compare("T-1", "T1") > 0);

        var lista = new List<string> { "Inversor 10", "INV-NORTE", "Inversor 2", "Inversor 1", "Inversor 21" };
        lista.Sort(c);
        Assert.Equal(["INV-NORTE", "Inversor 1", "Inversor 2", "Inversor 10", "Inversor 21"], lista);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void OInversor21RenomeadoParaInversor1VaiParaOComecoPeloNome()
    {
        var setup = new ElectricalSetup(models: [Modelo]);
        setup.AddInverters(Modelo.Id, 20);
        setup.RemoveInverters([setup.FindInverter("Inversor 1")!.Id]);
        var novo = Assert.Single(setup.AddInverters(Modelo.Id, 1));
        Assert.Equal("Inversor 21", novo.Name);
        Assert.Null(setup.RenameInverter(novo.Id, "Inversor 1"));
        Assert.Equal("Inversor 1", Nomes(setup)[^1]);

        Assert.Equal(20, setup.SortInverters(InverterOrder.Name));
        Assert.Equal(Enumerable.Range(1, 20).Select(n => $"Inversor {n}"), Nomes(setup));
        Assert.Equal(novo.Id, setup.Inverters[0].Id);

        // Ordenar de novo não muda nada.
        Assert.Equal(0, setup.SortInverters(InverterOrder.Name));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void ArrastarLevaOInversorParaAPosicaoDoOutro()
    {
        var setup = new ElectricalSetup(models: [Modelo]);
        var i = setup.AddInverters(Modelo.Id, 5);

        // Subindo: entra antes do alvo.
        Assert.True(setup.MoveInverter(i[4].Id, i[1].Id));
        Assert.Equal(["Inversor 1", "Inversor 5", "Inversor 2", "Inversor 3", "Inversor 4"], Nomes(setup));

        // Descendo: entra depois do alvo.
        Assert.True(setup.MoveInverter(i[0].Id, i[3].Id));
        Assert.Equal(["Inversor 5", "Inversor 2", "Inversor 3", "Inversor 4", "Inversor 1"], Nomes(setup));

        // Nele mesmo, ou inversor que não existe: nada muda.
        Assert.False(setup.MoveInverter(i[2].Id, i[2].Id));
        Assert.False(setup.MoveInverter(Guid.NewGuid(), i[2].Id));
        Assert.False(setup.MoveInverter(i[2].Id, Guid.NewGuid()));
        Assert.Equal(["Inversor 5", "Inversor 2", "Inversor 3", "Inversor 4", "Inversor 1"], Nomes(setup));

        // Para o começo e para o fim.
        Assert.True(setup.MoveInverter(i[0].Id, i[4].Id));
        Assert.Equal("Inversor 1", Nomes(setup)[0]);
        Assert.True(setup.MoveInverter(i[0].Id, i[3].Id));
        Assert.Equal("Inversor 1", Nomes(setup)[^1]);
    }

    /// <summary>Doze trafos (T1 a T12, cadastrados fora de ordem) e inversores espalhados neles.</summary>
    private static (ElectricalSetup Setup, Dictionary<string, Guid> Trafo) Usina()
    {
        var setup = new ElectricalSetup(models: [Modelo]);
        for (var k = 0; k < 12; k++) setup.AddTransformer();
        var trafo = setup.Transformers.ToDictionary(t => t.Nickname, t => t.Id);

        // O T10 cadastrado antes do T2 (o apelido manda, não a ordem do cadastro).
        var t10 = setup.FindTransformer(trafo["T10"])!;
        var t2 = setup.FindTransformer(trafo["T2"])!;
        Assert.Null(setup.EditTransformer(t10 with { Nickname = "TX" }));
        Assert.Null(setup.EditTransformer(t2 with { Nickname = "T10" }));
        Assert.Null(setup.EditTransformer(setup.FindTransformer(trafo["T10"])! with { Nickname = "T2" }));
        trafo = setup.Transformers.ToDictionary(t => t.Nickname, t => t.Id);

        var i = setup.AddInverters(Modelo.Id, 7);
        setup.SetTransformer([i[0].Id], trafo["T10"]);   // Inversor 1
        setup.SetTransformer([i[1].Id], trafo["T2"]);    // Inversor 2
        setup.SetTransformer([i[3].Id], trafo["T2"]);    // Inversor 4
        setup.SetTransformer([i[4].Id], trafo["T1"]);    // Inversor 5
        setup.SetTransformer([i[6].Id], trafo["T10"]);   // Inversor 7
        // Inversor 3 e Inversor 6 sem trafo.
        Assert.True(setup.MoveInverter(i[6].Id, i[0].Id));   // Inversor 7 no começo
        Assert.True(setup.MoveInverter(i[3].Id, i[1].Id));   // Inversor 4 antes do 2
        Assert.Equal(["Inversor 7", "Inversor 1", "Inversor 4", "Inversor 2", "Inversor 3", "Inversor 5", "Inversor 6"], Nomes(setup));
        return (setup, trafo);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void PorTrafoPeloApelidoNaturalSemTrafoPorUltimo()
    {
        var (setup, _) = Usina();

        // T1, T2, T10 (natural); dentro do trafo, a ordem de antes; sem trafo no fim.
        setup.SortInverters(InverterOrder.Transformer);
        Assert.Equal(["Inversor 5", "Inversor 4", "Inversor 2", "Inversor 7", "Inversor 1", "Inversor 3", "Inversor 6"], Nomes(setup));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void PorTrafoEDepoisNome()
    {
        var (setup, _) = Usina();

        setup.SortInverters(InverterOrder.TransformerThenName);
        Assert.Equal(["Inversor 5", "Inversor 2", "Inversor 4", "Inversor 1", "Inversor 7", "Inversor 3", "Inversor 6"], Nomes(setup));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void TrafoQueSumiuDoCadastroVemAntesDosSemTrafo()
    {
        var a = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty);
        var b = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 2", Guid.NewGuid());
        var setup = new ElectricalSetup(inverters: [a, b], models: [Modelo]);
        var t = setup.AddTransformer();
        var c = setup.AddInverters(Modelo.Id, 1)[0];
        setup.SetTransformer([c.Id], t.Id);

        Assert.Equal(2, setup.SortInverters(InverterOrder.Transformer));   // o do meio fica
        Assert.Equal(["Inversor 3", "Inversor 2", "Inversor 1"], Nomes(setup));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AOrdemNovaValeParaONumeroDoInversorNaTag()
    {
        var a = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty);
        var b = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 2", Guid.Empty);
        var setup = new ElectricalSetup(inverters: [a, b], models: [Modelo]);
        var modulos = new Dictionary<Guid, ModuleSpot>();
        ElectricalString Str(Guid inversor, double x)
        {
            var m = Guid.NewGuid();
            modulos[m] = new ModuleSpot(Guid.NewGuid(), x, 0);
            return new ElectricalString(Guid.NewGuid(), Guid.Empty, [m], inversor, string.Empty);
        }

        var sa = Str(a.Id, 0);
        var sb = Str(b.Id, 10);
        var varredura = new ScanSetup(ScanDirection.LeftToRight, []);

        var antes = StringNumbering.Number(TagScheme.Default, varredura, [], setup.Inverters, [sa, sb], modulos);
        Assert.Equal("I1.S1", antes.Tags[sa.Id]);
        Assert.Equal("I2.S1", antes.Tags[sb.Id]);

        Assert.True(setup.MoveInverter(b.Id, a.Id));
        var depois = StringNumbering.Number(TagScheme.Default, varredura, [], setup.Inverters, [sa, sb], modulos);
        Assert.Equal("I1.S1", depois.Tags[sb.Id]);
        Assert.Equal("I2.S1", depois.Tags[sa.Id]);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AOrdemNovaValeParaODistribuir()
    {
        var a = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 1", Guid.Empty, Target: 1);
        var b = new Inverter(Guid.NewGuid(), Modelo.Id, "Inversor 2", Guid.Empty, Target: 1);
        var setup = new ElectricalSetup(inverters: [a, b], models: [Modelo]);
        Assert.True(setup.MoveInverter(b.Id, a.Id));

        var modulos = new Dictionary<Guid, ModuleSpot>();
        var strings = new List<ElectricalString>();
        for (var k = 0; k < 2; k++)
        {
            var m = Guid.NewGuid();
            modulos[m] = new ModuleSpot(Guid.NewGuid(), k * 10, 0);
            strings.Add(new ElectricalString(Guid.NewGuid(), Guid.Empty, [m], Guid.Empty, string.Empty));
        }

        var r = StringAutoAllocation.Allocate(setup.Inverters, setup.Models, strings, modulos, AllocationScan.Default);
        var primeira = r.Changed.Single(s => s.Id == strings[0].Id);
        Assert.Equal(b.Id, primeira.Inverter);
    }
}
