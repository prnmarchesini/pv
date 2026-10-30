namespace Clivus.Core.Tests;

/// <summary>
/// A estrutura fixa bifila no perfil da mesa (passo 37.1): dois pilares por
/// pórtico, mãos francesas, terças e tirantes, guardados dentro do
/// <see cref="TableFrame"/>.
///
/// O risco deste passo é duplo: a bifila voltar diferente do arquivo, e a
/// monoposte deixar de sair como sempre saiu. Os dois têm teste aqui; a
/// bateria antiga do perfil cobre o segundo pelo outro lado.
///
/// Os números são os dos desenhos da Arctech (plano/bifila/01-modelo-de-referencia.md).
/// </summary>
public class BifilaFrameTests
{
    private const double Grau = Math.PI / 180;

    private static TableProfile Gc800() => BifilaReference.Profile(BifilaVariant.Gc800);

    private static TableProfile Gc1000() => BifilaReference.Profile(BifilaVariant.Gc1000);

    private static TableProfile Monoposte() => new(
        "Mesa do Renan 28 módulos",
        new TableLayout(new SolarModule("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
        new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0),
        20 * Grau);

    // ------------------------------------------------- o modelo de referência

    [Fact]
    [Trait("Etapa", "37")]
    public void OsDoisPadroesDaArctechSaoMesasQueExistem()
    {
        Assert.Null(Gc800().WhyInvalid);
        Assert.Null(Gc1000().WhyInvalid);
        Assert.True(Gc800().IsBifila);
        Assert.False(Monoposte().IsBifila);
    }

    /// <summary>Os números do desenho, um a um: é deles que saem os números de ouro do 37.2.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OPadraoGc800TemAsMedidasDoDesenho()
    {
        var p = Gc800();

        Assert.Equal(10, p.TiltDegrees, 9);
        Assert.Equal("JAM66D46-720/LB", p.Layout.Module.Model);
        Assert.Equal(28, p.Layout.ModuleCount);
        Assert.Equal(TableArrangement.DoubleRow, p.Layout.Arrangement);
        Assert.Equal(18.602, p.Layout.Length, 9);
        Assert.Equal(4.788, p.Layout.Depth, 9);

        Assert.Equal(4.030, p.Frame.RafterLength, 9);
        Assert.Equal(0.498, p.Frame.PillarAlongRafter, 9);
        Assert.Equal(2.200, p.Frame.MinEmbedment!.Value, 9);
        Assert.Equal([3.2, 3.5, 3.5, 3.5, 3.2], p.Frame.PillarSpans!);
        Assert.Equal(0.851, p.Frame.PillarCantilever, 9);

        var b = p.Frame.Bifila!;
        Assert.Equal(3.544, b.RearPillarAlongRafter, 9);
        Assert.Equal(0.126, b.PillarTopDrop, 9);
        Assert.Equal(1.244, b.FrontBraceLength, 9);
        Assert.Equal(0.447, b.FrontBraceBelowTop, 9);
        Assert.Equal(1.209, b.RearBraceLength, 9);
        Assert.Equal(0.773, b.RearBraceBelowTop, 9);
        Assert.Equal(0.492, b.PurlinFromModuleEdge, 9);
        Assert.Equal(BifilaRodLine.Rear, b.RodLine);
        Assert.Equal([2, 4], b.RodSpans);
        Assert.Equal(0.20, b.RodUpperBelowTop, 9);
        Assert.Equal(0.60, b.RodLowerBelowTop, 9);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void OPadraoGc1000SoMudaAsMaosFrancesas()
    {
        var a = Gc800();
        var b = Gc1000();

        Assert.Equal(1.296, b.Frame.Bifila!.FrontBraceLength, 9);
        Assert.Equal(0.548, b.Frame.Bifila.FrontBraceBelowTop, 9);
        Assert.Equal(1.233, b.Frame.Bifila.RearBraceLength, 9);
        Assert.Equal(0.826, b.Frame.Bifila.RearBraceBelowTop, 9);

        // Sem as mãos francesas e o nome, os dois são a mesma mesa.
        var semMaos = b.Frame.Bifila with
        {
            FrontBraceLength = a.Frame.Bifila!.FrontBraceLength,
            FrontBraceBelowTop = a.Frame.Bifila.FrontBraceBelowTop,
            RearBraceLength = a.Frame.Bifila.RearBraceLength,
            RearBraceBelowTop = a.Frame.Bifila.RearBraceBelowTop,
        };
        Assert.Equal(a with { Name = "x" }, b with { Name = "x", Frame = b.Frame with { Bifila = semMaos } });
    }

    /// <summary>Os 3,000 m entre pilares do desenho são na horizontal; na tesoura eles são 3,046.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OsPilaresFicamATresMetrosNaHorizontal()
    {
        Assert.Equal(3.000, Gc800().BifilaHorizontalSpacing!.Value, 3);
        Assert.Null(Monoposte().BifilaHorizontalSpacing);

        // E a volta: da distância na horizontal para a posição na tesoura.
        Assert.Equal(3.544, BifilaFrame.RearAlongRafter(0.498, 3.000, 10 * Grau), 3);
    }

    // ------------------------------------------------------- o arquivo

    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaVoltaIgualDoArquivo()
    {
        foreach (var original in new[] { Gc800(), Gc1000() })
        {
            var voltou = TableProfile.Parse(original.ToJson());

            Assert.Equal(original, voltou);
            Assert.Equal(original.Frame.Bifila!.RodSpans, voltou.Frame.Bifila!.RodSpans);
            Assert.Equal(original.Frame.Bifila.Beam, voltou.Frame.Bifila.Beam);
        }
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaSaiNaVersao4()
    {
        var json = Gc800().ToJson();

        Assert.Contains("\"formatVersion\": 4", json);
        Assert.Contains("\"bifila\"", json);
        Assert.Contains("\"rodLine\": \"Rear\"", json);
    }

    /// <summary>
    /// A regressão do passo: a monoposte continua saindo na versão 2, sem
    /// nenhum campo novo, exatamente como antes da bifila existir.
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AMonoposteContinuaSaindoComoSempre()
    {
        var json = Monoposte().ToJson();

        Assert.Contains("\"formatVersion\": 2", json);
        Assert.DoesNotContain("bifila", json, StringComparison.OrdinalIgnoreCase);

        var comPan = Monoposte().WithPan(new PanModule("Risen", "RSM132", 720, 49.5, 18.5, 41.2, 17.5, -0.25, 0.04, null, null)).ToJson();
        Assert.Contains("\"formatVersion\": 3", comPan);
        Assert.DoesNotContain("bifila", comPan, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A bifila com os dados elétricos do módulo também é versão 4: a 4 aceita os dois.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaComPanTambemVoltaIgual()
    {
        var original = Gc800().WithPan(new PanModule("JA Solar", "JAM66D46-720/LB", 720, 50.5, 18.0, 42.0, 17.1, -0.25, 0.045, null, null));
        var json = original.ToJson();

        Assert.Contains("\"formatVersion\": 4", json);
        Assert.Equal(original, TableProfile.Parse(json));
    }

    /// <summary>Arquivo mexido à mão: versão 4 sem a bifila, ou 2/3 com ela, não é lido pela metade.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void VersaoEBifilaTemQueBater()
    {
        var bifila = Gc800().ToJson();
        var mono = Monoposte().ToJson();

        // A mensagem diz o tipo com o nome da tela (biposte, desde 38.5).
        var erro1 = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(bifila.Replace("\"formatVersion\": 4", "\"formatVersion\": 2")));
        Assert.Contains("biposte", erro1.Message);

        var erro2 = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(mono.Replace("\"formatVersion\": 2", "\"formatVersion\": 4")));
        Assert.Contains("biposte", erro2.Message);
    }

    /// <summary>
    /// Cada campo da bifila, tirado do arquivo, é recusado pelo nome. O padrão
    /// casa o campo inteiro, inclusive objeto ("beam") e lista ("rodSpans",
    /// que o JSON indentado escreve em várias linhas).
    /// </summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData("frame.bifila.rearPillarAlongRafter", @"""rearPillarAlongRafter"": [^,]*,")]
    [InlineData("frame.bifila.pillarTopDrop", @"""pillarTopDrop"": [^,]*,")]
    [InlineData("frame.bifila.frontBraceLength", @"""frontBraceLength"": [^,]*,")]
    [InlineData("frame.bifila.frontBraceBelowTop", @"""frontBraceBelowTop"": [^,]*,")]
    [InlineData("frame.bifila.rearBraceLength", @"""rearBraceLength"": [^,]*,")]
    [InlineData("frame.bifila.rearBraceBelowTop", @"""rearBraceBelowTop"": [^,]*,")]
    [InlineData("frame.bifila.purlinFromModuleEdge", @"""purlinFromModuleEdge"": [^,]*,")]
    [InlineData("frame.bifila.purlinMaxBar", @"""purlinMaxBar"": [^,]*,")]
    [InlineData("frame.bifila.rodLine", @"""rodLine"": [^,]*,")]
    [InlineData("frame.bifila.rodSpans", @"""rodSpans"": \[[^\]]*\],")]
    [InlineData("frame.bifila.rodUpperBelowTop", @"""rodUpperBelowTop"": [^,]*,")]
    [InlineData("frame.bifila.rodLowerBelowTop", @"""rodLowerBelowTop"": [^,]*,")]
    [InlineData("frame.bifila.beam", @"""beam"": \{[^}]*\},")]
    [InlineData("frame.bifila.purlin", @"""purlin"": \{[^}]*\},")]
    [InlineData("frame.bifila.brace", @"""brace"": \{[^}]*\},")]
    [InlineData("frame.bifila.rodDiameter", @",\s*""rodDiameter"": [^\s}]*")]
    public void CampoDaBifilaAusenteERecusadoPeloNome(string campo, string padrao)
    {
        var json = Gc800().ToJson();
        var sem = System.Text.RegularExpressions.Regex.Replace(json, padrao, string.Empty);
        Assert.NotEqual(json, sem);

        var erro = Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(sem));
        Assert.Contains(campo, erro.Message);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void CampoDesconhecidoNaBifilaERecusado()
    {
        var json = Gc800().ToJson().Replace("\"pillarTopDrop\": 0.126,", "\"pillarTopDrop\": 0.126,\n      \"bracesPerFrame\": 2,");

        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));
    }

    /// <summary>
    /// A linha do tirante só pelo nome exato: nem número, nem número entre
    /// aspas (que o conversor do .NET aceitava), nem combinação de nomes.
    /// </summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("\"Rear, Front\"")]
    [InlineData("\"rear\"")]
    [InlineData("\"Atrás\"")]
    public void LinhaDeTiranteSoPeloNome(string valor)
    {
        var json = Gc800().ToJson().Replace("\"rodLine\": \"Rear\"", "\"rodLine\": " + valor);

        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));
    }

    // ------------------------------------------------------- o que recusar

    public static TheoryData<string, Func<BifilaFrame, BifilaFrame>> Erradas() => new()
    {
        { "traseiro", b => b with { RearPillarAlongRafter = 0.40 } },          // antes do dianteiro
        { "traseiro", b => b with { RearPillarAlongRafter = 0.55 } },          // colado no dianteiro (seção de 0,095)
        { "traseiro", b => b with { RearPillarAlongRafter = 4.10 } },          // fora da tesoura de 4,030
        { "traseiro", b => b with { RearPillarAlongRafter = double.NaN } },
        { "topo", b => b with { PillarTopDrop = -0.01 } },
        { "topo", b => b with { PillarTopDrop = 1.5 } },
        { "mão francesa", b => b with { FrontBraceLength = -1 } },
        { "mão francesa", b => b with { FrontBraceLength = 1244 } },           // em milímetro
        { "mão francesa", b => b with { RearBraceBelowTop = 0 } },              // peça que pega no topo
        { "terça", b => b with { PurlinFromModuleEdge = 0 } },
        { "terça", b => b with { PurlinMaxBar = 0.5 } },
        { "tirante", b => b with { RodUpperBelowTop = 0.6, RodLowerBelowTop = 0.2 } },
        { "tirante", b => b with { RodSpans = [0] } },
        { "tirante", b => b with { RodSpans = [2, 2] } },
        { "tirante", b => b with { RodDiameter = 0 } },
        { "tirante", b => b with { RodSpans = [] } },                           // linha sem vão
        { "tirante", b => b with { RodLine = BifilaRodLine.None } },            // vão sem linha
        { "tirante", b => b with { RodLine = BifilaRodLine.None, RodSpans = [], RodDiameter = double.PositiveInfinity } },
        { "mão francesa", b => b with { FrontBraceLength = 0, FrontBraceBelowTop = double.NaN } },
        { "topo", b => b with { PillarTopDrop = 0 } },                           // fura o módulo
        { "topo", b => b with { PillarTopDrop = 0.5 } },                         // abaixo da viga
        { "terça", b => b with { PurlinFromModuleEdge = 0.30 } },               // fora da tesoura (sobra 0,379)
        { "viga", b => b with { Beam = new BarSection(0.05, 0) } },
        { "terça", b => b with { Purlin = new BarSection(50, 0.1) } },
        { "mão francesa", b => b with { Brace = new BarSection(0.04, double.PositiveInfinity) } },
    };

    [Theory]
    [Trait("Etapa", "37")]
    [MemberData(nameof(Erradas))]
    public void BifilaQueNaoExisteERecusadaComOMotivo(string palavra, Func<BifilaFrame, BifilaFrame> estragar)
    {
        var perfil = Gc800();
        var errado = perfil with { Frame = perfil.Frame with { Bifila = estragar(perfil.Frame.Bifila!) } };

        var motivo = errado.WhyInvalid;

        Assert.NotNull(motivo);
        Assert.Contains(palavra, motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => errado.ToJson());
    }

    /// <summary>Sem mão francesa e sem tirante também é bifila: comprimento zero e linha nenhuma.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void BifilaSemMaoFrancesaNemTiranteExiste()
    {
        var perfil = Gc800();
        var nua = perfil with
        {
            Frame = perfil.Frame with
            {
                Bifila = perfil.Frame.Bifila! with
                {
                    FrontBraceLength = 0,
                    RearBraceLength = 0,
                    RodLine = BifilaRodLine.None,
                    RodSpans = [],
                },
            },
        };

        Assert.Null(nua.WhyInvalid);
        Assert.Equal(nua, TableProfile.Parse(nua.ToJson()));
    }

    /// <summary>Tirante num vão que a mesa não tem: a 2P14 tem 5 vãos.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void TiranteNumVaoQueNaoExisteERecusado()
    {
        var perfil = Gc800();
        var errado = perfil with { Frame = perfil.Frame with { Bifila = perfil.Frame.Bifila! with { RodSpans = [2, 6] } } };

        var motivo = errado.WhyInvalid;

        Assert.NotNull(motivo);
        Assert.Contains("6", motivo);
        Assert.Contains("5", motivo);
    }

    /// <summary>As duas terças da fileira de módulos não podem se cruzar: 2 × 1,2 é mais que os 2,384 do módulo.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void TercaQueNaoCabeNoModuloERecusada()
    {
        var perfil = Gc800();
        var errado = perfil with { Frame = perfil.Frame with { Bifila = perfil.Frame.Bifila! with { PurlinFromModuleEdge = 1.2 } } };

        Assert.Contains("terça", errado.WhyInvalid!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Os pilares encostados em planta: 0,10 m na tesoura a 60° são 0,05 m no
    /// chão, menos que os 0,095 do pilar. A conta é em planta, e não na tesoura.
    /// </summary>
    [Theory]
    [Trait("Etapa", "37")]
    [InlineData(60)]
    [InlineData(90)]
    public void PilaresQueSeEncostamEmPlantaSaoRecusados(double graus)
    {
        var perfil = Gc800();
        var errado = perfil with
        {
            TiltRadians = graus * Grau,
            Frame = perfil.Frame with { Bifila = perfil.Frame.Bifila! with { RearPillarAlongRafter = perfil.Frame.PillarAlongRafter + 0.10 } },
        };

        Assert.Contains("traseiro", errado.WhyInvalid!);
    }

    /// <summary>
    /// Vão-alvo absurdo na bifila sem vãos escritos: a tabela de pilares não
    /// fecha, e a validação diz isso em vez de lançar exceção (que derrubava a
    /// leitura da lista de mesas do desenho inteira).
    /// </summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void TabelaQueNaoFechaViraMotivoENaoExcecao()
    {
        var perfil = Gc800();
        var errado = perfil with { Frame = perfil.Frame with { PillarSpans = null, PillarSpanTarget = 0.01 } };

        Assert.NotNull(errado.WhyInvalid);

        var json = System.Text.RegularExpressions.Regex.Replace(perfil.ToJson(), @"""pillarSpans"": \[[^\]]*\],", string.Empty)
            .Replace("\"pillarSpanTarget\": 3.5", "\"pillarSpanTarget\": 0.01");
        Assert.Throws<InvalidOperationException>(() => TableProfile.Parse(json));
    }

    /// <summary>"4; 2" e "2; 4" são a mesma estrutura: os vãos ficam em ordem.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void OsVaosDoTiranteFicamEmOrdem()
    {
        var b = Gc800().Frame.Bifila!;
        var trocada = b with { RodSpans = [4, 2] };

        Assert.Equal([2, 4], trocada.RodSpans);
        Assert.Equal(b, trocada);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void ADistanciaNaTesouraRecusaMesaEmPe()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BifilaFrame.RearAlongRafter(0.498, 3.0, Math.PI / 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => BifilaFrame.RearAlongRafter(0.498, 3.0, double.NaN));
    }

    // ------------------------------------------------------- igualdade e texto

    [Fact]
    [Trait("Etapa", "37")]
    public void ABifilaEntraNaIgualdadeDaEstrutura()
    {
        var a = Gc800().Frame;

        Assert.NotEqual(a, a with { Bifila = null });
        Assert.NotEqual(a, a with { Bifila = a.Bifila! with { RodSpans = [2] } });

        // Lista comparada pelo valor, e não pela referência.
        var b = a with { Bifila = a.Bifila! with { RodSpans = [2, 4] } };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>A lista de vãos é copiada: mexer na de quem chamou não muda a estrutura.</summary>
    [Fact]
    [Trait("Etapa", "37")]
    public void AListaDeVaosDoTiranteECopiada()
    {
        var lista = new List<int> { 2, 4 };
        var b = Gc800().Frame.Bifila! with { RodSpans = lista };

        lista.Add(5);

        Assert.Equal([2, 4], b.RodSpans);
    }

    [Fact]
    [Trait("Etapa", "37")]
    public void ADescricaoDizQueEBifila()
    {
        var texto = Gc800().Frame.Describe();

        Assert.Contains("biposte", texto);
        Assert.Contains("3,544", texto);
        Assert.DoesNotContain("biposte", Monoposte().Frame.Describe());
    }
}
