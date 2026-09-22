using System.Diagnostics;
using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// O índice espacial não pode mudar a resposta — só o caminho até ela.
///
/// O erro que um índice comete é perder um triângulo: o ponto está dentro do
/// terreno e a consulta responde "fora". Isso não aparece em teste que use só
/// o índice, porque ele responde o mesmo das duas vezes. Por isso quase todo
/// teste aqui compara com a varredura linear, que é a resposta de referência.
///
/// E as malhas são ONDULADAS, com cota diferente em cada vértice. Num terreno
/// plano, escolher o triângulo vizinho errado devolve exatamente a mesma cota,
/// e a comparação não conseguiria distinguir acerto de erro.
/// </summary>
public class TriangleGridTests
{
    /// <summary>
    /// Cota ondulada e determinística. Não é plano: dois triângulos vizinhos
    /// dão respostas diferentes para o mesmo ponto, que é o que faz a escolha
    /// do triângulo importar.
    /// </summary>
    private static double Z(double x, double y) =>
        10 + 0.02 * x + 0.03 * y
           + 2.5 * Math.Sin(x * 0.37)
           + 1.7 * Math.Cos(y * 0.29)
           + 0.9 * Math.Sin((x + y) * 0.11);

    /// <summary>
    /// Quadriculado de <paramref name="lado"/> × <paramref name="lado"/>
    /// células, duas faces cada, sobre o terreno ondulado.
    /// </summary>
    /// <param name="deslocamento">
    /// Move a malha inteira, para a grade não nascer alinhada com a origem.
    /// </param>
    private static Tin Quadriculado(int lado, double passo = 10.0, double deslocamento = 0.0)
    {
        Point3 P(double x, double y) => new(x, y, Z(x, y));

        var triangulos = new List<Triangle>(lado * lado * 2);

        for (var i = 0; i < lado; i++)
        for (var j = 0; j < lado; j++)
        {
            var x = deslocamento + i * passo;
            var y = deslocamento + j * passo;

            triangulos.Add(new Triangle(P(x, y), P(x + passo, y), P(x + passo, y + passo)));
            triangulos.Add(new Triangle(P(x, y), P(x + passo, y + passo), P(x, y + passo)));
        }

        return new Tin(triangulos);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void IndiceEVarreduraLinearRespondemIgual()
    {
        // O teste que o plano pede: 10 mil pontos aleatórios, mesma resposta
        // com e sem índice. Semente fixa, para uma falha ser reproduzível.
        var tin = Quadriculado(20);
        var sorteio = new Random(20260922);

        var dentro = 0;

        for (var n = 0; n < 10_000; n++)
        {
            // A faixa passa da borda de propósito: parte dos pontos cai fora,
            // e "fora" também precisa bater entre os dois caminhos.
            var x = sorteio.NextDouble() * 240 - 20;
            var y = sorteio.NextDouble() * 240 - 20;

            var comIndice = tin.TryGetZ(x, y, out var zIndice);
            var linear = tin.TryGetZLinear(x, y, out var zLinear);

            Assert.True(
                comIndice == linear,
                $"({x}, {y}): índice disse {comIndice}, varredura disse {linear}.");

            if (!comIndice) continue;

            dentro++;

            // Terreno ondulado: se o índice entregasse o triângulo vizinho, a
            // cota sairia diferente e esta igualdade cairia.
            Assert.Equal(zLinear, zIndice, 9);
        }

        Assert.True(dentro > 3_000, $"Só {dentro} pontos caíram dentro; o teste não exercitou o índice.");
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaDesalinhadaDaGradeRespondeIgual()
    {
        // Numa malha cujo passo casa com o tamanho da célula, cada célula
        // contém um quadrado inteiro e nenhum triângulo fica a cavalo de uma
        // fronteira. O passo irracional aqui garante o contrário: os
        // triângulos cruzam fronteiras em posições arbitrárias.
        var tin = Quadriculado(17, passo: 7.3 * Math.Sqrt(2), deslocamento: 3.91);
        var sorteio = new Random(31);

        for (var n = 0; n < 5_000; n++)
        {
            var x = 3.91 + sorteio.NextDouble() * 180;
            var y = 3.91 + sorteio.NextDouble() * 180;

            var comIndice = tin.TryGetZ(x, y, out var zIndice);
            var linear = tin.TryGetZLinear(x, y, out var zLinear);

            Assert.True(comIndice == linear, $"({x}, {y}): índice {comIndice}, varredura {linear}.");
            if (comIndice) Assert.Equal(zLinear, zIndice, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaEmCoordenadaNegativaRespondeIgual()
    {
        // Coordenada negativa acontece em desenho sem sistema de coordenadas
        // definido, que é comum. O floor de um número negativo arredonda para
        // baixo, e é fácil errar o sinal de uma célula.
        var tin = Quadriculado(12, passo: 10, deslocamento: -300);
        var sorteio = new Random(5);

        for (var n = 0; n < 3_000; n++)
        {
            var x = -320 + sorteio.NextDouble() * 160;
            var y = -320 + sorteio.NextDouble() * 160;

            Assert.True(
                tin.TryGetZ(x, y, out var zIndice) == tin.TryGetZLinear(x, y, out var zLinear),
                $"({x}, {y}) divergiu.");

            if (tin.TryGetZ(x, y, out zIndice)) Assert.Equal(zLinear, zIndice, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void PontoNaBordaDaCelulaNaoSeperde()
    {
        // A fronteira entre células é onde um índice erra: o triângulo está
        // numa célula e o ponto é procurado na vizinha. Estes pontos caem
        // exatamente sobre vértices e arestas da malha.
        var tin = Quadriculado(10);

        for (var i = 0; i <= 10; i++)
        for (var j = 0; j <= 10; j++)
        {
            double x = i * 10, y = j * 10;

            Assert.True(tin.TryGetZ(x, y, out var z), $"O vértice ({x}, {y}) sumiu do índice.");
            Assert.Equal(Z(x, y), z, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void TrianguloMaiorQueACelulaContinuaSendoAchado()
    {
        Point3 P(double x, double y) => new(x, y, Z(x, y));

        // Dois triângulos de 1 km ao lado de mil de meio metro: a célula sai
        // do tamanho médio e os grandes cobrem muitas dela.
        var triangulos = new List<Triangle>
        {
            new(P(0, 0), P(1000, 0), P(1000, 1000)),
            new(P(0, 0), P(1000, 1000), P(0, 1000)),
        };

        for (var i = 0; i < 500; i++)
        {
            double x = 2000 + i * 0.5;
            triangulos.Add(new Triangle(P(x, 0), P(x + 0.5, 0), P(x + 0.5, 0.5)));
            triangulos.Add(new Triangle(P(x, 0), P(x + 0.5, 0.5), P(x, 0.5)));
        }

        var tin = new Tin(triangulos);

        foreach (var (x, y) in new[] { (500.0, 500.0), (10.0, 900.0), (999.0, 1.0), (2100.25, 0.25) })
        {
            Assert.True(tin.TryGetZ(x, y, out var z), $"({x}, {y}) sumiu.");
            Assert.True(tin.TryGetZLinear(x, y, out var esperado));
            Assert.Equal(esperado, z, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void SuperficieDeCurvasDeNivelNaoExplodeOIndice()
    {
        // Superfície feita a partir de curvas de nível não se parece com malha
        // regular: os triângulos são compridos e diagonais, e a caixa
        // envolvente de cada um cobre muitas células.
        //
        // Sem defesa, cada triângulo era duplicado em centenas de células, e o
        // índice pedia dezenas de gigabytes antes de responder a primeira
        // cota. Os compridos agora ficam de fora da grade e são varridos
        // sempre — são poucos, e a conta fecha.
        var sorteio = new Random(11);
        var triangulos = new List<Triangle>();

        for (var n = 0; n < 40_000; n++)
        {
            var x = sorteio.NextDouble() * 300;
            var y = sorteio.NextDouble() * 300;

            // Fatia comprida a 45°, que é o formato que estoura a caixa.
            var comprimento = 25 + sorteio.NextDouble() * 20;
            var espessura = 0.4 + sorteio.NextDouble();

            Point3 P(double px, double py) => new(px, py, Z(px, py));

            triangulos.Add(new Triangle(
                P(x, y),
                P(x + comprimento, y + comprimento),
                P(x + espessura, y)));
        }

        var relogio = Stopwatch.StartNew();
        var tin = new Tin(triangulos);
        var construcao = relogio.Elapsed;

        Assert.True(
            construcao < TimeSpan.FromSeconds(20),
            $"Construção levou {construcao.TotalSeconds:0.0} s numa malha de curvas de nível.");

        // 8 milhões de entradas = 32 MB. Sem a defesa, esta mesma malha pedia
        // dezenas de vezes isso, e crescia com o quadrado do número de
        // triângulos.
        Assert.True(
            tin.OccupancyCount <= 8_000_000,
            $"O índice guardou {tin.OccupancyCount:N0} entradas para {triangulos.Count:N0} triângulos.");

        // E continua respondendo igual à varredura.
        var conferencia = new Random(12);
        for (var n = 0; n < 300; n++)
        {
            var x = conferencia.NextDouble() * 340;
            var y = conferencia.NextDouble() * 340;

            Assert.True(
                tin.TryGetZ(x, y, out var zIndice) == tin.TryGetZLinear(x, y, out var zLinear),
                $"({x}, {y}) divergiu numa malha de curvas de nível.");

            if (tin.TryGetZ(x, y, out zIndice)) Assert.Equal(zLinear, zIndice, 9);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaEmFitaRespeitaOTetoDeCelulas()
    {
        // Faixa muito mais larga que alta: a conta do tamanho de célula,
        // se feita sobre a área, pede uma quantidade absurda de colunas.
        // O teto precisa valer sobre o número real de células, não sobre uma
        // estimativa contínua.
        Point3 P(double x, double y) => new(x, y, Z(x, y));

        var triangulos = new List<Triangle>();
        for (var i = 0; i < 20_000; i++)
        {
            double x = i * 0.5;
            triangulos.Add(new Triangle(P(x, 0), P(x + 0.5, 0), P(x + 0.5, 1e-5)));
            triangulos.Add(new Triangle(P(x, 0), P(x + 0.5, 1e-5), P(x, 1e-5)));
        }

        var tin = new Tin(triangulos);

        Assert.True(
            tin.CellCount <= 4_000_000,
            $"A grade criou {tin.CellCount:N0} células numa malha em fita.");

        Assert.True(tin.TryGetZ(5_000, 5e-6, out var z));
        Assert.True(tin.TryGetZLinear(5_000, 5e-6, out var esperado));
        Assert.Equal(esperado, z, 9);
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaDeUmTrianguloSoResponde()
    {
        var tin = new Tin([new Triangle(
            new Point3(0, 0, 0), new Point3(10, 0, 1), new Point3(0, 10, 2))]);

        Assert.True(tin.TryGetZ(1, 1, out _));
        Assert.False(tin.TryGetZ(9, 9, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaVaziaNaoQuebra()
    {
        var tin = new Tin([]);

        Assert.Equal(0, tin.CellCount);
        Assert.False(tin.TryGetZ(0, 0, out _));
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void IndiceEmCoordenadaUtm()
    {
        // A grade guarda o canto mínimo e trabalha com deslocamentos, então
        // coordenada grande não devia incomodá-la. Este teste é o que avisaria
        // se algum dia passasse a incomodar.
        const double x0 = 512_345.678;
        const double y0 = 7_456_789.012;

        Point3 P(double dx, double dy) => new(x0 + dx, y0 + dy, Z(dx, dy));

        var triangulos = new List<Triangle>();
        for (var i = 0; i < 40; i++)
        for (var j = 0; j < 40; j++)
        {
            double x = i * 0.25, y = j * 0.25;
            triangulos.Add(new Triangle(P(x, y), P(x + 0.25, y), P(x + 0.25, y + 0.25)));
            triangulos.Add(new Triangle(P(x, y), P(x + 0.25, y + 0.25), P(x, y + 0.25)));
        }

        var tin = new Tin(triangulos);
        Assert.Equal(3200, tin.TriangleCount);

        var sorteio = new Random(1);
        for (var n = 0; n < 500; n++)
        {
            var x = x0 + sorteio.NextDouble() * 10;
            var y = y0 + sorteio.NextDouble() * 10;

            Assert.True(tin.TryGetZ(x, y, out var z), $"({x}, {y}) sumiu.");
            Assert.True(tin.TryGetZLinear(x, y, out var esperado));

            // 1 mm, que é a tolerância dos verificadores de regra sagrada.
            Assert.Equal(esperado, z, 3);
        }
    }

    [Fact]
    [Trait("Etapa", "1")]
    public void MalhaGrandeConstroiERespondeDentroDoPrazo()
    {
        // Os números do plano: malha na ordem de milhões de triângulos,
        // construção em segundos, cem mil consultas em menos de um segundo.
        //
        // São 800 mil triângulos, não 2 milhões: o alvo real é exercitado, e
        // o pico de memória fica em ~200 MB, que cabe num agente de CI
        // apertado. Os limites são folgados de propósito — o teste existe para
        // acusar regressão de ordem de grandeza, não para medir a máquina.
        const int lado = 632;

        var relogio = Stopwatch.StartNew();
        var tin = Quadriculado(lado, passo: 0.25);
        var construcao = relogio.Elapsed;

        Assert.Equal(lado * lado * 2, tin.TriangleCount);

        var sorteio = new Random(7);
        var pontos = new (double X, double Y)[100_000];
        var limite = lado * 0.25;

        for (var i = 0; i < pontos.Length; i++)
            pontos[i] = (sorteio.NextDouble() * limite, sorteio.NextDouble() * limite);

        relogio.Restart();
        var achados = 0;
        foreach (var (x, y) in pontos)
            if (tin.TryGetZ(x, y, out _)) achados++;
        var consultas = relogio.Elapsed;

        Assert.True(achados > 99_000, $"Só {achados} de {pontos.Length} consultas acharam terreno.");

        Assert.True(
            construcao < TimeSpan.FromSeconds(30),
            $"Construção levou {construcao.TotalSeconds:0.0} s (limite 30 s).");

        Assert.True(
            consultas < TimeSpan.FromSeconds(5),
            $"100 mil consultas levaram {consultas.TotalSeconds:0.00} s (limite 5 s).");
    }
}
