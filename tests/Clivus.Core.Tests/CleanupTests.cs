namespace Clivus.Core.Tests;

/// <summary>
/// O Apagar da Edição (05/10/2026): que entidade cada opção leva, a cor
/// original de cada peça de mesa, as contagens da janela e as opções
/// digitadas na linha de comando.
/// </summary>
public class CleanupTests
{
    [Fact]
    [Trait("Etapa", "8")]
    public void SoONossoXDataEClassificado()
    {
        Assert.Equal(CleanupTarget.None, Cleanup.Classify(null, isText: true));
        Assert.Equal(CleanupTarget.None, Cleanup.Classify(string.Empty, isText: false));
        Assert.Equal(CleanupTarget.None, Cleanup.Classify("QualquerCoisaDoUsuario", isText: true));

        // As peças da mesa, a área, o alinhamento, a árvore e a marca do grupo não são apagadas por nenhuma opção.
        foreach (var tipo in new[] { TableIdentity.Tipo, PillarIdentity.Tipo, ModuleIdentity.Tipo, FaceIdentity.Tipo, GroupMarkIdentity.Tipo, AreaIdentity.Tipo, AlignmentIdentity.Tipo, TreeIdentity.Tipo })
        {
            Assert.Equal(CleanupTarget.None, Cleanup.Classify(tipo, isText: false));
            Assert.Equal(CleanupTarget.None, Cleanup.Classify(tipo, isText: true));
        }
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CadaTipoDoPluginTemOSeuAlvo()
    {
        Assert.Equal(CleanupTarget.Annotation, Cleanup.Classify(NoteIdentity.Tipo, isText: false));
        Assert.Equal(CleanupTarget.Annotation, Cleanup.Classify(AnalysisTextIdentity.Tipo, isText: true));
        Assert.Equal(CleanupTarget.Annotation, Cleanup.Classify(TagIdentity.Tipo, isText: true));
        Assert.Equal(CleanupTarget.StringTag, Cleanup.Classify(StringTagText.Tipo, isText: true));
        Assert.Equal(CleanupTarget.StringSign, Cleanup.Classify(StringSign.Tipo, isText: true));
        Assert.Equal(CleanupTarget.StringSign, Cleanup.Classify(StringSign.Tipo, isText: false));
        Assert.Equal(CleanupTarget.StringPath, Cleanup.Classify(ElectricalString.Tipo, isText: false));
        Assert.Equal(CleanupTarget.ShadowOutline, Cleanup.Classify(Cleanup.ShadowType, isText: false));
        Assert.Equal(CleanupTarget.ShadowLabel, Cleanup.Classify(Cleanup.ShadowType, isText: true));
        Assert.Equal(CleanupTarget.Equipment, Cleanup.Classify(EquipmentPlacement.Tipo, isText: false));
        Assert.Equal(CleanupTarget.TransformerArea, Cleanup.Classify(TransformerAreaMark.Tipo, isText: false));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(CleanupTarget.Annotation, CleanupOptions.Texts)]
    [InlineData(CleanupTarget.StringTag, CleanupOptions.Texts | CleanupOptions.Strings | CleanupOptions.Electrical)]
    [InlineData(CleanupTarget.StringSign, CleanupOptions.Texts | CleanupOptions.Strings)]
    [InlineData(CleanupTarget.StringPath, CleanupOptions.Strings)]
    [InlineData(CleanupTarget.ShadowOutline, CleanupOptions.Shadows)]
    [InlineData(CleanupTarget.ShadowLabel, CleanupOptions.Shadows | CleanupOptions.Texts)]
    [InlineData(CleanupTarget.Equipment, CleanupOptions.Electrical)]
    [InlineData(CleanupTarget.TransformerArea, CleanupOptions.Electrical | CleanupOptions.Strings)]
    [InlineData(CleanupTarget.None, CleanupOptions.None)]
    public void CadaAlvoSaiSoComAsSuasOpcoes(CleanupTarget alvo, CleanupOptions esperadas)
    {
        Assert.Equal(esperadas, Cleanup.ErasedBy(alvo));

        foreach (var opcao in Cleanup.Each)
            Assert.Equal(esperadas.HasFlag(opcao), Cleanup.Erases(alvo, opcao));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void CoresNaoApagamNada()
    {
        foreach (var alvo in Enum.GetValues<CleanupTarget>())
            Assert.False(Cleanup.Erases(alvo, CleanupOptions.Colors), alvo.ToString());
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ACorOriginalEADoDesenhoDaUsina()
    {
        var doTipo = new RgbColor(0, 160, 80);

        Assert.Equal(doTipo, Cleanup.OriginalColor(CleanupPiece.Contour, marked: false, triedAll: false, doTipo));
        Assert.Equal(doTipo, Cleanup.OriginalColor(CleanupPiece.Module, marked: false, triedAll: false, doTipo));
        Assert.Null(Cleanup.OriginalColor(CleanupPiece.Pillar, marked: false, triedAll: false, doTipo));
        Assert.Null(Cleanup.OriginalColor(CleanupPiece.Module, marked: false, triedAll: false, typeColor: null));

        // A mesa que não cabe fica inteira magenta (roxo se tentou todas), pilar inclusive.
        foreach (var peca in Enum.GetValues<CleanupPiece>())
        {
            Assert.Equal(Cleanup.MarkedColor, Cleanup.OriginalColor(peca, marked: true, triedAll: false, doTipo));
            Assert.Equal(Cleanup.TriedAllColor, Cleanup.OriginalColor(peca, marked: true, triedAll: true, doTipo));
        }

        Assert.Equal(RgbColor.Magenta, Cleanup.MarkedColor);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AContagemSomaPorOpcao()
    {
        var alvos = new[]
        {
            CleanupTarget.Annotation, CleanupTarget.Annotation, CleanupTarget.StringTag, CleanupTarget.StringSign, CleanupTarget.StringSign,
            CleanupTarget.StringPath, CleanupTarget.ShadowOutline, CleanupTarget.ShadowOutline, CleanupTarget.ShadowLabel,
            CleanupTarget.Equipment, CleanupTarget.Equipment, CleanupTarget.Equipment, CleanupTarget.TransformerArea, CleanupTarget.None,
        };

        var c = Cleanup.Count(alvos, recolored: 7, shadowModules: 4, inverters: 2, transformers: 1, substations: 1, skids: 1, freedStrings: 5);

        Assert.Equal(7, c.Of(CleanupOptions.Colors));
        Assert.Equal(6, c.Of(CleanupOptions.Texts));        // 2 anotações, a tag, 2 sinais, a etiqueta
        Assert.Equal(3 + 4, c.Of(CleanupOptions.Shadows));  // 2 contornos e a etiqueta; 4 módulos
        Assert.Equal(1, c.Of(CleanupOptions.Strings));
        Assert.Equal(2 + 1 + 1 + 1 + 4 + 5, c.Of(CleanupOptions.Electrical));
        Assert.Equal(4, c.Placed);

        Assert.False(c.IsEmpty(CleanupOptions.Strings));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Of(CleanupOptions.Colors | CleanupOptions.Texts));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void ContagemVaziaEOpcoesSemNada()
    {
        var c = Cleanup.Count([], 0, 0, 0, 0, 0, 0, 0);

        Assert.True(c.IsEmpty(CleanupOptions.All));
        Assert.True(c.IsEmpty(CleanupOptions.None));

        var soCores = Cleanup.Count([], 3, 0, 0, 0, 0, 0, 0);
        Assert.False(soCores.IsEmpty(CleanupOptions.All));
        Assert.True(soCores.IsEmpty(CleanupOptions.Texts | CleanupOptions.Strings));
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void AsLinhasSaoSoAsDasMarcadasNaOrdem()
    {
        var c = Cleanup.Count([CleanupTarget.Annotation, CleanupTarget.StringPath], 2, 0, 0, 0, 0, 0, 0);

        var linhas = c.Lines(CleanupOptions.Strings | CleanupOptions.Colors);

        Assert.Equal(2, linhas.Count);
        Assert.StartsWith(Cleanup.Name(CleanupOptions.Colors) + ":", linhas[0], StringComparison.Ordinal);
        Assert.StartsWith(Cleanup.Name(CleanupOptions.Strings) + ":", linhas[1], StringComparison.Ordinal);
        Assert.Contains("2", linhas[0], StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData("1", CleanupOptions.Colors)]
    [InlineData("135", CleanupOptions.Colors | CleanupOptions.Shadows | CleanupOptions.Electrical)]
    [InlineData(" 2, 4 ", CleanupOptions.Texts | CleanupOptions.Strings)]
    [InlineData("5 5", CleanupOptions.Electrical)]
    [InlineData("12345", CleanupOptions.All)]
    [InlineData("*", CleanupOptions.All)]
    public void LeAsOpcoesDigitadas(string texto, CleanupOptions esperadas) =>
        Assert.Equal(esperadas, Cleanup.Parse(texto));

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("1a")]
    [InlineData(",")]
    public void RecusaOQueNaoEOpcao(string? texto) => Assert.Null(Cleanup.Parse(texto));

    [Fact]
    [Trait("Etapa", "8")]
    public void OApagarEstaNoMenuDaEdicao()
    {
        var edicao = RibbonLayout.Tabs[0].Panels.Single(p => p.Title == "Edição");
        var menu = Assert.Single(edicao.Items.OfType<RibbonMenuSpec>());

        Assert.Contains(menu.Items, b => b.Command == PluginInfo.ComandoApagar);
        Assert.Equal(("CLIVUS_ERASE", "CLIVUS_BORRAR"), CommandNames.Table[PluginInfo.ComandoApagar]);
    }
}
