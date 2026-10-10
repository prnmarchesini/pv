namespace Clivus.Core;

/// <summary>Os comandos da aba Inversor que vieram com as melhorias de 10/10/2026 (itens 2 a 8, 17 e 19).</summary>
public static partial class PluginInfo
{
    /// <summary>"Ver em campo" da linha do inversor (item 4): o GUID; o retângulo fica selecionado, com zoom nele, até o Esc, e a janela volta.</summary>
    public const string ComandoEletricaVer = "CLIVUS_ELETRICA_VER";

    /// <summary>Só no build de teste: o local dos inversores pela aba (Automático, À mão, renomear a área, Repartir, a soma dos limites e o estado da linha).</summary>
    public const string ComandoEletricaJanelaLocalAutomatico = "CLIVUS_ELETRICA_JANELA_LOCAL_AUTO";
}
