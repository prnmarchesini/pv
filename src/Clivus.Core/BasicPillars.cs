namespace Clivus.Core;

/// <summary>
/// Os pilares de uma mesa na altura básica: no chão plano, com a ponta baixa
/// na mínima do projeto (<see cref="TableProfile.PillarsAtBasicHeight"/>).
/// Os nomes são os do Renan para o biposte: P1 é o pilar menor (o dianteiro)
/// e P2 o maior (o traseiro), os dois inteiros, com o enterro.
/// </summary>
/// <param name="LowEdge">A ponta baixa usada: a mínima do projeto.</param>
/// <param name="Embedment">O enterro: o T3 da mesa, ou o mínimo do projeto.</param>
/// <param name="FrontFree">O que o P1 tem acima do chão.</param>
/// <param name="RearFree">O que o P2 tem acima do chão; null na monoposte.</param>
public sealed record BasicPillars(double LowEdge, double Embedment, double FrontFree, double? RearFree)
{
    /// <summary>P1: o pilar dianteiro inteiro (o único, na monoposte).</summary>
    public double P1 => FrontFree + Embedment;

    /// <summary>P2: o pilar traseiro inteiro; null na monoposte.</summary>
    public double? P2 => RearFree + Embedment;
}
