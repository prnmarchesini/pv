namespace Clivus.Core;

/// <summary>
/// Quem leva a PB e a PA de cada estação (passo 38.3, revisão).
///
/// A PB e a PA são da estação, e não do pilar: na bifila os dois pilares do
/// pórtico têm as mesmas. Cotar ou contar as duas escrevia o texto duas vezes
/// e dobrava a quantificação. Mas também não basta "o dianteiro": se ele tem
/// problema (pé fora do terreno, enterrado) e o traseiro não, o pórtico ficava
/// sem PB e sem PA. A regra é uma só, e mora aqui para o desenho, as Alturas e
/// as Análises não divergirem: em cada estação, o primeiro pilar sem problema;
/// se todos têm, o primeiro.
/// </summary>
public static class PillarStations
{
    /// <summary>
    /// Os índices (na lista dada) dos pilares que levam a PB e a PA. Na
    /// monoposte, um pilar por estação: todos.
    /// </summary>
    /// <param name="pillars">A estação e se o pilar está sem problema, na ordem dos pilares.</param>
    public static IReadOnlySet<int> EdgeCarriers(IReadOnlyList<(double Station, bool Sound)> pillars)
    {
        ArgumentNullException.ThrowIfNull(pillars);

        var escolhidos = new Dictionary<double, int>();

        for (var i = 0; i < pillars.Count; i++)
        {
            var (estacao, sao) = pillars[i];

            if (!escolhidos.TryGetValue(estacao, out var atual))
                escolhidos[estacao] = i;
            else if (sao && !pillars[atual].Sound)
                escolhidos[estacao] = i;
        }

        return escolhidos.Values.ToHashSet();
    }

    /// <summary>
    /// O pilar da primeira (ou da última) estação que tem a PB: na bifila, o
    /// dianteiro sem terreno embaixo não deixa a ponta sem PB se o traseiro
    /// tem (revisão do 38.3). Sem nenhum com PB, o primeiro da estação.
    /// </summary>
    /// <exception cref="ArgumentException">Sem pilar nenhum.</exception>
    public static PillarResult EndCarrier(IReadOnlyList<PillarResult> pillars, bool first)
    {
        ArgumentNullException.ThrowIfNull(pillars);
        if (pillars.Count == 0) throw new ArgumentException("A mesa não tem pilar.", nameof(pillars));

        var estacao = first ? pillars.Min(p => p.Station) : pillars.Max(p => p.Station);
        var naEstacao = pillars.Where(p => p.Station == estacao).ToList();

        return naEstacao.FirstOrDefault(p => p.LowEdgeClearance is not null) ?? naEstacao[0];
    }
}
