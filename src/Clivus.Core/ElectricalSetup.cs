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
    private readonly List<Transformer> _trafos;
    private readonly List<Inverter> _inversores;

    public ElectricalSetup(IEnumerable<Transformer>? transformers = null, IEnumerable<Inverter>? inverters = null)
    {
        _trafos = transformers?.ToList() ?? [];
        _inversores = inverters?.ToList() ?? [];
    }

    public IReadOnlyList<Transformer> Transformers => _trafos;

    public IReadOnlyList<Inverter> Inverters => _inversores;

    public Transformer? FindTransformer(Guid id) => _trafos.FirstOrDefault(t => t.Id == id);

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
