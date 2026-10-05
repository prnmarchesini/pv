using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// Uma mesa da assinatura de arranjo: quantos módulos por fileira
/// (<see cref="Columns"/>) e quantas fileiras (<see cref="Rows"/>: 1 no 1V, 2
/// no 2V). É o formato, não a mesa: a mesma assinatura casa com qualquer mesa
/// igual da usina (elétrica, 11.1 e 11.6).
/// </summary>
public sealed record ArrangementTable(int Columns, int Rows)
{
    public bool IsValid => Columns > 0 && Rows > 0;

    public int ModuleCount => Columns * Rows;
}

/// <summary>
/// A assinatura de arranjo de um tipo de string: as mesas que ele cobre, na
/// ordem em que estão na fileira (uma de 28 2V; duas de 14 2V). O gerador
/// (11.6) só casa o tipo com grupos de mesas de assinatura igual. Vazia
/// enquanto as mesas não foram escolhidas (11.2).
/// </summary>
public sealed class StringArrangement : IEquatable<StringArrangement>
{
    public static StringArrangement Empty { get; } = new([]);

    public StringArrangement(IReadOnlyList<ArrangementTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        Tables = tables.ToList();
    }

    public IReadOnlyList<ArrangementTable> Tables { get; }

    public bool IsEmpty => Tables.Count == 0;

    public int ModuleCount => Tables.Sum(t => t.ModuleCount);

    /// <summary>"14x2" ou "7x2;7x2": colunas x fileiras, mesa a mesa.</summary>
    public string ToText() =>
        string.Join(';', Tables.Select(t => t.Columns.ToString(CultureInfo.InvariantCulture) + "x" + t.Rows.ToString(CultureInfo.InvariantCulture)));

    /// <summary>O inverso de <see cref="ToText"/>; null se o texto não é uma assinatura.</summary>
    public static StringArrangement? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Empty;

        var mesas = new List<ArrangementTable>();

        foreach (var parte in texto.Split(';'))
        {
            var lados = parte.Trim().Split('x');
            if (lados.Length != 2
                || !int.TryParse(lados[0], NumberStyles.None, CultureInfo.InvariantCulture, out var colunas)
                || !int.TryParse(lados[1], NumberStyles.None, CultureInfo.InvariantCulture, out var fileiras))
                return null;

            var mesa = new ArrangementTable(colunas, fileiras);
            if (!mesa.IsValid) return null;
            mesas.Add(mesa);
        }

        return new StringArrangement(mesas);
    }

    public bool Equals(StringArrangement? other) => other is not null && Tables.SequenceEqual(other.Tables);

    public override bool Equals(object? obj) => Equals(obj as StringArrangement);

    public override int GetHashCode() => ToText().GetHashCode(StringComparison.Ordinal);

    public override string ToString() => ToText();
}

/// <summary>
/// Um tipo de string da biblioteca (elétrica, 11.1): nome, assinatura de
/// arranjo, o desenho do cartesiano das mesas escolhidas (11.2, null
/// enquanto não há mesas) e o traçado: as strings do tipo, cada uma com o +
/// e o − nas pontas (11.3, 11.4). Uma mesa 2V de 28 pode ter duas strings
/// de 14.
/// </summary>
public sealed record StringType(Guid Id, string Name, StringArrangement Arrangement, ArrangementSketch? Sketch = null, IReadOnlyList<StringRoute>? Strings = null)
{
    /// <summary>As strings do traçado (vazio enquanto não há traçado).</summary>
    public IReadOnlyList<StringRoute> Routes => Strings ?? [];

    /// <summary>Se o tipo pode gerar: tem mesas e traçado válido.</summary>
    public bool CanGenerate => !Arrangement.IsEmpty && Routes.Count > 0 && StringRouting.WhyInvalid(Arrangement, Routes) is null;

    /// <summary>Os campos gravados no desenho: GUID, nome, assinatura, desenho do cartesiano.</summary>
    public const int FieldCount = 4;

    /// <summary>Os campos do formato 1 (11.1), sem o desenho do cartesiano.</summary>
    public const int FieldCountV1 = 3;

    /// <summary>O desenho do cartesiano, ou o padrão se o tipo não tem um.</summary>
    public ArrangementSketch SketchOrDefault => Sketch is { } d && d.Gaps.Count == Math.Max(0, Arrangement.Tables.Count - 1) ? d : ArrangementSketch.Default(Arrangement);

    public IReadOnlyList<string> ToFields() => [Id.ToString("D"), Name, Arrangement.ToText(), Sketch?.ToText() ?? string.Empty];

    /// <summary>O inverso de <see cref="ToFields"/> (aceita também os 3 campos do formato 1); null se não dá para ler.</summary>
    public static StringType? Parse(IReadOnlyList<string> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        if (campos.Count < FieldCountV1) return null;
        if (!Guid.TryParse(campos[0], out var id) || id == Guid.Empty) return null;
        if (string.IsNullOrWhiteSpace(campos[1])) return null;
        if (StringArrangement.Parse(campos[2]) is not { } arranjo) return null;

        ArrangementSketch? desenho = null;
        if (campos.Count > FieldCountV1 && campos[3].Length > 0 && (desenho = ArrangementSketch.Parse(campos[3])) is null) return null;

        return new StringType(id, campos[1], arranjo, desenho);
    }

    public bool Equals(StringType? other) =>
        other is not null && Id == other.Id && Name == other.Name && Arrangement.Equals(other.Arrangement) && Equals(Sketch, other.Sketch)
        && Routes.SequenceEqual(other.Routes);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// A biblioteca de tipos de string do desenho (elétrica, 11.1): acrescentar
/// com nome automático ("Modelo 1, 2, 3", sem reaproveitar número de quem
/// saiu), renomear sem repetir nome, apagar.
/// </summary>
public sealed class StringLibrary
{
    private readonly List<StringType> _tipos;

    public StringLibrary(IEnumerable<StringType> tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        _tipos = tipos.ToList();
    }

    public IReadOnlyList<StringType> Types => _tipos;

    public StringType? Find(Guid id) => _tipos.FirstOrDefault(t => t.Id == id);

    /// <summary>Acrescenta um tipo com o próximo nome livre.</summary>
    public StringType Add(StringArrangement arranjo, ArrangementSketch? desenho = null)
    {
        ArgumentNullException.ThrowIfNull(arranjo);

        var tipo = new StringType(Guid.NewGuid(), Tr.F("Modelo {0}", ProximoNumero()), arranjo, desenho);
        _tipos.Add(tipo);
        return tipo;
    }

    /// <summary>
    /// Troca as mesas do tipo (11.2: o usuário escolheu em campo). Null se
    /// deu certo, o porquê se não.
    /// </summary>
    public string? SetArrangement(Guid id, StringArrangement arranjo, ArrangementSketch desenho)
    {
        ArgumentNullException.ThrowIfNull(arranjo);
        ArgumentNullException.ThrowIfNull(desenho);

        var posicao = _tipos.FindIndex(t => t.Id == id);
        if (posicao < 0) return Tr.T("esse tipo de string não está mais na biblioteca");
        if (arranjo.IsEmpty) return Tr.T("nenhuma mesa do plugin na seleção");

        // Mesas diferentes: o traçado antigo não serve mais (células de outra grade).
        var antes = _tipos[posicao];
        _tipos[posicao] = antes with { Arrangement = arranjo, Sketch = desenho, Strings = antes.Arrangement.Equals(arranjo) ? antes.Strings : null };
        return null;
    }

    /// <summary>
    /// Grava o traçado do tipo (11.3, 11.4). Null se deu certo; o porquê se
    /// o traçado não serve (módulo que não existe, repetido, string de um
    /// módulo só).
    /// </summary>
    public string? SetStrings(Guid id, IReadOnlyList<StringRoute> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        var posicao = _tipos.FindIndex(t => t.Id == id);
        if (posicao < 0) return Tr.T("esse tipo de string não está mais na biblioteca");
        if (_tipos[posicao].Arrangement.IsEmpty) return Tr.T("o tipo ainda não tem mesas");
        if (StringRouting.WhyInvalid(_tipos[posicao].Arrangement, strings) is { } porque) return porque;

        _tipos[posicao] = _tipos[posicao] with { Strings = strings.ToList() };
        return null;
    }

    /// <summary>Renomeia; null se deu certo, o porquê se não.</summary>
    public string? Rename(Guid id, string? nome)
    {
        var posicao = _tipos.FindIndex(t => t.Id == id);
        if (posicao < 0) return Tr.T("esse tipo de string não está mais na biblioteca");

        var limpo = nome?.Trim() ?? string.Empty;
        if (limpo.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (_tipos.Any(t => t.Id != id && string.Equals(t.Name, limpo, StringComparison.CurrentCultureIgnoreCase)))
            return Tr.F("já existe um tipo de string chamado \"{0}\"", limpo);

        _tipos[posicao] = _tipos[posicao] with { Name = limpo };
        return null;
    }

    /// <summary>Tira o tipo da biblioteca; se ele existia.</summary>
    public bool Remove(Guid id) => _tipos.RemoveAll(t => t.Id == id) > 0;

    /// <summary>
    /// O número depois do maior dos nomes do padrão ("Modelo 3", no idioma da
    /// tela): quem foi apagado não volta a ser usado, e um nome dado pelo
    /// usuário ("Leste 28") não conta.
    /// </summary>
    private int ProximoNumero()
    {
        var prefixo = Tr.F("Modelo {0}", string.Empty);
        var maior = 0;

        foreach (var tipo in _tipos)
        {
            if (!tipo.Name.StartsWith(prefixo, StringComparison.CurrentCultureIgnoreCase)) continue;

            var resto = tipo.Name.AsSpan(prefixo.Length);
            if (resto.Length > 0 && int.TryParse(resto, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                maior = Math.Max(maior, n);
        }

        return maior + 1;
    }
}

/// <summary>Um pedaço do texto do traçado de um tipo, como vai no registro "STRING_TRACADOS".</summary>
public sealed record StringRouteChunk(Guid Type, int Index, string Text)
{
    public const int FieldCount = 3;

    public IReadOnlyList<string> ToFields() => [Type.ToString("D"), Index.ToString(CultureInfo.InvariantCulture), Text];

    public static StringRouteChunk? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var tipo) && int.TryParse(c[1], NumberStyles.None, CultureInfo.InvariantCulture, out var i)
            ? new StringRouteChunk(tipo, i, c[2])
            : null;
}

/// <summary>
/// A gravação da biblioteca no desenho (texto puro, pelo <see cref="RecordTable"/>).
/// Os tipos (formato 2, 11.2: com o desenho do cartesiano; o formato 1 do
/// 11.1 ainda é lido) e, à parte, o traçado de cada tipo em pedaços de 200
/// caracteres (um texto do desenho não pode ser longo; 11.3).
/// </summary>
public static class StringTypeRecords
{
    public const int Version = 2;

    public const int RouteVersion = 1;

    /// <summary>Tamanho de cada pedaço do texto do traçado.</summary>
    private const int Pedaco = 200;

    public static IReadOnlyList<string> Write(IReadOnlyList<StringType> tipos) =>
        RecordTable.Write(Version, StringType.FieldCount, tipos, t => t.ToFields());

    public static IReadOnlyList<string> WriteRoutes(IReadOnlyList<StringType> tipos)
    {
        var pedacos = new List<StringRouteChunk>();
        foreach (var tipo in tipos.Where(t => t.Routes.Count > 0))
        {
            var texto = StringRouting.ToText(tipo.Routes);
            for (var i = 0; i * Pedaco < texto.Length; i++)
                pedacos.Add(new StringRouteChunk(tipo.Id, i, texto.Substring(i * Pedaco, Math.Min(Pedaco, texto.Length - i * Pedaco))));
        }

        return RecordTable.Write(RouteVersion, StringRouteChunk.FieldCount, pedacos, p => p.ToFields());
    }

    public static RecordTableResult<StringType> Read(IReadOnlyList<string>? texto, string oQueE) => Read(texto, null, oQueE);

    /// <summary>Os tipos com o traçado de cada um. Traçado estragado vira problema, e o tipo fica sem traçado.</summary>
    public static RecordTableResult<StringType> Read(IReadOnlyList<string>? texto, IReadOnlyList<string>? tracados, string oQueE)
    {
        var formato1 = texto is { Count: > 1 } && texto[1] == "1";
        var tipos = formato1
            ? RecordTable.Read(texto, 1, StringType.FieldCountV1, StringType.Parse, oQueE)
            : RecordTable.Read(texto, Version, StringType.FieldCount, StringType.Parse, oQueE);

        if (tracados is null) return tipos;

        var lidos = RecordTable.Read(tracados, RouteVersion, StringRouteChunk.FieldCount, StringRouteChunk.Parse, oQueE);
        var problema = tipos.Problem ?? lidos.Problem;
        var porTipo = lidos.Items.GroupBy(p => p.Type).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Index).ToList());
        var itens = new List<StringType>();

        foreach (var tipo in tipos.Items)
        {
            if (!porTipo.TryGetValue(tipo.Id, out var pedacos))
            {
                itens.Add(tipo);
                continue;
            }

            var inteiro = pedacos.Select((p, i) => p.Index == i).All(x => x);
            var rotas = inteiro ? StringRouting.Parse(string.Concat(pedacos.Select(p => p.Text))) : null;

            if (rotas is null || StringRouting.WhyInvalid(tipo.Arrangement, rotas) is not null)
            {
                problema ??= Tr.F("o traçado de {0} está estragado e foi deixado de lado", tipo.Name);
                itens.Add(tipo);
                continue;
            }

            itens.Add(tipo with { Strings = rotas });
        }

        return new RecordTableResult<StringType>(itens, problema);
    }
}
