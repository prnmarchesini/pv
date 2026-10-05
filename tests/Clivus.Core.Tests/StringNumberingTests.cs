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
    [InlineData(true, "T%%", "I", "S", ".")]  // %%d no texto do CAD vira símbolo
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

    // ------------------------------------------------ 15.3 ordem dos blocos

    [Fact]
    [Trait("Etapa", "15")]
    public void SubirEDescerTrocaAOrdemDaLista()
    {
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();
        var c = setup.AddBlock();

        Assert.True(setup.Move(c.Id, -1));
        Assert.Equal([a.Id, c.Id, b.Id], setup.Blocks.Select(x => x.Id));
        Assert.True(setup.Move(a.Id, +2));
        Assert.Equal([c.Id, b.Id, a.Id], setup.Blocks.Select(x => x.Id));

        // Na ponta não anda, e bloco que não existe também não.
        Assert.False(setup.Move(c.Id, -1));
        Assert.False(setup.Move(a.Id, +1));
        Assert.False(setup.Move(Guid.NewGuid(), +1));
        Assert.Equal([c.Id, b.Id, a.Id], setup.Blocks.Select(x => x.Id));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ASequenciaNumeraUmBlocoInteiroDepoisOOutroEOResto()
    {
        var oeste = Guid.NewGuid();
        var leste = Guid.NewGuid();
        var solta = Guid.NewGuid();

        // Oeste: 1 (0,10) e 2 (0,0); leste: 3 (20,10) e 4 (20,0); solta: 5 (40,5).
        var itens = new[]
        {
            Em(1, 0, 10) with { Table = oeste }, Em(2, 0, 0) with { Table = oeste },
            Em(3, 20, 10) with { Table = leste }, Em(4, 20, 0) with { Table = leste },
            Em(5, 40, 5) with { Table = solta },
        };

        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var a = setup.AddBlock();
        var b = setup.AddBlock();
        setup.SetTables(a.Id, [oeste]);
        setup.SetTables(b.Id, [leste]);
        setup.SetDirection(a.Id, ScanDirection.BottomToTop);
        setup.SetDirection(b.Id, ScanDirection.TopToBottom);

        Assert.Equal([2, 1, 3, 4, 5], Numeros(setup.Sequence(itens)));

        // Reordenar a lista muda a sequência: o leste inteiro antes do oeste.
        setup.Move(b.Id, -1);
        Assert.Equal([3, 4, 2, 1, 5], Numeros(setup.Sequence(itens)));

        // Sem blocos, a usina inteira no sentido padrão.
        Assert.Equal([1, 2, 3, 4, 5], Numeros(new ScanSetup(ScanDirection.LeftToRight, []).Sequence(itens)));
    }

    // ------------------------------------------------------ 15.4 gerar

    private static readonly EquipmentSize Caixa = new(1, 1, 2);

    private static Transformer Trafo(string apelido) => new(Guid.NewGuid(), "Trafo", apelido, 800, 13800, 2500, 0, 6, "", Caixa, Guid.Empty);

    /// <summary>Uma usina pequena: dois trafos, três inversores (o 3 sem trafo), mesas oeste (x 0) e leste (x 20).</summary>
    private sealed class Usina
    {
        public readonly Transformer T1 = Trafo("TA");
        public readonly Transformer T2 = Trafo("TB");
        public readonly Inverter I1;
        public readonly Inverter I2;
        public readonly Inverter I3;
        public readonly Guid Oeste = Guid.NewGuid();
        public readonly Guid Leste = Guid.NewGuid();
        public readonly Dictionary<Guid, ModuleSpot> Modulos = [];
        public readonly List<ElectricalString> Strings = [];

        public Usina()
        {
            var modelo = Guid.NewGuid();
            I1 = new Inverter(Guid.NewGuid(), modelo, "Inversor 1", T1.Id);
            I2 = new Inverter(Guid.NewGuid(), modelo, "Inversor 2", T2.Id);
            I3 = new Inverter(Guid.NewGuid(), modelo, "Inversor 3", Guid.Empty);
        }

        public IReadOnlyList<Transformer> Trafos => [T1, T2];

        public IReadOnlyList<Inverter> Inversores => [I1, I2, I3];

        /// <summary>Uma string com o primeiro módulo em (x, y) na mesa dada, mais um módulo ao lado.</summary>
        public ElectricalString Str(Guid inversor, Guid mesa, double x, double y)
        {
            var primeiro = Guid.NewGuid();
            var segundo = Guid.NewGuid();
            Modulos[primeiro] = new ModuleSpot(mesa, x, y);
            Modulos[segundo] = new ModuleSpot(mesa, x + 1, y);
            var s = new ElectricalString(Guid.NewGuid(), Guid.Empty, [primeiro, segundo], inversor, "velha");
            Strings.Add(s);
            return s;
        }
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void GerarNumeraPorInversorNaOrdemDaVarredura()
    {
        var u = new Usina();
        var a = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var b = u.Str(u.I1.Id, u.Oeste, 0, 10);
        var c = u.Str(u.I1.Id, u.Leste, 20, 5);
        var d = u.Str(u.I2.Id, u.Leste, 20, 0);
        var e = u.Str(u.I2.Id, u.Oeste, 0, 5);
        var f = u.Str(u.I3.Id, u.Leste, 20, 10);

        var r = StringNumbering.Number(TagScheme.Default, new ScanSetup(ScanDirection.LeftToRight, []), u.Trafos, u.Inversores, u.Strings, u.Modulos);

        // Da esquerda para a direita; na mesma coluna, de cima para baixo.
        // O sequencial recomeça em cada inversor; o inversor 3 não tem trafo.
        Assert.Equal("T1.I1.S1", r.Tags[b.Id]);
        Assert.Equal("T1.I1.S2", r.Tags[a.Id]);
        Assert.Equal("T1.I1.S3", r.Tags[c.Id]);
        Assert.Equal("T2.I2.S1", r.Tags[e.Id]);
        Assert.Equal("T2.I2.S2", r.Tags[d.Id]);
        Assert.Equal("I3.S1", r.Tags[f.Id]);
        Assert.Equal([u.I3.Id], r.InvertersWithoutTransformer);
        Assert.Equal(0, r.Free);
        Assert.Equal(6, r.Tagged);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void OsBlocosNaOrdemDaListaMandamNaSequencia()
    {
        var u = new Usina();
        var a = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var b = u.Str(u.I1.Id, u.Oeste, 0, 10);
        var c = u.Str(u.I1.Id, u.Leste, 20, 0);
        var d = u.Str(u.I1.Id, u.Leste, 20, 10);

        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var leste = setup.AddBlock();
        var oeste = setup.AddBlock();
        setup.SetTables(leste.Id, [u.Leste]);
        setup.SetTables(oeste.Id, [u.Oeste]);
        setup.SetDirection(oeste.Id, ScanDirection.BottomToTop);

        var r = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos);
        Assert.Equal(["T1.I1.S1", "T1.I1.S2", "T1.I1.S3", "T1.I1.S4"], new[] { d, c, a, b }.Select(x => r.Tags[x.Id]));

        // Inverte a ordem dos dois blocos: o oeste inteiro vem antes.
        setup.Move(oeste.Id, -1);
        r = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos);
        Assert.Equal(["T1.I1.S1", "T1.I1.S2", "T1.I1.S3", "T1.I1.S4"], new[] { a, b, d, c }.Select(x => r.Tags[x.Id]));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void StringSemInversorOuSemPosicaoFicaSemTagEContada()
    {
        var u = new Usina();
        var livre = u.Str(Guid.Empty, u.Oeste, 0, 0);
        var fantasma = u.Str(Guid.NewGuid(), u.Oeste, 0, 5);
        var sumida = u.Str(u.I1.Id, u.Oeste, 0, 10);
        u.Modulos.Remove(sumida.Modules[0]);
        var boa = u.Str(u.I1.Id, u.Leste, 20, 0);

        var r = StringNumbering.Number(TagScheme.Default, new ScanSetup(ScanDirection.LeftToRight, []), u.Trafos, u.Inversores, u.Strings, u.Modulos);

        Assert.Equal(string.Empty, r.Tags[livre.Id]);
        Assert.Equal(string.Empty, r.Tags[fantasma.Id]);
        Assert.Equal(string.Empty, r.Tags[sumida.Id]);
        Assert.Equal("T1.I1.S1", r.Tags[boa.Id]);
        Assert.Equal((1, 1, 1, 1), (r.Free, r.UnknownInverter, r.Unplaced, r.Tagged));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ONumeroDoTrafoEDoInversorEhAPosicaoNaLista()
    {
        var u = new Usina();
        var s = u.Str(u.I2.Id, u.Oeste, 0, 0);

        // A lista de inversores com o 2 na frente: ele vira o inversor 1.
        var r = StringNumbering.Number(TagScheme.Default, new ScanSetup(ScanDirection.LeftToRight, []), [u.T2, u.T1], [u.I2, u.I1, u.I3], u.Strings, u.Modulos);
        Assert.Equal("T1.I1.S1", r.Tags[s.Id]);

        // Trafo que sumiu do cadastro: o inversor fica sem o pedaço do trafo, e avisado.
        r = StringNumbering.Number(TagScheme.Default, new ScanSetup(ScanDirection.LeftToRight, []), [u.T1], u.Inversores, u.Strings, u.Modulos);
        Assert.Equal("I2.S1", r.Tags[s.Id]);
        Assert.Equal([u.I2.Id], r.InvertersWithoutTransformer);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void AOrdemDeEntradaNaoMudaONumero()
    {
        var u = new Usina();
        for (var i = 0; i < 12; i++) u.Str(i % 2 == 0 ? u.I1.Id : u.I2.Id, i < 6 ? u.Oeste : u.Leste, i < 6 ? 0 : 20, i * 3);

        var setup = new ScanSetup(ScanDirection.TopToBottom, []);
        var uma = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos);
        var outra = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings.AsEnumerable().Reverse().ToList(), u.Modulos);

        Assert.Equal(uma.Tags.OrderBy(x => x.Key), outra.Tags.OrderBy(x => x.Key));
        Assert.Equal(12, uma.Tags.Values.Distinct().Count());
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ATagDesenhadaVaiEVoltaDoXData()
    {
        var t = new StringTagText(Guid.NewGuid(), "T1.I2.S3");
        Assert.Equal(t, StringTagText.Parse(t.ToFields()));
        Assert.Null(StringTagText.Parse(["nao-e-guid", "T1"]));
        Assert.Null(StringTagText.Parse([Guid.Empty.ToString("D"), "T1"]));
    }

    // ------------------------------------------------ 15.5 edição granular

    /// <summary>As strings com as tags aplicadas (como ficariam gravadas no desenho).</summary>
    private static List<ElectricalString> Aplicar(IEnumerable<ElectricalString> strings, IReadOnlyDictionary<Guid, string> tags) =>
        strings.Select(s => tags.TryGetValue(s.Id, out var t) ? s with { Tag = t } : s).ToList();

    [Fact]
    [Trait("Etapa", "15")]
    public void RegerarUmBlocoNaoMexeNosOutrosEDaOMesmoNumeroQueGerarTudo()
    {
        var u = new Usina();
        var a1 = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var a2 = u.Str(u.I1.Id, u.Oeste, 0, 10);
        var b1 = u.Str(u.I1.Id, u.Leste, 20, 0);
        var b2 = u.Str(u.I1.Id, u.Leste, 20, 10);
        var b3 = u.Str(u.I2.Id, u.Leste, 25, 5);

        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var oeste = setup.AddBlock();
        var leste = setup.AddBlock();
        setup.SetTables(oeste.Id, [u.Oeste]);
        setup.SetTables(leste.Id, [u.Leste]);

        var tudo = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos);
        var gravadas = Aplicar(u.Strings, tudo.Tags);
        Assert.Equal(["T1.I1.S3", "T1.I1.S4"], new[] { b2, b1 }.Select(x => tudo.Tags[x.Id]));

        // Muda o sentido do leste e regera só ele.
        setup.SetDirection(leste.Id, ScanDirection.BottomToTop);
        var bloco = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos, NumberingScope.OfBlock(leste.Id));

        Assert.Equal([b1.Id, b2.Id, b3.Id], bloco.Tags.Keys.OrderBy(k => u.Strings.FindIndex(x => x.Id == k)));
        Assert.Equal(["T1.I1.S3", "T1.I1.S4", "T2.I2.S1"], new[] { b1, b2, b3 }.Select(x => bloco.Tags[x.Id]));
        Assert.False(bloco.Tags.ContainsKey(a1.Id));
        Assert.Empty(bloco.DuplicateTags);

        // O mesmo que gerar tudo com o sentido novo.
        var deNovo = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos);
        foreach (var (id, tag) in bloco.Tags) Assert.Equal(deNovo.Tags[id], tag);
        Assert.Equal(tudo.Tags[a1.Id], deNovo.Tags[a1.Id]);
        Assert.Equal(tudo.Tags[a2.Id], deNovo.Tags[a2.Id]);

        // As mesas fora de bloco são um alcance também (aqui, nenhuma string).
        Assert.Empty(StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos, NumberingScope.OfBlock(Guid.Empty)).Tags);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void RefazerUmInversorSoTocaAsStringsDele()
    {
        var u = new Usina();
        var a = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var b = u.Str(u.I2.Id, u.Oeste, 0, 10);
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var gravadas = Aplicar(u.Strings, StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos).Tags);

        // Uma string nova alocada ao inversor 1, acima da a: refazer o 1 a numera.
        var nova = u.Str(u.I1.Id, u.Leste, 0, 20);
        gravadas.Add(nova with { Tag = string.Empty });
        var r = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos, NumberingScope.OfInverter(u.I1.Id));

        Assert.Equal(["T1.I1.S1", "T1.I1.S2"], new[] { nova, a }.Select(x => r.Tags[x.Id]));
        Assert.False(r.Tags.ContainsKey(b.Id));
        Assert.Equal(2, r.Tagged);
        Assert.Equal(0, r.Free);
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void ApagarPorInversorOuTudoSoEsvaziaATag()
    {
        var u = new Usina();
        var a = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var b = u.Str(u.I2.Id, u.Oeste, 0, 10);
        var livre = u.Str(Guid.Empty, u.Leste, 20, 0);
        var setup = new ScanSetup(ScanDirection.LeftToRight, []);

        var doInversor = StringNumbering.Clear(u.Strings, NumberingScope.OfInverter(u.I1.Id), setup, u.Modulos);
        Assert.Equal([a.Id], doInversor.Keys);
        Assert.Equal(string.Empty, doInversor[a.Id]);

        var tudo = StringNumbering.Clear(u.Strings, NumberingScope.All, setup, u.Modulos);
        Assert.Equal(new[] { a.Id, b.Id, livre.Id }.OrderBy(x => x), tudo.Keys.OrderBy(x => x));

        // Inversor vazio (Guid.Empty) não pega as strings livres.
        Assert.Empty(StringNumbering.Clear(u.Strings, NumberingScope.OfInverter(Guid.Empty), setup, u.Modulos));
    }

    [Fact]
    [Trait("Etapa", "15")]
    public void RegerarUmPedacoDepoisDeMudarAOrdemAvisaATagRepetida()
    {
        var u = new Usina();
        var a = u.Str(u.I1.Id, u.Oeste, 0, 0);
        var b = u.Str(u.I1.Id, u.Leste, 20, 0);
        var c = u.Str(u.I1.Id, u.Leste, 20, 10);

        var setup = new ScanSetup(ScanDirection.LeftToRight, []);
        var oeste = setup.AddBlock();
        var leste = setup.AddBlock();
        setup.SetTables(oeste.Id, [u.Oeste]);
        setup.SetTables(leste.Id, [u.Leste]);
        var gravadas = Aplicar(u.Strings, StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, u.Strings, u.Modulos).Tags);

        // O leste sobe na lista e só o oeste é regerado: a vira S3, e o c ainda é S3.
        setup.Move(leste.Id, -1);
        var r = StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos, NumberingScope.OfBlock(oeste.Id));

        Assert.Equal("T1.I1.S3", r.Tags[a.Id]);
        Assert.Equal(["T1.I1.S3"], r.DuplicateTags);

        // Gerar tudo resolve.
        Assert.Empty(StringNumbering.Number(TagScheme.Default, setup, u.Trafos, u.Inversores, gravadas, u.Modulos).DuplicateTags);
        _ = (b, c);
    }
}
