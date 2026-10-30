namespace Clivus.Core;

/// <summary>
/// Uma peça da estrutura para o quantitativo: de qual mesa, que peça, e o
/// comprimento.
/// </summary>
/// <param name="Table">O letreiro da mesa (F1.2).</param>
/// <param name="Part">A peça.</param>
/// <param name="Line">A linha de pilares da mão francesa e do tirante.</param>
/// <param name="Index">O pórtico, a linha de terça ou o vão (ver <see cref="StructureBar.Index"/>).</param>
/// <param name="Length">O comprimento do eixo, em metro.</param>
/// <param name="MaxBar">Na terça, a maior barra em que ela é cortada; null nas outras peças.</param>
public sealed record QuantityBar(string Table, StructurePart Part, PillarLine Line, int Index, double Length, double? MaxBar = null);

/// <summary>Uma linha da lista de material da estrutura.</summary>
/// <param name="Item">O nome da peça, como vai para o Excel.</param>
/// <param name="Length">O comprimento de corte, em metro (null nas contagens).</param>
/// <param name="Quantity">Quantas.</param>
/// <param name="Total">Os metros somados (null nas contagens).</param>
public sealed record StructureBillRow(string Item, double? Length, int Quantity, double? Total);

/// <summary>
/// A lista de material da bifila (passo 37.4): pilares por linha, vigas, mãos
/// francesas por linha, terças (com as barras em que são cortadas e as
/// emendas) e tirantes. Sai das mesmas peças do desenho: o plugin lê o
/// comprimento gravado em cada uma, como faz com os pilares.
///
/// O comprimento de corte é o da peça arredondado PARA CIMA no milímetro
/// (com 0,1 mm de folga para o ruído da conta): a peça nunca sai curta, e a
/// mão francesa de catálogo de 1,244 continua 1,244. Peças com o mesmo corte
/// são uma linha só.
/// </summary>
public static class StructureBill
{
    /// <summary>As peças de uma mesa montada, para o quantitativo.</summary>
    /// <param name="table">O letreiro da mesa.</param>
    /// <param name="structure">As peças, de <see cref="BifilaStructure.Build"/>.</param>
    /// <param name="purlinMaxBar">A maior barra de terça da estrutura.</param>
    public static IReadOnlyList<QuantityBar> From(string table, BifilaStructureResult structure, double purlinMaxBar)
    {
        ArgumentNullException.ThrowIfNull(structure);

        // Barra máxima que não é medida não corta terça nenhuma: fica sem.
        double? barra = double.IsFinite(purlinMaxBar) && purlinMaxBar > 0 ? purlinMaxBar : null;

        return structure.Bars
            .Select(b => new QuantityBar(table, b.Part, b.Line, b.Index, b.Length, b.Part == StructurePart.Purlin ? barra : null))
            .ToList();
    }

    /// <summary>
    /// O comprimento de corte de uma peça: para cima no milímetro, com 0,1 mm
    /// de folga (1,24402 é a peça de 1,244, e não uma de 1,245).
    /// </summary>
    public static double CutLength(double length) => Math.Ceiling(Math.Round((length - 0.0001) * 1000, 6)) / 1000;

    /// <summary>
    /// Em quantas barras de até <paramref name="maxBar"/> uma terça de
    /// <paramref name="length"/> é cortada. A folga é de um milionésimo da
    /// conta (7 µm numa barra de 7 m): 14,0000001 m em barras de 7 continua
    /// sendo duas barras, e não três. Medida que não é número dá zero.
    /// </summary>
    public static int PurlinBars(double length, double maxBar)
    {
        if (!(length > 0) || !(maxBar > 0) || !double.IsFinite(length) || !double.IsFinite(maxBar)) return 0;

        return Math.Max(1, (int)Math.Ceiling(length / maxBar - 1e-6));
    }

    /// <summary>As linhas da lista, na ordem da obra: pilares, vigas, mãos francesas, terças, tirantes.</summary>
    public static IReadOnlyList<StructureBillRow> Rows(IReadOnlyList<QuantityPillar> pillars, IReadOnlyList<QuantityBar> bars)
    {
        ArgumentNullException.ThrowIfNull(pillars);
        ArgumentNullException.ThrowIfNull(bars);

        var linhas = new List<StructureBillRow>();

        // Os pilares: uma linha por lado, com os metros. A compra por
        // comprimento já está na aba "Compra de pilares".
        foreach (var (lado, nome) in new[] { (PillarLine.Front, Tr.T("Pilar dianteiro")), (PillarLine.Rear, Tr.T("Pilar traseiro")) })
        {
            var deste = pillars.Where(p => p.Line == lado).ToList();
            if (deste.Count > 0) linhas.Add(new StructureBillRow(nome, null, deste.Count, Math.Round(deste.Sum(p => p.Length ?? 0), 3)));
        }

        Agrupar(linhas, bars.Where(b => b.Part == StructurePart.Beam), Tr.T("Viga"));
        Agrupar(linhas, bars.Where(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Front), Tr.T("Mão francesa dianteira"));
        Agrupar(linhas, bars.Where(b => b.Part == StructurePart.Brace && b.Line == PillarLine.Rear), Tr.T("Mão francesa traseira"));

        var tercas = bars.Where(b => b.Part == StructurePart.Purlin).ToList();
        Agrupar(linhas, tercas, Tr.T("Terça"));

        foreach (var porBarra in tercas.Where(t => t.MaxBar is { } m && double.IsFinite(m) && m > 0).GroupBy(t => t.MaxBar!.Value).OrderBy(g => g.Key))
        {
            var barra = porBarra.Key.ToString("0.###", Tr.Culture);
            var barras = porBarra.Sum(t => PurlinBars(t.Length, porBarra.Key));
            linhas.Add(new StructureBillRow(Tr.F("Terça: barras de até {0} m", barra), null, barras, null));
            linhas.Add(new StructureBillRow(Tr.F("Terça: emendas (barras de até {0} m)", barra), null, barras - porBarra.Count(), null));
        }

        Agrupar(linhas, bars.Where(b => b.Part == StructurePart.Rod), Tr.T("Tirante"));

        return linhas;
    }

    /// <summary>Uma mesa na lista: quantas peças de cada uma, e os metros de terça.</summary>
    public sealed record TableRow(string Table, int FrontPillars, int RearPillars, int Beams, int Braces, double PurlinMeters, int Rods);

    /// <summary>A lista por mesa (o plano pede "por mesa e total"), na ordem do letreiro.</summary>
    public static IReadOnlyList<TableRow> PerTable(IReadOnlyList<QuantityPillar> pillars, IReadOnlyList<QuantityBar> bars)
    {
        ArgumentNullException.ThrowIfNull(pillars);
        ArgumentNullException.ThrowIfNull(bars);

        var mesas = pillars.Where(p => p.Line != PillarLine.Single).Select(p => p.Table)
            .Concat(bars.Select(b => b.Table))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(m => m, StringComparer.Ordinal);

        return mesas
            .Select(m => new TableRow(
                m,
                pillars.Count(p => p.Table == m && p.Line == PillarLine.Front),
                pillars.Count(p => p.Table == m && p.Line == PillarLine.Rear),
                bars.Count(b => b.Table == m && b.Part == StructurePart.Beam),
                bars.Count(b => b.Table == m && b.Part == StructurePart.Brace),
                Math.Round(bars.Where(b => b.Table == m && b.Part == StructurePart.Purlin).Sum(b => b.Length), 3),
                bars.Count(b => b.Table == m && b.Part == StructurePart.Rod)))
            .ToList();
    }

    /// <summary>A aba "Estrutura" da planilha: o total da obra e, embaixo, mesa a mesa.</summary>
    internal static string Sheet(XlsxWriter planilha, IReadOnlyList<QuantityPillar> pillars, IReadOnlyList<QuantityBar> bars)
    {
        var aba = planilha.Sheet(Tr.T("Estrutura"));
        aba.Add([Tr.T("Peça"), Tr.T("Comprimento de corte (m)"), Tr.T("Quantidade"), Tr.T("Total (m)")]);

        foreach (var l in Rows(pillars, bars)) aba.Add([l.Item, l.Length, l.Quantity, l.Total]);

        aba.Add([]);
        aba.SectionRows.Add(aba.Count);
        aba.Add([Tr.T("Mesa"), Tr.T("Pilares dianteiros"), Tr.T("Pilares traseiros"), Tr.T("Vigas"), Tr.T("Mãos francesas"), Tr.T("Terça (m)"), Tr.T("Tirantes")]);
        foreach (var m in PerTable(pillars, bars)) aba.Add([m.Table, m.FrontPillars, m.RearPillars, m.Beams, m.Braces, m.PurlinMeters, m.Rods]);

        aba.Add([]);

        // Pilar bifila sem nenhuma peça: a lista sairia parecendo completa.
        if (bars.Count == 0)
            aba.AddNote(Tr.T("Nenhuma peça da estrutura foi achada no desenho, só os pilares: processe a área de novo para desenhar as vigas, mãos francesas, terças e tirantes."));

        aba.AddNote(Tr.T("Peças pequenas (parafusos, conectores, suportes de terça) não entram: dependem do fornecedor."));
        aba.AddNote(Tr.T("Tirante: comprimento entre os pontos de fixação; a peça de catálogo tem a rosca a mais."));
        return Tr.T("Estrutura");
    }

    private static void Agrupar(List<StructureBillRow> linhas, IEnumerable<QuantityBar> barras, string nome)
    {
        foreach (var g in barras.GroupBy(b => CutLength(b.Length)).OrderBy(g => g.Key))
            linhas.Add(new StructureBillRow(nome, g.Key, g.Count(), Math.Round(g.Key * g.Count(), 3)));
    }
}
