using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// Uma versão semântica (semver.org 2.0): MAIOR.MENOR.CORREÇÃO, com a
/// pré-versão opcional depois do hífen ("0.2.0-beta.1") e o build depois do
/// "+" ("0.1.0+3f2a1c9", a versão informativa que o SDK grava), que não conta
/// na comparação. A atualização (etapa 43) compara a versão publicada no
/// servidor com a instalada, e o instalador que espera o Civil 3D fechar não
/// instala por cima de uma mais nova.
/// </summary>
public sealed record SemanticVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<SemanticVersion>
{
    /// <summary>
    /// "0.1.1", "v0.1.1", "0.2.0-beta.1" ou "0.1.0+3f2a1c9"; null fora do
    /// formato (menos ou mais de três números, número com sinal ou letra,
    /// pré-versão vazia ou com caractere fora de letras, dígitos e hífen).
    /// </summary>
    public static SemanticVersion? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var s = texto.Trim();
        if (s.Length > 0 && s[0] is 'v' or 'V') s = s[1..];

        var mais = s.IndexOf('+');
        if (mais >= 0)
        {
            if (!Identificadores(s[(mais + 1)..])) return null;
            s = s[..mais];
        }

        string? pre = null;
        var hifen = s.IndexOf('-');
        if (hifen >= 0)
        {
            pre = s[(hifen + 1)..];
            if (!Identificadores(pre)) return null;
            s = s[..hifen];
        }

        var partes = s.Split('.');
        if (partes.Length != 3) return null;

        var numeros = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (partes[i].Length is 0 or > 9 || !partes[i].All(char.IsAsciiDigit)) return null;
            numeros[i] = int.Parse(partes[i], CultureInfo.InvariantCulture);
        }

        return new SemanticVersion(numeros[0], numeros[1], numeros[2], pre);
    }

    /// <summary>
    /// Se <paramref name="disponivel"/> é mais nova que <paramref name="instalada"/>.
    /// Qualquer uma fora do formato: não (melhor não oferecer do que oferecer
    /// sem saber o quê).
    /// </summary>
    public static bool IsNewer(string? disponivel, string? instalada) =>
        Parse(disponivel) is { } nova && Parse(instalada) is { } atual && nova.CompareTo(atual) > 0;

    /// <summary>A ordem do semver: os três números; a pré-versão vem antes da versão sem ela.</summary>
    public int CompareTo(SemanticVersion? outra)
    {
        if (outra is null) return 1;

        var c = Major.CompareTo(outra.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(outra.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(outra.Patch);
        if (c != 0) return c;

        if (PreRelease is null) return outra.PreRelease is null ? 0 : 1;
        if (outra.PreRelease is null) return -1;

        var a = PreRelease.Split('.');
        var b = outra.PreRelease.Split('.');

        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aNumero = a[i].All(char.IsAsciiDigit);
            var bNumero = b[i].All(char.IsAsciiDigit);

            c = (aNumero, bNumero) switch
            {
                // Número contra número: pelo valor (sem estourar com muitos dígitos).
                (true, true) => a[i].TrimStart('0').Length != b[i].TrimStart('0').Length
                    ? a[i].TrimStart('0').Length.CompareTo(b[i].TrimStart('0').Length)
                    : string.CompareOrdinal(a[i].TrimStart('0'), b[i].TrimStart('0')),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };

            if (c != 0) return Math.Sign(c);
        }

        return a.Length.CompareTo(b.Length);
    }

    /// <summary>"0.1.1" ou "0.2.0-beta.1" (sem o build).</summary>
    public override string ToString() =>
        PreRelease is null
            ? string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}")
            : string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}-{PreRelease}");

    /// <summary>Identificadores separados por ponto, cada um com letras, dígitos ou hífen, nenhum vazio.</summary>
    private static bool Identificadores(string texto) =>
        texto.Length > 0 && texto.Split('.').All(p => p.Length > 0 && p.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
}
