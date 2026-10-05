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
            if (prefixo.Any(c => char.IsControl(c) || c is '\\' or '{' or '}' or '|')) return Tr.F("o prefixo \"{0}\" tem caractere que não pode ir na tag", prefixo);
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
