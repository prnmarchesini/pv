using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Manda a usina para o servidor 3D (plano/contrato-servidor-3d.md) e devolve
/// o link da página. O endereço e a chave vêm de variável de ambiente
/// (CLIVUS_SERVIDOR e CLIVUS_SERVIDOR_CHAVE), como o serviço de módulos:
/// nada de segredo no desenho nem no repositório.
/// </summary>
internal static class Publicador3D
{
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>O endereço do servidor, sem a barra do fim; null se não está configurado ou não é http(s).</summary>
    internal static string? Endereco =>
        Environment.GetEnvironmentVariable("CLIVUS_SERVIDOR") is { Length: > 0 } e
        && Uri.TryCreate(e.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.ToString().TrimEnd('/')
            : null;

    private static string? Chave => Environment.GetEnvironmentVariable("CLIVUS_SERVIDOR_CHAVE") is { Length: > 0 } c ? c.Trim() : null;

    /// <summary>Publica o corpo (já no formato do contrato). O link, ou o erro em português.</summary>
    internal static (PublishedScene? Publicada, string? Erro) Publicar(string corpo)
    {
        if (Endereco is not { } endereco) return (null, "o servidor 3D não está configurado (variável CLIVUS_SERVIDOR)");

        try
        {
            // A cena de uma área grande passa de alguns MB: vai comprimida.
            using var comprimido = new MemoryStream();
            using (var gzip = new GZipStream(comprimido, CompressionLevel.Fastest, leaveOpen: true))
                gzip.Write(Encoding.UTF8.GetBytes(corpo));

            using var conteudo = new ByteArrayContent(comprimido.ToArray());
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            conteudo.Headers.ContentEncoding.Add("gzip");

            using var pedido = new HttpRequestMessage(HttpMethod.Post, endereco + "/api/v1/cenas") { Content = conteudo };
            if (Chave is { } chave) pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);

            using var resposta = Cliente.Send(pedido);
            using var leitor = new StreamReader(resposta.Content.ReadAsStream(), Encoding.UTF8);

            return Viewer3DPage.ParseResponse((int)resposta.StatusCode, leitor.ReadToEnd());
        }
        catch (HttpRequestException erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao publicar no servidor 3D.", erro);
            return (null, $"não consegui falar com o servidor 3D em {endereco} ({erro.Message})");
        }
        catch (TaskCanceledException)
        {
            return (null, $"o servidor 3D em {endereco} não respondeu a tempo");
        }
    }
}
