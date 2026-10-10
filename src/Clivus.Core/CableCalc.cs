namespace Clivus.Core;

/// <summary>
/// As contas do memorial (roteamento, etapa 23). O sistema não dimensiona,
/// ele multiplica (regra 8): todos os números de entrada vêm do usuário
/// (PAN, cabo, temperaturas, método); daqui só saem números, nunca veredito.
/// </summary>
public static class CableCalc
{
    /// <summary>A temperatura de referência do PAN (STC), °C.</summary>
    public const double Stc = 25;

    /// <summary>
    /// Voc da string na temperatura mínima (23.1): a Voc do módulo corrigida
    /// pelo coeficiente até a mínima, vezes os módulos em série. No frio ela
    /// SOBE acima da nominal (coeficiente negativo, temperatura abaixo de 25 °C):
    /// é o pior caso, o que se compara com a janela do inversor.
    /// </summary>
    public static double OpenCircuitAtMin(PanModule m, int serie, double tMin) =>
        serie * (m.Voc + m.VocCoefficient * (tMin - Stc));

    /// <summary>
    /// Tensão de operação da string na temperatura máxima (23.1): a Vmp
    /// corrigida até a máxima, vezes a série (<see cref="PanModule.VmpRelativeCoefficient"/>).
    /// A temperatura é usada como está (o sistema não soma o aquecimento da célula).
    /// </summary>
    public static double OperatingAtMax(PanModule m, int serie, double tMax) =>
        serie * m.Vmp * (1 + m.VmpRelativeCoefficient * (tMax - Stc));

    /// <summary>A corrente monofásica (A) de uma potência ativa (kW) na tensão (V) com o fator de potência.</summary>
    public static double SinglePhaseCurrent(double kw, double tensao, double fatorDePotencia) =>
        kw * 1000 / (tensao * fatorDePotencia);

    /// <summary>Queda de tensão monofásica, em V: 2 × I × L × (R cos φ + X sen φ) (ida e volta).</summary>
    public static double SinglePhaseDrop(double corrente, Cable cabo, double comprimento, double fatorDePotencia) =>
        2 / Math.Sqrt(3) * ThreePhaseDrop(corrente, cabo, comprimento, fatorDePotencia);

    /// <summary>
    /// Queda de tensão no CC (23.3), em V: corrente vezes a resistência na
    /// operação vezes o comprimento do circuito (o lance + mais o −, ida e volta).
    /// No cabo em operação sempre há QUEDA; o que sobe é a Voc no frio.
    /// </summary>
    public static double DcDrop(double corrente, Cable cabo, double comprimentoPositivo, double comprimentoNegativo) =>
        corrente * cabo.ResistanceOperating * (comprimentoPositivo + comprimentoNegativo) / 1000;

    /// <summary>A corrente trifásica (A) de uma potência ativa (kW) numa tensão de linha (V) com o fator de potência.</summary>
    public static double ThreePhaseCurrent(double kw, double tensao, double fatorDePotencia) =>
        kw * 1000 / (Math.Sqrt(3) * tensao * fatorDePotencia);

    /// <summary>A corrente trifásica (A) de uma potência aparente (kVA) numa tensão de linha (V).</summary>
    public static double ThreePhaseCurrentKva(double kva, double tensao) =>
        kva * 1000 / (Math.Sqrt(3) * tensao);

    /// <summary>
    /// Queda de tensão trifásica (23.5), em V: √3 × I × L × (R cos φ + X sen φ),
    /// com R e X em ohm/km e L em m.
    /// </summary>
    public static double ThreePhaseDrop(double corrente, Cable cabo, double comprimento, double fatorDePotencia)
    {
        var seno = Math.Sqrt(Math.Max(0, 1 - fatorDePotencia * fatorDePotencia));
        return Math.Sqrt(3) * corrente * comprimento / 1000 * (cabo.ResistanceOperating * fatorDePotencia + cabo.Reactance * seno);
    }

    /// <summary>A queda em porcentagem da tensão de referência.</summary>
    public static double Percent(double queda, double tensao) => tensao > 0 ? queda / tensao * 100 : double.NaN;
}
