namespace UFV.Core.Tests;

/// <summary>
/// A tabela de pilares diz onde cada pilar fica ao longo da mesa, contando do
/// zero da estrutura.
///
/// Os vãos são desiguais de propósito: o projeto do fabricante não distribui
/// pilar igualzinho, e forçar isso seria inventar estrutura. O que não pode é a
/// soma dos vãos não bater com o comprimento da mesa — aí a tabela descreve
/// uma mesa que não é aquela, e o erro só aparece no campo.
/// </summary>
public class PillarTableTests
{
    /// <summary>
    /// O exemplo do plano: vãos 3, 3, 3, 4, 3 somam 16 m e dão 6 pilares nas
    /// posições 0, 3, 6, 9, 13 e 16.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsPosicoesSaoAsDistanciasAcumuladasDoZero()
    {
        var tabela = new PillarTable([3, 3, 3, 4, 3]);

        Assert.True(tabela.IsValid);
        Assert.Equal([0.0, 3, 6, 9, 13, 16], tabela.Positions);
    }

    /// <summary>
    /// Um pilar por posição: n vãos dão n+1 pilares. Errar isto para menos
    /// deixa a mesa em balanço numa das pontas, e a lista de compra vem curta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void SaoSempreUmPilarAMaisQueOsVaos()
    {
        Assert.Equal(6, new PillarTable([3, 3, 3, 4, 3]).PillarCount);
        Assert.Equal(2, new PillarTable([3]).PillarCount);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void OTotalEASomaDosVaos()
    {
        Assert.Equal(16, new PillarTable([3, 3, 3, 4, 3]).TotalSpan, 9);
    }

    /// <summary>
    /// A validação que o plano pede: a tabela fecha com o comprimento da mesa,
    /// ou não serve para ela.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATabelaQueFechaComOComprimentoServe()
    {
        Assert.Null(new PillarTable([3, 3, 3, 4, 3]).WhyDoesNotFit(16));
    }

    /// <summary>
    /// E a que não fecha dá erro claro, com os dois números e a diferença.
    /// "Tabela inválida" sozinho mandaria o usuário procurar no escuro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATabelaQueNaoFechaDizOsDoisNumerosEADiferenca()
    {
        var tabela = new PillarTable([3, 3, 3, 4, 3]);
        var curta = tabela.WhyDoesNotFit(18.702);

        Assert.NotNull(curta);
        Assert.Contains("16", curta!);
        Assert.Contains("18,702", curta);
        Assert.Contains("2,702", curta);

        // O sinal importa mais que os números: invertê-lo manda o usuário
        // alongar a mesa quando ela precisa encurtar. Sem esta asserção a
        // troca passava verde.
        Assert.Contains("falta", curta);

        var comprida = tabela.WhyDoesNotFit(12);

        Assert.NotNull(comprida);
        Assert.Contains("sobra", comprida!);
    }

    /// <summary>
    /// Um milímetro de diferença é arredondamento de quem digitou, não erro de
    /// projeto. É a mesma tolerância com que o drapeamento enxuga vértice.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(16.0005, null)]
    [InlineData(15.9995, null)]
    // Cinco milímetros: passa se a tolerância for afrouxada para um
    // centímetro, e é esse afrouxamento que o teste existe para pegar. Com
    // 5 cm, como estava antes, o teste reprovava nos dois casos e não
    // travava nada.
    [InlineData(16.005, "não fecha")]
    [InlineData(15.995, "não fecha")]
    public void UmMilimetroDeDiferencaEToleradoEUmCentimetroNao(double comprimento, string? esperado)
    {
        var motivo = new PillarTable([3, 3, 3, 4, 3]).WhyDoesNotFit(comprimento);

        if (esperado is null) Assert.Null(motivo);
        else Assert.Contains(esperado, motivo!);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TabelaSemVaoNaoEValida()
    {
        var tabela = new PillarTable([]);

        Assert.False(tabela.IsValid);
        Assert.Contains("vão", tabela.WhyInvalid!);
    }

    /// <summary>
    /// Vão zero poria dois pilares no mesmo lugar; negativo andaria para trás.
    /// Os dois passariam pela soma se ela fosse a única conferência.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void VaoImpossivelNaoEValido(double ruim)
    {
        var tabela = new PillarTable([3, ruim, 3]);

        Assert.False(tabela.IsValid);

        // O motivo diz QUAL vão está errado: numa tabela de doze, procurar no
        // escuro é o que faz o usuário desistir.
        Assert.Contains("vão 2", tabela.WhyInvalid!);
    }

    /// <summary>
    /// Vão maior que o razoável é erro de escala, como no módulo e na mesa:
    /// quem digitou 300 achando centímetro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void VaoGrandeDemaisNaoEValido()
    {
        Assert.False(new PillarTable([3, 300, 3]).IsValid);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TabelaInvalidaNaoDevolvePosicaoNemTotal()
    {
        var tabela = new PillarTable([3, -3]);

        Assert.Throws<InvalidOperationException>(() => tabela.Positions);
        Assert.Throws<InvalidOperationException>(() => tabela.TotalSpan);
        Assert.Throws<InvalidOperationException>(() => tabela.PillarCount);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TabelaNulaNaoExplode()
    {
        var tabela = new PillarTable(null!);

        Assert.False(tabela.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(tabela.WhyInvalid));
    }

    /// <summary>
    /// As posições sobem sempre. É o que garante que a tabela descreve uma
    /// mesa e não um vaivém.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsPosicoesSobemSempre()
    {
        var posicoes = new PillarTable([3, 1.5, 4, 0.8, 3]).Positions;

        for (var i = 1; i < posicoes.Count; i++)
        {
            Assert.True(posicoes[i] > posicoes[i - 1]);
        }
    }

    /// <summary>
    /// A distribuição automática existe porque o Renan pediu: "pilares com 3 m
    /// de distanciamento, não precisa ser 3 m cravado, provavelmente vai dar
    /// quebrado". Ela é ponto de partida, não imposição — a tabela continua
    /// editável vão a vão.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ADistribuicaoAutomaticaFechaComOComprimento()
    {
        // A mesa 2V do Renan: 18,702 m com alvo de 3 m.
        var tabela = PillarTable.Distribute(18.702, 3);

        Assert.True(tabela.IsValid);
        Assert.Null(tabela.WhyDoesNotFit(18.702));
        Assert.Equal(7, tabela.PillarCount);
        Assert.Equal(3.117, tabela.Spans[0], 6);
    }

    /// <summary>
    /// O alvo é alvo, não regra: o vão sai quebrado, e o que não pode é a soma
    /// não fechar.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(18.702, 3)]
    [InlineData(37.224, 3)]
    [InlineData(16, 3)]
    [InlineData(2.5, 3)]
    [InlineData(1, 10)]
    public void ADistribuicaoSempreFecha(double comprimento, double alvo)
    {
        var tabela = PillarTable.Distribute(comprimento, alvo);

        Assert.True(tabela.IsValid);
        Assert.Null(tabela.WhyDoesNotFit(comprimento));
    }

    /// <summary>
    /// Mesa mais curta que o alvo ainda precisa de dois pilares, um em cada
    /// ponta. Zero vão deixaria a mesa sem apoio nenhum.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaCurtaGanhaUmVaoSo()
    {
        var tabela = PillarTable.Distribute(2.5, 3);

        Assert.Equal(2, tabela.PillarCount);
        Assert.Equal(2.5, tabela.Spans[0], 9);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, 3)]
    [InlineData(-5, 3)]
    [InlineData(18.702, 0)]
    [InlineData(18.702, -1)]
    [InlineData(double.NaN, 3)]
    public void DistribuirComEntradaImpossivelRecusa(double comprimento, double alvo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarTable.Distribute(comprimento, alvo));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoTrazAContagemEOsNumerosComVirgula()
    {
        var texto = PillarTable.Distribute(18.702, 3).Describe();

        Assert.Contains("7", texto);
        Assert.Contains("3,117", texto);
        Assert.DoesNotContain("3.117", texto);
    }

    /// <summary>
    /// O buraco que a revisão do 3.3 achou: Distribute escolhia o número de
    /// vãos só pelo alvo, e podia devolver uma tabela que esta mesma classe
    /// reprova — vão de 60 m, acima do máximo. Devolver objeto inválido é pior
    /// que lançar: o chamador acha que tem tabela.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(60, 60)]
    [InlineData(120, 55)]
    [InlineData(51, 51)]
    public void ADistribuicaoNuncaDevolveTabelaQueElaMesmaReprova(double comprimento, double alvo)
    {
        var tabela = PillarTable.Distribute(comprimento, alvo);

        Assert.True(tabela.IsValid, tabela.WhyInvalid);
        Assert.Null(tabela.WhyDoesNotFit(comprimento));
    }

    /// <summary>
    /// Alvo microscópico exigiria um vetor de bilhões de posições, e o cast
    /// para int saturaria em silêncio antes disso.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(1_000_000, 0.001)]
    [InlineData(1e9, 1e-9)]
    [InlineData(1e9, 3)]
    public void ADistribuicaoRecusaOQueNaoCabeNumaTabela(double comprimento, double alvo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PillarTable.Distribute(comprimento, alvo));
    }

    /// <summary>
    /// A tabela é um record: guardar a lista do chamador por referência
    /// deixaria uma tabela válida virar inválida depois de construída.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MexerNaListaDepoisNaoMudaATabela()
    {
        var vaos = new List<double> { 3, 3 };
        var tabela = new PillarTable(vaos);

        vaos[1] = -5;

        Assert.True(tabela.IsValid);
        Assert.Equal(6, tabela.TotalSpan, 9);
    }

    /// <summary>
    /// E a lista devolvida também não pode ser a de dentro: uma escrita
    /// silenciosamente ignorada é pior que uma que falha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsPosicoesDevolvidasNaoSaoAListaDeDentro()
    {
        var tabela = new PillarTable([3, 3]);

        Assert.False(tabela.Positions is List<double>);
        Assert.Equal([0.0, 3, 6], tabela.Positions);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void DuasTabelasComOsMesmosVaosSaoIguais()
    {
        Assert.Equal(new PillarTable([3, 4]), new PillarTable([3, 4]));
        Assert.NotEqual(new PillarTable([3, 4]), new PillarTable([4, 3]));

        Assert.Equal(
            new PillarTable([3, 4]).GetHashCode(),
            new PillarTable([3, 4]).GetHashCode());
    }

    /// <summary>
    /// Justamente a tabela que se quer ver num log ou num assert que falhou é
    /// a inválida. Se o ToString dela explodir, some a informação na hora em
    /// que ela mais importa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OTextoDeUmaTabelaInvalidaNaoExplode()
    {
        var texto = new PillarTable([3, -5]).ToString();

        Assert.Contains("-5", texto);
    }

    // ------------------------------------------------------------ balanço

    /// <summary>
    /// Balanço zero é o que o plugin fazia desde sempre: pilar cravado na
    /// ponta da estrutura. Continua sendo o padrão, e é projeto legítimo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void SemBalancoOPrimeiroPilarFicaNaPonta()
    {
        var tabela = PillarTable.Distribute(18.702, 3);

        Assert.Equal(0, tabela.Cantilever, 9);
        Assert.Equal(0, tabela.Positions[0], 9);
        Assert.Equal(18.702, tabela.Positions[^1], 6);
        Assert.Equal(7, tabela.PillarCount);
    }

    /// <summary>
    /// Com balanço, a estrutura passa do pilar: o primeiro fica recuado, e o
    /// último também. É a diferença entre "onde a estrutura começa" e "onde o
    /// primeiro pilar encosta no chão".
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ComBalancoAEstruturaPassaDoPilar()
    {
        var tabela = PillarTable.Distribute(18.702, 3, 0.5);

        Assert.Equal(0.5, tabela.Cantilever, 9);
        Assert.Equal(0.5, tabela.Positions[0], 9);
        Assert.Equal(18.202, tabela.Positions[^1], 6);

        // E a tabela continua fechando com a mesa: balanço + vãos + balanço.
        Assert.Null(tabela.WhyDoesNotFit(18.702));
        Assert.Equal(18.702, tabela.TotalLength, 6);
    }

    /// <summary>
    /// Os vãos iguais cobrem o miolo, não a mesa inteira. Com 0,5 m de balanço
    /// de cada lado sobram 17,702 m para dividir — e não 18,702.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OsVaosCobremOMioloENaoAMesaInteira()
    {
        var tabela = PillarTable.Distribute(18.702, 3, 0.5);

        Assert.Equal(17.702, tabela.TotalSpan, 6);
        Assert.Equal(17.702 / 6, tabela.Spans[0], 9);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(18.702, 3, 0)]
    [InlineData(18.702, 3, 0.5)]
    [InlineData(18.702, 3, 1.75)]
    [InlineData(37.224, 3, 0.9)]
    [InlineData(5, 3, 2.4)]
    public void ADistribuicaoComBalancoSempreFecha(double comprimento, double alvo, double balanco)
    {
        var tabela = PillarTable.Distribute(comprimento, alvo, balanco);

        Assert.True(tabela.IsValid, tabela.WhyInvalid);
        Assert.Null(tabela.WhyDoesNotFit(comprimento));
    }

    /// <summary>
    /// Os dois balanços não podem comer a mesa inteira: sobraria um vão de
    /// comprimento zero ou negativo, que é pilar em cima de pilar.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(18.702, 9.351)]
    [InlineData(18.702, 12)]
    [InlineData(18.702, -0.5)]
    [InlineData(18.702, double.NaN)]
    public void BalancoImpossivelERecusado(double comprimento, double balanco)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarTable.Distribute(comprimento, 3, balanco));
    }

    /// <summary>
    /// A tabela montada à mão também carrega o balanço, e ele entra na conta
    /// do que ela cobre. Sem isso, uma tabela com balanço nunca fecharia com a
    /// mesa — a diferença seria exatamente os dois balanços.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATabelaAMaoComBalancoFechaComAMesa()
    {
        var tabela = new PillarTable([3, 3, 3, 4, 3], 0.5);

        Assert.Equal(17, tabela.TotalLength, 9);
        Assert.Null(tabela.WhyDoesNotFit(17));

        // Sem o balanço ela cobriria 16 e não fecharia com 17.
        Assert.NotNull(new PillarTable([3, 3, 3, 4, 3]).WhyDoesNotFit(17));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void AsPosicoesComBalancoComecamNoBalanco()
    {
        var tabela = new PillarTable([3, 3, 3, 4, 3], 0.5);

        Assert.Equal([0.5, 3.5, 6.5, 9.5, 13.5, 16.5], tabela.Positions);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(99)]
    public void TabelaComBalancoImpossivelNaoEValida(double balanco)
    {
        var tabela = new PillarTable([3, 3], balanco);

        Assert.False(tabela.IsValid);
        Assert.Contains("balanço", tabela.WhyInvalid!);
    }

    /// <summary>
    /// Duas tabelas com os mesmos vãos e balanços diferentes são tabelas
    /// diferentes. Sem isto, trocar o balanço passaria despercebido por
    /// qualquer comparação.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OBalancoEntraNaIgualdade()
    {
        Assert.NotEqual(new PillarTable([3, 3], 0.5), new PillarTable([3, 3]));
        Assert.Equal(new PillarTable([3, 3], 0.5), new PillarTable([3, 3], 0.5));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoDizSeHaBalanco()
    {
        Assert.Contains("pilar na ponta", PillarTable.Distribute(18.702, 3).Describe());
        Assert.Contains("balanço de 0,5 m", PillarTable.Distribute(18.702, 3, 0.5).Describe());
    }
}
