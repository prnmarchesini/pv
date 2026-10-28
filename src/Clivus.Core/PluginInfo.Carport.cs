namespace Clivus.Core;

// Os comandos do carport (etapas 34 a 36, plano/carport).
public static partial class PluginInfo
{
    /// <summary>Desenha uma fila de carport: o eixo clicado no estacionamento, tudo assentado no terreno.</summary>
    public const string ComandoCarport = "CLIVUS_CARPORT";

    /// <summary>Refaz todas as filas de carport com o carport e o terreno de agora.</summary>
    public const string ComandoCarportRegerar = "CLIVUS_CARPORT_REGERAR";

    /// <summary>Apaga a fila de carport da peça clicada.</summary>
    public const string ComandoCarportApagar = "CLIVUS_CARPORT_APAGAR";

    /// <summary>A lista de material de todas as filas de carport, em Excel.</summary>
    public const string ComandoCarportLista = "CLIVUS_CARPORT_LISTA";

    /// <summary>Teste de nível 2: cadastra o carport de referência se o desenho não tem nenhum e desenha a fila pelos pontos digitados.</summary>
    public const string ComandoCarportAutomatico = "CLIVUS_CARPORT_AUTO";
}
