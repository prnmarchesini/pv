using System.Net.Http;
using System.Security.Cryptography;

namespace Clivus.Core;

/// <summary>O fim do download: de onde veio por último, o porquê de não servir (ou null) e se foi o conteúdo que não conferiu.</summary>
/// <param name="From">O último endereço pedido (depois dos redirecionamentos), ou null se nem pediu.</param>
/// <param name="Error">Por que o download não serve, ou null se serve.</param>
/// <param name="Mismatch">Se veio inteiro e o tamanho ou o SHA-256 não bate com a versão (o arquivo tem de ser apagado).</param>
public sealed record InstallerDownloadResult(Uri? From, string? Error, bool Mismatch);

/// <summary>
/// O download do instalador da atualização (etapa 43), sem arquivo nem
/// registro: quem chama dá o cliente HTTP (sem redirecionamento automático) e
/// o fluxo onde gravar, e decide o que fazer com o arquivo. Aqui ficam as
/// regras, para os testes: o endereço passa por
/// <see cref="PluginUpdate.WhyUrlRejected"/> com os endereços confiáveis, e
/// cada redirecionamento também;
/// o tamanho declarado tem de ser o da versão; nada além do tamanho da versão
/// é gravado; cada leitura tem tempo-limite; no fim, o tamanho e o SHA-256
/// conferem.
/// </summary>
public static class InstallerDownload
{
    /// <summary>Quantos redirecionamentos o download segue.</summary>
    public const int MaxRedirects = 3;

    /// <summary>Baixa a versão para <paramref name="destino"/>.</summary>
    /// <param name="cliente">O cliente HTTP, sem redirecionamento automático (o endereço novo passa pela regra aqui).</param>
    /// <param name="versao">A versão publicada (endereço, tamanho e SHA-256).</param>
    /// <param name="confiaveis">O servidor do Clivus configurado e os endereços de download (<see cref="PluginInfo.EnderecosDeDownload"/>).</param>
    /// <param name="destino">Onde gravar.</param>
    /// <param name="andamento">Os bytes gravados até agora, a cada pedaço.</param>
    /// <param name="leituraParada">O tempo-limite de cada leitura (e da resposta).</param>
    /// <param name="cancelar">O cancelamento de quem chamou.</param>
    public static async Task<InstallerDownloadResult> RunAsync(
        HttpClient cliente, PluginRelease versao, IReadOnlyList<string?> confiaveis, Stream destino,
        Action<long>? andamento, TimeSpan leituraParada, CancellationToken cancelar)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(confiaveis);
        ArgumentNullException.ThrowIfNull(destino);

        var lista = confiaveis.ToArray();
        if (PluginUpdate.WhyUrlRejected(versao.Url, lista) is { } recusa) return new InstallerDownloadResult(null, recusa, false);

        var endereco = new Uri(versao.Url);
        HttpResponseMessage? resposta = null;

        try
        {
            for (var salto = 0; ; salto++)
            {
                using (var pedido = new HttpRequestMessage(HttpMethod.Get, endereco))
                using (var espera = CancellationTokenSource.CreateLinkedTokenSource(cancelar))
                {
                    espera.CancelAfter(leituraParada);
                    resposta = await cliente.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, espera.Token).ConfigureAwait(false);
                }

                var status = (int)resposta.StatusCode;
                if (status is < 300 or >= 400) break;

                // Redirecionamento: o endereço novo passa pela mesma regra do primeiro.
                Uri? novo = null;
                if (resposta.Headers.Location is { } local && !Uri.TryCreate(endereco, local, out novo)) novo = null;
                resposta.Dispose();
                resposta = null;

                if (novo is null) return new InstallerDownloadResult(endereco, Tr.F("o download respondeu {0}", status), false);
                if (salto >= MaxRedirects) return new InstallerDownloadResult(endereco, Tr.T("o endereço do instalador redireciona demais"), false);
                if (PluginUpdate.WhyUrlRejected(novo.ToString(), lista) is { } outraRecusa) return new InstallerDownloadResult(novo, outraRecusa, false);

                endereco = novo;
            }

            if (!resposta.IsSuccessStatusCode)
                return new InstallerDownloadResult(endereco, Tr.F("o download respondeu {0}", (int)resposta.StatusCode), false);

            if (resposta.Content.Headers.ContentLength is { } declarado && declarado != versao.Size)
                return new InstallerDownloadResult(endereco, PluginUpdate.WhyDownloadRejected(declarado, null, versao), false);

            using var origem = await resposta.Content.ReadAsStreamAsync(cancelar).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                int lidos;
                using (var espera = CancellationTokenSource.CreateLinkedTokenSource(cancelar))
                {
                    espera.CancelAfter(leituraParada);
                    lidos = await origem.ReadAsync(buffer, espera.Token).ConfigureAwait(false);
                }

                if (lidos == 0) break;

                // Maior que o publicado já não confere: para de gravar.
                total += lidos;
                if (total > versao.Size) break;

                hash.AppendData(buffer, 0, lidos);
                await destino.WriteAsync(buffer.AsMemory(0, lidos), cancelar).ConfigureAwait(false);
                andamento?.Invoke(total);
            }

            var sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            return PluginUpdate.WhyDownloadRejected(total, sha, versao) is { } naoConfere
                ? new InstallerDownloadResult(endereco, naoConfere, true)
                : new InstallerDownloadResult(endereco, null, false);
        }
        catch (OperationCanceledException) when (cancelar.IsCancellationRequested)
        {
            return new InstallerDownloadResult(endereco, Tr.T("download cancelado"), false);
        }
        catch (OperationCanceledException)
        {
            return new InstallerDownloadResult(endereco, Tr.F("o download parou: a conexão ficou {0:0} segundos sem mandar nada", leituraParada.TotalSeconds), false);
        }
        catch (Exception erro) when (erro is HttpRequestException or IOException)
        {
            return new InstallerDownloadResult(endereco, Tr.F("não consegui baixar o instalador ({0})", erro.Message), false);
        }
        finally
        {
            resposta?.Dispose();
        }
    }
}
