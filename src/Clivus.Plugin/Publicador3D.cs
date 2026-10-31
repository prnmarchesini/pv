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
/// nada de segredo no desenho nem no repositório. Sem a chave (o PC do
/// cliente), o plugin se identifica pela licença (etapa 43,
/// <see cref="ServerAuthorization"/>).
/// </summary>
internal static class Publicador3D
{
    // Dez minutos: com o TIN (42.3) a cena passa de 10 MB e a internet do
    // canteiro é lenta; quem para antes é o Cancelar da janela de andamento.
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>
    /// O endereço do servidor, sem a barra do fim: a variável CLIVUS_SERVIDOR
    /// (teste) ou o endereço de produção embutido; null se nenhum. Servidor
    /// sem HTTPS (fora de localhost) é recusado no envio (plano/seguranca.md).
    /// </summary>
    internal static string? Endereco =>
        (Environment.GetEnvironmentVariable("CLIVUS_SERVIDOR") is { Length: > 0 } e ? e.Trim() : PluginInfo.ServidorDeProducao) is { Length: > 0 } endereco
        && Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : null;

    /// <summary>A chave de CLIVUS_SERVIDOR_CHAVE, ou null (em branco conta como sem chave: aí vale a licença).</summary>
    internal static string? Chave => Environment.GetEnvironmentVariable("CLIVUS_SERVIDOR_CHAVE") is { } c && !string.IsNullOrWhiteSpace(c) ? c.Trim() : null;

    /// <summary>
    /// Com o que o plugin se identifica no servidor do Clivus (3D e módulos):
    /// a chave, se definida; senão a licença válida deste PC; senão nada.
    /// </summary>
    internal static ServerAuthorization? Autorizacao()
    {
        if (Chave is { } chave) return ServerAuthorization.Choose(chave, null, LicenseState.Invalid);

        var (licenca, estado) = Licenciamento.ParaOServidor();
        return ServerAuthorization.Choose(null, licenca, estado);
    }

    /// <summary>Publica o corpo (já no formato do contrato). O link, ou o erro em português.</summary>
    internal static (PublishedScene? Publicada, string? Erro) Publicar(string corpo) =>
        PublicarAsync(corpo, null, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Publica fora da linha do AutoCAD (31/10/2026, Renan: "Comando clivus 3D
    /// trava gostoso [...] tela de exportando para web com a % e opção de
    /// cancelar, sem matar meu CAD"): comprime, manda em pedaços dizendo
    /// quanto já foi (<paramref name="andamento"/>: a fração de 0 a 1 e o
    /// texto) e para no <paramref name="cancelar"/>. O link, ou o erro em
    /// português; cancelado, <see cref="OperationCanceledException"/>.
    /// </summary>
    internal static async Task<(PublishedScene? Publicada, string? Erro)> PublicarAsync(string corpo, Action<double, string>? andamento, CancellationToken cancelar)
    {
        if (Endereco is not { } endereco) return (null, Tr.T("o servidor 3D não está configurado (variável CLIVUS_SERVIDOR)"));
        if (Viewer3DPage.WhyServerUnsafe(endereco) is { } inseguro) return (null, inseguro);

        try
        {
            // A cena de uma área grande passa de alguns MB: vai comprimida.
            andamento?.Invoke(0, Tr.T("Comprimindo a cena..."));
            using var comprimido = new MemoryStream();
            using (var gzip = new GZipStream(comprimido, CompressionLevel.Fastest, leaveOpen: true))
                gzip.Write(Encoding.UTF8.GetBytes(corpo));
            cancelar.ThrowIfCancellationRequested();

            var dados = comprimido.ToArray();
            var megas = dados.Length / 1024.0 / 1024.0;
            using var conteudo = new ConteudoComAndamento(dados, f => andamento?.Invoke(f, Tr.F("Enviando para a web: {0:0.0} de {1:0.0} MB...", f * megas, megas)));
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            conteudo.Headers.ContentEncoding.Add("gzip");

            using var pedido = new HttpRequestMessage(HttpMethod.Post, endereco + "/api/v1/cenas") { Content = conteudo };
            var autorizacao = Autorizacao();
            if (autorizacao is not null) pedido.Headers.Authorization = new AuthenticationHeaderValue(autorizacao.Scheme, autorizacao.Parameter);

            // O servidor recusa a chave (ou a licença) antes de o corpo ir: com o
            // TIN (42.3) a cena passa de 10 MB, e o 401 no meio do envio virava
            // "Error while copying content to a stream" em vez de "chave não aceita".
            pedido.Headers.ExpectContinue = true;
            // O erro do servidor volta no idioma da tela (etapa 10).
            pedido.Headers.AcceptLanguage.ParseAdd(Tr.AcceptLanguage);

            using var resposta = await Cliente.SendAsync(pedido, cancelar).ConfigureAwait(false);
            andamento?.Invoke(1, Tr.T("Esperando o servidor..."));
            var texto = await resposta.Content.ReadAsStringAsync(cancelar).ConfigureAwait(false);

            return Viewer3DPage.ParseResponse((int)resposta.StatusCode, texto, autorizacao);
        }
        catch (HttpRequestException erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao publicar no servidor 3D.", erro);
            return (null, Tr.F("não consegui falar com o servidor 3D em {0} ({1})", endereco, erro.Message));
        }
        catch (TaskCanceledException) when (!cancelar.IsCancellationRequested)
        {
            return (null, Tr.F("o servidor 3D em {0} não respondeu a tempo", endereco));
        }
    }

    /// <summary>O corpo mandado em pedaços de 64 kB, dizendo a fração que já foi.</summary>
    private sealed class ConteudoComAndamento(byte[] dados, Action<double> andamento) : HttpContent
    {
        private const int Pedaco = 64 * 1024;

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
        {
            for (var i = 0; i < dados.Length; i += Pedaco)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tamanho = Math.Min(Pedaco, dados.Length - i);
                await stream.WriteAsync(dados.AsMemory(i, tamanho), cancellationToken).ConfigureAwait(false);
                andamento((double)(i + tamanho) / dados.Length);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = dados.Length;
            return true;
        }
    }
}
