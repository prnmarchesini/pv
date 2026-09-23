namespace UFV.Core.Tests;

/// <summary>
/// O formato das tabelas de registro do plugin.
///
/// As três garantias testadas aqui foram exigidas pela revisão da etapa 2, e
/// até a revisão do 4.2 elas **não tinham teste em nível nenhum**: moravam
/// dentro do registro de áreas, no plugin, onde nenhum teste de nível 1
/// alcança. Quando o formato foi extraído para ser reaproveitado pelo
/// alinhamento, a única conferência possível foi leitura — o revisor teve que
/// reimplementar os dois algoritmos fora do repositório para comparar.
///
/// Trazer o formato para o Core resolve isso: ele é texto virando lista, não
/// tem nada de CAD, e agora as garantias são travadas por teste.
/// </summary>
public class RecordTableTests
{
    private sealed record Bicho(string Nome, int Patas);

    private const int Versao = 1;
    private const int CamposPorBicho = 2;
    private const string OQueE = "de bichos";

    private static IReadOnlyList<string> Campos(Bicho b) => [b.Nome, b.Patas.ToString()];

    private static Bicho? Montar(IReadOnlyList<string> campos) =>
        int.TryParse(campos[1], out var patas) && campos[0].Length > 0
            ? new Bicho(campos[0], patas)
            : null;

    private static RecordTableResult<Bicho> Ler(IReadOnlyList<string>? texto) =>
        RecordTable.Read(texto, Versao, CamposPorBicho, Montar, OQueE);

    private static IReadOnlyList<string> Gravar(params Bicho[] bichos) =>
        RecordTable.Write(Versao, CamposPorBicho, bichos, Campos);

    // ------------------------------------------------------- ida e volta

    [Fact]
    [Trait("Etapa", "4")]
    public void OQueEntraEOQueSai()
    {
        var bichos = new[] { new Bicho("gato", 4), new Bicho("galinha", 2) };

        var lido = Ler(RecordTable.Write(Versao, CamposPorBicho, bichos, Campos));

        Assert.Null(lido.Problem);
        Assert.Equal(bichos, lido.Items);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TabelaVaziaNaoEProblema()
    {
        var lido = Ler(Gravar());

        Assert.Null(lido.Problem);
        Assert.Empty(lido.Items);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void NadaGravadoNaoEProblema()
    {
        var lido = Ler(null);

        Assert.Null(lido.Problem);
        Assert.Empty(lido.Items);
    }

    /// <summary>
    /// O cabeçalho é o formato: versão, número, marca de quantidade, número.
    /// Este teste o crava, porque mudá-lo quebra todo registro já gravado em
    /// desenho de projeto.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void OCabecalhoTemQuatroCamposNaOrdemCombinada()
    {
        var texto = Gravar(new Bicho("gato", 4));

        Assert.Equal("FORMATO", texto[0]);
        Assert.Equal("1", texto[1]);
        Assert.Equal("QUANTIDADE", texto[2]);
        Assert.Equal("1", texto[3]);
        Assert.Equal("gato", texto[4]);
        Assert.Equal("4", texto[5]);
    }

    // ------------------------------- garantia 1: versão diferente é recusada

    /// <summary>
    /// Versão diferente é recusada sem ler item nenhum. Ler pela metade
    /// entregaria uma lista parecida com a que foi gravada, e parecida é o
    /// pior resultado possível.
    /// </summary>
    [Theory]
    [Trait("Etapa", "4")]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("nada")]
    public void VersaoDiferenteERecusadaSemLerItem(string versao)
    {
        var texto = Gravar(new Bicho("gato", 4)).ToList();
        texto[1] = versao;

        var lido = Ler(texto);

        Assert.NotNull(lido.Problem);
        Assert.Contains("outra versão", lido.Problem!);
        Assert.Empty(lido.Items);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void CabecalhoTrocadoERecusado()
    {
        var texto = Gravar(new Bicho("gato", 4)).ToList();
        texto[0] = "OUTRACOISA";

        Assert.Contains("cabeçalho", Ler(texto).Problem!);
    }

    /// <summary>
    /// O achado da revisão do 4.2: quando o terceiro campo do cabeçalho
    /// corrompia, a conferência de quantidade era simplesmente pulada — e a
    /// tabela voltava truncada, sem problema nenhum relatado.
    ///
    /// Ou seja, a garantia contra truncamento sumia exatamente quando o
    /// cabeçalho estava corrompido, que é quando ela mais importa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void MarcaDeQuantidadeCorrompidaERecusada()
    {
        var texto = Gravar(new Bicho("gato", 4), new Bicho("galinha", 2)).ToList();
        texto[2] = "LIXO";

        var lido = Ler(texto);

        Assert.NotNull(lido.Problem);
        Assert.Contains("cabeçalho", lido.Problem!);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void TabelaTruncadaNoCabecalhoERecusada()
    {
        Assert.Contains("truncado", Ler(["FORMATO", "1"]).Problem!);
        Assert.Contains("truncado", Ler([]).Problem!);
    }

    // --------------------- garantia 2: entrada ilegível nunca some calada

    [Fact]
    [Trait("Etapa", "4")]
    public void EntradaIlegivelERelatadaENaoSomeCalada()
    {
        var texto = Gravar(new Bicho("gato", 4), new Bicho("galinha", 2)).ToList();
        texto[5] = "patas de menos";

        var lido = Ler(texto);

        Assert.NotNull(lido.Problem);
        Assert.Contains("1 entrada", lido.Problem!);

        // O que deu para ler volta: perder o resto seria pior.
        Assert.Single(lido.Items);
    }

    // ------------- garantia 3: a quantidade declarada denuncia truncamento

    /// <summary>
    /// Sem conferir a quantidade, um registro cortado pela metade devolveria
    /// os itens que sobraram sem ninguém notar a falta dos outros.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void RegistroCortadoPelaMetadeEDenunciado()
    {
        var texto = Gravar(new Bicho("gato", 4), new Bicho("galinha", 2)).ToList();

        // Some o último bicho, mas o cabeçalho continua dizendo dois.
        texto.RemoveRange(texto.Count - 2, 2);

        var lido = Ler(texto);

        Assert.NotNull(lido.Problem);
        Assert.Contains("diz ter 2", lido.Problem!);
        Assert.Single(lido.Items);
    }

    /// <summary>
    /// Campo solto no fim, que não completa um item, não vira item nenhum — e
    /// a quantidade o denuncia.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void CampoSoltoNoFimNaoViraItem()
    {
        var texto = Gravar(new Bicho("gato", 4)).ToList();
        texto.Add("sobra");

        var lido = Ler(texto);

        Assert.Single(lido.Items);
        Assert.Null(lido.Problem);
    }

    // ----------------- o contrato de quantos campos cada item ocupa

    /// <summary>
    /// O outro achado da revisão do 4.2: a quantidade de campos por item
    /// morava em dois lugares soltos — a constante da leitura e o vetor da
    /// gravação —, ligados só por convenção. Desalinhados, a tabela inteira
    /// virava "entradas ilegíveis" e o índice se perdia.
    /// </summary>
    [Fact]
    [Trait("Etapa", "4")]
    public void GravarComCamposDeMaisOuDeMenosERecusado()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => RecordTable.Write<Bicho>(Versao, 2, [new Bicho("gato", 4)], _ => ["um", "dois", "tres"]));

        Assert.Contains("2 campo(s) por item", erro.Message);
        Assert.Contains("produziu 3", erro.Message);
    }

    [Fact]
    [Trait("Etapa", "4")]
    public void LerComOutroNumeroDeCamposDenunciaEmVozAlta()
    {
        // Gravado com dois campos por item, lido como se fossem três.
        var texto = Gravar(new Bicho("gato", 4), new Bicho("galinha", 2));

        var lido = RecordTable.Read<Bicho>(texto, Versao, 3, _ => null, OQueE);

        Assert.NotNull(lido.Problem);
        Assert.Empty(lido.Items);
    }

    [Theory]
    [Trait("Etapa", "4")]
    [InlineData(0)]
    [InlineData(-1)]
    public void CamposPorItemPrecisaSerPositivo(int quantos)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecordTable.Write<Bicho>(Versao, quantos, [], Campos));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecordTable.Read<Bicho>([], Versao, quantos, Montar, OQueE));
    }

    // --------------------------------------------------------------- texto

    [Fact]
    [Trait("Etapa", "4")]
    public void OProblemaNomeiaOQueORegistroE()
    {
        var texto = Gravar(new Bicho("gato", 4)).ToList();
        texto[1] = "9";

        Assert.Contains("de bichos", Ler(texto).Problem!);
    }
}
