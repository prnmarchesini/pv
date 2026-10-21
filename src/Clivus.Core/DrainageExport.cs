namespace Clivus.Core;

/// <summary>As medidas da caixa entre canaletas (32.6), gravadas no desenho.</summary>
public sealed record BoxSettings
{
    /// <summary>Comprimento interno, em m (ao longo da canaleta).</summary>
    public double InnerLength { get; init; } = 0.60;

    /// <summary>Largura interna, em m.</summary>
    public double InnerWidth { get; init; } = 0.60;

    /// <summary>Espessura da parede e do fundo, em m.</summary>
    public double Wall { get; init; } = 0.15;

    /// <summary>Quanto o fundo fica abaixo do fundo da canaleta mais funda que chega, em m.</summary>
    public double FloorGap { get; init; } = 0.10;

    /// <summary>Profundidade interna quando nenhuma canaleta chega nela, em m.</summary>
    public double MinimumDepth { get; init; } = 0.50;

    public string? Problem() =>
        !(InnerLength > 0) || !(InnerWidth > 0) ? Tr.T("As medidas internas da caixa precisam ser maiores que zero.")
        : !(Wall > 0) ? Tr.T("A parede da caixa precisa ser maior que zero.")
        : !(FloorGap >= 0) || !(MinimumDepth > 0) ? Tr.T("A folga do fundo não pode ser negativa e a profundidade mínima precisa ser maior que zero.")
        : null;
}

/// <summary>Uma caixa no desenho: o nome, as medidas e as cotas.</summary>
public sealed record BoxRecord(string Name, double InnerLength, double InnerWidth, double Wall, double Top, double Floor)
{
    /// <summary>Profundidade interna, em m.</summary>
    public double Depth => Top - Floor;

    /// <summary>Volume de concreto (paredes e fundo), em m³.</summary>
    public double Concrete =>
        (InnerLength + 2 * Wall) * (InnerWidth + 2 * Wall) * (Depth + Wall) - InnerLength * InnerWidth * Depth;
}

/// <summary>Uma canaleta para o Exportar: nome, seção, quantitativos e o dissipador.</summary>
public sealed record ChannelExportRow(string Name, ChannelSection Section, ChannelQuantities Quantities, DissipatorEnd Dissipator);

/// <summary>
/// Desenhos › Exportar canaletas (32.7). Renan, 21/10/2026: "o menu de
/// exportação que vai exportar quantos metros de cada canaleta e tipo". As
/// tabelas do Excel: cada canaleta, o resumo por seção, as caixas e os
/// dissipadores.
/// </summary>
public static class DrainageExport
{
    public static IReadOnlyList<CableTable> Tables(IReadOnlyList<ChannelExportRow> canaletas, IReadOnlyList<BoxRecord> caixas)
    {
        ArgumentNullException.ThrowIfNull(canaletas);
        ArgumentNullException.ThrowIfNull(caixas);

        static object R(double v, int casas) => Math.Round(v, casas);

        string Tipo(ChannelSection s) => s.Kind == ChannelKind.HalfPipe ? Tr.T("meia-cana") : Tr.T("trapezoidal");

        string Medidas(ChannelSection s) => s.Kind == ChannelKind.HalfPipe
            ? Tr.F("r {0:0.###} · e {1:0.###}", s.Radius, s.Thickness)
            : Tr.F("b {0:0.###} · z {1:0.###} · y {2:0.###} · e {3:0.###}", s.Base, s.SideSlope, s.Height, s.Thickness);

        var tabelas = new List<CableTable>
        {
            new(Tr.T("Canaletas"),
                [Tr.T("Canaleta"), Tr.T("Tipo"), Tr.T("Seção"), Tr.T("Medidas (m)"), Tr.T("Revestimento"), Tr.T("Metros em planta"), Tr.T("Metros no fundo"), Tr.T("Concreto (m³)"), Tr.T("Escavação (m³)")],
                canaletas.Select(c => (IReadOnlyList<object?>)[c.Name, Tipo(c.Section), c.Section.Name, Medidas(c.Section), c.Section.Lining,
                    R(c.Quantities.Length2D, 2), R(c.Quantities.Length3D, 2), R(c.Quantities.Concrete, 3), R(c.Quantities.Excavation, 3)]).ToList(),
                [Tr.T("Total"), null, null, null, null, R(canaletas.Sum(c => c.Quantities.Length2D), 2), R(canaletas.Sum(c => c.Quantities.Length3D), 2),
                    R(canaletas.Sum(c => c.Quantities.Concrete), 3), R(canaletas.Sum(c => c.Quantities.Excavation), 3)],
                [Tr.T("Metros no fundo: ao longo da declividade (o que se compra e assenta); em planta: o que se mede no desenho.")]),
        };

        // Por tipo: a seção inteira (nome e medidas) é o tipo.
        var porTipo = canaletas
            .GroupBy(c => (c.Section.Name, Medidas(c.Section), Tipo(c.Section)))
            .OrderBy(g => g.Key.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (IReadOnlyList<object?>)[g.Key.Name, g.Key.Item3, g.Key.Item2, g.Count(), R(g.Sum(c => c.Quantities.Length2D), 2), R(g.Sum(c => c.Quantities.Length3D), 2), R(g.Sum(c => c.Quantities.Concrete), 3)])
            .ToList();

        tabelas.Add(new CableTable(Tr.T("Por tipo"),
            [Tr.T("Seção"), Tr.T("Tipo"), Tr.T("Medidas (m)"), Tr.T("Canaletas"), Tr.T("Metros em planta"), Tr.T("Metros no fundo"), Tr.T("Concreto (m³)")],
            porTipo, [Tr.T("Total"), null, null, canaletas.Count, R(canaletas.Sum(c => c.Quantities.Length2D), 2), R(canaletas.Sum(c => c.Quantities.Length3D), 2), R(canaletas.Sum(c => c.Quantities.Concrete), 3)], []));

        tabelas.Add(new CableTable(Tr.T("Caixas"),
            [Tr.T("Caixa"), Tr.T("Interno (m)"), Tr.T("Parede (m)"), Tr.T("Profundidade (m)"), Tr.T("Topo (m)"), Tr.T("Fundo (m)"), Tr.T("Concreto (m³)")],
            caixas.Select(b => (IReadOnlyList<object?>)[b.Name, Tr.F("{0:0.##} × {1:0.##}", b.InnerLength, b.InnerWidth), b.Wall, R(b.Depth, 2), R(b.Top, 2), R(b.Floor, 2), R(b.Concrete, 3)]).ToList(),
            caixas.Count == 0 ? [] : [Tr.T("Total"), caixas.Count, null, null, null, null, R(caixas.Sum(b => b.Concrete), 3)], []));

        var dissipadores = canaletas.Where(c => c.Dissipator != DissipatorEnd.None).ToList();
        tabelas.Add(new CableTable(Tr.T("Dissipadores"),
            [Tr.T("Canaleta"), Tr.T("Ponta"), Tr.T("Modelo do álbum"), "L1 (m)", "L2 (m)", Tr.T("Comprimento (m)")],
            dissipadores.Select(c =>
            {
                var (l1, l2, comprimento) = EnergyDissipator.Size(c.Section.TopWidth);
                return (IReadOnlyList<object?>)[c.Name, c.Dissipator == DissipatorEnd.Downstream ? Tr.T("saída") : Tr.T("ponta de cima"),
                    EnergyDissipator.Model(c.Section.TopWidth) ?? "—", R(l1, 3), R(l2, 3), R(comprimento, 3)];
            }).ToList(),
            dissipadores.Count == 0 ? [] : [Tr.T("Total"), dissipadores.Count, null, null, null, null],
            [Tr.T("DNIT IPR 736, desenho 1.19 (DES): L1 = abertura, L2 = 3·L1, comprimento = 2·L1.")]));

        return tabelas;
    }

    /// <summary>
    /// O fundo da caixa: 10 cm (a folga) abaixo do fundo mais baixo das
    /// canaletas que chegam nela; sem canaleta, o topo menos a profundidade
    /// mínima. Nunca acima do topo menos a profundidade mínima.
    /// </summary>
    public static double BoxFloor(BoxSettings settings, double top, IEnumerable<double> incomingBottoms)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var fundos = incomingBottoms.Where(double.IsFinite).ToList();
        var minimo = top - settings.MinimumDepth;
        return fundos.Count == 0 ? minimo : Math.Min(minimo, fundos.Min() - settings.FloorGap);
    }
}
