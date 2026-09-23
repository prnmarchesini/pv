namespace UFV.Core;

/// <summary>
/// A conta que transforma a geometria da mesa em altura de pilar.
///
/// A fórmula do plano, no passo 3.5: "altura livre = cota da ponta baixa do
/// módulo + distância do pilar ao longo da tesoura × sen(tilt), com a ponta
/// baixa medida no MÓDULO".
///
/// A ênfase em MÓDULO é a parte que engana. O desenho do Renan mostra por quê:
/// o M1 é medido na ponta baixa do módulo, mas o T2 é contado da ponta baixa
/// da TESOURA, que é mais curta e fica centrada nos módulos. Os dois não
/// partem do mesmo lugar, e a diferença — a sobra, <c>(M2 − T1) / 2</c> — entra
/// na conta. Esquecê-la erra a altura de todo pilar da usina pelo mesmo valor,
/// que é o erro mais difícil de perceber: nada fica estranho, tudo fica errado.
///
/// Quem já tem esse número somado é <see cref="TableGeometry.PillarRow"/>, e é
/// de lá que ele deve vir.
///
/// Aqui não há limite de pilar, faixa de ponta baixa nem lista de comprimentos
/// comerciais: isso é a configuração do passo 4.1, e ter dois donos dos mesmos
/// números é como eles começam a divergir.
/// </summary>
public static class PillarSizing
{
    /// <summary>
    /// Maior inclinação aceita, em radianos: um quarto de volta.
    ///
    /// A 90° a mesa estaria em pé, e acima disso virada. Sem este limite,
    /// 120° devolvia 3,24 m de altura livre em silêncio, e 180° devolvia a
    /// própria ponta baixa — uma mesa de cabeça para baixo que o plugin
    /// enxergaria como deitada.
    /// </summary>
    public const double MaiorInclinacao = Math.PI / 2;

    /// <summary>
    /// Maiores medidas aceitas, em metro. Rede para erro de escala, como nas
    /// outras classes: quem digitar 300 achando que o campo era centímetro.
    /// </summary>
    private const double MaiorAlturaLivre = 20.0;

    private const double MaiorDistancia = 50.0;

    /// <summary>
    /// A altura livre do pilar — o P3 do desenho: o que ele tem acima do
    /// terreno.
    /// </summary>
    /// <param name="lowEdgeClearance">
    /// M1: a altura livre na ponta baixa do MÓDULO. Zero é aceito para isolar
    /// a subida na conta, mas mesa encostada no chão não é projeto — quem
    /// recusa isso é o verificador da regra sagrada 1.
    /// </param>
    /// <param name="alongSlope">
    /// A distância do pilar até a ponta baixa do módulo, medida sobre o plano
    /// inclinado: sobra + T2.
    /// </param>
    /// <param name="tiltRadians">A inclinação, em radianos, entre 0 e 90°.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se alguma entrada não for uma medida possível. Uma inclinação negativa
    /// devolvia altura livre negativa, e um pilar de comprimento negativo
    /// saía marcado como se coubesse.
    /// </exception>
    public static double FreeHeight(double lowEdgeClearance, double alongSlope, double tiltRadians)
    {
        Conferir(lowEdgeClearance, nameof(lowEdgeClearance), 0, MaiorAlturaLivre);
        Conferir(alongSlope, nameof(alongSlope), 0, MaiorDistancia);
        Conferir(tiltRadians, nameof(tiltRadians), 0, MaiorInclinacao);

        return lowEdgeClearance + alongSlope * Math.Sin(tiltRadians);
    }

    /// <summary>
    /// O pilar inteiro — o P1 do desenho: a altura livre mais o que fica
    /// enterrado.
    /// </summary>
    public static double Length(double freeHeight, double embedment)
    {
        Conferir(freeHeight, nameof(freeHeight), 0, MaiorAlturaLivre);
        Conferir(embedment, nameof(embedment), 0, MaiorAlturaLivre);

        return freeHeight + embedment;
    }

    private static void Conferir(double valor, string nome, double minimo, double maximo)
    {
        if (!double.IsFinite(valor))
            throw new ArgumentOutOfRangeException(nome, valor, "Precisa ser um número finito.");

        if (valor < minimo || valor > maximo)
        {
            throw new ArgumentOutOfRangeException(
                nome, valor, $"Precisa estar entre {minimo} e {maximo}.");
        }
    }
}
