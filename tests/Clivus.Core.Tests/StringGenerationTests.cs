namespace Clivus.Core.Tests;

/// <summary>Aba Gerar: casar os tipos com grupos de mesas (elétrica, 11.6) e o aviso da mesa que não casou (11.8).</summary>
public class StringGenerationTests
{
    private static RoutingCell C(int mesa, int coluna, int fileira) => new(mesa, coluna, fileira);

    /// <summary>Um tipo com a assinatura dada e uma string convencional por fileira do cartesiano.</summary>
    internal static StringType Tipo(string nome, params (int Colunas, int Fileiras)[] mesas)
    {
        var arranjo = new StringArrangement(mesas.Select(m => new ArrangementTable(m.Colunas, m.Fileiras)).ToList());
        var strings = Enumerable.Range(0, mesas.Max(m => m.Fileiras))
            .Select(f => StringRouting.WholeRow(arranjo, C(0, 0, f), RoutingKind.Conventional, out _)!)
            .ToList();
        return new StringType(Guid.NewGuid(), nome, arranjo, ArrangementSketch.Default(arranjo), strings);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void CadaTipoCaiNoGrupoDeAssinaturaIgual()
    {
        var uma28 = Tipo("Modelo 1", (14, 2));
        var duas14 = Tipo("Modelo 2", (7, 2), (7, 2));
        var fileira = MesasDeString.Fileira(1, 0, 0.5, (14, 2), (7, 2), (7, 2), (14, 2));

        var plano = StringGeneration.Plan([uma28, duas14], fileira, []);

        Assert.Equal(["Modelo 1: F1.1", "Modelo 2: F1.2, F1.3", "Modelo 1: F1.4"], plano.Groups.Select(g => $"{g.Type.Name}: {g.Labels}"));
        Assert.Empty(plano.Unmatched);
        Assert.Empty(plano.Skipped);
        Assert.Equal(2 + 2 + 2, plano.StringCount);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void DuasDe14SoPreenchemGrupoDeDuasDe14EASobraEAvisada()
    {
        var duas14 = Tipo("Modelo 2", (7, 2), (7, 2));
        var fileira = MesasDeString.Fileira(3, 0, 0.5, (7, 2), (7, 2), (7, 2));

        var plano = StringGeneration.Plan([duas14], fileira, []);

        Assert.Equal("F3.1, F3.2", plano.Groups.Single().Labels);
        Assert.Equal(["F3.3: mesa de 14 módulos (7x2) sem tipo de string"], plano.Unmatched);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void AOrdemDaSelecaoNaoMudaOResultado()
    {
        var uma28 = Tipo("Modelo 1", (14, 2));
        var duas14 = Tipo("Modelo 2", (7, 2), (7, 2));
        var mesas = MesasDeString.Fileira(1, 0, 0.5, (7, 2), (14, 2), (7, 2), (7, 2), (14, 2))
            .Concat(MesasDeString.Fileira(2, 8, 0.5, (7, 2), (7, 2)))
            .ToList();

        string Resumo(IReadOnlyList<FieldTable> lista)
        {
            var p = StringGeneration.Plan([uma28, duas14], lista, []);
            return string.Join(" | ", p.Groups.Select(g => $"{g.Type.Name}: {g.Labels}")) + " // " + string.Join(" | ", p.Unmatched);
        }

        var esperado = Resumo(mesas);
        Assert.Equal("Modelo 1: F1.2 | Modelo 2: F1.3, F1.4 | Modelo 1: F1.5 | Modelo 2: F2.1, F2.2 // F1.1: mesa de 14 módulos (7x2) sem tipo de string", esperado);

        var embaralhada = mesas.OrderBy(m => m.Id).ToList();
        Assert.Equal(esperado, Resumo(embaralhada));
        Assert.Equal(esperado, Resumo(Enumerable.Reverse(mesas).ToList()));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MesaForaDaSelecaoNoMeioOuOutraFileiraNaoFazemGrupo()
    {
        var duas14 = Tipo("Modelo 2", (7, 2), (7, 2));
        var fileira = MesasDeString.Fileira(1, 0, 0.5, (7, 2), (7, 2), (7, 2));
        var outra = MesasDeString.Fileira(2, 8, 0.5, (7, 2));

        var plano = StringGeneration.Plan([duas14], [fileira[0], fileira[2], outra[0]], []);

        Assert.Empty(plano.Groups);
        Assert.Equal(3, plano.Unmatched.Count);
        Assert.Contains(plano.Unmatched, u => u.StartsWith("F1.1:", StringComparison.Ordinal));
        Assert.Contains(plano.Unmatched, u => u.StartsWith("F1.3:", StringComparison.Ordinal));
        Assert.Contains(plano.Unmatched, u => u.StartsWith("F2.1:", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void TipoDe1VNaoCasaComMesa2V()
    {
        var umaV = Tipo("Modelo 1", (14, 1));
        var plano = StringGeneration.Plan([umaV], MesasDeString.Fileira(1, 0, 0.5, (14, 2), (14, 1)), []);

        Assert.Equal("F1.2", plano.Groups.Single().Labels);
        Assert.Equal(["F1.1: mesa de 28 módulos (14x2) sem tipo de string"], plano.Unmatched);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void AsStringsDoGrupoSeguemOTracadoDoTipoModuloAModulo()
    {
        var duas14 = Tipo("Modelo 2", (7, 2), (7, 2));
        var fileira = MesasDeString.Fileira(1, 0, 0.5, (7, 2), (7, 2));

        var grupo = StringGeneration.Plan([duas14], fileira, []).Groups.Single();
        var baixo = grupo.Strings[0];

        Assert.Equal(14, baixo.String.Modules.Count);
        var esperado = Enumerable.Range(0, 7).Select(c => fileira[0].ModuleAt(c, 0)!.Id)
            .Concat(Enumerable.Range(0, 7).Select(c => fileira[1].ModuleAt(c, 0)!.Id));
        Assert.Equal(esperado, baixo.String.Modules);
        Assert.Equal(baixo.String.Modules, baixo.Modules.Select(m => m.Id));
        Assert.Equal(duas14.Id, baixo.String.Type);
        Assert.False(baixo.String.IsAllocated);
        Assert.Equal(string.Empty, baixo.String.Tag);
        Assert.True(baixo.String.IsValid);
        Assert.Equal(grupo.Strings.Count, grupo.Strings.Select(s => s.String.Id).Distinct().Count());
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void NaMesaViradaOMesmoTracadoCaiNasMesmasPontasDoCartesiano()
    {
        // A F1.2 está virada 180°: a coluna 0 dela fica do lado de lá. O +
        // do tipo (célula 0.0.0 do cartesiano) continua na ponta de cá da fileira.
        var tipo = Tipo("Modelo 1", (7, 2), (7, 2));
        var normal = MesasDeString.Mesa("F1.1", 0, 0, 7, 2);
        var virada = MesasDeString.Mesa("F1.2", 7 * 1.1 + 0.5 + 7 * 1.1, 2 * 2.3, 7, 2, angulo: Math.PI);

        var grupo = StringGeneration.Plan([tipo], [normal, virada], []).Groups.Single();
        var baixo = grupo.Strings[0];

        Assert.Equal(normal.ModuleAt(0, 0)!.Id, baixo.String.Modules[0]);
        Assert.Equal(virada.ModuleAt(6, 1)!.Id, baixo.String.Modules[7]);
        Assert.Equal(virada.ModuleAt(0, 1)!.Id, baixo.String.Modules[^1]);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void RegerarSubstituiAsLivresENaoTocaNaLigadaAInversor()
    {
        var uma28 = Tipo("Modelo 1", (14, 2));
        var fileira = MesasDeString.Fileira(1, 0, 0.5, (14, 2), (14, 2), (14, 2));

        var livre = Existente(fileira[0], inversor: Guid.Empty);
        var ligada = Existente(fileira[1], inversor: Guid.NewGuid());
        var atravessa = new ExistingString(
            new ElectricalString(Guid.NewGuid(), Guid.Empty, [fileira[2].Modules[0].Id], Guid.Empty, string.Empty),
            new HashSet<Guid> { fileira[2].Id, Guid.NewGuid() });

        var plano = StringGeneration.Plan([uma28], fileira, [livre, ligada, atravessa]);

        Assert.Equal("F1.1", plano.Groups.Single().Labels);
        Assert.Equal([livre.String.Id], plano.Groups.Single().Replaces);
        Assert.Equal(2, plano.Skipped.Count);
        Assert.Contains("F1.2", plano.Skipped[0]);
        Assert.Contains("inversor", plano.Skipped[0]);
        Assert.Contains("F1.3", plano.Skipped[1]);
        Assert.Empty(plano.Unmatched);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void SemTipoComTracadoNadaCasaETodaMesaEAvisada()
    {
        var semTracado = new StringType(Guid.NewGuid(), "Modelo 1", new StringArrangement([new ArrangementTable(14, 2)]));
        var plano = StringGeneration.Plan([semTracado], MesasDeString.Fileira(1, 0, 0.5, (14, 2), (14, 2)), []);

        Assert.Empty(plano.Groups);
        Assert.Equal(2, plano.Unmatched.Count);
        Assert.Contains(plano.Notes, n => n.Contains("Modelo 1"));
        Assert.Contains(plano.Notes, n => n.Contains("nenhum tipo"));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void TipoQueNaoCobreAMesaInteiraAvisaQuantosFicamSemString()
    {
        var arranjo = new StringArrangement([new ArrangementTable(14, 2)]);
        var so = StringRouting.WholeRow(arranjo, C(0, 0, 0), RoutingKind.Conventional, out _)!;
        var meia = new StringType(Guid.NewGuid(), "Modelo 1", arranjo, null, [so]);

        var plano = StringGeneration.Plan([meia], MesasDeString.Fileira(1, 0, 0.5, (14, 2)), []);

        Assert.Single(plano.Groups);
        Assert.Contains(plano.Notes, n => n.Contains("14 módulo(s) sem string"));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void MesaFuradaNaoRecebeStringEEAvisada()
    {
        var uma28 = Tipo("Modelo 1", (14, 2));
        var inteira = MesasDeString.Mesa("F1.1", 0, 0, 14, 2);
        var furada = inteira with { Modules = inteira.Modules.Skip(1).ToList() };

        var plano = StringGeneration.Plan([uma28], [furada], []);

        Assert.Empty(plano.Groups);
        Assert.Contains("F1.1", plano.Unmatched.Single());
    }

    private static ExistingString Existente(FieldTable mesa, Guid inversor) =>
        new(new ElectricalString(Guid.NewGuid(), Guid.Empty, mesa.Modules.Take(3).Select(m => m.Id).ToList(), inversor, string.Empty), new HashSet<Guid> { mesa.Id });
}
