using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Uma área registrada no desenho.
/// </summary>
/// <param name="Identity">Quem ela é.</param>
/// <param name="Handle">Onde ela está neste arquivo.</param>
internal sealed record AreaRecord(AreaIdentity Identity, string Handle);

/// <summary>
/// O registro central das áreas do desenho.
///
/// Existe para o plugin saber o que existe sem varrer o desenho inteiro: um
/// arquivo de projeto tem milhares de entidades, e abrir todas para perguntar
/// "você é nossa?" a cada comando seria caro demais.
///
/// O registro é um índice, não a verdade. A verdade é o XData de cada
/// entidade, que viaja com ela. Quando os dois discordam — e discordam, porque
/// o registro não viaja na cópia entre desenhos — quem manda é o XData, e o
/// reindexar do passo 2.4 reconstrói o registro a partir dele.
///
/// O formato (cabeçalho, versão, quantidade, campos por item) mora em
/// <see cref="PluginRecords"/>, junto com as garantias que ele dá. Aqui fica
/// só o que é da área: quantos campos ela ocupa e como eles viram identidade.
/// </summary>
internal static class AreaStore
{
    private const string Chave = "AREAS";
    private const int VersaoDoFormato = 1;

    /// <summary>GUID, nome, handle e data.</summary>
    private const int CamposPorArea = 4;

    private const string OQueE = "de áreas";

    /// <summary>As áreas registradas, na ordem em que foram gravadas.</summary>
    internal static IReadOnlyList<AreaRecord> Load(Database database) => Ler(database).Items;

    /// <summary>
    /// Lê o registro e diz o que encontrou de errado nele.
    ///
    /// Nenhum descarte é silencioso: registro truncado, de outra versão, ou com
    /// entrada ilegível deixa rastro no log e devolve um problema para quem tem
    /// editor avisar.
    /// </summary>
    internal static RecordTableResult<AreaRecord> Ler(Database database) =>
        PluginRecords.Load<AreaRecord>(database, Chave, VersaoDoFormato, CamposPorArea, Montar, OQueE);

    /// <summary>Grava a lista inteira, substituindo a anterior.</summary>
    internal static void Save(Database database, IReadOnlyList<AreaRecord> areas) =>
        PluginRecords.Save(database, Chave, VersaoDoFormato, CamposPorArea, areas, Campos);

    /// <summary>
    /// Acrescenta ou atualiza uma área no registro, pelo identificador dela.
    /// </summary>
    /// <param name="problema">
    /// O que havia de errado no registro anterior, ou null. A gravação
    /// acontece de qualquer jeito — recusar deixaria a área recém-criada fora
    /// do índice —, mas quem chama precisa contar ao usuário que ficou um
    /// reindexar para rodar.
    /// </param>
    internal static void Upsert(Database database, AreaRecord area, out string? problema)
    {
        ArgumentNullException.ThrowIfNull(area);

        var registro = Ler(database);
        problema = registro.Problem;

        var atuais = registro.Items.ToList();
        var posicao = atuais.FindIndex(a => a.Identity.Id == area.Identity.Id);

        if (posicao >= 0) atuais[posicao] = area;
        else atuais.Add(area);

        Save(database, atuais);
    }

    private static IReadOnlyList<string> Campos(AreaRecord area) =>
    [
        area.Identity.Id.ToString("D"),
        area.Identity.Name,
        area.Handle,
        area.Identity.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
    ];

    private static AreaRecord? Montar(IReadOnlyList<string> campos)
    {
        if (!Guid.TryParse(campos[0], out var id)) return null;

        if (!DateTime.TryParse(
                campos[3],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var criadaEm))
        {
            return null;
        }

        var identidade = new AreaIdentity(id, campos[1], criadaEm);

        return identidade.IsValid ? new AreaRecord(identidade, campos[2]) : null;
    }
}
