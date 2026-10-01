namespace UFV.Core;

/// <summary>Um pilar para o quantitativo: de qual mesa, e as medidas.</summary>
/// <param name="Table">O letreiro da mesa (F1.2).</param>
/// <param name="Number">O número do pilar na mesa.</param>
/// <param name="Embedded">O que fica enterrado, em metro.</param>
/// <param name="Above">O que fica acima do terreno (P3), em metro; null sem terreno.</param>
/// <param name="Length">O comprimento total, em metro; null quando o pilar tem problema.</param>
public sealed record QuantityPillar(string Table, int Number, double Embedded, double? Above, double? Length);

/// <summary>
/// O relatório do Excel (passo 8.12, Melhorias.docx, 01/10/2026): "essas
/// quantidades, no final, quero poder exportar tudo em um excel, onde o passo
/// 2 é o sistema exportar a quantidade de módulos, mesas, pilares com o
/// tamanho total (enterrado + superior)".
///
/// Três abas: Resumo (mesas, módulos, kWp, pilares, metros de pilar),
/// Análises (a última quantificação de cada análise) e Pilares (um por
/// linha, com enterrado, acima e total).
/// </summary>
public static class QuantityReport
{
    private static readonly System.Globalization.CultureInfo Brasil = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A planilha.</summary>
    /// <param name="mesas">Quantas mesas.</param>
    /// <param name="modulos">Quantos módulos.</param>
    /// <param name="kwp">A potência instalada, em kWp.</param>
    /// <param name="pilares">Os pilares.</param>
    /// <param name="quantificacoes">A última quantificação de cada análise que foi feita.</param>
    public static XlsxWriter Build(int mesas, int modulos, double kwp, IReadOnlyList<QuantityPillar> pilares, IReadOnlyList<AnalysisTally> quantificacoes)
    {
        ArgumentNullException.ThrowIfNull(pilares);
        ArgumentNullException.ThrowIfNull(quantificacoes);

        var planilha = new XlsxWriter();

        var resumo = planilha.Sheet("Resumo");
        resumo.Add(["Item", "Quantidade", "Unidade"]);
        resumo.Add(["Mesas", mesas, "un"]);
        resumo.Add(["Módulos", modulos, "un"]);
        resumo.Add(["Potência", Math.Round(kwp, 3), "kWp"]);
        resumo.Add(["Pilares", pilares.Count, "un"]);
        resumo.Add(["Pilares com problema (sem comprimento)", pilares.Count(p => p.Length is null), "un"]);
        resumo.Add(["Pilar: soma dos comprimentos totais", Math.Round(pilares.Sum(p => p.Length ?? 0), 3), "m"]);
        resumo.Add(["Pilar: soma do enterrado", Math.Round(pilares.Sum(p => p.Embedded), 3), "m"]);
        resumo.Add(["Pilar: soma do acima do terreno", Math.Round(pilares.Sum(p => p.Above ?? 0), 3), "m"]);

        var analises = planilha.Sheet("Análises");
        analises.Add(["Análise", "Contado em", "Abaixo de", "Qtde abaixo", "Dentro", "Acima de", "Qtde acima", "Sem valor", "Total", "Quando"]);

        foreach (var q in quantificacoes.OrderBy(q => q.Kind))
        {
            Linha(analises, q, q.Points, q.Kind == IndependentKind.Slope ? "mesas" : "pilares");
            if (q.Modules is not null) Linha(analises, q, q.Modules, "módulos");
        }

        var aba = planilha.Sheet("Pilares");
        aba.Add(["Mesa", "Pilar", "Enterrado (m)", "Acima do terreno (m)", "Total (m)"]);

        foreach (var p in pilares.OrderBy(p => p.Table, StringComparer.Ordinal).ThenBy(p => p.Number))
            aba.Add([p.Table, p.Number, Math.Round(p.Embedded, 3), Arredondar(p.Above), Arredondar(p.Length)]);

        // Os comprimentos agrupados: é a lista de compra do ferro.
        var compra = planilha.Sheet("Compra de pilares");
        compra.Add(["Comprimento total (m)", "Quantidade"]);

        foreach (var g in pilares.Where(p => p.Length is not null).GroupBy(p => Math.Round(p.Length!.Value, 2)).OrderBy(g => g.Key))
            compra.Add([g.Key, g.Count()]);

        return planilha;
    }

    private static void Linha(List<object?[]> aba, AnalysisTally q, BandCount c, string unidade)
    {
        var sufixo = q.Kind == IndependentKind.Slope ? (q.Unit == SlopeUnit.Degrees ? " °" : " %") : " m";

        aba.Add([
            IndependentAnalysis.Name(q.Kind),
            unidade,
            q.Rule.Below is { } b ? b.ToString("0.###", Brasil) + sufixo : "—",
            c.Below,
            c.Inside,
            q.Rule.Above is { } a ? a.ToString("0.###", Brasil) + sufixo : "—",
            c.Above,
            c.Missing,
            c.Total,
            q.When.ToString("dd/MM/yyyy HH:mm", Brasil),
        ]);
    }

    private static object? Arredondar(double? valor) => valor is { } v ? Math.Round(v, 3) : null;
}
