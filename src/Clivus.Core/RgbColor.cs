using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// Uma cor, em vermelho, verde e azul de 0 a 255.
///
/// Mora no Core, e não usa a cor do AutoCAD, porque as regras de análise são
/// do motor e o motor não sabe que o AutoCAD existe. O plugin converte na
/// hora de pintar (<c>Color.FromRgb</c>, na etapa 5), e essa deve ser a
/// única tradução: cor não se converte em nenhum outro lugar.
///
/// Guardar RGB, e não índice ACI, é de propósito: o índice muda de aparência
/// conforme o fundo da tela e a tabela do desenho, e "vermelho para abaixo do
/// limite" precisa ser o mesmo vermelho em qualquer máquina.
/// </summary>
/// <param name="R">Vermelho, 0 a 255.</param>
/// <param name="G">Verde, 0 a 255.</param>
/// <param name="B">Azul, 0 a 255.</param>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>O vermelho puro: padrão para valor abaixo do limite.</summary>
    public static readonly RgbColor Red = new(255, 0, 0);

    /// <summary>O azul puro: padrão para valor acima do limite.</summary>
    public static readonly RgbColor Blue = new(0, 0, 255);

    /// <summary>
    /// O magenta: padrão da mesa na borda da área, que não é abaixo nem acima
    /// de nada e precisa se distinguir dos outros dois à primeira vista.
    /// </summary>
    public static readonly RgbColor Magenta = new(255, 0, 255);

    /// <summary>A cor como texto, no formato <c>#RRGGBB</c>.</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>
    /// Lê <c>#RRGGBB</c>, com ou sem a cerquilha, em qualquer caixa. É o
    /// formato em que a cor será gravada e lida de volta.
    ///
    /// Só o especificador hexadecimal, e não <c>HexNumber</c>: este aceita
    /// espaço em volta de cada par, e "#FF 000" seria lido como vermelho.
    /// </summary>
    public static bool TryParseHex(string? texto, out RgbColor cor)
    {
        cor = default;

        var limpo = texto?.Trim() ?? string.Empty;
        if (limpo.StartsWith('#')) limpo = limpo[1..];

        if (limpo.Length != 6) return false;

        if (!byte.TryParse(limpo[..2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(limpo[2..4], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(limpo[4..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        cor = new RgbColor(r, g, b);
        return true;
    }

    public override string ToString() => ToHex();
}
