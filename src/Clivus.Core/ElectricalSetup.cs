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

    /// <summary>Altura em que o retângulo do equipamento flutua sobre o terreno, em metros (12.3, 13.2, 14.6).</summary>
    public const double FloatHeight = 0.80;

    public static EquipmentSize TransformerSize { get; } = new(3.0, 2.5, 2.5);

    public static EquipmentSize ConsumerUnitSize { get; } = new(4.0, 3.0, 3.0);

    public static EquipmentSize InverterSize { get; } = new(1.1, 0.7, 0.6);

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

    public ElectricalSetup(
        IEnumerable<Transformer>? transformers = null,
        IEnumerable<Inverter>? inverters = null,
        IEnumerable<ConsumerUnit>? units = null)
    {
        _trafos = transformers?.ToList() ?? [];
        _inversores = inverters?.ToList() ?? [];
        _ucs = units?.ToList() ?? [];
    }

    public IReadOnlyList<ConsumerUnit> Units => _ucs;

    public IReadOnlyList<Transformer> Transformers => _trafos;

    public IReadOnlyList<Inverter> Inverters => _inversores;

    public Transformer? FindTransformer(Guid id) => _trafos.FirstOrDefault(t => t.Id == id);

    public ConsumerUnit? FindUnit(Guid id) => _ucs.FirstOrDefault(u => u.Id == id);

    // -------------------------------------------------------- subestações

    /// <summary>
    /// Cria a próxima subestação compartilhada (C1, C2...; o número segue o
    /// maior código, sem reaproveitar), com nome padrão e o tamanho padrão.
    /// </summary>
    public ConsumerUnit AddSharedUnit()
    {
        var codigo = "C" + NextNumber(_ucs.Select(u => u.Code), "C").ToString(CultureInfo.InvariantCulture);
        var uc = new ConsumerUnit(Guid.NewGuid(), codigo, Tr.F("Subestação {0}", codigo), ConsumerUnitMode.Shared, ElectricalDefaults.ConsumerUnitSize);
        _ucs.Add(uc);
        return uc;
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
        var uc = FindUnit(unit);
        if (uc is null) return Tr.T("essa subestação não está mais no cadastro");

        var posicao = _trafos.FindIndex(t => t.Id == transformer);
        if (posicao < 0) return Tr.T("esse transformador não está mais no cadastro");

        var trafo = _trafos[posicao];
        if (trafo.ConsumerUnit == unit) return null;

        // Vínculo para uma UC que não existe mais (registro estragado) não trava.
        if (FindUnit(trafo.ConsumerUnit) is { } dona)
            return Tr.F("{0} já está ligado a {1}; solte antes de ligar a outra subestação", trafo.Nickname, dona.Code);

        // A unitária é um bloquinho com o seu trafo: um só (12.2).
        if (uc.Mode == ConsumerUnitMode.Unitary && TransformersOf(unit).FirstOrDefault() is { } outro)
            return Tr.F("{0} é unitária e já tem o trafo {1}; solte-o antes", uc.Code, outro.Nickname);

        _trafos[posicao] = trafo with { ConsumerUnit = unit };
        return null;
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

        _trafos[posicao] = edited with { Name = nome, Nickname = apelido, Notes = edited.Notes?.Trim() ?? string.Empty, ConsumerUnit = _trafos[posicao].ConsumerUnit };
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

        var soltos = 0;
        for (var i = 0; i < _inversores.Count; i++)
        {
            if (_inversores[i].Transformer != id) continue;
            _inversores[i] = _inversores[i] with { Transformer = Guid.Empty };
            soltos++;
        }

        return soltos;
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
