namespace Clivus.Core;

/// <summary>
/// A altura da estrutura pelo P1 (38.9; Renan, 30/10/2026: "porque raios eu
/// não consigo alterar as alturas? A altura está no print, 3059 P1 e 3888
/// P2"). O biposte com P1 traz a altura dele: no chão plano, com o enterro
/// T3, o pilar dianteiro de P1 põe a ponta baixa dos módulos num lugar só, e
/// essa é a ponta baixa mínima das mesas dele na usina
/// (<see cref="SystemConfiguration.ForStructure"/>). Sem P1, a altura vem da
/// faixa do projeto, como antes.
///
/// É a conta de <see cref="TableProfile.PillarsOnFlatGround"/> ao contrário:
/// a parte livre do dianteiro é a face dos módulos no eixo dele (a ponta
/// baixa mais <c>PillarRow · sen(inclinação)</c>) menos o que o topo fica
/// abaixo dos módulos. Os testes conferem a volta.
/// </summary>
public static class StructureHeight
{
    /// <summary>
    /// A ponta baixa que o P1 dá no chão plano, ou null quando a estrutura
    /// não tem P1.
    /// </summary>
    /// <param name="geometry">A mesa em coordenadas locais, com a estrutura.</param>
    /// <param name="tiltRadians">A inclinação da mesa.</param>
    /// <param name="fallbackEmbedment">O enterro quando a estrutura não tem T3 (o do projeto).</param>
    public static double? LowEdge(TableGeometry geometry, double tiltRadians, double? fallbackEmbedment = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.Frame?.Bifila?.FrontPillar is not { } p1) return null;
        if ((geometry.Frame.MinEmbedment ?? fallbackEmbedment) is not { } enterro) return null;

        return p1 - enterro + geometry.PillarTopDrop - geometry.PillarRow * Math.Sin(tiltRadians);
    }

    /// <summary>
    /// O P1 que dá esta ponta baixa no chão plano, com este enterro: a conta de
    /// <see cref="LowEdge"/> ao contrário. É o que a altura livre digitada vira
    /// (38.9; Renan, 30/10/2026: "não consigo chegar nesses 80cm").
    /// </summary>
    /// <param name="geometry">A mesa em coordenadas locais.</param>
    /// <param name="tiltRadians">A inclinação da mesa.</param>
    /// <param name="lowEdge">A ponta baixa: a face de cima do módulo na ponta baixa, acima do chão.</param>
    /// <param name="embedment">O enterro T3.</param>
    public static double FrontPillarFor(TableGeometry geometry, double tiltRadians, double lowEdge, double embedment)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return lowEdge + embedment - geometry.PillarTopDrop + geometry.PillarRow * Math.Sin(tiltRadians);
    }

    /// <summary>
    /// O P2 que o chão plano pede com o P1 da estrutura (sem folga nenhuma), ou
    /// null sem P1. O P2 da estrutura menos este é a folga para o chão caindo
    /// para trás.
    /// </summary>
    public static double? FlatRearPillar(TableGeometry geometry, double tiltRadians, double? fallbackEmbedment = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.Frame?.Bifila?.FrontPillar is not { } p1 || geometry.RearPillarRow is not { } tras) return null;
        if (LowEdge(geometry, tiltRadians, fallbackEmbedment) is null) return null;

        // Os dois topos ficam à mesma distância da face dos módulos: a
        // diferença entre os pilares é a subida da face entre os dois eixos.
        return p1 + (tras - geometry.PillarRow) * Math.Sin(tiltRadians);
    }

    /// <summary>
    /// O declive do chão caindo para trás (fração) que a folga do P2 cobre,
    /// como o fabricante cota: a folga sobre a distância entre os pilares em
    /// planta, girando no pé do P1 (os 10 % da GC800). Na usina o motor segura
    /// a ponta baixa na borda do módulo, mais à frente que o P1, e a mesma
    /// folga cobre um pouco menos (revisão do 38.9). Zero sem folga; null sem P1.
    /// </summary>
    public static double? RearSlopeAllowance(TableGeometry geometry, double tiltRadians, double? fallbackEmbedment = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (FlatRearPillar(geometry, tiltRadians, fallbackEmbedment) is not { } plano
            || geometry.Frame?.Bifila?.RearPillar is not { } p2
            || geometry.RearPillarRow is not { } tras)
        {
            return null;
        }

        var emPlanta = (tras - geometry.PillarRow) * Math.Cos(tiltRadians);
        return emPlanta > 0 ? Math.Max(0, p2 - plano) / emPlanta : null;
    }
}
