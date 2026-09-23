using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Grava e lê a identidade da área na própria entidade, em XData.
///
/// O formato do pacote mora em <see cref="PluginXData"/>; aqui fica só o que é
/// da área. O limite de 255 bytes por texto do XData não incomoda: o que se
/// grava são um GUID, um nome curto e uma data.
/// </summary>
internal static class AreaXData
{
    /// <summary>Versão do formato deste pacote.</summary>
    private const int VersaoDoFormato = 1;

    /// <summary>GUID, nome e data.</summary>
    private const int QuantosCampos = 3;

    /// <summary>Grava a identidade na entidade, substituindo a anterior.</summary>
    internal static void Save(Transaction transacao, Entity entidade, AreaIdentity identidade)
    {
        ArgumentNullException.ThrowIfNull(identidade);

        PluginXData.Save(
            transacao,
            entidade,
            AreaIdentity.Tipo,
            VersaoDoFormato,
            identidade.Id.ToString("D"),
            identidade.Name,
            identidade.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A identidade gravada na entidade, ou null se ela não for nossa — ou se
    /// o que está lá não puder ser lido.
    /// </summary>
    internal static AreaIdentity? Load(Entity entidade)
    {
        var campos = PluginXData.Load(entidade, AreaIdentity.Tipo, VersaoDoFormato, QuantosCampos);
        if (campos is null) return null;

        if (!Guid.TryParse(campos[0], out var id)) return null;

        if (!DateTime.TryParse(
                campos[2],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var criadaEm))
        {
            return null;
        }

        var identidade = new AreaIdentity(id, campos[1], criadaEm);

        return identidade.IsValid ? identidade : null;
    }
}
