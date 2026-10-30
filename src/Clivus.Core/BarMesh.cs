using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Uma barra de estrutura (perfil de seção retangular) em triângulos, para o
/// 3D: o prisma do eixo, na largura e na altura da seção, com a altura para
/// onde a peça diz. A página 3D e o servidor desenham as faces extras como
/// triângulos (a lista das canaletas).
///
/// É a mesma malha para o carport e a bifila (revisão do 39.1: o 3D da bifila
/// explodia cada sólido do desenho, o que numa usina de 500 mesas eram 13 mil
/// sólidos explodidos; e o carport já tinha esta conta no Core).
/// </summary>
public static class BarMesh
{
    /// <summary>Os triângulos da barra: 12 com as tampas das pontas, 8 sem elas.</summary>
    /// <param name="start">Uma ponta do eixo.</param>
    /// <param name="end">A outra ponta.</param>
    /// <param name="width">A largura da seção.</param>
    /// <param name="depth">A altura da seção.</param>
    /// <param name="depthDirection">Para onde a altura aponta (a parte ao longo do eixo é descartada).</param>
    /// <param name="color">A cor das faces.</param>
    /// <param name="caps">
    /// Com as tampas. Sem elas a barra vista de lado é a mesma, com um terço a
    /// menos de triângulos: é o que a bifila manda, peça de 1 a 18 m vista de
    /// longe.
    /// </param>
    public static IReadOnlyList<Scene3DFace> Box(Point3 start, Point3 end, double width, double depth, Point3 depthDirection, RgbColor color, bool caps = true)
    {
        var eixo = Sub(end, start);
        var x = Unit(eixo);
        var z = Sub(depthDirection, Mul(x, Dot(depthDirection, x)));
        z = Norma(z) < 1e-9 ? Unit(Cross(x, new Point3(0, 0, 1))) : Unit(z);
        var y = Cross(z, x);

        var hw = width / 2;
        var hd = depth / 2;
        Point3 P(double sy, double sz) => Add(start, Add(Mul(y, sy * hw), Mul(z, sz * hd)));
        List<Point3> secao = [P(-1, -1), P(1, -1), P(1, 1), P(-1, 1)];

        return Prism(secao, eixo, color, caps);
    }

    /// <summary>
    /// As faces de um prisma de base quadrada, em triângulos: base, tampa (com
    /// <paramref name="caps"/>) e lados, nesta ordem.
    /// </summary>
    public static IReadOnlyList<Scene3DFace> Prism(IReadOnlyList<Point3> base_, Point3 vetor, RgbColor cor, bool caps = true)
    {
        ArgumentNullException.ThrowIfNull(base_);

        var faces = new List<Scene3DFace>();
        var tampa = base_.Select(p => Add(p, vetor)).ToList();

        void Quad(Point3 a, Point3 b, Point3 c, Point3 d)
        {
            faces.Add(new Scene3DFace([a, b, c], cor));
            faces.Add(new Scene3DFace([a, c, d], cor));
        }

        if (caps)
        {
            Quad(base_[3], base_[2], base_[1], base_[0]);
            Quad(tampa[0], tampa[1], tampa[2], tampa[3]);
        }

        for (var i = 0; i < base_.Count; i++)
        {
            var j = (i + 1) % base_.Count;
            Quad(base_[i], base_[j], tampa[j], tampa[i]);
        }

        return faces;
    }

    private static Point3 Add(Point3 a, Point3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Point3 Sub(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Point3 Mul(Point3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Point3 Cross(Point3 a, Point3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static double Norma(Point3 a) => Math.Sqrt(Dot(a, a));
    private static Point3 Unit(Point3 a) { var n = Norma(a); return n > 0 ? Mul(a, 1 / n) : a; }
}
