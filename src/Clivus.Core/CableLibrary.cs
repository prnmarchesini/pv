using System.Globalization;
using System.Text.Json;

namespace Clivus.Core;

/// <summary>O universo de um cabo da biblioteca (22.1).</summary>
public enum CableType
{
    /// <summary>Cabo solar de corrente contínua (strings).</summary>
    Dc,

    /// <summary>Baixa tensão alternada (inversor -> trafo).</summary>
    Ac,

    /// <summary>Média tensão (trafo -> subestação).</summary>
    Mv,
}

/// <summary>
/// Um cabo da biblioteca (22.1). Resistências e reatância em ohm/km; a
/// capacidade de condução em A por método de instalação (código da NBR 5410:
/// A1, A2, B1, B2, C, D, E, F...). O sistema não escolhe nem aprova cabo:
/// só usa os números que estão aqui (regra 8).
/// </summary>
public sealed record Cable(
    Guid Id,
    string Name,
    CableType Type,
    double SectionMm2,
    string Insulation,
    double Resistance20,
    double ResistanceOperating,
    double Reactance,
    string Formation,
    string Conductor,
    string InsulationMaterial,
    IReadOnlyDictionary<string, double> Ampacity)
{
    public const int FieldCount = 12;

    public bool IsValid =>
        Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Enum.IsDefined(Type)
        && Positivo(SectionMm2) && Positivo(Resistance20) && Positivo(ResistanceOperating) && double.IsFinite(Reactance) && Reactance >= 0
        && Ampacity.All(a => a.Key.Length > 0 && Positivo(a.Value));

    /// <summary>A corrente admissível do método (null se o cabo não tem valor para ele).</summary>
    public double? AmpacityFor(string? method) =>
        method is not null && Ampacity.TryGetValue(method.Trim().ToUpperInvariant(), out var a) ? a : null;

    /// <summary>"B1=66;D=58": o mapa como vai para o campo gravado.</summary>
    public static string FormatAmpacity(IReadOnlyDictionary<string, double> mapa) =>
        string.Join(";", mapa.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value.ToString("R", CultureInfo.InvariantCulture)));

    /// <summary>Lê "B1=66;D=58" (também com vírgula decimal e espaços); null se algum pedaço não se lê.</summary>
    public static IReadOnlyDictionary<string, double>? ParseAmpacity(string? texto, IFormatProvider? cultura = null)
    {
        var mapa = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var pedaco in (texto ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = pedaco.Split('=', StringSplitOptions.TrimEntries);
            if (partes.Length != 2 || partes[0].Length == 0) return null;
            if (!double.TryParse(partes[1], NumberStyles.Float, cultura ?? CultureInfo.InvariantCulture, out var v) || !Positivo(v)) return null;
            mapa[partes[0].ToUpperInvariant()] = v;
        }

        return mapa;
    }

    public IReadOnlyList<string> ToFields() =>
    [
        Id.ToString("D"), Name, Type.ToString(), Num(SectionMm2), Insulation, Num(Resistance20), Num(ResistanceOperating), Num(Reactance),
        Formation, Conductor, InsulationMaterial, FormatAmpacity(Ampacity),
    ];

    public static Cable? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Guid.TryParse(c[0], out var id)) return null;
        if (!Enum.TryParse<CableType>(c[2], out var tipo)) return null;
        if (!RouteSettings.Real(c[3], out var s) || !RouteSettings.Real(c[5], out var r20) || !RouteSettings.Real(c[6], out var rop) || !RouteSettings.Real(c[7], out var x)) return null;
        if (ParseAmpacity(c[11]) is not { } mapa) return null;

        var cabo = new Cable(id, c[1], tipo, s, c[4], r20, rop, x, c[8], c[9], c[10], mapa);
        return cabo.IsValid ? cabo : null;
    }

    public bool Equals(Cable? other) =>
        other is not null && ToFields().SequenceEqual(other.ToFields());

    public override int GetHashCode() => Id.GetHashCode();

    private static bool Positivo(double v) => double.IsFinite(v) && v > 0;

    private static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>
/// A biblioteca de cabos (22.1): um arquivo JSON editável na pasta do
/// usuário. Sem o arquivo, começa da lista de partida (<see cref="Default"/>),
/// com valores típicos de catálogo e da NBR 5410 que o Renan revisa: a tela
/// diz que são valores de partida.
/// </summary>
public static class CableLibrary
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    /// <summary>O nome do arquivo na pasta do usuário.</summary>
    public const string FileName = "cabos.json";

    /// <summary>
    /// As vias que a formação diz: "3x1x25" (três cabos de um condutor) = 3;
    /// "1x6", "1x(3x95)" ou "4x16" (um cabo de vários condutores) = 1. Null
    /// se a formação não começa com número.
    /// </summary>
    public static int? WiresFromFormation(string? formacao)
    {
        var texto = formacao ?? string.Empty;
        var unipolares = System.Text.RegularExpressions.Regex.Match(texto, @"^\s*(\d{1,2})\s*[xX×]\s*1\s*[xX×]");
        if (unipolares.Success && int.TryParse(unipolares.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1) return n;
        return System.Text.RegularExpressions.Regex.IsMatch(texto, @"^\s*\d") ? 1 : null;
    }

    /// <summary>Lê a biblioteca; arquivo que não existe = a de partida. Problema de leitura vai em <paramref name="problem"/> e volta a de partida.</summary>
    public static IReadOnlyList<Cable> Load(string caminho, out string? problem)
    {
        problem = null;
        if (!File.Exists(caminho)) return Default();

        try
        {
            var lidos = JsonSerializer.Deserialize<List<Entrada>>(File.ReadAllText(caminho)) ?? [];
            var cabos = new List<Cable>();
            foreach (var e in lidos)
            {
                if (e.ToCable() is { } c) cabos.Add(c);
                else problem = Tr.F("cabo ilegível na biblioteca: {0}", e.Name ?? "?");
            }

            return cabos;
        }
        catch (Exception erro) when (erro is IOException or JsonException or UnauthorizedAccessException)
        {
            problem = Tr.F("não consegui ler a biblioteca de cabos ({0}): {1}", caminho, erro.Message);
            return Default();
        }
    }

    /// <summary>Grava (atômico: escreve ao lado e troca).</summary>
    public static void Save(string caminho, IReadOnlyList<Cable> cabos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        var provisorio = caminho + ".gravando";
        File.WriteAllText(provisorio, JsonSerializer.Serialize(cabos.Select(Entrada.From).ToList(), Opcoes));
        File.Move(provisorio, caminho, overwrite: true);
    }

    /// <summary>
    /// A lista de partida. Cobre: resistência a 20 °C de catálogo (classe 5
    /// flexível no CC, classe 2 no CA e na MT); na operação, corrigida para
    /// 90 °C (cobre ×1,275; alumínio ×1,282). Capacidades: NBR 5410 tabela
    /// 37 (EPR/XLPE, 3 condutores carregados) no CA; cabo solar ao ar e
    /// enterrado no CC; catálogo de 8,7/15 kV na MT. VALORES DE PARTIDA.
    /// </summary>
    public static IReadOnlyList<Cable> Default()
    {
        var lista = new List<Cable>();

        Cable Novo(string nome, CableType tipo, double secao, string isolacao, double r20, double fator, double x, string formacao, string condutor, string material, params (string M, double A)[] cap) =>
            new(Guid.Parse(GuidDe(nome)), nome, tipo, secao, isolacao, r20, Math.Round(r20 * fator, 4), x, formacao, condutor, material,
                cap.ToDictionary(c => c.M, c => c.A));

        // CC: cabo solar 1,8 kV CC, cobre estanhado classe 5. Métodos: C (ao ar, na estrutura) e D (enterrado em eletroduto).
        foreach (var (s, r, c, d) in new[] { (4.0, 5.09, 55.0, 44.0), (6.0, 3.39, 70.0, 56.0), (10.0, 1.95, 98.0, 75.0), (16.0, 1.24, 132.0, 96.0) })
            lista.Add(Novo($"Solar 1,8 kV CC {s:0} mm²", CableType.Dc, s, "1,8 kV CC", r, 1.275, 0, $"1x{s:0}", "Cobre", "XLPE", ("C", c), ("D", d)));

        // CA: 0,6/1 kV EPR, cobre classe 2. NBR 5410 tabela 37, 3 condutores carregados.
        foreach (var (s, r, b1, c, d) in new[]
                 {
                     (16.0, 1.15, 88.0, 96.0, 75.0), (25.0, 0.727, 117.0, 119.0, 96.0), (35.0, 0.524, 144.0, 147.0, 115.0),
                     (50.0, 0.387, 175.0, 179.0, 135.0), (70.0, 0.268, 222.0, 229.0, 167.0), (95.0, 0.193, 269.0, 278.0, 197.0),
                     (120.0, 0.153, 312.0, 322.0, 223.0), (150.0, 0.124, 355.0, 371.0, 251.0), (185.0, 0.0991, 417.0, 424.0, 281.0),
                     (240.0, 0.0754, 490.0, 500.0, 324.0),
                 })
            lista.Add(Novo($"0,6/1 kV EPR cobre {s:0} mm²", CableType.Ac, s, "0,6/1 kV", r, 1.275, 0.09, $"3x1x{s:0}", "Cobre", "EPR", ("B1", b1), ("C", c), ("D", d)));

        // CA em alumínio (comum em usina): 0,6/1 kV XLPE.
        foreach (var (s, r, c, d) in new[] { (95.0, 0.320, 213.0, 152.0), (120.0, 0.253, 246.0, 172.0), (185.0, 0.164, 325.0, 217.0), (240.0, 0.125, 385.0, 251.0) })
            lista.Add(Novo($"0,6/1 kV XLPE alumínio {s:0} mm²", CableType.Ac, s, "0,6/1 kV", r, 1.282, 0.09, $"3x1x{s:0}", "Alumínio", "XLPE", ("C", c), ("D", d)));

        // MT: 8,7/15 kV EPR, cobre. Capacidade de catálogo, enterrado (D) e ao ar (C).
        foreach (var (s, r, c, d) in new[] { (25.0, 0.727, 165.0, 150.0), (35.0, 0.524, 200.0, 180.0), (50.0, 0.387, 240.0, 210.0), (70.0, 0.268, 300.0, 260.0), (95.0, 0.193, 360.0, 310.0), (120.0, 0.153, 415.0, 350.0) })
            lista.Add(Novo($"8,7/15 kV EPR cobre {s:0} mm²", CableType.Mv, s, "8,7/15 kV", r, 1.275, 0.13, $"3x1x{s:0}", "Cobre", "EPR", ("C", c), ("D", d)));

        // MT: 20/35 kV (usina em 34,5 kV).
        foreach (var (s, r, c, d) in new[] { (35.0, 0.524, 205.0, 185.0), (50.0, 0.387, 245.0, 215.0), (70.0, 0.268, 305.0, 265.0), (95.0, 0.193, 365.0, 315.0) })
            lista.Add(Novo($"20/35 kV EPR cobre {s:0} mm²", CableType.Mv, s, "20/35 kV", r, 1.275, 0.14, $"3x1x{s:0}", "Cobre", "EPR", ("C", c), ("D", d)));

        return lista;
    }

    /// <summary>Que tipo de cabo cada rota usa.</summary>
    public static CableType TypeFor(CableRoute rota) => rota switch
    {
        CableRoute.AlternatingCurrent => CableType.Ac,
        CableRoute.MediumVoltage => CableType.Mv,
        _ => CableType.Dc,
    };

    /// <summary>GUID fixo pelo nome: a lista de partida tem sempre os mesmos GUIDs.</summary>
    private static string GuidDe(string nome)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("clivus-cabo:" + nome));
        return new Guid(hash).ToString("D");
    }

    /// <summary>O cabo como vai para o JSON (nomes em inglês, números invariantes).</summary>
    private sealed class Entrada
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public double SectionMm2 { get; set; }
        public string? Insulation { get; set; }
        public double Resistance20 { get; set; }
        public double ResistanceOperating { get; set; }
        public double Reactance { get; set; }
        public string? Formation { get; set; }
        public string? Conductor { get; set; }
        public string? InsulationMaterial { get; set; }
        public Dictionary<string, double>? Ampacity { get; set; }

        public static Entrada From(Cable c) => new()
        {
            Id = c.Id.ToString("D"), Name = c.Name, Type = c.Type.ToString(), SectionMm2 = c.SectionMm2, Insulation = c.Insulation,
            Resistance20 = c.Resistance20, ResistanceOperating = c.ResistanceOperating, Reactance = c.Reactance, Formation = c.Formation,
            Conductor = c.Conductor, InsulationMaterial = c.InsulationMaterial, Ampacity = c.Ampacity.ToDictionary(k => k.Key, k => k.Value),
        };

        public Cable? ToCable()
        {
            if (!Guid.TryParse(Id, out var id) || !Enum.TryParse<CableType>(Type, out var tipo)) return null;
            // "b1" e "B1" no mesmo cabo (editado à mão): vale o último, não derruba a leitura.
            var mapa = (Ampacity ?? []).GroupBy(k => k.Key.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Last().Value);
            var c = new Cable(id, Name ?? string.Empty, tipo, SectionMm2, Insulation ?? string.Empty, Resistance20, ResistanceOperating, Reactance,
                Formation ?? string.Empty, Conductor ?? string.Empty, InsulationMaterial ?? string.Empty, mapa);
            return c.IsValid ? c : null;
        }
    }
}
