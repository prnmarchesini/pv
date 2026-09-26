using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A distribuição em planta: a linha de alinhamento é o eixo transversal;
/// toda fileira nasce nela, uma a cada pitch, e corre a 90° para o lado
/// escolhido, com as mesas enfileiradas da linha para fora, dentro da área.
///
/// O que se trava aqui é contagem e posição, em casos em que a resposta se
/// calcula à mão: retângulo dá a contagem exata; a mesa que sobra na borda
/// fica e é marcada; nenhuma mesa pisa em outra. O terreno não entra: isto é
/// planta, e a cota é da amostragem (5.2).
///
/// Reescrito em 26/09/2026, quando o Renan reprovou a primeira versão (que
/// punha as fileiras paralelas à linha): "o alinhamento é uma linha
/// perpendicular às fileiras, toda fileira nasce nele e vai a 90 graus".
/// </summary>
public class RowDistributorTests
{
    private static Point3 P(double x, double y) => new(x, y, 0);

    /// <summary>Retângulo de 100 m ao longo da linha por 50 m no sentido das fileiras.</summary>
    private static readonly Point3[] Retangulo = [P(0, 0), P(100, 0), P(100, 50), P(0, 50)];

    /// <summary>A linha de alinhamento na borda de baixo, da esquerda para a direita: as fileiras sobem para o norte.</summary>
    private static readonly Point3[] Alinhamento = [P(0, 0), P(100, 0)];

    /// <summary>Mesa de 20 m de comprimento (ao longo da fileira) ocupando 4 m em planta (ao longo da linha).</summary>
    private static readonly TableFootprint Mesa = new(Length: 20, PlanDepth: 4);

    private const double Norte = Math.PI / 2;

    private static PlanLayout Distribuir(
        IReadOnlyList<Point3>? area = null,
        IReadOnlyList<Point3>? alinhamento = null,
        LineSide lado = LineSide.Left,
        double pitch = 6,
        double gap = 0,
        TableFootprint? mesa = null) =>
        RowDistributor.Distribute(area ?? Retangulo, alinhamento ?? Alinhamento, lado, pitch, gap, mesa ?? Mesa);

    // ----------------------------------------------------- o retângulo

    /// <summary>
    /// Linha de (0,0) a (100,0), fileiras à esquerda (para o norte), pitch 6
    /// e mesa de 4 m de fundo: as fileiras nascem em x = 0, 6, 12, …, 96 (17
    /// estações dentro dos 100 m) e ocupam [x, x+4]. Cada uma corre de y = 0
    /// a 50: mesas em [0,20] e [20,40] inteiras, e a de [40,60] passa da
    /// borda: fica, marcada. 17 × 3 = 51 mesas, 17 marcadas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ORetanguloDaAContagemExata()
    {
        var layout = Distribuir();

        Assert.Equal(17, layout.Rows.Count);
        Assert.All(layout.Rows, fileira => Assert.Equal(3, fileira.Tables.Count));
        Assert.Equal(51, layout.Tables.Count);

        Assert.Equal(17, layout.PartlyOutsideCount);
        Assert.All(layout.Rows, f => Assert.False(f.Tables[0].PartlyOutside));
        Assert.All(layout.Rows, f => Assert.False(f.Tables[1].PartlyOutside));
        Assert.All(layout.Rows, f => Assert.True(f.Tables[2].PartlyOutside));
        Assert.Equal(0, layout.SkippedForOverlap);
    }

    /// <summary>
    /// A fileira 1 nasce no início da linha e corre a 90° dela: a primeira
    /// mesa parte da origem para o norte, com o fundo ao longo da linha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void APrimeiraFileiraNasceNoInicioDaLinhaECorreANoventaGraus()
    {
        var primeira = Distribuir().Rows[0].Tables[0];

        Assert.Equal(1, primeira.Row);
        Assert.Equal(1, primeira.Number);
        Assert.Equal(0, primeira.Origin.X, 9);
        Assert.Equal(0, primeira.Origin.Y, 9);
        Assert.Equal(Norte, primeira.DirectionRadians, 9);

        Assert.Equal(4, primeira.Corners.Count);
        Assert.Contains(primeira.Corners, c => Perto(c, 0, 0));
        Assert.Contains(primeira.Corners, c => Perto(c, 0, 20));
        Assert.Contains(primeira.Corners, c => Perto(c, 4, 20));
        Assert.Contains(primeira.Corners, c => Perto(c, 4, 0));
    }

    /// <summary>A segunda fileira nasce um pitch adiante, medido ao longo da linha.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPitchEMedidoAoLongoDaLinha()
    {
        var segunda = Distribuir().Rows[1].Tables[0];

        Assert.Equal(6, segunda.Origin.X, 9);
        Assert.Equal(0, segunda.Origin.Y, 9);
        Assert.Contains(segunda.Corners, c => Perto(c, 10, 20));
    }

    /// <summary>
    /// Só nascem fileiras ao longo do comprimento traçado: uma linha de 30 m
    /// dá as estações 0, 6, 12, 18, 24 e 30, seis fileiras, mesmo com a área
    /// seguindo por mais 70 m.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaCurtaDaMenosFileiras()
    {
        var curta = new[] { P(0, 0), P(30, 0) };
        var layout = Distribuir(alinhamento: curta);

        Assert.Equal(6, layout.Rows.Count);
        Assert.Equal(30, layout.Rows[5].Tables[0].Origin.X, 9);
    }

    /// <summary>
    /// Com espaçamento de 1 m: mesas em y = 0, 21 e 42. A terceira termina
    /// em 62, fora do retângulo: fica e é marcada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEspacamentoEntraEntreAsMesasEAUltimaQueSobraFicaMarcada()
    {
        var fileira = Distribuir(gap: 1).Rows[0];

        Assert.Equal(3, fileira.Tables.Count);
        Assert.Equal([0, 21, 42], fileira.Tables.Select(m => Math.Round(m.Origin.Y, 6)));
        Assert.False(fileira.Tables[1].PartlyOutside);
        Assert.True(fileira.Tables[2].PartlyOutside);
    }

    /// <summary>Numeração do plano de requisitos: F1.1, F1.2 ..., F2.1 ...</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AsMesasSaoNumeradasPorFileira()
    {
        var layout = Distribuir();

        Assert.Equal("F1.1", layout.Rows[0].Tables[0].Label);
        Assert.Equal("F1.3", layout.Rows[0].Tables[2].Label);
        Assert.Equal("F2.1", layout.Rows[1].Tables[0].Label);
        Assert.Equal("F17.2", layout.Rows[16].Tables[1].Label);
    }

    // -------------------------------------------------------------- lado

    /// <summary>
    /// À direita da linha (y decrescente) não há área: nenhuma fileira. É o
    /// que acontece quando o usuário clica o lado errado, e o resultado tem
    /// que dizer isso em vez de inventar mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OLadoErradoNaoTemFileira()
    {
        var layout = Distribuir(lado: LineSide.Right);

        Assert.Empty(layout.Rows);
        Assert.Empty(layout.Tables);
    }

    /// <summary>
    /// Linha traçada ao contrário: o lado direito é que dá as mesmas
    /// fileiras, e a fileira 1 passa a nascer em x = 100.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void InverterALinhaInverteOLadoEAOrdemDasFileiras()
    {
        var aoContrario = Alinhamento.Reverse().ToArray();
        var layout = Distribuir(alinhamento: aoContrario, lado: LineSide.Right);

        Assert.Equal(51, layout.Tables.Count);
        Assert.Equal(100, layout.Rows[0].Tables[0].Origin.X, 9);
        Assert.Equal(Norte, layout.Rows[0].Tables[0].DirectionRadians, 9);
        Assert.Empty(Distribuir(alinhamento: aoContrario, lado: LineSide.Left).Rows);
    }

    /// <summary>
    /// A linha no meio da área: as fileiras nascem nela e só vão para o
    /// lado escolhido; a metade de trás fica vazia. As mesas começam na
    /// linha, não na borda da área. O motor não adivinha que o usuário
    /// queria os dois lados.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaNoMeioSoEnchOLadoEscolhidoEAsMesasComecamNela()
    {
        var meio = new[] { P(0, 25), P(100, 25) };
        var layout = Distribuir(alinhamento: meio);

        Assert.Equal(17, layout.Rows.Count);
        Assert.All(layout.Rows, f => Assert.Equal(25, f.Tables[0].Origin.Y, 9));
        Assert.All(layout.Tables, m => Assert.True(m.Origin.Y >= 25 - 1e-9));
        Assert.All(layout.Rows, f => Assert.Equal(2, f.Tables.Count));
    }

    /// <summary>
    /// A linha fora e longe da área, do lado de trás: as fileiras nascem
    /// nela e as mesas começam onde a área começa, não atrás.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaAfastadaDaAreaAindaAlinha()
    {
        var abaixo = new[] { P(0, -12), P(100, -12) };
        var layout = Distribuir(alinhamento: abaixo);

        Assert.Equal(17, layout.Rows.Count);
        Assert.Equal(1, layout.Rows[0].Number);
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Tables[0].Origin.Y, 9));
        Assert.Equal(51, layout.Tables.Count);
    }

    // ----------------------------------------------------- área irregular

    /// <summary>
    /// Triângulo retângulo: 100 m na base (a linha) e 50 m de altura. A
    /// hipotenusa é y = 50 − x/2: a fileira em [x, x+4] alcança menos a
    /// cada estação, e a contagem de mesas por fileira não cresce.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OTrianguloTemMenosMesasACadaFileira()
    {
        var triangulo = new[] { P(0, 0), P(100, 0), P(0, 50) };
        var layout = Distribuir(area: triangulo);

        var porFileira = layout.Rows.Select(f => f.Tables.Count).ToList();

        Assert.Equal(porFileira.OrderByDescending(n => n), porFileira);

        // Fileira 1 em [0,4]: no fundo da mesa (x = 4) a hipotenusa está em
        // y = 48. Mesas em 0 e 20 inteiras (20+20 = 40 < 48) e a de 40 a 60
        // sai pela hipotenusa: fica, marcada.
        Assert.Equal(3, porFileira[0]);
        Assert.True(layout.Rows[0].Tables[2].PartlyOutside);
        Assert.False(layout.Rows[0].Tables[1].PartlyOutside);

        Assert.Equal(0, layout.SkippedForOverlap);
        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// O "L" é côncavo: as fileiras da esquerda vão até y = 50, as da direita
    /// só até y = 25. Nenhuma mesa inteira nasce no quadrante que falta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEleNaoPoeMesaNoQuadranteQueFalta()
    {
        var ele = new[] { P(0, 0), P(100, 0), P(100, 25), P(50, 25), P(50, 50), P(0, 50) };
        var layout = Distribuir(area: ele);

        foreach (var mesa in layout.Tables.Where(m => !m.PartlyOutside))
        {
            Assert.All(mesa.Corners, c => Assert.True(Polygons.Contains(ele, c.X, c.Y)));
        }

        // Fileiras de x ≥ 54 só têm a perna de baixo: 25/20 = 1 inteira e
        // uma marcada.
        var direita = layout.Rows.First(f => f.Tables[0].Origin.X >= 54);
        Assert.Equal(2, direita.Tables.Count);
        Assert.True(direita.Tables[1].PartlyOutside);

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// Área com um recorte entrando pela esquerda entre y = 20 e y = 30: a
    /// fileira 1 cruza o vão e nasce em dois trechos, e a numeração continua
    /// de um para o outro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AFileiraQueCruzaUmVaoNasceEmDoisTrechos()
    {
        var recortada = new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50), P(0, 30), P(10, 30), P(10, 20), P(0, 20) };
        var layout = Distribuir(area: recortada);

        var primeira = layout.Rows[0];

        // Trecho [0,20]: mesa em 0. Trecho [30,50]: mesa em 30.
        Assert.Equal([0, 30], primeira.Tables.Select(m => Math.Round(m.Origin.Y, 6)));
        Assert.Equal([1, 2], primeira.Tables.Select(m => m.Number));
        Assert.All(primeira.Tables, m => Assert.False(m.PartlyOutside));

        // A fileira 3 (x de 12 a 16) não é atingida pelo recorte.
        Assert.Equal(3, layout.Rows[2].Tables.Count);

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// O caso que a revisão do 5.1 pegou: a área tem um dente triangular
    /// entrando 3 m pela borda de baixo, inteiro dentro de F1.1. Os quatro
    /// cantos da mesa estão dentro e a mesa está parcialmente fora.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RecorteDaAreaDentroDaMesaMarcaAMesa()
    {
        var comDente = new[] { P(0, 0), P(1, 0), P(2, 3), P(3, 0), P(100, 0), P(100, 50), P(0, 50) };
        var layout = Distribuir(area: comDente);

        var primeira = layout.Rows[0].Tables[0];

        Assert.All(primeira.Corners, c => Assert.True(Polygons.Contains(comDente, c.X, c.Y)));
        Assert.True(primeira.PartlyOutside);

        // A vizinha, sem dente, continua inteira.
        Assert.False(layout.Rows[0].Tables[1].PartlyOutside);
    }

    /// <summary>
    /// O recorte que entra e sai da mesa sem deixar vértice dentro: uma
    /// fenda de x = 1 a 3 subindo até y = 45, que atravessa as mesas da
    /// fileira 1 de lado a lado. Só a aresta cruzando pega.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RecorteQueAtravessaAMesaSemVerticeDentroTambemMarca()
    {
        var comFenda = new[] { P(0, 0), P(1, 0), P(1, 45), P(3, 45), P(3, 0), P(100, 0), P(100, 50), P(0, 50) };
        var layout = Distribuir(area: comFenda);

        Assert.All(layout.Rows[0].Tables, m => Assert.True(m.PartlyOutside));
        Assert.All(layout.Rows[1].Tables.Take(2), m => Assert.False(m.PartlyOutside));
    }

    /// <summary>
    /// Área fininha inteira dentro da faixa da fileira 1, sem tocar nenhuma
    /// das três linhas de corte: mesmo assim a fileira nasce, marcada. É o
    /// que as arestas recortadas pela faixa garantem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AreaFininhaDentroDaFaixaAindaGanhaFileira()
    {
        var fininha = new[] { P(1, 0), P(1.8, 0), P(1.8, 50), P(1, 50) };
        var layout = Distribuir(area: fininha);

        Assert.Single(layout.Rows);
        Assert.Equal(3, layout.Rows[0].Tables.Count);
        Assert.All(layout.Rows[0].Tables, m => Assert.True(m.PartlyOutside));
    }

    /// <summary>
    /// Área até x = 97: a faixa da fileira 17 é [96,100], e a linha central
    /// em 98 está fora. Só a borda de cá pega a fileira; cortar pela linha
    /// central perderia justamente a fileira que precisa ficar marcada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AUltimaFileiraComALinhaCentralForaAindaNasce()
    {
        var estreita = new[] { P(0, 0), P(97, 0), P(97, 50), P(0, 50) };
        var layout = Distribuir(area: estreita);

        Assert.Equal(17, layout.Rows.Count);
        Assert.All(layout.Rows[16].Tables, m => Assert.True(m.PartlyOutside));
        Assert.Equal(96, layout.Rows[16].Tables[0].Origin.X, 9);
    }

    /// <summary>
    /// Área de 1,5 m ao longo da linha, com a linha começando na borda: a
    /// borda de cá da faixa coincide com a borda da área (tangente) e a
    /// linha central passa fora. O recuo de 1 mm é o que faz a fileira
    /// existir.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaNaBordaDeUmaAreaRasaAindaDaFileira()
    {
        var rasa = new[] { P(0, 0), P(1.5, 0), P(1.5, 50), P(0, 50) };
        var layout = Distribuir(area: rasa);

        Assert.Single(layout.Rows);
        Assert.Equal(3, layout.Rows[0].Tables.Count);
        Assert.All(layout.Rows[0].Tables, m => Assert.True(m.PartlyOutside));
    }

    /// <summary>
    /// Linha traçada 2 mm atrás da borda da área (clique à mão, sem OSNAP):
    /// as mesas começam onde a área começa, na borda, e a primeira de cada
    /// fileira é inteira. A linha manda nas estações; a área manda em onde a
    /// mesa começa. (Na versão de fileiras paralelas à linha esses 2 mm
    /// marcavam a fileira inteira.)
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaDoisMilimetrosAtrasDaBordaNaoMudaNada()
    {
        var quaseNaBorda = new[] { P(0, -0.002), P(100, -0.002) };
        var layout = Distribuir(alinhamento: quaseNaBorda);

        Assert.Equal(17, layout.Rows.Count);
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Tables[0].Origin.Y, 6));
        Assert.All(layout.Rows, f => Assert.False(f.Tables[0].PartlyOutside));
        Assert.All(layout.Rows, f => Assert.False(f.Tables[1].PartlyOutside));
    }

    /// <summary>
    /// Coordenadas UTM e tudo girado 30°: o caso real. Mesma contagem do
    /// retângulo na origem, sem falso "parcial" por arredondamento, e a
    /// fileira a 90° da linha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void EmUtmGiradoAContagemEAMesma()
    {
        var angulo = 30 * Math.PI / 180;

        Point3 Girar(double x, double y) => new(
            700000 + x * Math.Cos(angulo) - y * Math.Sin(angulo),
            7500000 + x * Math.Sin(angulo) + y * Math.Cos(angulo),
            0);

        var area = new[] { Girar(0, 0), Girar(100, 0), Girar(100, 50), Girar(0, 50) };
        var alinhamento = new[] { Girar(0, 0), Girar(100, 0) };

        var layout = Distribuir(area: area, alinhamento: alinhamento);

        Assert.Equal(17, layout.Rows.Count);
        Assert.Equal(51, layout.Tables.Count);
        Assert.Equal(17, layout.PartlyOutsideCount);
        Assert.Equal(angulo + Norte, layout.Tables[0].DirectionRadians, 9);
    }

    // ------------------------------------------------------- sobreposição

    /// <summary>
    /// Overlap direto: idênticas sobrepõem; encostadas não; um milímetro de
    /// invasão... dois milímetros sobrepõem (a folga de encosto é um);
    /// giradas 45° separadas só pelo eixo diagonal não sobrepõem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OverlapDistingueEncostarDeInvadir()
    {
        var a = Mesa2(0, 0, 0);

        Assert.True(RowDistributor.Overlap(a, Mesa2(0, 0, 0)));
        Assert.False(RowDistributor.Overlap(a, Mesa2(20, 0, 0)));
        Assert.False(RowDistributor.Overlap(a, Mesa2(0, 4, 0)));
        Assert.True(RowDistributor.Overlap(a, Mesa2(19.998, 0, 0)));
        Assert.True(RowDistributor.Overlap(a, Mesa2(0, 3.998, 0)));
        Assert.True(RowDistributor.Overlap(a, Mesa2(10, 2, 0)));

        // Girada 45° com o canto encostando na aresta de cima: só o eixo
        // diagonal separa; tirar um eixo do teste passaria a dizer que
        // sobrepõem.
        var girada = Mesa2(10, 4, Math.PI / 4);
        Assert.False(RowDistributor.Overlap(a, girada));

        // E puxada 2 mm para dentro, invade.
        Assert.True(RowDistributor.Overlap(a, Mesa2(10, 3.998, Math.PI / 4)));
    }

    /// <summary>
    /// O verificador independente concorda com Overlap em pares aleatórios:
    /// amostra pontos de uma mesa e pergunta se algum cai bem dentro da
    /// outra. Overlap frouxo (folga de meio metro) seria pego aqui.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OverlapConcordaComAmostragemDePontos()
    {
        var sorteio = new Random(51);
        var discordancias = 0;

        for (var caso = 0; caso < 400; caso++)
        {
            var a = Mesa2(sorteio.NextDouble() * 30, sorteio.NextDouble() * 30, sorteio.NextDouble() * Math.PI);
            var b = Mesa2(sorteio.NextDouble() * 30, sorteio.NextDouble() * 30, sorteio.NextDouble() * Math.PI);

            var porPontos = AlgumPontoBemDentro(a, b) || AlgumPontoBemDentro(b, a);
            var porEixos = RowDistributor.Overlap(a, b);

            // Só pode discordar quando a interseção é fininha demais para a
            // amostragem achar (menos de ~5 cm): aí Overlap diz sim e os
            // pontos não. O contrário nunca.
            if (porPontos && !porEixos) discordancias++;
        }

        Assert.Equal(0, discordancias);
    }

    private static PlacedTable Mesa2(double x, double y, double rumo)
    {
        var d = P(Math.Cos(rumo), Math.Sin(rumo));
        var n = P(-Math.Sin(rumo), Math.Cos(rumo));
        var o = P(x, y);

        Point3[] cantos =
        [
            o,
            P(o.X + d.X * 20, o.Y + d.Y * 20),
            P(o.X + d.X * 20 + n.X * 4, o.Y + d.Y * 20 + n.Y * 4),
            P(o.X + n.X * 4, o.Y + n.Y * 4),
        ];

        return new PlacedTable(1, 1, o, rumo, 20, 4, cantos, false);
    }

    /// <summary>Se algum ponto amostrado de a cai a mais de 5 mm para dentro de b.</summary>
    private static bool AlgumPontoBemDentro(PlacedTable a, PlacedTable b)
    {
        var (d, n) = Eixos(b);
        var (da, na) = Eixos(a);

        for (var i = 0; i <= 40; i++)
        {
            for (var j = 0; j <= 8; j++)
            {
                var px = a.Origin.X + da.X * (i * 0.5) + na.X * (j * 0.5);
                var py = a.Origin.Y + da.Y * (i * 0.5) + na.Y * (j * 0.5);

                var u = (px - b.Origin.X) * d.X + (py - b.Origin.Y) * d.Y;
                var v = (px - b.Origin.X) * n.X + (py - b.Origin.Y) * n.Y;

                if (u > 0.005 && u < 20 - 0.005 && v > 0.005 && v < 4 - 0.005) return true;
            }
        }

        return false;
    }

    // ------------------------------------------------ alinhamento quebrado

    /// <summary>
    /// Alinhamento em "V" invertido: dois trechos com direções diferentes,
    /// cada um com a sua família de fileiras (azimute diferente, fileira
    /// diferente). As duas famílias convergem para dentro da área; onde se
    /// encontram, a segunda não pisa na primeira: a mesa que pisaria é
    /// pulada e contada. Toda fileira nasce no seu trecho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AlinhamentoQuebradoDaDuasFamiliasSemSobreposicao()
    {
        var quebrado = new[] { P(0, 10), P(50, 0), P(100, 10) };
        var layout = Distribuir(alinhamento: quebrado);

        Assert.NotEmpty(layout.Tables);
        Assert.Contains(layout.Rows, f => f.Segment == 0);
        Assert.Contains(layout.Rows, f => f.Segment == 1);

        var direcoes = layout.Tables.Select(m => Math.Round(m.DirectionRadians, 6)).Distinct().ToList();
        Assert.Equal(2, direcoes.Count);

        // A primeira mesa de cada fileira nasce em cima do trecho da linha.
        foreach (var f in layout.Rows)
        {
            var (a, b) = f.Segment == 0 ? (quebrado[0], quebrado[1]) : (quebrado[1], quebrado[2]);
            var comprimento = Distancia(a, b);
            var origem = f.Tables[0].Origin;

            var afastamento = ((b.X - a.X) * (origem.Y - a.Y) - (b.Y - a.Y) * (origem.X - a.X)) / comprimento;
            Assert.Equal(0, afastamento, 6);
        }

        // Houve mesa pulada na junta, e cada uma pisa em alguma colocada.
        Assert.NotEmpty(layout.Overlapping);
        Assert.All(layout.Overlapping, pulada =>
            Assert.Contains(layout.Tables, colocada => RowDistributor.Overlap(pulada, colocada)));

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// Trecho de comprimento zero (dois cliques no mesmo lugar) é pulado; a
    /// linha continua valendo pelos outros trechos.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void TrechoDeComprimentoZeroEPulado()
    {
        var comRepetido = new[] { P(0, 0), P(0, 0), P(100, 0) };

        Assert.Equal(51, Distribuir(alinhamento: comRepetido).Tables.Count);
    }

    /// <summary>
    /// Um trecho de 5 mm (clique duplo com tremor) não é trecho: sem isto ele
    /// gerava uma família inteira de fileiras com o rumo do tremor. O tremor
    /// no fim da linha some, e a linha vale pelo trecho de verdade.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void TrechoDeCincoMilimetrosEPulado()
    {
        var comTremor = new[] { P(0, 0), P(100, 0), P(100.004, 0.003) };
        var layout = Distribuir(alinhamento: comTremor);

        Assert.Equal(51, layout.Tables.Count);
        Assert.All(layout.Tables, m => Assert.Equal(Norte, m.DirectionRadians, 9));
        Assert.Empty(layout.Overlapping);
    }

    /// <summary>
    /// Alinhamento em "L" de 90° pelas bordas de baixo e da direita: as
    /// fileiras do trecho vertical (que correm para oeste) caem inteiras em
    /// cima das do horizontal (que correm para o norte). Nenhuma é colocada,
    /// todas voltam em Overlapping com a posição em que teriam ficado.
    /// Contas: horizontal 17 fileiras × 3 = 51; vertical 9 estações (50/6)
    /// × 5 mesas (100/20 exatas) = 45.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NoEleDeNoventaGrausASegundaFamiliaVoltaComoSobreposta()
    {
        var ele = new[] { P(0, 0), P(100, 0), P(100, 50) };
        var layout = Distribuir(alinhamento: ele);

        Assert.Equal(51, layout.Tables.Count);
        Assert.Equal(45, layout.SkippedForOverlap);
        Assert.Equal(45, layout.Overlapping.Count);
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Segment));

        foreach (var pulada in layout.Overlapping)
        {
            Assert.Equal("(sobreposta)", pulada.Label);
            Assert.Equal(Math.PI, Math.Abs(pulada.DirectionRadians), 9);
            Assert.InRange(pulada.Origin.X, 0 - 1e-9, 100 + 1e-9);
            Assert.InRange(pulada.Origin.Y, 0 - 1e-9, 50 + 1e-9);
            Assert.Contains(layout.Tables, colocada => RowDistributor.Overlap(pulada, colocada));
        }

        NenhumaSobreposta(layout);
    }

    // ---------------------------------------------------- propriedade

    /// <summary>
    /// Áreas e alinhamentos aleatórios com semente fixa: nunca duas mesas se
    /// sobrepõem, toda mesa inteira tem os quatro cantos dentro, e toda mesa
    /// tem os cantos afastados exatamente pelo comprimento e pelo fundo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaNenhumaPisaEmOutraEmAreasAleatorias()
    {
        var sorteio = new Random(2026);

        for (var caso = 0; caso < 40; caso++)
        {
            // Polígono estrelado em volta de um centro: sempre simples.
            var lados = sorteio.Next(3, 9);
            var area = new List<Point3>();

            for (var i = 0; i < lados; i++)
            {
                var angulo = 2 * Math.PI * i / lados;
                var raio = 40 + sorteio.NextDouble() * 60;
                area.Add(P(raio * Math.Cos(angulo), raio * Math.Sin(angulo)));
            }

            var rumo = sorteio.NextDouble() * 2 * Math.PI;
            var alinhamento = new[]
            {
                P(-150 * Math.Cos(rumo), -150 * Math.Sin(rumo)),
                P(150 * Math.Cos(rumo), 150 * Math.Sin(rumo)),
            };

            var pitch = 5 + sorteio.NextDouble() * 5;
            var mesa = new TableFootprint(Length: 8 + sorteio.NextDouble() * 20, PlanDepth: 2 + sorteio.NextDouble() * 2.5);
            var lado = sorteio.Next(2) == 0 ? LineSide.Left : LineSide.Right;

            var layout = RowDistributor.Distribute(area, alinhamento, lado, pitch, sorteio.NextDouble(), mesa);

            NenhumaSobreposta(layout);

            foreach (var m in layout.Tables)
            {
                Assert.Equal(4, m.Corners.Count);
                Assert.Equal(mesa.Length, Distancia(m.Corners[0], m.Corners[1]), 6);
                Assert.Equal(mesa.PlanDepth, Distancia(m.Corners[1], m.Corners[2]), 6);

                if (!m.PartlyOutside)
                    Assert.All(m.Corners, c => Assert.True(Polygons.Contains(area, c.X, c.Y)));
            }
        }
    }

    // ------------------------------------------------------------ recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void PitchMenorQueOFundoDaMesaERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(pitch: 3.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(pitch: 4));
    }

    /// <summary>
    /// Medida abaixo de 10 cm é recusada. A revisão do 5.1 mostrou uma mesa
    /// de 1e-15 m travando o laço (o passo somado some abaixo da precisão do
    /// double) e uma de 0,1 mm gerando dez mil mesas por metro.
    /// </summary>
    [Theory]
    [Trait("Etapa", "5")]
    [InlineData(0, 4)]
    [InlineData(20, 0)]
    [InlineData(0.05, 4)]
    [InlineData(1e-15, 4)]
    [InlineData(20, 0.05)]
    [InlineData(double.NaN, 4)]
    [InlineData(20, double.PositiveInfinity)]
    public void MesaSemMedidaERecusada(double comprimento, double fundo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Distribuir(mesa: new TableFootprint(comprimento, fundo)));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void EspacamentoNegativoERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(gap: -0.1));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void LadoEmCimaERecusado()
    {
        Assert.Throws<ArgumentException>(() => Distribuir(lado: LineSide.On));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void AlinhamentoSemTrechoERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(alinhamento: [P(0, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(alinhamento: [P(0, 0), P(0, 0)]));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void AreaComMenosDeTresVerticesERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(area: [P(0, 0), P(100, 0)]));
    }

    // ------------------------------------------------------------ apoio

    private static bool Perto(Point3 p, double x, double y) =>
        Math.Abs(p.X - x) < 1e-6 && Math.Abs(p.Y - y) < 1e-6;

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Nenhum par de mesas com interseção de área positiva. Encostar (borda
    /// com borda) é permitido. Conferido de dois jeitos: pelo Overlap de
    /// produção e por um verificador independente, que pergunta se o centro
    /// e os cantos de uma mesa caem bem dentro da outra — para um Overlap
    /// frouxo não esconder sobreposição de todos os testes.
    /// </summary>
    private static void NenhumaSobreposta(PlanLayout layout)
    {
        var mesas = layout.Tables.ToList();

        for (var i = 0; i < mesas.Count; i++)
        {
            for (var j = i + 1; j < mesas.Count; j++)
            {
                Assert.False(
                    RowDistributor.Overlap(mesas[i], mesas[j]),
                    $"{mesas[i].Label} e {mesas[j].Label} se sobrepõem");
                Assert.False(
                    PontoNotavelDentro(mesas[i], mesas[j]) || PontoNotavelDentro(mesas[j], mesas[i]),
                    $"{mesas[i].Label} e {mesas[j].Label} se sobrepõem (por pontos)");
            }
        }
    }

    /// <summary>
    /// Os eixos unitários de uma mesa, tirados dos cantos: ao longo do
    /// comprimento e ao longo do fundo. Dos cantos, e não da direção com a
    /// normal esquerda, porque com as mesas à direita da linha o fundo vai
    /// para a direita.
    /// </summary>
    private static (Point3 D, Point3 N) Eixos(PlacedTable m)
    {
        var d = P((m.Corners[1].X - m.Corners[0].X) / m.Length, (m.Corners[1].Y - m.Corners[0].Y) / m.Length);
        var n = P((m.Corners[3].X - m.Corners[0].X) / m.PlanDepth, (m.Corners[3].Y - m.Corners[0].Y) / m.PlanDepth);

        return (d, n);
    }

    /// <summary>Se o centro ou um canto de a cai a mais de 5 mm para dentro de b.</summary>
    private static bool PontoNotavelDentro(PlacedTable a, PlacedTable b)
    {
        var (d, n) = Eixos(b);

        var centro = P((a.Corners[0].X + a.Corners[2].X) / 2, (a.Corners[0].Y + a.Corners[2].Y) / 2);

        foreach (var p in a.Corners.Append(centro))
        {
            var u = (p.X - b.Origin.X) * d.X + (p.Y - b.Origin.Y) * d.Y;
            var v = (p.X - b.Origin.X) * n.X + (p.Y - b.Origin.Y) * n.Y;

            if (u > 0.005 && u < b.Length - 0.005 && v > 0.005 && v < b.PlanDepth - 0.005) return true;
        }

        return false;
    }
}
