namespace Clivus.Core.Tests;

/// <summary>
/// O módulo inteiro a partir do .PAN (38.6, Renan 30/10/2026: "na hora de
/// puxar o pan, já poderia configurar o modelo, marca, altura, tudo [...] e
/// esse arquivo pan já subir no servidor").
/// </summary>
public class PanCadastroTests
{
    /// <summary>O trecho do JAM66D46-720_LB.PAN do projeto Aurora com as medidas e a parte elétrica.</summary>
    private const string Pan = """
        PVObject_=pvModule
          Version=7.4.3
          PVObject_Commercial=pvCommercial
            Manufacturer=JA Solar
            Model=JAM66D46-720/LB
            Width=1.303
            Height=2.384
            Depth=0.033
            Weight=38.200
          End of PVObject pvCommercial
          PNom=720.0
          Isc=18.590
          Voc=49.00
          Imp=17.480
          Vmp=41.19
          muISC=8.11
          muVocSpec=-119.5
          muPmpReq=-0.293
        End of PVObject pvModule
        """;

    [Fact]
    [Trait("Etapa", "38")]
    public void OPanTrazAsMedidasDoModulo()
    {
        var medidas = PanModule.DimensionsOf(Pan);

        Assert.NotNull(medidas);
        Assert.Equal(2.384, medidas.Height);
        Assert.Equal(1.303, medidas.Width);
        Assert.Equal(0.033, medidas.Thickness);
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void OModuloDoPanTemMarcaModeloPotenciaEMedidas()
    {
        var pan = PanModule.Parse(Pan, out _)!;
        var modulo = PanModule.DimensionsOf(Pan)!.ModuleOf(pan);

        Assert.Equal("JA Solar", modulo.Brand);
        Assert.Equal("JAM66D46-720/LB", modulo.Model);
        Assert.Equal(720, modulo.PowerWatts);
        Assert.Equal(2.384, modulo.Height);
        Assert.Equal(1.303, modulo.Width);
        Assert.Equal(0.033, modulo.Thickness);
        Assert.True(modulo.IsValid);
    }

    /// <summary>Largura maior que a altura no PAN: o lado maior vira a altura, como o plugin espera.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OLadoMaiorEAAltura()
    {
        var trocado = Pan.Replace("Width=1.303", "Width=2.384").Replace("Height=2.384", "Height=1.303");

        var medidas = PanModule.DimensionsOf(trocado)!;

        Assert.Equal(2.384, medidas.Height);
        Assert.Equal(1.303, medidas.Width);
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("Depth=0.033", "")]
    [InlineData("Width=1.303", "Width=abc")]
    [InlineData("Height=2.384", "Height=-2.384")]
    public void SemAsTresMedidasNaoHaMedida(string de, string para)
    {
        Assert.Null(PanModule.DimensionsOf(Pan.Replace(de, para)));
    }

    /// <summary>O cadastro com o PAN leva a parte elétrica e o arquivo; a lista do serviço a traz de volta.</summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void AParteEletricaVaiEVoltaPeloServico()
    {
        var pan = PanModule.Parse(Pan, out _)!;
        var modulo = PanModule.DimensionsOf(Pan)!.ModuleOf(pan);

        var json = ModuleLibrary.ToServiceJson(modulo, pan, Pan, "JAM66D46-720_LB.PAN");

        Assert.Contains("\"eletrica\"", json);
        Assert.Contains("\"pan_nome\":\"JAM66D46-720_LB.PAN\"", json);

        // O serviço devolve a lista sem o texto do PAN, com a parte elétrica.
        var lista = "[" + json.Replace("\"pan\":", "\"pan_ignorado\":") + "]";
        var eletrica = ModuleLibrary.ParseServiceElectrical(lista);

        var volta = Assert.Single(eletrica).Value;
        Assert.Equal(49.0, volta.Voc);
        Assert.Equal(18.59, volta.Isc);
        Assert.Equal(41.19, volta.Vmp);
        Assert.Equal(17.48, volta.Imp);
        Assert.Equal(pan.VocCoefficient, volta.VocCoefficient, 9);
        Assert.Equal(pan.IscCoefficient, volta.IscCoefficient, 9);
        Assert.Equal(-0.293, volta.PowerCoefficient);
        Assert.Equal(720, volta.Pmax);
        Assert.True(eletrica.ContainsKey("jam66d46-720/lb"));
    }

    [Fact]
    [Trait("Etapa", "38")]
    public void SemPanOCadastroEOMesmoDeAntes()
    {
        var modulo = new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

        var json = ModuleLibrary.ToServiceJson(modulo);

        Assert.DoesNotContain("eletrica", json);
        Assert.DoesNotContain("pan", json);
        Assert.Empty(ModuleLibrary.ParseServiceElectrical("[" + json + "]"));
    }

    /// <summary>
    /// O contrato com o serviço (tests/dados/servico-modulos-com-pan.json, o
    /// mesmo que o pytest do serviço confere contra a resposta dele): a lista
    /// com um módulo de PAN e um sem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void OContratoDoServicoComPanSeLe()
    {
        var json = File.ReadAllText(Path.Combine(Repositorio.Raiz, "tests", "dados", "servico-modulos-com-pan.json"));

        var modulos = ModuleLibrary.ParseService(json);
        var eletrica = ModuleLibrary.ParseServiceElectrical(json);

        Assert.Equal(2, modulos.Count);
        var ja = Assert.Single(eletrica).Value;
        Assert.Equal("JAM66D46-720/LB", ja.Model);
        Assert.Equal(-0.1195, ja.VocCoefficient);
        Assert.Equal(0.00811, ja.IscCoefficient);
        Assert.Null(ja.Noct);
    }

    /// <summary>
    /// A parte elétrica de um módulo com campo nulo (o NaN que o banco gravou
    /// como NULL) fica de fora, e a lista continua valendo (revisão do 38.6).
    /// </summary>
    [Fact]
    [Trait("Etapa", "38")]
    public void AParteEletricaQuebradaNaoDerrubaALista()
    {
        var json = File.ReadAllText(Path.Combine(Repositorio.Raiz, "tests", "dados", "servico-modulos-com-pan.json"))
            .Replace("\"mu_voc\": -0.1195", "\"mu_voc\": null");

        Assert.Equal(2, ModuleLibrary.ParseService(json).Count);
        Assert.Empty(ModuleLibrary.ParseServiceElectrical(json));
    }

    [Theory]
    [Trait("Etapa", "38")]
    [InlineData("{\"id\":\"1\",\"modelo\":\"X\",\"tem_pan\":true}", true)]
    [InlineData("{\"id\":\"1\",\"modelo\":\"X\",\"tem_pan\":false}", false)]
    [InlineData("{\"id\":\"1\",\"modelo\":\"X\"}", false)]
    [InlineData("não é json", false)]
    public void ARespostaDizSeOServicoGuardouOPan(string resposta, bool guardou)
    {
        Assert.Equal(guardou, ModuleLibrary.ServiceKeptPan(resposta));
    }
}
