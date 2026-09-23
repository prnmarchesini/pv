using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// Uma linha de alinhamento registrada no desenho.
/// </summary>
/// <param name="Identity">Quem ela é, e de que lado ficam as mesas.</param>
/// <param name="Handle">Onde ela está neste arquivo.</param>
internal sealed record AlignmentRecord(AlignmentIdentity Identity, string Handle);

/// <summary>
/// O registro central das linhas de alinhamento do desenho.
///
/// Mesmo papel do registro de áreas: índice para não varrer o desenho inteiro a
/// cada comando. E a mesma ressalva — o índice não é a verdade. A verdade é o
/// XData de cada entidade, que viaja com ela na cópia entre desenhos, e quando
/// os dois discordam quem manda é o XData.
///
/// O formato vem de <see cref="PluginRecords"/>; aqui fica só o que é do
/// alinhamento.
/// </summary>
internal static class AlignmentStore
{
    private const string Chave = "ALINHAMENTOS";
    private const int VersaoDoFormato = 1;

    /// <summary>GUID, nome, lado, handle e data.</summary>
    private const int CamposPorAlinhamento = 5;

    private const string OQueE = "de alinhamentos";

    /// <summary>Os alinhamentos registrados, na ordem em que foram gravados.</summary>
    internal static IReadOnlyList<AlignmentRecord> Load(Database database) => Ler(database).Items;

    /// <summary>Lê o registro e diz o que encontrou de errado nele.</summary>
    internal static RecordTableResult<AlignmentRecord> Ler(Database database) =>
        PluginRecords.Load<AlignmentRecord>(
            database, Chave, VersaoDoFormato, CamposPorAlinhamento, Montar, OQueE);

    /// <summary>Grava a lista inteira, substituindo a anterior.</summary>
    internal static void Save(Database database, IReadOnlyList<AlignmentRecord> alinhamentos) =>
        PluginRecords.Save(database, Chave, VersaoDoFormato, CamposPorAlinhamento, alinhamentos, Campos);

    /// <summary>
    /// Acrescenta ou atualiza um alinhamento no registro, pelo identificador.
    /// </summary>
    /// <param name="problema">
    /// O que havia de errado no registro anterior, ou null. A gravação
    /// acontece de qualquer jeito, mas quem chama precisa avisar o usuário.
    /// </param>
    internal static void Upsert(Database database, AlignmentRecord alinhamento, out string? problema)
    {
        ArgumentNullException.ThrowIfNull(alinhamento);

        var registro = Ler(database);
        problema = registro.Problem;

        var atuais = registro.Items.ToList();
        var posicao = atuais.FindIndex(a => a.Identity.Id == alinhamento.Identity.Id);

        if (posicao >= 0) atuais[posicao] = alinhamento;
        else atuais.Add(alinhamento);

        Save(database, atuais);
    }

    private static IReadOnlyList<string> Campos(AlignmentRecord alinhamento) =>
    [
        alinhamento.Identity.Id.ToString("D"),
        alinhamento.Identity.Name,
        // Texto, e não o número da enumeração: acrescentar um valor no meio
        // dela renumeraria os seguintes, e todo alinhamento antigo passaria a
        // dizer o outro lado, sem erro nenhum na leitura.
        alinhamento.Identity.Side.Name(),
        alinhamento.Handle,
        alinhamento.Identity.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
    ];

    private static AlignmentRecord? Montar(IReadOnlyList<string> campos)
    {
        if (!Guid.TryParse(campos[0], out var id)) return null;

        // Só o nome vale: Enum.TryParse aceitaria "1" como Left, e no dia de
        // renumerar a enumeração todo alinhamento gravado diria o outro lado.
        if (!LineSides.TryParseName(campos[2], out var lado)) return null;

        if (!DateTime.TryParse(
                campos[4],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var criadoEm))
        {
            return null;
        }

        var identidade = new AlignmentIdentity(id, campos[1], lado, criadoEm);

        return identidade.IsValid ? new AlignmentRecord(identidade, campos[3]) : null;
    }
}
