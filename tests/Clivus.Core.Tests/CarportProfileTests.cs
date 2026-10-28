using System.Globalization;
using System.Text.Json.Nodes;

namespace Clivus.Core.Tests;

/// <summary>
/// O perfil do carport (34.1): o segundo tipo de estrutura, com arquivo e
/// formato próprios. As mesmas guardas do perfil da mesa: o que entra é o que
/// sai, campo que falta é recusado pelo nome, campo desconhecido e outra versão
/// também, e o padrão é o carport biposte de referência.
/// </summary>
public class CarportProfileTests
{
    private static CarportProfile Padrao => CarportProfile.Reference;

    private static JsonObject ComoJson(CarportProfile perfil) => JsonNode.Parse(perfil.ToJson())!.AsObject();

    // ---------------------------------------------------------- o padrão

    [Fact]
    [Trait("Etapa", "34")]
    public void OPadraoEOModeloDeReferencia()
    {
        Assert.True(Padrao.IsValid, Padrao.WhyInvalid);

        Assert.Equal(3, Padrao.Array.ModulesDown);
        Assert.Equal(5, Padrao.Array.ModulesAcross);
        Assert.Equal(CarportOrientation.Portrait, Padrao.Array.Orientation);
        Assert.Equal(7, Padrao.TiltDegrees, 9);
        Assert.Equal(2.345, Padrao.ClearHeight, 9);
        Assert.Equal(15, Padrao.Frame.PillarLeanDegrees, 9);

        // A cobertura do .glb: 6,80 m em planta (Y -1,714 a 5,086), 5,67 m na largura.
        Assert.Equal(6.854, Padrao.Array.SlopeLength, 3);
        Assert.Equal(6.80, Padrao.Array.SlopeLength * Math.Cos(Padrao.TiltRadians), 2);
        Assert.Equal(5.67, Padrao.Array.BayWidth, 3);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void EmPaisagemAMedidaMaiorVaiAoLongoDaFila()
    {
        var paisagem = Padrao with { Array = Padrao.Array with { Orientation = CarportOrientation.Landscape } };

        Assert.Equal(1.134, paisagem.Array.ModuleDown, 9);
        Assert.Equal(2.278, paisagem.Array.ModuleAcross, 9);
        Assert.Equal(5 * 2.278, paisagem.Array.BayWidth, 9);
    }

    // ---------------------------------------------------------- ida e volta

    [Fact]
    [Trait("Etapa", "34")]
    public void OPerfilVoltaIgualAoQueFoiGravado()
    {
        Assert.Equal(Padrao, CarportProfile.Parse(Padrao.ToJson()));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void CadaCampoVoltaComOSeuValor()
    {
        var mexido = new CarportProfile(
            "Carport do teste",
            new CarportArray(new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033),
                CarportOrientation.Landscape, 4, 6, 0.02, 0.03),
            new CarportFrame(2.0, 10 * Math.PI / 180, 0.1, 0.4, 0.3, 1, 0.5, 0.7, 0.8,
                new CarportSection("P", 0.2, 0.15), new CarportSection("V", 0.25, 0.12),
                new CarportSection("T", 0.1, 0.06), new CarportSection("M", 0.08, 0.04),
                new CarportBase(0.4, 0.012, 0.5, 0.2)),
            5 * Math.PI / 180,
            3.1);
        Assert.True(mexido.IsValid, mexido.WhyInvalid);

        var v = CarportProfile.Parse(mexido.ToJson());

        Assert.Equal("Carport do teste", v.Name);
        Assert.Equal("Risen", v.Array.Module.Brand);
        Assert.Equal(720, v.Array.Module.PowerWatts, 9);
        Assert.Equal(CarportOrientation.Landscape, v.Array.Orientation);
        Assert.Equal(4, v.Array.ModulesDown);
        Assert.Equal(6, v.Array.ModulesAcross);
        Assert.Equal(0.02, v.Array.GapAcross, 9);
        Assert.Equal(0.03, v.Array.GapDown, 9);
        Assert.Equal(5, v.TiltDegrees, 9);
        Assert.Equal(3.1, v.ClearHeight, 9);
        Assert.Equal(2.0, v.Frame.PillarBaseSpacing, 9);
        Assert.Equal(10, v.Frame.PillarLeanDegrees, 9);
        Assert.Equal(0.1, v.Frame.PillarOffset, 9);
        Assert.Equal(0.4, v.Frame.BeamLowInset, 9);
        Assert.Equal(0.3, v.Frame.BeamHighInset, 9);
        Assert.Equal(1, v.Frame.PurlinsPerModule);
        Assert.Equal(0.5, v.Frame.PurlinEdgeOffset, 9);
        Assert.Equal(0.7, v.Frame.BraceOnPillar, 9);
        Assert.Equal(0.8, v.Frame.BraceOnBeam, 9);
        Assert.Equal(new CarportSection("V", 0.25, 0.12), v.Frame.Beam);
        Assert.Equal(new CarportBase(0.4, 0.012, 0.5, 0.2), v.Frame.Base);
    }

    /// <summary>Num Windows em português o separador é vírgula; o arquivo não muda.</summary>
    [Fact]
    [Trait("Etapa", "34")]
    public void OArquivoNaoDependeDaCultura()
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            var json = Padrao.ToJson();
            Assert.Contains("\"clearHeight\": 2.345", json);
            Assert.Equal(Padrao, CarportProfile.Parse(json));
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void AngulosVaoEmGrausEAPosicaoComoTexto()
    {
        var json = ComoJson(Padrao);

        Assert.Equal(7, json["tiltDegrees"]!.GetValue<double>(), 9);
        Assert.Equal(15, json["frame"]!["pillarLeanDegrees"]!.GetValue<double>(), 9);
        Assert.Equal("Portrait", json["array"]!["orientation"]!.GetValue<string>());
        Assert.Equal(CarportProfile.VersaoDoFormato, json["formatVersion"]!.GetValue<int>());
    }

    // ---------------------------------------------------------- recusas

    [Theory]
    [Trait("Etapa", "34")]
    [InlineData("clearHeight")]
    [InlineData("tiltDegrees")]
    [InlineData("array.gapAcross")]
    [InlineData("array.orientation")]
    [InlineData("frame.pillarOffset")]
    [InlineData("frame.purlinEdgeOffset")]
    [InlineData("frame.beam.depth")]
    [InlineData("frame.base.pedestalHeight")]
    public void CampoQueFaltaERecusadoPeloNome(string caminho)
    {
        var json = ComoJson(Padrao);
        var partes = caminho.Split('.');
        JsonNode no = json;
        foreach (var p in partes[..^1]) no = no[p]!;
        no.AsObject().Remove(partes[^1]);

        var erro = Assert.Throws<InvalidOperationException>(() => CarportProfile.Parse(json.ToJsonString()));
        Assert.Contains($"\"{caminho}\"", erro.Message);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void CampoDesconhecidoERecusado()
    {
        var json = ComoJson(Padrao);
        json["frame"]!.AsObject()["wind"] = 40;

        Assert.Throws<InvalidOperationException>(() => CarportProfile.Parse(json.ToJsonString()));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void OutraVersaoERecusada()
    {
        var json = ComoJson(Padrao);
        json["formatVersion"] = 2;

        var erro = Assert.Throws<InvalidOperationException>(() => CarportProfile.Parse(json.ToJsonString()));
        Assert.Contains("versão 2", erro.Message);
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void APosicaoEmNumeroERecusada()
    {
        var json = ComoJson(Padrao);
        json["array"]!["orientation"] = 1;

        Assert.Throws<InvalidOperationException>(() => CarportProfile.Parse(json.ToJsonString()));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void UmPerfilDeMesaNaoELidoComoCarport()
    {
        var mesa = new TableProfile(
            "Mesa",
            new TableLayout(new SolarModule("Risen", "X", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.1, 0.1),
            new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
            20 * Math.PI / 180);

        Assert.Throws<InvalidOperationException>(() => CarportProfile.Parse(mesa.ToJson()));
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void PerfilInvalidoNaoESalvo()
    {
        Assert.Throws<InvalidOperationException>(() => (Padrao with { Name = " " }).ToJson());
    }

    /// <summary>Os erros de escala e os de campo trocado que a validação existe para pegar.</summary>
    [Theory]
    [Trait("Etapa", "34")]
    [InlineData("altura em mm")]
    [InlineData("inclinação de 45°")]
    [InlineData("folga em cm")]
    [InlineData("bases mais afastadas que a cobertura")]
    [InlineData("pilar fora da cobertura")]
    [InlineData("recuo da viga maior que meia cobertura")]
    [InlineData("terça fora do módulo")]
    [InlineData("mão francesa abaixo do chão")]
    [InlineData("seção em mm")]
    [InlineData("nenhum módulo na descida")]
    public void ValoresImpossiveisSaoRecusados(string caso)
    {
        var p = Padrao;
        var f = p.Frame;
        var errado = caso switch
        {
            "altura em mm" => p with { ClearHeight = 2345 },
            "inclinação de 45°" => p with { TiltRadians = 45 * Math.PI / 180 },
            "folga em cm" => p with { Array = p.Array with { GapDown = 1.0 } },
            "bases mais afastadas que a cobertura" => p with { Frame = f with { PillarBaseSpacing = 7.0 } },
            "pilar fora da cobertura" => p with { Frame = f with { PillarOffset = 2.5 } },
            "recuo da viga maior que meia cobertura" => p with { Frame = f with { BeamLowInset = 3.5 } },
            "terça fora do módulo" => p with { Frame = f with { PurlinEdgeOffset = 1.2 } },
            "mão francesa abaixo do chão" => p with { Frame = f with { BraceOnPillar = 2.5 } },
            "seção em mm" => p with { Frame = f with { Pillar = f.Pillar with { Depth = 130 } } },
            "nenhum módulo na descida" => p with { Array = p.Array with { ModulesDown = 0 } },
            _ => throw new ArgumentException(caso),
        };

        Assert.False(errado.IsValid, caso);
        Assert.Throws<InvalidOperationException>(() => errado.ToJson());
    }

    [Fact]
    [Trait("Etapa", "34")]
    public void ADescricaoDizOVaoEAAlturaLivre()
    {
        var texto = Padrao.Describe();

        Assert.Contains("Carport biposte", texto);
        Assert.Contains("3 × 5", texto);
    }
}
