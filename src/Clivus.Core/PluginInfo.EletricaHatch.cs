namespace Clivus.Core;

// O hatch da área do trafo (05/10/2026).
public static partial class PluginInfo
{
    /// <summary>
    /// Só no build de teste: o botão "Hatch da área" (ou "Hatch de todos",
    /// com "*") da aba Transformador pelo mesmo caminho da janela solta
    /// (contexto da aplicação, fora de comando do documento). Para o nível 2.
    /// </summary>
    public const string ComandoEletricaJanelaHatchAutomatico = "CLIVUS_ELETRICA_JANELA_HATCH_AUTO";
}
