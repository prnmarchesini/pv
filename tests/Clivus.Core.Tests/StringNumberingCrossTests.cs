namespace Clivus.Core.Tests;

/// <summary>
/// Numeração das strings, 05/10/2026: cada bloco (e a usina inteira) com os
/// dois sentidos, o que avança e o de dentro da faixa; o registro
/// NUMERACAO_VARREDURA no formato 2, lendo o 1; e a contagem de strings por
/// bloco (o resumo da lista).
/// </summary>
public class StringNumberingCrossTests
{
    private static Guid G(int n) => new(n, 0, 0, new byte[8]);

    private static ScanItem Em(int n, double x, double y, Guid mesa = default) => new(G(n), x, y, mesa);

    private static int[] Numeros(IEnumerable<Guid> ids) => ids.Select(g => int.Parse(g.ToString("N")[..8], System.Globalization.NumberStyles.HexNumber)).ToArray();

    /// <summary>
    /// Duas mesas com quatro strings cada, em duas colunas de duas (planta):
    /// mesa A: 1 (0,10)  3 (20,10)     mesa B: 5 (100,10)  7 (120,10)
    ///         2 (0, 0)  4 (20, 0)             6 (100, 0)  8 (120, 0)
    /// </summary>
    private static readonly Guid MesaA = Guid.NewGuid();
    private static readonly Guid MesaB = Guid.NewGuid();

    private static ScanItem[] DuasMesas() =>
    [
        Em(4, 20, 0, MesaA), Em(1, 0, 10, MesaA), Em(3, 20, 10, MesaA), Em(2, 0, 0, MesaA),
        Em(8, 120, 0, MesaB), Em(5, 100, 10, MesaB), Em(7, 120, 10, MesaB), Em(6, 100, 0, MesaB),
    ];

    [Fact]
    [Trait("Etapa", "15")]
    public void OBlocoUsaOSentidoNaFaixaDele()
    {
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var bloco = setup.AddBlock();
        setup.SetTables(bloco.Id, [MesaA]);

        // Sem escolha: na faixa (coluna), de cima para baixo, como antes.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], Numeros(setup.Sequence(DuasMesas())));

        // O bloco de baixo para cima na faixa; a usina (mesa B) não muda.
        Assert.True(setup.SetCross(bloco.Id, ScanDirection.BottomToTop));
        Assert.Equal([2, 1, 4, 3, 5, 6, 7, 8], Numeros(setup.Sequence(DuasMesas())));

        // E o bloco da direita para a esquerda, de baixo para cima.
        Assert.True(setup.SetDirection(bloco.Id, ScanDirection.RightToLeft));
        Assert.Equal([4, 3, 2, 1, 5, 6, 7, 8], Numeros(setup.Sequence(DuasMesas())));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void AUsinaInteiraTambemTemOSentidoNaFaixa()
    {
        var setup = new ScanSetup(ScanDirection.TopToBottom, []);

        // De cima para baixo; na faixa (linha), da esquerda para a direita (o padrão).
        Assert.Equal(ScanDirection.LeftToRight, setup.DefaultCross);
        Assert.Equal([1, 3, 5, 7, 2, 4, 6, 8], Numeros(setup.Sequence(DuasMesas())));

        Assert.True(setup.SetDefaultCross(ScanDirection.RightToLeft));
        Assert.Equal([7, 5, 3, 1, 8, 6, 4, 2], Numeros(setup.Sequence(DuasMesas())));

        // Paralelo ao que avança: recusado, nada muda.
        Assert.False(setup.SetDefaultCross(ScanDirection.BottomToTop));
        Assert.Equal(ScanDirection.RightToLeft, setup.DefaultCross);

        // O bloco novo nasce com os dois sentidos da usina.
        var bloco = setup.AddBlock();
        Assert.Equal(ScanDirection.TopToBottom, bloco.Direction);
        Assert.Equal(ScanDirection.RightToLeft, bloco.Cross);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void SentidoNaFaixaParaleloEhRecusadoEMudarDeEixoVoltaAoPadrao()
    {
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var bloco = setup.AddBlock();

        Assert.False(setup.SetCross(bloco.Id, ScanDirection.RightToLeft));
        Assert.False(setup.SetCross(Guid.NewGuid(), ScanDirection.TopToBottom));
        Assert.True(setup.SetCross(bloco.Id, ScanDirection.BottomToTop));

        // No mesmo eixo (da esquerda para a direita -> da direita para a esquerda) o da faixa fica.
        setup.SetDirection(bloco.Id, ScanDirection.RightToLeft);
        Assert.Equal(ScanDirection.BottomToTop, setup.Find(bloco.Id)!.Cross);

        // O que avança vira vertical: na faixa vale o padrão (da esquerda para a direita)...
        setup.SetDirection(bloco.Id, ScanDirection.TopToBottom);
        Assert.Equal(ScanDirection.LeftToRight, setup.Find(bloco.Id)!.Cross);

        // ...e voltando ao horizontal, o padrão dele (de cima para baixo), o mesmo que o registro regravado dá.
        setup.SetDirection(bloco.Id, ScanDirection.LeftToRight);
        Assert.Equal(ScanDirection.TopToBottom, setup.Find(bloco.Id)!.Cross);
        var (relido, _) = ScanSetup.FromRows(setup.ToRows().Select(r => ScanRow.Parse(r.ToFields())!).ToList());
        Assert.Equal(setup.Find(bloco.Id)!.Cross, relido.Find(bloco.Id)!.Cross);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OFormato2GuardaOsDoisSentidosDaUsinaEDosBlocos()
    {
        var setup = new ScanSetup(ScanDirection.BottomToTop, []);
        setup.SetDefaultCross(ScanDirection.RightToLeft);
        var a = setup.AddBlock();
        var b = setup.AddBlock();
        setup.SetTables(a.Id, [MesaA]);
        setup.SetDirection(b.Id, ScanDirection.LeftToRight);
        setup.SetCross(b.Id, ScanDirection.BottomToTop);

        var texto = RecordTable.Write(ScanRow.Version, ScanRow.FieldCount, setup.ToRows(), r => r.ToFields());
        Assert.Equal(2, RecordTable.VersionOf(texto));

        var lido = RecordTable.Read(texto, ScanRow.Version, ScanRow.FieldCount, ScanRow.Parse, "da varredura");
        Assert.Null(lido.Problem);

        var (volta, perdidas) = ScanSetup.FromRows(lido.Items);
        Assert.Equal(0, perdidas);
        Assert.Equal(ScanDirection.BottomToTop, volta.DefaultDirection);
        Assert.Equal(ScanDirection.RightToLeft, volta.DefaultCross);
        Assert.Equal(setup.Blocks.Select(x => (x.Id, x.Name, x.Direction, x.Cross)), volta.Blocks.Select(x => (x.Id, x.Name, x.Direction, x.Cross)));
        Assert.Equal([MesaA], volta.Find(a.Id)!.Tables);
        Assert.Equal(ScanDirection.BottomToTop, volta.Find(b.Id)!.Cross);
        Assert.Equal(Numeros(setup.Sequence(DuasMesas())), Numeros(volta.Sequence(DuasMesas())));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OFormato1EhLidoComOSentidoNaFaixaDeAntesEAOrdemNaoMuda()
    {
        // Um registro gravado antes de 05/10/2026: 4 campos, sem o sentido na faixa.
        var bloco = Guid.NewGuid();
        List<string[]> antigas =
        [
            ["USINA", "", "", "TopToBottom"],
            ["BLOCO", bloco.ToString("D"), "Oeste", "RightToLeft"],
            ["MESA", bloco.ToString("D"), MesaA.ToString("D"), "RightToLeft"],
        ];
        var texto = RecordTable.Write(ScanRow.LegacyVersion, ScanRow.LegacyFieldCount, antigas, c => c);
        Assert.Equal(1, RecordTable.VersionOf(texto));

        var lido = RecordTable.Read(texto, ScanRow.LegacyVersion, ScanRow.LegacyFieldCount, ScanRow.ParseLegacy, "da varredura");
        Assert.Null(lido.Problem);
        var (setup, perdidas) = ScanSetup.FromRows(lido.Items);

        Assert.Equal(0, perdidas);
        Assert.Equal(ScanDirection.LeftToRight, setup.DefaultCross);
        Assert.Equal(ScanDirection.TopToBottom, setup.Blocks.Single().Cross);
        Assert.Equal([MesaA], setup.Blocks.Single().Tables);

        // A mesma ordem que a numeração dava antes: cada um com o sentido na faixa padrão.
        var antes = ScanOrder.Order(DuasMesas().Where(i => i.Table == MesaA).ToList(), ScanDirection.RightToLeft)
            .Concat(ScanOrder.Order(DuasMesas().Where(i => i.Table == MesaB).ToList(), ScanDirection.TopToBottom));
        Assert.Equal(Numeros(antes), Numeros(setup.Sequence(DuasMesas())));
        Assert.Equal([3, 4, 1, 2, 5, 7, 6, 8], Numeros(setup.Sequence(DuasMesas())));

        // Regravado, sai no formato 2, com o sentido na faixa explícito.
        Assert.All(setup.ToRows(), r => Assert.Equal(ScanRow.FieldCount, r.ToFields().Count));
        Assert.Equal("TopToBottom", setup.ToRows()[1].ToFields()[4]);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void LinhaDoFormato2ComFaixaEstranhaEhRecusadaOuCaiNoPadrao()
    {
        var bloco = Guid.NewGuid().ToString("D");

        Assert.Null(ScanRow.Parse(["BLOCO", bloco, "x", "LeftToRight", "Diagonal"]));
        Assert.Null(ScanRow.Parse(["BLOCO", bloco, "x", "LeftToRight", "3"]));
        Assert.Null(ScanRow.Parse(["BLOCO", bloco, "x", "LeftToRight"]));

        // Paralelo ao que avança: a linha vale, com o padrão na faixa (o bloco não se perde).
        var paralela = ScanRow.Parse(["BLOCO", bloco, "x", "LeftToRight", "RightToLeft"]);
        Assert.NotNull(paralela);
        Assert.Equal(ScanDirection.TopToBottom, paralela!.ValidCross);

        Assert.Equal(ScanDirection.BottomToTop, ScanRow.Parse(["BLOCO", bloco, "x", "LeftToRight", "BottomToTop"])!.ValidCross);

        // O formato 1 recusa o mesmo que recusava.
        Assert.Null(ScanRow.ParseLegacy(["BLOCO", bloco, "x", "Diagonal"]));
        Assert.Null(ScanRow.ParseLegacy(["OUTRO", "", "", "LeftToRight"]));
        Assert.Null(ScanRow.ParseLegacy(["BLOCO", "", "x", "LeftToRight"]));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ContaAsStringsDeCadaBlocoPelaMesaDoPrimeiroModulo()
    {
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var a = setup.AddBlock();
        var vazio = setup.AddBlock();
        setup.SetTables(a.Id, [MesaA]);

        var noA = Guid.NewGuid();
        var noB = Guid.NewGuid();
        var modulos = new Dictionary<Guid, ModuleSpot>
        {
            [noA] = new(MesaA, 0, 0),
            [noB] = new(MesaB, 100, 0),
        };

        ElectricalString S(Guid id, params Guid[] mods) => new(id, Guid.Empty, mods, Guid.Empty, string.Empty);
        var copia = Guid.NewGuid();
        var strings = new[]
        {
            S(Guid.NewGuid(), noA, noB),  // primeiro módulo na mesa A: do bloco
            S(copia, noA), S(copia, noA), // GUID repetido (cópia): conta uma vez
            S(Guid.NewGuid(), noB, noA),  // primeiro na mesa B: fora de bloco
            S(Guid.NewGuid(), Guid.NewGuid()), // módulo que não está no desenho
            S(Guid.NewGuid()),             // sem módulo
        };

        var conta = setup.CountStrings(strings, modulos);
        Assert.Equal(2, conta.ByBlock[a.Id]);
        Assert.Equal(0, conta.ByBlock[vazio.Id]);
        Assert.Equal(2, conta.InBlocks);
        Assert.Equal(1, conta.Outside);
        Assert.Equal(2, conta.Unplaced);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OsDoisSentidosParaOUsuario()
    {
        Assert.Equal("de cima para baixo; na faixa, da esquerda para a direita", ScanOrder.Describe(ScanDirection.TopToBottom, ScanDirection.LeftToRight));
        Assert.Equal(ScanDirection.TopToBottom, ScanOrder.ValidCross(ScanDirection.LeftToRight, null));
        Assert.Equal(ScanDirection.TopToBottom, ScanOrder.ValidCross(ScanDirection.LeftToRight, ScanDirection.RightToLeft));
        Assert.Equal(ScanDirection.BottomToTop, ScanOrder.ValidCross(ScanDirection.RightToLeft, ScanDirection.BottomToTop));
    }
}
