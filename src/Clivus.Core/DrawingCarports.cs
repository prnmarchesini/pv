using System.Globalization;

namespace Clivus.Core;

/// <summary>Um carport cadastrado no desenho: o perfil e a cor com que ele aparece.</summary>
public sealed record DrawingCarport(CarportProfile Profile, RgbColor Color)
{
    /// <summary>O nome do carport (o do perfil, aparado).</summary>
    public string Name => Profile.Name.Trim();
}

/// <summary>
/// Os carports cadastrados NO DESENHO (35.1, <c>plano/carport</c>): a mesma
/// ideia das mesas (<see cref="DrawingTables"/>), num registro próprio
/// (<see cref="StorageKey"/>), para não mexer no das mesas (regra 4 do plano:
/// nada da fixa muda). Versão, quantos, e por carport a cor e o perfil em
/// JSON partido em pedaços.
/// </summary>
public static class DrawingCarports
{
    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public const string StorageKey = "CARPORTS";

    private const string Versao = "1";

    /// <summary>Tamanho de cada pedaço do JSON (o mesmo das mesas).</summary>
    private const int Pedaco = 200;

    /// <summary>A cor do primeiro carport: cinza-aço, longe das cores das mesas e das análises.</summary>
    public static readonly RgbColor DefaultColor = new(150, 160, 170);

    /// <summary>Para o registro.</summary>
    public static IReadOnlyList<string> Encode(IReadOnlyList<DrawingCarport> carports)
    {
        ArgumentNullException.ThrowIfNull(carports);

        var campos = new List<string> { Versao, carports.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var c in carports)
        {
            var json = c.Profile.ToJson();
            var pedacos = Enumerable.Range(0, (json.Length + Pedaco - 1) / Pedaco)
                .Select(i => json.Substring(i * Pedaco, Math.Min(Pedaco, json.Length - i * Pedaco)))
                .ToList();

            campos.Add(c.Color.ToHex());
            campos.Add(pedacos.Count.ToString(CultureInfo.InvariantCulture));
            campos.AddRange(pedacos);
        }

        return campos;
    }

    /// <summary>
    /// Do registro. Carport ilegível fica de fora e entra em <paramref name="problemas"/>;
    /// registro de outra versão ou quebrado devolve a lista vazia com o motivo.
    /// </summary>
    public static IReadOnlyList<DrawingCarport> Decode(IReadOnlyList<string>? campos, out IReadOnlyList<string> problemas)
    {
        var lista = new List<DrawingCarport>();
        var erros = new List<string>();
        problemas = erros;

        if (campos is null || campos.Count == 0) return lista;

        if (campos[0] != Versao || campos.Count < 2 || !int.TryParse(campos[1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantos))
        {
            erros.Add(Tr.T("o registro dos carports do desenho é de outra versão ou está quebrado"));
            return lista;
        }

        var i = 2;
        for (var n = 0; n < quantos; n++)
        {
            if (i + 2 > campos.Count
                || !RgbColor.TryParseHex(campos[i], out var cor)
                || !int.TryParse(campos[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var pedacos)
                || i + 2 + pedacos > campos.Count)
            {
                erros.Add(Tr.F("o carport {0} do registro está quebrado; os seguintes não puderam ser lidos", n + 1));
                return lista;
            }

            var json = string.Concat(campos.Skip(i + 2).Take(pedacos));
            i += 2 + pedacos;

            try
            {
                lista.Add(new DrawingCarport(CarportProfile.Parse(json), cor));
            }
            catch (InvalidOperationException erro)
            {
                erros.Add(Tr.F("o carport {0} do registro não pôde ser lido: {1}", n + 1, erro.Message));
            }
        }

        return lista;
    }

    /// <summary>
    /// Por que a lista não serve, ou null: nome em branco, repetido entre os
    /// carports ou igual ao de uma mesa (na lista das Estruturas os dois
    /// aparecem juntos, e o nome é o que o projetista lê).
    /// </summary>
    public static string? WhyInvalid(IReadOnlyList<DrawingCarport> carports, IReadOnlyList<DrawingTable> mesas)
    {
        ArgumentNullException.ThrowIfNull(carports);
        ArgumentNullException.ThrowIfNull(mesas);

        if (carports.Any(c => c.Name.Length == 0)) return Tr.T("há carport sem nome");

        var repetido = carports.GroupBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null) return Tr.F("há dois carports chamados \"{0}\"", repetido.Key);

        var igual = carports.FirstOrDefault(c => DrawingTables.Find(mesas, c.Name) is not null);
        return igual is null ? null : Tr.F("o carport \"{0}\" tem o mesmo nome de uma mesa", igual.Name);
    }

    /// <summary>O carport com este nome, ou null.</summary>
    public static DrawingCarport? Find(IEnumerable<DrawingCarport> carports, string nome) =>
        carports.FirstOrDefault(c => string.Equals(c.Name, nome.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
