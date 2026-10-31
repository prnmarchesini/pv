namespace Clivus.Core;

/// <summary>
/// Onde fica a biblioteca de módulos da janela de Mesa e se um cadastro pode
/// ir para lá (38.8; a revisão pediu a decisão fora da janela, com teste).
///
/// Com <c>CLIVUS_SERVICO</c>, é o serviço local (<c>servidor/</c>), que não
/// pede chave. Sem ela, é o servidor do Clivus (o do Publicar 3D:
/// <c>CLIVUS_SERVIDOR</c> ou o de produção) com <c>/api/v1</c>. Lá o
/// cadastro leva a chave <c>CLIVUS_SERVIDOR_CHAVE</c> ou, no PC do cliente,
/// a licença (etapa 43, <see cref="ServerAuthorization"/>), só por HTTPS, e o
/// módulo fica pendente até a gestão conferir o .PAN.
/// </summary>
/// <param name="Address">O endereço da biblioteca, sem a barra do fim; null se não há um que preste.</param>
/// <param name="IsClivus">Se é o servidor do Clivus (e não um serviço local).</param>
/// <param name="Problem">Por que não há endereço, ou null.</param>
public sealed record ModuleServer(string? Address, bool IsClivus, string? Problem)
{
    /// <param name="servicoLocal">O valor de <c>CLIVUS_SERVICO</c> (ou <c>UFV_SERVICO</c>), ou null.</param>
    /// <param name="servidorDoClivus">O endereço do servidor do Clivus, ou null se a variável dele não presta.</param>
    public static ModuleServer Resolve(string? servicoLocal, string? servidorDoClivus)
    {
        if (!string.IsNullOrWhiteSpace(servicoLocal))
        {
            var local = servicoLocal.Trim().TrimEnd('/');

            return EhWeb(local)
                ? new ModuleServer(local, false, null)
                : new ModuleServer(null, false, Tr.F("CLIVUS_SERVICO não é um endereço http: \"{0}\"", local));
        }

        if (string.IsNullOrWhiteSpace(servidorDoClivus) || !EhWeb(servidorDoClivus.Trim()))
            return new ModuleServer(null, true, Tr.T("o servidor do Clivus não está configurado (CLIVUS_SERVIDOR inválido)"));

        return new ModuleServer(servidorDoClivus.Trim().TrimEnd('/') + "/api/v1", true, null);
    }

    /// <summary>
    /// Por que um cadastro não pode ir, ou null. No servidor do Clivus a
    /// chave é obrigatória e só vai por HTTPS (http só em localhost, para
    /// teste), como no Publicar 3D (plano/seguranca.md).
    /// </summary>
    /// <param name="chave">O valor de <c>CLIVUS_SERVIDOR_CHAVE</c>, ou null.</param>
    public string? WhyCannotRegister(string? chave) =>
        PorQueNao(!string.IsNullOrWhiteSpace(chave), Tr.T("Para mandar o módulo ao servidor do Clivus falta a chave (variável CLIVUS_SERVIDOR_CHAVE)."));

    /// <summary>
    /// O mesmo com a identificação do plugin (etapa 43): no servidor do Clivus
    /// o cadastro vai com a chave ou, no PC do cliente, com a licença
    /// (<see cref="ServerAuthorization.Choose"/>); sem nenhuma das duas, não
    /// vai. A identificação só vai por HTTPS, como a chave.
    /// </summary>
    /// <param name="autorizacao">A identificação escolhida, ou null se não há.</param>
    public string? WhyCannotSend(ServerAuthorization? autorizacao) =>
        PorQueNao(autorizacao is not null, Tr.T("Para mandar o módulo ao servidor do Clivus, ative o Clivus Solar neste computador (botão Ativar) ou defina a chave (variável CLIVUS_SERVIDOR_CHAVE)."));

    /// <summary>
    /// A identificação que vai no cadastro: a do plugin, só para o servidor
    /// do Clivus; o serviço local não pede nenhuma, e a chave ou a licença
    /// não saem do computador para ele (etapa 43).
    /// </summary>
    public ServerAuthorization? CredentialFor(ServerAuthorization? autorizacao) => IsClivus && Problem is null ? autorizacao : null;

    /// <summary>A regra das duas: o endereço, o HTTPS e, no servidor do Clivus, a identificação.</summary>
    private string? PorQueNao(bool identificado, string semIdentificacao)
    {
        if (Problem is { } problema) return problema;
        if (!IsClivus) return null;
        if (Viewer3DPage.WhyServerUnsafe(Address) is { } inseguro) return inseguro;

        return identificado ? null : semIdentificacao;
    }

    private static bool EhWeb(string endereco) =>
        Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
