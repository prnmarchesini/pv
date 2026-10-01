using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// De onde vêm os módulos da janela de Mesa: do serviço local (passo 8.3) e,
/// sem serviço no ar, da biblioteca embutida na DLL.
///
/// O endereço é <c>http://localhost:8765</c>, ou o da variável de ambiente
/// <c>UFV_SERVICO</c> (quando o serviço for para o servidor).
/// </summary>
internal static class FonteDeModulos
{
    private const string EnderecoPadrao = "http://localhost:8765";

    /// <summary>
    /// Curto de propósito: a janela espera por isto ao abrir, e serviço fora
    /// do ar não pode virar janela travada.
    /// </summary>
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromSeconds(1.5) };

    /// <summary>
    /// Para o cadastro, mais folga: gravar pode demorar (banco frio,
    /// antivírus), e um "não respondeu" com o módulo já gravado leva o
    /// usuário a um 409 na segunda tentativa.
    /// </summary>
    private static readonly HttpClient ClienteDoCadastro = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>O endereço, se for http(s) absoluto; null se UFV_SERVICO não presta.</summary>
    private static string? EnderecoValido() =>
        Uri.TryCreate(Endereco, UriKind.Absolute, out var base_)
        && (base_.Scheme == Uri.UriSchemeHttp || base_.Scheme == Uri.UriSchemeHttps)
            ? Endereco
            : null;

    private static DateTime? _foraDoArAte;

    internal static string Endereco =>
        (Environment.GetEnvironmentVariable("UFV_SERVICO") is { Length: > 0 } definido
            ? definido
            : EnderecoPadrao).TrimEnd('/');

    /// <summary>
    /// Os módulos e uma linha dizendo de onde vieram, para a janela mostrar.
    /// </summary>
    internal static (IReadOnlyList<SolarModule> Modulos, string Origem) Carregar()
    {
        if (EnderecoValido() is null)
        {
            RegistroDeDiagnostico.Registrar($"UFV_SERVICO não é um endereço http: \"{Endereco}\".");
            return (ModuleLibrary.Default(), $"Biblioteca embutida (UFV_SERVICO inválido: {Endereco}).");
        }

        // Serviço fora do ar há pouco: não espera de novo a cada janela.
        if (_foraDoArAte is { } ate && DateTime.UtcNow < ate)
            return (ModuleLibrary.Default(), "Biblioteca embutida (serviço fora do ar).");

        try
        {
            // Task.Run: sem ele, o .Result no fio da interface do AutoCAD
            // pode travar esperando o próprio fio.
            var json = Task.Run(() => Cliente.GetStringAsync($"{Endereco}/modulos")).GetAwaiter().GetResult();
            var modulos = ModuleLibrary.ParseService(json);

            return (modulos, $"Módulos do serviço ({Endereco}): {modulos.Count}.");
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            RegistroDeDiagnostico.Registrar($"Serviço de módulos indisponível em {Endereco}; usando a biblioteca embutida.", erro);
            if (erro is not InvalidOperationException) _foraDoArAte = DateTime.UtcNow.AddMinutes(1);

            var motivo = erro is InvalidOperationException ? erro.Message : "serviço fora do ar";
            return (ModuleLibrary.Default(), $"Biblioteca embutida ({motivo}).");
        }
    }

    /// <summary>
    /// Cadastra o módulo no serviço (passo 8.4). Devolve se deu certo e a
    /// frase para o usuário: o motivo da recusa vem do próprio serviço
    /// (modelo repetido, medida fora da faixa).
    /// </summary>
    internal static (bool Cadastrou, string Mensagem) Cadastrar(SolarModule modulo)
    {
        string json;

        try
        {
            json = ModuleLibrary.ToServiceJson(modulo);
        }
        catch (InvalidOperationException erro)
        {
            return (false, erro.Message);
        }

        if (EnderecoValido() is null)
        {
            return (false, $"UFV_SERVICO não é um endereço http: \"{Endereco}\".");
        }

        try
        {
            using var corpo = new StringContent(json, Encoding.UTF8, "application/json");
            using var resposta = Task.Run(() => ClienteDoCadastro.PostAsync($"{Endereco}/modulos", corpo)).GetAwaiter().GetResult();
            var texto = Task.Run(() => resposta.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

            if (resposta.IsSuccessStatusCode)
            {
                _foraDoArAte = null;
                return (true, $"Módulo {modulo.DisplayName} cadastrado no serviço.");
            }

            return (false, resposta.StatusCode == HttpStatusCode.Conflict
                ? Detalhe(texto) ?? "Já existe um módulo com esse modelo."
                : $"O serviço recusou ({(int)resposta.StatusCode}): {Detalhe(texto) ?? Cortar(texto)}");
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            RegistroDeDiagnostico.Registrar($"Falha ao cadastrar módulo em {Endereco}.", erro);
            return (false, $"O serviço de módulos não respondeu em {Endereco}. Se ele estava no ar, o módulo pode ter "
                + "sido gravado: feche e abra a janela de Mesa para conferir a lista. Fora do ar, rode tools\\servico-local.ps1.");
        }
    }

    /// <summary>
    /// O "detail" do FastAPI: texto (409) ou lista de erros de validação
    /// (422), dos quais sai a primeira mensagem.
    /// </summary>
    private static string? Detalhe(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("detail", out var detalhe)) return null;

            if (detalhe.ValueKind == JsonValueKind.String) return detalhe.GetString();

            // 422 do FastAPI: lista de erros, cada um com o campo em "loc".
            if (detalhe.ValueKind == JsonValueKind.Array)
            {
                var partes = new List<string>();

                foreach (var item in detalhe.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("msg", out var msg)) continue;

                    var texto = (msg.GetString() ?? string.Empty).Replace("Value error, ", string.Empty);
                    var campo = item.TryGetProperty("loc", out var loc) && loc.ValueKind == JsonValueKind.Array && loc.GetArrayLength() > 0
                        ? loc[loc.GetArrayLength() - 1].ToString()
                        : null;

                    partes.Add(campo is null || campo == "body" ? texto : $"{campo}: {texto}");
                }

                return partes.Count > 0 ? string.Join("; ", partes) : null;
            }
        }
        catch (Exception erro) when (erro is JsonException or InvalidOperationException)
        {
            // Resposta que não é JSON (proxy, página de erro): o chamador
            // mostra o texto cru.
        }

        return null;
    }

    /// <summary>Resposta crua (uma página de erro) cortada para caber numa mensagem.</summary>
    private static string Cortar(string texto) =>
        texto.Length <= 200 ? texto : texto[..200] + "...";
}
