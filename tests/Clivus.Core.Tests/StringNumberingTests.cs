namespace Clivus.Core.Tests;

/// <summary>Numeração das strings (elétrica, etapa 15): a composição da tag.</summary>
public class StringNumberingTests
{
    // ------------------------------------------------------------ 15.1 tag

    [Fact]
    [Trait("Etapa", "15")]
    public void ATagPadraoEhTrafoInversorStringComPonto()
    {
        Assert.Equal("T1.I1.S1", TagScheme.Default.Compose(1, 1, 1));
        Assert.Equal("T2.I13.S7", TagScheme.Default.Compose(2, 13, 7));
        Assert.Null(TagScheme.Default.Problem());
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void CadaPedacoTemOSeuPrefixoEOSeparadorEhOMesmo()
    {
        var risco = new TagScheme(true, "Trafo", "Inv", "S", "-");
        Assert.Equal("Trafo3-Inv2-S10", risco.Compose(3, 2, 10));

        // 1S1: sem o pedaço do trafo, inversor sem prefixo, colado.
        var colado = new TagScheme(false, "T", "", "S", "");
        Assert.Null(colado.Problem());
        Assert.Equal("1S1", colado.Compose(4, 1, 1));
        Assert.Equal("12S3", colado.Compose(null, 12, 3));

        var coladoComTrafo = new TagScheme(true, "T", "I", "S", "");
        Assert.Equal("T1I2S3", coladoComTrafo.Compose(1, 2, 3));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void InversorSemTrafoFicaSemOPedacoDoTrafo()
    {
        // Decisão de 04/10/2026: o pedaço do trafo some (com o separador),
        // em vez de um "T0" que parece um trafo de verdade.
        Assert.Equal("I4.S2", TagScheme.Default.Compose(null, 4, 2));
    }

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData(true, "T", "", "S", "")]      // T1 + 2 colados: T12S3 é T1.I2 ou T12?
    [InlineData(false, "", "I", "", "")]      // I2 + 3 colados
    [InlineData(true, "T", "I", "S", "/")]    // separador fora da lista
    [InlineData(true, "T1", "I", "S", ".")]   // prefixo terminando em algarismo
    [InlineData(true, "T", "I{", "S", ".")]   // caractere de formatação do texto
    [InlineData(true, "T", "I", "S\\", ".")]
    [InlineData(true, "T", "I", "Stringcomprida", ".")] // mais de 12 caracteres
    public void ComposicaoAmbiguaOuInvalidaEhRecusada(bool trafo, string t, string i, string s, string separador)
    {
        var esquema = new TagScheme(trafo, t, i, s, separador);
        Assert.NotNull(esquema.Problem());
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void NumeroForaDaFaixaNaoViraTag()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TagScheme.Default.Compose(1, 1, 0));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OEsquemaVaiEVoltaDosCampos()
    {
        var esquema = new TagScheme(false, "Trafo", "", "S", "");
        Assert.Equal(TagScheme.FieldCount, esquema.ToFields().Count);
        Assert.Equal(esquema, TagScheme.Parse(esquema.ToFields()));
        Assert.Equal(TagScheme.Default, TagScheme.Parse(TagScheme.Default.ToFields()));

        // Gravado estragado (separador que não existe) não volta.
        var estragado = TagScheme.Default.ToFields().ToArray();
        estragado[4] = "#";
        Assert.Null(TagScheme.Parse(estragado));
    }

    // ------------------------------------------------ 15.2 varredura e blocos

    private static ScanItem Em(int n, double x, double y) => new(new Guid(n, 0, 0, new byte[8]), x, y);

    private static int[] Numeros(IEnumerable<Guid> ids) => ids.Select(g => int.Parse(g.ToString("N")[..8], System.Globalization.NumberStyles.HexNumber)).ToArray();

    /// <summary>
    /// Quatro strings em duas colunas de duas (planta):
    /// 1 (0,10)  3 (20,10)
    /// 2 (0, 0)  4 (20, 0)
    /// </summary>
    private static readonly ScanItem[] Quadrado = [Em(4, 20, 0), Em(1, 0, 10), Em(3, 20, 10), Em(2, 0, 0)];

    [Theory]
    [Trait("Etapa", "15")]
    [InlineData(ScanDirection.LeftToRight, new[] { 1, 2, 3, 4 })]
    [InlineData(ScanDirection.RightToLeft, new[] { 3, 4, 1, 2 })]
    [InlineData(ScanDirection.TopToBottom, new[] { 1, 3, 2, 4 })]
    [InlineData(ScanDirection.BottomToTop, new[] { 2, 4, 1, 3 })]
    public void CadaSentidoVarreNoSeuSentido(ScanDirection sentido, int[] esperado)
    {
        // O sentido nomeado é o que avança; na mesma faixa, de cima para
        // baixo (sentido horizontal) ou da esquerda para a direita (vertical).
        Assert.Equal(esperado, Numeros(ScanOrder.Order(Quadrado, sentido)));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void QuaseAlinhadasContamComoAMesmaFaixa()
    {
        // Duas strings da mesma coluna com 3 cm de diferença em X: a de cima
        // vem antes, mesmo estando 3 cm à direita.
        var itens = new[] { Em(2, 0.00, 0), Em(1, 0.03, 2.2), Em(3, 10, 5) };
        Assert.Equal([1, 2, 3], Numeros(ScanOrder.Order(itens, ScanDirection.LeftToRight)));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void EmpateTotalDesempataPeloGuidSempreIgual()
    {
        var itens = new[] { Em(9, 5, 5), Em(3, 5, 5), Em(7, 5, 5) };
        Assert.Equal([3, 7, 9], Numeros(ScanOrder.Order(itens, ScanDirection.TopToBottom)));
        Assert.Equal([3, 7, 9], Numeros(ScanOrder.Order(itens.Reverse().ToArray(), ScanDirection.TopToBottom)));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void BlocoNovoGanhaNomeEmSequenciaEOSentidoDaUsina()
    {
        var setup = new ScanSetup(ScanDirection.TopToBottom, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();

        Assert.Equal(["Bloco 1", "Bloco 2"], setup.Blocks.Select(x => x.Name));
        Assert.Equal(ScanDirection.TopToBottom, a.Direction);
        Assert.Empty(b.Tables);

        Assert.True(setup.SetDirection(b.Id, ScanDirection.RightToLeft));
        Assert.Equal(ScanDirection.RightToLeft, setup.Find(b.Id)!.Direction);
        Assert.Equal(ScanDirection.TopToBottom, setup.Find(a.Id)!.Direction);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void MesaSoPertenceAUmBloco()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();

        Assert.Equal(0, setup.SetTables(a.Id, [m1, m2, m2]));
        // m2 passa para o bloco b: sai do a (conta quantas vieram de outro bloco).
        Assert.Equal(1, setup.SetTables(b.Id, [m2, m3]));

        Assert.Equal([m1], setup.Find(a.Id)!.Tables);
        Assert.Equal([m2, m3], setup.Find(b.Id)!.Tables);
        Assert.Equal(a.Id, setup.BlockOf(m1)?.Id);
        Assert.Equal(b.Id, setup.BlockOf(m2)?.Id);
        Assert.Null(setup.BlockOf(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void RenomearNaoRepeteEApagarSoltaAsMesas()
    {
        var m = Guid.NewGuid();
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();
        setup.SetTables(a.Id, [m]);

        Assert.NotNull(setup.Rename(b.Id, " bloco 1 "));
        Assert.NotNull(setup.Rename(b.Id, "  "));
        Assert.Null(setup.Rename(b.Id, " Norte "));
        Assert.Equal("Norte", setup.Find(b.Id)!.Name);

        Assert.True(setup.Remove(a.Id));
        Assert.Null(setup.BlockOf(m));
        Assert.False(setup.Remove(a.Id));

        // O nome novo vem depois do maior que existe.
        Assert.Equal("Bloco 1", setup.AddBlock().Name);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void AConfiguracaoVaiEVoltaDasLinhasDoRegistro()
    {
        var setup = new ScanSetup(ScanDirection.BottomToTop, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();
        var vazio = setup.AddBlock();
        setup.SetTables(a.Id, [Guid.NewGuid(), Guid.NewGuid()]);
        setup.SetTables(b.Id, [Guid.NewGuid()]);
        setup.SetDirection(b.Id, ScanDirection.RightToLeft);

        var linhas = setup.ToRows();
        Assert.All(linhas, l => Assert.Equal(ScanRow.FieldCount, l.ToFields().Count));

        var (volta, perdidas) = ScanSetup.FromRows(linhas.Select(l => ScanRow.Parse(l.ToFields())!).ToList());
        Assert.Equal(0, perdidas);
        Assert.Equal(ScanDirection.BottomToTop, volta.DefaultDirection);
        Assert.Equal(setup.Blocks.Select(x => (x.Id, x.Name, x.Direction)), volta.Blocks.Select(x => (x.Id, x.Name, x.Direction)));
        Assert.Equal(setup.Blocks.Select(x => x.Tables), volta.Blocks.Select(x => x.Tables));
        Assert.Empty(volta.Find(vazio.Id)!.Tables);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void LinhaDeMesaSemBlocoOuRepetidaEhPerdidaEContada()
    {
        var bloco = Guid.NewGuid();
        var mesa = Guid.NewGuid();
        var linhas = new List<ScanRow>
        {
            new(ScanRow.Plant, Guid.Empty, string.Empty, ScanDirection.LeftToRight),
            new(ScanRow.Block, bloco, "Bloco 1", ScanDirection.TopToBottom),
            new(ScanRow.Table, bloco, mesa.ToString("D"), ScanDirection.LeftToRight),
            new(ScanRow.Table, bloco, mesa.ToString("D"), ScanDirection.LeftToRight),
            new(ScanRow.Table, Guid.NewGuid(), Guid.NewGuid().ToString("D"), ScanDirection.LeftToRight),
        };

        var (setup, perdidas) = ScanSetup.FromRows(linhas);
        Assert.Equal(2, perdidas);
        Assert.Equal([mesa], setup.Blocks.Single().Tables);

        Assert.Null(ScanRow.Parse(["BLOCO", Guid.NewGuid().ToString("D"), "x", "Diagonal"]));
        Assert.Null(ScanRow.Parse(["OUTRO", "", "", "LeftToRight"]));
    }
}
