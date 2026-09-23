using System.Globalization;

namespace UFV.Core;

/// <summary>Em que pé está o terreno gravado no desenho.</summary>
public enum ProvenanceState
{
    /// <summary>Nunca se processou terreno neste desenho.</summary>
    SemCarimbo,

    /// <summary>O terreno gravado corresponde à superfície como ela está agora.</summary>
    Atual,

    /// <summary>A superfície continua lá, mas mudou depois do processamento.</summary>
    Desatualizado,

    /// <summary>A superfície que gerou o terreno não está mais no desenho.</summary>
    SuperficieSumiu,
}

/// <summary>
/// O carimbo de proveniência: qual superfície gerou o terreno, e quando.
///
/// Não é preciosismo. Depois de uma terraplenagem existe terreno novo no mesmo
/// desenho, e é exatamente aí que alguém abre um arquivo antigo achando que é
/// o atual e compra pilar do tamanho errado. O carimbo é o que permite ao
/// plugin dizer "este resultado está velho" em vez de deixar o número passar.
/// </summary>
/// <param name="Surface">A superfície como ela estava quando foi processada.</param>
/// <param name="ProcessedAt">Quando o processamento aconteceu.</param>
/// <param name="PluginVersion">Versão do plugin que processou.</param>
public sealed record ProvenanceStamp(
    SurfaceFingerprint Surface,
    DateTime ProcessedAt,
    string PluginVersion)
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Data e hora do processamento, como o usuário lê.</summary>
    public string ProcessedAtText => ProcessedAt.ToString("dd/MM/yyyy HH:mm", Brasil);
}

/// <summary>
/// A pergunta que o carimbo existe para responder: o terreno gravado ainda
/// vale?
///
/// Mora no Core porque é regra, não é CAD: quem lê o desenho entrega o carimbo
/// gravado e a superfície como ela está agora, e a decisão sai daqui — e pode
/// ser testada sem abrir nada.
/// </summary>
public static class ProvenanceCheck
{
    /// <summary>
    /// Compara o carimbo gravado com a superfície como ela está agora.
    /// </summary>
    /// <param name="gravado">O carimbo lido do desenho, ou null se não houver.</param>
    /// <param name="agora">
    /// A superfície de mesmo handle, como está agora, ou null se ela não
    /// estiver mais no desenho.
    /// </param>
    public static ProvenanceState Evaluate(ProvenanceStamp? gravado, SurfaceFingerprint? agora)
    {
        if (gravado is null) return ProvenanceState.SemCarimbo;
        if (agora is null) return ProvenanceState.SuperficieSumiu;

        return agora.MatchesTerrainOf(gravado.Surface)
            ? ProvenanceState.Atual
            : ProvenanceState.Desatualizado;
    }

    /// <summary>
    /// O aviso a mostrar, ou null quando não há o que avisar.
    ///
    /// Silêncio quando está tudo certo, e silêncio quando nunca se processou
    /// nada: aviso que aparece à toa é aviso que o usuário aprende a ignorar.
    /// </summary>
    public static string? Warning(ProvenanceStamp? gravado, SurfaceFingerprint? agora)
    {
        var estado = Evaluate(gravado, agora);

        return estado switch
        {
            ProvenanceState.SemCarimbo => null,
            ProvenanceState.Atual => null,

            ProvenanceState.SuperficieSumiu =>
                $"O terreno deste desenho foi calculado sobre a superfície "
                + $"\"{gravado!.Surface.Name}\", que não está mais aqui. "
                + "Os resultados estão desatualizados: processe o terreno de novo.",

            ProvenanceState.Desatualizado =>
                $"A superfície \"{agora!.Name}\" mudou depois do processamento de "
                + $"{gravado!.ProcessedAtText} ({string.Join("; ", agora.DescribeChangesFrom(gravado.Surface))}). "
                + "Os resultados estão desatualizados: processe o terreno de novo.",

            _ => null,
        };
    }
}
