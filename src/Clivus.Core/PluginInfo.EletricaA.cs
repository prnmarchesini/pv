namespace Clivus.Core;

// Os comandos das strings (elétrica, etapa 11, passos 11.2 a 11.8).
public static partial class PluginInfo
{
    /// <summary>Escolhe em campo as mesas de um tipo de string (11.2): cria o tipo, ou troca as mesas do que a janela pediu.</summary>
    public const string ComandoStringMesas = "CLIVUS_STRING_MESAS";

    /// <summary>Gera as strings nas mesas selecionadas (11.6 a 11.8): casa os tipos com grupos de mesas e desenha o traçado.</summary>
    public const string ComandoStringGerar = "CLIVUS_STRING_GERAR";

    /// <summary>O traçado de um tipo pela linha de comando, clique a clique como no cartesiano (11.3, 11.4). Para o nível 2.</summary>
    public const string ComandoStringTracadoAutomatico = "CLIVUS_STRING_TRACADO_AUTO";

    /// <summary>Clonar e espelhar um tipo pela linha de comando (11.5). Para o nível 2.</summary>
    public const string ComandoStringModeloAutomatico = "CLIVUS_STRING_MODELO_AUTO";
}
