using System.IO;
using System.Net.Http;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Core;
using Microsoft.Win32;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A licença do Clivus Solar no plugin (plano/contrato-ativacao.md). O
/// cliente gera o código no portal do app, cola no plugin (Ativar), e o
/// servidor devolve a licença assinada, guardada na pasta do usuário. Os
/// comandos do Clivus só rodam com licença válida; passado o prazo de
/// revalidar, ela é renovada em segundo plano (sem internet, vale até
/// expirar).
///
/// Sem chave pública gravada (<see cref="PluginInfo.ChavesPublicasDaLicenca"/>
/// vazio) o licenciamento fica desligado: é o estado de hoje e dos testes.
/// </summary>
internal static class Licenciamento
{
    private static readonly HttpClient Cliente = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Comandos que rodam sem licença: ativar, sobre, olá e a migração do nome.</summary>
    private static readonly HashSet<string> Livres = new(StringComparer.OrdinalIgnoreCase)
    {
        PluginInfo.ComandoAtivar, PluginInfo.ComandoAtivarAutomatico, PluginInfo.ComandoSobre, PluginInfo.ComandoOla, PluginInfo.ComandoMigrar,
    };

    private static bool _instalado;
    private static int _revalidando;

    /// <summary>As chaves públicas em vigor. Em Debug, os testes de nível 2 podem trocar por uma de teste.</summary>
    internal static IReadOnlyDictionary<string, string> Chaves
    {
        get
        {
#if DEBUG
            // Só no build de teste (o bundle instalado é Release): "kid=base64".
            if (Environment.GetEnvironmentVariable("CLIVUS_LICENCA_CHAVE_TESTE") is { Length: > 0 } teste && teste.Split('=', 2) is [var kid, var chave])
                return new Dictionary<string, string> { [kid] = chave };
#endif
            return PluginInfo.ChavesPublicasDaLicenca;
        }
    }

    /// <summary>Se o licenciamento está ligado (há chave pública).</summary>
    internal static bool Ligado => Chaves.Count > 0;

    /// <summary>O arquivo da licença, na pasta do usuário.</summary>
    internal static string Arquivo
    {
        get
        {
#if DEBUG
            if (Environment.GetEnvironmentVariable("CLIVUS_LICENCA_ARQUIVO_TESTE") is { Length: > 0 } teste) return teste;
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "licenca.txt");
        }
    }

    /// <summary>O endereço do servidor de licenças: CLIVUS_LICENCAS, senão o do servidor 3D.</summary>
    private static string? Endereco =>
        Environment.GetEnvironmentVariable("CLIVUS_LICENCAS") is { Length: > 0 } e && Uri.TryCreate(e.Trim(), UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : Publicador3D.Endereco;

    /// <summary>Esta máquina: o MachineGuid do Windows, com o prefixo do contrato, em SHA-256.</summary>
    internal static string Maquina
    {
        get
        {
            using var chave = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = chave?.GetValue("MachineGuid") as string;
            return License.MachineId(string.IsNullOrWhiteSpace(guid) ? Environment.MachineName : guid);
        }
    }

    /// <summary>O estado da licença guardada.</summary>
    internal static (LicenseState Estado, LicensePayload? Conteudo, string? Porque) Estado()
    {
        if (!Ligado) return (LicenseState.Valid, null, null);

        var texto = File.Exists(Arquivo) ? File.ReadAllText(Arquivo).Trim() : null;
        return License.Check(texto, Chaves, Maquina, DateTime.UtcNow);
    }

    /// <summary>Ativa com o código do portal. A frase do resultado, e se deu certo.</summary>
    internal static (bool Ok, string Frase) Ativar(string codigoDigitado)
    {
        if (License.NormalizeCode(codigoDigitado) is not { } codigo) return (false, "O código só tem letras, números e hífen.");
        if (Endereco is not { } endereco) return (false, "O servidor de licenças não está configurado.");
        if (Viewer3DPage.WhyServerUnsafe(endereco) is { } inseguro) return (false, char.ToUpperInvariant(inseguro[0]) + inseguro[1..] + ".");

        var corpo = License.ActivateBody(codigo, Maquina, Environment.MachineName, PluginInfo.VersaoLegivel(ClivusCommands.VersaoDoPlugin()));
        var (licenca, erro) = Enviar(endereco + "/api/v1/licencas/ativar", corpo);
        if (licenca is null) return (false, $"Não ativei: {erro}.");

        var (estado, conteudo, porque) = Ligado ? License.Check(licenca, Chaves, Maquina, DateTime.UtcNow) : (LicenseState.Valid, null, null);
        if (estado is LicenseState.Invalid or LicenseState.Expired) return (false, $"O servidor mandou uma licença que não vale ({porque}).");

        Gravar(licenca);
        return (true, conteudo is null
            ? "Clivus Solar ativado."
            : $"Clivus Solar ativado para {conteudo.Account} (plano {conteudo.Plan}), até {conteudo.ExpiresAt.ToLocalTime():dd/MM/yyyy}; renova sozinho com internet.");
    }

    /// <summary>Renova a licença; 403 (revogada ou máquina liberada) apaga a guardada. Se renovou.</summary>
    internal static bool Revalidar()
    {
        if (!Ligado || Endereco is not { } endereco || Viewer3DPage.WhyServerUnsafe(endereco) is not null || !File.Exists(Arquivo)) return false;

        var atual = File.ReadAllText(Arquivo).Trim();
        var (licenca, erro) = Enviar(endereco + "/api/v1/licencas/revalidar", License.RevalidateBody(atual, Maquina), out var status);

        if (licenca is not null && License.Check(licenca, Chaves, Maquina, DateTime.UtcNow).State is LicenseState.Valid or LicenseState.Revalidate)
        {
            Gravar(licenca);
            return true;
        }

        if (status == 403)
        {
            File.Delete(Arquivo);
            RegistroDeDiagnostico.Registrar($"Licença revogada pelo servidor: {erro}.");
        }

        return false;
    }

    private static (string? Licenca, string? Erro) Enviar(string url, string corpo) => Enviar(url, corpo, out _);

    private static (string? Licenca, string? Erro) Enviar(string url, string corpo, out int status)
    {
        status = 0;

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
            using var resposta = Cliente.Send(pedido);
            using var leitor = new StreamReader(resposta.Content.ReadAsStream(), Encoding.UTF8);
            status = (int)resposta.StatusCode;
            return License.ParseResponse(status, leitor.ReadToEnd());
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            return (null, "não consegui falar com o servidor de licenças (sem internet?)");
        }
    }

    private static void Gravar(string licenca)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
        File.WriteAllText(Arquivo, licenca);
    }

    // ------------------------------------------------------------ o bloqueio

    /// <summary>Barra os comandos do Clivus sem licença válida (o veto da trava do documento, antes do comando rodar).</summary>
    internal static void Instalar()
    {
        if (_instalado) return;

        AcadApp.DocumentManager.DocumentLockModeChanged += AoTravar;
        _instalado = true;
    }

    internal static void Desinstalar()
    {
        if (!_instalado) return;

        try { AcadApp.DocumentManager.DocumentLockModeChanged -= AoTravar; }
        catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao desligar o licenciamento.", erro); }

        _instalado = false;
    }

    private static void AoTravar(object? remetente, DocumentLockModeChangedEventArgs e)
    {
        try
        {
            var comando = e.GlobalCommandName?.TrimStart('#', '\'').Trim() ?? string.Empty;
            if (!comando.StartsWith(PluginInfo.PrefixoDeComando, StringComparison.OrdinalIgnoreCase) || Livres.Contains(comando)) return;
            if (!Ligado) return;

            var (estado, _, porque) = Estado();

            // Passou de revalidar: renova em segundo plano (só HTTP e arquivo),
            // e o comando roda agora com a licença que tem.
            if (estado == LicenseState.Revalidate && Interlocked.Exchange(ref _revalidando, 1) == 0)
                Task.Run(() => { try { Revalidar(); } catch (Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao revalidar a licença.", erro); } finally { _revalidando = 0; } });

            if (estado is LicenseState.Valid or LicenseState.Revalidate) return;

            e.Veto();
            e.Document?.Editor.WriteMessage(
                $"\nCLIVUS SOLAR sem licença válida ({porque ?? "não ativado"}). Gere seu código no portal do app e use o botão Ativar ({PluginInfo.ComandoAtivar}).\n");
        }
        catch (Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir a licença.", erro);
        }
    }
}
