namespace UFV.Core;

/// <summary>
/// A identidade de uma mesa desenhada: regra sagrada 3, "pilar, módulo e
/// mesa são objetos únicos com GUID próprio".
/// </summary>
/// <param name="Id">O GUID da mesa.</param>
/// <param name="Label">O letreiro (F1.3).</param>
/// <param name="StartElevation">A cota da ponta baixa no início.</param>
/// <param name="EndElevation">A cota da ponta baixa no fim.</param>
/// <param name="TiltRadians">A inclinação transversal.</param>
/// <param name="Marked">Se a fileira marcou a mesa.</param>
/// <param name="Reason">O motivo da marca, ou null.</param>
/// <param name="Dirty">
/// Se a mesa está SUJA: o que está desenhado não corresponde mais ao que o
/// motor calculou (foi movida, copiada, editada, ou o usuário mandou
/// recalcular). Suja é pintada de vermelho e entra no "recalcular tudo"
/// (etapa 7). Nasce limpa.
/// </param>
/// <param name="DirtyReason">Por que está suja ("movida", "copiada", "pedido do usuário"), ou null quando limpa.</param>
/// <param name="ModulePowerWatts">
/// A potência do módulo com que a mesa foi desenhada, em W, ou null numa
/// mesa de antes do 7.6 (a recontagem usa a do perfil atual e avisa). É o
/// que faz o kWp do recontar ser o dos módulos que ESTÃO no desenho, e não
/// o do perfil que estiver escolhido no dia.
/// </param>
public sealed record TableIdentity(
    Guid Id,
    string Label,
    double StartElevation,
    double EndElevation,
    double TiltRadians,
    bool Marked,
    string? Reason,
    bool Dirty = false,
    string? DirtyReason = null,
    double? ModulePowerWatts = null)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "Mesa";

    /// <summary>Se a identidade é utilizável: suja sem motivo, ou limpa com motivo, não é.</summary>
    public bool IsValid =>
        Id != Guid.Empty && !string.IsNullOrWhiteSpace(Label)
        && double.IsFinite(StartElevation) && double.IsFinite(EndElevation) && double.IsFinite(TiltRadians)
        && Dirty == !string.IsNullOrWhiteSpace(DirtyReason)
        && (ModulePowerWatts is null || (double.IsFinite(ModulePowerWatts.Value) && ModulePowerWatts > 0));

    /// <summary>A mesma mesa, suja por este motivo. O motivo é obrigatório.</summary>
    /// <exception cref="ArgumentException">Motivo em branco.</exception>
    public TableIdentity AsDirty(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return this with { Dirty = true, DirtyReason = reason.Trim() };
    }

    /// <summary>A mesma mesa, limpa.</summary>
    public TableIdentity AsClean() => this with { Dirty = false, DirtyReason = null };

    /// <summary>"F1.3: limpa" ou "F1.3: SUJA (movida)".</summary>
    public string DescribeState() => Dirty ? $"{Label}: SUJA ({DirtyReason})" : $"{Label}: limpa";
}

/// <summary>A identidade de um pilar desenhado.</summary>
/// <param name="Id">O GUID do pilar.</param>
/// <param name="Table">O GUID da mesa dele.</param>
/// <param name="Number">O número na mesa, a partir de 1 na estação zero.</param>
/// <param name="Station">A estação ao longo da mesa.</param>
/// <param name="Length">O comprimento total (P1), ou null se o pilar tem problema.</param>
/// <param name="Embedment">O enterro (P2).</param>
/// <param name="FreeHeight">A altura livre (P3), ou null sem terreno.</param>
/// <param name="Problem">O que estourou, ou null.</param>
/// <param name="GroundZ">A cota do terreno no pé, ou null sem terreno. Com o topo, é o que confere P3 de fora.</param>
public sealed record PillarIdentity(
    Guid Id,
    Guid Table,
    int Number,
    double Station,
    double? Length,
    double Embedment,
    double? FreeHeight,
    string? Problem,
    double? GroundZ)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "Pilar";

    /// <summary>Se a identidade é utilizável.</summary>
    public bool IsValid => Id != Guid.Empty && Table != Guid.Empty && Number > 0 && double.IsFinite(Station);
}

/// <summary>A identidade de um módulo desenhado (o bloco).</summary>
/// <param name="Id">O GUID do módulo.</param>
/// <param name="Table">O GUID da mesa dele.</param>
/// <param name="Column">A coluna na mesa.</param>
/// <param name="Row">A fileira na mesa: 0 é a de baixo.</param>
/// <param name="Clearance">A altura livre da ponta baixa, só na fileira de baixo; null nas outras ou sem terreno.</param>
public sealed record ModuleIdentity(Guid Id, Guid Table, int Column, int Row, double? Clearance)
{
    /// <summary>O tipo do bloco do módulo, como vai no XData.</summary>
    public const string Tipo = "Modulo";

    /// <summary>Se a identidade é utilizável.</summary>
    public bool IsValid => Id != Guid.Empty && Table != Guid.Empty && Column >= 0 && Row >= 0;
}

/// <summary>
/// A identidade de uma nota do desenho: a cota (risco e texto) e o aviso de
/// mesa que não cabe. Não é peça da mesa, mas pertence a ela: apagar ou
/// refazer a mesa leva as notas junto.
/// </summary>
/// <param name="Id">O GUID da nota.</param>
/// <param name="Table">O GUID da mesa.</param>
public sealed record NoteIdentity(Guid Id, Guid Table)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "Nota";

    public bool IsValid => Id != Guid.Empty && Table != Guid.Empty;
}

/// <summary>
/// A identidade da face superior de um módulo: entidade própria, GUID
/// próprio, e o GUID do módulo de que ela é a face. "Identidade própria" é
/// o que o Renan pediu para o PVsyst; a ligação com o módulo é o campo, e
/// não o mesmo GUID — a regra sagrada 3 diz "nenhum GUID repetido", e um
/// verificador literal dela tem que passar.
/// </summary>
/// <param name="Id">O GUID da face.</param>
/// <param name="Module">O GUID do módulo.</param>
/// <param name="Table">O GUID da mesa.</param>
/// <param name="Column">A coluna na mesa.</param>
/// <param name="Row">A fileira na mesa.</param>
public sealed record FaceIdentity(Guid Id, Guid Module, Guid Table, int Column, int Row)
{
    /// <summary>O tipo, como vai no XData.</summary>
    public const string Tipo = "Face";

    /// <summary>Se a identidade é utilizável.</summary>
    public bool IsValid => Id != Guid.Empty && Module != Guid.Empty && Table != Guid.Empty && Column >= 0 && Row >= 0;
}
