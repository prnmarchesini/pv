using UFV.Geo;

namespace UFV.Geo.Tests;

/// <summary>
/// Drapear é o que faz a linha assentar no terreno. O erro que importa não é
/// de centímetros: é a corda reta entre dois vértices distantes passando POR
/// DENTRO do morro. Em planta ela parece certa; só ao orbitar em 3D é que
/// aparece a linha enterrada — e nessa altura ela já virou área de projeto.
/// </summary>
public class DrapingTests
{
    /// <summary>
    /// Terreno com um morro no meio: sobe até a linha x = 50 e desce depois.
    /// A cota vai de 100 na borda a 110 no alto.
    /// </summary>
    private static Tin Morro()
    {
        double Z(double x) => 100 + (x <= 50 ? x : 100 - x) * 0.2;

        Point3 P(double x, double y) => new(x, y, Z(x));

        var triangulos = new List<Triangle>();

        // Duas faixas de triângulos, de 0 a 50 e de 50 a 100, para a quina do
        // morro cair exatamente sobre arestas.
        foreach (var (x0, x1) in new[] { (0.0, 50.0), (50.0, 100.0) })
        {
            triangulos.Add(new Triangle(P(x0, 0), P(x1, 0), P(x1, 100)));
            triangulos.Add(new Triangle(P(x0, 0), P(x1, 100), P(x0, 100)));
        }

        return new Tin(triangulos);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void ALinhaSobreOMorroNaoAtravessaOMorro()
    {
        // Uma linha de ponta a ponta, com dois vértices só. Sem vértice no
        // alto, a corda reta entre eles passaria dez metros por baixo do topo.
        var terreno = Morro();

        var drapejada = Draping.Along(terreno, [new Point3(0, 50, 0), new Point3(100, 50, 0)]);

        Assert.False(drapejada.HasGaps);

        // O vértice do alto tem que estar lá, com a cota do alto.
        var alto = drapejada.Vertices.FirstOrDefault(p => Math.Abs(p.X - 50) < 0.001);

        Assert.True(alto != default, "A linha não ganhou vértice no alto do morro.");
        Assert.Equal(110, alto.Z, 3);

        // E nenhum ponto da linha pode ficar abaixo do terreno.
        foreach (var ponto in drapejada.Vertices)
        {
            Assert.True(terreno.TryGetZ(ponto.X, ponto.Y, out var cotaDoTerreno));
            Assert.True(
                Math.Abs(ponto.Z - cotaDoTerreno) <= 0.001,
                $"O ponto ({ponto.X:0.##}, {ponto.Y:0.##}) está a {ponto.Z:0.###} "
                + $"e o terreno ali está a {cotaDoTerreno:0.###}.");
        }
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void OMeioDaCordaSeriaEnterradoSemODrapeamento()
    {
        // Este é o teste que mede o tamanho do problema, e não só a presença
        // da solução: sem o vértice do alto, o meio da corda ficaria 10 m
        // abaixo do terreno.
        var terreno = Morro();

        var inicio = new Point3(0, 50, 100);
        var fim = new Point3(100, 50, 100);

        Assert.True(terreno.TryGetZ(50, 50, out var noAlto));

        var meioDaCorda = (inicio.Z + fim.Z) / 2;

        Assert.True(
            noAlto - meioDaCorda > 9.9,
            "O terreno de teste precisa ter morro suficiente para o erro aparecer.");
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void LinhaSobrePlanoNaoGanhaVerticeDesnecessario()
    {
        // Num plano, a cota entre dois pontos é exatamente a interpolação
        // linear: todo vértice a mais é peso morto no desenho. Numa área de
        // usina seriam dezenas de milhares deles.
        Point3 P(double x, double y) => new(x, y, 100 + 0.05 * x + 0.03 * y);

        var triangulos = new List<Triangle>();
        for (var i = 0; i < 20; i++)
        for (var j = 0; j < 20; j++)
        {
            double x = i * 5, y = j * 5;
            triangulos.Add(new Triangle(P(x, y), P(x + 5, y), P(x + 5, y + 5)));
            triangulos.Add(new Triangle(P(x, y), P(x + 5, y + 5), P(x, y + 5)));
        }

        var drapejada = Draping.Along(
            new Tin(triangulos),
            [new Point3(1, 1, 0), new Point3(99, 99, 0)]);

        Assert.False(drapejada.HasGaps);
        Assert.Equal(2, drapejada.Vertices.Count);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void VerticeForaDoTerrenoEReportado()
    {
        // A linha continua com o traçado que o usuário desenhou — apagar o
        // trecho mudaria o desenho dele —, mas ele precisa saber que ali não
        // há terreno embaixo.
        var terreno = Morro();

        var drapejada = Draping.Along(
            terreno,
            [new Point3(50, 50, 0), new Point3(500, 50, 0)]);

        Assert.True(drapejada.HasGaps);
        Assert.Equal(1, drapejada.OutsideCount);

        // O ponto de fora mantém a posição que tinha.
        var ultimo = drapejada.Vertices[^1];
        Assert.Equal(500, ultimo.X, 3);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void PontoForaDoTerrenoNaoViraCotaZero()
    {
        // Zerar a cota de um ponto sem terreno embaixo seria o pior desfecho:
        // zero é um número plausível, e um ponto no nível do mar no meio de um
        // terreno a 100 m só apareceria como altura de pilar absurda, lá na
        // frente. A cota que veio é mantida, e a posição é anotada.
        const double cotaQueVeio = 123.45;

        var drapejada = Draping.Along(
            Morro(),
            [new Point3(50, 50, cotaQueVeio), new Point3(500, 50, cotaQueVeio)]);

        Assert.Single(drapejada.OutsideIndices);

        var semTerreno = drapejada.Vertices[drapejada.OutsideIndices[0]];

        Assert.Equal(500, semTerreno.X, 3);
        Assert.Equal(cotaQueVeio, semTerreno.Z, 3);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void OsPontosSemTerrenoSaoApontadosPelaPosicao()
    {
        // Saber quantos não basta: para avisar, pintar ou recusar, quem chama
        // precisa saber QUAIS.
        var drapejada = Draping.Along(
            Morro(),
            [new Point3(-200, 50, 7), new Point3(50, 50, 7), new Point3(400, 50, 7)]);

        Assert.Equal(2, drapejada.OutsideCount);

        foreach (var i in drapejada.OutsideIndices)
        {
            var ponto = drapejada.Vertices[i];
            Assert.False(
                Morro().TryGetZ(ponto.X, ponto.Y, out _),
                $"O ponto ({ponto.X}, {ponto.Y}) foi apontado como fora, mas tem terreno.");
        }
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void VerticeDoUsuarioNaoEEnxugadoPorSerColinear()
    {
        // O vértice do meio cai exatamente na reta entre os outros dois, sobre
        // terreno plano: pelo critério de enxugar, ele é redundante. Mas o
        // usuário o colocou ali, e apagá-lo muda o polígono que ele delimitou
        // — numa área de projeto, muda o que vai ser construído.
        Point3 P(double x, double y) => new(x, y, 100);

        var triangulos = new List<Triangle>
        {
            new(P(0, 0), P(200, 0), P(200, 200)),
            new(P(0, 0), P(200, 200), P(0, 200)),
        };

        var drapejada = Draping.Along(
            new Tin(triangulos),
            [new Point3(20, 100, 0), new Point3(100, 100, 0), new Point3(180, 100, 0)]);

        Assert.Equal(3, drapejada.Vertices.Count);
        Assert.Equal(100, drapejada.Vertices[1].X, 3);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void PontoSemTerrenoNaoEEnxugadoPorSerColinear()
    {
        // Três pontos fora do terreno, em linha e com a mesma cota: o do meio
        // parece redundante por todos os critérios. Ele fica, porque é um dos
        // pontos que o usuário precisa ver marcados.
        var drapejada = Draping.Along(
            Morro(),
            [
                new Point3(-500, 50, 7),
                new Point3(-400, 50, 7),
                new Point3(-300, 50, 7),
            ]);

        Assert.Equal(3, drapejada.Vertices.Count);
        Assert.Equal(3, drapejada.OutsideCount);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void LinhaInteiraForaDoTerrenoContaTodosOsPontos()
    {
        var drapejada = Draping.Along(
            Morro(),
            [new Point3(-500, -500, 0), new Point3(-400, -400, 0)]);

        Assert.Equal(2, drapejada.OutsideCount);
        Assert.Equal(2, drapejada.Vertices.Count);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void LinhaComUmPontoSoDevolveUmPonto()
    {
        var drapejada = Draping.Along(Morro(), [new Point3(25, 50, 0)]);

        Assert.Single(drapejada.Vertices);
        Assert.Equal(105, drapejada.Vertices[0].Z, 3);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void LinhaVaziaNaoQuebra()
    {
        var drapejada = Draping.Along(Morro(), []);

        Assert.Empty(drapejada.Vertices);
        Assert.False(drapejada.HasGaps);
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void OsVerticesDoUsuarioContinuamNaLinha()
    {
        // Drapear acrescenta pontos; não pode mover nem perder os que o
        // usuário desenhou, senão a área deixa de ser a que ele traçou.
        var terreno = Morro();

        var desenhados = new[]
        {
            new Point3(10, 20, 0),
            new Point3(40, 20, 0),
            new Point3(90, 80, 0),
        };

        var drapejada = Draping.Along(terreno, desenhados);

        foreach (var original in desenhados)
        {
            Assert.Contains(
                drapejada.Vertices,
                p => Math.Abs(p.X - original.X) < 0.001 && Math.Abs(p.Y - original.Y) < 0.001);
        }
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void AOrdemDosPontosENaDirecaoDoTracado()
    {
        // Os vértices acrescentados entram entre os do usuário, na ordem em
        // que a linha os encontra. Fora de ordem, a polilinha viraria um
        // zigue-zague.
        var drapejada = Draping.Along(Morro(), [new Point3(5, 50, 0), new Point3(95, 50, 0)]);

        for (var i = 1; i < drapejada.Vertices.Count; i++)
        {
            Assert.True(
                drapejada.Vertices[i].X > drapejada.Vertices[i - 1].X,
                "Os pontos saíram fora da ordem do traçado.");
        }
    }

    [Fact]
    [Trait("Etapa", "2")]
    public void LinhaFechadaAssentaNoTerrenoInteiro()
    {
        // O caso real: a área é um polígono fechado. O último ponto repete o
        // primeiro, e o trecho de volta também precisa assentar.
        var terreno = Morro();

        var drapejada = Draping.Along(
            terreno,
            [
                new Point3(10, 10, 0),
                new Point3(90, 10, 0),
                new Point3(90, 90, 0),
                new Point3(10, 90, 0),
                new Point3(10, 10, 0),
            ]);

        Assert.False(drapejada.HasGaps);

        foreach (var ponto in drapejada.Vertices)
        {
            Assert.True(terreno.TryGetZ(ponto.X, ponto.Y, out var cota));
            Assert.Equal(cota, ponto.Z, 3);
        }

        // Fecha onde começou.
        Assert.Equal(drapejada.Vertices[0].X, drapejada.Vertices[^1].X, 3);
        Assert.Equal(drapejada.Vertices[0].Y, drapejada.Vertices[^1].Y, 3);
    }

    /// <summary>
    /// Achado da revisão da etapa 2: a posição do vértice fora do terreno era
    /// anotada ANTES de saber se ele seria aceito. Um ponto recusado por
    /// repetir o anterior deixava a anotação apontando para a vaga dele, que o
    /// ponto seguinte ocupava — e o aviso acusava um vértice que estava sobre o
    /// terreno, enquanto o que estava fora passava despercebido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "2")]
    public void OAvisoApontaOVerticeQueEstaMesmoForaDoTerreno()
    {
        var terreno = Morro();

        // Dois cliques no mesmo lugar fora do terreno, e depois um dentro.
        // O segundo é recusado por repetir o primeiro.
        var drapejada = Draping.Along(terreno, [
            new Point3(-200, 50, 7),
            new Point3(-200, 50, 7),
            new Point3(50, 50, 0),
        ]);

        Assert.Equal(1, drapejada.OutsideCount);

        foreach (var posicao in drapejada.OutsideIndices)
        {
            var ponto = drapejada.Vertices[posicao];

            Assert.False(
                terreno.TryGetZ(ponto.X, ponto.Y, out _),
                $"O aviso apontou ({ponto.X:0.##}, {ponto.Y:0.##}), que está sobre o terreno.");
        }

        // E o que está de fato fora não pode ficar de fora do aviso.
        for (var i = 0; i < drapejada.Vertices.Count; i++)
        {
            var ponto = drapejada.Vertices[i];
            if (terreno.TryGetZ(ponto.X, ponto.Y, out _)) continue;

            Assert.Contains(i, drapejada.OutsideIndices);
        }
    }

    /// <summary>
    /// O mesmo morro, mas com malha fina: 40 por 40 células.
    ///
    /// O terreno de quatro triângulos não serve para testar o índice espacial:
    /// ele cabe inteiro numa célula, e qualquer caminhada pela grade acha tudo.
    /// Só com muitos triângulos a grade tem células suficientes para uma
    /// caminhada errada deixar triângulo para trás.
    /// </summary>
    private static Tin MorroFino()
    {
        const int Divisoes = 40;
        const double Lado = 100.0 / Divisoes;

        double Z(double x) => 100 + (x <= 50 ? x : 100 - x) * 0.2;

        Point3 P(double x, double y) => new(x, y, Z(x));

        var triangulos = new List<Triangle>();

        for (var i = 0; i < Divisoes; i++)
        {
            for (var j = 0; j < Divisoes; j++)
            {
                var x0 = i * Lado;
                var x1 = x0 + Lado;
                var y0 = j * Lado;
                var y1 = y0 + Lado;

                triangulos.Add(new Triangle(P(x0, y0), P(x1, y0), P(x1, y1)));
                triangulos.Add(new Triangle(P(x0, y0), P(x1, y1), P(x0, y1)));
            }
        }

        return new Tin(triangulos);
    }

    /// <summary>
    /// Achado da revisão da etapa 2: o índice prendia X e Y do segmento à caixa
    /// da malha separadamente, o que move as pontas em vez de cortar o
    /// segmento. Uma diagonal que entra por uma borda e sai por outra virava
    /// outra diagonal, e as células percorridas não eram as que ela cruza.
    ///
    /// O sintoma é o da etapa inteira: faltam vértices no trecho que está sobre
    /// o terreno, e a corda entre os que sobraram passa por dentro do morro.
    ///
    /// A diagonal é de propósito assimétrica. Uma a 45 graus pelo meio do
    /// quadrado não serve: nela prender cada eixo dá, por acaso, o mesmo
    /// resultado do recorte certo, e o defeito passa despercebido.
    /// </summary>
    [Fact]
    [Trait("Etapa", "2")]
    public void ADiagonalQueEntraPorUmaBordaESaiPorOutraNaoAtravessaOMorro()
    {
        var terreno = MorroFino();

        var drapejada = Draping.Along(terreno, [
            new Point3(-100, 30, 0),
            new Point3(150, 90, 0),
        ]);

        // Entre dois vértices consecutivos que estão sobre o terreno, a corda
        // reta tem que coincidir com o terreno: é isso que drapear significa.
        // Um triângulo que o índice não devolveu vira um cruzamento que não
        // entrou, e a corda passa por baixo.
        var afundamentos = 0;
        var pior = 0.0;

        for (var i = 1; i < drapejada.Vertices.Count; i++)
        {
            var a = drapejada.Vertices[i - 1];
            var b = drapejada.Vertices[i];

            var mx = (a.X + b.X) / 2;
            var my = (a.Y + b.Y) / 2;

            if (!terreno.TryGetZ(mx, my, out var cotaDoTerreno)) continue;

            var cotaDaCorda = (a.Z + b.Z) / 2;
            var erro = cotaDoTerreno - cotaDaCorda;

            if (erro <= 0.001) continue;

            afundamentos++;
            pior = Math.Max(pior, erro);
        }

        Assert.True(
            afundamentos == 0,
            $"{afundamentos} trecho(s) passam por dentro do morro; o pior afunda {pior:0.###} m.");

        // E a quina do morro, em x = 50, tem que estar entre os vértices.
        Assert.Contains(drapejada.Vertices, p => Math.Abs(p.X - 50) < 0.001);
    }
}
