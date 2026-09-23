namespace UFV.Core.Tests;

/// <summary>
/// O módulo é a menor peça do projeto e a origem de quase todo número que vem
/// depois: o comprimento da mesa, a posição dos pilares, a altura livre.
///
/// Um milímetro errado aqui vira meio metro no fim de uma mesa de 28 módulos,
/// e ninguém vai desconfiar do módulo — vão desconfiar da estrutura.
/// </summary>
public class SolarModuleTests
{
    /// <summary>O Risen do Renan, com os números do datasheet.</summary>
    private static SolarModule Risen(
        double altura = 2.384, double largura = 1.303, double espessura = 0.033, double wp = 720) =>
        new("Risen", "RSM132-8-720BHDG", wp, altura, largura, espessura);

    [Fact]
    [Trait("Etapa", "3")]
    public void OModuloGuardaAsMedidasEmMetros()
    {
        var modulo = Risen();

        Assert.True(modulo.IsValid);
        Assert.Equal(2.384, modulo.Height, 6);
        Assert.Equal(1.303, modulo.Width, 6);
        Assert.Equal(0.033, modulo.Thickness, 6);
    }

    [Theory]
    [Trait("Etapa", "3")]
    // Medida zero, negativa ou não finita não é módulo: é dado corrompido, e
    // seguir com ele entrega uma mesa de comprimento zero ou NaN.
    [InlineData(0, 1.303, 0.033)]
    [InlineData(-2.384, 1.303, 0.033)]
    [InlineData(2.384, 0, 0.033)]
    [InlineData(2.384, 1.303, 0)]
    [InlineData(double.NaN, 1.303, 0.033)]
    [InlineData(double.PositiveInfinity, 1.303, 0.033)]
    // Quem digitou milímetro no campo do metro: 2384 em vez de 2,384.
    [InlineData(2384, 1303, 33)]
    // E quem dividiu por mil duas vezes: um módulo de dois milímetros passaria
    // em qualquer validação de sinal e daria uma mesa de cinco centímetros.
    [InlineData(0.002384, 0.001303, 0.000033)]
    public void ModuloComMedidaImpossivelNaoEValido(double altura, double largura, double espessura)
    {
        Assert.False(Risen(altura, largura, espessura).IsValid);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ModuloSemModeloNaoEValido()
    {
        Assert.False(new SolarModule("Risen", "  ", 720, 2.384, 1.303, 0.033).IsValid);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-1)]
    [InlineData(0)]
    // Quem digitou em quilowatt.
    [InlineData(0.72)]
    // Quem digitou a potência da string, ou do inversor, no campo do módulo.
    [InlineData(500000)]
    [InlineData(double.NaN)]
    public void PotenciaForaDaFaixaDeUmModuloNaoEValida(double wp)
    {
        Assert.False(Risen(wp: wp).IsValid);
    }

    /// <summary>
    /// O módulo é mais alto que largo, e é essa orientação que a mesa assume.
    /// Trocar os dois campos é o erro de digitação mais fácil de cometer e o
    /// mais difícil de enxergar: a mesa sai com metade do comprimento certo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OModuloAvisaQuandoAlturaELarguraParecemTrocadas()
    {
        Assert.Null(Risen().LooksSwapped);

        var trocado = Risen(altura: 1.303, largura: 2.384);

        Assert.True(trocado.IsValid, "O módulo trocado passa na validação de medida — esse é o problema.");
        Assert.NotNull(trocado.LooksSwapped);
        Assert.Contains("trocados", trocado.LooksSwapped);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoTrazMarcaModeloPotenciaEMedidasComVirgula()
    {
        var texto = Risen().Describe();

        Assert.Contains("Risen", texto);
        Assert.Contains("RSM132-8-720BHDG", texto);
        Assert.Contains("720", texto);

        // Vírgula decimal, e não ponto: o texto vai para a linha de comando do
        // AutoCAD em português, e a máquina do projetista pode estar em
        // qualquer idioma.
        Assert.Contains("2,384", texto);
        Assert.Contains("1,303", texto);
        Assert.DoesNotContain("2.384", texto);
    }
}

/// <summary>
/// A biblioteca existe para o usuário não ter que digitar seis números toda
/// vez, e para dois projetos do mesmo módulo saírem com as mesmas medidas.
///
/// Ela é conveniência, não autoridade: o módulo livre, digitado na mão, vale
/// tanto quanto o da lista. Quem manda é o datasheet que o Renan tem na frente.
/// </summary>
public class ModuleLibraryTests
{
    [Fact]
    [Trait("Etapa", "3")]
    public void ABibliotecaPadraoCarregaETemModulos()
    {
        Assert.NotEmpty(ModuleLibrary.Default());
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void TodoModuloDaBibliotecaPadraoEValido()
    {
        foreach (var modulo in ModuleLibrary.Default())
        {
            Assert.True(modulo.IsValid, $"Módulo inválido na biblioteca: {modulo.Model}");
            Assert.Null(modulo.LooksSwapped);
        }
    }

    /// <summary>
    /// O Risen 720 Wp é o módulo que o Renan usa, e os números vêm do
    /// datasheet: 2384 x 1303 x 33 mm.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ORisen720EstaNaBibliotecaComAsMedidasDoDatasheet()
    {
        var modulo = ModuleLibrary.Find("RSM132-8-720BHDG");

        Assert.NotNull(modulo);
        Assert.Equal("Risen", modulo!.Brand);
        Assert.Equal(720, modulo.PowerWatts);
        Assert.Equal(2.384, modulo.Height, 6);
        Assert.Equal(1.303, modulo.Width, 6);
        Assert.Equal(0.033, modulo.Thickness, 6);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ProcurarNaoDiferenciaMaiusculaDeMinusculaNemEspacoEmVolta()
    {
        Assert.NotNull(ModuleLibrary.Find("  rsm132-8-720bhdg "));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ModeloQueNaoExisteDevolveNulo()
    {
        Assert.Null(ModuleLibrary.Find("nao-existe-este-modelo"));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ProcurarComModeloVazioDevolveNuloEmVezDeExplodir()
    {
        Assert.Null(ModuleLibrary.Find(null));
        Assert.Null(ModuleLibrary.Find("   "));
    }

    /// <summary>
    /// A lista devolvida não pode ser a de dentro: ela fica guardada pelo resto
    /// da sessão do AutoCAD, e um chamador distraído esvaziaria a biblioteca
    /// para todo mundo — de novo a lista vazia sem explicação.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ABibliotecaDevolvidaNaoPodeSerAlterada()
    {
        var modulos = ModuleLibrary.Default();

        Assert.False(
            modulos is List<SolarModule>,
            "Default() devolveu a lista de dentro; qualquer chamador pode esvaziá-la.");

        var antes = modulos.Count;

        Assert.Equal(antes, ModuleLibrary.Default().Count);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void JsonDeOutroFormatoDaErroClaroEmVezDeListaVazia()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => ModuleLibrary.Parse("{ isto nao e json }"));

        Assert.Contains("biblioteca", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Array vazio é o caso mais provável de arquivo estragado — alguém editou
    /// e apagou as entradas — e é exatamente o que não pode passar calado.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData("[]")]
    [InlineData("null")]
    public void BibliotecaSemModuloDaErroEmVezDeListaVazia(string json)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ModuleLibrary.Parse(json));

        Assert.Contains("vazia", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ModuloInvalidoNoJsonERecusadoComONomeDele()
    {
        const string json = """
        [
          { "brand": "X", "model": "BOM",  "powerWatts": 700, "height": 2.3, "width": 1.3, "thickness": 0.03 },
          { "brand": "X", "model": "RUIM", "powerWatts": 700, "height": 0,   "width": 1.3, "thickness": 0.03 }
        ]
        """;

        var erro = Assert.Throws<InvalidOperationException>(() => ModuleLibrary.Parse(json));

        Assert.Contains("RUIM", erro.Message);
    }

    /// <summary>
    /// No módulo digitado à mão, altura e largura trocadas são um aviso. Na
    /// biblioteca, que é dado nosso, são defeito: ninguém vai conferir o que
    /// veio pronto.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ModuloDeitadoNoJsonERecusado()
    {
        const string json = """
        [{ "brand": "X", "model": "DEITADO", "powerWatts": 700,
           "height": 1.3, "width": 2.3, "thickness": 0.03 }]
        """;

        var erro = Assert.Throws<InvalidOperationException>(() => ModuleLibrary.Parse(json));

        Assert.Contains("DEITADO", erro.Message);
    }

    /// <summary>
    /// Dois módulos com o mesmo modelo fazem a busca devolver um deles sem
    /// critério. O teste é sobre a REGRA, e por isso usa um JSON próprio: um
    /// teste que olhasse só o arquivo da biblioteca continuaria verde mesmo se
    /// a regra fosse apagada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ModeloRepetidoNoJsonERecusado()
    {
        const string json = """
        [
          { "brand": "X", "model": "Y",   "powerWatts": 700, "height": 2.3, "width": 1.3, "thickness": 0.03 },
          { "brand": "Z", "model": " y ", "powerWatts": 710, "height": 2.3, "width": 1.3, "thickness": 0.03 }
        ]
        """;

        var erro = Assert.Throws<InvalidOperationException>(() => ModuleLibrary.Parse(json));

        Assert.Contains("repetido", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Y", erro.Message);
    }

    /// <summary>
    /// A ordem é asserida contra uma lista escrita à mão, e a entrada está
    /// deliberadamente fora de ordem. Recalcular a ordenação com a mesma
    /// expressão da implementação seria uma tautologia: passaria inclusive se a
    /// implementação não ordenasse nada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void OsModulosSaemEmOrdemDeMarcaEModelo()
    {
        const string json = """
        [
          { "brand": "Trina", "model": "TSM-700", "powerWatts": 700, "height": 2.3, "width": 1.3, "thickness": 0.03 },
          { "brand": "Risen", "model": "RSM-740", "powerWatts": 740, "height": 2.3, "width": 1.3, "thickness": 0.03 },
          { "brand": "Trina", "model": "TSM-690", "powerWatts": 690, "height": 2.3, "width": 1.3, "thickness": 0.03 },
          { "brand": "Risen", "model": "RSM-720", "powerWatts": 720, "height": 2.3, "width": 1.3, "thickness": 0.03 }
        ]
        """;

        var modelos = ModuleLibrary.Parse(json).Select(m => m.Model).ToList();

        Assert.Equal(["RSM-720", "RSM-740", "TSM-690", "TSM-700"], modelos);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void ABibliotecaPadraoNaoTemModeloRepetido()
    {
        // O arquivo que vai na DLL também precisa obedecer à regra. Este teste
        // é sobre o DADO; quem testa a regra é ModeloRepetidoNoJsonERecusado.
        var modelos = ModuleLibrary.Default()
            .Select(m => m.Model.Trim().ToUpperInvariant())
            .ToList();

        Assert.Equal(modelos.Count, modelos.Distinct().Count());
    }
}
