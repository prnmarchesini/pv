namespace Clivus.Core;

/// <summary>
/// Os limites da seção Cores da janela de análises, montados numa regra
/// (42.6, 31/10/2026). Fora do biposte, uma coluna, como sempre foi: a caixa
/// marcada e vazia é erro. Nas análises de pilar de um desenho com biposte,
/// uma coluna por linha de pilar ("uma coluna para cada pilar o da frente e o
/// de traz, a cor pode ser o mesmo para ambos"): a caixa vazia é a linha sem
/// limite desse lado, e é preciso pelo menos um limite. As caixas Menor que e
/// Maior que valem para todas as linhas, inclusive as que não têm coluna.
/// </summary>
public static class ThresholdForm
{
    /// <summary>
    /// As colunas da janela para a análise: nas de pilar com biposte no
    /// desenho, uma por linha que o desenho tem (a monoposte, se houver, a da
    /// frente e a de trás); fora disso, uma só (a da regra).
    /// </summary>
    public static IReadOnlyList<PillarLine> Columns(IndependentKind tipo, IReadOnlySet<PillarLine> linhasDoDesenho)
    {
        ArgumentNullException.ThrowIfNull(linhasDoDesenho);

        return IndependentAnalysis.IsPillar(tipo) && (linhasDoDesenho.Contains(PillarLine.Front) || linhasDoDesenho.Contains(PillarLine.Rear))
            ? new[] { PillarLine.Single, PillarLine.Front, PillarLine.Rear }.Where(linhasDoDesenho.Contains).ToList()
            : [PillarLine.Single];
    }

    /// <summary>A regra das caixas, ou null com o motivo em <paramref name="erro"/> (frase pronta para a janela).</summary>
    /// <param name="colunas">As linhas com coluna na janela (<see cref="Columns"/>).</param>
    /// <param name="textos">O que está escrito em cada coluna: o limite de baixo e o de cima.</param>
    /// <param name="gravada">
    /// A regra gravada quando a janela abriu: a linha sem coluna (a monoposte
    /// num desenho só de biposte) fica com o limite dela.
    /// </param>
    public static ThresholdRule? Build(
        IReadOnlyList<PillarLine> colunas,
        IReadOnlyDictionary<PillarLine, (string Below, string Above)> textos,
        bool usarAbaixo,
        bool usarAcima,
        RgbColor corAbaixo,
        RgbColor corAcima,
        bool pecas,
        ThresholdRule gravada,
        out string? erro)
    {
        ArgumentNullException.ThrowIfNull(colunas);
        ArgumentNullException.ThrowIfNull(textos);
        ArgumentNullException.ThrowIfNull(gravada);

        erro = null;
        var varias = colunas.Count > 1;
        var lidos = new Dictionary<PillarLine, LineLimits>();

        foreach (var linha in colunas)
        {
            var (textoAbaixo, textoAcima) = textos.TryGetValue(linha, out var t) ? t : ("", "");

            if (!Limite(textoAbaixo, usarAbaixo, varias, linha, Tr.T("não consigo ler o limite de baixo"), out var abaixo, out erro)) return null;
            if (!Limite(textoAcima, usarAcima, varias, linha, Tr.T("não consigo ler o limite de cima"), out var acima, out erro)) return null;

            lidos[linha] = new LineLimits(abaixo, acima);
        }

        if (varias && lidos.Values.All(l => l.Below is null && l.Above is null))
        {
            erro = Tr.T("Preencha pelo menos um limite (de uma das colunas).");
            return null;
        }

        // A linha sem coluna fica com o limite gravado, e desmarcar um lado
        // desliga esse lado nela também.
        LineLimits? Escondida(LineLimits? gravado) =>
            gravado is null ? null : new LineLimits(usarAbaixo ? gravado.Below : null, usarAcima ? gravado.Above : null);

        var monoposte = lidos.TryGetValue(PillarLine.Single, out var lida) ? lida : Escondida(new LineLimits(gravada.Below, gravada.Above))!;

        var regra = new ThresholdRule(monoposte.Below, corAbaixo, monoposte.Above, corAcima, pecas)
        {
            Front = lidos.TryGetValue(PillarLine.Front, out var frente) ? frente : Escondida(gravada.Front),
            Rear = lidos.TryGetValue(PillarLine.Rear, out var tras) ? tras : Escondida(gravada.Rear),
        };

        if (regra.WhyInvalid is { } motivo)
        {
            erro = Frase(motivo);
            return null;
        }

        return regra;
    }

    /// <summary>
    /// O limite de uma caixa: desligado, nenhum. Numa coluna só, ligado e
    /// vazio é erro; com as colunas do biposte, vazio é a linha sem limite.
    /// </summary>
    private static bool Limite(string texto, bool ligado, bool varias, PillarLine linha, string naoLeu, out double? valor, out string? erro)
    {
        valor = null;
        erro = null;
        if (!ligado || (varias && string.IsNullOrWhiteSpace(texto))) return true;

        if (!NumberInput.TryParseMeasure(texto, out var lido))
        {
            erro = varias ? Tr.F("{0}: {1}.", IndependentAnalysis.LineName(linha), naoLeu) : Frase(naoLeu);
            return false;
        }

        valor = lido;
        return true;
    }

    private static string Frase(string motivo) => char.ToUpper(motivo[0], Tr.Culture) + motivo[1..] + ".";
}
