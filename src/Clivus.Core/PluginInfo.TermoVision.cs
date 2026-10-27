namespace Clivus.Core;

// Os comandos da ponte com o TermoVision (etapa 33).
public static partial class PluginInfo
{
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
}
