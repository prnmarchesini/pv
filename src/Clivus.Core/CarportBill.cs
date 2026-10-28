namespace Clivus.Core;

/// <summary>Uma fila de carport para a lista de material: o nome, o perfil e a fila montada.</summary>
public sealed record CarportBillRow(string Name, CarportProfile Profile, CarportRowResult Row);

/// <summary>Uma linha da lista: a peça, a seção, o comprimento de corte e a quantidade.</summary>
/// <param name="Item">O nome da peça, para o fornecedor.</param>
/// <param name="Section">A seção ou a medida ("2 × C 130×50", "0,31 × 0,31 × 0,006 m").</param>
/// <param name="Length">O comprimento de cada peça em metro, ou null para o que se conta por unidade.</param>
/// <param name="Quantity">Quantas.</param>
public sealed record CarportBillLine(string Item, string Section, double? Length, int Quantity)
{
    /// <summary>O comprimento somado das peças desta linha, ou null.</summary>
    public double? TotalLength => Length * Quantity;
}

/// <summary>Um pilar de uma fila, para a aba que vai a campo.</summary>
public sealed record CarportBillPillar(string Row, int Portal, string Side, double Length, double GroundZ);

/// <summary>A lista de material do carport (etapa 34.4).</summary>
public sealed record CarportBillResult(
    int Rows, int Bays, int Portals, int Modules, double Kwp,
    IReadOnlyList<CarportBillLine> Lines,
    IReadOnlyList<CarportBillPillar> Pillars);

/// <summary>
/// A lista de material do carport (etapa 34.4, <c>plano/carport</c>), tirada
/// das mesmas peças que vão para o desenho (<see cref="CarportGeometry"/>):
/// a soma dos comprimentos é a soma do que está desenhado.
///
/// Barras agrupadas pelo comprimento de corte arredondado ao centímetro para cima. A
/// terça é cortada a cada vão (emenda no pórtico). Grampos de módulo pela
/// regra de sempre: em cada linha de terça, um intermediário entre módulos
/// vizinhos e um de ponta em cada ponta da fila. Parafusos e ligações não
/// entram: dependem do fornecedor da estrutura.
/// </summary>
public static class CarportBill
{
    /// <summary>Monta a lista das filas dadas.</summary>
    public static CarportBillResult Build(IReadOnlyList<CarportBillRow> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        var barras = new List<(string Item, string Secao, double Comprimento)>();
        var unidades = new List<(string Item, string Secao, int Quantidade)>();
        var pilares = new List<CarportBillPillar>();
        int vaos = 0, porticos = 0, modulos = 0;
        double kwp = 0;

        foreach (var fila in filas)
        {
            var perfil = fila.Profile;
            var f = perfil.Frame;
            vaos += fila.Row.Bays.Count;
            porticos += fila.Row.Portals.Count;

            for (var k = 0; k < fila.Row.Portals.Count; k++)
            {
                var portico = fila.Row.Portals[k];
                var pilaresDoPortico = 0;
                foreach (var barra in portico.Bars)
                {
                    barras.Add((Nome(barra.Part), barra.Section.Name, barra.Length));
                    if (barra.Part != CarportPart.Pillar) continue;

                    var alto = pilaresDoPortico++ == 0;
                    pilares.Add(new CarportBillPillar(fila.Name, k + 1, alto ? Tr.T("lado alto") : Tr.T("lado baixo"),
                        barra.Length, alto ? portico.HighBase.Z : portico.LowBase.Z));
                }

                foreach (var bloco in portico.Blocks)
                {
                    var medida = string.Format(Tr.Culture, "{0:0.###} × {0:0.###} × {1:0.###} m", bloco.Size, bloco.Height);
                    unidades.Add((Nome(bloco.Part), medida, 1));
                }
            }

            foreach (var vao in fila.Row.Bays)
            {
                foreach (var terca in vao.Purlins) barras.Add((Nome(CarportPart.Purlin), terca.Section.Name, terca.Length));
                modulos += vao.Modules.Count;
                kwp += vao.Modules.Count * perfil.Array.Module.PowerWatts / 1000;
            }

            // Grampos: em cada linha de terça, entre módulos vizinhos e nas duas pontas da fila.
            var linhasDeTerca = perfil.Array.ModulesDown * f.PurlinsPerModule;
            var naFila = fila.Row.Bays.Count * perfil.Array.ModulesAcross;
            if (naFila > 0)
            {
                unidades.Add((Tr.T("Grampo intermediário de módulo"), string.Empty, linhasDeTerca * (naFila - 1)));
                unidades.Add((Tr.T("Grampo de ponta de módulo"), string.Empty, linhasDeTerca * 2));
            }

            unidades.Add((Tr.T("Módulo"), perfil.Array.Module.DisplayName,
                fila.Row.Bays.Sum(b => b.Modules.Count)));
        }

        var linhas = barras
            // Corte arredondado para CIMA ao centímetro: a peça nunca sai curta
            // (a menos de um milionésimo, para 2,30 não virar 2,31 por ponto flutuante).
            .GroupBy(b => (b.Item, b.Secao, Comprimento: Math.Ceiling(b.Comprimento * 100 - 1e-6) / 100))
            .OrderBy(g => Ordem(g.Key.Item)).ThenBy(g => g.Key.Secao, StringComparer.Ordinal).ThenBy(g => g.Key.Comprimento)
            .Select(g => new CarportBillLine(g.Key.Item, g.Key.Secao, g.Key.Comprimento, g.Count()))
            .Concat(unidades
                .GroupBy(u => (u.Item, u.Secao))
                .Where(g => g.Sum(u => u.Quantidade) > 0)
                .OrderBy(g => Ordem(g.Key.Item)).ThenBy(g => g.Key.Secao, StringComparer.Ordinal)
                .Select(g => new CarportBillLine(g.Key.Item, g.Key.Secao, null, g.Sum(u => u.Quantidade))))
            .ToList();

        return new CarportBillResult(filas.Count, vaos, porticos, modulos, kwp, linhas, pilares);
    }

    /// <summary>O Excel da lista: resumo, peças e um pilar por linha.</summary>
    public static XlsxWriter ToXlsx(CarportBillResult lista)
    {
        ArgumentNullException.ThrowIfNull(lista);

        var planilha = new XlsxWriter();
        var un = Tr.T("un");

        var resumo = planilha.Sheet(Tr.T("Carport — resumo"), primeira: true);
        resumo.PerCellFormat = true;
        resumo.Add([Tr.T("Item"), Tr.T("Quantidade"), Tr.T("Unidade")]);
        resumo.Add([Tr.T("Filas"), lista.Rows, un]);
        resumo.Add([Tr.T("Vãos"), lista.Bays, un]);
        resumo.Add([Tr.T("Pórticos"), lista.Portals, un]);
        resumo.Add([Tr.T("Módulos"), lista.Modules, un]);
        resumo.Add([Tr.T("Potência"), Math.Round(lista.Kwp, 3), "kWp"]);
        foreach (var g in lista.Lines.Where(l => l.Length is not null).GroupBy(l => (l.Item, l.Section)))
            resumo.Add([Tr.F("{0} {1}: soma dos comprimentos", g.Key.Item, g.Key.Section), Math.Round(g.Sum(l => l.TotalLength ?? 0), 2), "m"]);

        var pecas = planilha.Sheet(Tr.T("Carport — peças"));
        pecas.Add([Tr.T("Peça"), Tr.T("Seção ou medida"), Tr.T("Comprimento (m)"), Tr.T("Quantidade"), Tr.T("Total (m)")]);
        foreach (var l in lista.Lines)
            pecas.Add([l.Item, l.Section, l.Length is { } c ? Math.Round(c, 2) : null, l.Quantity, l.TotalLength is { } t ? Math.Round(t, 2) : null]);
        pecas.Add([]);
        pecas.AddNote(Tr.T("Comprimento de corte arredondado ao centímetro, para cima. A terça é cortada a cada vão (emenda no pórtico). Parafusos e ligações não entram: dependem do fornecedor da estrutura."));

        var aba = planilha.Sheet(Tr.T("Carport — pilares"));
        aba.Add([Tr.T("Fila"), Tr.T("Pórtico"), Tr.T("Pilar"), Tr.T("Comprimento (m)"), Tr.T("Cota do chão (m)")]);
        foreach (var p in lista.Pillars)
            aba.Add([p.Row, p.Portal, p.Side, Math.Round(p.Length, 3), Math.Round(p.GroundZ, 3)]);
        if (lista.Pillars.Count > 0)
            aba.AddTotal([Tr.T("Total"), null, lista.Pillars.Count, Math.Round(lista.Pillars.Sum(p => p.Length), 3), null]);

        return planilha;
    }

    private static string Nome(CarportPart parte) => parte switch
    {
        CarportPart.Pillar => Tr.T("Pilar"),
        CarportPart.Beam => Tr.T("Viga"),
        CarportPart.Purlin => Tr.T("Terça"),
        CarportPart.Brace => Tr.T("Mão francesa"),
        CarportPart.BasePlate => Tr.T("Chapa de base"),
        CarportPart.Pedestal => Tr.T("Bloco de base"),
        _ => parte.ToString(),
    };

    private static int Ordem(string item)
    {
        string[] ordem = [Nome(CarportPart.Pillar), Nome(CarportPart.Beam), Nome(CarportPart.Brace), Nome(CarportPart.Purlin),
            Nome(CarportPart.BasePlate), Nome(CarportPart.Pedestal), Tr.T("Módulo"), Tr.T("Grampo intermediário de módulo"), Tr.T("Grampo de ponta de módulo")];
        var i = Array.IndexOf(ordem, item);
        return i < 0 ? ordem.Length : i;
    }
}
