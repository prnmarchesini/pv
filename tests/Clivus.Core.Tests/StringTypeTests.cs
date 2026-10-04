namespace Clivus.Core.Tests;

/// <summary>Tipo de string e biblioteca (elétrica, 11.1).</summary>
public class StringTypeTests
{
    private static StringArrangement Uma28() => new([new ArrangementTable(14, 2)]);

    private static StringArrangement Duas14() => new([new ArrangementTable(7, 2), new ArrangementTable(7, 2)]);

    [Fact]
    [Trait("Etapa", "11")]
    public void AdicionarDaNomesEmSequencia()
    {
        var biblioteca = new StringLibrary([]);

        var a = biblioteca.Add(Uma28());
        var b = biblioteca.Add(Duas14());

        Assert.Equal(["Modelo 1", "Modelo 2"], biblioteca.Types.Select(t => t.Name));
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(Uma28(), a.Arrangement);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ONomeNovoNaoRepeteODeQuemFicou()
    {
        var biblioteca = new StringLibrary([]);
        var um = biblioteca.Add(Uma28());
        biblioteca.Add(Uma28());
        biblioteca.Add(Uma28());

        Assert.True(biblioteca.Remove(um.Id));
        var novo = biblioteca.Add(Duas14());

        // "Modelo 1" saiu; o novo vem depois do maior que existe, sem reaproveitar.
        Assert.Equal("Modelo 4", novo.Name);
        Assert.Equal(3, biblioteca.Types.Count);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void NomeDadoPeloUsuarioNaoEntraNaContagem()
    {
        var biblioteca = new StringLibrary([]);
        var um = biblioteca.Add(Uma28());
        var dois = biblioteca.Add(Uma28());

        biblioteca.Rename(um.Id, "Leste 28");
        biblioteca.Remove(dois.Id);

        // "Leste 28" não é do padrão; "Modelo 2" saiu e não volta.
        Assert.Equal("Modelo 1", biblioteca.Add(Uma28()).Name);
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void RenomearRecusaVazioERepetido()
    {
        var biblioteca = new StringLibrary([]);
        var a = biblioteca.Add(Uma28());
        var b = biblioteca.Add(Duas14());

        Assert.Null(biblioteca.Rename(a.Id, "Leste 28"));
        Assert.Equal("Leste 28", biblioteca.Find(a.Id)!.Name);

        Assert.NotNull(biblioteca.Rename(b.Id, "  "));
        Assert.NotNull(biblioteca.Rename(b.Id, "leste 28"));   // repetido, sem olhar maiúscula
        Assert.Equal("Modelo 2", biblioteca.Find(b.Id)!.Name);
        Assert.NotNull(biblioteca.Rename(Guid.NewGuid(), "Outro"));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void OTipoVaiEVoltaDosCamposDoDesenho()
    {
        var biblioteca = new StringLibrary([]);
        var a = biblioteca.Add(Duas14());
        biblioteca.Add(StringArrangement.Empty);
        biblioteca.Rename(a.Id, "Duas | de 14; oeste");

        var lidos = biblioteca.Types.Select(t => StringType.Parse(t.ToFields())!).ToList();

        Assert.Equal(biblioteca.Types, lidos);
        Assert.True(lidos[1].Arrangement.IsEmpty);
        Assert.Equal(StringType.FieldCount, a.ToFields().Count);
    }

    public static TheoryData<string[]> CamposEstragados => new()
    {
        new[] { "nao-e-guid", "Modelo 1", "14x2" },
        new[] { "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "", "14x2" },
        new[] { "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "Modelo 1", "14x0" },
        new[] { "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "Modelo 1", "quatorze" },
        new[] { "6f9619ff-8b86-d011-b42d-00cf4fc964ff", "Modelo 1" },
    };

    [Theory]
    [Trait("Etapa", "11")]
    [MemberData(nameof(CamposEstragados))]
    public void CampoEstragadoNaoViraTipo(string[] campos) =>
        Assert.Null(StringType.Parse(campos));

    [Fact]
    [Trait("Etapa", "11")]
    public void AAssinaturaDistingueUmaDe28DeDuasDe14()
    {
        Assert.NotEqual(Uma28(), Duas14());
        Assert.Equal(28, Uma28().ModuleCount);
        Assert.Equal(28, Duas14().ModuleCount);
        Assert.Equal(2, Duas14().Tables.Count);
        Assert.Equal("14x2", Uma28().ToText());
        Assert.Equal("7x2;7x2", Duas14().ToText());
        Assert.Equal(Duas14(), StringArrangement.Parse("7x2;7x2"));
        Assert.Equal(StringArrangement.Empty, StringArrangement.Parse(""));
    }

    [Fact]
    [Trait("Etapa", "11")]
    public void ONomePadraoSaiNoIdiomaDaTela()
    {
        using var _ = Tr.Use(UiLanguage.English, new Dictionary<string, string> { ["Modelo {0}"] = "Model {0}" });
        var biblioteca = new StringLibrary([]);

        Assert.Equal("Model 1", biblioteca.Add(Uma28()).Name);
    }
}
