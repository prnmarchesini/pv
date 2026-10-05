namespace Clivus.Core;

// Os comandos das melhorias da aba Inversor (05/10/2026).
public static partial class PluginInfo
{
    /// <summary>
    /// Só no build de teste: o botão Selecionar da aba Inversor pelo mesmo
    /// caminho da janela solta (contexto da aplicação, fora de comando do
    /// documento). Para o nível 2.
    /// </summary>
    public const string ComandoEletricaJanelaSelecionarAutomatico = "CLIVUS_ELETRICA_JANELA_SELECIONAR_AUTO";

    /// <summary>Só no build de teste: a coluna Trafo / o "Pôr no trafo" da tabela de inversores, pelo caminho da janela.</summary>
    public const string ComandoEletricaJanelaTrafoAutomatico = "CLIVUS_ELETRICA_JANELA_TRAFO_AUTO";

    /// <summary>Só no build de teste: o Salvar modelo da aba Inversor (com a potência), pelo caminho da janela.</summary>
    public const string ComandoEletricaJanelaModeloAutomatico = "CLIVUS_ELETRICA_JANELA_MODELO_AUTO";

    /// <summary>Só no build de teste: as linhas da tabela de inversores (strings, kWp, kW, CC/CA), como a aba monta.</summary>
    public const string ComandoEletricaJanelaTabelaAutomatico = "CLIVUS_ELETRICA_JANELA_TABELA_AUTO";
}
