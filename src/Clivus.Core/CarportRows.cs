using System.Globalization;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Uma fila de carport desenhada: o que basta para montá-la de novo com o
/// terreno (35.3/35.4): o carport (pelo nome), o eixo, o lado da água e os vãos.
/// </summary>
public sealed record CarportRowRecord(Guid Id, string Carport, double FromX, double FromY, double ToX, double ToY, bool FallsRight, int Bays)
{
    /// <summary>Monta a fila de novo, com o perfil dado e o chão de agora.</summary>
    public CarportRowResult? Build(CarportProfile perfil, Func<double, double, double?> chao, out string? motivo) =>
        CarportRow.Build(perfil, FromX, FromY, ToX, ToY, FallsRight, Bays, chao, out motivo);
}

/// <summary>
/// As filas de carport do desenho, no dicionário do plugin (35.3): versão,
/// quantas, e uma linha de texto por fila, com ponto decimal invariante.
/// </summary>
public static class CarportRows
{
    /// <summary>A chave do registro no dicionário do desenho.</summary>
    public const string StorageKey = "CARPORT_FILAS";

    private const string Versao = "1";
    private const char Separador = '|';

    public static IReadOnlyList<string> Encode(IReadOnlyList<CarportRowRecord> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var campos = new List<string> { Versao, filas.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var f in filas)
        {
            if (f.Carport.Contains(Separador)) throw new ArgumentException(Tr.T("o nome do carport não pode ter a barra vertical"));
            campos.Add(string.Join(Separador, f.Id.ToString("D"), f.Carport,
                N(f.FromX), N(f.FromY), N(f.ToX), N(f.ToY), f.FallsRight ? "1" : "0", f.Bays.ToString(CultureInfo.InvariantCulture)));
        }

        return campos;
    }

    public static IReadOnlyList<CarportRowRecord> Decode(IReadOnlyList<string>? campos)
    {
        var lista = new List<CarportRowRecord>();
        if (campos is null || campos.Count < 2 || campos[0] != Versao) return lista;

        foreach (var linha in campos.Skip(2))
        {
            var p = linha.Split(Separador);
            if (p.Length != 8 || !Guid.TryParse(p[0], out var id)) continue;
            if (!D(p[2], out var x1) || !D(p[3], out var y1) || !D(p[4], out var x2) || !D(p[5], out var y2)) continue;
            if (!int.TryParse(p[7], NumberStyles.None, CultureInfo.InvariantCulture, out var vaos)) continue;
            lista.Add(new CarportRowRecord(id, p[1], x1, y1, x2, y2, p[6] == "1", vaos));
        }

        return lista;
    }

    private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    private static bool D(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && double.IsFinite(v);
}

/// <summary>
/// O carport em faces para o 3D (a página do navegador e o servidor, 36.1):
/// cada barra, bloco e módulo vira uma caixa de seis faces (doze triângulos), na cor da peça.
/// </summary>
public static class CarportMesh
{
    /// <summary>A cor do módulo no 3D (o azul dos módulos da fixa).</summary>
    public static readonly RgbColor CorDoModulo = new(30, 60, 140);

    /// <summary>A cor da base (concreto).</summary>
    public static readonly RgbColor CorDaBase = new(175, 175, 170);

    /// <summary>As faces da fila: aço na cor dada, módulos em azul com a espessura do módulo, bases em concreto.</summary>
    public static IReadOnlyList<Scene3DFace> Faces(CarportProfile perfil, CarportRowResult fila, RgbColor corDoAco)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(fila);

        var faces = new List<Scene3DFace>();
        foreach (var p in fila.Portals)
        {
            foreach (var b in p.Bars) faces.AddRange(Barra(b, corDoAco));
            foreach (var bloco in p.Blocks)
            {
                var x = Unit(new Point3(bloco.Along.X, bloco.Along.Y, 0));
                var y = new Point3(-x.Y, x.X, 0);
                var c = bloco.BottomCenter;
                var h = bloco.Size / 2;
                var baixo = Retangulo(c, x, y, h, h);
                faces.AddRange(Prisma(baixo, new Point3(0, 0, bloco.Height), bloco.Part == CarportPart.BasePlate ? corDoAco : CorDaBase));
            }
        }

        var espessura = perfil.Array.Module.Thickness;
        foreach (var v in fila.Bays)
        {
            foreach (var t in v.Purlins) faces.AddRange(Barra(t, corDoAco));
            foreach (var m in v.Modules)
            {
                // A face de cima e a de baixo, a espessura do módulo abaixo dela.
                var n = Unit(Cross(Sub(m.Corners[1], m.Corners[0]), Sub(m.Corners[3], m.Corners[0])));
                if (n.Z < 0) n = Mul(n, -1);
                var baixo = m.Corners.Select(q => Sub(q, Mul(n, espessura))).ToList();
                faces.AddRange(Prisma(baixo, Mul(n, espessura), CorDoModulo));
            }
        }

        return faces;
    }

    private static IEnumerable<Scene3DFace> Barra(CarportBar b, RgbColor cor)
    {
        var eixo = Sub(b.End, b.Start);
        var x = Unit(eixo);
        var alt = b.DepthDirection;
        var z = Sub(alt, Mul(x, Dot(alt, x)));
        z = Norma(z) < 1e-9 ? Unit(Cross(x, new Point3(0, 0, 1))) : Unit(z);
        var y = Cross(z, x);

        var hw = b.Section.Width / 2;
        var hd = b.Section.Depth / 2;
        Point3 P(double sy, double sz) => Add(b.Start, Add(Mul(y, sy * hw), Mul(z, sz * hd)));
        List<Point3> secao = [P(-1, -1), P(1, -1), P(1, 1), P(-1, 1)];
        return Prisma(secao, eixo, cor);
    }

    // As faces de um prisma de base quadrada, em triângulos (a página 3D
    // desenha a lista das canaletas como triângulos): base, tampa e lados.
    private static IEnumerable<Scene3DFace> Prisma(IReadOnlyList<Point3> base_, Point3 vetor, RgbColor cor)
    {
        var tampa = base_.Select(p => Add(p, vetor)).ToList();
        IEnumerable<Scene3DFace> Quad(Point3 a, Point3 b, Point3 c, Point3 d) =>
            [new Scene3DFace([a, b, c], cor), new Scene3DFace([a, c, d], cor)];

        foreach (var f in Quad(base_[3], base_[2], base_[1], base_[0])) yield return f;
        foreach (var f in Quad(tampa[0], tampa[1], tampa[2], tampa[3])) yield return f;
        for (var i = 0; i < base_.Count; i++)
        {
            var j = (i + 1) % base_.Count;
            foreach (var f in Quad(base_[i], base_[j], tampa[j], tampa[i])) yield return f;
        }
    }

    private static List<Point3> Retangulo(Point3 c, Point3 x, Point3 y, double hx, double hy) =>
    [
        Add(c, Add(Mul(x, -hx), Mul(y, -hy))), Add(c, Add(Mul(x, hx), Mul(y, -hy))),
        Add(c, Add(Mul(x, hx), Mul(y, hy))), Add(c, Add(Mul(x, -hx), Mul(y, hy))),
    ];

    private static Point3 Add(Point3 a, Point3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Point3 Sub(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Point3 Mul(Point3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Point3 Cross(Point3 a, Point3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static double Norma(Point3 a) => Math.Sqrt(Dot(a, a));
    private static Point3 Unit(Point3 a) => Mul(a, 1 / Norma(a));
}

/// <summary>
/// O carport no Exportar para o TermoVision (36.1): cada vão vai como uma
/// mesa do contrato, com os quatro cantos da face de cima (1→2 a borda baixa
/// no sentido da fila, 3→4 a alta voltando), colunas e fiadas do vão. O id é
/// sempre o mesmo para o mesmo vão da mesma fila: o reenvio troca, não soma.
/// </summary>
public static class CarportReturn
{
    public static IEnumerable<TermoVisionTable> Tables(Guid fila, string nome, CarportProfile perfil, CarportRowResult montada)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(montada);
        if (montada.Portals.Count < 2) yield break;

        var a0 = montada.Portals[0].Center;
        var a1 = montada.Portals[^1].Center;
        double Ao(Point3 p) => (p.X - a0.X) * (a1.X - a0.X) + (p.Y - a0.Y) * (a1.Y - a0.Y);

        var arranjo = perfil.Array;
        for (var v = 0; v < montada.Bays.Count; v++)
        {
            var mods = montada.Bays[v].Modules;
            var baixos = mods.Where(m => m.Row == 0).SelectMany(m => new[] { m.Corners[0], m.Corners[1] }).ToList();
            var altos = mods.Where(m => m.Row == arranjo.ModulesDown - 1).SelectMany(m => new[] { m.Corners[2], m.Corners[3] }).ToList();
            if (baixos.Count == 0 || altos.Count == 0) continue;

            var cantos = new[] { baixos.MinBy(Ao), baixos.MaxBy(Ao), altos.MaxBy(Ao), altos.MinBy(Ao) }
                .Select(p => (p.X, p.Y, p.Z)).ToList();

            yield return new TermoVisionTable(
                IdDoVao(fila, v), Tr.F("{0} vão {1}", nome, v + 1), cantos, arranjo.ModulesAcross, arranjo.ModulesDown,
                new TermoVisionModule(arranjo.ModuleAcross, arranjo.ModuleDown, arranjo.GapAcross));
        }
    }

    private static Guid IdDoVao(Guid fila, int vao)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(fila.ToString("D") + "/" + vao.ToString(CultureInfo.InvariantCulture)));
        return new Guid(bytes);
    }
}
