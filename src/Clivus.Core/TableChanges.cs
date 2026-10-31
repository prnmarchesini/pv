using System.Globalization;

namespace Clivus.Core;

/// <summary>O que o banco do desenho disse que aconteceu com uma peça.</summary>
public enum ChangeKind
{
    /// <summary>A peça foi modificada (movida, girada, editada, mudada de cor).</summary>
    Modified,

    /// <summary>A peça foi acrescentada com a nossa identidade: é uma cópia.</summary>
    Appended,

    /// <summary>A peça foi apagada.</summary>
    Erased,

    /// <summary>A peça apagada voltou (desfazer de um apagar).</summary>
    Restored,
}

/// <summary>Uma mesa cujo contorno foi apagado: a remoção registrada (7.2, para o 7.6 recontar).</summary>
/// <param name="Id">O GUID da mesa.</param>
/// <param name="Label">O letreiro que ela tinha.</param>
/// <param name="When">Quando o vigia viu.</param>
public sealed record TableRemoval(Guid Id, string Label, DateTime When)
{
    /// <summary>Quantos campos de texto ocupa no registro.</summary>
    public const int FieldCount = 3;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Label);

    /// <summary>Os campos, na ordem: GUID, letreiro, data invariante.</summary>
    public IReadOnlyList<string> ToFields() =>
        [Id.ToString("D"), Label, When.ToString("O", CultureInfo.InvariantCulture)];

    /// <summary>O inverso de <see cref="ToFields"/>; null se não dá para ler.</summary>
    public static TableRemoval? Parse(IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count < FieldCount) return null;
        if (!Guid.TryParse(fields[0], out var id)) return null;

        if (!DateTime.TryParse(fields[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var quando))
            return null;

        var remocao = new TableRemoval(id, fields[1], quando);

        return remocao.IsValid ? remocao : null;
    }

    public string Describe() => Tr.F("{0} removida em {1:dd/MM/yyyy HH:mm}", Label, When.ToLocalTime());
}

/// <summary>As peças de uma mesa no desenho, contadas por tipo.</summary>
/// <param name="Modules">Os blocos de módulo.</param>
/// <param name="Faces">As faces superiores.</param>
/// <param name="Pillars">Os pilares.</param>
/// <param name="Bars">As peças da estrutura do biposte.</param>
public readonly record struct TablePieceCount(int Modules, int Faces, int Pillars, int Bars)
{
    /// <summary>Todas as peças somadas.</summary>
    public int Total => Modules + Faces + Pillars + Bars;
}

/// <summary>O que o vigia decidiu ao fim de um comando.</summary>
/// <param name="Dirty">Mesa → motivo da sujeira.</param>
/// <param name="Removed">Mesas cujo contorno foi apagado.</param>
/// <param name="Restored">Mesas cujo contorno voltou (desfazer): saem do registro de removidas.</param>
public sealed record ChangeResolution(
    IReadOnlyDictionary<Guid, string> Dirty,
    IReadOnlyList<TableRemoval> Removed,
    IReadOnlyList<Guid> Restored)
{
    public bool IsEmpty => Dirty.Count == 0 && Removed.Count == 0 && Restored.Count == 0;
}

/// <summary>
/// O livro de mudanças do vigia (7.2): acumula, durante um comando do
/// usuário, o que o banco do desenho disse sobre as nossas peças, e ao fim
/// do comando decide o que fazer com cada mesa. É puro: quem escuta o banco
/// é o plugin; quem decide é isto, com teste.
///
/// A regra é "só marca e pinta, nada mais": mesa tocada fica suja com um
/// motivo, e mesa cujo CONTORNO foi apagado é registrada como removida (o
/// contorno é quem carrega a identidade; sem ele não há onde gravar
/// sujeira). Entre motivos, vale o mais grave: copiada > peça apagada >
/// movida ou editada.
/// </summary>
public sealed class PendingChanges
{
    /// <summary>Motivo gravado quando a peça foi modificada.</summary>
    public static readonly string ReasonModified = Tr.N("movida ou editada");

    /// <summary>Motivo gravado quando apareceu uma peça com a nossa identidade.</summary>
    public static readonly string ReasonAppended = Tr.N("copiada");

    /// <summary>Motivo gravado quando uma peça (não o contorno) foi apagada.</summary>
    public static readonly string ReasonErased = Tr.N("peça apagada");

    private readonly Dictionary<Guid, int> _severidade = [];
    private readonly Dictionary<Guid, string> _removidas = [];
    private readonly HashSet<Guid> _restauradas = [];

    public bool IsEmpty => _severidade.Count == 0 && _removidas.Count == 0 && _restauradas.Count == 0;

    /// <summary>
    /// Anota um evento sobre uma peça da mesa.
    /// </summary>
    /// <param name="table">O GUID da mesa a que a peça pertence.</param>
    /// <param name="kind">O que aconteceu.</param>
    /// <param name="isContour">Se a peça é o contorno (a que carrega a identidade).</param>
    /// <param name="label">O letreiro da mesa, quando se sabe; usado na remoção.</param>
    public void Note(Guid table, ChangeKind kind, bool isContour, string? label = null)
    {
        if (table == Guid.Empty) return;

        if (kind == ChangeKind.Erased && isContour)
        {
            _removidas[table] = string.IsNullOrWhiteSpace(label) ? Tr.N("(sem letreiro)") : label.Trim();
            _restauradas.Remove(table);
            return;
        }

        if (kind == ChangeKind.Restored && isContour)
        {
            // O contorno voltou: não está mais removida, e a mesa foi tocada.
            _removidas.Remove(table);
            _restauradas.Add(table);
        }

        var severidade = kind switch
        {
            ChangeKind.Appended => 3,
            ChangeKind.Erased => 2,
            _ => 1,
        };

        _severidade[table] = Math.Max(severidade, _severidade.GetValueOrDefault(table));
    }

    /// <summary>
    /// Se a pendência da mesa pode ter vindo só do salvar. Até 30/10/2026,
    /// salvar o desenho sujava toda mesa com peça sólida (o biposte): o
    /// AutoCAD regrava cada sólido ao salvar e avisa "modificado", e o vigia
    /// via edição ("depois de salvar as mesas ficaram todas vermelhas, bug dos
    /// bravos"). E cada salvar regravava o motivo como "movida ou editada",
    /// por cima do que estava (peça apagada, copiada, pedido do usuário): o
    /// motivo gravado não basta. Pode ser do salvar a pendência "movida ou
    /// editada" de mesa com sólido, que continua onde foi desenhada (a âncora
    /// gravada e no lugar), inteira (as peças de uma mesa do mesmo tipo, ver
    /// <see cref="TypicalPieces"/>) e sozinha no lugar (outra mesa com a
    /// mesma âncora é cópia colada em cima). O resto fica pendente. O que não
    /// se distingue: a mesa marcada à mão e a peça mexida sem sair do lugar.
    /// </summary>
    /// <param name="table">A identidade gravada no contorno.</param>
    /// <param name="hasSolids">Se a mesa tem peça sólida (a estrutura do biposte).</param>
    /// <param name="anchorMoved">Se o primeiro vértice do contorno saiu da âncora; null sem âncora.</param>
    /// <param name="complete">Se a mesa tem as peças de uma mesa inteira do tipo dela.</param>
    /// <param name="alone">Se nenhuma outra mesa tem a mesma âncora em planta.</param>
    public static bool MayBeFromSave(TableIdentity table, bool hasSolids, bool? anchorMoved, bool complete, bool alone)
    {
        ArgumentNullException.ThrowIfNull(table);

        return table.Dirty
            && string.Equals(table.DirtyReason, ReasonModified, StringComparison.Ordinal)
            && hasSolids
            && anchorMoved == false
            && complete
            && alone;
    }

    /// <summary>
    /// Quantas peças uma mesa inteira tem, por tipo de mesa (o nome do
    /// perfil): a contagem mais comum entre as mesas do desenho desse tipo;
    /// no empate, a maior (falta peça, não sobra). Mesa sem nome de perfil
    /// conta junto com as outras sem nome.
    /// </summary>
    public static IReadOnlyDictionary<string, TablePieceCount> TypicalPieces(IEnumerable<(string? Profile, TablePieceCount Count)> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        return tables
            .GroupBy(t => t.Profile ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(t => t.Count)
                    .OrderByDescending(c => c.Count())
                    .ThenByDescending(c => c.Key.Total)
                    .First().Key,
                StringComparer.Ordinal);
    }

    /// <summary>Decide, esvazia o livro e devolve a decisão.</summary>
    /// <param name="when">A data das remoções.</param>
    public ChangeResolution Resolve(DateTime when)
    {
        var sujas = new Dictionary<Guid, string>();

        foreach (var (mesa, severidade) in _severidade)
        {
            // Mesa removida não fica suja: não há onde gravar.
            if (_removidas.ContainsKey(mesa)) continue;

            sujas[mesa] = severidade switch
            {
                3 => ReasonAppended,
                2 => ReasonErased,
                _ => ReasonModified,
            };
        }

        var removidas = _removidas.Select(r => new TableRemoval(r.Key, r.Value, when)).ToList();
        var restauradas = _restauradas.ToList();

        _severidade.Clear();
        _removidas.Clear();
        _restauradas.Clear();

        return new ChangeResolution(sujas, removidas, restauradas);
    }
}
