using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// Uma barra de estrutura como sólido: a caixa do comprimento do eixo, na
/// largura e na altura da seção, com a altura apontando para onde a peça diz.
/// É o mesmo sólido para as peças do carport e as da bifila (revisão do 38.2:
/// as duas tinham a sua cópia).
/// </summary>
internal static class SolidoDeBarra
{
    /// <summary>A caixa, ou null quando o eixo não tem comprimento.</summary>
    /// <param name="de">Uma ponta do eixo.</param>
    /// <param name="ate">A outra ponta.</param>
    /// <param name="largura">A largura da seção.</param>
    /// <param name="altura">A altura da seção.</param>
    /// <param name="direcaoDaAltura">Para onde a altura da seção aponta (a parte ao longo do eixo é descartada).</param>
    internal static Solid3d? Caixa(Point3 de, Point3 ate, double largura, double altura, Point3 direcaoDaAltura)
    {
        var inicio = new Point3d(de.X, de.Y, de.Z);
        var fim = new Point3d(ate.X, ate.Y, ate.Z);
        var eixo = fim - inicio;
        if (eixo.Length < 1e-6) return null;

        var x = eixo.GetNormal();
        var z = new Vector3d(direcaoDaAltura.X, direcaoDaAltura.Y, direcaoDaAltura.Z);
        z -= x * z.DotProduct(x);
        if (z.Length < 1e-9) z = x.GetPerpendicularVector();
        z = z.GetNormal();
        var y = z.CrossProduct(x);

        var solido = new Solid3d();

        try
        {
            solido.CreateBox(eixo.Length, largura, altura);
            solido.TransformBy(Matrix3d.AlignCoordinateSystem(
                Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis, inicio + eixo / 2, x, y, z));
        }
        catch
        {
            solido.Dispose();
            throw;
        }

        return solido;
    }
}
