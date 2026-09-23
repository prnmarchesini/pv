namespace UFV.Core.Tests;

/// <summary>
/// Estes são os números que o Renan compara com as propriedades da superfície
/// no Civil 3D. Um erro de formatação aqui faz a conferência dele dar falso
/// negativo, e a desconfiança recai sobre a leitura, que estava certa.
/// </summary>
public class TerrainSummaryTests
{
    private static TerrainSummary Exemplo(
        int triangulos = 24_000,
        int descartados = 0,
        double minZ = 610.25,
        double maxZ = 648.75,
        double area2d = 120_000,
        double area3d = 121_450.5) =>
        new("Topografo", triangulos, descartados, minZ, maxZ, area2d, area3d);

    [Fact]
    [Trait("Etapa", "1")]
    public void ODesnivelEADiferencaEntreAsCotas()
    {
        Assert.Equal(38.5, Exemplo().Desnivel, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void HectaresSaemDaAreaEmPlanta()
    {
        // 120 000 m² é uma usina de 12 ha, que é o tamanho típico do projeto.
        Assert.Equal(12, Exemplo().Hectares, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OResumoTrazONomeDaSuperficie()
    {
        Assert.Contains("Terreno processado: Topografo", Exemplo().Lines()[0]);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OsNumerosSaemEmPortugues()
    {
        // Separador de milhar é ponto e decimal é vírgula. Ler "120,000.00 m²"
        // num resumo em português faz o engenheiro conferir errado.
        var linhas = string.Join("\n", Exemplo().Lines());

        Assert.Contains("24.000", linhas);
        Assert.Contains("120.000,00 m²", linhas);
        Assert.Contains("610,250 m", linhas);
        Assert.Contains("12,00 ha", linhas);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SemDescarteNaoSeFalaEmDescarte()
    {
        // Linha a mais num resumo que o usuário confere item a item é ruído.
        var linhas = Exemplo(descartados: 0).Lines();

        Assert.DoesNotContain(linhas, l => l.Contains("descartados"));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void ComDescarteOUsuarioPrecisaSaber()
    {
        // Malha com muitos descartes é levantamento com problema, e ele
        // precisa saber agora — não quando um pilar sair num lugar estranho.
        var linhas = string.Join("\n", Exemplo(descartados: 137).Lines());

        Assert.Contains("descartados", linhas);
        Assert.Contains("137", linhas);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TerrenoPlanoTemDesnivelZero()
    {
        var resumo = Exemplo(minZ: 600, maxZ: 600);

        Assert.Equal(0, resumo.Desnivel);
        Assert.Contains("desnível de 0,000 m", string.Join("\n", resumo.Lines()));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void AAreaDoTerrenoApareceSeparadaDaAreaEmPlanta()
    {
        // São coisas diferentes: a de planta é a do mapa, a do terreno é a de
        // chão de verdade. Num terreno inclinado a segunda é maior, e
        // confundir as duas erra o cálculo de quanto cabe na área.
        var linhas = Exemplo().Lines();

        Assert.Contains(linhas, l => l.Contains("área em planta"));
        Assert.Contains(linhas, l => l.Contains("área do terreno"));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void CotaNegativaNaoQuebraOResumo()
    {
        // Desenho sem sistema de coordenadas, ou terreno abaixo do nível de
        // referência adotado.
        var resumo = Exemplo(minZ: -12.5, maxZ: 3.25);

        Assert.Equal(15.75, resumo.Desnivel, 9);
        Assert.Contains("-12,500 m", string.Join("\n", resumo.Lines()));
    }
}
