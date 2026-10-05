namespace Clivus.Core.Tests;

/// <summary>Alocação de strings em inversores (elétrica, 14.3 a 14.5): trava, contagem, soltar sem mexer em geometria.</summary>
public class StringAllocationTests
{
    private static ElectricalString S(Guid inversor, string tag = "") =>
        new(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], inversor, tag);

    // ------------------------------------------------------------ 14.3

    [Fact]
    [Trait("Etapa", "14")]
    public void AsLivresPassamASerDoInversorEOResteFicaIgual()
    {
        var inversor = Guid.NewGuid();
        var livre = S(Guid.Empty, "T1.I1.S3");

        var plano = StringAllocation.Allocate(inversor, [livre]);

        var nova = Assert.Single(plano.Changed);
        Assert.Equal(inversor, nova.Inverter);
        Assert.Equal(livre.Id, nova.Id);
        Assert.Equal(livre.Modules, nova.Modules);   // os módulos (a geometria) não mudam
        Assert.Equal(livre.Type, nova.Type);
        Assert.Equal(livre.Tag, nova.Tag);           // a tag é da numeração (etapa 15), não da alocação
        Assert.Empty(plano.Refused);
        Assert.Equal(0, plano.AlreadyHere);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void StringDeOutroInversorFicaTravadaENuncaTrocaDeDono()
    {
        var meu = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var dele = S(outro);
        var minha = S(meu);
        var livre = S(Guid.Empty);

        var plano = StringAllocation.Allocate(meu, [dele, minha, livre]);

        Assert.Equal(dele.Id, Assert.Single(plano.Refused).Id);
        Assert.Equal(livre.Id, Assert.Single(plano.Changed).Id);
        Assert.Equal(1, plano.AlreadyHere);
        Assert.DoesNotContain(plano.Changed, s => s.Id == dele.Id);
        Assert.True(StringAllocation.IsLockedFor(dele, meu));
        Assert.False(StringAllocation.IsLockedFor(minha, meu));
        Assert.False(StringAllocation.IsLockedFor(livre, meu));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SelecaoVaziaOuRepetidaNaoDuplica()
    {
        var inversor = Guid.NewGuid();
        var livre = S(Guid.Empty);

        var vazio = StringAllocation.Allocate(inversor, []);
        Assert.Empty(vazio.Changed);
        Assert.Empty(vazio.Refused);
        Assert.Equal(0, vazio.AlreadyHere);

        var repetida = StringAllocation.Allocate(inversor, [livre, livre, livre]);
        Assert.Single(repetida.Changed);

        Assert.Throws<ArgumentException>(() => StringAllocation.Allocate(Guid.Empty, [livre]));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void UmaStringNuncaCaiEmDoisInversores()
    {
        // Duas alocações seguidas, como dois cliques em inversores diferentes.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var strings = Enumerable.Range(0, 6).Select(_ => S(Guid.Empty)).ToList();

        var primeira = StringAllocation.Allocate(a, strings.Take(4));
        var depois = strings.Select(s => primeira.Changed.FirstOrDefault(c => c.Id == s.Id) ?? s).ToList();
        var segunda = StringAllocation.Allocate(b, depois);

        Assert.Equal(4, segunda.Refused.Count);
        Assert.Equal(2, segunda.Changed.Count);
        var final = depois.Select(s => segunda.Changed.FirstOrDefault(c => c.Id == s.Id) ?? s).ToList();
        Assert.Equal(4, final.Count(s => s.Inverter == a));
        Assert.Equal(2, final.Count(s => s.Inverter == b));
    }

    // ------------------------------------------------------------ 14.5

    [Fact]
    [Trait("Etapa", "14")]
    public void SoltarTodasLiberaSoAsDoInversorSemMexerNaGeometria()
    {
        var meu = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var minhas = new[] { S(meu, "T1.I1.S1"), S(meu, "T1.I1.S2") };
        var dele = S(outro);
        var livre = S(Guid.Empty);

        var soltas = StringAllocation.Release(meu, [.. minhas, dele, livre]);

        Assert.Equal(2, soltas.Count);
        Assert.All(soltas, s => Assert.False(s.IsAllocated));
        Assert.Equal(minhas.Select(m => m.Id), soltas.Select(s => s.Id));
        Assert.Equal(minhas.Select(m => m.Modules), soltas.Select(s => s.Modules));   // geometria: os mesmos módulos
        Assert.Equal(minhas.Select(m => m.Tag), soltas.Select(s => s.Tag));
        Assert.Empty(StringAllocation.Release(Guid.Empty, [livre]));
        Assert.Empty(StringAllocation.Release(meu, []));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void DepoisDeSoltarOutroInversorPodePegar()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var s = S(a);

        Assert.Single(StringAllocation.Allocate(b, [s]).Refused);

        var solta = Assert.Single(StringAllocation.Release(a, [s]));
        Assert.Equal(b, Assert.Single(StringAllocation.Allocate(b, [solta]).Changed).Inverter);
    }
}
