using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// O registro das mesas removidas (7.2): o vigia anota aqui cada contorno
/// apagado, e o "recontar" (7.6) é quem consome. Mora no dicionário do
/// desenho, no formato de <see cref="PluginRecords"/>.
/// </summary>
internal static class RemovalStore
{
    private const string Chave = "REMOVIDAS";
    private const int VersaoDoFormato = 1;
    private const string OQueE = "de mesas removidas";

    internal static RecordTableResult<TableRemoval> Ler(Database database) =>
        PluginRecords.Load<TableRemoval>(database, Chave, VersaoDoFormato, TableRemoval.FieldCount, TableRemoval.Parse, OQueE);

    internal static void Save(Database database, IReadOnlyList<TableRemoval> remocoes) =>
        PluginRecords.Save(database, Chave, VersaoDoFormato, TableRemoval.FieldCount, remocoes, r => r.ToFields());

    /// <summary>Acrescenta remoções, sem repetir mesa. Devolve o problema do registro anterior, se havia.</summary>
    internal static string? Add(Database database, IReadOnlyList<TableRemoval> novas)
    {
        ArgumentNullException.ThrowIfNull(novas);

        var registro = Ler(database);
        var atuais = registro.Items.ToList();

        foreach (var nova in novas)
        {
            if (atuais.All(r => r.Id != nova.Id)) atuais.Add(nova);
        }

        Save(database, atuais);

        return registro.Problem;
    }

    /// <summary>Tira do registro as mesas cujo contorno voltou.</summary>
    internal static void Remove(Database database, IReadOnlyList<Guid> mesas)
    {
        ArgumentNullException.ThrowIfNull(mesas);

        var atuais = Ler(database).Items.Where(r => !mesas.Contains(r.Id)).ToList();

        Save(database, atuais);
    }
}
