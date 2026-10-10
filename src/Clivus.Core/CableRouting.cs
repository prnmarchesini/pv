using System.Globalization;
using Clivus.Geo;

namespace Clivus.Core;

// O contrato do roteamento de cabo (plano/roteamento): a vala é polilinha
// do usuário marcada com XData "Vala" (a camada é só aparência); cada lance
// de cabo é uma Polyline3d com XData "Lance" (GUID, rota, polaridade, de
// onde e para onde). O comprimento é sempre medido na geometria (regra 7):
// nunca guardado como número que possa ficar velho.

/// <summary>A polaridade de um lance: só o CC tem (regra 6: sempre os dois por string).</summary>
public enum CablePolarity
{
    None,
    Positive,
    Negative,
}

/// <summary>O que fica em cada ponta de um lance.</summary>
public enum CableEndKind
{
    String,
    Combiner,
    Inverter,
    Transformer,
    /// <summary>A subestação física: a UC unitária ou o bloco compartilhado (o mesmo GUID do retângulo em campo).</summary>
    Substation,
}

/// <summary>Uma ponta de lance: o tipo e o GUID do cadastro (ou da string).</summary>
public readonly record struct CableEnd(CableEndKind Kind, Guid Id);

/// <summary>As camadas e as cores de cada rota (17.2): uma para a vala e uma (duas no CC) para o cabo. Aparência, nunca identidade.</summary>
public static class CableLayers
{
    /// <summary>O pedaço do nome da rota nas camadas.</summary>
    public static string Code(CableRoute rota) => rota switch
    {
        CableRoute.DirectCurrent => "CC",
        CableRoute.Combiner => "COMBINER",
        CableRoute.AlternatingCurrent => "CA",
        _ => "MT",
    };

    public static string Trench(CableRoute rota) => PluginInfo.PrefixoDeDados + "_VALA_" + Code(rota);

    public static string Cable(CableRoute rota, CablePolarity polaridade) => PluginInfo.PrefixoDeDados + "_CABO_" + Code(rota) + polaridade switch
    {
        CablePolarity.Positive => "_POS",
        CablePolarity.Negative => "_NEG",
        _ => string.Empty,
    };

    /// <summary>A cor (ACI) da camada da vala: tons de terra, um por rota.</summary>
    public static short TrenchColor(CableRoute rota) => rota switch
    {
        CableRoute.DirectCurrent => 42,
        CableRoute.Combiner => 34,
        CableRoute.AlternatingCurrent => 22,
        _ => 14,
    };

    /// <summary>
    /// A cor (ACI) da camada do cabo: no CC o positivo vermelho e o negativo
    /// "preto" (ACI 7: preto no fundo branco, branco no escuro, para não sumir).
    /// </summary>
    public static short CableColor(CableRoute rota, CablePolarity polaridade) => (rota, polaridade) switch
    {
        (CableRoute.DirectCurrent, CablePolarity.Negative) => 7,
        (CableRoute.DirectCurrent, _) => 1,
        (CableRoute.Combiner, _) => 6,
        (CableRoute.AlternatingCurrent, _) => 4,
        _ => 5,
    };

    /// <summary>Todas as camadas do roteamento, com a cor de cada uma.</summary>
    public static IEnumerable<(string Name, short Color)> All() =>
        CableRoutes.All.SelectMany(r => (r == CableRoute.DirectCurrent
                ? new[] { CablePolarity.Positive, CablePolarity.Negative }
                : [CablePolarity.None])
            .Select(p => (Cable(r, p), CableColor(r, p)))
            .Prepend((Trench(r), TrenchColor(r))));
}

/// <summary>A marca de vala (17.4): a polilinha do usuário é vala desta rota. Uma vala serve a uma rota.</summary>
public sealed record TrenchMark(CableRoute Route)
{
    public const string Tipo = "Vala";
    public const int FieldCount = 1;

    public IReadOnlyList<string> ToFields() => [Route.ToString()];

    public static TrenchMark? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Enum.TryParse<CableRoute>(c[0], out var r) && Enum.IsDefined(r) ? new TrenchMark(r) : null;
}

/// <summary>
/// Um lance de cabo (17.6, regra 5): GUID próprio, a rota, a polaridade (só
/// CC) e as duas pontas. O comprimento não fica aqui: é o da geometria.
/// </summary>
public sealed record CableRun(Guid Id, CableRoute Route, CablePolarity Polarity, CableEnd From, CableEnd To)
{
    public const string Tipo = "Lance";
    public const int FieldCount = 7;

    public IReadOnlyList<string> ToFields() =>
        [Id.ToString("D"), Route.ToString(), Polarity.ToString(), From.Kind.ToString(), From.Id.ToString("D"), To.Kind.ToString(), To.Id.ToString("D")];

    public static CableRun? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount) return null;
        if (!Guid.TryParse(c[0], out var id) || id == Guid.Empty) return null;
        if (!Enum.TryParse<CableRoute>(c[1], out var rota) || !Enum.IsDefined(rota)) return null;
        if (!Enum.TryParse<CablePolarity>(c[2], out var pol) || !Enum.IsDefined(pol)) return null;
        if (!Ponta(c[3], c[4], out var de) || !Ponta(c[5], c[6], out var para)) return null;
        return new CableRun(id, rota, pol, de, para);
    }

    private static bool Ponta(string tipo, string guid, out CableEnd ponta)
    {
        ponta = default;
        if (!Enum.TryParse<CableEndKind>(tipo, out var k) || !Enum.IsDefined(k) || !Guid.TryParse(guid, out var g) || g == Guid.Empty) return false;
        ponta = new CableEnd(k, g);
        return true;
    }
}

/// <summary>
/// O lado forçado de uma string (18.3): o usuário mandou o cabo dela para
/// esta ponta da fileira. Guardado; a regeração respeita e não volta ao automático.
/// </summary>
public sealed record StringSide(Guid String, RowEnd End)
{
    public const int FieldCount = 2;

    public IReadOnlyList<string> ToFields() => [String.ToString("D"), End.ToString()];

    public static StringSide? Parse(IReadOnlyList<string> c) =>
        c.Count >= FieldCount && Guid.TryParse(c[0], out var s) && s != Guid.Empty && Enum.TryParse<RowEnd>(c[1], out var e) && Enum.IsDefined(e)
            ? new StringSide(s, e)
            : null;
}

/// <summary>
/// Os valores de uma aba da rota de cabos (17.3, 20.1, 22.2, 22.5), cada aba
/// independente: profundidade da vala, raio de busca da vala em volta do
/// equipamento, alcance da reta do fim da fileira até a vala (CC), fator de
/// potência (CA e MT), método de instalação e o cabo escolhido (uma cópia
/// do da biblioteca: o desenho não depende da biblioteca de quem abre).
/// </summary>
/// <remarks>Trifásico (<see cref="ThreePhase"/>) só conta no CA: o plano pede "se é trifásico"; a MT é sempre trifásica.</remarks>
public sealed record RouteSettings(CableRoute Route, double Depth, double Radius, double Reach, double PowerFactor, string Method, Cable? Cable, bool ThreePhase = true)
{
    public const int FixedFieldCount = 6;
    public const int FieldCount = FixedFieldCount + Cable.FieldCount + 1;

    public const double MaxDepth = 10;
    public const double MaxRadius = 500;

    /// <summary>O padrão de cada aba (valores de partida; o usuário muda).</summary>
    public static RouteSettings Default(CableRoute rota) => rota switch
    {
        CableRoute.DirectCurrent => new(rota, 0.6, 10, 100, 1, "D", null),
        CableRoute.Combiner => new(rota, 0.6, 10, 100, 1, "D", null),
        CableRoute.AlternatingCurrent => new(rota, 0.8, 10, 100, 1, "D", null),
        _ => new(rota, 1.0, 10, 100, 1, "D", null),
    };

    /// <summary>Por que não serve (null se serve).</summary>
    public string? WhyInvalid()
    {
        if (!double.IsFinite(Depth) || Depth <= 0 || Depth > MaxDepth) return Tr.F("a profundidade tem que ser maior que 0 e até {0} m", MaxDepth);
        if (!double.IsFinite(Radius) || Radius <= 0 || Radius > MaxRadius) return Tr.F("o raio tem que ser maior que 0 e até {0} m", MaxRadius);
        if (!double.IsFinite(Reach) || Reach <= 0 || Reach > 10 * MaxRadius) return Tr.F("o alcance tem que ser maior que 0 e até {0} m", 10 * MaxRadius);
        if (!double.IsFinite(PowerFactor) || PowerFactor <= 0 || PowerFactor > 1) return Tr.T("o fator de potência tem que ser maior que 0 e até 1");
        return null;
    }

    public IReadOnlyList<string> ToFields() =>
        [Route.ToString(), Num(Depth), Num(Radius), Num(Reach), Num(PowerFactor), Method ?? string.Empty,
         .. Cable?.ToFields() ?? Enumerable.Repeat(string.Empty, Cable.FieldCount), ThreePhase ? "3" : "1"];

    public static RouteSettings? Parse(IReadOnlyList<string> c)
    {
        if (c.Count < FieldCount || !Enum.TryParse<CableRoute>(c[0], out var rota) || !Enum.IsDefined(rota)) return null;
        if (!Real(c[1], out var d) || !Real(c[2], out var r) || !Real(c[3], out var a) || !Real(c[4], out var fp)) return null;

        var resto = c.Skip(FixedFieldCount).Take(Cable.FieldCount).ToList();
        Cable? cabo = null;
        if (resto.Any(x => x.Length > 0) && (cabo = Cable.Parse(resto)) is null) return null;

        if (c[FieldCount - 1] is not ("3" or "1")) return null;
        var s = new RouteSettings(rota, d, r, a, fp, c[5], cabo, c[FieldCount - 1] == "3");
        return s.WhyInvalid() is null ? s : null;
    }

    internal static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    internal static bool Real(string t, out double v) =>
        double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
}
