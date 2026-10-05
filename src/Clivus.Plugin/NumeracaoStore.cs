using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A configuração da numeração das strings gravada no desenho (elétrica,
/// etapa 15), no dicionário do plugin: a composição da tag na chave
/// "NUMERACAO".
/// </summary>
internal static class NumeracaoStore
{
    private const string ChaveDoEsquema = "NUMERACAO";
    private const int VersaoDoEsquema = 1;
    private static readonly string OQueEsquema = Tr.N("da composição da tag");

    /// <summary>A composição gravada, ou a padrão (T1.I1.S1) se não há nenhuma; o problema do registro, se havia.</summary>
    internal static (TagScheme Esquema, string? Problema) Esquema(Database database)
    {
        var lido = PluginRecords.Load<TagScheme>(database, ChaveDoEsquema, VersaoDoEsquema, TagScheme.FieldCount, TagScheme.Parse, OQueEsquema);
        return (lido.Items.Count > 0 ? lido.Items[0] : TagScheme.Default, lido.Problem);
    }

    internal static void GravarEsquema(Database database, TagScheme esquema)
    {
        if (esquema.Problem() is { } problema) throw new ArgumentException(problema, nameof(esquema));
        PluginRecords.Save(database, ChaveDoEsquema, VersaoDoEsquema, TagScheme.FieldCount, [esquema], e => e.ToFields());
    }
}
