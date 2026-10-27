using Clivus.Core;

namespace Clivus.Core.Tests;

public class TermoVisionLinkTests
{
    private const string Endereco =
        "clivus://termovision?v=1&servidor=https%3A%2F%2Fapp.termovision.tech&envio=cv_abc123&token=tvc_SeGrEdO-9";

    [Fact]
    [Trait("Etapa", "33")]
    public void Endereco_do_contrato_e_lido()
    {
        Assert.True(TermoVisionLink.TryParse(Endereco, out var link, out var erro), erro);
        Assert.Equal("https://app.termovision.tech", link!.Server);
        Assert.Equal("cv_abc123", link.Shipment);
        Assert.Equal("tvc_SeGrEdO-9", link.Token);
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("  \"" + Endereco + "\"  ")]
    [InlineData("<" + Endereco + ">\r\n")]
    [InlineData("CLIVUS://TermoVision?token=tvc_SeGrEdO-9&envio=cv_abc123&servidor=https://app.termovision.tech/&v=1")]
    public void Colado_com_sobra_ou_fora_de_ordem_e_lido(string texto)
    {
        Assert.True(TermoVisionLink.TryParse(texto, out var link, out var erro), erro);
        Assert.Equal("https://app.termovision.tech", link!.Server);
        Assert.Equal("cv_abc123", link.Shipment);
        Assert.Equal("tvc_SeGrEdO-9", link.Token);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Servidor_de_teste_na_maquina_aceita_http()
    {
        var texto = "clivus://termovision?v=1&servidor=http%3A%2F%2F127.0.0.1%3A8731&envio=cv_1&token=tvc_1";
        Assert.True(TermoVisionLink.TryParse(texto, out var link, out var erro), erro);
        Assert.Equal("http://127.0.0.1:8731", link!.Server);
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("", "vazio")]
    [InlineData("https://app.termovision.tech", "não é um endereço")]
    [InlineData("clivus://outro?v=1", "não é um endereço")]
    [InlineData("clivus://termovision?servidor=https%3A%2F%2Fa.b&envio=cv_1&token=tvc_1", "versão ?")]
    [InlineData("clivus://termovision?v=2&servidor=https%3A%2F%2Fa.b&envio=cv_1&token=tvc_1", "versão 2")]
    [InlineData("clivus://termovision?v=1&envio=cv_1&token=tvc_1", "Falta o servidor")]
    [InlineData("clivus://termovision?v=1&servidor=http%3A%2F%2Fa.b&envio=cv_1&token=tvc_1", "https://")]
    [InlineData("clivus://termovision?v=1&servidor=ftp%3A%2F%2Fa.b&envio=cv_1&token=tvc_1", "não é um endereço web")]
    [InlineData("clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&token=tvc_1", "envio")]
    [InlineData("clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&envio=xx_1&token=tvc_1", "envio")]
    [InlineData("clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&envio=cv_1", "token")]
    [InlineData("clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&envio=cv_1&token=tvc_", "token")]
    [InlineData("clivus://termovision?v=1&servidor=https%3A%2F%2Fa.b&envio=cv_1&token=tvc_a%20b", "caractere inválido")]
    public void Endereco_que_nao_serve_diz_o_motivo(string texto, string trecho)
    {
        using var _ = Tr.Use(UiLanguage.Portuguese);
        Assert.False(TermoVisionLink.TryParse(texto, out var link, out var erro));
        Assert.Null(link);
        Assert.Contains(trecho, erro);
    }

    [Fact]
    [Trait("Etapa", "33")]
    public void Texto_do_endereco_nunca_mostra_o_token()
    {
        TermoVisionLink.TryParse(Endereco, out var link, out _);
        Assert.DoesNotContain("tvc_", link!.ToString());
        Assert.DoesNotContain("SeGrEdO", link.ToString());
        Assert.Contains("cv_abc123", link.ToString());
    }

    [Theory]
    [Trait("Etapa", "33")]
    [InlineData("https://a.b?x=1")]
    [InlineData("https://a.b#x")]
    [InlineData("https://eu:senha@a.b")]
    public void Servidor_com_consulta_fragmento_ou_senha_e_recusado(string servidor) =>
        Assert.NotNull(TermoVisionLink.WhyServerUnsafe(servidor));
}
