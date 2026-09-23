namespace UFV.Core.Tests;

/// <summary>
/// A pasta de perfis de mesa.
///
/// O que pode dar errado aqui não é gravar: é o nome. Ele é texto livre
/// digitado pelo projetista, e "Mesa 2V / 28 módulos" tem uma barra no meio.
/// Sem tratamento, isso tenta criar pasta, ou escreve fora do lugar.
/// </summary>
public sealed class TableProfileStoreTests : IDisposable
{
    private readonly string _pasta =
        Path.Combine(Path.GetTempPath(), "ufv-perfis-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private TableProfileStore Pasta() => new(_pasta);

    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableProfile Perfil(string nome = "Mesa do Renan 28 módulos") => new(
        nome,
        new TableLayout(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        new TableFrame(3.00, 2.50, 0.15, 0.07),
        20 * Math.PI / 180);

    [Fact]
    [Trait("Etapa", "3")]
    public void OPerfilGravadoVoltaIgual()
    {
        var pasta = Pasta();
        var original = Perfil();

        pasta.Save(original);

        Assert.Equal(original, pasta.Load(original.Name));
    }

    /// <summary>
    /// Antes do primeiro perfil a pasta nem existe, e esse é o estado normal
    /// de quem acabou de instalar o plugin — não é erro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PastaQueNaoExisteDaListaVaziaENaoErro()
    {
        Assert.Empty(Pasta().List());
        Assert.False(Pasta().Exists("qualquer"));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void AListaSaiEmOrdemAlfabetica()
    {
        var pasta = Pasta();

        foreach (var nome in new[] { "Zebra", "alfa", "Mesa 2V" }) pasta.Save(Perfil(nome));

        Assert.Equal(["alfa", "Mesa 2V", "Zebra"], pasta.List());
    }

    /// <summary>
    /// Barra, dois-pontos e ponto de interrogação são nomes legítimos de mesa
    /// e nomes impossíveis de arquivo. Todos têm que voltar inteiros.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("Mesa 2V / 28 módulos")]
    [InlineData("Mesa: a do galpão")]
    [InlineData("Mesa \\ teste")]
    [InlineData("Mesa 100% ok")]
    [InlineData("Mesa *asterisco* <maior> |cano|")]
    [InlineData("Mesa ção ã é ü")]
    [InlineData("Mesa 🌞 sol")]
    [InlineData("  Mesa com espaço  ")]
    public void NomeComplicadoVoltaInteiro(string nome)
    {
        var pasta = Pasta();

        pasta.Save(Perfil(nome));

        // O nome volta EXATO, inclusive com os espaços das pontas: trimar no
        // caminho do arquivo faria " Mesa " sobrescrever "Mesa" enquanto a
        // listagem mostrava só um dos dois.
        Assert.Contains(nome, pasta.List());
        Assert.Equal(nome, pasta.Load(nome).Name);
    }

    /// <summary>
    /// Dois nomes diferentes não podem virar o mesmo arquivo. Trocar tudo o
    /// que é proibido por sublinhado faria "Mesa 2V/28" apagar "Mesa 2V-28"
    /// em silêncio — e o projetista perderia um perfil sem saber.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void DoisNomesDiferentesNaoViramOMesmoArquivo()
    {
        var pasta = Pasta();

        pasta.Save(Perfil("Mesa 2V/28"));
        pasta.Save(Perfil("Mesa 2V-28"));
        pasta.Save(Perfil("Mesa 2V_28"));

        Assert.Equal(3, pasta.List().Count);
    }

    /// <summary>
    /// O nome não pode escapar da pasta dos perfis, nem por caminho relativo.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("../fora")]
    [InlineData("..\\..\\fora")]
    [InlineData("C:\\Windows\\system32\\mesa")]
    public void NomeQueTentaSairDaPastaNaoSai(string nome)
    {
        var pasta = Pasta();

        pasta.Save(Perfil(nome));

        // Gravou dentro da pasta, e voltou com o nome que o usuário digitou.
        Assert.Single(Directory.GetFiles(_pasta));
        Assert.Equal(nome, pasta.Load(nome).Name);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void GravarDeNovoSubstituiOAnterior()
    {
        var pasta = Pasta();

        pasta.Save(Perfil());
        pasta.Save(Perfil() with { TiltRadians = 25 * Math.PI / 180 });

        Assert.Single(pasta.List());
        Assert.Equal(25, pasta.Load(Perfil().Name).TiltDegrees, 9);
    }

    /// <summary>
    /// O arquivo provisório da gravação não pode aparecer na lista: ele existe
    /// para a queda de energia não cortar um perfil pela metade, e some no
    /// fim.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AGravacaoNaoDeixaArquivoSolto()
    {
        var pasta = Pasta();

        pasta.Save(Perfil());

        Assert.Single(Directory.GetFiles(_pasta));
        Assert.EndsWith(TableProfileStore.Extensao, Directory.GetFiles(_pasta)[0]);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ApagarDizSeHaviaOQueApagar()
    {
        var pasta = Pasta();

        Assert.False(pasta.Delete("não existe"));

        pasta.Save(Perfil());

        Assert.True(pasta.Delete(Perfil().Name));
        Assert.Empty(pasta.List());
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void LerOQueNaoExisteDaErroQueDizONome()
    {
        var erro = Assert.Throws<FileNotFoundException>(() => Pasta().Load("Mesa fantasma"));

        Assert.Contains("Mesa fantasma", erro.Message);
    }

    /// <summary>
    /// Perfil que descreve uma mesa impossível não chega ao disco. Gravá-lo
    /// deixaria uma mesa impossível com nome bonito esperando alguém abrir.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilInvalidoNaoChegaAoDisco()
    {
        var pasta = Pasta();
        var impossivel = Perfil() with
        {
            Layout = new TableLayout(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        };

        Assert.Throws<InvalidOperationException>(() => pasta.Save(impossivel));
        Assert.Empty(pasta.List());
        Assert.False(Directory.Exists(_pasta));
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("")]
    [InlineData("   ")]
    public void PerfilSemNomeNaoEGravado(string nome)
    {
        Assert.Throws<InvalidOperationException>(() => Pasta().Save(Perfil(nome)));
    }

    /// <summary>
    /// Nome gigante estoura o limite de caminho do Windows, e o erro que vem
    /// de lá não diz ao projetista o que fazer. Este diz.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void NomeGiganteERecusadoComOMotivo()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => Pasta().Save(Perfil(new string('m', TableProfileStore.MaiorNome + 1))));

        Assert.Contains(TableProfileStore.MaiorNome.ToString(), erro.Message);
    }

    /// <summary>
    /// Arquivo estragado na pasta dá erro claro, e não uma mesa estranha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ArquivoEstragadoDaErroClaro()
    {
        var pasta = Pasta();

        pasta.Save(Perfil());

        var arquivo = Directory.GetFiles(_pasta)[0];
        File.WriteAllText(arquivo, "{ isto nao e perfil }");

        Assert.Throws<InvalidOperationException>(() => pasta.Load(Perfil().Name));
    }

    /// <summary>
    /// O Windows não distingue maiúscula de minúscula no nome de arquivo; o
    /// escape distingue. Sem conferir, salvar "mesa" apagava "Mesa" em
    /// silêncio — e pedir "Mesa" de volta devolvia "mesa".
    ///
    /// É o mesmo desastre que a regra dos nomes diferentes existe para
    /// impedir, por um caminho que ela não cobria.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PerfilQueSoDifereNaCaixaERecusado()
    {
        var pasta = Pasta();

        pasta.Save(Perfil("Mesa do galpão"));

        var erro = Assert.Throws<InvalidOperationException>(
            () => pasta.Save(Perfil("MESA DO GALPÃO")));

        Assert.Contains("Mesa do galpão", erro.Message);
        Assert.Contains("maiúsculas", erro.Message);

        // E o perfil original continua inteiro.
        Assert.Equal(["Mesa do galpão"], pasta.List());
    }

    /// <summary>
    /// Regravar o MESMO perfil não é colisão: é substituição legítima.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void RegravarOMesmoNomeNaoEColisao()
    {
        var pasta = Pasta();

        pasta.Save(Perfil("Mesa do galpão"));
        pasta.Save(Perfil("Mesa do galpão"));

        Assert.Single(pasta.List());
    }

    /// <summary>
    /// Espaço nas pontas faz nome diferente, e o Windows corta espaço no fim
    /// do nome de arquivo sem avisar. Sem escapá-lo, os dois viravam o mesmo
    /// arquivo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void EspacoNasPontasNaoColideComONomeSemEspaco()
    {
        var pasta = Pasta();

        pasta.Save(Perfil("Mesa"));
        pasta.Save(Perfil(" Mesa "));

        Assert.Equal(2, pasta.List().Count);
        Assert.Equal("Mesa", pasta.Load("Mesa").Name);
        Assert.Equal(" Mesa ", pasta.Load(" Mesa ").Name);
    }

    /// <summary>
    /// Um nome curto pode virar um arquivo enorme: cada caractere convertido
    /// ocupa cinco. Cento e vinte barras passam pelo limite do nome e viram
    /// seiscentos caracteres de arquivo — e o erro que o Windows devolve não
    /// diz nada ao projetista.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void NomeCurtoQueViraArquivoEnormeERecusadoComOMotivo()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => Pasta().Save(Perfil(new string('/', TableProfileStore.MaiorNome))));

        Assert.Contains("nome mais simples", erro.Message);
    }

    /// <summary>
    /// Pasta com separador no fim é caminho legítimo. Sem tratar, a
    /// conferência de segurança disparava em TODO nome, acusando o nome pelo
    /// erro de quem montou o caminho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void PastaComBarraNoFimFuncionaIgual()
    {
        var pasta = new TableProfileStore(_pasta + Path.DirectorySeparatorChar);

        pasta.Save(Perfil());

        Assert.Single(pasta.List());
        Assert.True(pasta.Exists(Perfil().Name));
        Assert.Equal(Perfil(), pasta.Load(Perfil().Name));
    }

    /// <summary>
    /// Dois Civil 3D salvando o mesmo perfil ao mesmo tempo. Com um nome fixo
    /// de arquivo provisório, eles brigavam pelo mesmo arquivo — e na pior
    /// janela um publicava o arquivo pela metade do outro por cima do perfil
    /// bom, que é o que o escrever-e-trocar existia para impedir.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void GravacoesAoMesmoTempoNaoBrigamPeloMesmoArquivo()
    {
        var pasta = Pasta();
        var falhas = 0;

        Parallel.For(0, 64, _ =>
        {
            try
            {
                pasta.Save(Perfil());
            }
            catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
            {
                Interlocked.Increment(ref falhas);
            }
        });

        Assert.Equal(0, falhas);

        // E o perfil no disco está inteiro, não cortado pela metade.
        Assert.Equal(Perfil(), pasta.Load(Perfil().Name));

        // Nenhum provisório ficou para trás.
        Assert.Single(Directory.GetFiles(_pasta));
    }
}
