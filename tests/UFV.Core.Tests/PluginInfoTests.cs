namespace UFV.Core.Tests;

public class PluginInfoTests
{
    [Fact]
    [Trait("Etapa", "0")]
    public void MensagemTrazNomeEVersao()
    {
        Assert.Equal("Plugin UFV carregado, versão 0.1.0", PluginInfo.MensagemDeApresentacao("0.1.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Etapa", "0")]
    public void SemVersaoLegivelDizDesconhecida(string? versao)
    {
        Assert.Equal("Plugin UFV carregado, versão desconhecida", PluginInfo.MensagemDeApresentacao(versao));
    }

    [Theory]
    [InlineData("0.1.0+3f2a1c9d", "0.1.0")]
    [InlineData("1.2.3-beta+abc", "1.2.3-beta")]
    [InlineData("  0.1.0  ", "0.1.0")]
    [Trait("Etapa", "0")]
    public void SufixoDeBuildNaoVaiParaATela(string informada, string esperada)
    {
        // O SDK grava "versao+hash" na versao informativa. O usuario le a versao,
        // nao o hash do commit.
        Assert.Equal($"Plugin UFV carregado, versão {esperada}", PluginInfo.MensagemDeApresentacao(informada));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void PrefixoDeIdentidadeEOCombinado()
    {
        // 02-arquitetura.md: XData e dicionario sob MARCHENG_UFV. Mudar isso
        // orfana a identidade de todo desenho ja processado.
        Assert.Equal("MARCHENG_UFV", PluginInfo.PrefixoDeIdentidade);
    }
}
