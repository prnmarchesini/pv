using Clipper2Lib;
using Clivus.Geo;

namespace Clivus.Core;

/// <summary>
/// Um pedaço da sombra no chão, pronto para virar hachura: os contornos (em
/// planta) e o plano inclinado em que ele fica, ajustado ao terreno debaixo
/// dele e um pouco acima.
/// </summary>
/// <param name="Loops">Os contornos (em planta), externos e furos; cada um fechado implicitamente.</param>
/// <param name="OriginX">O x da origem do plano (o meio do pedaço).</param>
/// <param name="OriginY">O y da origem do plano.</param>
/// <param name="Z0">A cota do plano na origem.</param>
/// <param name="SlopeX">Quanto o plano sobe por metro em x.</param>
/// <param name="SlopeY">Quanto o plano sobe por metro em y.</param>
/// <param name="TileX">A coluna do ladrilho de 8 m (<see cref="GroundPatches.TileSize"/>) a que o pedaço pertence.</param>
/// <param name="TileY">A linha do ladrilho de 8 m.</param>
/// <param name="Size">O lado do quadrado em que o pedaço foi ajustado (8 m, ou menos se precisou dividir).</param>
/// <param name="MinClearance">A menor folga do plano acima do terreno nos pontos conferidos (m).</param>
/// <param name="MaxClearance">A maior folga do plano acima do terreno nos pontos conferidos (m).</param>
public sealed record GroundPatch(
    IReadOnlyList<IReadOnlyList<(double X, double Y)>> Loops,
    double OriginX, double OriginY, double Z0, double SlopeX, double SlopeY,
    int TileX, int TileY, double Size, double MinClearance, double MaxClearance)
{
    /// <summary>A cota do plano em (x, y).</summary>
    public double HeightAt(double x, double y) => Z0 + SlopeX * (x - OriginX) + SlopeY * (y - OriginY);

    /// <summary>A normal do plano, unitária, para cima.</summary>
    public (double X, double Y, double Z) Normal
    {
        get
        {
            var n = Math.Sqrt(SlopeX * SlopeX + SlopeY * SlopeY + 1);
            return (-SlopeX / n, -SlopeY / n, 1 / n);
        }
    }
}

/// <summary>
/// A sombra no chão em pedaços que acompanham o terreno (42.1, regra
/// sagrada 5: tudo que o plugin desenha acompanha o terreno). A hachura é
/// plana, e o terreno não: a mancha é cortada nos ladrilhos de 8 m de uma
/// grade fixa do desenho, e cada pedaço ganha o plano que melhor passa pelo
/// terreno debaixo dele (mínimos quadrados), levantado até a folga
/// (<see cref="Clearance"/>) no ponto mais alto do terreno. Se o terreno do
/// pedaço não cabe num plano (entre a menor e a maior folga mais que
/// <see cref="Spread"/>), o pedaço é dividido em quatro, até 1 m.
/// <para>
/// Os pontos conferidos: uma grade de 9 × 9 na caixa do pedaço, os vértices
/// do contorno, o contorno a cada oitavo do lado, e os vértices dos
/// triângulos do TIN que esses pontos tocam (o terreno é plano dentro do
/// triângulo: o ponto mais alto dele contra o plano está num vértice).
/// </para>
/// <para>
/// A grade de 8 m é fixa no desenho (múltiplos de 8 m nas coordenadas): o
/// mesmo ladrilho em dois cálculos é o mesmo, e o escopo da seleção apaga e
/// redesenha ladrilhos inteiros.
/// </para>
/// </summary>
public static class GroundPatches
{
    /// <summary>O lado do ladrilho, em metro.</summary>
    public const double TileSize = 8;

    /// <summary>A folga mínima do pedaço acima do terreno (4 cm).</summary>
    public const double Clearance = 0.04;

    /// <summary>Quanto a folga pode variar dentro de um pedaço antes de dividi-lo (5 cm: a hachura fica entre 4 e 9 cm do chão).</summary>
    public const double Spread = 0.05;

    /// <summary>O menor lado de um pedaço dividido.</summary>
    public const double MinSize = 1;

    /// <summary>O ladrilho de 8 m do ponto.</summary>
    public static (int X, int Y) TileOf(double x, double y) => ((int)Math.Floor(x / TileSize), (int)Math.Floor(y / TileSize));

    /// <summary>
    /// A mancha em pedaços com o plano de cada um. Pedaço sem terreno
    /// debaixo (nenhum ponto conferido no TIN) fica de fora.
    /// </summary>
    /// <param name="rings">A mancha (em planta; furos ao contrário dos externos, como a Clipper devolve).</param>
    /// <param name="terrain">O terreno.</param>
    /// <param name="clearance">A folga mínima acima do terreno.</param>
    /// <param name="spread">Quanto a folga pode variar dentro do pedaço.</param>
    /// <param name="minSize">O menor lado de um pedaço dividido.</param>
    public static IReadOnlyList<GroundPatch> Build(
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> rings, Tin terrain,
        double clearance = Clearance, double spread = Spread, double minSize = MinSize)
    {
        ArgumentNullException.ThrowIfNull(rings);
        ArgumentNullException.ThrowIfNull(terrain);

        var mancha = ShadowPaths.Paths(rings);
        var pedacos = new List<GroundPatch>();
        if (ShadowPaths.Bounds(mancha) is not { } caixa) return pedacos;

        var (tx0, ty0) = TileOf(caixa.MinX, caixa.MinY);
        var (tx1, ty1) = TileOf(caixa.MaxX, caixa.MaxY);

        // Primeiro em faixas de 8 m, depois cada faixa em ladrilhos: cada
        // corte olha só o pedaço da mancha que está na faixa.
        for (var ty = ty0; ty <= ty1; ty++)
        {
            var faixa = Clipper.RectClip(Retangulo(caixa.MinX - 1, ty * TileSize, caixa.MaxX + 1, (ty + 1) * TileSize), mancha);
            if (faixa.Count == 0) continue;

            for (var tx = tx0; tx <= tx1; tx++)
            {
                var x0 = tx * TileSize;
                var y0 = ty * TileSize;
                var ladrilho = Clipper.RectClip(Retangulo(x0, y0, x0 + TileSize, y0 + TileSize), faixa);
                Ajustar(ladrilho, x0, y0, TileSize, tx, ty, terrain, clearance, spread, minSize, pedacos);
            }
        }

        return pedacos;
    }

    /// <summary>
    /// A borda da mancha (os anéis como linhas) cortada nos ladrilhos de 8 m:
    /// cada trecho com o ladrilho dele, para o escopo da seleção apagar e
    /// redesenhar a borda junto com a hachura do ladrilho. Os cortes dos
    /// ladrilhos não viram borda: só o contorno da mancha.
    /// </summary>
    public static IReadOnlyList<(int TileX, int TileY, IReadOnlyList<(double X, double Y)> Line)> Borders(IReadOnlyList<IReadOnlyList<(double X, double Y)>> rings)
    {
        ArgumentNullException.ThrowIfNull(rings);

        // O anel vira linha aberta que volta ao começo.
        var linhas = new Paths64();
        foreach (var anel in rings)
        {
            var caminho = ShadowPaths.Path(anel);
            if (caminho.Count < 3) continue;
            caminho.Add(caminho[0]);
            linhas.Add(caminho);
        }

        var trechos = new List<(int, int, IReadOnlyList<(double X, double Y)>)>();
        if (ShadowPaths.Bounds(linhas) is not { } caixa) return trechos;

        var (tx0, ty0) = TileOf(caixa.MinX, caixa.MinY);
        var (tx1, ty1) = TileOf(caixa.MaxX, caixa.MaxY);

        for (var ty = ty0; ty <= ty1; ty++)
        {
            var faixa = Clipper.RectClipLines(Retangulo(caixa.MinX - 1, ty * TileSize, caixa.MaxX + 1, (ty + 1) * TileSize), linhas);
            if (faixa.Count == 0) continue;

            for (var tx = tx0; tx <= tx1; tx++)
            {
                foreach (var trecho in Clipper.RectClipLines(Retangulo(tx * TileSize, ty * TileSize, (tx + 1) * TileSize, (ty + 1) * TileSize), faixa))
                {
                    if (trecho.Count < 2) continue;
                    trechos.Add((tx, ty, trecho.Select(p => (p.X / ShadowPaths.Scale, p.Y / ShadowPaths.Scale)).ToList()));
                }
            }
        }

        return trechos;
    }

    private static void Ajustar(
        Paths64 pedaco, double x0, double y0, double lado, int tx, int ty, Tin terreno,
        double folga, double variacao, double menor, List<GroundPatch> saida)
    {
        pedaco = new Paths64(pedaco.Where(c => c.Count >= 3 && Math.Abs(Clipper.Area(c)) > 1));
        if (pedaco.Count == 0 || Math.Abs(Clipper.Area(pedaco)) < 2500) return;   // menos de 0,0025 m²: nada a hachurar

        var pontos = Pontos(pedaco, lado, terreno);
        if (pontos.Count == 0) return;

        var (ox, oy) = (x0 + lado / 2, y0 + lado / 2);
        var (a, b, c) = Plano(pontos, ox, oy);

        var (rMin, rMax) = (double.PositiveInfinity, double.NegativeInfinity);
        foreach (var (x, y, z) in pontos)
        {
            var r = z - (a + b * (x - ox) + c * (y - oy));
            (rMin, rMax) = (Math.Min(rMin, r), Math.Max(rMax, r));
        }

        if (rMax - rMin > variacao && lado / 2 >= menor - 1e-9)
        {
            var meio = lado / 2;
            for (var j = 0; j < 2; j++)
            {
                for (var i = 0; i < 2; i++)
                {
                    var (qx, qy) = (x0 + i * meio, y0 + j * meio);
                    Ajustar(Clipper.RectClip(Retangulo(qx, qy, qx + meio, qy + meio), pedaco), qx, qy, meio, tx, ty, terreno, folga, variacao, menor, saida);
                }
            }

            return;
        }

        saida.Add(new GroundPatch(
            ShadowPaths.Rings(pedaco), ox, oy, a + rMax + folga, b, c, tx, ty, lado, folga, folga + (rMax - rMin)));
    }

    /// <summary>Os pontos do terreno que o plano do pedaço precisa respeitar.</summary>
    private static List<(double X, double Y, double Z)> Pontos(Paths64 pedaco, double lado, Tin terreno)
    {
        var pontos = new List<(double X, double Y, double Z)>();
        var vertices = new HashSet<int>();
        var r = Clipper.GetBounds(pedaco);
        var (x0, y0, x1, y1) = (r.left / ShadowPaths.Scale, r.top / ShadowPaths.Scale, r.right / ShadowPaths.Scale, r.bottom / ShadowPaths.Scale);
        var passo = lado / 8;

        void Ponto(double x, double y)
        {
            if (!terreno.TryFindTriangle(x, y, out var t) || !terreno.TryGetZ(x, y, out var z)) return;
            pontos.Add((x, y, z));
            vertices.Add(t);
        }

        // A grade na caixa do pedaço (as pontas incluídas).
        var nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / passo));
        var ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / passo));
        for (var j = 0; j <= ny; j++)
            for (var i = 0; i <= nx; i++)
                Ponto(x0 + (x1 - x0) * i / nx, y0 + (y1 - y0) * j / ny);

        // O contorno, a cada oitavo do lado.
        foreach (var caminho in pedaco)
        {
            for (var k = 0; k < caminho.Count; k++)
            {
                var (p, q) = (caminho[k], caminho[(k + 1) % caminho.Count]);
                var (px, py, qx, qy) = (p.X / ShadowPaths.Scale, p.Y / ShadowPaths.Scale, q.X / ShadowPaths.Scale, q.Y / ShadowPaths.Scale);
                var partes = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((qx - px) * (qx - px) + (qy - py) * (qy - py)) / passo));
                for (var s = 0; s < partes; s++) Ponto(px + (qx - px) * s / partes, py + (qy - py) * s / partes);
            }
        }

        // Os vértices dos triângulos tocados que caem na caixa: o terreno é
        // plano dentro do triângulo, o mais alto dele contra o plano é um vértice.
        foreach (var t in vertices)
        {
            var tri = terreno.Triangles[t];
            foreach (var v in new[] { tri.A, tri.B, tri.C })
                if (v.X >= x0 && v.X <= x1 && v.Y >= y0 && v.Y <= y1 && v.IsFinite) pontos.Add((v.X, v.Y, v.Z));
        }

        return pontos;
    }

    /// <summary>O plano z = a + b·(x − ox) + c·(y − oy) de mínimos quadrados; com menos de três pontos fora de uma reta, horizontal na média.</summary>
    internal static (double A, double B, double C) Plano(IReadOnlyList<(double X, double Y, double Z)> pontos, double ox, double oy)
    {
        double n = 0, sx = 0, sy = 0, sz = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0;

        foreach (var (x, y, z) in pontos)
        {
            var (dx, dy) = (x - ox, y - oy);
            n++;
            sx += dx;
            sy += dy;
            sz += z;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
            sxz += dx * z;
            syz += dy * z;
        }

        if (n == 0) return (0, 0, 0);

        // As equações normais, por Cramer.
        var det = n * (sxx * syy - sxy * sxy) - sx * (sx * syy - sxy * sy) + sy * (sx * sxy - sxx * sy);
        if (Math.Abs(det) < 1e-9 * Math.Max(1, n * n * n)) return (sz / n, 0, 0);

        var da = sz * (sxx * syy - sxy * sxy) - sx * (sxz * syy - sxy * syz) + sy * (sxz * sxy - sxx * syz);
        var db = n * (sxz * syy - syz * sxy) - sz * (sx * syy - sxy * sy) + sy * (sx * syz - sxz * sy);
        var dc = n * (sxx * syz - sxy * sxz) - sx * (sx * syz - sxz * sy) + sz * (sx * sxy - sxx * sy);

        return (da / det, db / det, dc / det);
    }

    private static Rect64 Retangulo(double x0, double y0, double x1, double y1) =>
        new((long)Math.Round(x0 * ShadowPaths.Scale), (long)Math.Round(y0 * ShadowPaths.Scale), (long)Math.Round(x1 * ShadowPaths.Scale), (long)Math.Round(y1 * ShadowPaths.Scale));
}
