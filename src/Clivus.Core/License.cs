using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>O conteúdo de uma licença (plano/contrato-ativacao.md).</summary>
public sealed record LicensePayload(
    string Id, string Account, string Plan, string Machine, DateTime IssuedAt, DateTime RevalidateAt, DateTime ExpiresAt);

/// <summary>O que vale para a licença agora.</summary>
public enum LicenseState
{
    /// <summary>Boa e dentro do prazo.</summary>
    Valid,

    /// <summary>Boa, mas passou da data de revalidar: tentar renovar (sem internet, segue até expirar).</summary>
    Revalidate,

    /// <summary>Passou de expira_em: pedir o código de novo ou revalidar com internet.</summary>
    Expired,

    /// <summary>Assinatura que não confere, formato errado, ou de outra máquina.</summary>
    Invalid,
}

/// <summary>
/// A licença do Clivus Solar (04/10/2026): texto "{payload}.{assinatura}" em
/// base64url, assinado pelo servidor com ECDSA P-256/SHA-256 (r‖s); o plugin
/// confere com a chave pública e a máquina. Gratuito por ora, mas com licença
/// de verdade: cobrar depois é política do servidor.
/// </summary>
public static class License
{
    /// <summary>O prefixo do identificador da máquina (contrato).</summary>
    public const string MachinePrefix = "clivus:";

    /// <summary>O identificador da máquina: SHA-256 (hex minúsculo) de "clivus:" + MachineGuid.</summary>
    public static string MachineId(string machineGuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineGuid);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(MachinePrefix + machineGuid.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>O código digitado, limpo: maiúsculas, sem espaço; null se tem caractere fora de letras, números e hífen.</summary>
    public static string? NormalizeCode(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var limpo = codigo.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return limpo.Length is >= 4 and <= 64 && limpo.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? limpo : null;
    }

    /// <summary>
    /// Confere a licença: assinatura com a chave pública do <c>kid</c> dela
    /// (SubjectPublicKeyInfo em base64; sem <c>kid</c>, qualquer uma das
    /// chaves), versão, máquina e prazos. O conteúdo é devolvido mesmo expirado
    /// (para revalidar), nunca com assinatura inválida.
    /// </summary>
    public static (LicenseState State, LicensePayload? Payload, string? Why) Check(
        string? licenca, IReadOnlyDictionary<string, string> publicKeys, string machine, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(publicKeys);
        if (publicKeys.Count == 0) throw new ArgumentException("nenhuma chave pública", nameof(publicKeys));

        if (string.IsNullOrWhiteSpace(licenca)) return (LicenseState.Invalid, null, Tr.T("sem licença"));

        var partes = licenca.Trim().Split('.');
        if (partes.Length != 2) return (LicenseState.Invalid, null, Tr.T("licença em formato errado"));

        byte[] payload, assinatura;
        try
        {
            payload = Base64Url(partes[0]);
            assinatura = Base64Url(partes[1]);
        }
        catch (FormatException)
        {
            return (LicenseState.Invalid, null, Tr.T("licença em formato errado"));
        }

        // O kid escolhe a chave; ele ainda não está conferido, mas só serve
        // para escolher: a assinatura é que decide.
        string? kid = null;
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("kid", out var k) && k.ValueKind == JsonValueKind.String)
                kid = k.GetString();
        }
        catch (JsonException)
        {
            return (LicenseState.Invalid, null, Tr.T("licença com conteúdo incompleto"));
        }

        var candidatas = kid is null ? publicKeys.Values : publicKeys.TryGetValue(kid, out var chaveDoKid) ? [chaveDoKid] : [];
        if (!candidatas.Any(c => Confere(payload, assinatura, c))) return (LicenseState.Invalid, null, Tr.T("a assinatura da licença não confere"));

        LicensePayload conteudo;
        try
        {
            using var json = JsonDocument.Parse(payload);
            var r = json.RootElement;
            if (r.GetProperty("v").GetInt32() != 1) return (LicenseState.Invalid, null, Tr.T("licença de uma versão que este plugin não conhece"));

            conteudo = new LicensePayload(
                r.GetProperty("licenca").GetString()!, r.GetProperty("conta").GetString()!, r.GetProperty("plano").GetString()!,
                r.GetProperty("maquina").GetString()!, Data(r, "emitida_em"), Data(r, "revalidar_em"), Data(r, "expira_em"));
        }
        catch (Exception erro) when (erro is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return (LicenseState.Invalid, null, Tr.T("licença com conteúdo incompleto"));
        }

        if (!string.Equals(conteudo.Machine, machine, StringComparison.OrdinalIgnoreCase))
            return (LicenseState.Invalid, null, Tr.T("a licença é de outra máquina"));

        if (nowUtc > conteudo.ExpiresAt) return (LicenseState.Expired, conteudo, Tr.T("a licença expirou: conecte à internet para revalidar, ou ative de novo"));
        if (nowUtc > conteudo.RevalidateAt) return (LicenseState.Revalidate, conteudo, null);

        return (LicenseState.Valid, conteudo, null);
    }

    private static bool Confere(byte[] payload, byte[] assinatura, string publicKeyBase64)
    {
        try
        {
            using var chave = ECDsa.Create();
            chave.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return chave.VerifyData(payload, assinatura, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception erro) when (erro is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static DateTime Data(JsonElement r, string nome) =>
        DateTime.Parse(r.GetProperty(nome).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    /// <summary>base64url sem preenchimento (contrato).</summary>
    public static byte[] Base64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException("base64url inválido") };
        return Convert.FromBase64String(s);
    }

    /// <summary>O inverso de <see cref="Base64Url(string)"/>.</summary>
    public static string ToBase64Url(byte[] dados) => Convert.ToBase64String(dados).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>O corpo de "ativar" (contrato).</summary>
    public static string ActivateBody(string codigo, string machine, string machineName, string pluginVersion) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["codigo"] = codigo, ["maquina"] = machine, ["nome_maquina"] = machineName, ["plugin"] = pluginVersion });

    /// <summary>O corpo de "revalidar" (contrato).</summary>
    public static string RevalidateBody(string licenca, string machine) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["licenca"] = licenca, ["maquina"] = machine });

    /// <summary>A resposta de ativar ou revalidar: a licença nova, ou o erro (o do servidor, ou um nosso pelo código).</summary>
    public static (string? License, string? Error) ParseResponse(int status, string? corpo)
    {
        string? licenca = null, erro = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(corpo))
            {
                using var json = JsonDocument.Parse(corpo);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (json.RootElement.TryGetProperty("licenca", out var l) && l.ValueKind == JsonValueKind.String) licenca = l.GetString();
                    if (json.RootElement.TryGetProperty("erro", out var e) && e.ValueKind == JsonValueKind.String) erro = e.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        if (status is >= 200 and < 300)
            return licenca is { Length: > 0 } ? (licenca, null) : (null, Tr.T("o servidor respondeu sem a licença"));

        return (null, erro ?? (status switch
        {
            404 => Tr.T("código não encontrado"),
            403 => Tr.T("código revogado ou máquina liberada"),
            409 => Tr.T("este código já está ativo no número máximo de máquinas"),
            _ => Tr.F("o servidor de licenças respondeu {0}", status),
        }));
    }
}
