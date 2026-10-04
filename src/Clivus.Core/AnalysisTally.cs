using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// A última quantificação de uma análise (passo 8.12), gravada no desenho
/// para ir ao Excel: a regra com que foi feita, a contagem dos pontos
/// (pilares, ou mesas na declividade) e, nas pontas, a dos módulos.
/// </summary>
public sealed record AnalysisTally(
    IndependentKind Kind,
    ThresholdRule Rule,
    SlopeUnit Unit,
    BandCount Points,
    BandCount? Modules,
    DateTime When)
{
    private const string Versao = "1";

    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public static string StorageKey(IndependentKind tipo) => "QUANT_" + tipo.ToString().ToUpperInvariant();

    /// <summary>Para o registro.</summary>
    public IReadOnlyList<string> Encode()
    {
        var campos = new List<string> { Versao, SlopeLabel.Name(Unit), When.ToString("O", CultureInfo.InvariantCulture) };
        campos.AddRange(Contagem(Points));
        campos.AddRange(Modules is null ? ["", "", "", ""] : Contagem(Modules));
        campos.AddRange(IndependentAnalysis.Encode(Rule));
        return campos;
    }

    /// <summary>Do registro; null se ilegível.</summary>
    public static AnalysisTally? Decode(IndependentKind tipo, IReadOnlyList<string>? campos)
    {
        if (campos is null || campos.Count != 17 || campos[0] != Versao) return null;

        var unidade = SlopeLabel.Parse(campos[1]);
        if (unidade is null) return null;

        if (!DateTime.TryParse(campos[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var quando)) return null;

        var pontos = LerContagem(campos, 3);
        if (pontos is null) return null;

        var modulos = campos[7] == "" ? null : LerContagem(campos, 7);
        var regra = IndependentAnalysis.Decode(campos.Skip(11).ToList(), tipo);

        return new AnalysisTally(tipo, regra, unidade.Value, pontos, modulos, quando);
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
