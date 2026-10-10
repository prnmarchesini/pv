using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// O local dos inversores (Renan, 10/10/2026): numa área desenhada (sala,
/// skid) ou automático, ao lado da vala, no ponto de menor cabo CC.
/// </summary>
public class InverterSitesTests
{
    private static bool Dentro(IReadOnlyList<Point3> poligono, Point3 centro, double w, double l) =>
        EquipmentFootprint.Corners(centro.X, centro.Y, w, l).All(c => Polygons.Contains(poligono, c.X, c.Y));

    private static bool SeSobrepoem(Point3 a, Point3 b, double w, double l) =>
        Math.Abs(a.X - b.X) < w - 1e-9 && Math.Abs(a.Y - b.Y) < l - 1e-9;

    [Fact]
    [Trait("Etapa", "14")]
    public void InversoresCabemNaAreaSemSobrepor()
    {
        Point3[] sala = [new(0, 0, 0), new(10, 0, 0), new(10, 4, 0), new(0, 4, 0)];

        var centros = InverterSites.InArea(sala, Enumerable.Repeat((1.1, 0.7), 6).ToList());

        Assert.All(centros, c => Assert.NotNull(c));
        Assert.All(centros, c => Assert.True(Dentro(sala, c!.Value, 1.1, 0.7)));
        for (var i = 0; i < centros.Count; i++)
            for (var j = i + 1; j < centros.Count; j++)
                Assert.False(SeSobrepoem(centros[i]!.Value, centros[j]!.Value, 1.1 + InverterSites.Gap, 0.7));
    }

    /// <summary>Área girada (45°) e em L: todos dentro; o que não coube volta null.</summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void AreaGiradaEmLEOQueNaoCabe()
    {
        var c = Math.Sqrt(0.5);
        Point3 R(double x, double y) => new(1000 + x * c - y * c, 2000 + x * c + y * c, 0);
        Point3[] girada = [R(0, 0), R(20, 0), R(20, 5), R(0, 5)];
        var g = InverterSites.InArea(girada, Enumerable.Repeat((1.0, 1.0), 4).ToList());
        Assert.All(g, p => Assert.True(p is { } x && Dentro(girada, x, 1, 1)));

        Point3[] emL = [new(0, 0, 0), new(12, 0, 0), new(12, 3, 0), new(3, 3, 0), new(3, 12, 0), new(0, 12, 0)];
        var l = InverterSites.InArea(emL, Enumerable.Repeat((1.0, 1.0), 8).ToList());
        Assert.All(l.Where(p => p is not null), p => Assert.True(Dentro(emL, p!.Value, 1, 1)));
        Assert.True(l.Count(p => p is not null) >= 6);

        Point3[] pequena = [new(0, 0, 0), new(3, 0, 0), new(3, 2, 0), new(0, 2, 0)];
        var p = InverterSites.InArea(pequena, Enumerable.Repeat((1.0, 1.0), 5).ToList());
        Assert.Equal(2, p.Count(x => x is not null));
        Assert.Null(p[^1]);
    }

    /// <summary>
    /// Vala reta de x = 0 a 100: strings que batem nela em 10, 20 e 90 (5 m de
    /// saída cada). O menor cabo é na mediana, x = 20.
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void PontoDeMenorCaboEAMedianaDasBatidas()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)]]);
        TrenchPoint Em(double x) => rede.Nearest(new Point3(x, 0, 0), 1)!.Value;
        InverterSites.StringAccess Uma(double x) => new([[(Em(x), 5.0), (Em(x), 5.0)]]);

        var melhor = InverterSites.BestTrenchPoint(rede, [Uma(10), Uma(20), Uma(90)]);

        Assert.NotNull(melhor);
        Assert.Equal(20, melhor!.Value.Point.At.X, 6);
        Assert.Equal(2 * (5 + 10 + 5 + 0 + 5 + 70) / 1.0, melhor.Value.Total, 6);
    }

    /// <summary>Cada string vai pelo lado que der menos cabo até o ponto (como o router, 18.2).</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void CadaStringUsaOLadoMaisCurto()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)]]);
        TrenchPoint Em(double x) => rede.Nearest(new Point3(x, 0, 0), 1)!.Value;

        // Três strings em x = 80 só por um lado; uma que pode sair em 0 (saída longa) ou em 100 (curta).
        InverterSites.StringAccess Fixa = new([[(Em(80), 1.0), (Em(80), 1.0)]]);
        var dupla = new InverterSites.StringAccess([[(Em(0), 1.0), (Em(0), 1.0)], [(Em(100), 1.0), (Em(100), 1.0)]]);

        var melhor = InverterSites.BestTrenchPoint(rede, [Fixa, Fixa, Fixa, dupla]);

        Assert.Equal(80, melhor!.Value.Point.At.X, 6);
        Assert.Equal(3 * 2 + 2 * (1 + 20), melhor.Value.Total, 6);
    }

    [Fact]
    [Trait("Etapa", "18")]
    public void SemBatidaNaoHaPonto()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)]]);
        Assert.Null(InverterSites.BestTrenchPoint(rede, [new InverterSites.StringAccess([])]));
    }

    /// <summary>Ao lado da vala, afastado meia caixa mais a folga; do outro lado se o primeiro está ocupado.</summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void AoLadoDaValaDoLadoLivre()
    {
        Point3[] direcao = [new(1, 0, 0)];

        var (livre, ok) = InverterSites.BesideTrench(new Point3(50, 0, 0), direcao, 1.1, 0.7, _ => true);
        Assert.True(ok);
        Assert.Equal(50, livre.X, 6);
        Assert.Equal(0.35 + InverterSites.Gap, livre.Y, 6);

        var (outro, ok2) = InverterSites.BesideTrench(new Point3(50, 0, 0), direcao, 1.1, 0.7, cantos => cantos.All(c => c.Y < 0));
        Assert.True(ok2);
        Assert.Equal(-(0.35 + InverterSites.Gap), outro.Y, 6);

        // Nenhum lado livre: devolve o primeiro e diz que não estava livre (quem chama avisa).
        var (_, ok3) = InverterSites.BesideTrench(new Point3(50, 0, 0), direcao, 1.1, 0.7, _ => false);
        Assert.False(ok3);
    }

    /// <summary>
    /// Num cruzamento em T (a principal em X e um ramal subindo em +Y), o
    /// lado normal à principal cai em cima do ramal: o inversor vai para um
    /// lugar sem vala (revisão de 10/10/2026).
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void NoCruzamentoNaoFicaEmCimaDoRamal()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)], [new(50, 0, 0), new(50, 40, 0)]]);
        var no = rede.Nearest(new Point3(50, 0, 0), 0.01)!.Value;

        var (centro, livre) = InverterSites.BesideTrench(no.At, rede.DirectionsAt(no), 1.1, 0.7,
            cantos => !rede.Touches(cantos.Select(c => new Point3(c.X, c.Y, 0)).ToList()));

        Assert.True(livre);
        Assert.False(rede.Touches(EquipmentFootprint.Corners(centro.X, centro.Y, 1.1, 0.7).Select(c => new Point3(c.X, c.Y, 0)).ToList()));
        Assert.True(rede.DirectionsAt(no).Count >= 2);
    }

    /// <summary>
    /// Strings em valas que não se ligam: vale o ponto que alcança mais delas
    /// (antes devolvia nada e o inversor não era posto).
    /// </summary>
    [Fact]
    [Trait("Etapa", "18")]
    public void ValasSeparadasFicaComAQueAlcancaMais()
    {
        var rede = new TrenchNetwork([[new(0, 0, 0), new(100, 0, 0)], [new(0, 50, 0), new(100, 50, 0)]]);
        TrenchPoint Em(double x, double y) => rede.Nearest(new Point3(x, y, 0), 1)!.Value;
        InverterSites.StringAccess Uma(double x, double y) => new([[(Em(x, y), 1.0), (Em(x, y), 1.0)]]);

        var melhor = InverterSites.BestTrenchPoint(rede, [Uma(10, 0), Uma(20, 0), Uma(30, 0), Uma(50, 50)]);

        Assert.NotNull(melhor);
        Assert.Equal(3, melhor!.Value.Reached);
        Assert.Equal(0, melhor.Value.Point.At.Y, 6);
        Assert.Equal(20, melhor.Value.Point.At.X, 6);
    }

    /// <summary>
    /// Área côncava com um dente estreito entrando: a caixa não pode
    /// atravessar o dente (os 4 cantos dentro não bastam); e a vaga ocupada
    /// (um inversor que já está na área) fica de fora.
    /// </summary>
    [Fact]
    [Trait("Etapa", "14")]
    public void DenteDaAreaEVagaOcupada()
    {
        // Retângulo 10 x 2 com um dente de 0,2 m de largura descendo do topo até y = 0,5, em x = 2: cai entre os cantos de uma vaga.
        Point3[] comDente = [new(0, 0, 0), new(10, 0, 0), new(10, 2, 0), new(2.1, 2, 0), new(2.1, 0.5, 0), new(1.9, 0.5, 0), new(1.9, 2, 0), new(0, 2, 0)];
        var centros = InverterSites.InArea(comDente, Enumerable.Repeat((1.0, 1.0), 3).ToList());
        Assert.All(centros.Where(c => c is not null), c => Assert.True(InverterSites.Dentro(comDente, EquipmentFootprint.Corners(c!.Value.X, c.Value.Y, 1, 1))));
        Assert.All(centros, c => Assert.NotNull(c));

        Point3[] sala = [new(0, 0, 0), new(10, 0, 0), new(10, 4, 0), new(0, 4, 0)];
        var primeiro = InverterSites.InArea(sala, [(1.0, 1.0)])[0]!.Value;
        var ocupado = EquipmentFootprint.Corners(primeiro.X, primeiro.Y, 1, 1).Select(c => new Point3(c.X, c.Y, 0)).ToList();
        var segundo = InverterSites.InArea(sala, [(1.0, 1.0)], livre: cantos => !InverterSites.Overlaps(cantos, ocupado))[0]!.Value;
        Assert.False(InverterSites.Overlaps(EquipmentFootprint.Corners(segundo.X, segundo.Y, 1, 1), ocupado));
    }

    [Fact]
    [Trait("Etapa", "14")]
    public void LocalGravaELeEATrocaSoOsEscolhidos()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var area = Guid.NewGuid();
        var lista = InverterPlacement.With([], [a, b], InverterPlacementMode.Area, area);

        Assert.All(lista, p => Assert.Equal(p, InverterPlacement.Parse(p.ToFields())));
        Assert.Null(InverterPlacement.Parse(new InverterPlacement(a, InverterPlacementMode.Area, Guid.Empty).ToFields()));

        var depois = InverterPlacement.With(lista, [b], InverterPlacementMode.Automatic);
        Assert.Equal(InverterPlacementMode.Area, depois.Single(p => p.Inverter == a).Mode);
        Assert.Equal(new InverterPlacement(b, InverterPlacementMode.Automatic, Guid.Empty), depois.Single(p => p.Inverter == b));
        Assert.Single(InverterPlacement.With(depois, [b], null));

        var marca = new SiteMark(area, "Área 1");
        Assert.Equal(marca, SiteMark.Parse(marca.ToFields()));
    }
}
