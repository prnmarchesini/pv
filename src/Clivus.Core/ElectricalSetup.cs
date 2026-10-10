using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// Um trafo padrão para partir (elétrica, 13.1): o cadastro livre é o normal,
/// mas a tabela poupa digitação nos casos comuns. A entrada é a baixa tensão
/// (o lado dos inversores) e a saída a média tensão, como no contrato.
/// </summary>
public sealed record TransformerTemplate(double InputVoltage, double OutputVoltage, double PowerKva, double KFactor, double ImpedancePercent, EquipmentSize Size)
{
    /// <summary>"2.500 kVA — 800 V / 13.800 V — Z 6,0 %", no idioma da tela.</summary>
    public string Describe() =>
        Tr.F("{0:#,0} kVA — {1:#,0} V / {2:#,0} V — Z {3:0.0} %", PowerKva, InputVoltage, OutputVoltage, ImpedancePercent);
}

/// <summary>Medidas e tabelas de partida da configuração elétrica.</summary>
public static class ElectricalDefaults
{
    /// <summary>Maior nome aceito (o mesmo limite das outras perguntas do plugin).</summary>
    public const int MaxNameLength = 100;

    /// <summary>Quantos equipamentos (subestações unitárias, inversores) se cria de uma vez, no máximo.</summary>
    public const int MaxAtOnce = 500;

    /// <summary>Limites do modelo de inversor (14.1): folgados, só barram número sem sentido.</summary>
    public const int MaxMppts = 100;

    public const int MaxInputsPerMppt = 100;

    /// <summary>A maior potência nominal CA de um modelo de inversor, em kW (folgada: barra só número sem sentido).</summary>
    public const double MaxInverterPowerKw = 100_000;

    public static EquipmentSize TransformerSize { get; } = new(3.0, 2.5, 2.5);

    public static EquipmentSize ConsumerUnitSize { get; } = new(4.0, 3.0, 3.0);

    public static EquipmentSize InverterSize { get; } = new(1.1, 0.7, 0.6);

    /// <summary>A combiner box de partida (roteamento, 19.1): uma caixa de parede no poste.</summary>
    public static EquipmentSize CombinerSize { get; } = new(0.8, 0.3, 0.8);

    /// <summary>
    /// Os trafos padrão (13.1): elevadores de usina, 800 V dos inversores para
    /// 13,8 kV ou 34,5 kV, mais um pequeno de 380 V. Impedância típica da
    /// faixa; o projetista confere e muda no cadastro.
    /// </summary>
    public static IReadOnlyList<TransformerTemplate> TransformerTemplates { get; } =
    [
        new(800, 13800, 1250, 1, 5.0, new EquipmentSize(2.4, 2.0, 2.2)),
        new(800, 13800, 2500, 1, 6.0, new EquipmentSize(3.0, 2.5, 2.5)),
        new(800, 13800, 3150, 1, 6.5, new EquipmentSize(3.2, 2.6, 2.6)),
        new(800, 34500, 2500, 1, 6.5, new EquipmentSize(3.2, 2.6, 2.7)),
        new(800, 34500, 3150, 1, 7.0, new EquipmentSize(3.4, 2.8, 2.8)),
        new(380, 13800, 500, 1, 4.5, new EquipmentSize(1.8, 1.4, 1.8)),
    ];
}

/// <summary>
/// O skid (14.7): um trafo e os inversores agrupados nele, com nome. O
/// vínculo de cada inversor mora no inversor (<see cref="Inverter.Transformer"/>);
/// este registro guarda só o nome do grupo, um por trafo.
/// </summary>
public sealed record Skid(Guid Transformer, string Name)
{
    public const int FieldCount = 2;

    public bool IsValid => Transformer != Guid.Empty && !string.IsNullOrWhiteSpace(Name);

    public IReadOnlyList<string> ToFields() => [Transformer.ToString("D"), Name];

    public static Skid? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var t) && new Skid(t, c[1]) is { IsValid: true } s ? s : null;
}

/// <summary>
/// O que agrupar inversores num trafo deu (14.7): quantos entraram, quantos
/// já eram dele, os recusados (de outro skid: travados até serem tirados) e
/// quantos não estão mais no cadastro. <see cref="Problem"/> não nulo: nada
/// foi feito.
/// </summary>
public sealed record SkidResult(int Added, int AlreadyHere, IReadOnlyList<Inverter> Refused, int Missing, string? Problem);

/// <summary>
/// O que pôr inversores num trafo pela tabela deu (05/10/2026): quantos
/// mudaram, quantos já eram dele e quantos não estão mais no cadastro.
/// <see cref="Problem"/> não nulo: nada foi feito.
/// </summary>
public sealed record TransformerAssignment(int Changed, int AlreadyThere, int Missing, string? Problem);

/// <summary>
/// Uma opção da UC no formulário do trafo (13.1): a UC e se o trafo pode ir
/// para ela agora (a mesma trava da aba Subestação), com o porquê se não.
/// </summary>
public sealed record UnitChoice(ConsumerUnit Unit, bool Allowed, string? Reason);

/// <summary>
/// Um equipamento que vai para o campo como retângulo (12.3, 13.2, 14.6): o
/// tipo, o GUID do cadastro, a tag escrita no topo e a dimensão.
/// </summary>
public sealed record EquipmentInfo(EquipmentKind Kind, Guid Id, string Tag, EquipmentSize Size);

/// <summary>
/// O cadastro da configuração elétrica de um desenho (etapas 12 a 14):
/// subestações, trafos, modelos de inversor e inversores, com as regras de
/// nome, vínculo e trava. Lê as listas gravadas, muda em memória e devolve as
/// listas para gravar. O vínculo é dado (cada elo guarda o GUID do de cima),
/// nunca posição no desenho.
/// </summary>
public sealed class ElectricalSetup
{
    private readonly List<ConsumerUnit> _ucs;
    private readonly List<Transformer> _trafos;
    private readonly List<Inverter> _inversores;
    private readonly List<InverterModel> _modelos;
    private readonly List<Skid> _skids;
    private readonly List<Substation> _blocos;
    private readonly List<Combiner> _combiners;

    /// <summary>
    /// O cadastro lido. A compartilhada sem bloco (formato 1, antes de
    /// 05/10/2026) ou com bloco que sumiu cai no bloco que existe, ou num
    /// "Subestação compartilhada" criado aqui com GUID derivado da primeira
    /// delas (ler de novo antes de gravar dá o mesmo bloco). Quantas foram
    /// postas assim fica em <see cref="MigratedUnits"/>; a próxima gravação
    /// leva o bloco para o desenho.
    /// </summary>
    public ElectricalSetup(
        IEnumerable<Transformer>? transformers = null,
        IEnumerable<Inverter>? inverters = null,
        IEnumerable<ConsumerUnit>? units = null,
        IEnumerable<InverterModel>? models = null,
        IEnumerable<Skid>? skids = null,
        IEnumerable<Substation>? substations = null,
        IEnumerable<Combiner>? combiners = null)
    {
        _combiners = combiners?.ToList() ?? [];
        _trafos = transformers?.ToList() ?? [];
        _inversores = inverters?.ToList() ?? [];
        _ucs = units?.ToList() ?? [];
        _modelos = models?.ToList() ?? [];
        _skids = skids?.ToList() ?? [];
        _blocos = substations?.ToList() ?? [];

        for (var i = 0; i < _ucs.Count; i++)
        {
            var u = _ucs[i];
            if (u.Mode == ConsumerUnitMode.Unitary && u.Substation != Guid.Empty) _ucs[i] = u with { Substation = Guid.Empty };
            if (u.Mode != ConsumerUnitMode.Shared || FindSubstation(u.Substation) is not null) continue;

            var bloco = _blocos.FirstOrDefault();
            if (bloco is null)
            {
                bloco = new Substation(LegacySubstationId(u.Id), Tr.T("Subestação compartilhada"), u.Size);
                _blocos.Add(bloco);
            }

            _ucs[i] = u with { Substation = bloco.Id };
            MigratedUnits++;
        }

        // Inversor sem cor (formato 1, antes de 05/10/2026) ganha uma da
        // paleta, na ordem da lista: ler de novo antes de gravar dá a mesma.
        for (var i = 0; i < _inversores.Count; i++)
        {
            if (_inversores[i].Color is not null) continue;
            _inversores[i] = _inversores[i] with { Color = InverterColors.Next(_inversores.Select(x => x.Color).OfType<RgbColor>()) };
            ColoredInverters++;
        }
    }

    /// <summary>Quantos inversores sem cor (desenho antigo) ganharam a cor automática na leitura.</summary>
    public int ColoredInverters { get; }

    /// <summary>Quantas UCs compartilhadas sem bloco (desenho antigo) foram postas num bloco na leitura.</summary>
    public int MigratedUnits { get; }

    /// <summary>O GUID do bloco criado na leitura de um desenho antigo: o mesmo a cada leitura.</summary>
    private static Guid LegacySubstationId(Guid primeiraUc)
    {
        var bytes = primeiraUc.ToByteArray();
        var marca = "CLIVUS-SUBESTACAO"u8;
        for (var i = 0; i < bytes.Length; i++) bytes[i] ^= marca[i % marca.Length];
        return new Guid(bytes);
    }

    /// <summary>Os blocos físicos da subestação compartilhada (na prática, um por usina).</summary>
    public IReadOnlyList<Substation> Substations => _blocos;

    public Substation? FindSubstation(Guid id) => id == Guid.Empty ? null : _blocos.FirstOrDefault(b => b.Id == id);

    /// <summary>As UCs compartilhadas dentro do bloco (o vínculo mora na UC).</summary>
    public IReadOnlyList<ConsumerUnit> UnitsOf(Guid substation) =>
        _ucs.Where(u => u.Mode == ConsumerUnitMode.Shared && u.Substation == substation).ToList();

    public IReadOnlyList<Skid> Skids => _skids;

    public Skid? FindSkid(Guid transformer) => _skids.FirstOrDefault(s => s.Transformer == transformer);

    public IReadOnlyList<InverterModel> Models => _modelos;

    public InverterModel? FindModel(Guid id) => _modelos.FirstOrDefault(m => m.Id == id);

    public IReadOnlyList<ConsumerUnit> Units => _ucs;

    public IReadOnlyList<Transformer> Transformers => _trafos;

    public IReadOnlyList<Inverter> Inverters => _inversores;

    public Transformer? FindTransformer(Guid id) => _trafos.FirstOrDefault(t => t.Id == id);

    public IReadOnlyList<Combiner> Combiners => _combiners;

    public Combiner? FindCombiner(Guid id) => _combiners.FirstOrDefault(c => c.Id == id);

    public ConsumerUnit? FindUnit(Guid id) => _ucs.FirstOrDefault(u => u.Id == id);

    // ------------------------------------------------- equipamento em campo

    /// <summary>
    /// O equipamento do cadastro com a tag e a dimensão do retângulo: a
    /// subestação mostra o nome (o código se o nome está vazio), o trafo o
    /// apelido, o inversor o nome (a dimensão é a do modelo dele). Null se
    /// não está no cadastro (ou o inversor está sem modelo).
    /// </summary>
    public EquipmentInfo? FindEquipment(EquipmentKind kind, Guid id) => kind switch
    {
        // A subestação física: a unitária (UC e bloco ao mesmo tempo) ou o
        // bloco da compartilhada. A UC compartilhada não vai para o campo.
        EquipmentKind.ConsumerUnit when FindUnit(id) is { Mode: ConsumerUnitMode.Unitary } u =>
            new EquipmentInfo(kind, id, string.IsNullOrWhiteSpace(u.Name) ? u.Code : u.Name, u.Size),
        EquipmentKind.ConsumerUnit when FindSubstation(id) is { } b => new EquipmentInfo(kind, id, b.Name, b.Size),
        EquipmentKind.Transformer when FindTransformer(id) is { } t => new EquipmentInfo(kind, id, t.Nickname, t.Size),
        EquipmentKind.Inverter when FindInverter(id) is { } i && FindModel(i.Model) is { } m => new EquipmentInfo(kind, id, i.Name, m.Size),
        EquipmentKind.Combiner when FindCombiner(id) is { } c => new EquipmentInfo(kind, id, c.Name, c.Size),
        _ => null,
    };

    /// <summary>
    /// Os equipamentos que respondem pelo texto: o GUID, ou a tag (código ou
    /// nome da subestação, apelido do trafo, nome do inversor), sem olhar maiúscula. Mais de um
    /// = ambíguo, quem chama recusa.
    /// </summary>
    public IReadOnlyList<EquipmentInfo> FindEquipment(string? tagOrId)
    {
        var texto = tagOrId?.Trim() ?? string.Empty;
        if (texto.Length == 0) return [];

        var todos = Equipment().ToList();
        if (Guid.TryParse(texto, out var id)) return todos.Where(e => e.Id == id).ToList();

        // A UC compartilhada responde pelo bloco dela (código ou nome): "C1" põe o cubículo em campo.
        bool DaSubestacao(EquipmentInfo e) =>
            e.Kind == EquipmentKind.ConsumerUnit
            && (FindUnit(e.Id) is { } u ? SameName(u.Code, texto)
                : UnitsOf(e.Id).Any(u => SameName(u.Code, texto) || SameName(u.Name, texto)));

        return todos.Where(e => SameName(e.Tag, texto) || DaSubestacao(e)).ToList();
    }

    /// <summary>
    /// Todos os equipamentos do cadastro que vão para o campo: subestações
    /// unitárias, blocos compartilhados, trafos e inversores (com modelo),
    /// nessa ordem.
    /// </summary>
    public IEnumerable<EquipmentInfo> Equipment() =>
        _ucs.Select(u => FindEquipment(EquipmentKind.ConsumerUnit, u.Id))
            .Concat(_blocos.Select(b => FindEquipment(EquipmentKind.ConsumerUnit, b.Id)))
            .Concat(_trafos.Select(t => FindEquipment(EquipmentKind.Transformer, t.Id)))
            .Concat(_inversores.Select(i => FindEquipment(EquipmentKind.Inverter, i.Id)))
            .Concat(_combiners.Select(c => FindEquipment(EquipmentKind.Combiner, c.Id)))
            .OfType<EquipmentInfo>();

    // -------------------------------------------------------- subestações

    /// <summary>
    /// O bloco da subestação compartilhada: o que existe, ou um novo
    /// ("Subestação compartilhada", tamanho padrão). A usina tem um só
    /// (12.1): pedir de novo devolve o mesmo.
    /// </summary>
    public (Substation Block, bool Created) EnsureSharedSubstation()
    {
        if (_blocos.FirstOrDefault() is { } existe) return (existe, false);

        var bloco = new Substation(Guid.NewGuid(), Tr.T("Subestação compartilhada"), ElectricalDefaults.ConsumerUnitSize);
        _blocos.Add(bloco);
        return (bloco, true);
    }

    /// <summary>
    /// Cria a próxima UC compartilhada (C1, C2...; o número segue o maior
    /// código, sem reaproveitar) dentro do bloco da usina (criado se não
    /// existe), com nome padrão.
    /// </summary>
    public ConsumerUnit AddSharedUnit() => AddSharedUnit(EnsureSharedSubstation().Block.Id);

    /// <summary>Cria a próxima UC compartilhada dentro do bloco dado.</summary>
    public ConsumerUnit AddSharedUnit(Guid substation)
    {
        if (FindSubstation(substation) is null) throw new InvalidOperationException(Tr.T("essa subestação não está mais no cadastro"));

        var codigo = "C" + NextNumber(_ucs.Select(u => u.Code), "C").ToString(CultureInfo.InvariantCulture);
        var uc = new ConsumerUnit(Guid.NewGuid(), codigo, Tr.F("Subestação {0}", codigo), ConsumerUnitMode.Shared, ElectricalDefaults.ConsumerUnitSize, substation);
        _ucs.Add(uc);
        return uc;
    }

    /// <summary>Troca nome e tamanho do bloco compartilhado. Null se deu certo, o porquê se não.</summary>
    public string? EditSubstation(Guid id, string? name, EquipmentSize size)
    {
        ArgumentNullException.ThrowIfNull(size);

        var posicao = _blocos.FindIndex(b => b.Id == id);
        if (posicao < 0) return Tr.T("essa subestação não está mais no cadastro");

        var nome = name?.Trim() ?? string.Empty;
        if (nome.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (nome.Length > ElectricalDefaults.MaxNameLength) return Tr.F("o nome tem no máximo {0} caracteres", ElectricalDefaults.MaxNameLength);
        if (!size.IsValid) return Tr.T("largura, comprimento e altura têm que ser maiores que zero");

        _blocos[posicao] = _blocos[posicao] with { Name = nome, Size = size };
        return null;
    }

    /// <summary>
    /// Tira o bloco compartilhado e as UCs dele do cadastro. Os trafos delas
    /// ficam sem UC (o vínculo some, o trafo fica). Quantas UCs saíram e
    /// quantos trafos foram soltos, ou null se o bloco não existia.
    /// </summary>
    public (int Units, int Transformers)? RemoveSubstation(Guid id)
    {
        if (_blocos.RemoveAll(b => b.Id == id) == 0) return null;

        var ucs = _ucs.Where(u => u.Mode == ConsumerUnitMode.Shared && u.Substation == id).Select(u => u.Id).ToList();
        var soltos = ucs.Sum(u => RemoveUnit(u) ?? 0);
        return (ucs.Count, soltos);
    }

    /// <summary>
    /// Cria <paramref name="count"/> subestações unitárias (12.2): bloquinhos
    /// independentes U1, U2..., cada um com no máximo um trafo.
    /// </summary>
    public IReadOnlyList<ConsumerUnit> AddUnitaryUnits(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, ElectricalDefaults.MaxAtOnce);

        var primeiro = NextNumber(_ucs.Select(u => u.Code), "U");
        var novas = new List<ConsumerUnit>(count);

        for (var i = 0; i < count; i++)
        {
            var codigo = "U" + (primeiro + i).ToString(CultureInfo.InvariantCulture);
            novas.Add(new ConsumerUnit(Guid.NewGuid(), codigo, Tr.F("Subestação {0}", codigo), ConsumerUnitMode.Unitary, ElectricalDefaults.ConsumerUnitSize));
        }

        _ucs.AddRange(novas);
        return novas;
    }

    /// <summary>Troca nome e tamanho do bloquinho (código e modo não mudam). Null se deu certo, o porquê se não.</summary>
    public string? EditUnit(Guid id, string? name, EquipmentSize size)
    {
        ArgumentNullException.ThrowIfNull(size);

        var posicao = _ucs.FindIndex(u => u.Id == id);
        if (posicao < 0) return Tr.T("essa subestação não está mais no cadastro");

        var nome = name?.Trim() ?? string.Empty;
        if (nome.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (nome.Length > ElectricalDefaults.MaxNameLength) return Tr.F("o nome tem no máximo {0} caracteres", ElectricalDefaults.MaxNameLength);
        if (!size.IsValid) return Tr.T("largura, comprimento e altura têm que ser maiores que zero");

        _ucs[posicao] = _ucs[posicao] with { Name = nome, Size = size };
        return null;
    }

    /// <summary>
    /// Tira a subestação do cadastro. Os trafos dela ficam sem UC (o vínculo
    /// some, o trafo fica). Quantos trafos foram soltos, ou null se não existia.
    /// </summary>
    public int? RemoveUnit(Guid id)
    {
        if (_ucs.RemoveAll(u => u.Id == id) == 0) return null;

        var soltos = 0;
        for (var i = 0; i < _trafos.Count; i++)
        {
            if (_trafos[i].ConsumerUnit != id) continue;
            _trafos[i] = _trafos[i] with { ConsumerUnit = Guid.Empty };
            soltos++;
        }

        return soltos;
    }

    /// <summary>Os trafos ligados à subestação (o vínculo mora no trafo).</summary>
    public IReadOnlyList<Transformer> TransformersOf(Guid unit) => _trafos.Where(t => t.ConsumerUnit == unit).ToList();

    /// <summary>
    /// Associa o trafo à subestação (12.1). Trafo que já é de outra fica
    /// travado: soltar primeiro é um ato explícito, nada muda sozinho. Null se
    /// deu certo (ou já era dela), o porquê se não.
    /// </summary>
    public string? LinkTransformer(Guid unit, Guid transformer)
    {
        var posicao = _trafos.FindIndex(t => t.Id == transformer);
        if (FindUnit(unit) is null) return Tr.T("essa subestação não está mais no cadastro");
        if (posicao < 0) return Tr.T("esse transformador não está mais no cadastro");

        if (LinkProblem(_trafos[posicao], unit) is { } porque) return porque;

        _trafos[posicao] = _trafos[posicao] with { ConsumerUnit = unit };
        return null;
    }

    /// <summary>
    /// Por que o trafo não pode ir para a UC agora (null: pode, ou já é
    /// dela; vazio = soltar, sempre pode). A trava é a mesma em todo lugar:
    /// trafo de outra UC fica travado até ser solto; a unitária tem um trafo só.
    /// </summary>
    private string? LinkProblem(Transformer trafo, Guid unit)
    {
        if (unit == Guid.Empty || trafo.ConsumerUnit == unit) return null;
        if (FindUnit(unit) is not { } uc) return Tr.T("essa subestação não está mais no cadastro");

        // Vínculo para uma UC que não existe mais (registro estragado) não trava.
        if (FindUnit(trafo.ConsumerUnit) is { } dona)
            return Tr.F("{0} já está ligado a {1}; solte antes de ligar a outra subestação", trafo.Nickname, dona.Code);

        // A unitária é um bloquinho com o seu trafo: um só (12.2).
        if (uc.Mode == ConsumerUnitMode.Unitary && TransformersOf(unit).FirstOrDefault(t => t.Id != trafo.Id) is { } outro)
            return Tr.F("{0} é unitária e já tem o trafo {1}; solte-o antes", uc.Code, outro.Nickname);

        return null;
    }

    /// <summary>
    /// As UCs que o formulário do trafo oferece (13.1), cada uma com se o
    /// trafo pode ir para ela agora. A UC dele mesmo é sempre permitida.
    /// </summary>
    public IReadOnlyList<UnitChoice> UnitChoices(Guid transformer)
    {
        var trafo = FindTransformer(transformer);
        return _ucs.Select(u => trafo is null
                ? new UnitChoice(u, false, Tr.T("esse transformador não está mais no cadastro"))
                : LinkProblem(trafo, u.Id) is { } porque ? new UnitChoice(u, false, porque) : new UnitChoice(u, true, null))
            .ToList();
    }

    /// <summary>Solta o trafo da subestação dele; se havia o que soltar.</summary>
    public bool UnlinkTransformer(Guid transformer)
    {
        var posicao = _trafos.FindIndex(t => t.Id == transformer);
        if (posicao < 0 || _trafos[posicao].ConsumerUnit == Guid.Empty) return false;

        _trafos[posicao] = _trafos[posicao] with { ConsumerUnit = Guid.Empty };
        return true;
    }

    // ------------------------------------------------------------- trafos

    /// <summary>
    /// Cria o próximo trafo (Trafo 1, apelido T1; o número segue o maior
    /// apelido do padrão, sem reaproveitar). Sem padrão, os campos elétricos
    /// ficam zerados para o projetista preencher; com padrão, vêm dele.
    /// </summary>
    public Transformer AddTransformer(TransformerTemplate? template = null)
    {
        var n = NextNumber(_trafos.Select(t => t.Nickname), "T");

        var trafo = template is null
            ? new Transformer(Guid.NewGuid(), Tr.F("Trafo {0}", n), "T" + n.ToString(CultureInfo.InvariantCulture), 0, 0, 0, 0, 0, string.Empty, ElectricalDefaults.TransformerSize, Guid.Empty)
            : new Transformer(Guid.NewGuid(), Tr.F("Trafo {0}", n), "T" + n.ToString(CultureInfo.InvariantCulture),
                template.InputVoltage, template.OutputVoltage, template.PowerKva, template.KFactor, template.ImpedancePercent, string.Empty, template.Size, Guid.Empty);

        _trafos.Add(trafo);
        return trafo;
    }

    /// <summary>
    /// Troca os campos do cadastro (nome, apelido, tensões, potência, fator K,
    /// impedância, observações, dimensão). A UC do trafo não muda por aqui:
    /// vínculo só pela associação (12.1). Null se deu certo, o porquê se não.
    /// </summary>
    public string? EditTransformer(Transformer edited)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var posicao = _trafos.FindIndex(t => t.Id == edited.Id);
        if (posicao < 0) return Tr.T("esse transformador não está mais no cadastro");

        if (ValidTransformer(edited, out var valido) is { } porque) return porque;

        _trafos[posicao] = valido! with { ConsumerUnit = _trafos[posicao].ConsumerUnit };
        return null;
    }

    /// <summary>
    /// O Salvar do formulário do trafo (13.1): os campos do cadastro e a UC
    /// escolhida (vazio = nenhuma), tudo ou nada. A UC segue a mesma trava da
    /// aba Subestação (<see cref="LinkTransformer"/>). Null se deu certo, o
    /// porquê se não (e nada muda).
    /// </summary>
    public string? SaveTransformer(Transformer edited, Guid unit)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var posicao = _trafos.FindIndex(t => t.Id == edited.Id);
        if (posicao < 0) return Tr.T("esse transformador não está mais no cadastro");

        if (ValidTransformer(edited, out var valido) is { } porque) return porque;
        if (LinkProblem(_trafos[posicao], unit) is { } trava) return trava;

        _trafos[posicao] = valido! with { ConsumerUnit = unit };
        return null;
    }

    /// <summary>Confere os campos do cadastro do trafo: o trafo com nome, apelido e observações aparados, ou o porquê.</summary>
    private string? ValidTransformer(Transformer edited, out Transformer? valido)
    {
        valido = null;

        var apelido = edited.Nickname?.Trim() ?? string.Empty;
        var nome = edited.Name?.Trim() ?? string.Empty;

        if (apelido.Length == 0) return Tr.T("o apelido (tag) não pode ficar vazio");
        if (apelido.Length > ElectricalDefaults.MaxNameLength || nome.Length > ElectricalDefaults.MaxNameLength)
            return Tr.F("nome e apelido têm no máximo {0} caracteres", ElectricalDefaults.MaxNameLength);
        if (_trafos.Any(t => t.Id != edited.Id && SameName(t.Nickname, apelido)))
            return Tr.F("já existe um transformador com o apelido \"{0}\"", apelido);

        double[] numeros = [edited.InputVoltage, edited.OutputVoltage, edited.PowerKva, edited.KFactor, edited.ImpedancePercent];
        if (numeros.Any(v => !double.IsFinite(v) || v < 0))
            return Tr.T("tensões, potência, fator K e impedância não podem ser negativos");
        if (!edited.Size.IsValid) return Tr.T("largura, comprimento e altura têm que ser maiores que zero");

        valido = edited with { Name = nome, Nickname = apelido, Notes = edited.Notes?.Trim() ?? string.Empty };
        return null;
    }

    /// <summary>
    /// Tira o trafo do cadastro. Os inversores do skid dele ficam sem trafo
    /// (o vínculo some, o inversor fica). Quantos inversores foram soltos, ou
    /// null se o trafo não existia.
    /// </summary>
    public int? RemoveTransformer(Guid id)
    {
        if (_trafos.RemoveAll(t => t.Id == id) == 0) return null;

        _skids.RemoveAll(s => s.Transformer == id);
        var soltos = 0;
        for (var i = 0; i < _inversores.Count; i++)
        {
            if (_inversores[i].Transformer != id) continue;
            _inversores[i] = _inversores[i] with { Transformer = Guid.Empty };
            soltos++;
        }

        return soltos;
    }

    // ------------------------------------------------- modelos de inversor

    /// <summary>
    /// Cria o próximo modelo ("Modelo de inversor 1, 2..."), genérico: 1 MPPT
    /// com 1 entrada e o tamanho padrão, para o projetista ajustar (14.1).
    /// </summary>
    public InverterModel AddModel()
    {
        var n = NextNumber(_modelos.Select(m => m.Name), Tr.F("Modelo de inversor {0}", string.Empty));
        // 6 MPPTs x 4 entradas (05/10/2026: com 1 x 1, o modelo salvo sem
        // mexer deixava cada inversor com uma string só).
        var modelo = new InverterModel(Guid.NewGuid(), Tr.F("Modelo de inversor {0}", n), 6, 4, ElectricalDefaults.InverterSize);
        _modelos.Add(modelo);
        return modelo;
    }

    /// <summary>
    /// Troca nome, os MPPTs com as entradas de cada um e a dimensão (o total
    /// é a soma).
    /// Null se deu certo, o porquê se não.
    /// </summary>
    public string? EditModel(InverterModel edited)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var posicao = _modelos.FindIndex(m => m.Id == edited.Id);
        if (posicao < 0) return Tr.T("esse modelo de inversor não está mais no cadastro");

        var nome = edited.Name?.Trim() ?? string.Empty;
        if (nome.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (nome.Length > ElectricalDefaults.MaxNameLength) return Tr.F("o nome tem no máximo {0} caracteres", ElectricalDefaults.MaxNameLength);
        if (_modelos.Any(m => m.Id != edited.Id && SameName(m.Name, nome))) return Tr.F("já existe um modelo de inversor chamado \"{0}\"", nome);
        if (edited.InputsByMppt is null || edited.Mppts is < 1 or > ElectricalDefaults.MaxMppts || edited.InputsByMppt.Any(n => n is < 1 or > ElectricalDefaults.MaxInputsPerMppt))
            return Tr.F("de 1 a {0} MPPTs, cada um com 1 a {1} entradas", ElectricalDefaults.MaxMppts, ElectricalDefaults.MaxInputsPerMppt);
        if (!edited.Size.IsValid) return Tr.T("largura, comprimento e altura têm que ser maiores que zero");
        if (!InverterModel.IsValidPower(edited.PowerKw)) return Tr.F("a potência vai de 0 (não informada) a {0:#,0} kW", ElectricalDefaults.MaxInverterPowerKw);

        _modelos[posicao] = edited with { Name = nome, InputsByMppt = [.. edited.InputsByMppt] };
        return null;
    }

    /// <summary>
    /// Tira o modelo do cadastro, se nenhum inversor é dele (o inversor
    /// precisa do modelo). Null se tirou, o porquê se não.
    /// </summary>
    public string? RemoveModel(Guid id)
    {
        if (FindModel(id) is null) return Tr.T("esse modelo de inversor não está mais no cadastro");

        var usam = _inversores.Count(i => i.Model == id);
        if (usam > 0) return Tr.F("{0} inversor(es) são deste modelo; apague-os antes", usam);

        _modelos.RemoveAll(m => m.Id == id);
        return null;
    }

    // ----------------------------------------------------------- inversores

    public Inverter? FindInverter(Guid id) => _inversores.FirstOrDefault(i => i.Id == id);

    /// <summary>O inversor pelo GUID ou pelo nome (sem olhar maiúscula); null se nenhum ou mais de um responde.</summary>
    public Inverter? FindInverter(string? nameOrId)
    {
        var texto = nameOrId?.Trim() ?? string.Empty;
        if (texto.Length == 0) return null;
        if (Guid.TryParse(texto, out var id)) return FindInverter(id);

        var achados = _inversores.Where(i => SameName(i.Name, texto)).Take(2).ToList();
        return achados.Count == 1 ? achados[0] : null;
    }

    /// <summary>
    /// Cria <paramref name="count"/> inversores do modelo (14.2), "Inversor N"
    /// continuando do maior número da usina (qualquer modelo), sem skid, cada
    /// um com uma cor da paleta (<see cref="InverterColors"/>), as menos usadas primeiro.
    /// </summary>
    public IReadOnlyList<Inverter> AddInverters(Guid model, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, ElectricalDefaults.MaxAtOnce);
        if (FindModel(model) is null) throw new InvalidOperationException(Tr.T("esse modelo de inversor não está mais no cadastro"));

        var prefixo = Tr.F("Inversor {0}", string.Empty);
        var primeiro = NextNumber(_inversores.Select(i => i.Name), prefixo);
        var novos = new List<Inverter>(count);
        foreach (var n in Enumerable.Range(primeiro, count))
        {
            // Cada um com a cor da paleta menos usada até aqui (os novos contam).
            var cor = InverterColors.Next(_inversores.Concat(novos).Select(i => i.Color).OfType<RgbColor>());
            novos.Add(new Inverter(Guid.NewGuid(), model, Tr.F("Inversor {0}", n), Guid.Empty, cor));
        }

        _inversores.AddRange(novos);
        return novos;
    }

    /// <summary>
    /// Troca o nome e o modelo do inversor (14.5). O nome é único na usina
    /// (é por ele que o inversor é achado). Null se deu certo, o porquê se não.
    /// </summary>
    public string? EditInverter(Guid id, string? name, Guid model)
    {
        var posicao = _inversores.FindIndex(i => i.Id == id);
        if (posicao < 0) return Tr.T("esse inversor não está mais no cadastro");

        var nome = name?.Trim() ?? string.Empty;
        if (InverterNameProblem(id, nome) is { } porque) return porque;
        if (FindModel(model) is null) return Tr.T("esse modelo de inversor não está mais no cadastro");

        _inversores[posicao] = _inversores[posicao] with { Name = nome, Model = model };
        return null;
    }

    /// <summary>O porquê de o nome (já aparado) não servir para o inversor, ou null.</summary>
    private string? InverterNameProblem(Guid id, string nome)
    {
        if (nome.Length == 0) return Tr.T("o nome não pode ficar vazio");
        if (nome.Length > ElectricalDefaults.MaxNameLength) return Tr.F("o nome tem no máximo {0} caracteres", ElectricalDefaults.MaxNameLength);
        if (Guid.TryParse(nome, out _)) return Tr.T("o nome não pode ser um GUID");
        if (_inversores.Any(i => i.Id != id && SameName(i.Name, nome))) return Tr.F("já existe um inversor chamado \"{0}\"", nome);
        return null;
    }

    /// <summary>
    /// Só o nome do inversor (o nome editado na própria linha da tabela,
    /// 05/10/2026), com as mesmas regras de <see cref="EditInverter"/>; o
    /// modelo não muda (nem é conferido: um inversor de modelo que sumiu
    /// também pode ser renomeado). Null se deu certo, o porquê se não.
    /// </summary>
    public string? RenameInverter(Guid id, string? name)
    {
        var posicao = _inversores.FindIndex(i => i.Id == id);
        if (posicao < 0) return Tr.T("esse inversor não está mais no cadastro");

        var nome = name?.Trim() ?? string.Empty;
        if (InverterNameProblem(id, nome) is { } porque) return porque;

        _inversores[posicao] = _inversores[posicao] with { Name = nome };
        return null;
    }

    /// <summary>
    /// Só o modelo do inversor (a caixa Modelo da linha da tabela,
    /// 05/10/2026). Recusa o modelo com menos entradas que as strings já
    /// alocadas nele (<paramref name="allocatedStrings"/>, contadas pelo
    /// vínculo no desenho por quem chama): trocar deixaria o inversor em
    /// excesso sem ninguém pedir. Null se deu certo, o porquê se não.
    /// </summary>
    public string? ChangeInverterModel(Guid id, Guid model, int allocatedStrings)
    {
        var posicao = _inversores.FindIndex(i => i.Id == id);
        if (posicao < 0) return Tr.T("esse inversor não está mais no cadastro");
        if (FindModel(model) is not { } modelo) return Tr.T("esse modelo de inversor não está mais no cadastro");

        var inversor = _inversores[posicao];
        if (inversor.Model == model) return null;
        if (allocatedStrings > modelo.TotalInputs)
            return Tr.F("o {0} tem {1} entradas e o {2} já tem {3} strings; solte {4} string(s) antes ou escolha um modelo maior",
                modelo.Name, modelo.TotalInputs, inversor.Name, allocatedStrings, allocatedStrings - modelo.TotalInputs);

        _inversores[posicao] = inversor with { Model = model };
        return null;
    }

    /// <summary>
    /// Troca a cor do inversor (só representação: as strings dele são
    /// repintadas por quem chama; o vínculo não muda). Se ele existia.
    /// </summary>
    public bool SetInverterColor(Guid id, RgbColor color)
    {
        var posicao = _inversores.FindIndex(i => i.Id == id);
        if (posicao < 0) return false;

        _inversores[posicao] = _inversores[posicao] with { Color = color };
        return true;
    }

    /// <summary>
    /// Tira o inversor do cadastro; se existia. As strings dele têm que ser
    /// soltas por quem chama (o vínculo mora na string, no desenho).
    /// </summary>
    public bool RemoveInverter(Guid id)
    {
        if (FindInverter(id) is not { } inversor) return false;

        _inversores.RemoveAll(i => i.Id == id);
        for (var c = 0; c < _combiners.Count; c++)
            if (_combiners[c].Inverter == id) _combiners[c] = _combiners[c] with { Inverter = Guid.Empty };
        if (inversor.Transformer != Guid.Empty && InvertersOf(inversor.Transformer).Count == 0) _skids.RemoveAll(s => s.Transformer == inversor.Transformer);
        return true;
    }

    /// <summary>
    /// Tira vários inversores de uma vez (as linhas escolhidas da tabela,
    /// 05/10/2026), cada um como <see cref="RemoveInverter"/>. Devolve os que
    /// existiam e saíram, na ordem do cadastro (as strings deles têm que ser
    /// soltas por quem chama).
    /// </summary>
    public IReadOnlyList<Inverter> RemoveInverters(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var pedidos = ids.ToHashSet();
        var sairam = _inversores.Where(i => pedidos.Contains(i.Id)).ToList();
        foreach (var i in sairam) RemoveInverter(i.Id);
        return sairam;
    }

    // ------------------------------------------------------------- combiner

    /// <summary>Uma combiner nova (19.1): "CB N", 16 entradas, sem inversor.</summary>
    public Combiner AddCombiner()
    {
        var n = NextNumber(_combiners.Select(c => c.Name), "CB");
        var c = new Combiner(Guid.NewGuid(), "CB" + n, 16, ElectricalDefaults.CombinerSize, Guid.Empty);
        _combiners.Add(c);
        return c;
    }

    /// <summary>Grava a combiner editada (nome único, entradas, dimensão, inversor que existe). Null se gravou, o porquê se não.</summary>
    public string? EditCombiner(Combiner editada)
    {
        ArgumentNullException.ThrowIfNull(editada);
        var i = _combiners.FindIndex(c => c.Id == editada.Id);
        if (i < 0) return Tr.T("essa combiner não está mais no cadastro");
        if (string.IsNullOrWhiteSpace(editada.Name)) return Tr.T("o nome (tag) da combiner não pode ficar em branco");
        if (_combiners.Any(c => c.Id != editada.Id && SameName(c.Name, editada.Name))) return Tr.F("já há uma combiner chamada {0}", editada.Name.Trim());
        if (editada.Inputs is < 1 or > Combiner.MaxInputs) return Tr.F("as entradas têm que ser de 1 a {0}", Combiner.MaxInputs);
        if (!editada.Size.IsValid) return Tr.T("largura, comprimento e altura têm que ser maiores que zero");
        if (editada.Inverter != Guid.Empty && FindInverter(editada.Inverter) is null) return Tr.T("esse inversor não está mais no cadastro");

        _combiners[i] = editada with { Name = editada.Name.Trim() };
        return null;
    }

    /// <summary>Tira a combiner do cadastro; as strings dela são soltas por quem chama.</summary>
    public bool RemoveCombiner(Guid id) => _combiners.RemoveAll(c => c.Id == id) > 0;

    // ----------------------------------------------------------------- skid

    /// <summary>Os inversores do skid do trafo (o vínculo mora no inversor).</summary>
    public IReadOnlyList<Inverter> InvertersOf(Guid transformer) => _inversores.Where(i => i.Transformer == transformer).ToList();

    /// <summary>
    /// Agrupa os inversores no skid do trafo (14.7), com nome (vazio = "Skid
    /// T1"). Inversor de outro skid fica travado: tirar de lá antes é um ato
    /// explícito, nada muda de dono sozinho. Vínculo para trafo que não existe
    /// mais não trava. O nome do skid só é gravado se ele fica com inversor.
    /// </summary>
    public SkidResult Group(Guid transformer, string? name, IEnumerable<Guid> inverters)
    {
        ArgumentNullException.ThrowIfNull(inverters);

        if (FindTransformer(transformer) is not { } trafo) return new SkidResult(0, 0, [], 0, Tr.T("esse transformador não está mais no cadastro"));

        var nome = name?.Trim() ?? string.Empty;
        if (nome.Length == 0) nome = FindSkid(transformer)?.Name ?? Tr.F("Skid {0}", trafo.Nickname);
        if (nome.Length > ElectricalDefaults.MaxNameLength) return new SkidResult(0, 0, [], 0, Tr.F("o nome tem no máximo {0} caracteres", ElectricalDefaults.MaxNameLength));

        int entraram = 0, jaEram = 0, sumidos = 0;
        var recusados = new List<Inverter>();

        foreach (var id in inverters.Distinct())
        {
            var posicao = _inversores.FindIndex(i => i.Id == id);
            if (posicao < 0) { sumidos++; continue; }

            var inversor = _inversores[posicao];
            if (inversor.Transformer == transformer) jaEram++;
            else if (FindTransformer(inversor.Transformer) is not null) recusados.Add(inversor);
            else
            {
                _inversores[posicao] = inversor with { Transformer = transformer };
                entraram++;
            }
        }

        if (InvertersOf(transformer).Count > 0)
        {
            _skids.RemoveAll(s => s.Transformer == transformer);
            _skids.Add(new Skid(transformer, nome));
        }

        return new SkidResult(entraram, jaEram, recusados, sumidos, null);
    }

    /// <summary>
    /// Tira o inversor do skid (o inversor fica, sem trafo). O skid que fica
    /// sem inversor deixa de existir. Se havia o que tirar.
    /// </summary>
    public bool Ungroup(Guid inverter)
    {
        var posicao = _inversores.FindIndex(i => i.Id == inverter);
        if (posicao < 0 || _inversores[posicao].Transformer == Guid.Empty) return false;

        var trafo = _inversores[posicao].Transformer;
        _inversores[posicao] = _inversores[posicao] with { Transformer = Guid.Empty };
        if (InvertersOf(trafo).Count == 0) _skids.RemoveAll(s => s.Transformer == trafo);
        return true;
    }

    /// <summary>
    /// Põe os inversores no trafo (a coluna Trafo da aba Inversor e o "Pôr no
    /// trafo" das linhas escolhidas, 05/10/2026). É o mesmo vínculo do skid
    /// (<see cref="Inverter.Transformer"/>), mas por escolha explícita na
    /// tabela: o inversor que era de outro trafo MUDA (não fica travado como
    /// na seleção em campo de <see cref="Group"/>). <see cref="Guid.Empty"/>
    /// = sem trafo. O nome do skid do trafo de destino fica; o skid de onde o
    /// inversor saiu e que ficou sem inversor deixa de existir (como em
    /// <see cref="Ungroup"/>).
    /// </summary>
    public TransformerAssignment SetTransformer(IEnumerable<Guid> inverters, Guid transformer)
    {
        ArgumentNullException.ThrowIfNull(inverters);

        if (transformer != Guid.Empty && FindTransformer(transformer) is null)
            return new TransformerAssignment(0, 0, 0, Tr.T("esse transformador não está mais no cadastro"));

        int mudaram = 0, jaEram = 0, sumidos = 0;
        var deixados = new HashSet<Guid>();

        foreach (var id in inverters.Distinct())
        {
            var posicao = _inversores.FindIndex(i => i.Id == id);
            if (posicao < 0) { sumidos++; continue; }

            var antes = _inversores[posicao].Transformer;
            if (antes == transformer) { jaEram++; continue; }

            _inversores[posicao] = _inversores[posicao] with { Transformer = transformer };
            if (antes != Guid.Empty) deixados.Add(antes);
            mudaram++;
        }

        foreach (var trafo in deixados)
            if (InvertersOf(trafo).Count == 0) _skids.RemoveAll(s => s.Transformer == trafo);

        return new TransformerAssignment(mudaram, jaEram, sumidos, null);
    }

    // ----------------------------------------------------------- comuns

    public static bool SameName(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// O número depois do maior nome do padrão (prefixo seguido só de
    /// algarismos: "T3", "Inversor 12"). Quem saiu não volta a ser usado, e
    /// nome dado pelo usuário não conta.
    /// </summary>
    internal static int NextNumber(IEnumerable<string> nomes, string prefixo)
    {
        var maior = 0;

        foreach (var nome in nomes)
        {
            if (nome is null || !nome.StartsWith(prefixo, StringComparison.CurrentCultureIgnoreCase)) continue;

            var resto = nome.AsSpan(prefixo.Length);
            if (resto.Length > 0 && int.TryParse(resto, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                maior = Math.Max(maior, n);
        }

        return maior + 1;
    }
}
