using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// A biblioteca de tipos de string gravada no desenho (elétrica, 11.1), no
/// dicionário do plugin, chave "STRING_TIPOS". O formato mora no Core
/// (<see cref="StringTypeRecords"/>).
/// </summary>
internal static class StringTypeStore
{
    private const string Chave = "STRING_TIPOS";
    private static readonly string OQueE = Tr.N("de tipos de string");

    internal static RecordTableResult<StringType> Ler(Database database)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, Chave);
            var texto = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            var lido = StringTypeRecords.Read(texto, OQueE);

            if (lido.Problem is { } problema)
            {
                var cru = texto is null ? "(vazio)" : string.Join(" | ", texto);
                RegistroDeDiagnostico.Registrar($"Registro com problema: {problema}. Conteúdo: {(cru.Length > 4000 ? cru[..4000] + "..." : cru)}");
            }

            return lido;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler o registro de tipos de string do desenho.", erro);
            return new RecordTableResult<StringType>([], Tr.F("o registro {0} está ilegível", Tr.T(OQueE)));
        }
    }

    internal static void Gravar(Database database, IReadOnlyList<StringType> tipos)
    {
        var buffer = new ResultBuffer();
        foreach (var campo in StringTypeRecords.Write(tipos)) buffer.Add(new TypedValue((int)DxfCode.Text, campo));
        PluginDictionary.Save(database, Chave, buffer);
    }

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
