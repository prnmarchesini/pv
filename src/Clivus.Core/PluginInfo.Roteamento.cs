namespace Clivus.Core;

/// <summary>Os comandos do roteamento de cabo (etapas 17 a 24).</summary>
public static partial class PluginInfo
{
    /// <summary>A janela da rota de cabos (17.1): abas CC, Combiner, CA, MT e Resumo, cada rota liberada quando os dois lados do trecho existem no desenho.</summary>
    public const string ComandoRotaCabos = "CLIVUS_ROTA_CABOS";

    /// <summary>"Selecionar vala" da aba (17.4): a rota e a seleção das polilinhas em campo, que viram vala dela.</summary>
    public const string ComandoRotaVala = "CLIVUS_ROTA_VALA";

    /// <summary>"Gerar" da aba (18.6, 20.4, 21.4): a rota; apaga os cabos dela e desenha de novo, avisando e pintando o que não deu.</summary>
    public const string ComandoRotaGerar = "CLIVUS_ROTA_GERAR";

    /// <summary>"Apagar" da aba (17.9): a rota e Tudo ou Selecionar (os lances escolhidos em campo). Só cabo.</summary>
    public const string ComandoRotaApagar = "CLIVUS_ROTA_APAGAR";

    /// <summary>"Forçar lado" (18.3): a rota, as strings em campo e um clique do lado para onde os cabos delas vão (ou Automatico).</summary>
    public const string ComandoRotaLado = "CLIVUS_ROTA_LADO";

    /// <summary>Local dos inversores (10/10/2026): Area (clique na polilinha fechada), Automatico (pela rota CC) ou Manual, e os inversores.</summary>
    public const string ComandoEletricaLocal = "CLIVUS_ELETRICA_LOCAL";

    /// <summary>"Recolocar automáticos" da aba CC: os inversores automáticos voltam ao ponto de menor cabo e a rota é refeita.</summary>
    public const string ComandoRotaRecolocar = "CLIVUS_ROTA_RECOLOCAR";

    /// <summary>Só no build de teste: confere cada vala e cada cabo contra o TIN e escreve os vértices dos cabos (nível 2).</summary>
    public const string ComandoRotaConferirAutomatico = "CLIVUS_ROTA_CONFERIR_AUTO";

    /// <summary>"+ Strings" da combiner (19.2): a combiner (GUID ou nome) e a seleção das strings em campo, como a alocação no inversor.</summary>
    public const string ComandoEletricaCombiner = "CLIVUS_ELETRICA_COMBINER";
}
