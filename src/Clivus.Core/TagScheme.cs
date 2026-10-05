using System.Globalization;
using System.Text;

namespace Clivus.Core;

/// <summary>
/// A composição da tag das strings (15.1), num modelo de texto livre
/// (05/10/2026, pedido do Renan: "no PVcase ele me dava uma caixa de texto com
/// os campos"). Ex.: <c>T{T}-INV{I}S{S}</c> dá T1-INV1S1.
/// <para>
/// Campos: {T} o número do trafo, {I} o do inversor, {S} o da string. Com
/// zeros à esquerda: {I:00} dá 01, {S:000} dá 001 (o número maior que a
/// largura sai inteiro). Tudo fora das chaves é texto fixo (letras, risquinho,
/// ponto, espaço ou nada).
/// </para>
/// <para>
/// O número do trafo e o do inversor são a posição deles na lista do cadastro
/// (1, 2, 3...); o da string é o sequencial dentro do inversor, que recomeça
/// do 1 em cada inversor (regra elétrica 8).
/// </para>
/// <para>
/// Inversor sem trafo: o "pedaço" do trafo some. Um pedaço é um campo com o
/// texto fixo logo antes dele, desde o campo anterior (ou desde o começo).
/// Se o pedaço que some é o primeiro, a pontuação que começava o pedaço
/// seguinte (ponto, risquinho, espaço...) some junto, para a tag não começar
/// com separador. Ex.: T{T}-INV{I}S{S} sem trafo dá INV3S1; T{T}.I{I}.S{S}
/// dá I3.S1 (o que a composição antiga dava).
/// </para>
/// <para>
/// Fundo e moldura (05/10/2026): o texto desenhado ganha a máscara que
/// esconde o que está atrás (<see cref="Background"/>) e/ou o quadro em
/// volta (<see cref="Border"/>). Não mudam o texto da tag.
/// </para>
/// </summary>
public sealed record TagScheme(string Template, bool Background = false, bool Border = false)
{
    /// <summary>O formato do registro NUMERACAO: 3 campos (modelo, fundo, moldura).</summary>
    public const int Version = 2;

    public const int FieldCount = 3;

    /// <summary>O formato 1 (até 05/10/2026): os 5 campos da <see cref="LegacyTagScheme"/>.</summary>
    public const int LegacyVersion = 1;

    public const int LegacyFieldCount = LegacyTagScheme.FieldCount;

    /// <summary>O maior modelo aceito, em caracteres.</summary>
    public const int MaxLength = 60;

    /// <summary>O máximo de zeros de um campo ({S:000000}); o mínimo é 2 ({I:0} não muda nada).</summary>
    public const int MaxZeros = 6;

    /// <summary>T1.I1.S1.</summary>
    public static TagScheme Default { get; } = new("T{T}.I{I}.S{S}");

    /// <summary>Os campos e o texto que cada botão "+" insere.</summary>
    public const string TransformerField = "{T}";

    public const string InverterField = "{I}";

    public const string StringField = "{S}";

    /// <summary>
    /// O que impede o modelo de valer, ou null. <paramref name="inverters"/>
    /// é quantos inversores há no cadastro: com mais de um, {I} é obrigatório.
    /// </summary>
    public string? Problem(int inverters = 1)
    {
        if (Tokens(Template, out var problema) is not { } tokens) return problema;

        var campos = tokens.Where(t => t.Field != '\0').ToList();
        if (!campos.Any(c => c.Field == 'S')) return Tr.T("falta {S}, o número da string: sem ele as tags se repetem");
        if (inverters > 1 && !campos.Any(c => c.Field == 'I'))
            return Tr.F("falta {{I}}, o número do inversor: com {0} inversores as tags se repetem", inverters);

        return null;
    }

    /// <summary>Se o modelo tem o campo do trafo.</summary>
    public bool HasTransformer => Tokens(Template, out _) is { } t && t.Any(x => x.Field == 'T');

    /// <summary>
    /// A tag. <paramref name="transformer"/> null é inversor sem trafo: o
    /// pedaço do trafo some (veja a classe), em vez de um "T0" que parece um
    /// trafo de verdade. Números começam do 1.
    /// </summary>
    public string Compose(int? transformer, int inverter, int str)
    {
        if (transformer is < 1) throw new ArgumentOutOfRangeException(nameof(transformer), transformer, "O número do trafo começa do 1.");
        ArgumentOutOfRangeException.ThrowIfLessThan(inverter, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(str, 1);

        if (Tokens(Template, out var problema) is not { } tokens) throw new InvalidOperationException(problema);

        // Os pedaços: o texto antes de cada campo e o campo; o rabo é o texto depois do último.
        var pedacos = new List<(string Texto, Token Campo)>();
        var texto = new StringBuilder();
        foreach (var t in tokens)
        {
            if (t.Field == '\0')
            {
                texto.Append(t.Text);
                continue;
            }

            pedacos.Add((texto.ToString(), t));
            texto.Clear();
        }

        var rabo = texto.ToString();
        var saida = new StringBuilder();
        var tirouOPrimeiro = false;

        for (var i = 0; i < pedacos.Count; i++)
        {
            var (antes, campo) = pedacos[i];
            int? valor = campo.Field switch
            {
                'T' => transformer,
                'I' => inverter,
                _ => str,
            };

            if (valor is not { } v)
            {
                if (saida.Length == 0) tirouOPrimeiro = true;
                continue;
            }

            if (tirouOPrimeiro && saida.Length == 0) antes = SemPontuacaoNoComeco(antes);
            saida.Append(antes);
            saida.Append(v.ToString(campo.Zeros > 0 ? new string('0', campo.Zeros) : "0", CultureInfo.InvariantCulture));
        }

        saida.Append(rabo);
        return saida.ToString();
    }

    public IReadOnlyList<string> ToFields() => [Template ?? string.Empty, Background ? "1" : "0", Border ? "1" : "0"];

    /// <summary>O formato 2. Modelo que não vale (para um inversor) é recusado.</summary>
    public static TagScheme? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Count < FieldCount || c[1] is not ("0" or "1") || c[2] is not ("0" or "1")) return null;

        var esquema = new TagScheme(c[0], c[1] == "1", c[2] == "1");
        return esquema.Problem() is null ? esquema : null;
    }

    /// <summary>O formato 1 (os 5 campos de antes), convertido no modelo que dá as mesmas tags, sem fundo nem moldura.</summary>
    public static TagScheme? ParseLegacy(IReadOnlyList<string> c) =>
        LegacyTagScheme.Parse(c) is { } antiga ? new TagScheme(antiga.ToTemplate()) : null;

    private static string SemPontuacaoNoComeco(string texto)
    {
        var i = 0;
        while (i < texto.Length && !char.IsLetterOrDigit(texto[i])) i++;
        return texto[i..];
    }

    /// <summary>Um pedaço do modelo: texto fixo (<see cref="Field"/> '\0') ou campo (T, I, S) com os zeros.</summary>
    private readonly record struct Token(string Text, char Field, int Zeros);

    /// <summary>O modelo em pedaços, ou null e o porquê.</summary>
    private static List<Token>? Tokens(string? modelo, out string? problema)
    {
        problema = null;

        if (string.IsNullOrEmpty(modelo))
        {
            problema = Tr.T("o modelo da tag está vazio: escreva, por exemplo, T{T}.I{I}.S{S}");
            return null;
        }

        if (modelo.Length > MaxLength)
        {
            problema = Tr.F("o modelo passa de {0} caracteres", MaxLength);
            return null;
        }

        if (char.IsWhiteSpace(modelo[0]) || char.IsWhiteSpace(modelo[^1]))
        {
            problema = Tr.T("o modelo não pode começar nem terminar com espaço");
            return null;
        }

        // \ e % mudam o texto no CAD (\P, %%d); | é reservado.
        if (modelo.FirstOrDefault(c => char.IsControl(c) || c is '\\' or '|' or '%') is var proibido and not '\0')
        {
            problema = Tr.F("o caractere \"{0}\" não pode ir na tag", char.IsControl(proibido) ? " " : proibido.ToString());
            return null;
        }

        var tokens = new List<Token>();
        var texto = new StringBuilder();
        var i = 0;

        while (i < modelo.Length)
        {
            var c = modelo[i];

            if (c == '}')
            {
                problema = Tr.T("tem um } sem o { antes");
                return null;
            }

            if (c != '{')
            {
                texto.Append(c);
                i++;
                continue;
            }

            var fim = modelo.IndexOf('}', i + 1);
            var aberto = modelo.IndexOf('{', i + 1);
            if (fim < 0 || (aberto >= 0 && aberto < fim))
            {
                problema = Tr.T("tem um { sem o } depois");
                return null;
            }

            var chave = modelo.Substring(i + 1, fim - i - 1);
            if (Campo(chave) is not { } campo)
            {
                problema = Tr.F("campo {{{0}}} desconhecido: use {{T}}, {{I}} ou {{S}} (com zeros à esquerda, ex. {{I:00}})", chave);
                return null;
            }

            if (tokens.Any(t => t.Field == campo.Field))
            {
                problema = Tr.F("o campo {{{0}}} aparece duas vezes", campo.Field);
                return null;
            }

            var anterior = tokens.LastOrDefault(t => t.Field != '\0');
            if (anterior.Field != '\0' && anterior.Zeros == 0 && texto.ToString().All(char.IsDigit))
            {
                problema = Tr.F("entre {{{0}}} e {{{1}}} precisa de uma letra ou separador (senão 12 pode ser 1 e 2 ou 12), ou use zeros, ex. {{{0}:00}}", anterior.Field, campo.Field);
                return null;
            }

            if (texto.Length > 0) tokens.Add(new Token(texto.ToString(), '\0', 0));
            texto.Clear();
            tokens.Add(campo);
            i = fim + 1;
        }

        if (texto.Length > 0) tokens.Add(new Token(texto.ToString(), '\0', 0));
        return tokens;
    }

    /// <summary>"T", "i", "S:000" etc.; null se não é campo.</summary>
    private static Token? Campo(string chave)
    {
        var partes = chave.Split(':');
        if (partes.Length > 2 || partes[0].Length != 1) return null;

        var letra = char.ToUpperInvariant(partes[0][0]);
        if (letra is not ('T' or 'I' or 'S')) return null;

        var zeros = 0;
        if (partes.Length == 2)
        {
            if (partes[1].Length is < 2 or > MaxZeros || partes[1].Any(c => c != '0')) return null;
            zeros = partes[1].Length;
        }

        return new Token(string.Empty, letra, zeros);
    }
}

/// <summary>
/// A composição da tag de antes (formato 1 do registro NUMERACAO, até
/// 05/10/2026): três pedaços, trafo (opcional), inversor e string, cada um com
/// o seu prefixo, e um separador ("." , "-" ou nada) entre eles. Só é lida, e
/// vira o modelo livre equivalente (<see cref="ToTemplate"/>): o desenho
/// antigo continua gerando as mesmas tags.
/// </summary>
public sealed record LegacyTagScheme(bool IncludeTransformer, string TransformerPrefix, string InverterPrefix, string StringPrefix, string Separator)
{
    public const int FieldCount = 5;

    /// <summary>O maior prefixo aceito, em caracteres.</summary>
    public const int MaxPrefixLength = 12;

    /// <summary>T1.I1.S1.</summary>
    public static LegacyTagScheme Default { get; } = new(true, "T", "I", "S", ".");

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
            if (prefixo.Any(c => char.IsControl(c) || c is '\\' or '{' or '}' or '|' or '%')) return Tr.F("o prefixo \"{0}\" tem caractere que não pode ir na tag", prefixo);
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

    /// <summary>A tag como a composição antiga dava (a referência da conversão).</summary>
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

    /// <summary>
    /// O modelo livre que dá as mesmas tags: Trafo T, Inversor I, String S,
    /// ponto → T{T}.I{I}.S{S}; sem o trafo → I{I}.S{S}.
    /// </summary>
    public string ToTemplate()
    {
        var pedacos = new List<string>(3);
        if (IncludeTransformer) pedacos.Add(TransformerPrefix + TagScheme.TransformerField);
        pedacos.Add(InverterPrefix + TagScheme.InverterField);
        pedacos.Add(StringPrefix + TagScheme.StringField);
        return string.Join(Separator, pedacos);
    }

    public IReadOnlyList<string> ToFields() => [IncludeTransformer ? "1" : "0", TransformerPrefix, InverterPrefix, StringPrefix, Separator];

    public static LegacyTagScheme? Parse(IReadOnlyList<string> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Count < FieldCount || c[0] is not ("0" or "1")) return null;

        var esquema = new LegacyTagScheme(c[0] == "1", c[1], c[2], c[3], c[4]);
        return esquema.Problem() is null ? esquema : null;
    }
}
