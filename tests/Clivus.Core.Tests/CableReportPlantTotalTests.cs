namespace Clivus.Core.Tests;

/// <summary>
/// Segunda rodada de 10/10/2026 no resumo de cabos (plano/melhorias-2026-10-10.md):
/// item 7 (a linha do total da usina, sempre no fim, na grade e no Exportar) e
/// item 8 (a corrente máxima do cabo no método da aba, o fator de correção à
/// vista e a corrente corrigida, que é a que o Suporta compara).
/// </summary>
public class CableReportPlantTotalTests
{
    private static readonly EquipmentSize Caixa = new(2, 1, 2.2);

    // UC1 (T1 com o Inversor 1) e UC2 (T2 com o Inversor 2), como as duas UCs do print do Renan.
    private static (ElectricalSetup Setup, Inverter[] Inv) Usina()
    {
        var u1 = new ConsumerUnit(Guid.NewGuid(), "UC1", "", ConsumerUnitMode.Unitary, Caixa);
        var u2 = new ConsumerUnit(Guid.NewGuid(), "UC2", "", ConsumerUnitMode.Unitary, Caixa);
        var t1 = new Transformer(Guid.NewGuid(), "Seco", "T1", 800, 13800, 2500, 4, 6.5, "", Caixa, u1.Id);
        var t2 = new Transformer(Guid.NewGuid(), "Seco", "T2", 800, 13800, 2500, 4, 6.5, "", Caixa, u2.Id);
        var modelo = new InverterModel(Guid.NewGuid(), "M", 2, 2, Caixa);
        var i1 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 1", t1.Id);
        var i2 = new Inverter(Guid.NewGuid(), modelo.Id, "Inversor 2", t2.Id);
        return (new ElectricalSetup(transformers: [t1, t2], inverters: [i1, i2], models: [modelo], units: [u1, u2]), [i1, i2]);
    }

    private static CableReport.CircuitRun Cc(Inverter inv, string tag, double comprimento, int vias = 1) =>
        new(CableRoute.DirectCurrent, new(CableEndKind.String, Guid.NewGuid()), new(CableEndKind.Inverter, inv.Id), tag, inv.Name, 2, comprimento, vias, null, "D");

    [Fact]
    [Trait("Etapa", "24")]
    public void OTotalDaUsinaSomaTodasAsUcs()
    {
        var (setup, inv) = Usina();
        var circuitos = new[] { Cc(inv[0], "S1", 100), Cc(inv[0], "S2", 50, vias: 2), Cc(inv[1], "S3", 30) };
        var grupos = CableReport.Group(circuitos, c => CableReport.GroupPath(setup, c));
        Assert.Equal(["UC1", "UC2"], grupos.Select(g => g.Name));

        // A linha da grade: soma das UCs, mesmo com os grupos fechados (é calculada dos grupos, não das linhas abertas).
        var total = CableReport.PlantTotal(grupos);
        Assert.Equal("Total da usina", total.Name);
        Assert.Equal(3, total.CircuitCount);
        Assert.Equal(6, total.Runs);
        Assert.Equal(180, total.Length, 9);
        Assert.Equal(2 + 4 + 2, total.Cables);
        Assert.Equal(100 + 100 + 30, total.CableLength, 9);
        Assert.Equal(grupos.Sum(g => g.Cables), total.Cables);
        Assert.Empty(total.Children);

        // Sem grupo nenhum, zero (a grade não mostra a linha, mas a conta não quebra).
        Assert.Equal(0, CableReport.PlantTotal([]).CircuitCount);

        // O Exportar: a tabela agrupada termina com o total da usina, igual ao da grade, e o CSV também.
        var tabela = CableReport.GroupedCircuits("Resumo CC", grupos, dc: true, routeColumn: false);
        var h = tabela.Header.ToList();
        Assert.Equal("Total da usina (3 circuito(s))", tabela.Total[0]);
        Assert.Equal((double)total.Runs, tabela.Total[h.IndexOf("Lances")]);
        Assert.Equal(total.Length, (double)tabela.Total[h.IndexOf("Comprimento (m)")]!, 9);
        Assert.Equal((double)total.Cables, tabela.Total[h.IndexOf("Cabos")]);
        Assert.Equal(total.CableLength, (double)tabela.Total[h.IndexOf("Total de cabo (m)")]!, 9);
        var ultima = tabela.ToCsv(System.Globalization.CultureInfo.InvariantCulture).TrimEnd().Split("\r\n")[^1];
        Assert.StartsWith("Total da usina (3 circuito(s));", ultima);
        Assert.Contains(";230;", ultima);
    }

    private static readonly PanModule Modulo = new("X", "M", 720, 50, 18, 42, 17, -0.125, 0.007, null);

    [Fact]
    [Trait("Etapa", "23")]
    public void ACorrenteMaximaOFatorEACorrigida()
    {
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc && c.SectionMm2 == 6);
        var metodo = cabo.Ampacity.Keys.First();
        var maxima = cabo.Ampacity[metodo];

        var conta = StringCheck.For(Modulo, 28, -5, cabo, metodo)!;

        // A máxima é a da biblioteca no método; o fator, por enquanto, 1,0; a corrigida, máxima × fator.
        Assert.Equal(maxima, conta.Ampacity);
        Assert.Equal(1.0, conta.CorrectionFactor);
        Assert.Equal(maxima, conta.CorrectedAmpacity!.Value, 9);

        // As colunas, na ordem, com as células alinhadas.
        var h = StringCheck.Headers().ToList();
        var c = conta.Cells();
        Assert.Equal(h.Count, c.Count);
        Assert.Equal(maxima, c[h.IndexOf("Corrente máxima do cabo (A)")]);
        Assert.Equal(1.0, c[h.IndexOf("Fator de correção")]);
        Assert.Equal(maxima, (double)c[h.IndexOf("Corrente corrigida (A)")]!, 9);
        Assert.True(h.IndexOf("Corrente máxima do cabo (A)") < h.IndexOf("Corrente corrigida (A)"));
        Assert.Equal(h.Count - 1, h.IndexOf("Suporta (Isc × 1,25)"));

        // O Suporta compara Isc × 1,25 com a CORRIGIDA: com o fator, quem passava deixa de passar.
        var justo = new StringCheck(1000, 800, 20, 19, 25);
        Assert.True(justo.Supports);
        var comFator = justo with { CorrectionFactor = 0.8 };
        Assert.Equal(20, comFator.CorrectedAmpacity!.Value, 9);
        Assert.False(comFator.Supports);
        Assert.Equal("Não", comFator.Cells()[^1]);

        // Sem a máxima no método: a máxima, a corrigida e o Suporta vazios; o fator continua à vista.
        var semMetodo = StringCheck.For(Modulo, 28, -5, cabo, "XYZ")!;
        var cs = semMetodo.Cells();
        Assert.Null(cs[h.IndexOf("Corrente máxima do cabo (A)")]);
        Assert.Null(cs[h.IndexOf("Corrente corrigida (A)")]);
        Assert.Null(cs[^1]);
        Assert.Equal(1.0, cs[h.IndexOf("Fator de correção")]);
    }

    /// <summary>As colunas novas também estão na tabela do Exportar (o resumo CC agrupado).</summary>
    [Fact]
    [Trait("Etapa", "24")]
    public void AsColunasDaCorrenteVaoParaOExportar()
    {
        var (setup, inv) = Usina();
        var cabo = CableLibrary.Default().First(c => c.Type == CableType.Dc && c.SectionMm2 == 6);
        var metodo = cabo.Ampacity.Keys.First();
        var circuito = Cc(inv[0], "S1", 100) with { Cable = cabo, Method = metodo };
        var grupos = CableReport.Group([circuito], c => CableReport.GroupPath(setup, c));
        var contas = new Dictionary<CableEnd, StringCheck> { [circuito.From] = StringCheck.For(Modulo, 28, -5, cabo, metodo)! };

        var tabela = CableReport.GroupedCircuits("Resumo CC", grupos, dc: true, routeColumn: false, contas);
        var h = tabela.Header.ToList();
        var linha = tabela.Rows.Single(r => r[1] is "S1");

        Assert.Equal(cabo.Ampacity[metodo], linha[h.IndexOf("Corrente máxima do cabo (A)")]);
        Assert.Equal(1.0, linha[h.IndexOf("Fator de correção")]);
        Assert.Equal(cabo.Ampacity[metodo], (double)linha[h.IndexOf("Corrente corrigida (A)")]!, 9);
        Assert.Contains("Corrente corrigida (A)", tabela.ToCsv(System.Globalization.CultureInfo.InvariantCulture).Split("\r\n")[0]);
    }
}
