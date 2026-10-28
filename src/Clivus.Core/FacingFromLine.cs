namespace Clivus.Core;

/// <summary>
/// O azimute de para onde a mesa olha, tirado de uma linha de apoio desenhada
/// pelo projetista (Renan, 28/10/2026: <i>"eu quero um botao que libera eu fazer
/// uma linha e essa linha define o azimute ... ela serve somente para tirar o
/// azimute, então, eu faço e ela já some"</i>).
///
/// A linha é a direção das fileiras; a mesa olha para uma das duas
/// perpendiculares. Fica a que está mais perto do azimute atual (no Brasil, o
/// norte): uma linha horizontal dá 0°; a mesma linha subindo 20° para a direita
/// dá 340°, isto é, 20° para oeste.
///
/// Convenção do <see cref="SystemConfiguration.FacingAzimuthRadians"/>: do
/// norte, no sentido horário, de 0 a 2π; o norte é o +Y do desenho.
/// </summary>
public static class FacingFromLine
{
    /// <summary>
    /// O azimute (radianos, de 0 a 2π) da perpendicular à linha mais perto de
    /// <paramref name="atualRadianos"/>, ou null se os dois pontos coincidem.
    /// </summary>
    public static double? Azimuth(double x1, double y1, double x2, double y2, double atualRadianos = 0)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || Math.Sqrt(dx * dx + dy * dy) < 1e-9) return null;

        // As duas perpendiculares: (dy, -dx) e (-dy, dx). Azimute de (ex, ny) = atan2(ex, ny).
        var um = Normalizar(Math.Atan2(dy, -dx));
        var outro = Normalizar(um + Math.PI);
        var atual = double.IsFinite(atualRadianos) ? Normalizar(atualRadianos) : 0;

        // Arredonda a um milésimo de grau: "0,000000001°" não é o que o projetista quis.
        var escolhido = Diferenca(um, atual) <= Diferenca(outro, atual) ? um : outro;
        var graus = Math.Round(escolhido * 180 / Math.PI, 3);
        return Normalizar(graus * Math.PI / 180);
    }

    /// <summary>O azimute em palavras: "norte", "20° para oeste do norte", "sul".</summary>
    public static string Describe(double graus)
    {
        if (!double.IsFinite(graus)) return string.Empty;

        var g = ((graus % 360) + 360) % 360;
        if (g < 0.0005 || g > 359.9995) return Tr.T("a mesa olha para o norte");
        if (Math.Abs(g - 180) < 0.0005) return Tr.T("a mesa olha para o sul");

        return g < 180
            ? (g <= 90 ? Tr.F("a mesa olha {0:0.###}° para leste do norte", g) : Tr.F("a mesa olha {0:0.###}° para leste do sul", 180 - g))
            : (g >= 270 ? Tr.F("a mesa olha {0:0.###}° para oeste do norte", 360 - g) : Tr.F("a mesa olha {0:0.###}° para oeste do sul", g - 180));
    }

    private static double Normalizar(double a)
    {
        var dois = 2 * Math.PI;
        var r = a % dois;
        if (r < 0) r += dois;
        return r >= dois - 1e-12 ? 0 : r;
    }

    private static double Diferenca(double a, double b)
    {
        var d = Math.Abs(a - b) % (2 * Math.PI);
        return Math.Min(d, 2 * Math.PI - d);
    }
}
