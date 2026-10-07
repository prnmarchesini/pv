namespace Clivus.Core.Tests;

/// <summary>
/// Erro grave de 07/10/2026 (Renan: "cliquei em regerar mesas da usina
/// inteira, e o sistema manteve as strings antigas"): apagar mesa leva tudo
/// que é dela. E a UC compartilhada é UC1, UC2 ("é UC1 UC2, de unidade
/// consumidora"), com o desenho antigo (C1, C2) lido no nome novo.
/// </summary>
public class ErasedTablesTests
{
    private static ElectricalString Str(params Guid[] modulos) => new(Guid.NewGuid(), Guid.Empty, modulos, Guid.Empty, string.Empty);

    [Fact]
    [Trait("Etapa", "16")]
    public void StringQueTocaModuloQueSumiuMorre()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var viva = Str(a, b);
        var meioApagada = Str(b, c);
        var orfa = Str(Guid.NewGuid());
        var semModulo = Str();

        var mortas = StringsOfErasedTables.Doomed([viva, meioApagada, orfa, semModulo], new HashSet<Guid> { a, b });

        Assert.Equal(new HashSet<Guid> { meioApagada.Id, orfa.Id, semModulo.Id }, mortas);
    }

    [Fact]
    [Trait("Etapa", "16")]
    public void MesaApagadaSaiDoBlocoEOBlocoVazioSai()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var b1 = setup.AddBlock();
        var b2 = setup.AddBlock();
        var b3 = setup.AddBlock();
        setup.SetTables(b1.Id, [m1, m2]);
        setup.SetTables(b2.Id, [m3]);

        Assert.Equal(2, setup.ForgetTables(new HashSet<Guid> { m2, m3 }));

        Assert.Equal([b1.Id, b3.Id], setup.Blocks.Select(b => b.Id));
        Assert.Equal([m1], setup.Find(b1.Id)!.Tables);
        Assert.Equal(0, setup.ForgetTables(new HashSet<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    [Trait("Etapa", "12")]
    public void ACompartilhadaEUcEODesenhoAntigoViraUc()
    {
        Assert.Equal("UC1", new ElectricalSetup().AddSharedUnit().Code);

        var caixa = new EquipmentSize(4, 3, 3);
        var antiga = new ConsumerUnit(Guid.NewGuid(), "C2", "Subestação C2", ConsumerUnitMode.Shared, caixa);
        var lida = ConsumerUnit.Parse(antiga.ToFields())!;
        Assert.Equal("UC2", lida.Code);
        Assert.Equal("Subestação UC2", lida.Name);

        // Nome dado pelo usuário fica; a unitária (U1) não muda.
        Assert.Equal("Medição norte", ConsumerUnit.Parse((antiga with { Name = "Medição norte" }).ToFields())!.Name);
        var unitaria = new ConsumerUnit(Guid.NewGuid(), "U1", "Subestação U1", ConsumerUnitMode.Unitary, caixa);
        Assert.Equal("U1", ConsumerUnit.Parse(unitaria.ToFields())!.Code);
    }
}
