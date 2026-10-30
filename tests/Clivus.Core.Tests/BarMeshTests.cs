using Clivus.Geo;

namespace Clivus.Core.Tests;

/// <summary>
/// A barra em triângulos para o 3D (passo 39.1): a mesma malha do carport e da
/// bifila, montada no Core pelo eixo e pela seção da peça.
/// </summary>
public class BarMeshTests
{
    private static readonly RgbColor Aco = CarportMesh.CorDoAco;

    [Fact]
    [Trait("Etapa", "39")]
    public void ComTampasSao12TriangulosESemSao8()
    {
        var a = new Point3(0, 0, 0);
        var b = new Point3(4.03, 0, 0.7);

        Assert.Equal(12, BarMesh.Box(a, b, 0.05, 0.08, new Point3(0, 0, 1), Aco).Count);
        Assert.Equal(8, BarMesh.Box(a, b, 0.05, 0.08, new Point3(0, 0, 1), Aco, caps: false).Count);
        Assert.All(BarMesh.Box(a, b, 0.05, 0.08, new Point3(0, 0, 1), Aco), f => Assert.Equal(3, f.Corners.Count));
    }

    /// <summary>
    /// Todo canto da malha fica a meia diagonal da seção do eixo, e entre as
    /// duas pontas: é a caixa do sólido do desenho.
    /// </summary>
    [Fact]
    [Trait("Etapa", "39")]
    public void OsCantosFicamNaCaixaDaPeca()
    {
        var a = new Point3(10, 20, 700);
        var b = new Point3(11, 22, 701);
        var (dx, dy, dz) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var meiaDiagonal = Math.Sqrt(0.05 * 0.05 + 0.08 * 0.08) / 2;

        foreach (var p in BarMesh.Box(a, b, 0.05, 0.08, new Point3(0, 0, 1), Aco).SelectMany(f => f.Corners))
        {
            var (px, py, pz) = (p.X - a.X, p.Y - a.Y, p.Z - a.Z);
            var t = (px * dx + py * dy + pz * dz) / l;
            var (qx, qy, qz) = (px - t * dx / l, py - t * dy / l, pz - t * dz / l);

            Assert.InRange(t, -1e-9, l + 1e-9);
            Assert.Equal(meiaDiagonal, Math.Sqrt(qx * qx + qy * qy + qz * qz), 9);
        }
    }

    /// <summary>A peça da bifila no 3D: a barra de cada peça montada sai com o comprimento dela.</summary>
    [Fact]
    [Trait("Etapa", "39")]
    public void APecaDaBifilaViraABarraDela()
    {
        var viga = new StructureBar(StructurePart.Beam, 0, PillarLine.Single, new Point3(0, 0, 0), new Point3(0, 3.97, 0.7), new BarSection(0.05, 0.08), new Point3(0, -0.17, 0.98));

        var faces = BarMesh.Box(viga.Start, viga.End, viga.Section.Width, viga.Section.Depth, viga.DepthDirection, Aco, caps: false);
        var ys = faces.SelectMany(f => f.Corners).Select(p => p.Y).ToList();

        Assert.Equal(8, faces.Count);
        Assert.True(ys.Max() - ys.Min() >= 3.97 - 1e-9);
    }
}
