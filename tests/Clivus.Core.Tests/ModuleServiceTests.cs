namespace Clivus.Core.Tests;

/// <summary>
/// Passo 8.3: o plugin lê a lista de módulos do serviço local
/// (<c>GET /modulos</c>), com os nomes de campo do Python, e passa pela
/// mesma validação da biblioteca embutida.
/// </summary>
public class ModuleServiceTests
{
    private const string Dois = """
        [
          {"id": "a1", "marca": "Risen", "modelo": "RSM132-8-720BHDG", "potencia_w": 720,
           "altura_m": 2.384, "largura_m": 1.303, "espessura_m": 0.033},
          {"id": "b2", "marca": "Jinko", "modelo": "JKM610N", "potencia_w": 610,
           "altura_m": 2.382, "largura_m": 1.134, "espessura_m": 0.030}
        ]
        """;

    [Fact]
    [Trait("Etapa", "8")]
    public void LeOsCamposDoServico()
    {
        var modulos = ModuleLibrary.ParseService(Dois);

        Assert.Equal(2, modulos.Count);

        // Mesma ordem da embutida: marca, depois modelo.
        Assert.Equal("Jinko", modulos[0].Brand);
        Assert.Equal(new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033), modulos[1]);
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData("[]", "nenhum módulo")]
    [InlineData("não é json", "não pôde ser lida")]
    [InlineData("""[{"marca":"X","modelo":"Y","potencia_w":720,"altura_m":2.3,"largura_m":0,"espessura_m":0.03}]""", "medida impossível")]
    [InlineData("""[{"marca":"X","modelo":"Y","potencia_w":720,"altura_m":1.1,"largura_m":2.3,"espessura_m":0.03}]""", "suspeito")]
    [InlineData("""[{"marca":"X","modelo":"Y","potencia_w":720,"altura_m":2.3,"largura_m":1.1,"espessura_m":0.03},{"marca":"Z","modelo":"y","potencia_w":720,"altura_m":2.3,"largura_m":1.1,"espessura_m":0.03}]""", "repetido")]
    public void ListaRuimERecusadaComMotivo(string json, string trecho)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ModuleLibrary.ParseService(json));

        Assert.Contains(trecho, erro.Message);
    }

    /// <summary>
    /// O serviço nasce com os módulos da biblioteca embutida (semear.py lê
    /// uma cópia do mesmo arquivo); lidos dos dois lados, são os mesmos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "8")]
    public void ACopiaDoServicoEAMesmaBibliotecaEmbutida()
    {
        var copia = Path.Combine(Repositorio.Raiz, "servidor", "app", "modulos_iniciais.json");

        Assert.Equal(ModuleLibrary.Default(), ModuleLibrary.Parse(File.ReadAllText(copia)));
    }
}

/// <summary>Passo 8.4: o módulo que o plugin cadastra no serviço.</summary>
public class ModuleServiceEntryTests
{
    [Fact]
    [Trait("Etapa", "8")]
    public void OJsonTemOsNomesDoServico()
    {
        var json = ModuleLibrary.ToServiceJson(new SolarModule(" Risen ", " RSM132-8-720BHDG ", 720, 2.384, 1.303, 0.033));

        Assert.Contains("\"marca\":\"Risen\"", json);
        Assert.Contains("\"modelo\":\"RSM132-8-720BHDG\"", json);
        Assert.Contains("\"potencia_w\":720", json);
        Assert.Contains("\"largura_m\":1.303", json);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void IdaEVoltaDaOMesmoModulo()
    {
        var modulo = new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

        var volta = ModuleLibrary.ParseService("[" + ModuleLibrary.ToServiceJson(modulo) + "]");

        Assert.Equal(modulo, Assert.Single(volta));
    }

    [Theory]
    [Trait("Etapa", "8")]
    [InlineData(0.72, 2.384, 1.303)]
    [InlineData(720, 1.303, 2.384)]
    public void ModuloQueOPluginRecusaNaoVaiProServico(double watts, double altura, double largura)
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => ModuleLibrary.ToServiceJson(new SolarModule("X", "Y", watts, altura, largura, 0.03)));

        Assert.False(string.IsNullOrWhiteSpace(erro.Message));
    }
}

/// <summary>Achados da revisão do 8.4: o que o serviço recusaria, o plugin recusa antes.</summary>
public class ModuleServiceEntryRevisionTests
{
    [Fact]
    [Trait("Etapa", "8")]
    public void MarcaEmBrancoERecusada()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => ModuleLibrary.ToServiceJson(new SolarModule("  ", "Y", 720, 2.384, 1.303, 0.033)));

        Assert.Contains("marca", erro.Message);
    }

    [Fact]
    [Trait("Etapa", "8")]
    public void NomeComprideDemaisERecusado()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => ModuleLibrary.ToServiceJson(new SolarModule("X", new string('M', 121), 720, 2.384, 1.303, 0.033)));

        Assert.Contains("120", erro.Message);
    }
}
