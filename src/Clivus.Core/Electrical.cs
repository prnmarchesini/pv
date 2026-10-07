using System.Globalization;

namespace Clivus.Core;

// O contrato da parte elétrica (plano/eletrica/04-modelo-de-dados.md e
// 05-contrato-interno.md). A cadeia UC -> trafo -> inversor -> string ->
// módulos é guardada em dado: cada elo guarda o GUID do elo de cima, nunca
// derivado de posição no desenho. Os campos de texto vão para o desenho
// (XData da string, registros do dicionário para o resto).

/// <summary>
/// Uma string desenhada (o traçado sobre os módulos). O vínculo com o
/// inversor mora só aqui (<see cref="Inverter"/>): uma string tem no máximo
/// um inversor por construção (regra elétrica 2). Desalocar troca só este
/// campo; a geometria fica (regra 3).
/// </summary>
public sealed record ElectricalString(Guid Id, Guid Type, IReadOnlyList<Guid> Modules, Guid Inverter, string Tag)
{
    public const string Tipo = "String";

    /// <summary>Os campos fixos antes da lista de módulos: GUID, tipo, inversor, tag, quantos módulos.</summary>
    public const int FixedFieldCount = 5;

    public bool IsAllocated => Inverter != Guid.Empty;

    public bool IsValid => Id != Guid.Empty && Modules.Count > 0 && Modules.All(m => m != Guid.Empty) && Modules.Distinct().Count() == Modules.Count;

    public IReadOnlyList<string> ToFields() =>
    [
        Id.ToString("D"),
        Type == Guid.Empty ? string.Empty : Type.ToString("D"),
        Inverter == Guid.Empty ? string.Empty : Inverter.ToString("D"),
        Tag ?? string.Empty,
        Modules.Count.ToString(CultureInfo.InvariantCulture),
        .. Modules.Select(m => m.ToString("D")),
    ];

    public static ElectricalString? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);

        if (c.Count < FixedFieldCount) return null;
        if (!Guid.TryParse(c[0], out var id)) return null;
        if (!OptionalGuid(c[1], out var tipo) || !OptionalGuid(c[2], out var inversor)) return null;
        if (!int.TryParse(c[4], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || c.Count != FixedFieldCount + n) return null;

        var modulos = new List<Guid>(n);
        for (var i = 0; i < n; i++)
        {
            if (!Guid.TryParse(c[FixedFieldCount + i], out var m)) return null;
            modulos.Add(m);
        }

        var s = new ElectricalString(id, tipo, modulos, inversor, c[3]);
        return s.IsValid ? s : null;
    }

    public bool Equals(ElectricalString? other) =>
        other is not null && Id == other.Id && Type == other.Type && Inverter == other.Inverter && Tag == other.Tag && Modules.SequenceEqual(other.Modules);

    public override int GetHashCode() => Id.GetHashCode();

    internal static bool OptionalGuid(string texto, out Guid valor)
    {
        valor = Guid.Empty;
        return texto.Length == 0 || Guid.TryParse(texto, out valor);
    }
}

/// <summary>
/// Apagar mesa leva a parte elétrica dela (Renan, 07/10/2026: "toda vez que
/// regerar as mesas é preciso apagar tudo o que é relacionado com a mesa,
/// strings, tags, etc"). O módulo redesenhado nasce com GUID novo, então a
/// string que apontava para um módulo que sumiu não tem mais onde estar.
/// </summary>
public static class StringsOfErasedTables
{
    /// <summary>
    /// As strings que morrem: alguma peça dela não é módulo vivo no desenho
    /// (apagado agora, ou já órfã de um apagar antigo), ou ela não tem módulo.
    /// </summary>
    public static IReadOnlySet<Guid> Doomed(IEnumerable<ElectricalString> strings, IReadOnlySet<Guid> livingModules)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(livingModules);

        return strings.Where(s => s.Modules.Count == 0 || s.Modules.Any(m => !livingModules.Contains(m))).Select(s => s.Id).ToHashSet();
    }
}

/// <summary>Dimensão física de um equipamento em campo, em metros (o retângulo 3D).</summary>
public sealed record EquipmentSize(double Width, double Length, double Height)
{
    public bool IsValid => Width > 0 && Length > 0 && Height > 0 && double.IsFinite(Width + Length + Height);

    internal IEnumerable<string> Fields() => [Num(Width), Num(Length), Num(Height)];

    internal static EquipmentSize? Parse(IReadOnlyList<string> c, int de) =>
        c.Count >= de + 3 && Real(c[de], out var w) && Real(c[de + 1], out var l) && Real(c[de + 2], out var h)
            ? new EquipmentSize(w, l, h) is { IsValid: true } d ? d : null
            : null;

    internal static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    internal static bool Real(string t, out double v) =>
        double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
}

/// <summary>
/// Um modelo de inversor (elétrica, 14.1): genérico do cliente ou cadastrado
/// (ex. Huawei 250). Os MPPTs com as entradas de cada um (5 MPPTs com 4, 4,
/// 4, 5 e 5 entradas: cada MPPT tem a sua quantidade); o total é a soma.
/// </summary>
/// <remarks>
/// Formato 2 (05/10/2026): o terceiro campo é quantos MPPTs e o quarto a
/// lista das entradas de cada um, separada por ";" ("4;4;4;5;5"). O formato
/// 1 (MPPT e entradas por MPPT, o mesmo número para todos) continua sendo
/// lido por <see cref="ParseLegacy"/>: vira a lista com o valor repetido.
/// A estrutura por MPPT é para crescer (balanceamento por MPPT); por ora só
/// o total vale para a capacidade.
/// <para>
/// Formato 3 (05/10/2026, pedido do Renan: "falta o campo para inserir a
/// potência"): o oitavo campo é a potência nominal CA em kW
/// (<see cref="PowerKw"/>), vazio quando não informada. Os formatos 1 e 2
/// (7 campos) são lidos com a potência 0.
/// </para>
/// </remarks>
public sealed record InverterModel(Guid Id, string Name, IReadOnlyList<int> InputsByMppt, EquipmentSize Size, double PowerKw = 0)
{
    public const int FieldCount = 8;

    /// <summary>Os campos dos formatos 1 e 2 (antes da potência).</summary>
    public const int LegacyFieldCount = 7;

    /// <summary>O separador da lista de entradas no campo gravado.</summary>
    public const char ListSeparator = ';';

    /// <summary>Todos os MPPTs com o mesmo número de entradas (o formato 1, e o atalho dos testes).</summary>
    public InverterModel(Guid id, string name, int mppts, int inputsPerMppt, EquipmentSize size, double powerKw = 0)
        : this(id, name, Uniform(mppts, inputsPerMppt), size, powerKw)
    {
    }

    /// <summary>A potência nominal CA foi informada (maior que zero).</summary>
    public bool HasPower => PowerKw > 0;

    /// <summary>A potência aceita: número finito de 0 (não informada) a <see cref="ElectricalDefaults.MaxInverterPowerKw"/>.</summary>
    public static bool IsValidPower(double kw) => double.IsFinite(kw) && kw >= 0 && kw <= ElectricalDefaults.MaxInverterPowerKw;

    public int Mppts => InputsByMppt.Count;

    /// <summary>A soma das entradas de todos os MPPTs: a capacidade do inversor em strings.</summary>
    public int TotalInputs => InputsByMppt.Sum();

    /// <summary>Todos os MPPTs com o mesmo número de entradas.</summary>
    public bool IsUniform => InputsByMppt.Distinct().Count() <= 1;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Mppts > 0 && InputsByMppt.All(n => n > 0) && Size.IsValid && IsValidPower(PowerKw);

    /// <summary>Formato 3: os 7 campos do 2 e a potência em kW (vazia quando 0).</summary>
    public IReadOnlyList<string> ToFields() =>
        [Id.ToString("D"), Name, Mppts.ToString(CultureInfo.InvariantCulture), FormatInputs(InputsByMppt), .. Size.Fields(),
         HasPower ? PowerKw.ToString("0.############", CultureInfo.InvariantCulture) : string.Empty];

    /// <summary>"4;4;4;5;5": a lista como vai para o campo gravado.</summary>
    public static string FormatInputs(IEnumerable<int> inputs) =>
        string.Join(ListSeparator, inputs.Select(n => n.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Lê a lista de entradas ("4;4;4;5;5", também com vírgula ou espaço).
    /// Null se algum pedaço não é número inteiro sem sinal.
    /// </summary>
    public static IReadOnlyList<int>? ParseInputs(string? text)
    {
        var pedacos = (text ?? string.Empty).Split([ListSeparator, ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lista = new List<int>(pedacos.Length);

        foreach (var p in pedacos)
        {
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return null;
            lista.Add(n);
        }

        return lista;
    }

    /// <summary>
    /// A lista com <paramref name="count"/> MPPTs: encolhe tirando do fim;
    /// cresce repetindo o último valor (<paramref name="whenEmpty"/> se a
    /// lista estava vazia). Genérica para a tela usar com o texto das caixas.
    /// </summary>
    public static IReadOnlyList<T> Resize<T>(IReadOnlyList<T> inputs, int count, T whenEmpty)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (count <= 0) return [];

        var ultimo = inputs.Count > 0 ? inputs[^1] : whenEmpty;
        return [.. inputs.Take(count), .. Enumerable.Repeat(ultimo, Math.Max(0, count - inputs.Count))];
    }

    /// <summary>
    /// Dentro dos limites que o cadastro aceita (<see cref="ElectricalDefaults.MaxMppts"/>,
    /// <see cref="ElectricalDefaults.MaxInputsPerMppt"/>): registro estragado com número
    /// enorme não volta do desenho (a soma estouraria).
    /// </summary>
    private bool WithinLimits => Mppts <= ElectricalDefaults.MaxMppts && InputsByMppt.All(n => n <= ElectricalDefaults.MaxInputsPerMppt);

    /// <summary>
    /// Formatos 3 e 2: GUID, nome, quantos MPPTs, a lista ("4;4;4;5;5"), a
    /// dimensão e (só no 3) a potência em kW, vazia = não informada. Com 7
    /// campos (o 2), a potência fica 0.
    /// </summary>
    public static InverterModel? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < LegacyFieldCount || !Guid.TryParse(c[0], out var id)) return null;
        if (!int.TryParse(c[2], NumberStyles.None, CultureInfo.InvariantCulture, out var mppt)) return null;
        if (ParseInputs(c[3]) is not { } entradas || entradas.Count != mppt) return null;
        if (EquipmentSize.Parse(c, 4) is not { } tamanho) return null;

        var potencia = 0.0;
        if (c.Count >= FieldCount && c[7].Length > 0
            && !double.TryParse(c[7], NumberStyles.Float, CultureInfo.InvariantCulture, out potencia)) return null;

        var m = new InverterModel(id, c[1], entradas, tamanho, potencia);
        return m.IsValid && m.WithinLimits ? m : null;
    }

    /// <summary>Formato 1 (antes de 05/10/2026): MPPT e entradas por MPPT; vira a lista com o mesmo valor repetido.</summary>
    public static InverterModel? ParseLegacy(IReadOnlyList<string> c)
    {
        if (c.Count < LegacyFieldCount || !Guid.TryParse(c[0], out var id)) return null;
        if (!int.TryParse(c[2], NumberStyles.None, CultureInfo.InvariantCulture, out var mppt)) return null;
        if (!int.TryParse(c[3], NumberStyles.None, CultureInfo.InvariantCulture, out var entradas)) return null;
        if (mppt > ElectricalDefaults.MaxMppts || EquipmentSize.Parse(c, 4) is not { } tamanho) return null;

        var m = new InverterModel(id, c[1], mppt, entradas, tamanho);
        return m.IsValid && m.WithinLimits ? m : null;
    }

    public bool Equals(InverterModel? other) =>
        other is not null && Id == other.Id && Name == other.Name && Size == other.Size && PowerKw.Equals(other.PowerKw) && InputsByMppt.SequenceEqual(other.InputsByMppt);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Size, Mppts);

    private static int[] Uniform(int mppts, int inputsPerMppt) => mppts > 0 ? Enumerable.Repeat(inputsPerMppt, mppts).ToArray() : [];
}

/// <summary>
/// Um inversor da usina (elétrica, 14.2): instância de um modelo, com nome
/// (a tag, ex. "Inversor 1"), o trafo do skid (14.7), vazio se não agrupado,
/// e a cor dele (as strings alocadas são pintadas com ela; só representação,
/// o vínculo continua na string). As strings dele são as que apontam para ele
/// (<see cref="ElectricalString.Inverter"/>).
/// </summary>
/// <remarks>
/// Formato 2 (05/10/2026): 5 campos, o último é a cor ("#RRGGBB"). O formato
/// 1 (4 campos, sem cor) continua sendo lido: a cor fica null e o cadastro
/// (<see cref="ElectricalSetup"/>) dá uma automática na leitura.
/// </remarks>
public sealed record Inverter(Guid Id, Guid Model, string Name, Guid Transformer, RgbColor? Color = null)
{
    public const int FieldCount = 5;

    /// <summary>Os campos do formato 1 (antes da cor).</summary>
    public const int LegacyFieldCount = 4;

    public bool IsValid => Id != Guid.Empty && Model != Guid.Empty && !string.IsNullOrWhiteSpace(Name);

    public IReadOnlyList<string> ToFields() =>
        [Id.ToString("D"), Model.ToString("D"), Name, Transformer == Guid.Empty ? string.Empty : Transformer.ToString("D"), Color?.ToHex() ?? string.Empty];

    /// <summary>Lê o formato 2 (5 campos, com a cor) ou o 1 (4 campos, cor null).</summary>
    public static Inverter? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < LegacyFieldCount || !Guid.TryParse(c[0], out var id) || !Guid.TryParse(c[1], out var modelo)) return null;
        if (!ElectricalString.OptionalGuid(c[3], out var trafo)) return null;

        RgbColor? cor = null;
        if (c.Count >= FieldCount && c[4].Length > 0)
        {
            if (!RgbColor.TryParseHex(c[4], out var lida)) return null;
            cor = lida;
        }

        var i = new Inverter(id, modelo, c[2], trafo, cor);
        return i.IsValid ? i : null;
    }
}

/// <summary>
/// Um transformador (elétrica, 13.1): cadastro livre. O apelido é a tag
/// (T1, T2). A UC dele (12.1) fica em <see cref="ConsumerUnit"/>, vazia se
/// ainda não associado. Tensões em volts, potência em kVA, impedância em %.
/// </summary>
public sealed record Transformer(
    Guid Id,
    string Name,
    string Nickname,
    double InputVoltage,
    double OutputVoltage,
    double PowerKva,
    double KFactor,
    double ImpedancePercent,
    string Notes,
    EquipmentSize Size,
    Guid ConsumerUnit)
{
    public const int FieldCount = 13;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Nickname) && Size.IsValid
        && InputVoltage >= 0 && OutputVoltage >= 0 && PowerKva >= 0 && KFactor >= 0 && ImpedancePercent >= 0;

    public IReadOnlyList<string> ToFields() =>
    [
        Id.ToString("D"), Name, Nickname,
        EquipmentSize.Num(InputVoltage), EquipmentSize.Num(OutputVoltage), EquipmentSize.Num(PowerKva),
        EquipmentSize.Num(KFactor), EquipmentSize.Num(ImpedancePercent), Notes ?? string.Empty,
        .. Size.Fields(),
        ConsumerUnit == Guid.Empty ? string.Empty : ConsumerUnit.ToString("D"),
    ];

    public static Transformer? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Guid.TryParse(c[0], out var id)) return null;

        var numeros = new double[5];
        for (var i = 0; i < 5; i++)
            if (!EquipmentSize.Real(c[3 + i], out numeros[i])) return null;

        if (EquipmentSize.Parse(c, 9) is not { } tamanho) return null;
        if (!ElectricalString.OptionalGuid(c[12], out var uc)) return null;

        var t = new Transformer(id, c[1], c[2], numeros[0], numeros[1], numeros[2], numeros[3], numeros[4], c[8], tamanho, uc);
        return t.IsValid ? t : null;
    }
}

/// <summary>O modo da subestação (12.1, 12.2).</summary>
public enum ConsumerUnitMode
{
    /// <summary>Compartilhada: UC1, UC2... cada uma com um ou mais trafos.</summary>
    Shared,

    /// <summary>Unitária: um bloquinho independente com o seu trafo.</summary>
    Unitary,
}

/// <summary>
/// Uma unidade consumidora (elétrica, 12.1–12.3): código (UC1, U1), nome e
/// modo. A unitária é ao mesmo tempo a UC e o bloquinho físico (tem a
/// dimensão e vai para o campo). A compartilhada é uma medição dentro do
/// bloco físico <see cref="Substation"/> (o GUID do <see cref="Clivus.Core.Substation"/>);
/// a dimensão dela fica gravada mas não vale nada (quem vai para o campo é o
/// bloco). Os trafos dela são os que apontam para ela
/// (<see cref="Transformer.ConsumerUnit"/>).
/// </summary>
/// <remarks>
/// Formato 2 (05/10/2026): 8 campos, o último é o bloco. O formato 1 (7
/// campos, sem bloco) continua sendo lido: a compartilhada sem bloco é posta
/// num bloco na leitura (<see cref="ElectricalSetup"/>).
/// </remarks>
public sealed record ConsumerUnit(Guid Id, string Code, string Name, ConsumerUnitMode Mode, EquipmentSize Size, Guid Substation = default)
{
    public const int FieldCount = 8;

    /// <summary>Os campos do formato 1 (antes do bloco físico).</summary>
    public const int LegacyFieldCount = 7;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Code) && Size.IsValid;

    public IReadOnlyList<string> ToFields() =>
    [
        Id.ToString("D"), Code, Name ?? string.Empty, Mode == ConsumerUnitMode.Shared ? "C" : "U", .. Size.Fields(),
        Mode == ConsumerUnitMode.Shared && Substation != Guid.Empty ? Substation.ToString("D") : string.Empty,
    ];

    /// <summary>Lê o formato 2 (8 campos) ou o 1 (7 campos, sem bloco). A unitária nunca tem bloco.</summary>
    public static ConsumerUnit? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < LegacyFieldCount || !Guid.TryParse(c[0], out var id)) return null;

        ConsumerUnitMode? modo = c[3] switch { "C" => ConsumerUnitMode.Shared, "U" => ConsumerUnitMode.Unitary, _ => null };
        if (modo is null || EquipmentSize.Parse(c, 4) is not { } tamanho) return null;

        var bloco = Guid.Empty;
        if (c.Count >= FieldCount && !ElectricalString.OptionalGuid(c[7], out bloco)) return null;
        if (modo == ConsumerUnitMode.Unitary) bloco = Guid.Empty;

        var (codigo, nome) = modo == ConsumerUnitMode.Shared ? CodigoAtual(c[1], c[2]) : (c[1], c[2]);
        var u = new ConsumerUnit(id, codigo, nome, modo.Value, tamanho, bloco);
        return u.IsValid ? u : null;
    }

    /// <summary>
    /// A UC compartilhada é UC1, UC2... (Renan, 07/10/2026: "é UC1 UC2, de
    /// unidade consumidora"). Desenho de antes guardou C1, C2: ao ler, o código
    /// vira UCn, e o nome padrão ("Subestação C1") acompanha; nome dado pelo
    /// usuário fica. Gravado assim no próximo salvar do cadastro.
    /// </summary>
    internal static (string Codigo, string Nome) CodigoAtual(string codigo, string nome)
    {
        if (codigo.Length < 2 || codigo[0] != 'C' || !codigo.AsSpan(1).ToString().All(char.IsAsciiDigit)) return (codigo, nome);

        var novo = "U" + codigo;
        return (novo, nome == Tr.F("Subestação {0}", codigo) ? Tr.F("Subestação {0}", novo) : nome);
    }
}

/// <summary>
/// O bloco físico da subestação compartilhada (elétrica, 12.1 e 12.3): um
/// cubículo só em campo, com nome e dimensão, que abriga várias UCs (UC1,
/// UC2...; cada uma aponta para ele em <see cref="ConsumerUnit.Substation"/>).
/// A unitária não usa este registro: ela é o próprio bloco.
/// </summary>
public sealed record Substation(Guid Id, string Name, EquipmentSize Size)
{
    public const int FieldCount = 5;

    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Size.IsValid;

    public IReadOnlyList<string> ToFields() => [Id.ToString("D"), Name, .. Size.Fields()];

    public static Substation? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var id) && EquipmentSize.Parse(c, 2) is { } tamanho
            && new Substation(id, c[1], tamanho) is { IsValid: true } s
            ? s
            : null;
}

/// <summary>O tipo de equipamento desenhado em campo (o retângulo com a tag).</summary>
public enum EquipmentKind
{
    /// <summary>A subestação física: a unitária (o GUID da UC) ou o bloco compartilhado (o GUID do <see cref="Substation"/>).</summary>
    ConsumerUnit,
    Transformer,
    Inverter,
}

/// <summary>
/// A identidade do retângulo de um equipamento em campo (12.3, 13.2, 14.6):
/// o tipo e o GUID do cadastro. É representação: mover o retângulo não muda
/// vínculo nenhum (regra elétrica 1).
/// </summary>
public sealed record EquipmentPlacement(EquipmentKind Kind, Guid Equipment)
{
    public const string Tipo = "Equipamento";
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [Kind.ToString(), Equipment.ToString("D")];

    public static EquipmentPlacement? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Enum.TryParse<EquipmentKind>(c[0], out var k) && Enum.IsDefined(k) && Guid.TryParse(c[1], out var g) && g != Guid.Empty
            ? new EquipmentPlacement(k, g)
            : null;
}
