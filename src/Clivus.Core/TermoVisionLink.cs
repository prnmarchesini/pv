namespace Clivus.Core;

/// <summary>
/// O endereço que o TermoVision abre para mandar uma área ao Clivus:
/// <c>clivus://termovision?v=1&amp;servidor=…&amp;envio=cv_…&amp;token=tvc_…</c>
/// (contrato em <c>plano/termovision/contrato-clivus.md</c>).
///
/// O token grava mesas na usina por 7 dias: ele nunca aparece em
/// <see cref="ToString"/>, que é o que vai para mensagem e registro.
/// </summary>
/// <param name="Server">O endereço do servidor, sem barra no fim.</param>
/// <param name="Shipment">O id do envio (<c>cv_…</c>).</param>
/// <param name="Token">O token do envio (<c>tvc_…</c>).</param>
public sealed record TermoVisionLink(string Server, string Shipment, string Token)
{
    /// <summary>A única versão do contrato que este plugin entende.</summary>
    public const string Version = "1";

    /// <summary>
    /// Lê o endereço como veio do Windows ou colado pela pessoa (com espaços ou
    /// aspas em volta). Falso com o motivo em português quando não serve.
    /// </summary>
    public static bool TryParse(string? texto, out TermoVisionLink? link, out string? erro)
    {
        link = null;
        erro = null;

        var limpo = (texto ?? "").Trim().Trim('"', '\'', '<', '>').Trim();
        if (limpo.Length == 0)
        {
            erro = Tr.T("O endereço do TermoVision está vazio.");
            return false;
        }

        if (!Uri.TryCreate(limpo, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "clivus", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "termovision", StringComparison.OrdinalIgnoreCase))
        {
            erro = Tr.T("Isto não é um endereço do TermoVision (clivus://termovision?...).");
            return false;
        }

        var partes = Query(uri.Query);

        if (!partes.TryGetValue("v", out var versao) || versao != Version)
        {
            erro = Tr.F("O endereço é da versão {0} do TermoVision; este Clivus entende a versão 1. Atualize o Clivus.",
                string.IsNullOrEmpty(versao) ? "?" : versao);
            return false;
        }

        if (!partes.TryGetValue("servidor", out var servidor) || servidor.Length == 0)
        {
            erro = Tr.T("Falta o servidor no endereço do TermoVision.");
            return false;
        }

        if (WhyServerUnsafe(servidor) is { } porque)
        {
            erro = porque;
            return false;
        }

        if (!partes.TryGetValue("envio", out var envio) || !envio.StartsWith("cv_", StringComparison.Ordinal) || envio.Length < 4)
        {
            erro = Tr.T("Falta o envio (cv_...) no endereço do TermoVision.");
            return false;
        }

        if (!partes.TryGetValue("token", out var token) || !token.StartsWith("tvc_", StringComparison.Ordinal) || token.Length < 5)
        {
            erro = Tr.T("Falta o token (tvc_...) no endereço do TermoVision.");
            return false;
        }

        if (!SoVisivel(envio) || !SoVisivel(token))
        {
            erro = Tr.T("O envio ou o token do endereço do TermoVision tem caractere inválido.");
            return false;
        }

        link = new TermoVisionLink(servidor.TrimEnd('/'), envio, token);
        return true;
    }

    /// <summary>
    /// Por que o servidor não serve (nulo quando serve): só HTTPS, com a
    /// exceção do servidor de teste na própria máquina.
    /// </summary>
    public static string? WhyServerUnsafe(string servidor)
    {
        if (!Uri.TryCreate(servidor, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return Tr.T("O servidor do endereço do TermoVision não é um endereço web válido.");

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            return Tr.T("O servidor do TermoVision precisa ser https:// (http só na própria máquina, para teste).");

        return null;
    }

    /// <summary>O endereço sem o token: para mensagem e registro.</summary>
    public override string ToString() => $"clivus://termovision?v={Version}&servidor={Server}&envio={Shipment}";

    // Uri.Query vem com o "?" e os valores ainda codificados; o "+" fica "+"
    // (encodeURIComponent nunca gera "+" para espaço).
    private static Dictionary<string, string> Query(string query)
    {
        var partes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var par in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var igual = par.IndexOf('=');
            var nome = Uri.UnescapeDataString(igual < 0 ? par : par[..igual]).Trim();
            var valor = igual < 0 ? "" : Uri.UnescapeDataString(par[(igual + 1)..]).Trim();
            if (nome.Length > 0 && !partes.ContainsKey(nome)) partes[nome] = valor;
        }
        return partes;
    }

    private static bool SoVisivel(string valor) => valor.All(c => c > ' ' && c < 127);
}
