using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// A contagem de uma linha de pilares (o pilar da frente, o de trás ou o da
/// monoposte), cada uma pelos limites dela (31/10/2026).
/// </summary>
public sealed record LineCount(PillarLine Line, BandCount Count);

/// <summary>
/// A última quantificação de uma análise (passo 8.12), gravada no desenho
/// para ir ao Excel: a regra com que foi feita, a contagem dos pontos
/// (pilares, ou mesas na declividade) e, nas pontas, a dos módulos.
/// </summary>
/// <param name="Scope">
/// Onde foi contada ("desenho inteiro", "Área 1", ...): desde 31/10/2026 a
/// análise pode ser de uma área só. Null nas gravadas antes, que eram do
/// desenho inteiro.
/// </param>
/// <param name="Lines">
/// Nas análises de pilar com biposte, a contagem de cada linha de pilar pelos
/// limites dela; <see cref="Points"/> é a soma. Null nas outras.
/// </param>
public sealed record AnalysisTally(
    IndependentKind Kind,
    ThresholdRule Rule,
    SlopeUnit Unit,
    BandCount Points,
    BandCount? Modules,
    DateTime When,
    string? Scope = null,
    IReadOnlyList<LineCount>? Lines = null)
{
    private const string Versao = "1";

    /// <summary>A versão com o lugar e as linhas de pilar (31/10/2026).</summary>
    private const string VersaoComLugar = "2";

    /// <summary>Os campos antes da regra: versão, unidade, quando, os pontos e os módulos (e, na 2, o lugar e as linhas).</summary>
    private const int AntesDaRegra = 11;

    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public static string StorageKey(IndependentKind tipo) => "QUANT_" + tipo.ToString().ToUpperInvariant();

    /// <summary>Onde foi contada, para a tela e o Excel: as antigas eram do desenho inteiro.</summary>
    public string Where => Scope is { Length: > 0 } onde ? onde : Tr.T("desenho inteiro");

    /// <summary>Para o registro.</summary>
    public IReadOnlyList<string> Encode()
    {
        var campos = new List<string> { VersaoComLugar, SlopeLabel.Name(Unit), When.ToString("O", CultureInfo.InvariantCulture) };
        campos.AddRange(Contagem(Points));
        campos.AddRange(Modules is null ? ["", "", "", ""] : Contagem(Modules));
        campos.Add(Scope ?? "");
        campos.Add(Lines is null ? "" : string.Join(";", Lines.Select(l => l.Line + "=" + string.Join(",", Contagem(l.Count)))));
        campos.AddRange(IndependentAnalysis.Encode(Rule));
        return campos;
    }

    /// <summary>Do registro; null se ilegível.</summary>
    public static AnalysisTally? Decode(IndependentKind tipo, IReadOnlyList<string>? campos)
    {
        if (campos is null || campos.Count < 3) return null;

        // A versão 1 tem a regra de seis campos logo depois dos módulos; a 2,
        // o lugar e as linhas antes da regra (de seis ou oito campos).
        var lugar = campos[0] == VersaoComLugar;
        if (!(campos[0] == Versao && campos.Count == AntesDaRegra + 6) && !(lugar && campos.Count is AntesDaRegra + 2 + 6 or AntesDaRegra + 2 + 8)) return null;

        var unidade = SlopeLabel.Parse(campos[1]);
        if (unidade is null) return null;

        if (!DateTime.TryParse(campos[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var quando)) return null;

        var pontos = LerContagem(campos, 3);
        if (pontos is null) return null;

        var modulos = campos[7] == "" ? null : LerContagem(campos, 7);

        string? onde = null;
        List<LineCount>? linhas = null;

        if (lugar)
        {
            onde = campos[AntesDaRegra] == "" ? null : campos[AntesDaRegra];

            if (campos[AntesDaRegra + 1] != "")
            {
                linhas = [];

                foreach (var parte in campos[AntesDaRegra + 1].Split(';'))
                {
                    var (nome, numeros) = (parte.Split('=')[0], parte.Split('=').ElementAtOrDefault(1)?.Split(','));
                    if (!Enum.TryParse<PillarLine>(nome, out var linha) || !Enum.IsDefined(linha) || numeros is not { Length: 4 }) return null;
                    if (LerContagem(numeros, 0) is not { } conta) return null;
                    linhas.Add(new LineCount(linha, conta));
                }
            }
        }

        var regra = IndependentAnalysis.Decode(campos.Skip(lugar ? AntesDaRegra + 2 : AntesDaRegra).ToList(), tipo);

        return new AnalysisTally(tipo, regra, unidade.Value, pontos, modulos, quando, onde, linhas);
    }

    private static IEnumerable<string> Contagem(BandCount c) =>
        new[] { c.Below, c.Inside, c.Above, c.Missing }.Select(n => n.ToString(CultureInfo.InvariantCulture));

    private static BandCount? LerContagem(IReadOnlyList<string> campos, int de)
    {
        var n = new int[4];

        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(campos[de + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]) || n[i] < 0)
                return null;
        }

        return new BandCount(n[0], n[1], n[2], n[3]);
    }
}
