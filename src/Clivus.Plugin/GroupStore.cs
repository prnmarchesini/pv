using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O registro dos grupos de mesas (7.9), no dicionário do desenho, no
/// formato de <see cref="PluginRecords"/>. O nome é único (sem distinguir
/// maiúsculas): criar com nome que existe substitui o grupo.
/// </summary>
internal static class GroupStore
{
    private const string Chave = "GRUPOS";
    private const int VersaoDoFormato = 1;
    private static readonly string OQueE = Tr.N("de grupos");

    internal static RecordTableResult<TableGroup> Ler(Database database) =>
        PluginRecords.Load<TableGroup>(database, Chave, VersaoDoFormato, TableGroup.FieldCount, TableGroup.Parse, OQueE);

    internal static IReadOnlyList<TableGroup> Load(Database database) => Ler(database).Items;

    internal static void Save(Database database, IReadOnlyList<TableGroup> grupos) =>
        PluginRecords.Save(database, Chave, VersaoDoFormato, TableGroup.FieldCount, grupos, g => g.ToFields());

    /// <summary>Acrescenta ou substitui pelo nome. Devolve o problema do registro anterior, se havia.</summary>
    internal static string? Upsert(Database database, TableGroup grupo, out bool substituiu)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        var registro = Ler(database);
        var atuais = registro.Items.ToList();
        var posicao = atuais.FindIndex(g => MesmoNome(g.Name, grupo.Name));

        substituiu = posicao >= 0;

        // O número: o do grupo substituído, ou o próximo livre (quem apaga
        // não renumera os outros; o número escrito no desenho não muda).
        var numero = substituiu ? atuais[posicao].Number : 0;
        if (numero <= 0) numero = atuais.Select(g => g.Number).DefaultIfEmpty(0).Max() + 1;

        var numerado = grupo with { Number = numero };

        if (substituiu) atuais[posicao] = numerado;
        else atuais.Add(numerado);

        Save(database, atuais);

        return registro.Problem;
    }

    internal static bool Remove(Database database, string nome)
    {
        var atuais = Ler(database).Items.ToList();
        var removidos = atuais.RemoveAll(g => MesmoNome(g.Name, nome));

        if (removidos > 0) Save(database, atuais);

        return removidos > 0;
    }

    internal static TableGroup? Find(Database database, string nome) =>
        Load(database).FirstOrDefault(g => MesmoNome(g.Name, nome));

    internal static bool MesmoNome(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
