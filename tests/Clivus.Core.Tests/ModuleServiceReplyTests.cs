namespace Clivus.Core.Tests;

/// <summary>
/// A resposta do servidor ao cadastro de um módulo (passo 38.8): no de
/// produção o módulo fica pendente até a gestão validar o .PAN, e o erro vem
/// em <c>{"erro"}</c>; no serviço local, entra na hora, e o erro vem no
/// "detail" do FastAPI.
/// </summary>
public class ModuleServiceReplyTests
{
    /// <summary>A resposta 201 do servidor de produção, como o clivussolar devolve (lista + situação).</summary>
    private static string RespostaDeProducao() =>
        File.ReadAllText(Path.Combine(Repositorio.Raiz, "tests", "dados", "servidor-modulo-pendente.json"));

    [Fact]
    [Trait("Etapa", "38")]
    public void ARespostaDeProducaoDizPendenteEGuardouOPan()
    {
        var resposta = RespostaDeProducao();

        Assert.True(ModuleServiceReply.IsPending(resposta));
        Assert.True(ModuleLibrary.ServiceKeptPan(resposta));
        Assert.Null(ModuleServiceReply.ErrorMessage(resposta));
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("{\"id\": \"a\", \"modelo\": \"X\", \"tem_pan\": true}")]
    [InlineData("{\"situacao\": \"aprovado\"}")]
    [InlineData("{\"situacao\": null}")]
    [InlineData("{\"situacao\": 1}")]
    [InlineData("[{\"situacao\": \"pendente\"}]")]
    [InlineData("<html>502 Bad Gateway</html>")]
    [InlineData("")]
    public void SemSituacaoPendenteOModuloNaoEstaPendente(string resposta)
    {
        Assert.False(ModuleServiceReply.IsPending(resposta));
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("{\"erro\": \"Já existe um módulo JAM66D46-720/LB.\"}", "Já existe um módulo JAM66D46-720/LB.")]
    [InlineData("{\"detail\": \"Já existe um módulo com esse modelo.\"}", "Já existe um módulo com esse modelo.")]
    [InlineData("{\"detail\": [{\"loc\": [\"body\", \"altura_m\"], \"msg\": \"Value error, maior que 3\"}]}", "altura_m: maior que 3")]
    [InlineData("{\"detail\": [{\"loc\": [\"body\"], \"msg\": \"Value error, a largura está maior que a altura\"}]}", "a largura está maior que a altura")]
    [InlineData("{\"detail\": [{\"loc\": [\"body\", \"voc\"], \"msg\": \"a\"}, {\"msg\": \"b\"}]}", "voc: a; b")]
    public void AMensagemDeErroSaiDosDoisServidores(string resposta, string mensagem)
    {
        Assert.Equal(mensagem, ModuleServiceReply.ErrorMessage(resposta));
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("<html>502 Bad Gateway</html>")]
    [InlineData("{}")]
    [InlineData("{\"erro\": \"\"}")]
    [InlineData("{\"erro\": 3}")]
    [InlineData("{\"detail\": []}")]
    [InlineData("{\"detail\": [{\"loc\": [\"body\"]}]}")]
    [InlineData("[\"erro\"]")]
    public void SemMensagemDeErroVoltaNull(string resposta)
    {
        Assert.Null(ModuleServiceReply.ErrorMessage(resposta));
    }
}
