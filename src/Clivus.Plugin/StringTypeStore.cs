using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A biblioteca de tipos de string gravada no desenho (elétrica, 11.1), no
/// dicionário do plugin, chave "STRING_TIPOS".
/// </summary>
internal static class StringTypeStore
{
    private const string Chave = "STRING_TIPOS";
    private const int VersaoDoFormato = 1;
    private static readonly string OQueE = Tr.N("de tipos de string");

    internal static RecordTableResult<StringType> Ler(Database database) =>
        PluginRecords.Load<StringType>(database, Chave, VersaoDoFormato, StringType.FieldCount, StringType.Parse, OQueE);

    internal static void Gravar(Database database, IReadOnlyList<StringType> tipos) =>
        PluginRecords.Save(database, Chave, VersaoDoFormato, StringType.FieldCount, tipos, t => t.ToFields());

    /// <summary>
    /// Lê a biblioteca, aplica a mudança e grava. A frase de problema do
    /// registro (se havia) volta para quem chamou mostrar.
    /// </summary>
    internal static string? Mudar(Database database, Action<StringLibrary> mudanca)
    {
        var lido = Ler(database);
        var biblioteca = new StringLibrary(lido.Items);
        mudanca(biblioteca);
        Gravar(database, biblioteca.Types);
        return lido.Problem;
    }
}
