using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// Correções de 10/10/2026, à noite (revisão da segunda rodada): o
/// automático nunca dentro de uma área; a escolha de automático guardada; a
/// cópia de área com GUID e nome próprios; área dentro de área (a menor); a
/// caixa da área sem encostar em vala.
/// </summary>
public class InverterSitesRevisaoTests
{
    private static readonly RouteSettings Config = RouteSettings.Default(CableRoute.DirectCurrent);

    private static InverterSites.StringAccess Uma(TrenchNetwork rede, double x) =>
        new([[(rede.Nearest(new Point3(x, 0, 0), 1)!.Value, 5.0), (rede.Nearest(new Point3(x, 0, 0), 1)!.Value, 5.0)]]);

    /// <summary>
    /// O melhor ponto (x = 50) tem uma sala dos dois lados da vala: o
    /// automático vai ao ponto seguinte da lista, fora dela, e diz que saiu do melhor.
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void AutomaticoNaoCaiDentroDeUmaArea()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)]]);
        var strings = new[] { Uma(rede, 40), Uma(rede, 50), Uma(rede, 50), Uma(rede, 60) };
        Point3[] sala = [new(45, -5, 0), new(55, -5, 0), new(55, 5, 0), new(45, 5, 0)];

        // Sem a área: o ponto de menor cabo, em x = 50 (como antes).
        var sem = InverterSites.AutomaticSite(rede, strings, 1.1, 0.7, _ => true, [], out var alcanca);
        Assert.True(alcanca);
        Assert.NotNull(sem);
        Assert.False(sem!.Value.Moved);
        Assert.Equal(50, sem.Value.Center.X, 6);

        var com = InverterSites.AutomaticSite(rede, strings, 1.1, 0.7, _ => true, [sala], out alcanca);
        Assert.True(alcanca);
        Assert.NotNull(com);
        Assert.True(com!.Value.Moved);
        var cantos = EquipmentFootprint.Corners(com.Value.Center.X, com.Value.Center.Y, 1.1, 0.7);
        Assert.False(InverterSites.Overlaps(cantos, sala));
        Assert.True(com.Value.Total > sem.Value.Total);

        // Nem como "lugar sem folga": com tudo ocupado (livre falso), ainda fora da sala.
        var ocupado = InverterSites.AutomaticSite(rede, strings, 1.1, 0.7, _ => false, [sala], out _);
        Assert.NotNull(ocupado);
        Assert.False(ocupado!.Value.Free);
        Assert.False(InverterSites.Overlaps(EquipmentFootprint.Corners(ocupado.Value.Center.X, ocupado.Value.Center.Y, 1.1, 0.7), sala));
    }

    /// <summary>Toda a vala dentro de uma área: o automático não é posto (null), mas as strings alcançam a rede.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ValaTodaDentroDeAreaNaoPoe()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)]]);
        Point3[] grande = [new(-10, -10, 0), new(110, -10, 0), new(110, 10, 0), new(-10, 10, 0)];

        Assert.Null(InverterSites.AutomaticSite(rede, [Uma(rede, 50)], 1.1, 0.7, _ => true, [grande], out var alcanca));
        Assert.True(alcanca);
        Assert.Null(InverterSites.AutomaticSite(rede, [new InverterSites.StringAccess([])], 1.1, 0.7, _ => true, [], out alcanca));
        Assert.False(alcanca);
    }

    /// <summary>O lugar proibido não volta nem como o primeiro lugar sem folga; o BesideTrench de sempre não mudou.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void AoLadoDaValaComLugarProibido()
    {
        Point3[] direcao = [new(1, 0, 0)];
        var acima = InverterSites.BesideTrench(new Point3(50, 0, 0), direcao, 1.1, 0.7, _ => false, cantos => cantos.All(c => c.Y > 0));
        Assert.NotNull(acima);
        Assert.False(acima!.Value.Free);
        Assert.True(acima.Value.Center.Y < 0);

        Assert.Null(InverterSites.BesideTrench(new Point3(50, 0, 0), direcao, 1.1, 0.7, _ => true, _ => true));
        Assert.Equal((new Point3(50, 0, 0), false), InverterSites.BesideTrench(new Point3(50, 0, 0), [], 1.1, 0.7, _ => true));
    }

    /// <summary>
    /// O automático levado com o MOVE para dentro de uma área aparece como da
    /// área, mas a escolha de automático fica gravada; quem foi mudado de
    /// propósito (Escolher área) fica com a área.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void EscolhaDeAutomaticoFicaGravada()
    {
        var auto = Guid.NewGuid();
        var mao = Guid.NewGuid();
        var area = Guid.NewGuid();
        Point3[] sala = [new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0)];
        var gravado = new List<InverterPlacement> { new(auto, InverterPlacementMode.Automatic, Guid.Empty) };

        // Dentro da sala: a geometria diz área (tabela e rota); gravado continua automático.
        var dentro = InverterSites.Reconcile(gravado, [auto, mao], new Dictionary<Guid, (double, double)> { [auto] = (5, 5), [mao] = (5, 6) }, [(area, sala)]);
        Assert.Equal(InverterPlacementMode.Area, dentro.Single(p => p.Inverter == auto).Mode);
        var paraGravar = InverterSites.ForRecord(gravado, dentro);
        Assert.Equal(InverterPlacementMode.Automatic, paraGravar.Single(p => p.Inverter == auto).Mode);
        Assert.Equal(new InverterPlacement(mao, InverterPlacementMode.Area, area), paraGravar.Single(p => p.Inverter == mao));

        // Tirado da sala (depois de gravar): volta a ser automático, não "à mão".
        var fora = InverterSites.Reconcile(paraGravar, [auto, mao], new Dictionary<Guid, (double, double)> { [auto] = (50, 5) }, [(area, sala)]);
        Assert.Equal(new InverterPlacement(auto, InverterPlacementMode.Automatic, Guid.Empty), fora.Single(p => p.Inverter == auto));

        // Escolher área de propósito: fica da área.
        var escolhido = InverterSites.ForRecord(gravado, InverterPlacement.With(dentro, [auto], InverterPlacementMode.Area, area), [auto]);
        Assert.Equal(InverterPlacementMode.Area, escolhido.Single(p => p.Inverter == auto).Mode);
    }

    /// <summary>
    /// COPY da polilinha da área: a de menor handle fica com o GUID; a cópia
    /// ganha GUID e nome próprios, os mesmos a cada leitura. Nenhuma é descartada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void CopiaDeAreaViraOutraArea()
    {
        var id = Guid.NewGuid();
        var outra = new SiteMark(Guid.NewGuid(), "Área 2");
        (long, SiteMark)[] lidas = [(300, new SiteMark(id, "Área 1")), (120, new SiteMark(id, "Área 1")), (200, outra)];

        var efetivas = SiteMark.ResolveCopies(lidas);

        Assert.Equal(3, efetivas.Count);
        Assert.Equal(new SiteMark(id, "Área 1"), efetivas[1]);
        Assert.Equal(outra, efetivas[2]);
        Assert.NotEqual(id, efetivas[0].Id);
        Assert.Equal(SiteMark.CopyId(id, 300), efetivas[0].Id);
        Assert.Equal("Área 3", efetivas[0].Name);
        Assert.Equal(efetivas, SiteMark.ResolveCopies(lidas));
        Assert.Equal(3, efetivas.Select(e => e.Id).Distinct().Count());
        Assert.Equal(3, efetivas.Select(e => e.Name).Distinct().Count());

        // Sem repetição, nada muda.
        Assert.Equal([outra], SiteMark.ResolveCopies([(5, outra)]));
    }

    /// <summary>Área dentro de área: o contorno de acesso da rota é o da de dentro (a menor), como a tabela.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void AreaDentroDeAreaValeAMenor()
    {
        Point3[] fora = [new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), new(0, 100, 0)];
        Point3[] dentro = [new(40, 40, 0), new(60, 40, 0), new(60, 60, 0), new(40, 60, 0)];
        var p = new Point3(50, 50, 0);

        Assert.Same(dentro, CableRouter.AccessOutline(p, null, [fora, dentro]));
        Assert.Same(dentro, InverterSites.SmallestAt(50, 50, [fora, dentro]));
        Assert.Same(fora, InverterSites.SmallestAt(10, 10, [fora, dentro]));
        Assert.Null(InverterSites.SmallestAt(200, 10, [fora, dentro]));
        Assert.Equal(400, InverterSites.PlanArea(dentro), 6);
    }

    /// <summary>A caixa posta na área não encosta na vala que passa por ela (a grade e a vaga livre).</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void CaixaDaAreaNaoEncostaNaVala()
    {
        Point3[] sala = [new(0, 0, 0), new(10, 0, 0), new(10, 4, 0), new(0, 4, 0)];
        var rede = new TrenchNetwork([[new(5, -10, 0), new(5, 10, 0)]]);
        bool Livre(IReadOnlyList<(double X, double Y)> cantos) => !rede.Touches([.. cantos.Select(c => new Point3(c.X, c.Y, 0))]);

        var cheia = InverterSites.Fill(sala, Enumerable.Repeat((1.1, 0.7), 6).ToList(), [], Livre);

        Assert.Contains(cheia.Centers, c => c is not null);
        foreach (var c in cheia.Centers.Where(c => c is not null))
            Assert.True(Livre(EquipmentFootprint.Corners(c!.Value.X, c.Value.Y, 1.1, 0.7)));

        // Sem o livre, alguma caixa cairia em cima da vala (o teste prova alguma coisa).
        var semVala = InverterSites.Fill(sala, Enumerable.Repeat((1.1, 0.7), 6).ToList(), []);
        Assert.Contains(semVala.Centers, c => c is not null && !Livre(EquipmentFootprint.Corners(c.Value.X, c.Value.Y, 1.1, 0.7)));
    }
}
