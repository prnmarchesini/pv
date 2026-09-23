namespace UFV.Core.Tests;

/// <summary>
/// Escolher a superfície errada não dá erro nenhum: dá uma usina inteira
/// calculada sobre o terreno errado. Por isso a lista precisa ser estável,
/// legível, e dizer com todas as letras quando não há o que escolher.
/// </summary>
public class SurfaceCatalogTests
{
    [Fact]
    [Trait("Etapa", "1")]
    public void AListaSaiEmOrdemAlfabetica()
    {
        // A ordem em que o CAD devolve as superfícies não é estável entre
        // aberturas, e uma lista que muda de ordem sozinha faz o usuário
        // clicar na linha errada por decoreba.
        var organizada = SurfaceCatalog.Organize(
        [
            new SurfaceSummary("Terreno projetado", 500),
            new SurfaceSummary("EG", 4000),
            new SurfaceSummary("terreno natural", 12000),
        ]);

        Assert.Equal(
            ["EG", "terreno natural", "Terreno projetado"],
            organizada.Select(s => s.Name));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MaiusculaEMinusculaNaoSeparamALista()
    {
        // O par importa: numa comparação por código de caractere, "Alfa" e
        // "beta" saem nesta mesma ordem por acidente, e o teste não provaria
        // nada. "beta" antes de "Zulu" só acontece se as maiúsculas forem
        // ignoradas de verdade — por código, 'Z' (0x5A) vem antes de 'b' (0x62).
        var organizada = SurfaceCatalog.Organize(
        [
            new SurfaceSummary("Zulu", 1),
            new SurfaceSummary("beta", 1),
        ]);

        Assert.Equal(["beta", "Zulu"], organizada.Select(s => s.Name));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AcentoNaoJogaASuperficieParaOFimDaLista()
    {
        // Nome de superfície em português leva acento, e numa comparação por
        // código "Área" iria parar depois de "Azul".
        var organizada = SurfaceCatalog.Organize(
        [
            new SurfaceSummary("Azul", 1),
            new SurfaceSummary("Área", 1),
            new SurfaceSummary("Ambiente", 1),
        ]);

        Assert.Equal(["Ambiente", "Área", "Azul"], organizada.Select(s => s.Name));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AListaSaiOrdenadaPeloNomeQueAparece()
    {
        // Ordenar pelo nome cru e mostrar o nome aparado deixa a lista fora de
        // ordem aos olhos de quem lê: o espaço à esquerda jogaria "ZZZ" para o
        // topo, exibido sem o espaço.
        var organizada = SurfaceCatalog.Organize(
        [
            new SurfaceSummary("  ZZZ", 1),
            new SurfaceSummary("AAA", 1),
        ]);

        Assert.Equal(["AAA", "ZZZ"], organizada.Select(s => s.DisplayName));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OrganizarNaoPerdeNemInventaSuperficie()
    {
        var entrada = new[]
        {
            new SurfaceSummary("C", 3),
            new SurfaceSummary("A", 1),
            new SurfaceSummary("B", 2),
        };

        var organizada = SurfaceCatalog.Organize(entrada);

        Assert.Equal(entrada.Length, organizada.Count);
        Assert.Equal(entrada.OrderBy(s => s.Name), organizada);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OrganizarCarregaOQueVemJuntoDoResumo()
    {
        // No plugin, cada superfície viaja com o identificador dela no
        // desenho. Sem isso, a escolha do usuário não aponta para nada.
        var entrada = new[]
        {
            (Id: 30, Resumo: new SurfaceSummary("C", 1)),
            (Id: 10, Resumo: new SurfaceSummary("A", 1)),
            (Id: 20, Resumo: new SurfaceSummary("B", 1)),
        };

        var organizada = SurfaceCatalog.Organize(entrada, x => x.Resumo);

        Assert.Equal([10, 20, 30], organizada.Select(x => x.Id));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SemSuperficieEComReferenciaExternaAMensagemEOutra()
    {
        // A topografia costuma chegar por referência externa, e a superfície
        // de dentro de uma XRef não pertence a este desenho. Dizer só "não há
        // superfície" mandaria o usuário criar o que já existe.
        var motivo = SurfaceCatalog.WhyNothingToChoose([], temReferenciaExterna: true);

        Assert.Equal(SurfaceCatalog.NenhumaSuperficieComXref, motivo);
        Assert.Contains("referência externa", motivo);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void ListaVaziaExplicaOQueFazer()
    {
        // Não basta não mostrar nada: o usuário precisa saber que falta criar
        // a superfície no Civil 3D.
        var motivo = SurfaceCatalog.WhyNothingToChoose([]);

        Assert.Equal(SurfaceCatalog.NenhumaSuperficie, motivo);
        Assert.Contains("Civil 3D", motivo);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SuperficieVaziaNaoServeDeTerreno()
    {
        // Superfície existe, mas sem ponto nenhum: não há cota para ler.
        var vazia = new SurfaceSummary("EG", 0);

        Assert.False(vazia.CanBeTerrain);
        Assert.Equal(SurfaceCatalog.TodasVazias, SurfaceCatalog.WhyNothingToChoose([vazia]));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void UmaSuperficieBoaJaBastaParaEscolher()
    {
        var motivo = SurfaceCatalog.WhyNothingToChoose(
        [
            new SurfaceSummary("vazia", 0),
            new SurfaceSummary("boa", 4000),
        ]);

        Assert.Null(motivo);
    }

    [Theory]
    [InlineData("EG", 4000, "EG — 4.000 pontos")]
    [InlineData("Terreno natural", 1_234_567, "Terreno natural — 1.234.567 pontos")]
    [InlineData("Um ponto só", 1, "Um ponto só — 1 ponto")]
    [InlineData("Vazia", 0, "Vazia — 0 pontos")]
    [Trait("Etapa", "1")]
    public void ALinhaDaListaDizNomeEQuantidade(string nome, int pontos, string esperado)
    {
        // Separador de milhar em português: 4.000, não 4,000.
        Assert.Equal(esperado, new SurfaceSummary(nome, pontos).Describe());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("	")]
    [Trait("Etapa", "1")]
    public void SuperficieSemNomeAindaApareceNaLista(string nome)
    {
        // Sumir com ela seria pior: o usuário veria uma lista incompleta sem
        // saber por quê.
        Assert.StartsWith(SurfaceSummary.SemNome, new SurfaceSummary(nome, 10).Describe());
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void EspacoEmVoltaDoNomeNaoVaiParaATela()
    {
        Assert.Equal("EG — 10 pontos", new SurfaceSummary("  EG  ", 10).Describe());
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void DuasSuperficiesComOMesmoNomeNaoSeEmbaralham()
    {
        // Acontece com referência externa: duas superfícies homônimas no mesmo
        // desenho. A ordem entre elas precisa ser previsível.
        var organizada = SurfaceCatalog.Organize(
        [
            new SurfaceSummary("EG", 9000),
            new SurfaceSummary("EG", 100),
        ]);

        Assert.Equal([100, 9000], organizada.Select(s => s.PointCount));
    }
}
