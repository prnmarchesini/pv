namespace Clivus.Core;

/// <summary>Os comandos da numeração das strings e do resumo (elétrica, etapas 15 e 16).</summary>
public static partial class PluginInfo
{
    /// <summary>A aba Numeração numa janela solta (a mesma da janela da configuração elétrica).</summary>
    public const string ComandoNumeracao = "CLIVUS_NUMERACAO";

    /// <summary>A numeração pela linha de comando: composição da tag, varredura, blocos, gerar e editar. Para o nível 2.</summary>
    public const string ComandoNumeracaoAutomatico = "CLIVUS_NUMERACAO_AUTO";

    /// <summary>O resumo elétrico (16.1) na linha de comando, com as linhas de conferência. Para o nível 2.</summary>
    public const string ComandoEletricaResumoAutomatico = "CLIVUS_ELETRICA_RESUMO_AUTO";

    /// <summary>Só no build de teste: grava uma cadeia elétrica de exemplo (UC, trafos, inversores) e aloca as strings. Para o nível 2.</summary>
    public const string ComandoNumeracaoExemploAutomatico = "CLIVUS_NUMERACAO_EXEMPLO_AUTO";

    /// <summary>Só no build de teste: depois do exemplo, dois blocos pelas mesas das strings dos inversores (1 e 2; 3 e 4). Para o nível 2.</summary>
    public const string ComandoNumeracaoExemploBlocosAutomatico = "CLIVUS_NUMERACAO_EXEMPLO_BLOCOS_AUTO";

    /// <summary>"Selecionar" do bloco na aba Numeração: o bloco (nome ou GUID) e a seleção das mesas dele em campo (05/10/2026).</summary>
    public const string ComandoNumeracaoMesas = "CLIVUS_NUMERACAO_MESAS";

    /// <summary>"Mostrar" do bloco na aba Numeração: as mesas do bloco (nome ou GUID) ficam selecionadas no desenho.</summary>
    public const string ComandoNumeracaoMostrar = "CLIVUS_NUMERACAO_MOSTRAR";

    /// <summary>Só no build de teste: o MESMO caminho dos botões da linha do bloco (Mostrar) e o resumo da lista, fora de comando. Para o nível 2.</summary>
    public const string ComandoNumeracaoJanelaAutomatico = "CLIVUS_NUMERACAO_JANELA_AUTO";
}
