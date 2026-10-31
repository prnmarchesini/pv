namespace Clivus.Core;

/// <summary>
/// Os pilares de uma mesa no chão plano (<see cref="TableProfile.PillarsAtBasicHeight"/>
/// e <see cref="TableProfile.PillarsOnFlatGround"/>). Os nomes são os do
/// Renan para o biposte: P1 é o pilar menor (o dianteiro) e P2 o maior (o
/// traseiro), os dois inteiros, com o enterro.
/// </summary>
/// <param name="LowEdge">A ponta baixa usada: na altura básica, a mínima do projeto na grade de cotas.</param>
/// <param name="Embedment">O enterro do P1 (o nominal da mesa desde o 38.12; o mínimo dela, ou o do projeto, sem nominal).</param>
/// <param name="FrontFree">O que o P1 tem acima do chão.</param>
/// <param name="RearFree">O que o P2 tem acima do chão; null na monoposte.</param>
/// <param name="RearEmbedment">O enterro nominal do P2 (38.12: cada linha tem o seu); null, o do P1.</param>
public sealed record BasicPillars(double LowEdge, double Embedment, double FrontFree, double? RearFree, double? RearEmbedment = null)
{
    /// <summary>P1: o pilar dianteiro inteiro (o único, na monoposte).</summary>
    public double P1 => FrontFree + Embedment;

    /// <summary>P2: o pilar traseiro inteiro; null na monoposte.</summary>
    public double? P2 => RearFree + (RearEmbedment ?? Embedment);
}
