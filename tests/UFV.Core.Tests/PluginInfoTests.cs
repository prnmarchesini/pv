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
    [InlineData("+")]          // versao informativa malformada
    [InlineData("+abc123")]    // so o sufixo de build, sem versao
    [InlineData("  +abc  ")]
    [Trait("Etapa", "0")]
    public void SemVersaoLegivelDizDesconhecida(string? versao)
    {
        // Nenhuma destas pode terminar a frase no vazio ("... carregado, versão ").
        Assert.Equal("Plugin UFV carregado, versão desconhecida", PluginInfo.MensagemDeApresentacao(versao));
    }

    [Theory]
    [InlineData("0.1.0+3f2a1c9d", "0.1.0")]
    [InlineData("1.2.3-beta+abc", "1.2.3-beta")]
    [InlineData("  0.1.0  ", "0.1.0")]
    [InlineData("0.1.0 +abc", "0.1.0")]
    [Trait("Etapa", "0")]
    public void SufixoDeBuildNaoVaiParaATela(string informada, string esperada)
    {
        // O SDK grava "versao+hash" na versao informativa. O usuario le a versao,
        // nao o hash do commit.
        Assert.Equal($"Plugin UFV carregado, versão {esperada}", PluginInfo.MensagemDeApresentacao(informada));
    }

    [Fact]
    [Trait("Etapa", "0")]
    public void AMensagemNuncaTerminaNoVazio()
    {
        foreach (var entrada in new string?[] { null, "", " ", "+", "+x", "  +  ", "\t" })
        {
            var mensagem = PluginInfo.MensagemDeApresentacao(entrada);

            Assert.False(
                mensagem.TrimEnd().EndsWith("versão", StringComparison.Ordinal),
                $"A mensagem ficou sem versão para a entrada {entrada ?? "(null)"}: '{mensagem}'");
        }
    }
}
