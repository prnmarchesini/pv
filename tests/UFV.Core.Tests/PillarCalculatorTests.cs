using UFV.Core.Invariants;
using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// Os pilares de uma mesa resolvida: cota do terreno no pé, cota de topo,
/// altura livre, enterro e comprimento ideal. Regra sagrada 1 em toda saída:
/// o pilar nunca flutua, e o que estoura é marcado, nunca escondido.
/// </summary>
public class PillarCalculatorTests
{
    private const double Grau = Math.PI / 180;
    private const double Tilt = 20 * Grau;

    private static double ZDoPlano(double x, double y) => 0.02 * x + 0.03 * y + 700;

    private static Tin PlanoInclinado()
    {
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));

        return new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);
    }

    private static Tin Plano(double cota)
    {
        Point3 P(double x, double y) => new(x, y, cota);

        return new Tin(
        [
            new Triangle(P(-100, -100), P(300, -100), P(300, 300)),
            new Triangle(P(-100, -100), P(300, 300), P(-100, 300)),
        ]);
    }

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    private static SystemConfiguration Config() => SystemConfiguration.Default;

    /// <summary>A terceira mesa da segunda fileira de um retângulo: linha norte-sul na borda oeste, fileiras para o leste.</summary>
    private static (PlacedTable Cell, RowOrientation Orientation) Celula()
    {
        var geo = Geometria();
        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };

        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, 8, 0.5, new TableFootprint(geo.Length, geo.Depth * Math.Cos(Tilt)));
        var cell = layout.Rows[1].Tables[2];
        var orientacao = RowOrientation.Resolve(cell.DirectionRadians, LineSide.Right, Config().UpslopeAzimuthRadians);

        return (cell, orientacao);
    }

    private static SolvedTable Resolvida(double z0, double z1) => new("F2.3", z0, z1, 0, false, null);

    // ------------------------------------------------------ mesa nivelada

    /// <summary>
    /// Mesa nivelada em 700,50 sobre terreno plano em 700: todo pilar tem
    /// altura livre 0,50 + (sobra + T2) × sen(tilt), enterro 0,90 e
    /// comprimento igual à soma. É o número do 3.5, agora sobre terreno.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void NaMesaNiveladaTodoPilarTemAMesmaAltura()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), Plano(700), Config());

        Assert.Equal(geo.Pillars.Count, pilares.Pillars.Count);
        Assert.True(pilares.AllSound);
        Assert.Equal(0, pilares.LongitudinalTiltRadians, 12);

        var livreEsperada = 0.50 + geo.PillarRow * Math.Sin(Tilt);

        foreach (var pilar in pilares.Pillars)
        {
            Assert.Equal(700, pilar.GroundZ!.Value, 9);
            Assert.Equal(700 + livreEsperada, pilar.TopZ, 9);
            Assert.Equal(livreEsperada, pilar.FreeHeight!.Value, 9);
            Assert.Equal(0.90, pilar.Embedment, 9);
            Assert.Equal(livreEsperada + 0.90, pilar.Length!.Value, 9);
            Assert.Null(pilar.Problem);
        }

        Assert.Equal(livreEsperada + 0.90, pilares.LongestPillar!.Value, 9);
    }

    /// <summary>O pé de cada pilar fica dentro da célula, e a estação é a da tabela.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPeDoPilarFicaNaCelula()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), Plano(700), Config());

        var fundo = geo.Depth * Math.Cos(Tilt);

        for (var i = 0; i < pilares.Pillars.Count; i++)
        {
            Assert.Equal(geo.Pillars[i].Station, pilares.Pillars[i].Station, 9);
            Assert.InRange(pilares.Pillars[i].X, cell.Origin.X - 1e-6, cell.Origin.X + geo.Length + 1e-6);
            Assert.InRange(pilares.Pillars[i].Y, cell.Origin.Y - 1e-6, cell.Origin.Y + fundo + 1e-6);
        }
    }

    // ------------------------------------------------------ mesa girada

    /// <summary>
    /// Mesa com a ponta final 0,50 m mais alta: a cota da ponta baixa na
    /// estação s é início + 0,50·s/L, exatamente como o 5.3 supôs — e o topo
    /// do pilar é isso mais a subida transversal (reduzida pelo cosseno do giro).
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ComGiroACotaDaPontaBaixaEAQueOCincoTresSupos()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 701.00), Plano(700), Config());

        Assert.Equal(Math.Asin(0.50 / geo.Length), pilares.LongitudinalTiltRadians, 12);

        // Com giro os comprimentos diferem de pilar para pilar: o mais
        // comprido é o máximo de verdade, e o Describe traz a faixa.
        Assert.Equal(pilares.Pillars.Max(p => p.Length!.Value), pilares.LongestPillar!.Value, 9);
        Assert.True(pilares.Pillars.Max(p => p.Length!.Value) - pilares.Pillars.Min(p => p.Length!.Value) > 0.3);
        Assert.Contains(" a ", pilares.Describe());

        for (var i = 0; i < geo.Pillars.Count; i++)
        {
            var s = geo.Pillars[i].Station;

            var pontaBaixa = pilares.Placement.Apply(new Point3(s, 0, 0));
            Assert.Equal(700.50 + 0.50 * s / geo.Length, pontaBaixa.Z, 9);

            var topoEsperado = pontaBaixa.Z + geo.PillarRow * Math.Sin(Tilt) * Math.Cos(pilares.LongitudinalTiltRadians);
            Assert.Equal(topoEsperado, pilares.Pillars[i].TopZ, 9);
        }

        Assert.True(pilares.AllSound);
    }

    /// <summary>
    /// O giro desloca o pé do pilar em planta, e o terreno é reamostrado na
    /// posição real: num plano inclinado, a cota do terreno do pilar é a do
    /// plano no X e Y reportados, e o X e Y reportados diferem dos da mesa
    /// sem giro. É a pendência do 5.2 fechada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OPeDoPilarEReamostradoNaPosicaoReal()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var semGiro = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), PlanoInclinado(), Config());
        var comGiro = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 703.50), PlanoInclinado(), Config());

        var ultimo = geo.Pillars.Count - 1;

        // O último pilar anda em planta com o giro (o comprimento encurta para L·cos).
        var desloc = Math.Sqrt(
            Math.Pow(comGiro.Pillars[ultimo].X - semGiro.Pillars[ultimo].X, 2)
            + Math.Pow(comGiro.Pillars[ultimo].Y - semGiro.Pillars[ultimo].Y, 2));

        Assert.True(desloc > 0.05, $"o pé só andou {desloc} m");

        foreach (var pilar in comGiro.Pillars)
            Assert.Equal(ZDoPlano(pilar.X, pilar.Y), pilar.GroundZ!.Value, 6);
    }

    // ------------------------------------------------------ estouros

    /// <summary>
    /// Mesa baixa demais: em 699,90 sobre terreno em 700, o topo do pilar
    /// ainda fica acima do chão pela subida transversal (0,9 m), mas em
    /// 698,50 não: altura livre negativa é a regra sagrada 1 violada, o
    /// pilar é marcado com o motivo, sem comprimento, e a mesa não é movida.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaAbaixoDoChaoMarcaOPilarSemEsconder()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(698.50, 698.50), Plano(700), Config());

        Assert.False(pilares.AllSound);
        Assert.Equal(geo.Pillars.Count, pilares.ProblemCount);

        foreach (var pilar in pilares.Pillars)
        {
            Assert.True(pilar.FreeHeight < 0);
            Assert.Null(pilar.Length);
            Assert.NotNull(pilar.Problem);
            Assert.Contains("enterrado", pilar.Problem);
            Assert.Equal(700, pilar.GroundZ!.Value, 9);
        }

        Assert.Contains("com problema", pilares.Describe());
        Assert.Null(pilares.LongestPillar);
    }

    /// <summary>
    /// Um pilar só com problema: terreno com um morro sob o segundo pilar.
    /// Os outros seguem inteiros; o resultado diz qual estourou.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SoOPilarNoMorroEMarcado()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var nivelado = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), Plano(700), Config());
        var segundo = nivelado.Pillars[1];

        // Um morro pontudo de 3 m exatamente no pé do segundo pilar.
        Point3 P(double x, double y, double z) => new(x, y, z);

        var morro = new Tin(
        [
            new Triangle(P(segundo.X - 0.5, segundo.Y - 0.5, 700), P(segundo.X + 0.5, segundo.Y - 0.5, 700), P(segundo.X, segundo.Y, 703)),
            new Triangle(P(segundo.X + 0.5, segundo.Y - 0.5, 700), P(segundo.X + 0.5, segundo.Y + 0.5, 700), P(segundo.X, segundo.Y, 703)),
            new Triangle(P(segundo.X + 0.5, segundo.Y + 0.5, 700), P(segundo.X - 0.5, segundo.Y + 0.5, 700), P(segundo.X, segundo.Y, 703)),
            new Triangle(P(segundo.X - 0.5, segundo.Y + 0.5, 700), P(segundo.X - 0.5, segundo.Y - 0.5, 700), P(segundo.X, segundo.Y, 703)),
            new Triangle(P(-100, -100, 700), P(300, -100, 700), P(segundo.X - 0.5, segundo.Y - 0.5, 700)),
        ]);

        // O plano em volta é incompleto de propósito (só o morro e um
        // triângulo): os pilares fora dele caem "fora do terreno", e o do
        // morro cai "enterrado". Os dois motivos são marcados, distintos.
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), morro, Config());

        Assert.Contains("enterrado", pilares.Pillars[1].Problem!);
        Assert.Contains(pilares.Pillars, p => p.Problem is not null && p.Problem.Contains("fora do terreno"));
    }

    /// <summary>Pé fora do terreno: sem cota, sem comprimento, marcado.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void PeForaDoTerrenoEMarcado()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        Point3 P(double x, double y) => new(x, y, 700);
        var longe = new Tin([new Triangle(P(1000, 1000), P(1010, 1000), P(1010, 1010))]);

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), longe, Config());

        Assert.All(pilares.Pillars, p =>
        {
            Assert.Null(p.GroundZ);
            Assert.Null(p.FreeHeight);
            Assert.Null(p.Length);
            Assert.Contains("fora do terreno", p.Problem!);
        });
    }

    // ------------------------------------------------------ regra sagrada 1

    /// <summary>
    /// Regra sagrada 1 em toda saída: em terrenos e cotas aleatórios, todo
    /// pilar sem problema tem altura livre e enterro acima da tolerância do
    /// verificador, e todo pilar com altura livre abaixo dela tem problema.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void RegraSagradaUmEmTodaSaida()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var sorteio = new Random(56);

        for (var caso = 0; caso < 40; caso++)
        {
            var cota = 699 + sorteio.NextDouble() * 3;
            var z0 = cota + (sorteio.NextDouble() - 0.5) * 2;
            var z1 = z0 + (sorteio.NextDouble() - 0.5) * 2;

            var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(z0, z1), Plano(cota), Config());

            foreach (var pilar in pilares.Pillars)
            {
                if (pilar.Problem is null)
                {
                    Assert.Null(FloatingPillar.Check(pilar.FreeHeight!.Value, pilar.Embedment));
                    Assert.Equal(pilar.FreeHeight!.Value + pilar.Embedment, pilar.Length!.Value, 9);
                }
                else
                {
                    Assert.True(pilar.FreeHeight is null || pilar.FreeHeight < FloatingPillar.Tolerancia);
                    Assert.Null(pilar.Length);
                }
            }
        }
    }

    /// <summary>
    /// Altura livre acima da escala do Core (vértice espúrio em z = 0 sob a
    /// mesa): marcado como fora de escala, sem comprimento, sem exceção. É
    /// o "nunca escondido" do plano valendo também para o absurdo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AlturaLivreForaDeEscalaEMarcadaENaoExcecao()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), Plano(0), Config());

        Assert.False(pilares.AllSound);
        Assert.All(pilares.Pillars, p =>
        {
            Assert.Null(p.Length);
            Assert.Contains("fora de escala", p.Problem!);
            Assert.True(p.FreeHeight > 20);
        });
    }

    /// <summary>
    /// Desnível maior que o comprimento (só possível sem limite de
    /// declividade): não há mesa rígida assim. Todo pilar marcado com o
    /// motivo, matriz nivelada, sem exceção derrubando a fileira.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DesnivelMaiorQueOComprimentoEMarcadoENaoExcecao()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700, 730), Plano(700), Config());

        Assert.Equal(0, pilares.LongitudinalTiltRadians, 12);
        Assert.All(pilares.Pillars, p => Assert.Contains("não há giro", p.Problem!));
    }

    /// <summary>O enterro é o mínimo da configuração, e não um número fixo.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OEnterroEOMinimoDaConfiguracao()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var config = Config() with { MinEmbedment = 1.20 };

        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.50, 700.50), Plano(700), config);

        Assert.All(pilares.Pillars, p =>
        {
            Assert.Equal(1.20, p.Embedment, 9);
            Assert.Equal(p.FreeHeight!.Value + 1.20, p.Length!.Value, 9);
        });
    }

    /// <summary>Célula de outro comprimento que a geometria é recusada: seria a mesa de outro perfil.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void CelulaDeOutroComprimentoERecusada()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var outra = cell with { Length = cell.Length + 1 };

        Assert.Throws<ArgumentException>(
            () => PillarCalculator.Compute(geo, outra, orientacao, Tilt, Resolvida(700.5, 700.5), Plano(700), Config()));
    }

    // ------------------------------------------------------ recusas

    [Fact]
    [Trait("Etapa", "5")]
    public void CotaNaoFinitaERecusada()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700, double.NaN), Plano(700), Config()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(double.PositiveInfinity, 700), Plano(700), Config()));
    }

    [Fact]
    [Trait("Etapa", "5")]
    public void ConfiguracaoQuebradaERecusada()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();

        Assert.Throws<InvalidOperationException>(
            () => PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.5, 700.5), Plano(700), Config() with { MinEmbedment = 9 }));
    }

    /// <summary>O comprimento é o ideal: não há arredondamento comercial (decisão do Renan no 4.1).</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void OComprimentoEOIdealSemArredondamento()
    {
        var geo = Geometria();
        var (cell, orientacao) = Celula();
        var pilares = PillarCalculator.Compute(geo, cell, orientacao, Tilt, Resolvida(700.4321, 700.4321), Plano(700), Config());

        var esperado = 0.4321 + geo.PillarRow * Math.Sin(Tilt) + 0.90;

        Assert.Equal(esperado, pilares.Pillars[0].Length!.Value, 9);
    }
}
