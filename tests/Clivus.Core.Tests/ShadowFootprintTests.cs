using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A sombra desenhável de todos os objetos (05/10/2026, Renan: "ao gerar
/// sombras, quero que gere as sombras das mesas também, o motor de sombras
/// tem que gerar as sombras de todos objetos do desenho"), e o cancelar do
/// cálculo do período ("um botão de cancelar a qualquer momento").
/// </summary>
public class ShadowFootprintTests
{
    private static SunPosition SolAoNorte(double elevacao) => new(0, elevacao, 0, 0);

    private static double? Plano(double x, double y) => 0;

    /// <summary>Uma mesa inclinada para o norte: borda baixa em y0 na cota z0, sobe 2 m em 2 m de fundo (a de <see cref="ShadingModelTests"/>).</summary>
    private static IReadOnlyList<Point3> Mesa(double y0, double z0) =>
        [new(0, y0, z0), new(10, y0, z0), new(10, y0 - 2, z0 + 2), new(0, y0 - 2, z0 + 2)];

    [Fact]
    [Trait("Etapa", "9")]
    public void AMesaHorizontalFazSombraNoChaoDeslocadaParaLongeDoSol()
    {
        // Mesa plana de 10 x 2 a 2 m do chão; sol ao norte a 45°: a sombra é
        // o mesmo retângulo, 2 m para o sul.
        IReadOnlyList<Point3> mesa = [new(0, 0, 2), new(10, 0, 2), new(10, 2, 2), new(0, 2, 2)];
        var mancha = new ShadowFootprint([], [mesa], Plano);

        mancha.Add(SolAoNorte(45));

        var anel = Assert.Single(mancha.Ground());
        Assert.Equal(20, ShadowUnion.Area(anel), 1);
        Assert.Equal(-2, anel.Min(p => p.Y), 2);
        Assert.Equal(0, anel.Max(p => p.Y), 2);
        Assert.Equal(1, mancha.Steps);
        Assert.Empty(mancha.ShadedTables);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void ASombraDaMesaNoChaoAcompanhaOTerreno()
    {
        // O chão sobe 1 m para o sul a cada 10 m... aqui um degrau: ao sul de
        // y = -1 o chão está a 1 m. A sombra da mesa a 2 m cai mais perto.
        IReadOnlyList<Point3> mesa = [new(0, 0, 2), new(10, 0, 2), new(10, 2, 2), new(0, 2, 2)];
        double? Degrau(double x, double y) => y < -0.5 ? 1 : 0;

        var mancha = new ShadowFootprint([], [mesa], Degrau);
        mancha.Add(SolAoNorte(45));

        var anel = Assert.Single(mancha.Ground());
        Assert.Equal(-1, anel.Min(p => p.Y), 2);   // 1 m de queda até o chão alto, não 2
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AFileiraDaFrenteFazSombraNaDeTrasComOSolBaixoENaoNelaMesma()
    {
        var frente = Mesa(0, 0.5);
        var tras = Mesa(-4, 0.5);

        var baixo = new ShadowFootprint([], [frente, tras], Plano);
        baixo.Add(SolAoNorte(10));

        Assert.Equal([1], baixo.ShadedTables);
        var anel = Assert.Single(baixo.OnTable(1));
        Assert.True(ShadowUnion.Area(anel) > 5, $"área {ShadowUnion.Area(anel)}");
        Assert.Empty(baixo.OnTable(0));
        Assert.NotEmpty(baixo.Ground());

        // Sol alto: a sombra da da frente cai no vão, não na de trás.
        var alto = new ShadowFootprint([], [frente, tras], Plano);
        alto.Add(SolAoNorte(60));
        Assert.Empty(alto.ShadedTables);

        // Sem as mesas fazendo sombra, não há mancha nenhuma.
        var semMesas = new ShadowFootprint([], [frente, tras], Plano, tablesCastShadow: false);
        semMesas.Add(SolAoNorte(10));
        Assert.Empty(semMesas.Ground());
        Assert.Empty(semMesas.ShadedTables);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AArvoreContinuaFazendoSombraNoChaoENaMesa()
    {
        // Árvore alta ao norte da mesa: com o sol ao norte, a sombra cobre a mesa e o chão.
        var mesa = Mesa(0, 0.5);
        var arvore = new ShadowCylinder(5, 4, 2, 0, 12);

        var mancha = new ShadowFootprint([arvore], [mesa], Plano, tablesCastShadow: false);
        mancha.Add(SolAoNorte(40));

        Assert.NotEmpty(mancha.Ground());
        Assert.Equal([0], mancha.ShadedTables);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OsPassosSeJuntamNumaManchaSo()
    {
        IReadOnlyList<Point3> mesa = [new(0, 0, 2), new(10, 0, 2), new(10, 2, 2), new(0, 2, 2)];
        var mancha = new ShadowFootprint([], [mesa], Plano);

        mancha.Add(new SunPosition(-20, 45, 0, 0));
        mancha.Add(new SunPosition(0, 45, 0, 0));
        mancha.Add(new SunPosition(20, 45, 0, 0));
        mancha.Add(new SunPosition(0, 1, 0, 0));   // sol posto: não conta

        var anel = Assert.Single(mancha.Ground());
        Assert.Equal(3, mancha.Steps);
        Assert.True(ShadowUnion.Area(anel) > 20.5, $"área {ShadowUnion.Area(anel)}");
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OAcumuladorDaOMesmoQueAUniaoDeUmaVezEOSentidoNaoAbreBuraco()
    {
        IReadOnlyList<(double X, double Y)> Quadrado(double x, double y, double lado, bool horario = false)
        {
            IReadOnlyList<(double X, double Y)> q = [(x, y), (x + lado, y), (x + lado, y + lado), (x, y + lado)];
            return horario ? q.Reverse().ToList() : q;
        }

        var quadrados = Enumerable.Range(0, 40).Select(i => Quadrado(i * 0.7 % 9, i * 1.3 % 7, 1.5, horario: i % 2 == 1)).ToList();

        // Fila pequena: junta várias vezes no caminho.
        var acumulado = new ShadowAccumulator(maxPending: 3);
        foreach (var q in quadrados) acumulado.Add(q);

        var deUmaVez = ShadowUnion.Union(quadrados.Select(Normalizar));
        Assert.Equal(deUmaVez.Sum(ShadowUnion.Area), acumulado.Rings().Sum(ShadowUnion.Area), 2);

        // Dois quadrados iguais em sentidos opostos não se anulam.
        var dois = new ShadowAccumulator();
        dois.Add(Quadrado(0, 0, 2));
        dois.Add(Quadrado(0, 0, 2, horario: true));
        Assert.Equal(4, ShadowUnion.Area(Assert.Single(dois.Rings())), 3);

        static List<(double X, double Y)> Normalizar(IReadOnlyList<(double X, double Y)> q)
        {
            var soma = 0.0;
            for (var i = 0; i < q.Count; i++) soma += q[i].X * q[(i + 1) % q.Count].Y - q[(i + 1) % q.Count].X * q[i].Y;
            return soma < 0 ? q.Reverse().ToList() : q.ToList();
        }
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void AMesaSoFazSombraComAParteAcimaDoPlanoDaOutra()
    {
        // A que recebe é horizontal a 1 m; a que faz sombra cruza o plano dela
        // (metade abaixo, metade acima): só a metade de cima conta.
        IReadOnlyList<Point3> recebe = [new(-20, -20, 1), new(20, -20, 1), new(20, 20, 1), new(-20, 20, 1)];
        IReadOnlyList<Point3> faz = [new(0, 0, 0), new(4, 0, 0), new(4, 4, 2), new(0, 4, 2)];

        var sombra = Shading.PolygonShadowOnPlane(faz, Sol(90).Direction, recebe);

        // Sol a pino: a sombra é a planta da metade de cima (y de 2 a 4).
        Assert.Equal(4 * 2, ShadowUnion.Area(sombra.Select(p => (p.X, p.Y)).ToList()), 3);
        Assert.All(sombra, p => Assert.Equal(1, p.Z, 6));
        Assert.Empty(Shading.PolygonShadowOnPlane(faz, Sol(1).Direction, recebe));

        static SunPosition Sol(double elevacao) => new(0, elevacao, 0, 0);
    }

    [Fact]
    [Trait("Etapa", "9")]
    public void OPeriodoParaQuandoOUsuarioCancela()
    {
        var modelo = new ShadingModel([new ShadowQuad(Mesa(0, 0.5), 0), new ShadowQuad(Mesa(-4, 0.5), 1)], []);
        var instantes = Shading.Instants(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), new TimeOnly(9, 0), new TimeOnly(15, 0), TimeSpan.FromHours(1));

        using var cancelar = new CancellationTokenSource();
        var vistos = new List<DateTime>();

        Assert.Throws<OperationCanceledException>(() => modelo.Worst(-23.5, -46.6, -3, instantes, (t, _) =>
        {
            vistos.Add(t);
            if (vistos.Count == 10) cancelar.Cancel();
        }, cancelar.Token));

        // Parou no passo do clique, não no fim do ano.
        Assert.Equal(10, vistos.Count);

        // Sem cancelar, o andamento vê todos os instantes, em ordem.
        var todos = new List<DateTime>();
        var pior = modelo.Worst(-23.5, -46.6, -3, instantes.Take(14), (t, _) => todos.Add(t));
        Assert.Equal(14, todos.Count);
        Assert.Equal(14, pior.Instants);
        Assert.Equal(instantes.Take(14), todos);
    }
}
