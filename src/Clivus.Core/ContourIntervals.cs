namespace Clivus.Core;

/// <summary>
/// O intervalo das curvas de nível da superfície que o Clivus cria (28/10/2026:
/// a área aberta do TermoVision chegava sem curvas). A curva secundária é a
/// menor da lista que dá no máximo 20 curvas no desnível do terreno; a mestra,
/// o par dela. Um estacionamento de 3,7 m de desnível sai com 0,2 m e 1 m.
/// </summary>
public static class ContourIntervals
{
    private static readonly (double Secundaria, double Mestra)[] Pares =
    [
        (0.1, 0.5), (0.2, 1.0), (0.25, 1.0), (0.5, 2.5), (1.0, 5.0), (2.0, 10.0), (5.0, 25.0), (10.0, 50.0),
    ];

    /// <summary>Quantas curvas secundárias no máximo no desnível do terreno.</summary>
    public const int MaiorNumeroDeCurvas = 20;

    /// <summary>O par (secundária, mestra) em metro para este desnível.</summary>
    public static (double Minor, double Major) For(double desnivel)
    {
        if (!double.IsFinite(desnivel) || desnivel <= 0) return Pares[2];

        foreach (var (secundaria, mestra) in Pares)
            if (desnivel / secundaria <= MaiorNumeroDeCurvas) return (secundaria, mestra);

        return Pares[^1];
    }
}
