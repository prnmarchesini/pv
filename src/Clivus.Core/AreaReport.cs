using System.Globalization;

namespace Clivus.Core;

/// <summary>
/// O Excel do resumo por área (41.3): a aba "Resumo", com uma linha por área,
/// a das mesas de fora das áreas (se houver) e o total; e uma aba por área,
/// com a contagem por tipo de mesa e os itens dela (pilares, pendentes,
/// ocupação). Os números são os de <see cref="AreaSummary"/>: a planilha não
/// refaz conta.
/// </summary>
public static class AreaReport
{
    /// <summary>A planilha.</summary>
    /// <param name="summary">O resumo por área.</param>
    /// <param name="drawing">O nome do desenho, para o título.</param>
    /// <param name="when">A hora da exportação.</param>
    public static XlsxWriter Build(AreaSummary summary, string drawing, DateTime when)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(drawing);

        var planilha = new XlsxWriter();
        Resumo(planilha, summary, drawing, when);

        foreach (var area in summary.Areas) Detalhe(planilha, area);
        if (summary.Outside is { } fora) Detalhe(planilha, fora);

        return planilha;
    }

    private static void Resumo(XlsxWriter planilha, AreaSummary summary, string drawing, DateTime when)
    {
        var aba = planilha.Sheet(Tr.T("Resumo"));
        aba.TitleRows.Add(0);
        aba.Add([Tr.F("Clivus Solar — {0}: resumo por área", drawing)]);
        aba.AddNote(Tr.F("Exportado em {0}", when.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)));
        aba.Add([]);

        aba.HeaderRow = aba.Count;
        aba.Add([
            Tr.T("Área"), Tr.T("Área (ha)"), Tr.T("Mesas"), Tr.T("Módulos"), "kWp", Tr.T("Pilares"), Tr.T("Pilares (m)"),
            Tr.T("Pendentes"), Tr.T("Não cabem no terreno"), Tr.T("Na divisa"), Tr.T("Área dos módulos (m²)"), Tr.T("Ocupação (%)"),
            Tr.T("Tipos de mesa"), Tr.T("Segue os objetos"),
        ]);

        foreach (var linha in summary.Areas) aba.Add(Linha(linha));
        if (summary.Outside is { } fora) aba.Add(Linha(fora));
        aba.AddTotal(Linha(summary.Total));

        aba.Add([]);
        foreach (var nota in Notas(summary)) aba.AddNote(nota);
    }

    private static object?[] Linha(AreaSummaryRow l) =>
    [
        l.Name,
        l.Hectares is { } ha ? Math.Round(ha, 4) : null,
        l.Census.Tables,
        l.Census.Modules,
        Math.Round(l.Census.PowerKwp, 3),
        l.Census.Pillars,
        Math.Round(l.PillarMeters, 2),
        l.Census.Dirty,
        l.Census.Marked,
        l.OnBorder,
        Math.Round(l.ModuleArea, 2),
        l.Occupation is { } o ? Math.Round(o * 100, 2) : null,
        l.TypesText,
        l.Kind == AreaRowKind.Area ? (l.FollowsObjects ? Tr.T("sim") : Tr.T("não")) : null,
    ];

    /// <summary>A aba de uma área: a contagem por tipo de mesa e, embaixo, os itens.</summary>
    private static void Detalhe(XlsxWriter planilha, AreaSummaryRow l)
    {
        var aba = planilha.Sheet(QuantityReport.SheetName(planilha, l.Name));
        aba.PerCellFormat = true;
        aba.TitleRows.Add(0);
        aba.Add([l.Name]);
        aba.AddNote(l.Kind == AreaRowKind.Outside
            ? Tr.T("Mesas com o centro fora de toda área de implantação.")
            : l.FollowsObjects ? Tr.T("A área segue os objetos do cliente (o Regerar área a refaz por eles).") : Tr.T("A área é gerada pelo alinhamento."));
        aba.Add([]);

        aba.HeaderRow = aba.Count;
        aba.Add([Tr.T("Tipo de mesa"), Tr.T("Mesas"), Tr.T("Módulos"), "kWp"]);
        foreach (var t in l.Types) aba.Add([t.Name, t.Tables, t.Modules, Math.Round(t.PowerKwp, 3)]);
        aba.AddTotal([Tr.T("Total"), l.Census.Tables, l.Census.Modules, Math.Round(l.Census.PowerKwp, 3)]);

        aba.Add([]);
        aba.SectionRows.Add(aba.Count);
        aba.Add([Tr.T("Item"), Tr.T("Quantidade"), Tr.T("Unidade")]);

        var un = Tr.T("un");
        if (l.Hectares is { } ha) aba.Add([Tr.T("Área em planta"), Math.Round(ha, 4), "ha"]);
        aba.Add([Tr.T("Mesas"), l.Census.Tables, un]);
        aba.Add([Tr.T("Módulos"), l.Census.Modules, un]);
        aba.Add([Tr.T("Potência"), Math.Round(l.Census.PowerKwp, 3), "kWp"]);
        aba.Add([Tr.T("Pilares"), l.Census.Pillars, un]);
        aba.Add([Tr.T("Pilar: soma dos comprimentos totais"), Math.Round(l.PillarMeters, 3), "m"]);
        aba.Add([Tr.T("Pilares com problema (sem comprimento)"), l.Census.PillarsWithoutLength, un]);
        aba.Add([Tr.T("Mesas pendentes"), l.Census.Dirty, un]);
        aba.Add([Tr.T("Mesas que não cabem no terreno"), l.Census.Marked, un]);
        if (l.Kind == AreaRowKind.Area) aba.Add([Tr.T("Mesas na divisa (algum canto fora da área)"), l.OnBorder, un]);
        aba.Add([Tr.T("Área dos módulos"), Math.Round(l.ModuleArea, 2), "m²"]);
        if (l.Occupation is { } o) aba.Add([Tr.T("Ocupação (área dos módulos ÷ área em planta)"), Math.Round(o * 100, 2), "%"]);

        if (l.TablesWithoutFace > 0)
        {
            aba.Add([]);
            aba.AddNote(Tr.F("ATENÇÃO: {0} mesa(s) sem a face dos módulos no desenho ficaram fora da área dos módulos.", l.TablesWithoutFace));
        }
    }

    /// <summary>As regras da conta, embaixo do resumo: quem lê a planilha sem o Clivus precisa delas.</summary>
    private static IEnumerable<string> Notas(AreaSummary summary)
    {
        yield return Tr.T("Cada mesa conta uma vez, na área onde está o centro dela (dentro de duas áreas, na menor). \"Na divisa\": mesa com algum canto fora da área em que conta.");
        yield return Tr.T("O total é o desenho inteiro (bate com o Recontar); a área do total é a da união das áreas (área sobreposta conta uma vez).");
        yield return Tr.T("Ocupação: a área dos módulos (a face de cima de cada um, em verdadeira grandeza) ÷ a área em planta. Pilares (m): a soma dos comprimentos gravados no desenho.");

        if (summary.Total.Census.SimulatedPowerWatts is { } simulada)
            yield return Tr.F("ATENÇÃO: potência do módulo trocada pela área para {0:0.#} Wp (simulação): o kWp acima usa essa potência em todas as mesas", simulada);

        if (summary.Total.Census.TablesWithoutPower > 0)
            yield return Tr.F("ATENÇÃO: {0} mesa(s) sem potência gravada (desenhadas antes do 7.6): usaram {1:N0} W do perfil atual", summary.Total.Census.TablesWithoutPower, summary.Total.Census.FallbackPowerWatts);
    }
}
