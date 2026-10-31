namespace Clivus.Core;

/// <summary>
/// Como o plugin se identifica no servidor do Clivus, no Publicar 3D e no
/// cadastro de módulos (etapa 43, o primeiro cliente; plano/contrato-servidor-3d.md):
///
/// - com a chave <c>CLIVUS_SERVIDOR_CHAVE</c> definida (o PC do Renan, a conta
///   Develop), <c>Authorization: Bearer {chave}</c>, como sempre foi;
/// - sem a chave e com licença válida (ou passada de revalidar, que ainda
///   vale), <c>Authorization: Licenca {payload}.{assinatura}</c>, o texto do
///   licenca.txt como está; o servidor confere a assinatura dele mesmo;
/// - sem nenhuma das duas, nada: o servidor responde 401 com o motivo.
/// </summary>
/// <param name="Scheme">O esquema do cabeçalho: <see cref="BearerScheme"/> ou <see cref="LicenseScheme"/>.</param>
/// <param name="Parameter">A chave ou a licença.</param>
public sealed record ServerAuthorization(string Scheme, string Parameter)
{
    /// <summary>O esquema da chave única (conta Develop).</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>O esquema da licença do cliente (contrato da etapa 43).</summary>
    public const string LicenseScheme = "Licenca";

    /// <summary>Mais que isso não é licença do servidor: não vai num cabeçalho.</summary>
    public const int LongestLicense = 4096;

    /// <summary>Se é a licença (e não a chave).</summary>
    public bool IsLicense => Scheme == LicenseScheme;

    /// <summary>
    /// A identificação a mandar. A chave manda; sem ela, a licença, se o
    /// estado dela deixa os comandos rodarem (<see cref="LicenseState.Valid"/>
    /// ou <see cref="LicenseState.Revalidate"/>) e o texto tem a forma do
    /// contrato. Null: nenhuma.
    /// </summary>
    /// <param name="serverKey">O valor de <c>CLIVUS_SERVIDOR_CHAVE</c>, ou null.</param>
    /// <param name="license">O texto do licenca.txt, ou null.</param>
    /// <param name="licenseState">O que <see cref="License.Check"/> disse dele.</param>
    public static ServerAuthorization? Choose(string? serverKey, string? license, LicenseState licenseState)
    {
        if (!string.IsNullOrWhiteSpace(serverKey)) return new ServerAuthorization(BearerScheme, serverKey.Trim());

        if (licenseState is not (LicenseState.Valid or LicenseState.Revalidate)) return null;

        return IsLicenseText(license) ? new ServerAuthorization(LicenseScheme, license!.Trim()) : null;
    }

    /// <summary>
    /// Se o texto tem a forma da licença: <c>{payload}.{assinatura}</c>, as
    /// duas partes em base64url, sem espaço nem quebra de linha no meio (que
    /// estragariam o cabeçalho).
    /// </summary>
    public static bool IsLicenseText(string? license)
    {
        if (string.IsNullOrWhiteSpace(license)) return false;

        var texto = license.Trim();
        if (texto.Length > LongestLicense) return false;

        var ponto = texto.IndexOf('.');
        if (ponto <= 0 || ponto == texto.Length - 1 || texto.IndexOf('.', ponto + 1) >= 0) return false;

        return texto.All(c => c == '.' || c == '-' || c == '_' || char.IsAsciiLetterOrDigit(c));
    }

    /// <summary>
    /// A frase de quando o servidor recusa (401 ou 403) sem dizer o motivo
    /// (resposta fora do contrato, um proxy no caminho): o que conferir
    /// depende de com o que o plugin se identificou.
    /// </summary>
    public static string RefusedMessage(ServerAuthorization? authorization) => authorization switch
    {
        { IsLicense: true } => Tr.T("o servidor não aceitou a licença deste computador (ative de novo no botão Ativar)"),
        null => Tr.T("o servidor pede a ativação do Clivus Solar neste computador (botão Ativar)"),
        _ => Tr.T("a chave do servidor não foi aceita (confira CLIVUS_SERVIDOR_CHAVE)"),
    };
}
