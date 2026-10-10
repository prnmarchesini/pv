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

/// <summary>
/// As contas por string do resumo CC (Renan, 10/10/2026, item 16; "por
/// enquanto não vamos considerar fatores de agrupamento"): a Voc da string
/// na temperatura mínima, a Vmp e as correntes do módulo (Isc e Imp, as da
/// string em série), a capacidade do cabo no método da aba e se ele suporta.
/// </summary>
/// <remarks>
/// O critério de "suporta" é o simples pedido para este teste:
/// Isc × <see cref="SafetyFactor"/> (1,25) ≤ capacidade de condução do cabo
/// no método de instalação da aba (da biblioteca de cabos), sem fator de
/// agrupamento nem de temperatura. É a exceção pedida pelo Renan à regra 8
/// (o sistema não aprova cabo): a parte 2, com os fatores da norma, vem depois.
/// Sem capacidade para o método, a coluna fica vazia (null), nunca "Não".
/// </remarks>
public sealed record StringCheck(double VocAtMin, double Vmp, double Isc, double Imp, double? Ampacity)
{
    /// <summary>O fator sobre a Isc do critério simples (item 16).</summary>
    public const double SafetyFactor = 1.25;

    /// <summary>A corrente de projeto: Isc × 1,25.</summary>
    public double DesignCurrent => Isc * SafetyFactor;

    /// <summary>Se o cabo suporta (Isc × 1,25 ≤ capacidade); null sem a capacidade.</summary>
    public bool? Supports => Ampacity is { } a ? DesignCurrent <= a + 1e-9 : null;

    /// <summary>
    /// As contas de uma string: <paramref name="modulo"/> é o do PAN (null:
    /// sem conta, as colunas ficam vazias), <paramref name="serie"/> os módulos
    /// em série, <paramref name="tMin"/> a mínima de Configurações (°C).
    /// </summary>
    public static StringCheck? For(PanModule? modulo, int serie, double tMin, Cable? cabo, string? metodo)
    {
        if (modulo is null || serie <= 0) return null;
        return new StringCheck(CableCalc.OpenCircuitAtMin(modulo, serie, tMin), serie * modulo.Vmp, modulo.Isc, modulo.Imp, cabo?.AmpacityFor(metodo));
    }

    /// <summary>Os títulos das colunas, na ordem de <see cref="Cells"/>.</summary>
    public static IReadOnlyList<string> Headers() =>
    [
        Tr.T("Voc na mínima (V)"), Tr.T("Vmp, Vmppt (V)"), Tr.T("Isc (A)"), Tr.T("Imp, Imppt (A)"),
        Tr.T("Capacidade do cabo (A)"), Tr.T("Suporta (Isc × 1,25)"),
    ];

    /// <summary>As células, na ordem de <see cref="Headers"/>.</summary>
    public IReadOnlyList<object?> Cells() =>
        [VocAtMin, Vmp, Isc, Imp, Ampacity, Supports is { } s ? (s ? Tr.T("Sim") : Tr.T("Não")) : null];
}
