namespace Clivus.Core;

/// <summary>Os comandos da potência do módulo e do PAN na estrutura (Melhorias de 10/10/2026, itens 14 e 15).</summary>
public static partial class PluginInfo
{
    /// <summary>"Trocar potência do módulo…" do botão direito da área (item 14): a potência em Wp, simulação para a usina inteira; Mesa desfaz.</summary>
    public const string ComandoTrocarPotencia = "CLIVUS_TROCAR_POTENCIA";

    /// <summary>Atualizar a potência das mesas desenhadas pelo PAN da estrutura (item 15): o nome da estrutura (vazio: todas). Só a potência muda.</summary>
    public const string ComandoPotenciaPeloPan = "CLIVUS_POTENCIA_PELO_PAN";

    /// <summary>Carregar o .PAN na estrutura (item 15): o nome da estrutura e o caminho do arquivo. Grava os dados elétricos e a potência no perfil do desenho.</summary>
    public const string ComandoEstruturaPan = "CLIVUS_ESTRUTURA_PAN";

    /// <summary>Só no build de teste (nível 2): a fonte do módulo de cada estrutura, a potência simulada, as divergências e o kWp do recontar e do resumo.</summary>
    public const string ComandoPotenciaAutomatico = "CLIVUS_POTENCIA_AUTO";
}
