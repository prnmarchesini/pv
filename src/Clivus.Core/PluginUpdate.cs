using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>
/// A versão publicada no servidor (<c>GET /api/v1/versao</c>, etapa 43):
/// o número, de onde baixar o instalador, o SHA-256 e o tamanho dele, as
/// notas para o usuário, quando saiu e se é obrigatória.
/// </summary>
public sealed record PluginRelease(SemanticVersion Version, string Url, string Sha256, long Size, string Notes, DateTime? PublishedAt, bool Required);

/// <summary>
/// A atualização do plugin no PC do cliente (etapa 43; Renan, 31/10/2026:
/// "quero que o cliente receba atualizações também, veja a melhor forma").
/// O plugin consulta a versão publicada ao abrir (uma vez por dia) ou pelo
/// comando Atualizar; com versão nova, baixa o instalador, confere o tamanho
/// e o SHA-256 e o roda com <see cref="WaitForCivil3DOption"/>: ele espera o
/// Civil 3D fechar e instala sozinho. As regras ficam aqui, sem rede nem
/// processo, para os testes.
/// </summary>
public static class PluginUpdate
{
    /// <summary>A rota pública da versão (contrato da etapa 43).</summary>
    public const string VersionRoute = "/api/v1/versao";

    /// <summary>A opção do instalador que espera o Civil 3D fechar para instalar.</summary>
    public const string WaitForCivil3DOption = "/aguardar-civil3d";

    /// <summary>
    /// O mutex do instalador que está esperando: um só por sessão, e o plugin
    /// sabe que a atualização já foi baixada e só falta fechar o Civil 3D.
    /// </summary>
    public const string WaitingMutexName = @"Local\ClivusSolar-AtualizacaoEsperando";

    /// <summary>Mais que isso não é o instalador do Clivus (hoje tem uns 3 MB): nem baixa.</summary>
    public const long LargestInstaller = 200L * 1024 * 1024;

    /// <summary>De quanto em quanto tempo a abertura do Civil 3D consulta a versão: no máximo uma vez por dia.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <summary>Notas maiores que isso são cortadas na janela (a página do servidor tem o texto inteiro).</summary>
    public const int LongestNotes = 8000;

    /// <summary>
    /// A resposta de <c>GET /api/v1/versao</c>: a versão publicada; ou nenhuma
    /// sem erro (404: o servidor ainda não publicou versão); ou o erro (o do
    /// servidor, como veio, ou um nosso quando a resposta foge do contrato).
    /// </summary>
    public static (PluginRelease? Release, string? Error) ParseReply(int status, string? corpo)
    {
        JsonElement raiz = default;
        var temJson = false;
        JsonDocument? documento = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(corpo))
            {
                documento = JsonDocument.Parse(corpo);
                raiz = documento.RootElement;
                temJson = raiz.ValueKind == JsonValueKind.Object;
            }
        }
        catch (JsonException)
        {
            temJson = false;
        }

        try
        {
            if (status == 404) return (null, null);

            if (status is < 200 or >= 300)
            {
                return (null, temJson && raiz.TryGetProperty("erro", out var erro) && erro.ValueKind == JsonValueKind.String && erro.GetString() is { Length: > 0 } texto
                    ? texto
                    : Tr.F("o servidor respondeu {0}", status));
            }

            if (!temJson) return (null, Tr.T("o servidor respondeu sem a versão"));

            return Ler(raiz) is { } versao
                ? (versao, null)
                : (null, Tr.T("o servidor mandou a versão fora do contrato"));
        }
        finally
        {
            documento?.Dispose();
        }
    }

    private static PluginRelease? Ler(JsonElement r)
    {
        string? Texto(string nome) => r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        if (SemanticVersion.Parse(Texto("versao")) is not { } versao) return null;
        if (Texto("url") is not { Length: > 0 } url || !Uri.TryCreate(url, UriKind.Absolute, out _)) return null;
        if (Texto("sha256") is not { } sha || !IsSha256(sha)) return null;
        if (!r.TryGetProperty("tamanho", out var t) || t.ValueKind != JsonValueKind.Number || !t.TryGetInt64(out var tamanho) || tamanho is <= 0 or > LargestInstaller) return null;

        var notas = Texto("notas")?.Trim() ?? string.Empty;
        if (notas.Length > LongestNotes)
        {
            // Sem partir um caractere de dois códigos (emoji) no corte.
            var corte = char.IsHighSurrogate(notas[LongestNotes - 1]) ? LongestNotes - 1 : LongestNotes;
            notas = notas[..corte] + "…";
        }

        DateTime? publicada = Texto("publicada_em") is { } quando
            && DateTime.TryParse(quando, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var data)
            ? data
            : null;

        var obrigatoria = r.TryGetProperty("obrigatoria", out var o) && o.ValueKind == JsonValueKind.True;

        return new PluginRelease(versao, url, sha.ToLowerInvariant(), tamanho, notas, publicada, obrigatoria);
    }

    /// <summary>Se é um SHA-256 em hexadecimal (64 dígitos, maiúsculas ou minúsculas).</summary>
    public static bool IsSha256(string? texto) =>
        texto is { Length: 64 } && texto.All(char.IsAsciiHexDigit);

    /// <summary>
    /// Se a abertura do Civil 3D consulta agora: nunca consultou, ou faz um
    /// dia. Relógio que voltou (a última "no futuro") também consulta, para
    /// um relógio errado uma vez não calar a atualização até aquela data.
    /// </summary>
    public static bool IsCheckDue(DateTime? ultimaUtc, DateTime agoraUtc) =>
        ultimaUtc is not { } ultima
        || agoraUtc - ultima >= CheckInterval
        || ultima - agoraUtc > TimeSpan.FromMinutes(5);

    /// <summary>
    /// Se a consulta conta como a do dia (grava nas preferências): o servidor
    /// respondeu dentro do contrato, e não há versão obrigatória mais nova
    /// pendente. Essa avisa a cada abertura do Civil 3D até ser instalada,
    /// venha a consulta da abertura ou do comando Atualizar.
    /// </summary>
    /// <param name="respondeu">Se o servidor respondeu (a versão, ou 404 sem versão).</param>
    /// <param name="maisNova">A versão publicada, quando é mais nova que a instalada; senão null.</param>
    public static bool RecordsCheck(bool respondeu, PluginRelease? maisNova) =>
        respondeu && maisNova is not { Required: true };

    /// <summary>
    /// Por que o plugin não baixa deste endereço, ou null se baixa. Só HTTPS
    /// (http só em localhost, o servidor falso dos testes, como no resto do
    /// plugin) e só do mesmo esquema, host e porta de um dos endereços
    /// confiáveis: o servidor do Clivus configurado e os de
    /// <see cref="PluginInfo.EnderecosDeDownload"/>. Sem isso, um servidor
    /// comprometido mandaria o plugin rodar um executável de qualquer lugar.
    /// </summary>
    /// <param name="url">O endereço do instalador que a versão trouxe.</param>
    /// <param name="confiaveis">Os endereços confiáveis (os vazios não contam).</param>
    public static string? WhyUrlRejected(string? url, params string?[]? confiaveis)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var endereco) || (endereco.Scheme != Uri.UriSchemeHttps && endereco.Scheme != Uri.UriSchemeHttp))
            return Tr.T("o endereço do instalador não é um endereço web");

        if (endereco.Scheme != Uri.UriSchemeHttps && !endereco.IsLoopback)
            return Tr.T("o instalador só é baixado por HTTPS");

        return (confiaveis ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Any(c => Viewer3DPage.IsLinkFromServer(endereco.ToString(), c!))
            ? null
            : Tr.F("o instalador vem de {0}, que não é o servidor nem o site do Clivus", endereco.Host);
    }

    /// <summary>
    /// O nome do instalador baixado: sai do número da versão (já conferido), e
    /// nunca do endereço, que é do servidor.
    /// </summary>
    public static string InstallerFileName(SemanticVersion versao) => $"ClivusSolar-Setup-{versao}.exe";

    /// <summary>
    /// Por que o arquivo baixado não serve, ou null se serve: o tamanho e o
    /// SHA-256 têm de ser os da versão publicada.
    /// </summary>
    public static string? WhyDownloadRejected(long tamanho, string? sha256, PluginRelease versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        if (tamanho != versao.Size)
            return Tr.F("o arquivo baixado tem {0:N0} bytes, e a versão publicada diz {1:N0}", tamanho, versao.Size);

        return string.Equals(sha256, versao.Sha256, StringComparison.OrdinalIgnoreCase)
            ? null
            : Tr.T("o SHA-256 do arquivo baixado não confere com o da versão publicada");
    }

    /// <summary>O SHA-256 em hexadecimal minúsculo.</summary>
    public static string Sha256Hex(Stream fluxo)
    {
        ArgumentNullException.ThrowIfNull(fluxo);
        return Convert.ToHexString(SHA256.HashData(fluxo)).ToLowerInvariant();
    }

    /// <summary>
    /// A linha de comando do instalador que espera o Civil 3D fechar: a
    /// opção, o registro dele e o idioma da tela do plugin (a mensagem do fim
    /// sai no mesmo idioma).
    /// </summary>
    public static string InstallerArguments(string arquivoDoRegistro, UiLanguage idioma)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivoDoRegistro);
        if (arquivoDoRegistro.Contains('"') || arquivoDoRegistro.EndsWith('\\'))
            throw new ArgumentException("caminho do registro que não cabe entre aspas", nameof(arquivoDoRegistro));

        return $"{WaitForCivil3DOption} \"/log={arquivoDoRegistro}\" /idioma={Tr.Code(idioma)}";
    }

    /// <summary>
    /// A pasta das DLLs do bundle que o instalador põe para o usuário
    /// (<c>%APPDATA%\Autodesk\ApplicationPlugins\ClivusSolar.bundle\Contents</c>).
    /// </summary>
    public static string InstallerBundleContents(string pastaDoAppData) =>
        Path.Combine(pastaDoAppData, "Autodesk", "ApplicationPlugins", "ClivusSolar.bundle", "Contents");

    /// <summary>
    /// Se o plugin que está rodando é o do instalador. Só esse a atualização
    /// troca: o instalador põe a versão nova na pasta do usuário, e um plugin
    /// carregado de outro lugar (Arquivos de Programas, a pasta do build)
    /// deixaria o Civil 3D com duas cópias.
    /// </summary>
    /// <param name="pastaDoPlugin">A pasta da Clivus.Plugin.dll carregada.</param>
    /// <param name="pastaDoAppData">O %APPDATA% do usuário.</param>
    public static bool IsInstallerBundle(string? pastaDoPlugin, string? pastaDoAppData)
    {
        if (string.IsNullOrWhiteSpace(pastaDoPlugin) || string.IsNullOrWhiteSpace(pastaDoAppData)) return false;

        try
        {
            var esperada = Path.GetFullPath(InstallerBundleContents(pastaDoAppData)).TrimEnd('\\', '/');
            var atual = Path.GetFullPath(pastaDoPlugin).TrimEnd('\\', '/');
            return string.Equals(esperada, atual, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception erro) when (erro is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
