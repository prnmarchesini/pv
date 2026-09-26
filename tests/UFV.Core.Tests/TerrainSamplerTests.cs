using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// A amostragem do terreno sob a mesa: a cota no pé de cada pilar e sob a
/// ponta baixa de cada módulo da fileira de baixo, uma vez só.
///
/// O terreno dos testes é um plano, onde a cota certa em qualquer ponto se
/// calcula à mão: z = 0,02·x + 0,03·y + 700.
/// </summary>
public class TerrainSamplerTests
{
    private const double Grau = Math.PI / 180;

    private static double ZDoPlano(double x, double y) => 0.02 * x + 0.03 * y + 700;

    private static Tin PlanoInclinado()
    {
        Point3 P(double x, double y) => new(x, y, ZDoPlano(x, y));

        return new Tin(
        [
            new Triangle(P(0, 0), P(200, 0), P(200, 200)),
            new Triangle(P(0, 0), P(200, 200), P(0, 200)),
        ]);
    }

    private static SolarModule Risen() => new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() => new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableFrame Estrutura() => new(3.00, 2.50, 0.15, 0.07, 3.00, 0);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), Estrutura());

    /// <summary>Mesa colocada a 20° com a ponta baixa em (30, 40), olhando para o norte.</summary>
    private static Transform Colocacao(double x = 30, double y = 40) =>
        Transform.Place(20 * Grau, SystemConfiguration.Default.UpslopeAzimuthRadians, new Point3(x, y, 0));

    [Fact]
    [Trait("Etapa", "5")]
    public void HaUmaAmostraPorPilarEUmaPorColunaDaFileiraDeBaixo()
    {
        var geo = Geometria();
        var amostras = TerrainSampler.Sample(geo, Colocacao(), PlanoInclinado());

        Assert.Equal(geo.Pillars.Count, amostras.Pillars.Count);
        Assert.Equal(Mesa().Columns, amostras.LowEdge.Count);
        Assert.Equal(Enumerable.Range(0, Mesa().Columns), amostras.LowEdge.Select(m => m.Column));
        Assert.Equal(geo.Pillars.Select(p => p.Station), amostras.Pillars.Select(p => p.Station));
        Assert.True(amostras.IsComplete);
        Assert.Equal(0, amostras.OutsideCount);
    }

    /// <summary>
    /// A cota de cada pilar é a do plano onde ele fura o chão. O "onde" é a
    /// projeção do apoio do pilar levada pela matriz — o pilar é vertical.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ACotaDoPilarEADoTerrenoOndeEleFuraOChao()
    {
        var geo = Geometria();
        var colocacao = Colocacao();
        var amostras = TerrainSampler.Sample(geo, colocacao, PlanoInclinado());

        for (var i = 0; i < geo.Pillars.Count; i++)
        {
            var pe = colocacao.Apply(geo.Pillars[i].Anchor);

            Assert.Equal(pe.X, amostras.Pillars[i].X, 9);
            Assert.Equal(pe.Y, amostras.Pillars[i].Y, 9);
            Assert.Equal(ZDoPlano(pe.X, pe.Y), amostras.Pillars[i].GroundZ!.Value, 6);
        }
    }

    /// <summary>
    /// Sob a ponta baixa vale a cota mais alta entre as duas pontas e o
    /// meio: num plano que sobe para o norte e a mesa olhando para o norte,
    /// a ponta baixa corre leste-oeste e o plano sobe 0,02 por metro em x, então
    /// a cota mais alta é a da ponta leste do módulo.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void SobAPontaBaixaValeACotaMaisAlta()
    {
        var geo = Geometria();
        var colocacao = Colocacao();
        var amostras = TerrainSampler.Sample(geo, colocacao, PlanoInclinado());

        foreach (var modulo in geo.Modules.Where(m => m.Row == 0))
        {
            var a = colocacao.Apply(modulo.TopFace[0]);
            var b = colocacao.Apply(modulo.TopFace[1]);
            var esperada = Math.Max(ZDoPlano(a.X, a.Y), ZDoPlano(b.X, b.Y));

            var amostra = amostras.LowEdge.Single(m => m.Column == modulo.Column);

            Assert.Equal(esperada, amostra.GroundZ!.Value, 6);
            Assert.Equal((a.X + b.X) / 2, amostra.X, 9);
            Assert.Equal((a.Y + b.Y) / 2, amostra.Y, 9);
        }
    }

    /// <summary>
    /// O caso que a revisão do 5.2 pegou: uma crista do terreno cruzando a
    /// ponta baixa a um quarto do módulo, entre a ponta e o meio. Três
    /// pontos amostrados não a viam; a amostra da aresta inteira vê.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void UmaCristaEntreOsPontosDaArestaEVista()
    {
        var geo = Geometria();
        var colocacao = Colocacao();

        // A ponta baixa do módulo da coluna 3, no mundo.
        var modulo = geo.Modules.Single(m => m.Row == 0 && m.Column == 3);
        var a = colocacao.Apply(modulo.TopFace[0]);
        var b = colocacao.Apply(modulo.TopFace[1]);
        var crista = new Point3(a.X + (b.X - a.X) * 0.25, a.Y + (b.Y - a.Y) * 0.25, 0);

        // Um terreno plano em 700 com uma crista de 0,5 m: um "telhado" de
        // triângulos que sobe até a linha da crista, perpendicular à ponta
        // baixa, e desce do outro lado.
        Point3 P(double x, double y, double z) => new(x, y, z);

        var dx = (b.X - a.X) / 2;
        var dy = (b.Y - a.Y) / 2;

        var terreno = new Tin(
        [
            new Triangle(P(-100, -100, 700), P(crista.X - dy * 200, crista.Y + dx * 200, 700.5), P(crista.X + dy * 200, crista.Y - dx * 200, 700.5)),
            new Triangle(P(-100, -100, 700), P(crista.X + dy * 200, crista.Y - dx * 200, 700.5), P(300, -100, 700)),
            new Triangle(P(300, -100, 700), P(crista.X + dy * 200, crista.Y - dx * 200, 700.5), P(300, 300, 700)),
            new Triangle(P(300, 300, 700), P(crista.X + dy * 200, crista.Y - dx * 200, 700.5), P(crista.X - dy * 200, crista.Y + dx * 200, 700.5)),
            new Triangle(P(300, 300, 700), P(crista.X - dy * 200, crista.Y + dx * 200, 700.5), P(-100, 300, 700)),
            new Triangle(P(-100, 300, 700), P(crista.X - dy * 200, crista.Y + dx * 200, 700.5), P(-100, -100, 700)),
        ]);

        var amostras = TerrainSampler.Sample(geo, colocacao, terreno);
        var daColuna = amostras.LowEdge.Single(m => m.Column == 3);

        Assert.NotNull(daColuna.GroundZ);
        Assert.Equal(700.5, daColuna.GroundZ!.Value, 3);
    }

    /// <summary>
    /// Uma ponta dentro e outra fora do terreno: a amostra daquela coluna é
    /// null, e não o máximo do pedaço que existe. O plano acaba em x = 0 e a
    /// mesa cresce para oeste a partir de x = 10: o módulo que atravessa a
    /// borda é o que tem uma ponta em x &lt; 0 e outra em x &gt; 0.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ModuloComUmaPontaForaDoTerrenoENull()
    {
        var geo = Geometria();
        var colocacao = Colocacao(10, 40);
        var amostras = TerrainSampler.Sample(geo, colocacao, PlanoInclinado());

        var atravessa = geo.Modules.Where(m => m.Row == 0).Single(m =>
        {
            var a = colocacao.Apply(m.TopFace[0]);
            var b = colocacao.Apply(m.TopFace[1]);
            return (a.X < 0) != (b.X < 0);
        });

        Assert.Null(amostras.LowEdge.Single(m => m.Column == atravessa.Column).GroundZ);

        // E os módulos inteiramente dentro continuam com cota.
        var dentro = geo.Modules.Where(m => m.Row == 0 && m.Column < atravessa.Column).ToList();
        Assert.NotEmpty(dentro);
        Assert.All(dentro, m => Assert.NotNull(amostras.LowEdge.Single(s => s.Column == m.Column).GroundZ));
    }

    /// <summary>
    /// Duas pontas dentro e um buraco na triangulação no meio: null. O buraco
    /// é uma fresta de 10 cm sem triângulo, cruzando a ponta baixa.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void BuracoNoTerrenoSobAPontaBaixaENull()
    {
        var geo = Geometria();
        var colocacao = Colocacao();

        var modulo = geo.Modules.Single(m => m.Row == 0 && m.Column == 5);
        var a = colocacao.Apply(modulo.TopFace[0]);
        var b = colocacao.Apply(modulo.TopFace[1]);
        var meioX = (a.X + b.X) / 2;

        // Plano em 700 em duas placas, com uma fresta vertical (em x) de 10 cm.
        Point3 P(double x, double y) => new(x, y, 700);

        var terreno = new Tin(
        [
            new Triangle(P(-100, -100), P(meioX - 0.05, -100), P(meioX - 0.05, 300)),
            new Triangle(P(-100, -100), P(meioX - 0.05, 300), P(-100, 300)),
            new Triangle(P(meioX + 0.05, -100), P(300, -100), P(300, 300)),
            new Triangle(P(meioX + 0.05, -100), P(300, 300), P(meioX + 0.05, 300)),
        ]);

        var amostras = TerrainSampler.Sample(geo, colocacao, terreno);

        Assert.Null(amostras.LowEdge.Single(m => m.Column == 5).GroundZ);
        Assert.NotNull(amostras.LowEdge.Single(m => m.Column == 0).GroundZ);
    }

    /// <summary>
    /// A estação da ponta baixa é a do meio do módulo em coordenadas locais:
    /// sobra da esquerda + coluna × (largura + folga) + meia largura.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AEstacaoDaPontaBaixaEADoMeioDoModulo()
    {
        var amostras = TerrainSampler.Sample(Geometria(), Colocacao(), PlanoInclinado());

        foreach (var amostra in amostras.LowEdge)
        {
            var esperada = 0.10 + amostra.Column * (1.303 + 0.02) + 1.303 / 2;
            Assert.Equal(esperada, amostra.Station, 9);
        }
    }

    /// <summary>Numa mesa 1V não há fileira de cima, e cada coluna tem a sua amostra.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaUmVTambemEAmostrada()
    {
        var mesa = new TableLayout(Risen(), 14, TableArrangement.SingleRow, 0.02, 0.02, 0.10, 0.10);
        var geo = TableGeometry.Local(mesa, PillarTable.Distribute(mesa.Length, 3), new TableFrame(2.00, 1.50, 0.15, 0.07, 3.00, 0));

        var amostras = TerrainSampler.Sample(geo, Colocacao(), PlanoInclinado());

        Assert.Equal(14, amostras.LowEdge.Count);
        Assert.True(amostras.IsComplete);
    }

    /// <summary>A mais baixa é o mínimo das que existem: no plano, o canto sudoeste da mesa.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AMaisBaixaEOMinimoDasAmostras()
    {
        var amostras = TerrainSampler.Sample(Geometria(), Colocacao(), PlanoInclinado());

        var todas = amostras.Pillars.Select(p => p.GroundZ!.Value).Concat(amostras.LowEdge.Select(m => m.GroundZ!.Value));

        Assert.Equal(todas.Min(), amostras.LowestGround!.Value, 9);
        Assert.Equal(todas.Max(), amostras.HighestGround!.Value, 9);
        Assert.True(amostras.LowestGround < amostras.HighestGround);
    }

    /// <summary>A fileira de cima do 2V não entra: a ponta baixa dela não está junto ao solo.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AFileiraDeCimaNaoEAmostrada()
    {
        var geo = Geometria();
        var amostras = TerrainSampler.Sample(geo, Colocacao(), PlanoInclinado());

        Assert.Equal(geo.Modules.Count / 2, amostras.LowEdge.Count);
    }

    /// <summary>
    /// A amostra não depende da cota da mesa: colocada a 0 ou a 750 m, as
    /// cotas do terreno são as mesmas. É o que permite amostrar uma vez e
    /// otimizar depois.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void AAmostraNaoDependeDaCotaDaMesa()
    {
        var geo = Geometria();
        var emBaixo = TerrainSampler.Sample(geo, Colocacao(), PlanoInclinado());
        var noAlto = TerrainSampler.Sample(
            geo,
            Transform.Place(20 * Grau, SystemConfiguration.Default.UpslopeAzimuthRadians, new Point3(30, 40, 750)),
            PlanoInclinado());

        // Elemento a elemento: os records de amostra comparam por valor, as
        // listas que os guardam não.
        Assert.Equal(emBaixo.Pillars, noAlto.Pillars);
        Assert.Equal(emBaixo.LowEdge, noAlto.LowEdge);
    }

    /// <summary>Fora do terreno é null, nunca zero, e é contado.</summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ForaDoTerrenoENullEContado()
    {
        var geo = Geometria();
        var amostras = TerrainSampler.Sample(geo, Colocacao(1000, 1000), PlanoInclinado());

        Assert.False(amostras.IsComplete);
        Assert.Equal(amostras.Pillars.Count + amostras.LowEdge.Count, amostras.OutsideCount);
        Assert.All(amostras.Pillars, p => Assert.Null(p.GroundZ));
        Assert.All(amostras.LowEdge, m => Assert.Null(m.GroundZ));
        Assert.Null(amostras.HighestGround);
    }

    /// <summary>
    /// Mesa com uma ponta fora do terreno: o plano começa em x = 0, e a mesa
    /// olhando para o norte cresce para OESTE a partir da origem (o +X local
    /// é a subida girada 90° no sentido horário: subida para o sul, +X para
    /// oeste). Com a origem em x = 10 e 18,7 m de comprimento, a ponta oeste
    /// sai do terreno: parte das amostras é null, parte não, e a mais alta e
    /// a mais baixa vêm só das que existem.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void MesaNaBordaDoTerrenoTemAmostrasDosDoisTipos()
    {
        var geo = Geometria();
        var amostras = TerrainSampler.Sample(geo, Colocacao(10, 40), PlanoInclinado());

        Assert.False(amostras.IsComplete);
        Assert.Contains(amostras.LowEdge, m => m.GroundZ is null);
        Assert.Contains(amostras.LowEdge, m => m.GroundZ is not null);
        Assert.NotNull(amostras.HighestGround);
        Assert.True(amostras.HighestGround >= amostras.LowestGround);
    }

    /// <summary>
    /// De ponta a ponta: a célula do distribuidor, a orientação, a matriz e a
    /// amostra. A cota do pilar é a do plano no pé dele, e o pé está dentro
    /// da célula.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void DaCelulaAAmostraFechaComOPlano()
    {
        var geo = Geometria();
        var tilt = 20 * Grau;
        var fundoEmPlanta = geo.Depth * Math.Cos(tilt);

        var area = new[] { new Point3(0, 0, 0), new Point3(200, 0, 0), new Point3(200, 200, 0), new Point3(0, 200, 0) };
        var alinhamento = new[] { new Point3(10, 10, 0), new Point3(10, 190, 0) };

        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, 8, 0.5, new TableFootprint(geo.Length, fundoEmPlanta));
        var celula = layout.Rows[1].Tables[2];

        var orientacao = RowOrientation.Resolve(celula.DirectionRadians, LineSide.Right, SystemConfiguration.Default.UpslopeAzimuthRadians);
        var matriz = TablePlacement.Plan(celula, orientacao, tilt, 0);
        var amostras = TerrainSampler.Sample(geo, matriz, PlanoInclinado());

        Assert.True(amostras.IsComplete);

        foreach (var pilar in amostras.Pillars)
        {
            Assert.Equal(ZDoPlano(pilar.X, pilar.Y), pilar.GroundZ!.Value, 6);

            // Dentro da célula: entre y = 18 (fileira 2) e y = 18 + fundo.
            Assert.InRange(pilar.Y, 18 - 1e-6, 18 + fundoEmPlanta + 1e-6);
            Assert.InRange(pilar.X, celula.Origin.X - 1e-6, celula.Origin.X + geo.Length + 1e-6);
        }
    }

    /// <summary>
    /// Matriz não finita é recusada. Uma não rígida de verdade não dá para
    /// montar (o construtor de Transform é privado), e IsRigid já cobre
    /// finitude: este é o único caminho que sobra.
    /// </summary>
    [Fact]
    [Trait("Etapa", "5")]
    public void ColocacaoNaoFinitaERecusada()
    {
        var geo = Geometria();

        // Uma matriz não finita: NaN de cota.
        var quebrada = Transform.Place(0.1, 0, new Point3(0, 0, double.NaN));

        Assert.Throws<ArgumentException>(() => TerrainSampler.Sample(geo, quebrada, PlanoInclinado()));
    }
}
