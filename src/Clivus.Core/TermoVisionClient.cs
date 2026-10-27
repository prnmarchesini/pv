using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>O resumo do envio (<c>GET /envio</c>), para a pessoa ver antes de baixar.</summary>
public sealed record TermoVisionSummary(string PlantName, string AreaName, int Epsg, DateTimeOffset? ExpiresAt, string CreatedBy, double EstimatedMb);

/// <summary>
/// Os três pedidos do contrato, com o token do envio no <c>Authorization</c>.
/// Sem CAD: o plugin chama de uma tarefa em segundo plano. Toda falha vira uma
/// frase em português (o <c>detail</c> do servidor quando ele manda); o token
/// nunca entra nela.
/// </summary>
public sealed class TermoVisionClient : IDisposable
{
    /// <summary>Limite do pacote baixado.</summary>
    public const long MaxPackageBytes = 1024L * 1024 * 1024;

    /// <summary>Limite do retorno descomprimido, do contrato (413 acima disso).</summary>
    public const long MaxReturnBytes = 20L * 1024 * 1024;

    private readonly TermoVisionLink _link;
    private readonly HttpClient _http;

    public TermoVisionClient(TermoVisionLink link, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _link = link;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = timeout ?? TimeSpan.FromMinutes(5);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>O resumo do envio.</summary>
    public async Task<(TermoVisionSummary? Resumo, string? Erro)> SummaryAsync(CancellationToken cancelar = default)
    {
        var (status, corpo, erro) = await Pedir(HttpMethod.Get, "/api/clivus/v1/envio", null, null, cancelar, MaxReturnBytes).ConfigureAwait(false);
        if (erro is not null) return (null, erro);
        if (status != HttpStatusCode.OK) return (null, Falha(status, corpo!));

        try
        {
            using var doc = JsonDocument.Parse(corpo!);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) throw new FormatException();
            DateTimeOffset? expira = r.TryGetProperty("expira_em", out var x) && x.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(x.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
            return (new TermoVisionSummary(
                Nome(r, "usina"), Nome(r, "area"),
                r.TryGetProperty("crs", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty("epsg", out var e)
                    && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var epsg) ? epsg : 0,
                expira,
                r.TryGetProperty("criado_por", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "",
                r.TryGetProperty("tamanho_estimado_mb", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : 0), null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return (null, Tr.T("O TermoVision mandou um resumo do envio que não consegui ler."));
        }
    }

    /// <summary>O pacote ZIP inteiro, na memória.</summary>
    public async Task<(byte[]? Zip, string? Erro)> PackageAsync(CancellationToken cancelar = default)
    {
        var (status, corpo, erro) = await Pedir(HttpMethod.Get, "/api/clivus/v1/pacote", null, null, cancelar, MaxPackageBytes).ConfigureAwait(false);
        if (erro is not null) return (null, erro);
        return status == HttpStatusCode.OK ? (corpo, null) : (null, Falha(status, corpo!));
    }

    /// <summary>
    /// Manda as mesas. <paramref name="pedido"/> é o id do clique: reenviar o
    /// mesmo clique com o mesmo id não grava de novo no servidor.
    /// </summary>
    public async Task<(TermoVisionResult? Resultado, string? Erro)> SendAsync(byte[] corpoJson, string pedido, CancellationToken cancelar = default)
    {
        if (corpoJson.LongLength > MaxReturnBytes)
            return (null, Tr.T("As mesas passam de 20 MB, o limite do TermoVision. Exporte por partes."));

        using var comprimido = new MemoryStream();
        using (var gzip = new GZipStream(comprimido, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(corpoJson);

        var conteudo = new ByteArrayContent(comprimido.ToArray());
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        conteudo.Headers.ContentEncoding.Add("gzip");

        var (status, corpo, erro) = await Pedir(HttpMethod.Post, "/api/clivus/v1/retorno", conteudo, pedido, cancelar, MaxReturnBytes).ConfigureAwait(false);
        if (erro is not null) return (null, erro);
        if (status != HttpStatusCode.OK) return (null, Falha(status, corpo!));

        var resultado = TermoVisionResult.TryParse(Encoding.UTF8.GetString(corpo!));
        return resultado is null
            ? (null, Tr.T("O TermoVision gravou, mas mandou uma resposta que não consegui ler."))
            : (resultado, null);
    }

    private async Task<(HttpStatusCode Status, byte[]? Corpo, string? Erro)> Pedir(
        HttpMethod metodo, string caminho, HttpContent? conteudo, string? pedido, CancellationToken cancelar, long limite)
    {
        // O Timeout do HttpClient só cobre até os cabeçalhos (ResponseHeadersRead):
        // o prazo cobre também a leitura do corpo.
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        prazo.CancelAfter(_http.Timeout);
        try
        {
            using var mensagem = new HttpRequestMessage(metodo, _link.Server + caminho) { Content = conteudo };
            mensagem.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _link.Token);
            mensagem.Headers.AcceptLanguage.ParseAdd(Tr.AcceptLanguage);
            if (pedido is not null) mensagem.Headers.Add("X-Pedido", pedido);

            using var resposta = await _http.SendAsync(mensagem, HttpCompletionOption.ResponseHeadersRead, prazo.Token).ConfigureAwait(false);
            if (resposta.Content.Headers.ContentLength > limite)
                return (resposta.StatusCode, null, Tr.F("A resposta do TermoVision passa do limite de {0} MB.", limite / (1024 * 1024)));

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(prazo.Token).ConfigureAwait(false);
            using var memoria = new MemoryStream();
            var bloco = new byte[81920];
            int lidos;
            while ((lidos = await fluxo.ReadAsync(bloco, prazo.Token).ConfigureAwait(false)) > 0)
            {
                if (memoria.Length + lidos > limite)
                    return (resposta.StatusCode, null, Tr.F("A resposta do TermoVision passa do limite de {0} MB.", limite / (1024 * 1024)));
                memoria.Write(bloco, 0, lidos);
            }
            return (resposta.StatusCode, memoria.ToArray(), null);
        }
        catch (Exception e) when (e is HttpRequestException or IOException)
        {
            // IOException: a conexão caiu no meio do corpo (HttpIOException).
            return (0, null, Tr.F("Não consegui falar com o TermoVision em {0} ({1}).", _link.Server, e.Message));
        }
        catch (OperationCanceledException) when (!cancelar.IsCancellationRequested)
        {
            return (0, null, Tr.F("O TermoVision em {0} não respondeu a tempo.", _link.Server));
        }
    }

    // A frase de cada erro do contrato; o detail do servidor vai junto quando vem.
    private static string Falha(HttpStatusCode status, byte[] corpo)
    {
        var detalhe = TermoVisionResult.Detail(Encoding.UTF8.GetString(corpo));
        return (int)status switch
        {
            401 => Tr.F("O TermoVision não aceitou o acesso deste envio: {0} Exporte a área de novo no TermoVision.",
                detalhe ?? Tr.T("o token venceu ou deixou de valer.")),
            404 => detalhe is null
                ? Tr.T("A área deste envio foi desfeita no TermoVision.")
                : Tr.F("A área deste envio foi desfeita no TermoVision: {0}", detalhe),
            400 => Tr.F("O TermoVision recusou as mesas: {0}", detalhe ?? Tr.T("sem motivo.")),
            413 => Tr.T("As mesas passam de 20 MB, o limite do TermoVision. Exporte por partes."),
            var n => Tr.F("O TermoVision respondeu {0}: {1}", n, detalhe ?? Tr.T("sem motivo.")),
        };
    }

    private static string Nome(JsonElement r, string campo) =>
        r.TryGetProperty(campo, out var o) && o.ValueKind == JsonValueKind.Object && o.TryGetProperty("nome", out var n)
        && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
}
