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
}
