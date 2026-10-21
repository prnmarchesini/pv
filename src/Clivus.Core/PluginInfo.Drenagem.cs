namespace Clivus.Core;

/// <summary>Os comandos da drenagem (etapas 25 a 31, plano/drenagem).</summary>
public static partial class PluginInfo
{
    /// <summary>Análise › Flechas (26): a janela das flechas de sentido da água sobre o terreno.</summary>
    public const string ComandoDrenagemFlechas = "CLIVUS_DRN_FLECHAS";

    /// <summary>Análise › Linhas (27): desenha (ou escolhe) a linha de análise e põe o gráfico do perfil.</summary>
    public const string ComandoDrenagemLinha = "CLIVUS_DRN_LINHA";

    /// <summary>Desenho › Canaleta (28): a janela da canaleta (seção, conversão da linha, 3D e perfil).</summary>
    public const string ComandoDrenagemCanaleta = "CLIVUS_DRN_CANALETA";

    /// <summary>Memória › Bacias (30): as zonas de influência de cada canaleta, com hatch e a lista canaleta × área.</summary>
    public const string ComandoDrenagemBacias = "CLIVUS_DRN_BACIAS";

    /// <summary>Memória › Cálculo (31): parâmetros, tabelas do memorial e o Excel.</summary>
    public const string ComandoDrenagemMemoria = "CLIVUS_DRN_MEMORIA";

    /// <summary>"Gerar flechas" (26.2): [Areas/Janela/Tudo], com a configuração gravada pela janela; Janela pede os dois cantos.</summary>
    public const string ComandoDrenagemFlechasGerar = "CLIVUS_DRN_FLECHAS_GERAR";

    /// <summary>"Apagar flechas" (26.2): as flechas e a legenda.</summary>
    public const string ComandoDrenagemFlechasApagar = "CLIVUS_DRN_FLECHAS_APAGAR";

    /// <summary>Só no build de teste: confere contra o TIN tudo o que a drenagem desenhou e escreve o placar (nível 2).</summary>
    public const string ComandoDrenagemConferirAutomatico = "CLIVUS_DRN_CONFERIR_AUTO";

    /// <summary>"Converter" da janela da canaleta (28.2): seção (nome da biblioteca), assentamento [Borda/Minima] e a seleção; linha ou polilinha vira canaleta, canaleta troca de seção.</summary>
    public const string ComandoDrenagemCanaletaConverter = "CLIVUS_DRN_CANALETA_CONVERTER";

    /// <summary>"Voltar a linha" (28.5): as canaletas escolhidas voltam a ser linha de análise (sai o 3D).</summary>
    public const string ComandoDrenagemCanaletaDesfazer = "CLIVUS_DRN_CANALETA_DESFAZER";

    /// <summary>"Pegar da canaleta" (28.5): clica numa canaleta e a seção dela vai para a janela.</summary>
    public const string ComandoDrenagemCanaletaLer = "CLIVUS_DRN_CANALETA_LER";

    /// <summary>"Calcular" das bacias (30.1): [Areas/Tudo/Polilinha]; Polilinha pede a polilinha fechada da região.</summary>
    public const string ComandoDrenagemBaciasCalcular = "CLIVUS_DRN_BACIAS_CALCULAR";

    /// <summary>"Apagar" das bacias: o hatch, a borda e o rótulo (os ajustes manuais ficam).</summary>
    public const string ComandoDrenagemBaciasApagar = "CLIVUS_DRN_BACIAS_APAGAR";

    /// <summary>"Ajuste manual" (30.3): uma polilinha fechada e a canaleta para onde a área dela vai, ou [Soltar] o ajuste.</summary>
    public const string ComandoDrenagemBaciaManual = "CLIVUS_DRN_BACIA_MANUAL";
}
