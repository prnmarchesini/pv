namespace UFV.Core.Tests;

/// <summary>
/// O carimbo existe por um motivo concreto do plano de requisitos: depois de
/// uma terraplenagem existe terreno novo no mesmo desenho, e é aí que alguém
/// abre um arquivo antigo achando que é o atual e compra pilar do tamanho
/// errado.
///
/// Por isso os dois erros aqui têm pesos diferentes. Deixar passar uma
/// mudança de terreno é o erro caro. Avisar à toa é o erro que ensina o
/// usuário a ignorar o aviso — e aí o primeiro erro volta.
/// </summary>
public class ProvenanceTests
{
    private static SurfaceFingerprint Superficie(
        string handle = "1A2B",
        string nome = "Topografo",
        int revisao = 7,
        int pontos = 12_621,
        int triangulos = 25_107,
        double minZ = 707.0,
        double maxZ = 764.0) =>
        new(handle, nome, revisao, pontos, triangulos, minZ, maxZ);

    private static ProvenanceStamp Carimbo(SurfaceFingerprint? superficie = null) =>
        new(superficie ?? Superficie(), new DateTime(2026, 9, 22, 14, 30, 0), "0.1.0");

    [Fact]
    [Trait("Etapa", "1")]
    public void SemCarimboNaoHaNadaAAvisar()
    {
        // Desenho em que nunca se processou terreno.
        Assert.Equal(ProvenanceState.SemCarimbo, ProvenanceCheck.Evaluate(null, Superficie()));
        Assert.Null(ProvenanceCheck.Warning(null, Superficie()));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SuperficieIntactaNaoGeraAviso()
    {
        // Aviso que aparece à toa é aviso que o usuário aprende a ignorar.
        Assert.Equal(ProvenanceState.Atual, ProvenanceCheck.Evaluate(Carimbo(), Superficie()));
        Assert.Null(ProvenanceCheck.Warning(Carimbo(), Superficie()));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SuperficieApagadaEDetectada()
    {
        var estado = ProvenanceCheck.Evaluate(Carimbo(), agora: null);
        var aviso = ProvenanceCheck.Warning(Carimbo(), agora: null);

        Assert.Equal(ProvenanceState.SuperficieSumiu, estado);
        Assert.Contains("não está mais aqui", aviso);
        Assert.Contains("Topografo", aviso);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TerraplenagemEDetectada()
    {
        // O caso que motiva o carimbo: a superfície foi reconstruída, tem
        // outra quantidade de pontos e outras cotas.
        var depois = Superficie(revisao: 9, pontos: 15_000, triangulos: 29_880, minZ: 705.5, maxZ: 761.2);

        Assert.Equal(ProvenanceState.Desatualizado, ProvenanceCheck.Evaluate(Carimbo(), depois));

        var aviso = ProvenanceCheck.Warning(Carimbo(), depois);

        Assert.Contains("desatualizados", aviso);
        Assert.Contains("22/09/2026 14:30", aviso);
        Assert.Contains("pontos", aviso);
        Assert.Contains("cotas", aviso);
    }

    [Theory]
    [Trait("Etapa", "1")]
    [InlineData("revisão")]
    [InlineData("pontos")]
    [InlineData("triângulos")]
    [InlineData("cota mínima")]
    [InlineData("cota máxima")]
    public void QualquerMudancaSozinhaJaInvalida(string oQueMudou)
    {
        // Cada uma destas, isolada, significa que a superfície foi mexida.
        // Nenhuma pode passar despercebida só porque as outras continuam iguais.
        var depois = oQueMudou switch
        {
            "revisão" => Superficie(revisao: 8),
            "pontos" => Superficie(pontos: 12_622),
            "triângulos" => Superficie(triangulos: 25_108),
            "cota mínima" => Superficie(minZ: 706.99),
            "cota máxima" => Superficie(maxZ: 764.01),
            _ => throw new ArgumentOutOfRangeException(nameof(oQueMudou)),
        };

        Assert.Equal(
            ProvenanceState.Desatualizado,
            ProvenanceCheck.Evaluate(Carimbo(), depois));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void RenomearASuperficieNaoInvalidaOTerreno()
    {
        // Renomear não muda cota nenhuma. Invalidar por isso só ensinaria o
        // usuário a desconfiar do aviso.
        var renomeada = Superficie(nome: "Topografo - revisado");

        Assert.Equal(ProvenanceState.Atual, ProvenanceCheck.Evaluate(Carimbo(), renomeada));
        Assert.Null(ProvenanceCheck.Warning(Carimbo(), renomeada));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OutraSuperficieComOsMesmosNumerosNaoPassaPorEssa()
    {
        // Duas superfícies podem ter a mesma contagem e as mesmas cotas —
        // uma cópia, por exemplo. O handle é o que distingue.
        var copia = Superficie(handle: "9F9F");

        Assert.Equal(ProvenanceState.Desatualizado, ProvenanceCheck.Evaluate(Carimbo(), copia));
        Assert.Contains("outra superfície", ProvenanceCheck.Warning(Carimbo(), copia));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MudancaDeCotaAbaixoDoMilimetroNaoConta()
    {
        // Um milímetro é a tolerância dos verificadores de regra sagrada.
        // Abaixo disso é ruído de arredondamento, não terraplenagem.
        var ruido = Superficie(minZ: 707.0004, maxZ: 763.9996);

        Assert.Equal(ProvenanceState.Atual, ProvenanceCheck.Evaluate(Carimbo(), ruido));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void OAvisoDizOQueMudouENaoSoQueMudou()
    {
        // "Está desatualizado" sem dizer o quê deixa o engenheiro sem saber se
        // confia ou não. O aviso mostra o antes e o depois.
        var depois = Superficie(triangulos: 30_000);
        var mudancas = depois.DescribeChangesFrom(Superficie());

        Assert.Single(mudancas);
        Assert.Contains("25.107", mudancas[0]);
        Assert.Contains("30.000", mudancas[0]);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SuperficieIgualNaoTemMudancaAListar()
    {
        Assert.Empty(Superficie().DescribeChangesFrom(Superficie()));
    }
}
