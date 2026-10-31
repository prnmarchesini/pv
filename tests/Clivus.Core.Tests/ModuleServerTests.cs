namespace Clivus.Core.Tests;

/// <summary>
/// Onde fica a biblioteca de módulos e se um cadastro pode ir (passo 38.8,
/// revisão): o serviço local com CLIVUS_SERVICO, sem chave; sem ela, o
/// servidor do Clivus com /api/v1, que pede a chave e só por HTTPS.
/// </summary>
public class ModuleServerTests
{
    private const string Producao = "https://clivus.marcheng.com.br";

    [Fact]
    [Trait("Etapa", "38")]
    public void SemServicoLocalABibliotecaEhOServidorDoClivus()
    {
        var servidor = ModuleServer.Resolve(null, Producao);

        Assert.True(servidor.IsClivus);
        Assert.Equal(Producao + "/api/v1", servidor.Address);
        Assert.Null(servidor.Problem);
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("http://localhost:8765", "http://localhost:8765")]
    [InlineData("http://localhost:8765/", "http://localhost:8765")]
    [InlineData("  http://192.168.0.10:8765  ", "http://192.168.0.10:8765")]
    public void ComServicoLocalElePrevalece(string local, string endereco)
    {
        var servidor = ModuleServer.Resolve(local, Producao);

        Assert.False(servidor.IsClivus);
        Assert.Equal(endereco, servidor.Address);

        // O serviço local não pede chave, nem HTTPS (é a rede do escritório).
        Assert.Null(servidor.WhyCannotRegister(null));
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("localhost:8765")]
    [InlineData("ftp://localhost")]
    [InlineData("servico")]
    public void ServicoLocalQueNaoEhEnderecoWebNaoCaiCaladoNaProducao(string local)
    {
        var servidor = ModuleServer.Resolve(local, Producao);

        Assert.Null(servidor.Address);
        Assert.False(servidor.IsClivus);
        Assert.Contains("CLIVUS_SERVICO", servidor.Problem, StringComparison.Ordinal);
        Assert.NotNull(servidor.WhyCannotRegister("chave"));
    }

    /// <summary>CLIVUS_SERVIDOR malformado: a biblioteca diz que é ele, e não culpa a CLIVUS_SERVICO.</summary>
    [Theory]
    [Trait("Etapa", "38")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ftp://clivus")]
    public void ServidorDoClivusQueNaoPrestaVaiParaAEmbutidaComOMotivo(string? servidorDoClivus)
    {
        var servidor = ModuleServer.Resolve(null, servidorDoClivus);

        Assert.Null(servidor.Address);
        Assert.Contains("CLIVUS_SERVIDOR", servidor.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("CLIVUS_SERVICO", servidor.Problem, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void NoServidorDoClivusOCadastroPedeAChave()
    {
        var servidor = ModuleServer.Resolve(null, Producao);

        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", servidor.WhyCannotRegister(null), StringComparison.Ordinal);
        Assert.Contains("CLIVUS_SERVIDOR_CHAVE", servidor.WhyCannotRegister("   "), StringComparison.Ordinal);
        Assert.Null(servidor.WhyCannotRegister("chave"));
    }

    /// <summary>A chave só vai por HTTPS; http, só para um servidor de teste em localhost.</summary>
    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("http://clivus.exemplo.com", false)]
    [InlineData("http://177.153.20.214", false)]
    [InlineData("http://localhost:8000", true)]
    [InlineData("http://127.0.0.1:8000", true)]
    [InlineData("https://clivus.exemplo.com", true)]
    public void AChaveSoVaiPorHttps(string servidorDoClivus, bool vai)
    {
        var motivo = ModuleServer.Resolve(null, servidorDoClivus).WhyCannotRegister("chave");

        if (vai)
            Assert.Null(motivo);
        else
            Assert.Contains("HTTPS", motivo, StringComparison.Ordinal);
    }

    // ------------------------------------------------- o .PAN que não cabe

    /// <summary>
    /// O .PAN maior que o que os servidores guardam é recusado antes de subir
    /// (revisão do 38.8: chegava ao 413 no meio do envio, como conexão cortada).
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OPanMaiorQueOServidorGuardaNaoSobe()
    {
        var modulo = new SolarModule("JA Solar", "JAM66D46-720/LB", 720, 2.384, 1.303, 0.033);

        var noLimite = ModuleLibrary.ToServiceJson(modulo, pan: new string('x', ModuleLibrary.MaiorPan), panNome: "a.pan");
        Assert.Contains("\"pan\"", noLimite, StringComparison.Ordinal);

        var erro = Assert.Throws<InvalidOperationException>(() =>
            ModuleLibrary.ToServiceJson(modulo, pan: new string('x', ModuleLibrary.MaiorPan + 1), panNome: "a.pan"));
        Assert.Contains("500", erro.Message, StringComparison.Ordinal);
    }
}
