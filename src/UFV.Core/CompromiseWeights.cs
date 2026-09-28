namespace UFV.Core;

/// <summary>
/// Os pesos da análise que posiciona a mesa que não cabe (27/09/2026). Cada
/// peso é custo por metro, somado módulo a módulo da fileira de baixo.
///
/// A ordem vem do Renan: "é melhor levantar pilar e deixar as pontas iguais
/// do que enfiar a ponta do último módulo na terra". Por isso ficar acima da
/// faixa é barato (é só pilar mais alto), ficar abaixo dela é caro, e
/// enterrar é muito caro. O degrau com a vizinha acima do máximo custa por
/// módulo da mesa, menos que o terreno: senão a mesa num buraco de 10 m
/// subiria até a cota das vizinhas, "na altura das nuvens" (26/09/2026).
/// </summary>
/// <param name="AboveBand">Metro de ponta baixa acima da faixa, por módulo.</param>
/// <param name="BelowBand">
/// Metro de ponta baixa abaixo da faixa, por módulo, VEZES o número de
/// módulos da mesa: um módulo abaixo pesa mais que subir a mesa inteira.
/// </param>
/// <param name="BelowBandEach">
/// O custo fixo de CADA módulo abaixo da faixa, em metro de mesa inteira
/// levantada (vezes o número de módulos): quantos metros de mesa vale subir
/// para tirar um módulo de baixo da faixa, além do que o metro custa.
/// </param>
/// <param name="Buried">Metro de ponta baixa ABAIXO DO CHÃO, por módulo, vezes o número de módulos, somado ao de baixo da faixa.</param>
/// <param name="StepPerModule">Metro de degrau acima do máximo, em cada junta, por módulo da mesa.</param>
/// <param name="StepReach">
/// Até quantos metros além do degrau máximo a junta ainda puxa a mesa. Além
/// disso o degrau não tem conserto (buraco, paredão) e não puxa mais nada.
/// </param>
/// <param name="Tiebreak">Metro de altura livre, só para desempatar dentro da faixa (a mais baixa).</param>
public sealed record CompromiseWeights(
    double AboveBand,
    double BelowBand,
    double BelowBandEach,
    double Buried,
    double StepPerModule,
    double StepReach,
    double Tiebreak)
{
    /// <summary>Os pesos de partida.</summary>
    public static readonly CompromiseWeights Default = new(
        AboveBand: 1,
        BelowBand: 4,
        BelowBandEach: 0.05,
        Buried: 40,
        StepPerModule: 0.35,
        StepReach: 1.0,
        Tiebreak: 1e-3);
}
