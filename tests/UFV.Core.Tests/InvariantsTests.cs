using UFV.Core.Invariants;
using UFV.Geo;

namespace UFV.Core.Tests;

/// <summary>
/// Os verificadores das regras sagradas.
///
/// O plano é explícito: "resultado que fere qualquer uma destas regras é BUG,
/// não caso de análise. Cada regra vira um verificador automático em
/// UFV.Core.Invariants, rodado sobre toda saída do motor em todo teste."
///
/// Estes testes não testam a mesa: testam o verificador. Um verificador que
/// aprova tudo é pior que verificador nenhum, porque dá confiança.
/// </summary>
public class InvariantsTests
{
    private static SolarModule Risen() =>
        new("Risen", "RSM132-8-720BHDG", 720, 2.384, 1.303, 0.033);

    private static TableLayout Mesa() =>
        new(Risen(), 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10);

    private static TableGeometry Geometria() =>
        TableGeometry.Local(Mesa(), PillarTable.Distribute(Mesa().Length, 3), new TableFrame(3.00, 2.50, 0.15, 0.07, 3.00, 0));

    // ------------------------------------------- regra 2: mesa é monolito

    [Fact]
    [Trait("Etapa", "3")]
    public void AMesaDeitadaEMonolito()
    {
        Assert.Null(RigidTable.Check(Geometria()));
    }

    /// <summary>
    /// E continua sendo depois de colocada. É para isto que existe a matriz
    /// única: transformação rígida leva plano em plano, sempre.
    /// </summary>
    [Theory]
    [Trait("Etapa", "3")]
    [InlineData(0, 0)]
    [InlineData(0.35, 1.1)]
    [InlineData(-0.2, 4.9)]
    [InlineData(1.2, 0)]
    public void AMesaColocadaContinuaMonolito(double tilt, double azimute)
    {
        var mundo = Geometria().Transformed(Transform.Place(tilt, azimute, new Point3(500_000, 7_400_000, 812)));

        Assert.Null(RigidTable.Check(mundo));
    }

    /// <summary>
    /// O teste que dá valor ao verificador: uma mesa entortada de propósito
    /// tem que ser recusada. Dois milímetros — o dobro da tolerância — é o
    /// tipo de defeito que nenhum olho pega no desenho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void UmaMesaEntortadaERecusada()
    {
        var torta = Entortar(Geometria(), 0.002);
        var motivo = RigidTable.Check(torta);

        Assert.NotNull(motivo);
        Assert.Contains("plano", motivo!);
    }

    /// <summary>
    /// Um décimo de milímetro é ruído de arredondamento, não mesa torta. O
    /// verificador que reprovasse isso seria desligado na primeira semana.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void RuidoDeArredondamentoNaoEMesaTorta()
    {
        Assert.Null(RigidTable.Check(Entortar(Geometria(), 0.0001)));
    }

    /// <summary>
    /// A tolerância é de um milímetro, como manda a arquitetura. Este teste
    /// existe para ela não ser afrouxada sem alguém reparar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void ATolerânciaEDeUmMilimetro()
    {
        Assert.Null(RigidTable.Check(Entortar(Geometria(), 0.0009)));
        Assert.NotNull(RigidTable.Check(Entortar(Geometria(), 0.0011)));
    }

    /// <summary>
    /// O apoio do pilar conta tanto quanto o módulo: a regra fala em "todos os
    /// módulos e todos os topos de pilar". Um pilar fora do plano é mesa que
    /// não assenta, e seria o defeito mais fácil de deixar passar.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void UmApoioDePilarForaDoPlanoERecusado()
    {
        var geo = Geometria();

        var pilares = geo.Pillars
            .Select((p, i) => i == 3
                ? new PillarPiece(p.Station, p.Anchor with { Z = p.Anchor.Z + 0.005 }, p.Footprint)
                : p)
            .ToList();

        Assert.NotNull(RigidTable.Check(Remontar(geo, geo.Modules, pilares)));
    }

    [Fact]
    [Trait("Etapa", "3")]
    public void GeometriaVaziaOuNulaNaoExplode()
    {
        Assert.NotNull(RigidTable.Check(null!));
    }

    /// <summary>
    /// Coordenada não finita nunca pode ser aprovada em silêncio: a conta do
    /// plano daria NaN, e comparação com NaN é sempre falsa — o verificador
    /// aprovaria a mesa exatamente quando ela está mais quebrada.
    /// </summary>
    [Fact]
    [Trait("Etapa", "3")]
    public void CoordenadaNaoFinitaERecusada()
    {
        var geo = Geometria();

        var modulos = geo.Modules
            .Select((m, i) => i == 0
                ? new ModulePiece(m.Column, m.Row,
                    [m.TopFace[0] with { Z = double.NaN }, m.TopFace[1], m.TopFace[2], m.TopFace[3]],
                    m.Solid)
                : m)
            .ToList();

        Assert.NotNull(RigidTable.Check(Remontar(geo, modulos, geo.Pillars)));
    }

    // ------------------------------------------------------------ apoio

    /// <summary>Levanta um canto de um módulo, deixando a mesa torta.</summary>
    private static TableGeometry Entortar(TableGeometry geo, double quanto)
    {
        var modulos = geo.Modules
            .Select((m, i) => i == geo.Modules.Count - 1
                ? new ModulePiece(m.Column, m.Row,
                    [m.TopFace[0], m.TopFace[1], m.TopFace[2] with { Z = m.TopFace[2].Z + quanto }, m.TopFace[3]],
                    m.Solid)
                : m)
            .ToList();

        return Remontar(geo, modulos, geo.Pillars);
    }

    /// <summary>
    /// Remonta uma geometria com peças trocadas.
    ///
    /// TableGeometry não tem construtor público de propósito — geometria só
    /// nasce de dado validado. Para testar o verificador é preciso produzir
    /// mesa quebrada, e este é o único lugar onde isso é legítimo.
    /// </summary>
    private static TableGeometry Remontar(
        TableGeometry original,
        IReadOnlyList<ModulePiece> modulos,
        IReadOnlyList<PillarPiece> pilares) =>
        TableGeometry.ForTesting(modulos, pilares, original.RafterOffset, original.PillarRow);
}
