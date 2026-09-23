using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// A ponte entre as tabelas de registro do Core e o dicionário do desenho.
///
/// Todo o miolo — cabeçalho, versão, conferência de quantidade, fatiamento em
/// campos, contagem de ilegíveis — mora em <see cref="RecordTable"/>, no Core,
/// onde tem teste de nível 1. Aqui fica só o que precisa do AutoCAD: converter
/// entre lista de texto e <see cref="ResultBuffer"/>, e falar com o
/// dicionário.
///
/// A divisão não é estética. Enquanto o formato vivia deste lado, as três
/// garantias que ele dá não tinham teste em lugar nenhum — a revisão do 4.2
/// teve que reimplementar o algoritmo fora do repositório para conferir que
/// uma refatoração não tinha quebrado nada.
/// </summary>
internal static class PluginRecords
{
    /// <summary>Grava a lista inteira sob a chave, substituindo a anterior.</summary>
    internal static void Save<T>(
        Database database,
        string chave,
        int versao,
        int camposPorItem,
        IReadOnlyList<T> itens,
        Func<T, IReadOnlyList<string>> campos)
    {
        ArgumentNullException.ThrowIfNull(database);

        var texto = RecordTable.Write(versao, camposPorItem, itens, campos);
        var buffer = new ResultBuffer();

        foreach (var campo in texto) buffer.Add(new TypedValue((int)DxfCode.Text, campo));

        PluginDictionary.Save(database, chave, buffer);
    }

    /// <summary>Lê a lista gravada sob a chave, e diz o que encontrou de errado.</summary>
    internal static RecordTableResult<T> Load<T>(
        Database database,
        string chave,
        int versao,
        int camposPorItem,
        Func<IReadOnlyList<string>, T?> montar,
        string oQueE)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(database);

        try
        {
            using var dados = PluginDictionary.Load(database, chave);

            var texto = dados?.AsArray()
                .Select(campo => campo.Value as string ?? string.Empty)
                .ToList();

            var lido = RecordTable.Read(texto, versao, camposPorItem, montar, oQueE);

            if (lido.Problem is { } problema)
                RegistroDeDiagnostico.Registrar($"Registro com problema: {problema}.");

            return lido;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Não consegui ler o registro {oQueE} do desenho.", erro);
            return new RecordTableResult<T>([], $"o registro {oQueE} está ilegível");
        }
    }
}
