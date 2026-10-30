using System.Text.Json;

namespace Clivus.Core;

/// <summary>
/// A resposta do servidor ao cadastro de um módulo (38.8). Vale para os dois
/// servidores: o serviço local (<c>servidor/</c>, FastAPI) e o de produção
/// (clivussolar, <c>/api/v1/modulos</c>).
///
/// No de produção, o módulo do cliente chega pendente e só entra na lista de
/// todos depois que a gestão do Clivus confere o .PAN e aprova. Renan,
/// 30/10/2026: "os arquivos pan do cliente vão para a página de gestão do
/// clivus, onde uma pessoa valida os pans e vai integrando a plataforma de
/// forma pública após validação".
/// </summary>
public static class ModuleServiceReply
{
    /// <summary>
    /// Se o módulo ficou esperando a validação da gestão ("situacao":
    /// "pendente"). O serviço local não manda "situacao": lá o módulo entra
    /// na lista na hora.
    /// </summary>
    public static bool IsPending(string respostaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(respostaJson);

            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("situacao", out var situacao)
                && situacao.ValueKind == JsonValueKind.String
                && situacao.GetString() == "pendente";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// A mensagem de erro da resposta, ou null se ela não é JSON ou não tem
    /// mensagem nenhuma. O servidor de produção manda <c>{"erro": "..."}</c>,
    /// já no idioma do pedido. O serviço local manda o "detail" do FastAPI:
    /// um texto (409) ou a lista de erros de validação (422), de que sai cada
    /// mensagem com o campo.
    /// </summary>
    public static string? ErrorMessage(string respostaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(respostaJson);
            var raiz = doc.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object) return null;

            if (raiz.TryGetProperty("erro", out var erro) && erro.ValueKind == JsonValueKind.String)
                return erro.GetString() is { Length: > 0 } texto ? texto : null;

            if (!raiz.TryGetProperty("detail", out var detalhe)) return null;

            if (detalhe.ValueKind == JsonValueKind.String)
                return detalhe.GetString() is { Length: > 0 } texto ? texto : null;

            if (detalhe.ValueKind != JsonValueKind.Array) return null;

            var partes = new List<string>();

            foreach (var item in detalhe.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("msg", out var msg) || msg.ValueKind != JsonValueKind.String) continue;

                var texto = (msg.GetString() ?? string.Empty).Replace("Value error, ", string.Empty, StringComparison.Ordinal);
                var campo = item.TryGetProperty("loc", out var loc) && loc.ValueKind == JsonValueKind.Array && loc.GetArrayLength() > 0
                    ? loc[loc.GetArrayLength() - 1].ToString()
                    : null;

                partes.Add(campo is null || campo == "body" ? texto : $"{campo}: {texto}");
            }

            return partes.Count > 0 ? string.Join("; ", partes) : null;
        }
        catch (JsonException)
        {
            // Resposta que não é JSON (proxy, página de erro): o chamador
            // mostra o texto cru.
            return null;
        }
    }
}
