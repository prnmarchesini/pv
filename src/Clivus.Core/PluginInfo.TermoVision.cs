namespace Clivus.Core;

// Os comandos da ponte com o TermoVision (etapa 33).
public static partial class PluginInfo
{
    /// <summary>
    /// O botão único do TermoVision (28/10/2026, Renan: "não é melhor um botão
    /// só?"): abre a área quando o desenho ainda não veio de lá (ou quando
    /// chegou uma pelo navegador); senão manda as mesas.
    /// </summary>
    public const string ComandoTermoVision = "CLIVUS_TV";

    /// <summary>Abre uma área do TermoVision: o endereço clivus:// vindo da pasta de entrada ou colado.</summary>
    public const string ComandoTermoVisionAbrir = "CLIVUS_TV_ABRIR";

    /// <summary>Teste de nível 2: monta o desenho a partir de um pacote .zip no disco, sem rede.</summary>
    public const string ComandoTermoVisionAbrirAutomatico = "CLIVUS_TV_ABRIR_AUTO";

    /// <summary>Manda as mesas do desenho de volta ao TermoVision.</summary>
    public const string ComandoTermoVisionExportar = "CLIVUS_TV_EXPORTAR";

    /// <summary>Teste de nível 2: grava o corpo do retorno num arquivo em vez de mandar.</summary>
    public const string ComandoTermoVisionExportarAutomatico = "CLIVUS_TV_EXPORTAR_AUTO";

    /// <summary>Chave do dicionário do plugin com o servidor e o envio do TermoVision.</summary>
    public const string ChaveTermoVision = "TERMOVISION";

    /// <summary>
    /// Teste de nível 2 do projeto sem envio (contrato v2, 02/11/2026): grava o
    /// corpo num arquivo, ou manda ao servidor com "ENVIAR".
    /// </summary>
    public const string ComandoTermoVisionProjetoAutomatico = "CLIVUS_TV_PROJETO_AUTO";

    /// <summary>Chave do dicionário do plugin com o projeto do TermoVision que o desenho criou (o GUID, a usina e o link).</summary>
    public const string ChaveTermoVisionProjeto = "TERMOVISION_PROJETO";
}
