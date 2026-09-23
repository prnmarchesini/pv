using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// Grava e lê a identidade da linha de alinhamento na própria entidade, em
/// XData.
///
/// O formato do pacote mora em <see cref="PluginXData"/>; aqui fica só o que é
/// do alinhamento. E o que é dele, além do nome, é o <b>lado</b> — a
/// informação que o desenho não carrega sozinho e que custa caro perder.
/// </summary>
internal static class AlignmentXData
{
    /// <summary>Versão do formato deste pacote.</summary>
    private const int VersaoDoFormato = 1;

    /// <summary>GUID, nome, lado e data.</summary>
    private const int QuantosCampos = 4;

    /// <summary>Grava a identidade na entidade, substituindo a anterior.</summary>
    internal static void Save(Transaction transacao, Entity entidade, AlignmentIdentity identidade)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        PluginXData.Save(
            transacao,
            entidade,
            AlignmentIdentity.Tipo,
            VersaoDoFormato,
            identidade.Id.ToString("D"),
            identidade.Name,
            // Nome do lado, e nunca o número da enumeração — ver LineSides.Name.
            identidade.Side.Name(),
            identidade.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A identidade gravada na entidade, ou null se ela não for um alinhamento
    /// nosso — ou se o que está lá não puder ser lido.
    /// </summary>
    internal static AlignmentIdentity? Load(Entity entidade)
    {
        var campos = PluginXData.Load(entidade, AlignmentIdentity.Tipo, VersaoDoFormato, QuantosCampos);
        if (campos is null) return null;

        if (!Guid.TryParse(campos[0], out var id)) return null;

        // Só o nome vale, e nenhum número: Enum.TryParse aceitaria "1" como
        // Left, que é exatamente a porta que este formato fecha.
        if (!LineSides.TryParseName(campos[2], out var lado)) return null;

        if (!DateTime.TryParse(
                campos[3],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var criadoEm))
        {
            return null;
        }

        var identidade = new AlignmentIdentity(id, campos[1], lado, criadoEm);

        return identidade.IsValid ? identidade : null;
    }
}
