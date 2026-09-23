namespace UFV.Core.Tests;

/// <summary>
/// O comprimento da mesa é o número de que tudo depois depende: quantas mesas
/// cabem na área, onde ficam os pilares, quanto de estrutura comprar.
///
/// Ele não vem de medição nenhuma — é uma soma. E é justamente por ser uma soma
/// fácil que ninguém confere: erra-se o número de espaçamentos (27 e não 28),
/// ou esquece-se a sobra de uma das pontas, e o resultado continua parecendo
/// razoável.
/// </summary>
public class TableLayoutTests
{
    /// <summary>O módulo do Renan, com as medidas do datasheet.</summary>
    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    /// <summary>
    /// A mesa de referência do Renan, dada por ele em 23/09/2026: 28 módulos,
    /// 2 cm de espaçamento nos dois sentidos, 10 cm de sobra de cada lado.
    /// </summary>
    private static TableLayout Referencia(TableArrangement arranjo = TableArrangement.SingleRow) =>
        new(Risen(), 28, arranjo, 0.02, 0.02, 0.10, 0.10);

    /// <summary>
    /// 28 × 1,303 + 27 × 0,02 + 0,10 + 0,10 = 37,224 m.
    ///
    /// São 27 espaçamentos e não 28: eles ficam ENTRE os módulos, e entre 28
    /// módulos há 27 vãos. Errar isso soma 2 cm — invisível — e a conta passa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AMesaDeReferenciaEm1VTem37224()
    {
        var mesa = Referencia();

        Assert.True(mesa.IsValid);
        Assert.Equal(28, mesa.Columns);
        Assert.Equal(37.224, mesa.Length, 6);
    }

    /// <summary>
    /// Em 1V a mesa tem a altura de um módulo só, e nenhum espaçamento
    /// vertical entra na conta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void Em1VALarguraDaMesaEADeUmModulo()
    {
        Assert.Equal(2.384, Referencia().Depth, 6);
    }

    /// <summary>
    /// Em 2V os 28 módulos viram 14 colunas de dois:
    /// 14 × 1,303 + 13 × 0,02 + 0,20 = 18,702 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AMesaDeReferenciaEm2VTemMetadeDasColunas()
    {
        var mesa = Referencia(TableArrangement.DoubleRow);

        Assert.True(mesa.IsValid);
        Assert.Equal(14, mesa.Columns);
        Assert.Equal(18.702, mesa.Length, 6);
    }

    /// <summary>
    /// Em 2V a mesa tem dois módulos de altura, com o espaçamento vertical
    /// entre eles: 2 × 2,384 + 0,02 = 4,788 m.
    ///
    /// É esse número que não cabe numa tesoura de 3 m, e é por isso que a mesa
    /// de referência do Renan é 1V.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void Em2VALarguraDaMesaSomaOsDoisModulosEOEspacamento()
    {
        Assert.Equal(4.788, Referencia(TableArrangement.DoubleRow).Depth, 6);
    }

    /// <summary>
    /// Ímpar em 2V deixaria uma coluna pela metade. O plugin não escolhe por
    /// conta própria qual ponta fica vazia — isso é decisão de projeto.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void NumeroImparDeModulosNaoFechaUmaMesa2V()
    {
        var mesa = new TableLayout(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.False(mesa.IsValid);
        Assert.Contains("par", mesa.WhyInvalid!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void NumeroImparDeModulosFechaUmaMesa1V()
    {
        var mesa = new TableLayout(Risen(), 27, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.True(mesa.IsValid);
        Assert.Equal(27, mesa.Columns);
    }

    /// <summary>
    /// Uma mesa de um módulo não tem espaçamento nenhum: a conta precisa dar
    /// largura mais as duas sobras, e não largura menos um espaçamento.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaDeUmModuloNaoTemEspacamento()
    {
        var mesa = new TableLayout(Risen(), 1, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.True(mesa.IsValid);
        Assert.Equal(1.503, mesa.Length, 6);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0)]
    [InlineData(-1)]
    public void MesaSemModuloNaoEValida(int quantos)
    {
        var mesa = new TableLayout(Risen(), quantos, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.False(mesa.IsValid);
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void MesaComModuloInvalidoNaoEValida()
    {
        var quebrado = new SolarModule("X", "Y", 720, 0, 1.303, 0.033);
        var mesa = new TableLayout(quebrado, 28, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.False(mesa.IsValid);
        Assert.Contains("medida impossível", mesa.WhyInvalid!);
    }

    /// <summary>
    /// Espaçamento e sobra podem ser zero — estrutura encostada é projeto
    /// legítimo. Negativo não: seria módulo sobrepondo módulo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void EspacamentoESobraPodemSerZero()
    {
        var mesa = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0, 0, 0, 0);

        Assert.True(mesa.IsValid);
        Assert.Equal(28 * 1.303, mesa.Length, 6);
    }

    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-0.02, 0.10, 0.10, "espaçamento entre módulos")]
    [InlineData(0.02, -0.10, 0.10, "sobra da esquerda")]
    [InlineData(0.02, 0.10, -0.10, "sobra da direita")]
    [InlineData(double.NaN, 0.10, 0.10, "espaçamento entre módulos")]
    [InlineData(0.02, double.PositiveInfinity, 0.10, "sobra da esquerda")]
    // Rede para erro de escala: quem digitou 20 achando que o campo era em
    // centímetro. Não é regra de projeto, é o mesmo teto que o módulo tem.
    [InlineData(20, 0.10, 0.10, "espaçamento entre módulos")]
    [InlineData(0.02, 40, 0.10, "sobra da esquerda")]
    public void MedidaNegativaOuNaoFinitaNaoEValida(
        double horizontal, double esquerda, double direita, string campo)
    {
        var mesa = new TableLayout(
            Risen(), 28, TableArrangement.SingleRow, horizontal, 0.02, esquerda, direita);

        Assert.False(mesa.IsValid);

        // O motivo nomeia o campo, e o teste exige isso: sem esta asserção,
        // trocar a ordem das checagens apontaria a sobra errada na janela do
        // 3.7 e nenhum teste notaria.
        Assert.Contains(campo, mesa.WhyInvalid!);
    }

    /// <summary>
    /// Em 2V o espaçamento vertical participa da conta, e aí medida impossível
    /// nele derruba a mesa — ao contrário do 1V, onde o campo é ignorado.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(-0.02)]
    [InlineData(double.NaN)]
    public void Em2VOEspacamentoVerticalImpossivelDerrubaAMesa(double vertical)
    {
        var mesa = new TableLayout(
            Risen(), 28, TableArrangement.DoubleRow, 0.02, vertical, 0.10, 0.10);

        Assert.False(mesa.IsValid);
        Assert.Contains("fileiras", mesa.WhyInvalid!);
    }

    /// <summary>
    /// Em 1V o espaçamento vertical não entra em conta nenhuma. Se ele for
    /// absurdo, a mesa continua válida — não há duas fileiras para separar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void Em1VOEspacamentoVerticalNaoAfetaNada()
    {
        var semEspaco = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0.02, 0, 0.10, 0.10);
        var comEspaco = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0.02, 0.5, 0.10, 0.10);

        Assert.Equal(semEspaco.Length, comEspaco.Length, 9);
        Assert.Equal(semEspaco.Depth, comEspaco.Depth, 9);

        // Nem mesmo um valor impossível derruba a mesa: não se recusa um
        // projeto por causa de um campo que não participa de conta nenhuma.
        var absurdo = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0.02, -9, 0.10, 0.10);

        Assert.True(absurdo.IsValid);
        Assert.Equal(semEspaco.Length, absurdo.Length, 9);
    }

    /// <summary>
    /// Sobras diferentes de cada lado existem: a estrutura nem sempre é
    /// simétrica. A conta tem que somar as duas, e não dobrar uma.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AsDuasSobrasEntramSeparadas()
    {
        var mesa = new TableLayout(Risen(), 28, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.25);

        Assert.Equal(37.224 + 0.15, mesa.Length, 6);
    }

    /// <summary>
    /// O texto vai para a linha de comando do AutoCAD, em português, e a
    /// máquina do projetista pode estar em qualquer idioma.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ADescricaoTrazOComprimentoComVirgula()
    {
        var texto = Referencia().Describe();

        Assert.Contains("37,224", texto);
        Assert.Contains("28", texto);
        Assert.DoesNotContain("37.224", texto);
    }

    /// <summary>
    /// A mesa inválida não pode devolver comprimento plausível: quem esquecer
    /// de olhar o IsValid levaria um número que parece certo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaInvalidaNaoDevolveComprimento()
    {
        var mesa = new TableLayout(Risen(), 0, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.Throws<InvalidOperationException>(() => mesa.Length);
    }

    /// <summary>
    /// A mesa válida não tem motivo de invalidez, e a inválida sempre tem um.
    /// Sem isto, a janela do 3.7 mostraria "inválida" sem dizer por quê.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void TodaMesaInvalidaDizPorQue()
    {
        Assert.Null(Referencia().WhyInvalid);

        TableLayout[] quebradas =
        [
            new(Risen(), 0, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10),
            new(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new(Risen(), 28, TableArrangement.SingleRow, -0.02, 0.02, 0.10, 0.10),
            new(new SolarModule("X", "Y", 720, 0, 1.303, 0.033), 28,
                TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10),
        ];

        foreach (var mesa in quebradas)
        {
            Assert.False(mesa.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(mesa.WhyInvalid));
        }
    }

    /// <summary>
    /// O buraco que a revisão do 3.2 achou: com os dois espaçamentos valendo
    /// 0,02 em todos os testes, trocar o vertical pelo horizontal na conta da
    /// profundidade era indetectável — e "espaçamento vertical próprio" é a
    /// única coisa que o 2V acrescenta ao enunciado do passo.
    ///
    /// Aqui eles são diferentes de propósito: 0,02 na horizontal e 0,05 na
    /// vertical. Trocar um pelo outro muda um dos dois números.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void Em2VOsDoisEspacamentosVaoParaContasDiferentes()
    {
        var mesa = new TableLayout(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.05, 0.10, 0.10);

        // 14 × 1,303 + 13 × 0,02 + 0,20 — só o horizontal entra.
        Assert.Equal(18.702, mesa.Length, 6);

        // 2 × 2,384 + 0,05 — só o vertical entra.
        Assert.Equal(4.818, mesa.Depth, 6);
    }

    /// <summary>
    /// Depth lança pelo mesmo motivo que Length: um número plausível vindo de
    /// uma mesa que não existe é pior que uma exceção.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaInvalidaNaoDevolveProfundidadeNemColunas()
    {
        var mesa = new TableLayout(Risen(), 27, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.Throws<InvalidOperationException>(() => mesa.Depth);

        // Sem esta guarda, Columns devolveria 13 por truncamento — e 13 é um
        // número que o 3.3 usaria para posicionar pilar sem desconfiar.
        Assert.Throws<InvalidOperationException>(() => mesa.Columns);
    }

    /// <summary>
    /// O parâmetro é não-anulável, mas null! atravessa. Sem esta defesa, Length
    /// lançaria NullReferenceException vazando do Core.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaSemModuloNenhumDizPorQueEmVezDeExplodir()
    {
        var mesa = new TableLayout(null!, 28, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.False(mesa.IsValid);
        Assert.Contains("módulo", mesa.WhyInvalid!);
        Assert.Throws<InvalidOperationException>(() => mesa.Length);
    }

    /// <summary>
    /// Mesa de mil módulos teria quase um quilômetro e meio. Não existe: quem
    /// digitou isso errou o campo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void MesaComModulosDemaisNaoEValida()
    {
        var mesa = new TableLayout(Risen(), 100_000, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.False(mesa.IsValid);
    }

    /// <summary>
    /// Ímpar em 1V fecha, e o comprimento tem que fechar junto:
    /// 27 × 1,303 + 26 × 0,02 + 0,20 = 35,901 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void AMesaImparEm1VTemOComprimentoCerto()
    {
        var mesa = new TableLayout(Risen(), 27, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);

        Assert.Equal(27, mesa.Columns);
        Assert.Equal(35.901, mesa.Length, 6);
    }
}
