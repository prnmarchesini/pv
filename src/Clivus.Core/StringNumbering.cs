using System.Globalization;

namespace Clivus.Core;

// A numeração das strings (elétrica, etapa 15; plano/eletrica/etapas/
// etapa-15-numeracao.md). Toda a lógica mora aqui: a composição da tag, a
// varredura, os blocos e a ordem. O plugin só seleciona, grava e desenha.

/// <summary>
/// A composição da tag em três pedaços (15.1): trafo, inversor e string, cada
/// um com o seu prefixo (ex. "T", "Trafo" ou nada) somado ao número, e um
/// separador entre os pedaços ("." , "-" ou nada, colado). Ex.: T1.I1.S1, ou
/// 1S1 (sem o pedaço do trafo, inversor sem prefixo, colado).
/// <para>
/// O número do trafo e o do inversor são a posição deles na lista do cadastro
/// (1, 2, 3...): eles são nomeados um a um pelo usuário, na ordem que ele pôs
/// (15.2). O da string é o sequencial dentro do inversor, que recomeça do 1 em
/// cada inversor (regra elétrica 8).
/// </para>
/// <para>
/// Só o pedaço do trafo pode ficar de fora: sem o do inversor, duas strings de
/// inversores diferentes teriam a mesma tag.
/// </para>
/// </summary>
public sealed record TagScheme(bool IncludeTransformer, string TransformerPrefix, string InverterPrefix, string StringPrefix, string Separator)
{
    public const int FieldCount = 5;

    /// <summary>O maior prefixo aceito, em caracteres.</summary>
    public const int MaxPrefixLength = 12;

    /// <summary>T1.I1.S1.</summary>
    public static TagScheme Default { get; } = new(true, "T", "I", "S", ".");

    /// <summary>Os separadores aceitos: ponto, risquinho ou nada (colado).</summary>
    public static IReadOnlyList<string> Separators { get; } = [".", "-", string.Empty];

    /// <summary>
    /// O que impede a composição de valer, ou null. Recusa a tag ambígua:
    /// colado, um pedaço sem prefixo depois de outro faz "T12S3" ser T1.I2 ou
    /// T12; prefixo terminando em algarismo, idem.
    /// </summary>
    public string? Problem()
    {
        if (!Separators.Contains(Separator ?? "\0")) return Tr.T("o separador tem que ser ponto, risquinho ou nada");

        foreach (var prefixo in new[] { TransformerPrefix, InverterPrefix, StringPrefix })
        {
            if (prefixo is null) return Tr.T("falta um prefixo");
            if (prefixo.Length > MaxPrefixLength) return Tr.F("o prefixo \"{0}\" passa de {1} caracteres", prefixo, MaxPrefixLength);
            if (prefixo.Any(c => char.IsControl(c) || c is '\\' or '{' or '}' or '|' or '%')) return Tr.F("o prefixo \"{0}\" tem caractere que não pode ir na tag", prefixo);
            if (prefixo.Length > 0 && (char.IsDigit(prefixo[^1]) || char.IsWhiteSpace(prefixo[0]) || char.IsWhiteSpace(prefixo[^1])))
                return Tr.F("o prefixo \"{0}\" não pode terminar em algarismo nem ter espaço nas pontas", prefixo);
        }

        if (Separator!.Length == 0)
        {
            // Colado: todo pedaço depois do primeiro precisa de prefixo.
            if (IncludeTransformer && InverterPrefix!.Length == 0) return Tr.T("colado, o inversor precisa de prefixo (senão T12 é T1 com o inversor 2 ou o trafo 12)");
            if (StringPrefix!.Length == 0) return Tr.T("colado, a string precisa de prefixo (senão 12 é o inversor 1 com a string 2 ou o inversor 12)");
        }

        return null;
    }

    /// <summary>
    /// A tag. <paramref name="transformer"/> null é inversor sem trafo: o
    /// pedaço do trafo sai (com o separador), em vez de um "T0" que parece um
    /// trafo de verdade. Números começam do 1.
    /// </summary>
    public string Compose(int? transformer, int inverter, int str)
    {
        if (transformer is < 1) throw new ArgumentOutOfRangeException(nameof(transformer), transformer, "O número do trafo começa do 1.");
        ArgumentOutOfRangeException.ThrowIfLessThan(inverter, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(str, 1);

        var pedacos = new List<string>(3);
        if (IncludeTransformer && transformer is { } t) pedacos.Add(TransformerPrefix + t.ToString(CultureInfo.InvariantCulture));
        pedacos.Add(InverterPrefix + inverter.ToString(CultureInfo.InvariantCulture));
        pedacos.Add(StringPrefix + str.ToString(CultureInfo.InvariantCulture));
        return string.Join(Separator, pedacos);
    }

    public IReadOnlyList<string> ToFields() => [IncludeTransformer ? "1" : "0", TransformerPrefix, InverterPrefix, StringPrefix, Separator];

    public static TagScheme? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Count < FieldCount || c[0] is not ("0" or "1")) return null;

        var esquema = new TagScheme(c[0] == "1", c[1], c[2], c[3], c[4]);
        return esquema.Problem() is null ? esquema : null;
    }
}

/// <summary>
/// O sentido da varredura das strings (15.2). O sentido nomeado é o que
/// avança; strings na mesma faixa (mesma coluna, nos horizontais; mesma
/// linha, nos verticais) vão de cima para baixo ou da esquerda para a direita.
/// </summary>
public enum ScanDirection
{
    LeftToRight,
    RightToLeft,
    TopToBottom,
    BottomToTop,
}

/// <summary>Uma string a varrer: o GUID, a posição em planta do primeiro módulo dela e a mesa dele (que diz o bloco).</summary>
public sealed record ScanItem(Guid Id, double X, double Y, Guid Table = default);

/// <summary>A ordem da varredura (15.2).</summary>
public static class ScanOrder
{
    /// <summary>
    /// A tolerância da faixa, em metro: strings cujo primeiro módulo difere
    /// menos que isto no eixo do sentido contam como lado a lado (a mesma
    /// coluna ou linha), e aí vale o outro eixo. Menor que um módulo, maior
    /// que o desalinho de uma mesa no terreno.
    /// </summary>
    public const double Band = 0.5;

    /// <summary>O sentido para o usuário: "da esquerda para a direita".</summary>
    public static string Describe(ScanDirection direction) => direction switch
    {
        ScanDirection.LeftToRight => Tr.T("da esquerda para a direita"),
        ScanDirection.RightToLeft => Tr.T("da direita para a esquerda"),
        ScanDirection.TopToBottom => Tr.T("de cima para baixo"),
        _ => Tr.T("de baixo para cima"),
    };

    /// <summary>
    /// Os GUIDs na ordem da varredura. Faixas pelo eixo do sentido (uma faixa
    /// nova quando o item passa de <paramref name="band"/> do começo da
    /// faixa: medido do começo, e não do vizinho, para mesas deslocadas aos
    /// poucos não fundirem colunas inteiras numa faixa só); dentro da faixa,
    /// pelo outro eixo; empate total, pelo GUID (sempre a mesma ordem).
    /// Posição que não é número fica de fora.
    /// </summary>
    public static IReadOnlyList<Guid> Order(IReadOnlyList<ScanItem> items, ScanDirection direction, double band = Band)
    {
        ArgumentNullException.ThrowIfNull(items);

        // P: o eixo que avança; S: o da faixa (de cima para baixo ou da esquerda para a direita).
        (double P, double S) Eixos(ScanItem i) => direction switch
        {
            ScanDirection.LeftToRight => (i.X, -i.Y),
            ScanDirection.RightToLeft => (-i.X, -i.Y),
            ScanDirection.TopToBottom => (-i.Y, i.X),
            _ => (i.Y, i.X),
        };

        var porEixo = items
            .Where(i => double.IsFinite(i.X) && double.IsFinite(i.Y))
            .Select(i => (Item: i, Eixo: Eixos(i)))
            .OrderBy(x => x.Eixo.P)
            .ThenBy(x => x.Item.Id)
            .ToList();

        var faixas = new List<List<(ScanItem Item, (double P, double S) Eixo)>>();
        double? comeco = null;

        foreach (var x in porEixo)
        {
            if (comeco is null || x.Eixo.P - comeco.Value > band)
            {
                faixas.Add([]);
                comeco = x.Eixo.P;
            }

            faixas[^1].Add(x);
        }

        return faixas
            .SelectMany(f => f.OrderBy(x => x.Eixo.S).ThenBy(x => x.Eixo.P).ThenBy(x => x.Item.Id))
            .Select(x => x.Item.Id)
            .ToList();
    }
}

/// <summary>
/// Um bloco da varredura (15.2): as mesas dele e o sentido próprio. A ordem
/// dos blocos na lista é a ordem da numeração (15.3).
/// </summary>
public sealed record NumberingBlock(Guid Id, string Name, ScanDirection Direction, IReadOnlyList<Guid> Tables);

/// <summary>
/// Uma linha do registro da varredura (chave "NUMERACAO_VARREDURA"): a usina
/// inteira (o sentido padrão), um bloco (nome e sentido, na ordem da lista)
/// ou uma mesa de um bloco.
/// </summary>
public sealed record ScanRow(string Kind, Guid Owner, string Value, ScanDirection Direction)
{
    public const int FieldCount = 4;
    public const string Plant = "USINA";
    public const string Block = "BLOCO";
    public const string Table = "MESA";

    public IReadOnlyList<string> ToFields() =>
        [Kind, Owner == Guid.Empty ? string.Empty : Owner.ToString("D"), Value ?? string.Empty, Direction.ToString()];

    public static ScanRow? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Count < FieldCount || c[0] is not (Plant or Block or Table)) return null;
        if (!ElectricalString.OptionalGuid(c[1], out var dono)) return null;
        if (int.TryParse(c[3], out _) || !Enum.TryParse<ScanDirection>(c[3], out var sentido) || !Enum.IsDefined(sentido)) return null;

        return c[0] switch
        {
            Plant => new ScanRow(Plant, Guid.Empty, string.Empty, sentido),
            Block when dono != Guid.Empty && !string.IsNullOrWhiteSpace(c[2]) => new ScanRow(Block, dono, c[2], sentido),
            Table when dono != Guid.Empty && Guid.TryParse(c[2], out var m) && m != Guid.Empty => new ScanRow(Table, dono, m.ToString("D"), sentido),
            _ => null,
        };
    }
}

/// <summary>
/// A configuração da varredura (15.2, 15.3): o sentido da usina inteira (o
/// padrão) e os blocos, na ordem da lista. Uma mesa pertence a no máximo um
/// bloco; a que não está em bloco nenhum é numerada no sentido da usina,
/// depois de todos os blocos.
/// </summary>
public sealed class ScanSetup
{
    private readonly List<NumberingBlock> _blocos;

    public ScanSetup(ScanDirection defaultDirection, IEnumerable<NumberingBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        DefaultDirection = defaultDirection;
        _blocos = blocks.ToList();
    }

    public ScanDirection DefaultDirection { get; set; }

    public IReadOnlyList<NumberingBlock> Blocks => _blocos;

    public NumberingBlock? Find(Guid id) => _blocos.FirstOrDefault(b => b.Id == id);

    /// <summary>O bloco da mesa, ou null (a mesa vai no sentido da usina).</summary>
    public NumberingBlock? BlockOf(Guid table) => _blocos.FirstOrDefault(b => b.Tables.Contains(table));

    /// <summary>O bloco de cada mesa que está em bloco (a primeira ocorrência vale).</summary>
    public IReadOnlyDictionary<Guid, Guid> BlockByTable()
    {
        var dono = new Dictionary<Guid, Guid>();
        foreach (var b in _blocos)
            foreach (var t in b.Tables) dono.TryAdd(t, b.Id);
        return dono;
    }

    /// <summary>Um bloco novo no fim da lista, sem mesas, no sentido da usina, com o próximo nome livre ("Bloco 3").</summary>
    public NumberingBlock AddBlock()
    {
        var prefixo = Tr.F("Bloco {0}", string.Empty);
        var maior = 0;

        foreach (var b in _blocos)
        {
            if (!b.Name.StartsWith(prefixo, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (int.TryParse(b.Name.AsSpan(prefixo.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n)) maior = Math.Max(maior, n);
        }

        var bloco = new NumberingBlock(Guid.NewGuid(), Tr.F("Bloco {0}", maior + 1), DefaultDirection, []);
        _blocos.Add(bloco);
        return bloco;
    }

    /// <summary>
    /// Anda o bloco na lista (15.3): -1 sobe, +1 desce. A ordem da lista é a
    /// da numeração. Falso se o bloco não existe ou sairia da lista.
    /// </summary>
    public bool Move(Guid id, int delta)
    {
        var i = _blocos.FindIndex(b => b.Id == id);
        var destino = i + delta;
        if (i < 0 || delta == 0 || destino < 0 || destino >= _blocos.Count) return false;

        var bloco = _blocos[i];
        _blocos.RemoveAt(i);
        _blocos.Insert(destino, bloco);
        return true;
    }

    /// <summary>
    /// A sequência da usina (15.2, 15.3): o bloco 1 inteiro no sentido dele,
    /// depois o 2, e segue na ordem da lista; por último as strings de mesa
    /// fora de bloco, no sentido da usina.
    /// </summary>
    public IReadOnlyList<Guid> Sequence(IReadOnlyList<ScanItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var dono = BlockByTable();
        var porBloco = items.ToLookup(i => dono.GetValueOrDefault(i.Table));
        var sequencia = new List<Guid>(items.Count);

        foreach (var b in _blocos) sequencia.AddRange(ScanOrder.Order(porBloco[b.Id].ToList(), b.Direction));
        sequencia.AddRange(ScanOrder.Order(porBloco[Guid.Empty].ToList(), DefaultDirection));
        return sequencia;
    }

    /// <summary>Tira o bloco da lista; as mesas dele voltam ao sentido da usina.</summary>
    public bool Remove(Guid id) => _blocos.RemoveAll(b => b.Id == id) > 0;

    public bool SetDirection(Guid id, ScanDirection direction) => Trocar(id, b => b with { Direction = direction });

    /// <summary>Renomeia; null se deu certo, o porquê se não.</summary>
    public string? Rename(Guid id, string? name)
    {
        if (Find(id) is null) return Tr.T("esse bloco não está mais na lista");

        var limpo = name?.Trim() ?? string.Empty;
        if (limpo.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (_blocos.Any(b => b.Id != id && string.Equals(b.Name, limpo, StringComparison.CurrentCultureIgnoreCase)))
            return Tr.F("já existe um bloco chamado \"{0}\"", limpo);

        Trocar(id, b => b with { Name = limpo });
        return null;
    }

    /// <summary>
    /// As mesas do bloco passam a ser estas (substitui as de antes). Mesa que
    /// estava em outro bloco sai dele; volta quantas vieram de outro bloco,
    /// para quem chamou avisar.
    /// </summary>
    public int SetTables(Guid id, IEnumerable<Guid> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (Find(id) is null) throw new ArgumentException("Bloco que não está na lista.", nameof(id));

        var novas = tables.Where(t => t != Guid.Empty).Distinct().ToList();
        var conjunto = novas.ToHashSet();
        var tiradas = 0;

        for (var i = 0; i < _blocos.Count; i++)
        {
            if (_blocos[i].Id == id) continue;

            var ficam = _blocos[i].Tables.Where(t => !conjunto.Contains(t)).ToList();
            tiradas += _blocos[i].Tables.Count - ficam.Count;
            if (ficam.Count != _blocos[i].Tables.Count) _blocos[i] = _blocos[i] with { Tables = ficam };
        }

        Trocar(id, b => b with { Tables = novas });
        return tiradas;
    }

    private bool Trocar(Guid id, Func<NumberingBlock, NumberingBlock> mudanca)
    {
        var i = _blocos.FindIndex(b => b.Id == id);
        if (i < 0) return false;

        _blocos[i] = mudanca(_blocos[i]);
        return true;
    }

    /// <summary>As linhas do registro: a usina, e cada bloco seguido das mesas dele, na ordem da lista.</summary>
    public IReadOnlyList<ScanRow> ToRows()
    {
        var linhas = new List<ScanRow> { new(ScanRow.Plant, Guid.Empty, string.Empty, DefaultDirection) };

        foreach (var b in _blocos)
        {
            linhas.Add(new ScanRow(ScanRow.Block, b.Id, b.Name, b.Direction));
            linhas.AddRange(b.Tables.Select(t => new ScanRow(ScanRow.Table, b.Id, t.ToString("D"), b.Direction)));
        }

        return linhas;
    }

    /// <summary>
    /// A configuração das linhas lidas. Bloco repetido, mesa de bloco que não
    /// existe e mesa que já está em outro bloco são perdidos e contados (quem
    /// chamou avisa).
    /// </summary>
    public static (ScanSetup Setup, int Lost) FromRows(IReadOnlyList<ScanRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var padrao = rows.FirstOrDefault(r => r.Kind == ScanRow.Plant)?.Direction ?? ScanDirection.LeftToRight;
        var blocos = new List<(Guid Id, string Nome, ScanDirection Sentido, List<Guid> Mesas)>();
        var usadas = new HashSet<Guid>();
        var perdidas = 0;

        foreach (var r in rows)
        {
            if (r.Kind == ScanRow.Block)
            {
                if (blocos.Any(b => b.Id == r.Owner)) perdidas++;
                else blocos.Add((r.Owner, r.Value, r.Direction, []));
            }
            else if (r.Kind == ScanRow.Table)
            {
                var dono = blocos.FindIndex(b => b.Id == r.Owner);

                if (dono < 0 || !Guid.TryParse(r.Value, out var mesa) || !usadas.Add(mesa)) perdidas++;
                else blocos[dono].Mesas.Add(mesa);
            }
        }

        return (new ScanSetup(padrao, blocos.Select(b => new NumberingBlock(b.Id, b.Nome, b.Sentido, b.Mesas))), perdidas);
    }
}

/// <summary>Onde está um módulo, para a varredura: a mesa dele e a posição em planta (o centro).</summary>
public sealed record ModuleSpot(Guid Table, double X, double Y);

/// <summary>Até onde vai uma operação de numeração (15.5).</summary>
public enum NumberingScopeKind
{
    /// <summary>A usina inteira.</summary>
    All,

    /// <summary>As strings de um inversor (pelo vínculo).</summary>
    Inverter,

    /// <summary>As strings cujo primeiro módulo está numa mesa do bloco (alvo vazio: as mesas fora de bloco).</summary>
    Block,
}

/// <summary>
/// O alcance de gerar ou apagar (15.5): tudo, um inversor ou um bloco. Cada
/// operação é independente: o que fica fora do alcance não muda.
/// </summary>
public sealed record NumberingScope(NumberingScopeKind Kind, Guid Target)
{
    public static NumberingScope All { get; } = new(NumberingScopeKind.All, Guid.Empty);

    public static NumberingScope OfInverter(Guid inverter) => new(NumberingScopeKind.Inverter, inverter);

    /// <summary>Um bloco; <see cref="Guid.Empty"/> são as mesas fora de bloco.</summary>
    public static NumberingScope OfBlock(Guid block) => new(NumberingScopeKind.Block, block);

    /// <summary>Se a string entra: pelo vínculo (inversor) ou pela mesa do primeiro módulo (bloco). String sem posição não entra em bloco nenhum.</summary>
    public bool Contains(ElectricalString s, IReadOnlyDictionary<Guid, Guid> blockByTable, IReadOnlyDictionary<Guid, ModuleSpot> modules)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(blockByTable);
        ArgumentNullException.ThrowIfNull(modules);

        return Kind switch
        {
            NumberingScopeKind.All => true,
            NumberingScopeKind.Inverter => s.Inverter != Guid.Empty && s.Inverter == Target,
            _ => s.Modules.Count > 0 && modules.TryGetValue(s.Modules[0], out var lugar) && blockByTable.GetValueOrDefault(lugar.Table) == Target,
        };
    }
}

/// <summary>
/// O resultado da numeração (15.4, 15.5), só das strings do alcance.
/// </summary>
/// <param name="Tags">A tag nova de cada string do alcance (vazia: a string fica sem tag). String fora do alcance não aparece: não muda.</param>
/// <param name="Tagged">Quantas strings ganharam tag.</param>
/// <param name="Free">Strings sem inversor: ficam sem tag (avisar quantas).</param>
/// <param name="UnknownInverter">Strings cujo inversor não está no cadastro: ficam sem tag.</param>
/// <param name="Unplaced">Strings alocadas cujo primeiro módulo não está no desenho: sem posição, ficam sem tag.</param>
/// <param name="InvertersWithoutTransformer">Inversores com string numerada e sem trafo (não agrupados em skid): a tag sai sem o pedaço do trafo.</param>
/// <param name="InvertersWithMissingTransformer">Inversores com string numerada cujo trafo não está mais no cadastro (erro de cadastro): a tag também sai sem o pedaço do trafo.</param>
/// <param name="DuplicateTags">
/// Tags que ficam repetidas no desenho depois de juntar as novas com as que
/// ficaram (regerar um pedaço depois de mudar blocos ou alocação): o aviso
/// para gerar tudo de novo. Vazia na usina inteira.
/// </param>
/// <param name="StaleOutside">Strings fora do alcance com uma tag diferente da que a usina inteira daria hoje (a ordem ou a alocação mudou): o aviso para gerar tudo.</param>
/// <param name="DuplicateStrings">Entidades de string com o mesmo GUID de outra (cópia da polilinha): ficam sem tag, para não sair a mesma tag duas vezes.</param>
/// <param name="OverCapacity">Inversores do alcance com mais strings que entradas no modelo (a tag sai, mas avisada).</param>
public sealed record StringNumberingResult(
    IReadOnlyDictionary<Guid, string> Tags,
    int Tagged,
    int Free,
    int UnknownInverter,
    int Unplaced,
    IReadOnlyList<Guid> InvertersWithoutTransformer,
    IReadOnlyList<Guid> InvertersWithMissingTransformer,
    IReadOnlyList<string> DuplicateTags,
    int StaleOutside,
    int DuplicateStrings,
    IReadOnlyList<InverterLoad> OverCapacity);

/// <summary>Um inversor e a carga dele: quantas strings tem e quantas entradas o modelo dá.</summary>
public sealed record InverterLoad(Guid Inverter, int Strings, int Capacity);

/// <summary>
/// A numeração das strings (15.4, 15.5): varre a usina na ordem dos blocos
/// (cada um no seu sentido, depois o resto no sentido da usina) e, dentro de
/// cada inversor, numera 1, 2, 3 nessa ordem (regra elétrica 8). O inversor
/// é o do vínculo (<see cref="ElectricalString.Inverter"/>), nunca o mais
/// perto. A conta é sempre da usina inteira; o alcance só escolhe quais
/// strings recebem a tag nova. Assim regerar um bloco ou um inversor dá o
/// mesmo número que gerar tudo, e o resto não é tocado.
/// </summary>
public static class StringNumbering
{
    public static StringNumberingResult Number(
        TagScheme scheme,
        ScanSetup setup,
        IReadOnlyList<Transformer> transformers,
        IReadOnlyList<Inverter> inverters,
        IReadOnlyList<ElectricalString> strings,
        IReadOnlyDictionary<Guid, ModuleSpot> modules,
        NumberingScope? scope = null,
        IReadOnlyList<InverterModel>? models = null)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(transformers);
        ArgumentNullException.ThrowIfNull(inverters);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(modules);
        if (scheme.Problem() is { } problema) throw new ArgumentException(problema, nameof(scheme));

        scope ??= NumberingScope.All;
        var donoDaMesa = setup.BlockByTable();

        // O número do trafo e o do inversor: a posição na lista do cadastro.
        var numeroDoTrafo = new Dictionary<Guid, int>();
        for (var i = 0; i < transformers.Count; i++) numeroDoTrafo.TryAdd(transformers[i].Id, i + 1);

        var numeroDoInversor = new Dictionary<Guid, int>();
        var inversorPorId = new Dictionary<Guid, Inverter>();
        for (var i = 0; i < inverters.Count; i++)
        {
            if (numeroDoInversor.TryAdd(inverters[i].Id, i + 1)) inversorPorId[inverters[i].Id] = inverters[i];
        }

        var tags = new Dictionary<Guid, string>();
        var livres = 0;
        var semInversor = 0;
        var semPosicao = 0;
        var aVarrer = new List<ScanItem>();
        var porId = new Dictionary<Guid, ElectricalString>();
        var noAlcance = new HashSet<Guid>();

        // GUID repetido (a polilinha copiada leva o XData junto): as cópias
        // ficam sem tag e contadas, senão a mesma tag sairia duas vezes.
        var repetidos = strings.GroupBy(s => s.Id).Where(g => g.Count() > 1).ToDictionary(g => g.Key, g => g.Count());
        var copias = 0;

        foreach (var s in strings)
        {
            if (!porId.TryAdd(s.Id, s)) continue;

            var dentro = scope.Contains(s, donoDaMesa, modules);

            if (repetidos.TryGetValue(s.Id, out var vezes))
            {
                if (dentro)
                {
                    noAlcance.Add(s.Id);
                    tags[s.Id] = string.Empty;
                    copias += vezes;
                }

                continue;
            }
            if (dentro)
            {
                noAlcance.Add(s.Id);
                tags[s.Id] = string.Empty;
            }

            if (!s.IsAllocated) livres += dentro ? 1 : 0;
            else if (!inversorPorId.ContainsKey(s.Inverter)) semInversor += dentro ? 1 : 0;
            else if (s.Modules.Count == 0 || !modules.TryGetValue(s.Modules[0], out var lugar)) semPosicao += dentro ? 1 : 0;
            else aVarrer.Add(new ScanItem(s.Id, lugar.X, lugar.Y, lugar.Table));
        }

        var sequencial = new Dictionary<Guid, int>();
        var semTrafo = new List<Guid>();
        var trafoSumido = new List<Guid>();
        var numeradas = 0;
        var desatualizadas = 0;

        foreach (var id in setup.Sequence(aVarrer))
        {
            var inversor = inversorPorId[porId[id].Inverter];
            var n = sequencial.GetValueOrDefault(inversor.Id) + 1;
            sequencial[inversor.Id] = n;

            int? trafo = numeroDoTrafo.TryGetValue(inversor.Transformer, out var t) ? t : null;
            var tag = scheme.Compose(trafo, numeroDoInversor[inversor.Id], n);

            if (!noAlcance.Contains(id))
            {
                // Fora do alcance não muda; só se avisa a tag que a usina inteira não daria mais.
                if (!string.IsNullOrEmpty(porId[id].Tag) && porId[id].Tag != tag) desatualizadas++;
                continue;
            }

            var lista = inversor.Transformer == Guid.Empty ? semTrafo : trafoSumido;
            if (trafo is null && !lista.Contains(inversor.Id)) lista.Add(inversor.Id);

            tags[id] = tag;
            numeradas++;
        }

        // Inversor do alcance acima da capacidade do modelo: a tag sai, avisada (regra elétrica 6).
        var capacidade = (models ?? []).GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First().TotalInputs);
        var acima = inverters
            .Where(i => noAlcance.Any(id => porId[id].Inverter == i.Id) && capacidade.TryGetValue(i.Model, out var c) && sequencial.GetValueOrDefault(i.Id) > c)
            .GroupBy(i => i.Id).Select(g => g.First())
            .Select(i => new InverterLoad(i.Id, sequencial[i.Id], capacidade[i.Model]))
            .ToList();

        // A tag de cada string depois de aplicar: a nova no alcance, a de antes fora.
        var repetidas = porId.Values
            .Select(s => tags.TryGetValue(s.Id, out var nova) ? nova : s.Tag ?? string.Empty)
            .Where(tag => tag.Length > 0)
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

        return new StringNumberingResult(tags, numeradas, livres, semInversor, semPosicao, semTrafo, trafoSumido, repetidas, desatualizadas, copias, acima);
    }

    /// <summary>Apagar as tags (15.5): a tag vazia para cada string do alcance. Só a tag; a geometria e o vínculo não mudam.</summary>
    public static IReadOnlyDictionary<Guid, string> Clear(
        IReadOnlyList<ElectricalString> strings,
        NumberingScope scope,
        ScanSetup setup,
        IReadOnlyDictionary<Guid, ModuleSpot> modules)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(setup);

        var donoDaMesa = setup.BlockByTable();
        var apagadas = new Dictionary<Guid, string>();
        foreach (var s in strings)
            if (scope.Contains(s, donoDaMesa, modules)) apagadas[s.Id] = string.Empty;

        return apagadas;
    }
}

/// <summary>
/// A identidade do texto da tag desenhado sobre a string (15.4): de qual
/// string ele é e o que escreve. É assim que apagar e refazer acham o texto
/// de cada string, sem olhar camada nem posição.
/// </summary>
public sealed record StringTagText(Guid String, string Tag)
{
    public const string Tipo = "StringTag";
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [String.ToString("D"), Tag ?? string.Empty];

    public static StringTagText? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Count >= FieldCount && Guid.TryParse(c[0], out var s) && s != Guid.Empty ? new StringTagText(s, c[1]) : null;
    }
}
