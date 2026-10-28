using System.Globalization;
using Clivus.Geo;

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

    [Fact]
    [Trait("Etapa", "36")]
    public void NoRetornoOCarportVaiComTipoEEstrutura()
    {
        var p = CarportProfile.Reference;
        var fila = CarportRow.Build(p, 0, 0, 1, 0, true, 2, (_, _) => 0, out _)!;
        var mesas = CarportReturn.Tables(Guid.NewGuid(), "C", p, fila).ToList();

        // A soma das estruturas dos vãos é a fila inteira, sem pórtico repetido.
        Assert.Equal(CarportMesh.Faces(p, fila, CarportMesh.CorDoAco).Count, mesas.Sum(m => m.Structure!.Count));
        Assert.All(mesas, m => Assert.Equal(TermoVisionTable.TipoCarport, m.Kind));

        var json = System.Text.Json.JsonDocument.Parse(TermoVisionReturn.Build("t", "d", mesas, out _)).RootElement.GetProperty("mesas");
        Assert.Equal("carport", json[0].GetProperty("tipo").GetString());
        var tri = json[0].GetProperty("estrutura").GetProperty("triangulos");
        Assert.Equal(mesas[0].Structure!.Count, tri.GetArrayLength());
        Assert.Equal(10, tri[0].GetArrayLength());
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void AMesaFixaVaiComTipoFixaESemEstrutura()
    {
        var mesa = new TermoVisionTable(Guid.NewGuid(), "F1.1", [(0, 0, 0), (10, 0, 0), (10, 4, 1), (0, 4, 1)], 14, 2, new TermoVisionModule(1.3, 2.4, 0.02));
        var json = System.Text.Json.JsonDocument.Parse(TermoVisionReturn.Build("t", "d", [mesa], out _)).RootElement.GetProperty("mesas")[0];

        Assert.Equal("fixa", json.GetProperty("tipo").GetString());
        Assert.False(json.TryGetProperty("estrutura", out _));
    }

    // ---------------------------------------------------------- o Usina com carport

    private static Point3 P(double x, double y) => new(x, y, 0);

    [Fact]
    [Trait("Etapa", "36")]
    public void OUsinaEncheAAreaComFilasDeCarport()
    {
        var p = CarportProfile.Reference;
        Point3[] area = [P(0, 0), P(60, 0), P(60, 40), P(0, 40)];
        Point3[] alinhamento = [P(0, 0), P(0, 40)];

        // Mesa olhando para o norte: subida para o sul, fileiras leste-oeste a partir da borda oeste.
        var layout = RowDistributor.Distribute(area, alinhamento, LineSide.Right, CarportLayout.Pitch(p), 0, CarportLayout.Footprint(p), Math.PI);
        var filas = CarportLayout.Rows(layout, p.Name, "A1");

        Assert.NotEmpty(filas);
        Assert.Equal(layout.Rows.Count, filas.Count);                    // vãos colados: uma fila por fileira
        Assert.Equal(layout.Tables.Count, filas.Sum(f => f.Bays));
        Assert.All(filas, f => Assert.Equal("A1", f.Area));

        foreach (var f in filas)
        {
            var montada = f.Build(p, (_, _) => 0, out var motivo);
            Assert.True(montada is not null, motivo);
            Assert.Equal(f.Bays, montada!.Bays.Count);

            // Tudo dentro da área, e a água cai para o norte (a borda baixa é a mais ao norte).
            var cantos = montada.Bays.SelectMany(v => v.Modules).SelectMany(m => m.Corners).ToList();
            Assert.All(cantos, c => Assert.InRange(c.X, -0.01, 60.01));
            Assert.All(cantos, c => Assert.InRange(c.Y, -0.01, 40.01));
            var baixa = montada.Bays[0].Modules.Where(m => m.Row == 0).SelectMany(m => m.Corners.Take(2)).Average(c => c.Y);
            var alta = montada.Bays[0].Modules.Where(m => m.Row == p.Array.ModulesDown - 1).SelectMany(m => m.Corners.Skip(2)).Average(c => c.Y);
            Assert.True(baixa > alta, "borda baixa ao norte");
        }
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void OPitchNuncaSobrepoeAsFilas()
    {
        var p = CarportProfile.Reference;
        // O corredor do carport (padrão 6 m) entre as coberturas; sem corredor, coladas mas sem sobrepor.
        Assert.Equal(CarportLayout.Footprint(p).PlanDepth + 6, CarportLayout.Pitch(p), 9);
        Assert.Equal(CarportLayout.Footprint(p).PlanDepth + CarportLayout.FolgaMinima, CarportLayout.Pitch(p with { Aisle = 0 }), 9);
    }

    [Fact]
    [Trait("Etapa", "36")]
    public void AFilaDaAreaGuardaAArea()
    {
        var f = new CarportRowRecord(Guid.NewGuid(), "C", 1, 2, 3, 4, true, 2) { Area = "heliponto" };
        Assert.Equal("heliponto", CarportRows.Decode(CarportRows.Encode([f]))[0].Area);
    }
}
