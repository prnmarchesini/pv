using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A distribuição em planta, nas três regras do Renan (26/09/2026): a
/// fileira corre perpendicular ao azimute da configuração; a linha de
/// alinhamento só diz onde cada fileira começa; mesa não passa da área.
///
/// O que se trava aqui é contagem e posição, em casos em que a resposta se
/// calcula à mão: retângulo dá a contagem exata; a mesa que passaria da
/// borda não entra; nenhuma mesa pisa em outra. O terreno não entra: isto é
/// planta, e a cota é da amostragem (5.2).
/// </summary>
public class RowDistributorTests
{
    private static Point3 P(double x, double y) => new(x, y, 0);

    /// <summary>Retângulo de 100 m (leste-oeste) por 50 m (norte-sul).</summary>
    private static readonly Point3[] Retangulo = [P(0, 0), P(100, 0), P(100, 50), P(0, 50)];

    /// <summary>A linha de alinhamento na borda oeste, de baixo para cima; as fileiras vão para a direita (leste).</summary>
    private static readonly Point3[] Alinhamento = [P(0, 0), P(0, 50)];

    /// <summary>Mesa de 20 m de comprimento (ao longo da fileira) ocupando 4 m em planta (no sentido do azimute).</summary>
    private static readonly TableFootprint Mesa = new(Length: 20, PlanDepth: 4);

    /// <summary>Subida para o sul (mesa olhando para o norte): fileiras leste-oeste.</summary>
    private const double Sul = Math.PI;

    private static PlanLayout Distribuir(
        IReadOnlyList<Point3>? area = null,
        IReadOnlyList<Point3>? alinhamento = null,
        LineSide lado = LineSide.Right,
        double pitch = 6,
        double gap = 0,
        TableFootprint? mesa = null,
        double azimute = Sul) =>
        RowDistributor.Distribute(area ?? Retangulo, alinhamento ?? Alinhamento, lado, pitch, gap, mesa ?? Mesa, azimute);

    // ----------------------------------------------------- o retângulo

    /// <summary>
    /// Linha de (0,0) a (0,50) na borda oeste, fileiras para o leste, pitch
    /// 6 e mesa de 4 m de fundo: a fileira 1 encosta no início da linha
    /// (y = 0) e as faixas são [0,4], [6,10], …, [42,46]; a de [48,52]
    /// passaria da borda de cima e não entra. Ao longo de cada fileira,
    /// 100/20 = 5 mesas exatas. 8 × 5 = 40 mesas, 5 descartadas.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ORetanguloDaAContagemExata()
    {
        var layout = Distribuir();

        Assert.Equal(8, layout.Rows.Count);
        Assert.All(layout.Rows, fileira => Assert.Equal(5, fileira.Tables.Count));
        Assert.Equal(40, layout.Tables.Count);
        Assert.Equal(5, layout.DroppedOutside);
        Assert.All(layout.Tables, m => Assert.False(m.PartlyOutside));
    }

    /// <summary>
    /// A fileira 1 encosta no início da linha e corre para o leste. A célula
    /// ocupa [0,4] em y, e o fundo (canto 3 − canto 0) aponta no sentido do
    /// azimute, para o sul: a origem, que é o canto da borda BAIXA, fica em
    /// y = 4, e a borda alta em y = 0. A mesa olha para o norte.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void APrimeiraFileiraEncostaNoInicioDaLinhaECorrePerpendicularAoAzimute()
    {
        var primeira = Distribuir().Rows[0].Tables[0];

        Assert.Equal(1, primeira.Row);
        Assert.Equal(1, primeira.Number);
        Assert.Equal(0, primeira.DirectionRadians, 9);

        Assert.Equal(4, primeira.Corners.Count);
        Assert.Contains(primeira.Corners, c => Perto(c, 0, 0));
        Assert.Contains(primeira.Corners, c => Perto(c, 20, 0));
        Assert.Contains(primeira.Corners, c => Perto(c, 20, 4));
        Assert.Contains(primeira.Corners, c => Perto(c, 0, 4));

        Assert.Equal(4, primeira.Origin.Y, 9);
        Assert.Equal(0, primeira.Corners[3].Y, 9);
    }

    /// <summary>A fileira corre perpendicular ao azimute, seja qual for o rumo da linha: linha diagonal, fileira leste-oeste começando nela.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AFileiraCorrePerpendicularAoAzimuteENaoALinha()
    {
        var diagonal = new[] { P(0, 0), P(30, 50) };
        var layout = Distribuir(alinhamento: diagonal);

        Assert.All(layout.Tables, m => Assert.Equal(0, m.DirectionRadians, 9));

        // A fileira 2 (faixa [6,10]) começa onde a linha x = 0,6·y é mais
        // adiantada dentro da faixa: y = 9,999 → x = 5,9994. A origem é o
        // canto da borda baixa, em y = 10.
        var segunda = layout.Rows[1].Tables[0];
        Assert.Equal(10, segunda.Origin.Y, 9);
        Assert.Equal(0.6 * 9.999, segunda.Origin.X, 6);
    }

    /// <summary>Com o azimute para o leste, as fileiras correm norte-sul e a linha da borda de baixo é que as começa.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComOAzimuteParaOLesteAsFileirasCorremNorteSul()
    {
        var baixo = new[] { P(0, 0), P(100, 0) };
        var layout = Distribuir(alinhamento: baixo, lado: LineSide.Left, azimute: Math.PI / 2);

        Assert.All(layout.Tables, m => Assert.Equal(Math.PI / 2, m.DirectionRadians, 9));
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Tables[0].Origin.Y, 9));
        Assert.All(layout.Rows, f => Assert.Equal(0, Math.Min(f.Tables[0].Corners[0].X, f.Tables[0].Corners[3].X) % 6, 9));

        // Faixas em x: [0,4], [6,10], …, [96,100]: 17 fileiras de 2 mesas (50/20).
        Assert.Equal(17, layout.Rows.Count);
        Assert.All(layout.Rows, f => Assert.Equal(2, f.Tables.Count));
    }

    /// <summary>O pitch é medido no sentido do azimute, de início a início.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPitchEMedidoNoSentidoDoAzimute()
    {
        var segunda = Distribuir().Rows[1].Tables[0];

        Assert.Equal(10, segunda.Origin.Y, 9);
        Assert.Equal(0, segunda.Origin.X, 9);
        Assert.Contains(segunda.Corners, c => Perto(c, 20, 6));
    }

    /// <summary>
    /// Com espaçamento de 1 m: mesas em x = 0, 21, 42 e 63. A quinta
    /// terminaria em 104, fora do retângulo: não entra, e conta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEspacamentoEntraEntreAsMesasEAQueNaoCabeNaoEntra()
    {
        var layout = Distribuir(gap: 1);
        var fileira = layout.Rows[0];

        Assert.Equal(4, fileira.Tables.Count);
        Assert.Equal([0, 21, 42, 63], fileira.Tables.Select(m => Math.Round(m.Origin.X, 6)));

        // Uma por fileira, mais as cinco posições da faixa [48,52], que
        // passa da borda de cima inteira.
        Assert.Equal(8 + 5, layout.DroppedOutside);
    }

    /// <summary>Numeração do plano de requisitos: F1.1, F1.2 ..., F2.1 ...</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AsMesasSaoNumeradasPorFileira()
    {
        var layout = Distribuir();

        Assert.Equal("F1.1", layout.Rows[0].Tables[0].Label);
        Assert.Equal("F1.5", layout.Rows[0].Tables[4].Label);
        Assert.Equal("F2.1", layout.Rows[1].Tables[0].Label);
        Assert.Equal("F8.3", layout.Rows[7].Tables[2].Label);
    }

    /// <summary>
    /// A fileira 1 é a do início da linha: com a linha traçada de cima para
    /// baixo, F1 fica no topo (faixa [46,50]) e a célula cresce para baixo,
    /// no sentido em que a linha caminha.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AFileiraUmEADoInicioDaLinha()
    {
        var deCimaParaBaixo = new[] { P(0, 50), P(0, 0) };
        var layout = Distribuir(alinhamento: deCimaParaBaixo, lado: LineSide.Left);

        Assert.Equal(8, layout.Rows.Count);
        Assert.Contains(layout.Rows[0].Tables[0].Corners, c => Perto(c, 0, 50));
        Assert.Contains(layout.Rows[0].Tables[0].Corners, c => Perto(c, 0, 46));
        Assert.Contains(layout.Rows[1].Tables[0].Corners, c => Perto(c, 0, 44));
        Assert.All(layout.Tables, m => Assert.Equal(0, m.DirectionRadians, 9));
    }

    // -------------------------------------------------------------- lado

    /// <summary>
    /// À esquerda da linha (para o oeste) não há área: nenhuma fileira. É o
    /// que acontece quando o usuário clica o lado errado, e o resultado tem
    /// que dizer isso em vez de inventar mesa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OLadoErradoNaoTemFileira()
    {
        var layout = Distribuir(lado: LineSide.Left);

        Assert.Empty(layout.Rows);
        Assert.Empty(layout.Tables);
    }

    /// <summary>
    /// A linha no meio da área: as mesas começam nela e só vão para o lado
    /// escolhido; a metade de trás fica vazia. O motor não adivinha que o
    /// usuário queria os dois lados.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaNoMeioSoEnchOLadoEscolhidoEAsMesasComecamNela()
    {
        var meio = new[] { P(50, 0), P(50, 50) };
        var layout = Distribuir(alinhamento: meio);

        Assert.Equal(8, layout.Rows.Count);
        Assert.All(layout.Rows, f => Assert.Equal(50, f.Tables[0].Origin.X, 9));
        Assert.All(layout.Rows, f => Assert.Equal(2, f.Tables.Count));
        Assert.All(layout.Tables, m => Assert.True(m.Origin.X >= 50 - 1e-9));
    }

    /// <summary>
    /// A linha fora e longe da área, do lado de trás: as fileiras nascem
    /// nela e as mesas começam onde a área começa, não atrás.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaAfastadaDaAreaAindaAlinha()
    {
        var atras = new[] { P(-12, 0), P(-12, 50) };
        var layout = Distribuir(alinhamento: atras);

        Assert.Equal(8, layout.Rows.Count);
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Tables[0].Origin.X, 9));
        Assert.Equal(40, layout.Tables.Count);
    }

    /// <summary>
    /// Só nascem fileiras cujas faixas cruzam a linha: uma linha de (0,0) a
    /// (0,20) dá as faixas [0,4], [6,10], [12,16] e [18,22] (esta cruza a
    /// linha na ponta), quatro fileiras, mesmo com a área seguindo até 50.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaCurtaDaMenosFileiras()
    {
        var curta = new[] { P(0, 0), P(0, 20) };
        var layout = Distribuir(alinhamento: curta);

        Assert.Equal(4, layout.Rows.Count);
        Assert.Equal(22, layout.Rows[3].Tables[0].Origin.Y, 9);
    }

    /// <summary>
    /// Linha quebrada escalonada: as fileiras de baixo começam em x = 0, as
    /// de cima em x = 10, e a que cruza o degrau começa no ponto mais
    /// adiantado dele (x = 10), para nenhum canto ficar atrás da linha. É
    /// para isso que a linha existe: o alinhamento lateral.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ALinhaQuebradaEscalonaOComecoDasFileiras()
    {
        var escada = new[] { P(0, 0), P(0, 25), P(10, 25), P(10, 50) };
        var layout = Distribuir(alinhamento: escada);

        Assert.All(layout.Tables, m => Assert.Equal(0, m.DirectionRadians, 9));

        foreach (var f in layout.Rows)
        {
            // A origem é o canto de cima da célula (borda baixa, azimute sul).
            var topo = f.Tables[0].Origin.Y;
            var esperado = topo - 1e-3 < 25 ? 0 : 10;
            Assert.Equal(esperado, f.Tables[0].Origin.X, 6);
        }

        Assert.Contains(layout.Rows, f => Math.Abs(f.Tables[0].Origin.X) < 1e-6);
        Assert.Contains(layout.Rows, f => Math.Abs(f.Tables[0].Origin.X - 10) < 1e-6);
    }

    /// <summary>Linha paralela às fileiras (leste-oeste com azimute sul): não há como escolher o lado. Recusada com explicação.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaParalelaAsFileirasERecusada()
    {
        var paralela = new[] { P(0, 0), P(100, 0) };

        var erro = Assert.Throws<ArgumentException>(() => Distribuir(alinhamento: paralela));
        Assert.Contains("paralela", erro.Message);
    }

    // ----------------------------------------------------- área irregular

    /// <summary>
    /// Triângulo retângulo: 100 m na base e 50 m de altura, hipotenusa
    /// x = 100 − 2y. A fileira em [y, y+4] cabe até x = 92 − 12k no topo da
    /// faixa: 4 mesas na primeira, e menos a cada fileira; a que passaria
    /// da hipotenusa não entra.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OTrianguloTemMenosMesasACadaFileira()
    {
        var triangulo = new[] { P(0, 0), P(100, 0), P(0, 50) };
        var layout = Distribuir(area: triangulo);

        var porFileira = layout.Rows.Select(f => f.Tables.Count).ToList();

        Assert.Equal(porFileira.OrderByDescending(n => n), porFileira);
        Assert.Equal(4, porFileira[0]);
        Assert.All(layout.Tables, m => Assert.All(m.Corners, c => Assert.True(Polygons.Contains(triangulo, c.X, c.Y))));

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// O "L" é côncavo: as fileiras de baixo cruzam o retângulo inteiro, as
    /// de cima só a perna esquerda. Nenhuma mesa no quadrante que falta.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEleNaoPoeMesaNoQuadranteQueFalta()
    {
        var ele = new[] { P(0, 0), P(100, 0), P(100, 25), P(50, 25), P(50, 50), P(0, 50) };
        var layout = Distribuir(area: ele);

        Assert.All(layout.Tables, m => Assert.All(m.Corners, c => Assert.True(Polygons.Contains(ele, c.X, c.Y))));

        // Fileiras de y ≥ 30 só têm a perna esquerda: 50/20 = 2 inteiras.
        var alta = layout.Rows.First(f => f.Tables[0].Origin.Y >= 30);
        Assert.Equal(2, alta.Tables.Count);

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// Área com um furo em forma de "U": a fileira que cruza o vão nasce em
    /// dois trechos, e a numeração continua de um para o outro.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AFileiraQueCruzaUmVaoNasceEmDoisTrechos()
    {
        var u = new[] { P(0, 0), P(40, 0), P(40, 30), P(60, 30), P(60, 0), P(100, 0), P(100, 50), P(0, 50) };
        var layout = Distribuir(area: u);

        var baixa = layout.Rows[0];

        // Trecho [0,40]: mesas em 0 e 20. Trecho [60,100]: mesas em 60 e 80.
        Assert.Equal([0, 20, 60, 80], baixa.Tables.Select(m => Math.Round(m.Origin.X, 6)));
        Assert.Equal([1, 2, 3, 4], baixa.Tables.Select(m => m.Number));

        NenhumaSobreposta(layout);
    }

    /// <summary>
    /// Um dente triangular da área entrando 3 m pela borda de baixo, dentro
    /// da posição da mesa F1.1: os quatro cantos estariam dentro, mas a
    /// mesa é invadida e não entra. A posição seguinte vira F1.1.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RecorteDaAreaDentroDaMesaDescartaAMesa()
    {
        var comDente = new[] { P(0, 0), P(5, 0), P(10, 3), P(15, 0), P(100, 0), P(100, 50), P(0, 50) };
        var layout = Distribuir(area: comDente);

        Assert.Equal(4, layout.Rows[0].Tables.Count);
        Assert.Equal(20, layout.Rows[0].Tables[0].Origin.X, 9);
        Assert.Equal("F1.1", layout.Rows[0].Tables[0].Label);
        Assert.Equal(5 + 1, layout.DroppedOutside);
    }

    /// <summary>
    /// O recorte que entra e sai da mesa sem deixar vértice dentro: um canal
    /// de y = 1 a 3 atravessando a fileira 1 de lado a lado. Só a aresta
    /// cruzando pega; nenhuma mesa da fileira 1 entra.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RecorteQueAtravessaAMesaSemVerticeDentroTambemDescarta()
    {
        var comCanal = new[] { P(0, 0), P(100, 0), P(100, 1), P(-5, 1), P(-5, 3), P(100, 3), P(100, 50), P(0, 50), P(0, 3), P(0, 1) };
        var layout = Distribuir(area: comCanal);

        Assert.All(layout.Rows, f => Assert.True(f.Tables[0].Origin.Y >= 6 - 1e-9));
        Assert.Equal(7, layout.Rows.Count);
    }

    /// <summary>
    /// Área fininha (1,8 m de fundo) dentro da faixa 1, sem tocar nenhuma
    /// linha de corte: a faixa é achada (pelas arestas recortadas) mas
    /// nenhuma mesa cabe: nada entra, e as posições descartadas contam.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AreaFininhaNaoCabeMesaNenhuma()
    {
        var fininha = new[] { P(0, 1), P(100, 1), P(100, 1.8), P(0, 1.8) };
        var layout = Distribuir(area: fininha, alinhamento: [P(0, 0), P(0, 4)]);

        Assert.Empty(layout.Rows);
        Assert.True(layout.DroppedOutside >= 1);
    }

    /// <summary>
    /// Linha traçada 2 mm atrás da borda da área (clique à mão, sem OSNAP):
    /// as mesas começam onde a área começa, e nada muda.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void LinhaDoisMilimetrosAtrasDaBordaNaoMudaNada()
    {
        var quaseNaBorda = new[] { P(-0.002, 0), P(-0.002, 50) };
        var layout = Distribuir(alinhamento: quaseNaBorda);

        Assert.Equal(40, layout.Tables.Count);
        Assert.All(layout.Rows, f => Assert.Equal(0, f.Tables[0].Origin.X, 6));
    }

    /// <summary>
    /// Coordenadas UTM e tudo girado 30° (o azimute junto): o caso real.
    /// Mesma contagem do retângulo na origem, e a fileira a 30°.
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
        var alinhamento = new[] { Girar(0, 0), Girar(0, 50) };

        // Girar o mundo 30° anti-horário tira 30° do azimute.
        var layout = Distribuir(area: area, alinhamento: alinhamento, azimute: Sul - angulo);

        Assert.Equal(8, layout.Rows.Count);
        Assert.Equal(40, layout.Tables.Count);
        Assert.Equal(5, layout.DroppedOutside);
        Assert.Equal(angulo, layout.Tables[0].DirectionRadians, 9);
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

    // ---------------------------------------------------- propriedade

    /// <summary>
    /// Áreas, linhas e azimutes aleatórios com semente fixa: nunca duas
    /// mesas se sobrepõem, toda mesa tem os quatro cantos dentro da área, os
    /// cantos afastados exatamente pelo comprimento e pelo fundo, e toda
    /// fileira perpendicular ao azimute.
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

            var azimute = sorteio.NextDouble() * 2 * Math.PI;
            var subida = P(Math.Sin(azimute), Math.Cos(azimute));

            // A linha ao longo do azimute (atravessando as fileiras), com um
            // desvio de até 40° para não ser sempre a mesma.
            var desvio = (sorteio.NextDouble() - 0.5) * 80 * Math.PI / 180;
            var rumoDaLinha = Math.Atan2(subida.Y, subida.X) + desvio;
            var alinhamento = new[]
            {
                P(-150 * Math.Cos(rumoDaLinha), -150 * Math.Sin(rumoDaLinha)),
                P(150 * Math.Cos(rumoDaLinha), 150 * Math.Sin(rumoDaLinha)),
            };

            var pitch = 5 + sorteio.NextDouble() * 5;
            var mesa = new TableFootprint(Length: 8 + sorteio.NextDouble() * 20, PlanDepth: 2 + sorteio.NextDouble() * 2.5);
            var lado = sorteio.Next(2) == 0 ? LineSide.Left : LineSide.Right;

            var layout = RowDistributor.Distribute(area, alinhamento, lado, pitch, sorteio.NextDouble(), mesa, azimute);

            NenhumaSobreposta(layout);

            foreach (var m in layout.Tables)
            {
                Assert.Equal(4, m.Corners.Count);
                Assert.Equal(mesa.Length, Distancia(m.Corners[0], m.Corners[1]), 6);
                Assert.Equal(mesa.PlanDepth, Distancia(m.Corners[1], m.Corners[2]), 6);
                Assert.All(m.Corners, c => Assert.True(Polygons.Contains(area, c.X, c.Y)));

                // Perpendicular ao azimute.
                var aoLongo = Math.Cos(m.DirectionRadians) * subida.X + Math.Sin(m.DirectionRadians) * subida.Y;
                Assert.Equal(0, aoLongo, 9);
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

    [Fact]
    [Trait("Etapa", "5")]
    public void AzimuteNaoFinitoERecusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Distribuir(azimute: double.NaN));
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
