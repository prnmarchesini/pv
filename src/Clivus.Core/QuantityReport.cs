using System.Globalization;

namespace Clivus.Core;

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
/// As abas do layout: Resumo (mesas, módulos, kWp, pilares, metros de
/// pilar), Análises (a última quantificação de cada análise), Pilares (um
/// por linha, com enterrado, acima e total) e Compra de pilares. Desde o
/// menu Exportar (10/10/2026) o usuário escolhe quais vão (<see cref="ExportSheets"/>),
/// e entram também o resumo elétrico e as tabelas de cabos, no fim.
/// </summary>
public static class QuantityReport
{
    /// <summary>A planilha.</summary>
    /// <param name="mesas">Quantas mesas.</param>
    /// <param name="modulos">Quantos módulos.</param>
    /// <param name="kwp">A potência instalada, em kWp.</param>
    /// <param name="pilares">Os pilares.</param>
    /// <param name="quantificacoes">A última quantificação de cada análise que foi feita.</param>
    /// <param name="abas">Quais abas do layout vão (todas, sem escolha).</param>
    /// <param name="extras">Tabelas a mais (cabos): uma aba cada, no fim, com o título como nome.</param>
    /// <param name="eletrico">O resumo elétrico (a aba vai se não for null).</param>
    /// <param name="capa">O nome do desenho e a hora: com ela, a primeira aba é o Sumário (o que há em cada aba).</param>
    public static XlsxWriter Build(int mesas, int modulos, double kwp, IReadOnlyList<QuantityPillar> pilares, IReadOnlyList<AnalysisTally> quantificacoes,
        ExportSheets abas = ExportSheets.Layout, IEnumerable<CableTable>? extras = null, SystemSummary? eletrico = null, (string Desenho, DateTime Quando)? capa = null)
    {
        ArgumentNullException.ThrowIfNull(pilares);
        ArgumentNullException.ThrowIfNull(quantificacoes);

        var planilha = new XlsxWriter();
        var indice = new List<(string Aba, string Conteudo)>();

        if (abas.HasFlag(ExportSheets.Summary)) indice.Add((Resumo(planilha, mesas, modulos, kwp, pilares), Tr.T("Mesas, módulos, potência e os totais de pilar.")));
        if (abas.HasFlag(ExportSheets.Analyses)) indice.Add((Analises(planilha, quantificacoes), Tr.T("A última quantificação de cada análise.")));
        if (abas.HasFlag(ExportSheets.Pillars)) indice.Add((Pilares(planilha, pilares), Tr.T("Um pilar por linha: enterrado, acima do terreno e total.")));
        if (abas.HasFlag(ExportSheets.PillarPurchase)) indice.Add((Compra(planilha, pilares), Tr.T("Os comprimentos agrupados, com a quantidade de cada um.")));
        if (eletrico is not null) indice.Add((ResumoEletrico(planilha, eletrico), Tr.T("Subestações, trafos, inversores, strings, módulos e kWp pela cadeia de vínculo.")));
        foreach (var t in extras ?? []) indice.Add((Tabela(planilha, t), t.Title));

        if (capa is { } c) Sumario(planilha, c.Desenho, c.Quando, indice);
        return planilha;
    }

    /// <summary>A primeira aba: o título, o desenho, a hora e o que há em cada aba.</summary>
    private static void Sumario(XlsxWriter planilha, string desenho, DateTime quando, IReadOnlyList<(string Aba, string Conteudo)> indice)
    {
        var aba = planilha.Sheet(Tr.T("Sumário"), primeira: true);
        aba.TitleRows.Add(0);
        aba.Add([Tr.F("Clivus Solar — {0}", desenho)]);
        aba.AddNote(Tr.F("Exportado em {0}", quando.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)));
        aba.Add([]);
        aba.HeaderRow = aba.Count;
        aba.Add([Tr.T("Aba"), Tr.T("O que tem")]);
        foreach (var (nome, conteudo) in indice) aba.Add([nome, conteudo]);
    }

    /// <summary>
    /// O resumo elétrico pela cadeia: subestação, UC e trafo em negrito
    /// (seções), inversores e strings embaixo, total no fim, pendências como nota.
    /// </summary>
    private static string ResumoEletrico(XlsxWriter planilha, SystemSummary r)
    {
        var aba = planilha.Sheet(Tr.T("Resumo elétrico"));
        aba.Add([Tr.T("Item"), Tr.T("Detalhe"), Tr.T("Strings"), Tr.T("Módulos"), "kWp", Tr.T("Observação")]);

        foreach (var l in r.Rows())
        {
            object?[] linha = [new string(' ', l.Level * 4) + l.Name, l.Detail, l.Strings, l.Modules, Math.Round(l.PowerKwp, 3), l.Note];
            if (l.Kind == SummaryRowKind.Total) aba.AddTotal(linha);
            else
            {
                if (l.Kind is SummaryRowKind.Substation or SummaryRowKind.Unit or SummaryRowKind.Transformer) aba.SectionRows.Add(aba.Count);
                aba.Add(linha);
            }
        }

        var pendencias = r.Pending();
        if (pendencias.Count == 0) return Tr.T("Resumo elétrico");
        aba.Add([]);
        foreach (var p in pendencias) aba.AddNote(p);
        return Tr.T("Resumo elétrico");
    }

    /// <summary>Uma tabela como aba: o cabeçalho, as linhas, o total e as notas embaixo (nome da aba = título, até 31 letras, sem os caracteres proibidos).</summary>
    public static string Tabela(XlsxWriter planilha, CableTable t)
    {
        ArgumentNullException.ThrowIfNull(planilha);
        ArgumentNullException.ThrowIfNull(t);

        var nome = new string(t.Title.Where(c => ":\\/?*[]".IndexOf(c) < 0).ToArray()).Trim();
        if (nome.Length > 31) nome = nome[..31];
        if (nome.Length == 0) nome = Tr.T("Tabela");

        // Nome repetido (o Excel não aceita): ganha " (2)", " (3)"... dentro das 31 letras.
        var unico = nome;
        for (var n = 2; planilha.HasSheet(unico); n++)
        {
            var sufixo = $" ({n})";
            unico = (nome.Length + sufixo.Length > 31 ? nome[..(31 - sufixo.Length)] : nome) + sufixo;
        }

        var aba = planilha.Sheet(unico);
        aba.Add([.. t.Header]);
        foreach (var r in t.Rows) aba.Add([.. r]);
        foreach (var g in t.GroupRows ?? []) aba.SectionRows.Add(g + 1);
        if (t.Total.Count > 0) aba.AddTotal([.. t.Total]);
        if (t.Notes.Count == 0) return unico;
        aba.Add([]);
        foreach (var n in t.Notes) aba.AddNote(n);
        return unico;
    }

    private static string Resumo(XlsxWriter planilha, int mesas, int modulos, double kwp, IReadOnlyList<QuantityPillar> pilares)
    {
        var un = Tr.T("un");
        var resumo = planilha.Sheet(Tr.T("Resumo"));
        resumo.PerCellFormat = true;
        resumo.Add([Tr.T("Item"), Tr.T("Quantidade"), Tr.T("Unidade")]);
        resumo.Add([Tr.T("Mesas"), mesas, un]);
        resumo.Add([Tr.T("Módulos"), modulos, un]);
        resumo.Add([Tr.T("Potência"), Math.Round(kwp, 3), "kWp"]);
        resumo.Add([Tr.T("Pilares"), pilares.Count, un]);
        resumo.Add([Tr.T("Pilares com problema (sem comprimento)"), pilares.Count(p => p.Length is null), un]);
        resumo.Add([Tr.T("Pilar: soma dos comprimentos totais"), Math.Round(pilares.Sum(p => p.Length ?? 0), 3), "m"]);
        resumo.Add([Tr.T("Pilar: soma do enterrado"), Math.Round(pilares.Sum(p => p.Embedded), 3), "m"]);
        resumo.Add([Tr.T("Pilar: soma do acima do terreno"), Math.Round(pilares.Sum(p => p.Above ?? 0), 3), "m"]);
        return Tr.T("Resumo");
    }

    private static string Analises(XlsxWriter planilha, IReadOnlyList<AnalysisTally> quantificacoes)
    {
        var analises = planilha.Sheet(Tr.T("Análises"));
        analises.Add([Tr.T("Análise"), Tr.T("Contado em"), Tr.T("Abaixo de"), Tr.T("Qtde abaixo"), Tr.T("Dentro"), Tr.T("Acima de"), Tr.T("Qtde acima"), Tr.T("Sem valor"), Tr.T("Total"), Tr.T("Quando")]);

        foreach (var q in quantificacoes.OrderBy(q => q.Kind))
        {
            Linha(analises, q, q.Points, q.Kind == IndependentKind.Slope ? Tr.T("mesas") : Tr.T("pilares"));
            if (q.Modules is not null) Linha(analises, q, q.Modules, Tr.T("módulos"));
        }

        return Tr.T("Análises");
    }

    private static string Pilares(XlsxWriter planilha, IReadOnlyList<QuantityPillar> pilares)
    {
        var aba = planilha.Sheet(Tr.T("Pilares"));
        aba.Add([Tr.T("Mesa"), Tr.T("Pilar"), Tr.T("Enterrado (m)"), Tr.T("Acima do terreno (m)"), Tr.T("Total (m)")]);

        foreach (var p in pilares.OrderBy(p => p.Table, StringComparer.Ordinal).ThenBy(p => p.Number))
            aba.Add([p.Table, p.Number, Math.Round(p.Embedded, 3), Arredondar(p.Above), Arredondar(p.Length)]);

        if (pilares.Count > 0)
            aba.AddTotal([Tr.T("Total"), pilares.Count, Math.Round(pilares.Sum(p => p.Embedded), 3), Math.Round(pilares.Sum(p => p.Above ?? 0), 3), Math.Round(pilares.Sum(p => p.Length ?? 0), 3)]);
        return Tr.T("Pilares");
    }

    private static string Compra(XlsxWriter planilha, IReadOnlyList<QuantityPillar> pilares)
    {
        // Os comprimentos agrupados: é a lista de compra do ferro.
        var compra = planilha.Sheet(Tr.T("Compra de pilares"));
        compra.Add([Tr.T("Comprimento total (m)"), Tr.T("Quantidade")]);

        foreach (var g in pilares.Where(p => p.Length is not null).GroupBy(p => Math.Round(p.Length!.Value, 2)).OrderBy(g => g.Key))
            compra.Add([g.Key, g.Count()]);

        var comComprimento = pilares.Where(p => p.Length is not null).ToList();
        if (comComprimento.Count > 0) compra.AddTotal([Tr.T("Total"), comComprimento.Count]);
        return Tr.T("Compra de pilares");
    }

    private static void Linha(List<object?[]> aba, AnalysisTally q, BandCount c, string unidade)
    {
        var sufixo = q.Kind == IndependentKind.Slope ? (q.Unit == SlopeUnit.Degrees ? " °" : " %") : " m";

        aba.Add([
            IndependentAnalysis.Name(q.Kind),
            unidade,
            q.Rule.Below is { } b ? b.ToString("0.###", Tr.Culture) + sufixo : "—",
            c.Below,
            c.Inside,
            q.Rule.Above is { } a ? a.ToString("0.###", Tr.Culture) + sufixo : "—",
            c.Above,
            c.Missing,
            c.Total,
            q.When.ToString("dd/MM/yyyy HH:mm", Tr.Culture),
        ]);
    }

    private static object? Arredondar(double? valor) => valor is { } v ? Math.Round(v, 3) : null;
}

/// <summary>O que vai para o Excel (o menu Exportar, 10/10/2026).</summary>
[Flags]
public enum ExportSheets
{
    None = 0,
    Summary = 1,
    Analyses = 2,
    Pillars = 4,
    PillarPurchase = 8,
    ElectricalSummary = 16,
    Cables = 32,

    /// <summary>As quatro abas do layout (o Excel de antes do menu).</summary>
    Layout = Summary | Analyses | Pillars | PillarPurchase,
}
