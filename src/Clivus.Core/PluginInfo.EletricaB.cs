namespace Clivus.Core;

// Os comandos das etapas 12 a 14 (subestação, trafo, inversor).
public static partial class PluginInfo
{
    /// <summary>Põe (ou move) em campo o retângulo de um equipamento: subestação, trafo ou inversor (12.3, 13.2, 14.6).</summary>
    public const string ComandoEletricaPosicionar = "CLIVUS_ELETRICA_POSICIONAR";

    /// <summary>Aloca strings num inversor pela seleção em campo, só strings (14.3).</summary>
    public const string ComandoEletricaAlocar = "CLIVUS_ELETRICA_ALOCAR";

    /// <summary>A configuração elétrica pela linha de comando: cadastrar, vincular, listar (só no build de teste; nível 2).</summary>
    public const string ComandoEletricaAutomatico = "CLIVUS_ELETRICA_AUTO";
}
