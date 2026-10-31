using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O escopo das sombras (42.1, Renan, 31/10/2026: "Seleção, tudo de uma area,
/// tudo de uma area mais seleção, varias areas, desenho inteiro ... vou
/// querer apagar as sombras de algumas areas, recalcular de outras"). Três
/// áreas: a Usina (0 a 100 × 0 a 50) com a Oeste (0 a 50) e a Leste (50 a
/// 100) dentro dela; mesas de 10 × 4 nas duas metades e uma fora de tudo.
/// </summary>
public class ShadowScopeTests
{
    private static readonly Guid Usina = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Oeste = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Leste = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    private static IReadOnlyList<Point3> Retangulo(double x0, double y0, double x1, double y1, double z = 0) =>
        [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)];

    private static readonly IReadOnlyList<ShadowArea> Areas =
    [
        new(Usina, "Usina", Retangulo(0, 0, 100, 50)),
        new(Oeste, "Area Oeste", Retangulo(0, 0, 50, 50)),
        new(Leste, "Area Leste", Retangulo(50, 0, 100, 50)),
    ];

    private static ShadowTable Mesa(int n, double x, double y) => new(Guid.Parse($"00000000-0000-0000-0000-{n:D12}"), Retangulo(x, y, x + 10, y + 4, 2));

    /// <summary>Duas na Oeste, duas na Leste (uma delas com um canto na Oeste) e uma fora de tudo.</summary>
    private static readonly IReadOnlyList<ShadowTable> Mesas =
    [
        Mesa(1, 10, 10), Mesa(2, 30, 30), Mesa(3, 60, 10), Mesa(4, 47, 30), Mesa(5, 150, 10),
    ];

    private static double AreaDe(ShadowRegionPart parte) => parte.Rings.Sum(a => Assinada(a));

    private static double Assinada(IReadOnlyList<(double X, double Y)> anel)
    {
        var s = 0.0;
        for (var i = 0; i < anel.Count; i++) s += anel[i].X * anel[(i + 1) % anel.Count].Y - anel[(i + 1) % anel.Count].X * anel[i].Y;
        return s / 2;
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void UmaAreaRecebeSoAsMesasDelaPeloCentroEOChaoDela()
    {
        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Oeste], []), Areas, Mesas, 8);

        // A mesa 4 (de 47 a 57) tem o centro na Leste: não é da Oeste.
        Assert.Equal([true, true, false, false, false], plano.Receives);
        Assert.Equal([1, 1, 2, 2, null], plano.AreaOfTable);

        var parte = Assert.Single(plano.Ground);
        Assert.Equal(Oeste, parte.Area);
        Assert.Equal(50 * 50, AreaDe(parte), 3);
        Assert.Empty(plano.Tiles);
        Assert.Equal((0.0, 0.0, 50.0, 50.0), plano.Bounds);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AAreaGrandeLevaAsMenoresDentroDela()
    {
        // 42.3 (tela de 31/10/2026, "Nenhuma mesa de Area 1"): até a 42.1 a
        // Usina não levava nada (o território dela é ela menos a Oeste e a
        // Leste). Escolher a de fora é escolher tudo o que está nela.
        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Usina], []), Areas, Mesas, 8);

        Assert.Equal([true, true, true, true, false], plano.Receives);
        Assert.Equal([Oeste, Leste], plano.Ground.Select(p => p.Area!.Value).OrderBy(g => g));
        Assert.Equal(new HashSet<Guid> { Usina, Oeste, Leste }, plano.IncludedAreas.ToHashSet());
        Assert.Equal((0.0, 0.0, 100.0, 50.0), plano.Bounds);

        // Calcular a Usina troca as sombras das de dentro; a de dentro, só as dela.
        Assert.True(plano.Erases(new ShadowTag(ShadowPlace.Ground, Oeste, null, (1, 1))));
        Assert.True(plano.Erases(new ShadowTag(ShadowPlace.Table, Leste, Mesas[2].Id, null)));
        var oeste = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Oeste], []), Areas, Mesas, 8);
        Assert.Equal(new HashSet<Guid> { Oeste }, oeste.IncludedAreas.ToHashSet());
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void DuasAreasQuaseIguaisVaoUmaComAOutra()
    {
        // O Aurora: a "Area 1" e a "area1" traçadas de novo por cima, com uns
        // centímetros de diferença. As mesas contam na menor; qualquer das duas
        // escolhida leva as mesas e o chão.
        var grande = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
        var menor = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
        IReadOnlyList<ShadowArea> areas = [new(grande, "Area 1", Retangulo(-0.03, -0.02, 50.02, 50.03)), new(menor, "area1", Retangulo(0, 0, 50, 50))];
        IReadOnlyList<ShadowTable> mesas = [Mesa(1, 10, 10), Mesa(2, 30, 30)];

        foreach (var escolhida in new[] { grande, menor })
        {
            var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [escolhida], []), areas, mesas, 8);

            Assert.Equal([true, true], plano.Receives);
            Assert.Equal(new HashSet<Guid> { grande, menor }, plano.IncludedAreas.ToHashSet());
            Assert.Equal(50.05 * 50.05, plano.Ground.Sum(AreaDe), 1);
        }
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AsAreasMarcadasEAsMesasEscolhidasSomam()
    {
        // 42.3: a janela soma o que está marcado (várias áreas e mais as mesas escolhidas).
        var escopo = new ShadowScope(ShadowScopeKind.AreaAndSelection, [Oeste, Leste], [Mesas[4].Id]);
        Assert.Null(escopo.WhyInvalid());

        var plano = ShadowScopePlan.Build(escopo, Areas, Mesas, 8);
        Assert.Equal([true, true, true, true, true], plano.Receives);
        Assert.NotNull(new ShadowScope(ShadowScopeKind.AreaAndSelection, [], [Mesas[4].Id]).WhyInvalid());
        Assert.NotNull(new ShadowScope(ShadowScopeKind.Area, [Oeste, Leste], []).WhyInvalid());
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AAreaQueSoEncostaNaoVaiJunto()
    {
        // A Leste com metade fora da Oeste deslocada: não está dentro, fica de fora.
        var outra = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
        IReadOnlyList<ShadowArea> areas = [new(Oeste, "Area Oeste", Retangulo(0, 0, 50, 50)), new(outra, "Outra", Retangulo(25, 0, 75, 50))];
        IReadOnlyList<ShadowTable> mesas = [Mesa(1, 10, 10), Mesa(3, 60, 10)];

        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Oeste], []), areas, mesas, 8);

        Assert.Equal([true, false], plano.Receives);
        Assert.Equal(new HashSet<Guid> { Oeste }, plano.IncludedAreas.ToHashSet());
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void VariasAreasEODesenhoInteiro()
    {
        var duas = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Areas, [Oeste, Leste], []), Areas, Mesas, 8);
        Assert.Equal([true, true, true, true, false], duas.Receives);
        Assert.Equal([Oeste, Leste], duas.Ground.Select(p => p.Area!.Value).OrderBy(g => g));

        var tudo = ShadowScopePlan.Build(ShadowScope.WholeDrawing, Areas, Mesas, 8);
        Assert.Equal([true, true, true, true, true], tudo.Receives);

        // As duas metades e, fora das áreas, os ladrilhos em volta da mesa 5.
        Assert.Contains(tudo.Ground, p => p.Area == Oeste && Math.Abs(AreaDe(p) - 2500) < 1e-3);
        Assert.Contains(tudo.Ground, p => p.Area == Leste && Math.Abs(AreaDe(p) - 2500) < 1e-3);
        var fora = Assert.Single(tudo.Ground, p => p.Area is null);
        Assert.All(fora.Rings.SelectMany(a => a), p => Assert.True(p.X >= 100 - 1e-6, $"{p} dentro das áreas"));
        Assert.Contains((18, 1), tudo.Tiles);   // a mesa 5 (150 a 160, 10 a 14) cai no ladrilho (18, 1)
        Assert.DoesNotContain(tudo.Ground, p => p.Area == Usina);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void ASelecaoRecebeAsEscolhidasEOChaoDosLadrilhosEmVoltaRepartidoPelasAreas()
    {
        // A mesa 4 (de 47 a 57, 30 a 34) passa da Oeste para a Leste.
        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Selection, [], [Mesas[3].Id]), Areas, Mesas, 2);

        Assert.Equal([false, false, false, true, false], plano.Receives);

        // 45 a 59 e 28 a 36: ladrilhos 5 a 7 em x e 3 a 4 em y.
        Assert.Equal(6, plano.Tiles.Count);
        Assert.Contains((5, 3), plano.Tiles);
        Assert.Contains((7, 4), plano.Tiles);

        // Os ladrilhos (40 a 64, 24 a 40) repartidos: 10 m na Oeste, 14 m na Leste.
        Assert.Equal(10 * 16, AreaDe(Assert.Single(plano.Ground, p => p.Area == Oeste)), 3);
        Assert.Equal(14 * 16, AreaDe(Assert.Single(plano.Ground, p => p.Area == Leste)), 3);
        Assert.DoesNotContain(plano.Ground, p => p.Area is null || p.Area == Usina);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void AreaMaisSelecaoNaoRepeteOChaoDaArea()
    {
        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.AreaAndSelection, [Oeste], [Mesas[2].Id]), Areas, Mesas, 1);

        Assert.Equal([true, true, true, false, false], plano.Receives);
        Assert.Equal(2500, AreaDe(Assert.Single(plano.Ground, p => p.Area == Oeste)), 3);

        // A mesa 3 (60 a 70, 10 a 14) com 1 m: ladrilhos 7 a 8 em x, 1 em y, todos na Leste.
        Assert.Equal(16 * 8, AreaDe(Assert.Single(plano.Ground, p => p.Area == Leste)), 3);
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void OApagarDoEscopoLevaSoODele()
    {
        var oeste = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Oeste], []), Areas, Mesas, 8);
        Assert.True(oeste.Erases(new ShadowTag(ShadowPlace.Ground, Oeste, null, (1, 1))));
        Assert.True(oeste.Erases(new ShadowTag(ShadowPlace.Table, Oeste, Mesas[0].Id, null)));
        Assert.True(oeste.Erases(new ShadowTag(ShadowPlace.Label, Oeste, null, (2, 2))));
        Assert.False(oeste.Erases(new ShadowTag(ShadowPlace.Ground, Leste, null, (7, 1))));
        Assert.False(oeste.Erases(new ShadowTag(ShadowPlace.Ground, null, null, (20, 1))));

        var selecao = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Selection, [], [Mesas[2].Id]), Areas, Mesas, 2);
        Assert.True(selecao.Erases(new ShadowTag(ShadowPlace.Table, Leste, Mesas[2].Id, null)));
        Assert.False(selecao.Erases(new ShadowTag(ShadowPlace.Table, Leste, Mesas[3].Id, null)));
        Assert.True(selecao.Erases(new ShadowTag(ShadowPlace.Ground, Leste, null, (7, 1))));     // um ladrilho da seleção
        Assert.False(selecao.Erases(new ShadowTag(ShadowPlace.Ground, Leste, null, (11, 4))));   // outro da Leste

        var tudo = ShadowScopePlan.Build(ShadowScope.WholeDrawing, Areas, Mesas, 8);
        Assert.True(tudo.Erases(new ShadowTag(ShadowPlace.Ground, null, null, null)));
    }

    [Fact]
    [Trait("Etapa", "42")]
    public void FazemSombraOsObjetosAteAMargemEOEscopoPedeOQueFalta()
    {
        var plano = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [Oeste], []), Areas, Mesas, 8);

        Assert.True(plano.Reaches(60, 10, 70, 14, 15));     // a 10 m da Oeste
        Assert.False(plano.Reaches(150, 10, 160, 14, 15));  // a 100 m
        Assert.True(plano.Reaches(150, 10, 160, 14, 100));

        Assert.Null(new ShadowScope(ShadowScopeKind.Area, [Oeste], []).WhyInvalid());
        Assert.NotNull(new ShadowScope(ShadowScopeKind.Area, [], []).WhyInvalid());
        Assert.NotNull(new ShadowScope(ShadowScopeKind.Areas, [], []).WhyInvalid());
        Assert.NotNull(new ShadowScope(ShadowScopeKind.Selection, [], []).WhyInvalid());
        Assert.NotNull(new ShadowScope(ShadowScopeKind.AreaAndSelection, [Oeste], []).WhyInvalid());
        Assert.Null(ShadowScope.WholeDrawing.WhyInvalid());

        Assert.Equal(ShadowScopeKind.AreaAndSelection, ShadowScope.Parse("area+selecao"));
        Assert.Equal(ShadowScopeKind.Drawing, ShadowScope.Parse("TUDO"));
        Assert.Null(ShadowScope.Parse("qualquer"));
    }
}
