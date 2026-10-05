namespace Clivus.Core.Tests;

/// <summary>
/// A atribuição automática das strings nos inversores (pedido do Renan em
/// 05/10/2026): a varredura própria da alocação (sentido e sentido na faixa),
/// a mesma regra de ordem da numeração; as livres enchem os inversores na
/// ordem da lista até a capacidade; as já alocadas não mudam e contam.
/// </summary>
public class StringAutoAllocationTests
{
    private static readonly EquipmentSize Caixa = ElectricalDefaults.InverterSize;

    private static Guid G(int n) => new(n, 0, 0, new byte[8]);

    private static int N(Guid g) => int.Parse(g.ToString("N")[..8], System.Globalization.NumberStyles.HexNumber);

    /// <summary>
    /// Quatro strings em duas colunas de duas (planta), o primeiro módulo de cada uma:
    /// 1 (0,10)  3 (20,10)
    /// 2 (0, 0)  4 (20, 0)
    /// </summary>
    private static readonly ScanItem[] Quadrado = [new(G(4), 20, 0), new(G(1), 0, 10), new(G(3), 20, 10), new(G(2), 0, 0)];

    /// <summary>Uma string por item do quadrado: módulo 100+n na posição dele.</summary>
    private static (List<ElectricalString> Strings, Dictionary<Guid, ModuleSpot> Modulos) Usina(IEnumerable<ScanItem> itens)
    {
        var strings = new List<ElectricalString>();
        var modulos = new Dictionary<Guid, ModuleSpot>();
        foreach (var i in itens)
        {
            var modulo = G(100 + N(i.Id));
            strings.Add(new ElectricalString(i.Id, Guid.Empty, [modulo], Guid.Empty, string.Empty));
            modulos[modulo] = new ModuleSpot(G(900), i.X, i.Y);
        }

        return (strings, modulos);
    }

    private static (InverterModel Modelo, Inverter[] Inversores) Inversores(int entradas, int quantos)
    {
        var modelo = new InverterModel(Guid.NewGuid(), "M", 1, entradas, Caixa);
        return (modelo, Enumerable.Range(1, quantos).Select(n => new Inverter(G(500 + n), modelo.Id, $"Inversor {n}", Guid.Empty)).ToArray());
    }

    private static Dictionary<int, int> DeQuem(AutoAllocationResult r) => r.Changed.ToDictionary(s => N(s.Id), s => N(s.Inverter) - 500);

    // ------------------------------------------------ a varredura (ScanOrder)

    [Theory]
    [Trait("Etapa", "14")]
    [InlineData(ScanDirection.TopToBottom, ScanDirection.LeftToRight, new[] { 1, 3, 2, 4 })]
    [InlineData(ScanDirection.TopToBottom, ScanDirection.RightToLeft, new[] { 3, 1, 4, 2 })]
    [InlineData(ScanDirection.BottomToTop, ScanDirection.LeftToRight, new[] { 2, 4, 1, 3 })]
    [InlineData(ScanDirection.BottomToTop, ScanDirection.RightToLeft, new[] { 4, 2, 3, 1 })]
    [InlineData(ScanDirection.LeftToRight, ScanDirection.TopToBottom, new[] { 1, 2, 3, 4 })]
    [InlineData(ScanDirection.LeftToRight, ScanDirection.BottomToTop, new[] { 2, 1, 4, 3 })]
    [InlineData(ScanDirection.RightToLeft, ScanDirection.TopToBottom, new[] { 3, 4, 1, 2 })]
    [InlineData(ScanDirection.RightToLeft, ScanDirection.BottomToTop, new[] { 4, 3, 2, 1 })]
    public void ASentidoNaFaixaEscolhidoMudaSoAOrdemDentroDaFaixa(ScanDirection sentido, ScanDirection faixa, int[] esperado)
    {
        Assert.Equal(esperado, ScanOrder.Order(Quadrado, sentido, ScanOrder.Band, faixa).Select(N));
    }

    [Theory]
    [Trait("Etapa", "14")]
    [InlineData(ScanDirection.LeftToRight)]
    [InlineData(ScanDirection.RightToLeft)]
    [InlineData(ScanDirection.TopToBottom)]
    [InlineData(ScanDirection.BottomToTop)]
    public void SemOSentidoNaFaixaAOrdemEADaNumeracao(ScanDirection sentido)
    {
        // A numeração chama sem o sentido na faixa: tem que dar o de antes.
        Assert.Equal(ScanOrder.Order(Quadrado, sentido), ScanOrder.Order(Quadrado, sentido, ScanOrder.Band, ScanOrder.DefaultCross(sentido)));
        Assert.Equal(2, ScanOrder.CrossOptions(sentido).Count);
        Assert.All(ScanOrder.CrossOptions(sentido), f => Assert.True(ScanOrder.IsPerpendicular(sentido, f)));
        Assert.Throws<ArgumentException>(() => ScanOrder.Order(Quadrado, sentido, ScanOrder.Band, sentido));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AVarreduraDaAlocacaoVaiEVoltaERecusaSentidosNoMesmoEixo()
    {
        var v = new AllocationScan(ScanDirection.BottomToTop, ScanDirection.RightToLeft);

        Assert.Equal(v, AllocationScan.Parse(v.ToFields()));
        Assert.Equal(AllocationScan.FieldCount, v.ToFields().Count);
        Assert.Null(AllocationScan.Parse(["TopToBottom", "BottomToTop"]));
        Assert.Null(AllocationScan.Parse(["0", "2"]));
        Assert.Null(AllocationScan.Parse(["Diagonal", "LeftToRight"]));
        Assert.True(AllocationScan.Default.IsValid);
    }

    // ------------------------------------------------ a atribuição

    [Fact]
    [Trait("Etapa", "14")]
    public void AsLivresEnchemOsInversoresNaOrdemDaListaSeguindoAVarredura()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 2, quantos: 2);

        var r = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, AllocationScan.Default);

        // De cima para baixo, na faixa da esquerda para a direita: 1, 3 | 2, 4.
        Assert.Equal(new Dictionary<int, int> { [1] = 1, [3] = 1, [2] = 2, [4] = 2 }, DeQuem(r));
        Assert.Equal([1, 3, 2, 4], r.Changed.Select(s => N(s.Id)));
        Assert.Equal(0, r.Leftover);

        var direitaParaEsquerda = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, new AllocationScan(ScanDirection.TopToBottom, ScanDirection.RightToLeft));
        Assert.Equal(new Dictionary<int, int> { [3] = 1, [1] = 1, [4] = 2, [2] = 2 }, DeQuem(direitaParaEsquerda));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void AsJaAlocadasNaoMudamEContamNaCapacidade()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 2, quantos: 2);

        // A 1 já é do Inversor 2: o 1 leva 3 e 2; o 2 (que já tem a 1) leva só a 4.
        strings[1] = strings[1] with { Inverter = inversores[1].Id };

        var r = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal(new Dictionary<int, int> { [3] = 1, [2] = 1, [4] = 2 }, DeQuem(r));
        Assert.DoesNotContain(r.Changed, s => N(s.Id) == 1);
        Assert.Equal(new Dictionary<Guid, int> { [inversores[0].Id] = 2, [inversores[1].Id] = 1 }, r.Added);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void InversorCheioEPuladoEASobraEContada()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 1, quantos: 2);

        // O Inversor 1 já está cheio com uma string de fora do quadrado (sem posição, já alocada).
        strings.Add(new ElectricalString(G(50), Guid.Empty, [G(150)], inversores[0].Id, string.Empty));

        var r = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal([inversores[0].Id], r.Full);
        Assert.Equal(new Dictionary<int, int> { [1] = 2 }, DeQuem(r));
        Assert.Equal(3, r.Leftover);
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void NenhumaStringFicaEmDoisInversores()
    {
        var itens = Enumerable.Range(1, 30).Select(n => new ScanItem(G(n), n % 6 * 5.0, n / 6 * 3.0)).ToArray();
        var (strings, modulos) = Usina(itens);
        var (modelo, inversores) = Inversores(entradas: 7, quantos: 5);
        strings[4] = strings[4] with { Inverter = inversores[2].Id };

        var r = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal(r.Changed.Count, r.Changed.Select(s => s.Id).Distinct().Count());
        Assert.DoesNotContain(r.Changed, s => s.Id == strings[4].Id);
        Assert.All(r.Changed, s => Assert.False(strings.Single(x => x.Id == s.Id).IsAllocated));
        Assert.Equal(29, r.Changed.Count);    // 35 entradas, 1 já ocupada: as 29 livres cabem
        Assert.All(inversores, i => Assert.True(r.Changed.Count(s => s.Inverter == i.Id) + strings.Count(s => s.Inverter == i.Id) <= 7));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemModeloSemPosicaoECopiaFicamDeFora()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 10, quantos: 1);
        var semModelo = new Inverter(G(600), Guid.NewGuid(), "Sem modelo", Guid.Empty);

        strings.Add(new ElectricalString(G(60), Guid.Empty, [G(160)], Guid.Empty, string.Empty));   // módulo fora do desenho
        strings.Add(strings[0]);                                                                       // cópia da 4

        var r = StringAutoAllocation.Allocate([semModelo, .. inversores], [modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal([semModelo.Id], r.WithoutModel);
        Assert.Equal(1, r.Unplaced);
        Assert.Equal(2, r.Duplicates);
        Assert.Equal([1, 3, 2], r.Changed.Select(s => N(s.Id)));
        Assert.All(r.Changed, s => Assert.Equal(inversores[0].Id, s.Inverter));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void StringDeInversorQueSumiuDoCadastroContaComoLivre()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 10, quantos: 1);
        strings[1] = strings[1] with { Inverter = G(777) };   // a 1, de um inversor apagado

        var r = StringAutoAllocation.Allocate(inversores, [modelo], strings, modulos, AllocationScan.Default);

        Assert.Equal([1, 3, 2, 4], r.Changed.Select(s => N(s.Id)));
        Assert.All(r.Changed, s => Assert.Equal(inversores[0].Id, s.Inverter));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void SemStringLivreNadaMuda()
    {
        var (strings, modulos) = Usina(Quadrado);
        var (modelo, inversores) = Inversores(entradas: 10, quantos: 1);
        var todas = strings.Select(s => s with { Inverter = inversores[0].Id }).ToList();

        var r = StringAutoAllocation.Allocate(inversores, [modelo], todas, modulos, AllocationScan.Default);

        Assert.Empty(r.Changed);
        Assert.Equal(0, r.Leftover);
    }
}
