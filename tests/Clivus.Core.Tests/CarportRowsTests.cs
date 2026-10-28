using System.Globalization;

namespace Clivus.Core.Tests;

/// <summary>O registro das filas desenhadas e a malha 3D do carport (35.3, 36.1).</summary>
public class CarportRowsTests
{
    [Fact]
    [Trait("Etapa", "35")]
    public void AsFilasVoltamIguais()
    {
        IReadOnlyList<CarportRowRecord> filas =
        [
            new(Guid.NewGuid(), "Carport biposte", 711560.123456789, 7664200.5, 711600.25, 7664210.75, true, 3),
            new(Guid.NewGuid(), "Outro", -1.5, 2.25, 3, 4, false, 1),
        ];

        Assert.Equal(filas, CarportRows.Decode(CarportRows.Encode(filas)));
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void ONumeroNaoDependeDaCultura()
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            var f = new CarportRowRecord(Guid.NewGuid(), "A", 1.5, 2.5, 3.5, 4.5, true, 2);
            Assert.Equal(f, CarportRows.Decode(CarportRows.Encode([f]))[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void LinhaQuebradaFicaDeFora()
    {
        Assert.Empty(CarportRows.Decode(["1", "1", "lixo"]));
        Assert.Empty(CarportRows.Decode(["9", "0"]));
        Assert.Empty(CarportRows.Decode(null));
    }

    [Fact]
    [Trait("Etapa", "35")]
    public void ARegistradaMontaAMesmaFila()
    {
        var p = CarportProfile.Reference;
        var registro = new CarportRowRecord(Guid.NewGuid(), p.Name, 0, 0, 30, 0, true, 3);

        var fila = registro.Build(p, (_, _) => 0, out var motivo);

        Assert.True(fila is not null, motivo);
        Assert.Equal(4, fila!.Portals.Count);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void AMalhaTemDozeTriangulosPorPeca()
    {
        var p = CarportProfile.Reference;
        var fila = CarportRow.Build(p, 0, 0, 1, 0, true, 2, (_, _) => 0, out _)!;

        var faces = CarportMesh.Faces(p, fila, new RgbColor(150, 160, 170));

        var barras = fila.Portals.Sum(x => x.Bars.Count) + fila.Bays.Sum(v => v.Purlins.Count);
        var blocos = fila.Portals.Sum(x => x.Blocks.Count);
        var modulos = fila.Bays.Sum(v => v.Modules.Count);
        Assert.Equal(12 * (barras + blocos + modulos), faces.Count);
        Assert.All(faces, f => Assert.Equal(3, f.Corners.Count));
        Assert.Equal(12 * modulos, faces.Count(f => f.Color == CarportMesh.CorDoModulo));
        Assert.All(faces, f => Assert.All(f.Corners, c => Assert.True(c.IsFinite)));
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void OModuloNoTresDeTemAEspessuraAbaixoDaFaceDeCima()
    {
        var p = CarportProfile.Reference;
        var fila = CarportRow.Build(p, 0, 0, 1, 0, true, 1, (_, _) => 0, out _)!;
        var cima = fila.Bays[0].Modules[0].Corners;

        var faces = CarportMesh.Faces(p, fila, new RgbColor(1, 2, 3)).Where(f => f.Color == CarportMesh.CorDoModulo).Take(12).ToList();
        var zs = faces.SelectMany(f => f.Corners).Select(c => c.Z).ToList();

        Assert.Equal(cima.Max(c => c.Z), zs.Max(), 6);
        Assert.True(zs.Min() < cima.Min(c => c.Z) - 0.02);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void CadaVaoVaiAoTermoVisionComoMesa()
    {
        var p = CarportProfile.Reference;
        var fila = CarportRow.Build(p, 100, 200, 101, 200, false, 3, (x, _) => 0.02 * x, out _)!;
        var id = Guid.NewGuid();

        var mesas = CarportReturn.Tables(id, "Carport", p, fila).ToList();

        Assert.Equal(3, mesas.Count);
        Assert.All(mesas, m =>
        {
            Assert.Null(TermoVisionReturn.WhyInvalid(m));
            Assert.Equal(5, m.Columns);
            Assert.Equal(3, m.Rows);
            // Borda baixa primeiro, no sentido da fila (+X), e a alta voltando.
            Assert.True(m.Corners[1].E > m.Corners[0].E);
            Assert.True(m.Corners[3].E < m.Corners[2].E);
            Assert.True((m.Corners[2].Z + m.Corners[3].Z) / 2 > (m.Corners[0].Z + m.Corners[1].Z) / 2);
        });
        Assert.Equal(p.Array.BayWidth, mesas[0].Corners[1].E - mesas[0].Corners[0].E, 3);

        // O mesmo vão da mesma fila sai sempre com o mesmo id.
        Assert.Equal(mesas.Select(m => m.Id), CarportReturn.Tables(id, "Carport", p, fila).Select(m => m.Id));
        Assert.Equal(3, mesas.Select(m => m.Id).Distinct().Count());
    }
}
