namespace UFV.Core.Tests;

/// <summary>
/// A identidade da área é o que permite ao plugin reconhecer uma polilinha no
/// meio de milhares. Se ela falhar, o resultado não é um erro: é uma área que
/// some da lista, ou duas áreas diferentes contadas como a mesma.
/// </summary>
public class AreaIdentityTests
{
    [Fact]
    [Trait("Etapa", "2")]
    public void CadaAreaNasceComIdentidadePropria()
    {
        var agora = new DateTime(2026, 9, 23, 10, 0, 0);

        var primeira = AreaIdentity.Create("Área 1", agora);
        var segunda = AreaIdentity.Create("Área 1", agora);

        // Mesmo nome, mesma hora: continuam sendo duas áreas.
        Assert.NotEqual(primeira.Id, segunda.Id);
        Assert.True(primeira.IsValid);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void GuidVazioNaoEIdentidade()
    {
        // É o que sobra de um XData truncado ou de um campo ilegível. Aceitar
        // faria duas áreas diferentes parecerem a mesma.
        var quebrada = new AreaIdentity(Guid.Empty, "Área", DateTime.Now);

        Assert.False(quebrada.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Etapa", "2")]
    public void AreaSemNomeAindaTemComoSerChamada(string? nome)
    {
        var area = AreaIdentity.Create(nome, DateTime.Now);

        Assert.Equal(AreaIdentity.SemNome, area.DisplayName);
        Assert.True(area.IsValid);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void EspacoEmVoltaDoNomeNaoVaiParaATela()
    {
        Assert.Equal("Área sul", AreaIdentity.Create("  Área sul  ", DateTime.Now).DisplayName);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void ADescricaoDizONomeEAData()
    {
        var area = AreaIdentity.Create("Área sul", new DateTime(2026, 9, 23, 14, 5, 0));

        Assert.Equal("Área sul (criada em 23/09/2026 14:05)", area.Describe());
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void RenomearNaoTrocaAIdentidade()
    {
        // O nome é do usuário e ele muda quando quiser; a identidade é do
        // plugin e não muda nunca. É o que faz o resultado calculado continuar
        // valendo depois de um "renomear".
        var area = AreaIdentity.Create("Área 1", DateTime.Now);
        var renomeada = area with { Name = "Área norte" };

        Assert.Equal(area.Id, renomeada.Id);
        Assert.Equal("Área norte", renomeada.DisplayName);
    }
}
