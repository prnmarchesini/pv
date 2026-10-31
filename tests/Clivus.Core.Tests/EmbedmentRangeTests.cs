namespace Clivus.Core.Tests;

/// <summary>
/// 38.12 (31/10/2026, Renan): o enterro mínimo, nominal e máximo, por linha de
/// pilar no biposte ("no biposte, o enterro mínimo e máximo é POR PILAR, o da
/// frente e de traz"), e a estaca do catálogo: o enterro varia com o terreno
/// entre o mínimo e o máximo, e a estaca cresce ou é cortada fora disso ("o
/// pilar fica com 7m por exemplo, foda-se eu tenho que respeitar o enterrado").
/// </summary>
public class EmbedmentRangeTests
{
    private static readonly EmbedmentRange Faixa = new(1.0, 1.5, 2.0);

    /// <summary>A GC800 do catálogo: a que tem o P1 e o P2.</summary>
    private static TableProfile Gc800() => VendorStructures.All.Single(e => e.Model.Contains("GC800MM", StringComparison.Ordinal)).Profile;

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData(2.20, 1.68, 3.88)]   // cabe: a estaca do catálogo, com o enterro que sobra
    [InlineData(3.20, 1.00, 4.20)]   // nem com o mínimo: a estaca cresce, com o mínimo enterrado
    [InlineData(1.50, 2.00, 3.50)]   // afundaria além do máximo: cortada, com o máximo enterrado
    [InlineData(1.88, 2.00, 3.88)]   // no máximo, ainda a do catálogo
    [InlineData(2.88, 1.00, 3.88)]   // no mínimo, ainda a do catálogo
    public void AEstacaDoCatalogoVariaOEnterroEntreOMinimoEOMaximo(double livre, double enterro, double estaca)
    {
        var (e, comprimento) = Faixa.Stake(livre, 3.88);

        Assert.Equal(enterro, e, 9);
        Assert.Equal(estaca, comprimento, 9);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void SemCatalogoAEstacaSaiComONominal()
    {
        var (e, comprimento) = Faixa.Stake(2.2, null);

        Assert.Equal(1.5, e, 9);
        Assert.Equal(3.7, comprimento, 9);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void OEmBrancoVemDoProjetoEONominalEOMinimo()
    {
        var soOProjeto = EmbedmentRange.Of(null, null, null, 1.1, 2.5);
        Assert.Equal(new EmbedmentRange(1.1, 1.1, 2.5), soOProjeto);

        var comNominal = EmbedmentRange.Of(1.0, 1.4, null, 1.1, 1.2);
        Assert.Equal(new EmbedmentRange(1.0, 1.4, 1.4), comNominal);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void OsTresTemQueEstarEmOrdem()
    {
        Assert.Null(EmbedmentRange.WhyInvalid(1.0, 1.5, 2.0));
        Assert.Null(EmbedmentRange.WhyInvalid(null, 1.5, null));
        Assert.NotNull(EmbedmentRange.WhyInvalid(1.6, 1.5, 2.0));
        Assert.NotNull(EmbedmentRange.WhyInvalid(1.0, 2.5, 2.0));
        Assert.NotNull(EmbedmentRange.WhyInvalid(2.1, null, 2.0));
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void OPilarDeTrasTemOEnterroDeleEEmBrancoOsDaFrente()
    {
        var gc800 = Gc800();
        var frente = gc800.Frame with { MinEmbedment = 1.0, NominalEmbedment = 1.2, MaxEmbedment = 1.6 };

        var igual = frente.EmbedmentFor(PillarLine.Rear, 0.5, 3.0);
        Assert.Equal(new EmbedmentRange(1.0, 1.2, 1.6), igual);

        var proprio = frente with { Bifila = frente.Bifila! with { RearMinEmbedment = 1.5, RearNominalEmbedment = 1.8, RearMaxEmbedment = 2.4 } };
        Assert.Equal(new EmbedmentRange(1.5, 1.8, 2.4), proprio.EmbedmentFor(PillarLine.Rear, 0.5, 3.0));
        Assert.Equal(new EmbedmentRange(1.0, 1.2, 1.6), proprio.EmbedmentFor(PillarLine.Front, 0.5, 3.0));

        Assert.NotNull(proprio.CatalogStake(PillarLine.Front));
        Assert.Equal(gc800.Frame.Bifila!.FrontPillar, proprio.CatalogStake(PillarLine.Front));
        Assert.Equal(gc800.Frame.Bifila!.RearPillar, proprio.CatalogStake(PillarLine.Rear));
        Assert.Null(proprio.CatalogStake(PillarLine.Single));
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void AEstruturaRecusaOEnterroForaDeOrdem()
    {
        var gc800 = Gc800();

        Assert.NotNull((gc800.Frame with { MinEmbedment = 1.5, NominalEmbedment = 1.2 }).WhyInvalid);
        Assert.NotNull((gc800.Frame with { Bifila = gc800.Frame.Bifila! with { RearNominalEmbedment = 3.0, RearMaxEmbedment = 2.0 } }).WhyInvalid);
        Assert.Null((gc800.Frame with { MinEmbedment = 1.0, NominalEmbedment = 1.2, MaxEmbedment = 2.0 }).WhyInvalid);
    }

    /// <summary>O mínimo, o nominal e o máximo, da frente e de trás, voltam iguais do arquivo; o perfil de antes abre sem eles.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OEnterroVaiEVoltaDoArquivo()
    {
        var gc800 = Gc800();
        var perfil = gc800 with
        {
            Frame = gc800.Frame with
            {
                MinEmbedment = 1.0,
                NominalEmbedment = 1.2,
                MaxEmbedment = 1.8,
                Bifila = gc800.Frame.Bifila! with { RearMinEmbedment = 1.3, RearNominalEmbedment = 1.5, RearMaxEmbedment = 2.2 },
            },
        };

        var volta = TableProfile.Parse(perfil.ToJson());

        Assert.Equal(1.2, volta.Frame.NominalEmbedment);
        Assert.Equal(1.8, volta.Frame.MaxEmbedment);
        Assert.Equal(1.3, volta.Frame.Bifila!.RearMinEmbedment);
        Assert.Equal(1.5, volta.Frame.Bifila!.RearNominalEmbedment);
        Assert.Equal(2.2, volta.Frame.Bifila!.RearMaxEmbedment);

        var antigo = TableProfile.Parse(gc800.ToJson());
        Assert.Null(antigo.Frame.NominalEmbedment);
        Assert.Null(antigo.Frame.Bifila!.RearNominalEmbedment);
    }

    /// <summary>
    /// A altura da estrutura é a do P1 com o enterro NOMINAL (Renan: "aqui é o
    /// enterro mínimo, não é para levantar a estrutura"): mudar o mínimo não
    /// muda a altura; mudar o nominal muda.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void AAlturaDaEstruturaVemDoNominalENaoDoMinimo()
    {
        var gc800 = Gc800();
        TableProfile Com(double? minimo, double? nominal) => gc800 with { Frame = gc800.Frame with { MinEmbedment = minimo, NominalEmbedment = nominal } };

        var base_ = Com(1.0, 1.5).StructureLowEdge!.Value;

        Assert.Equal(base_, Com(0.8, 1.5).StructureLowEdge!.Value, 9);
        Assert.Equal(base_ + 0.3, Com(1.0, 1.2).StructureLowEdge!.Value, 9);

        // Sem nominal, o mínimo, como antes do 38.12.
        Assert.Equal(Com(1.5, null).StructureLowEdge!.Value, base_, 9);
    }

    /// <summary>No chão plano, o P2 de trás usa o enterro nominal dele.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void NoChaoPlanoCadaPilarLevaONominalDaLinhaDele()
    {
        var gc800 = Gc800();
        var mesmo = gc800 with { Frame = gc800.Frame with { MinEmbedment = 1.0, NominalEmbedment = 1.2 } };
        var maisFundo = mesmo with { Frame = mesmo.Frame with { Bifila = mesmo.Frame.Bifila! with { RearNominalEmbedment = 1.5 } } };

        var projeto = SystemConfiguration.Default;
        var a = mesmo.PillarsAtBasicHeight(projeto);
        var b = maisFundo.PillarsAtBasicHeight(projeto);

        Assert.Equal(a.P1, b.P1, 9);
        Assert.Equal(a.P2!.Value + 0.3, b.P2!.Value, 9);
    }

    /// <summary>
    /// O de trás com o que ele herda da frente (revisão do 38.12): o nominal
    /// de trás escrito abaixo do mínimo da frente, ou acima do máximo dela, é
    /// recusado, e não ajustado calado.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void ODeTrasNaoPodeSairDaFaixaQueHerdaDaFrente()
    {
        var gc800 = Gc800();
        var frente = gc800.Frame with { MinEmbedment = 1.0, NominalEmbedment = 1.2, MaxEmbedment = 1.6 };
        TableFrame Tras(double? minimo, double? nominal, double? maximo) =>
            frente with { Bifila = frente.Bifila! with { RearMinEmbedment = minimo, RearNominalEmbedment = nominal, RearMaxEmbedment = maximo } };

        Assert.Contains("trás", Tras(null, 0.8, null).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("trás", Tras(null, 2.0, null).WhyInvalid, StringComparison.Ordinal);
        Assert.Contains("trás", Tras(1.8, null, null).WhyInvalid, StringComparison.Ordinal);
        Assert.Null(Tras(1.3, null, 2.4).WhyInvalid);
        Assert.Null(Tras(null, 1.5, 2.0).WhyInvalid);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void OBiposteComSoONominalServe()
    {
        var gc800 = Gc800();
        var soNominal = gc800 with { Frame = gc800.Frame with { MinEmbedment = null, NominalEmbedment = 2.2 } };

        Assert.Null(soNominal.WhyInvalid);
        Assert.Equal(gc800.StructureLowEdge!.Value, soNominal.StructureLowEdge!.Value, 9);
    }

    /// <summary>Dois perfis que só diferem no enterro não são iguais; o perfil com enterro novo vai na versão 6 e volta igual.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OEnterroEntraNaIgualdadeEOArquivoVaiNaVersao6()
    {
        var gc800 = Gc800();
        var comNominal = gc800 with { Frame = gc800.Frame with { NominalEmbedment = 2.3 } };
        var comTras = gc800 with { Frame = gc800.Frame with { Bifila = gc800.Frame.Bifila! with { RearMaxEmbedment = 2.6 } } };

        Assert.NotEqual(gc800.Frame, comNominal.Frame);
        Assert.NotEqual(gc800.Frame, comTras.Frame);

        Assert.Contains("\"formatVersion\": 6", comNominal.ToJson(), StringComparison.Ordinal);
        Assert.Equal(comNominal, TableProfile.Parse(comNominal.ToJson()));
        Assert.Equal(comTras, TableProfile.Parse(comTras.ToJson()));
        Assert.DoesNotContain("\"formatVersion\": 6", gc800.ToJson(), StringComparison.Ordinal);
    }
}
