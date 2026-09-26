using System.Globalization;

namespace UFV.Core;

/// <summary>
/// Um grupo de mesas com nome (7.9): o GUID do grupo, o nome que o usuário
/// deu e os GUIDs das mesas. A mesa não sabe do grupo; o grupo é que
/// aponta para as mesas. Mesa apagada continua no grupo (a lista a conta
/// como "sumida"): se o usuário desfizer o apagar, ela volta a contar sem
/// o grupo ter mudado. Só recriar o grupo com o mesmo nome a tira.
/// </summary>
/// <param name="Id">O GUID do grupo.</param>
/// <param name="Name">O nome, como o usuário deu.</param>
/// <param name="Tables">Os GUIDs das mesas, sem repetição.</param>
/// <param name="CreatedAt">Quando foi criado.</param>
/// <param name="Number">
/// O número do grupo, o que vai escrito no meio do contorno dele no
/// desenho (1, 2, 3…, na ordem de criação; quem apaga não renumera os
/// outros). Zero em registro antigo, que ainda não tinha número.
/// </param>
public sealed record TableGroup(Guid Id, string Name, IReadOnlyList<Guid> Tables, DateTime CreatedAt, int Number = 0)
{
    /// <summary>Quantos campos de texto ocupa no registro: GUID, nome, mesas (uma lista), data, número.</summary>
    public const int FieldCount = 5;

    /// <summary>Os campos do registro antes do número (26/09/2026).</summary>
    private const int FieldCountWithoutNumber = 4;

    /// <summary>O separador dos GUIDs no campo das mesas.</summary>
    private const char Separador = ';';

    public bool IsValid =>
        Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Tables.Count > 0 && Number >= 0
        && Tables.All(t => t != Guid.Empty) && Tables.Distinct().Count() == Tables.Count;

    /// <summary>O letreiro do desenho: o número em cima, o nome embaixo.</summary>
    public string Caption => Number > 0 ? $"{Number}\\P{Name}" : Name;

    /// <summary>Os campos, na ordem: GUID, nome, mesas separadas por ponto e vírgula, data invariante.</summary>
    public IReadOnlyList<string> ToFields() =>
    [
        Id.ToString("D"),
        Name,
        string.Join(Separador, Tables.Select(t => t.ToString("D"))),
        CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        Number.ToString(CultureInfo.InvariantCulture),
    ];

    /// <summary>O inverso de <see cref="ToFields"/>; null se não dá para ler.</summary>
    public static TableGroup? Parse(IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count < FieldCountWithoutNumber) return null;
        if (!Guid.TryParse(fields[0], out var id)) return null;

        var numero = 0;
        if (fields.Count >= FieldCount && !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out numero)) return null;

        var mesas = new List<Guid>();

        foreach (var parte in fields[2].Split(Separador, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Guid.TryParse(parte, out var mesa)) return null;
            mesas.Add(mesa);
        }

        if (!DateTime.TryParse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var quando))
            return null;

        var grupo = new TableGroup(id, fields[1], mesas, quando, numero);

        return grupo.IsValid ? grupo : null;
    }
}
