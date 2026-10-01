using System.Net.Http;
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
        if (!Uri.TryCreate(Endereco, UriKind.Absolute, out var base_)
            || (base_.Scheme != Uri.UriSchemeHttp && base_.Scheme != Uri.UriSchemeHttps))
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
}
