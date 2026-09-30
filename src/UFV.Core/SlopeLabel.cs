using System.Globalization;

namespace UFV.Core;

/// <summary>A unidade em que a declividade da mesa é escrita no desenho.</summary>
public enum SlopeUnit
{
    /// <summary>Porcentagem: desnível sobre a distância em planta, vezes cem.</summary>
    Percent,

    /// <summary>Graus: o ângulo da mesa com a horizontal.</summary>
    Degrees,
}

/// <summary>
/// O texto da análise de declividade (Renan, 29/09/2026: "quero uma flecha e
/// a indicação em % ou graus, eu decido qual unidade"). A declividade é a da
/// mesa ao longo da fileira: o desnível entre as duas pontas da borda baixa
/// sobre a distância entre elas em planta.
/// </summary>
public static class SlopeLabel
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Desnível abaixo do qual a mesa é plana e não leva seta: um milímetro.</summary>
    public const double FlatDrop = 0.001;

    /// <summary>A declividade escrita: "5,2%" ou "3,0°".</summary>
    /// <param name="drop">O desnível entre as pontas, em metro (o sinal não importa).</param>
    /// <param name="planLength">A distância entre as pontas em planta, em metro.</param>
    /// <param name="unit">A unidade.</param>
    /// <exception cref="ArgumentOutOfRangeException">Distância que não é medida, ou desnível que não é número.</exception>
    public static string Format(double drop, double planLength, SlopeUnit unit)
    {
        if (!double.IsFinite(planLength) || planLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(planLength), planLength, "A distância em planta precisa ser positiva.");

        if (!double.IsFinite(drop))
            throw new ArgumentOutOfRangeException(nameof(drop), drop, "O desnível não é um número.");

        var tangente = Math.Abs(drop) / planLength;

        return unit == SlopeUnit.Degrees
            ? $"{(Math.Atan(tangente) * 180 / Math.PI).ToString("0.0", Brasil)}°"
            : $"{(tangente * 100).ToString("0.0", Brasil)}%";
    }

    /// <summary>Se a mesa é plana (não leva seta).</summary>
    public static bool IsFlat(double drop) => Math.Abs(drop) < FlatDrop;

    /// <summary>A unidade pelo nome gravado ("PORCENTO" ou "GRAUS"); null se não for nenhum dos dois.</summary>
    public static SlopeUnit? Parse(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "PORCENTO" => SlopeUnit.Percent,
        "GRAUS" => SlopeUnit.Degrees,
        _ => null,
    };

    /// <summary>O nome gravado da unidade.</summary>
    public static string Name(SlopeUnit unit) => unit == SlopeUnit.Degrees ? "GRAUS" : "PORCENTO";
}
