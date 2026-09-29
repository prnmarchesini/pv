using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>A PB presa numa ponta da mesa: de que vizinha veio e quanto é.</summary>
/// <param name="Label">O letreiro da vizinha.</param>
/// <param name="Clearance">A PB do pilar da ponta dela que encosta nesta mesa.</param>
internal sealed record PontaPresa(string Label, double Clearance);

/// <summary>
/// As pontas das vizinhas de uma mesa, para o Recalcular não abrir a junta
/// (regra sagrada 6, 29/09/2026): a mesa recalculada sozinha tem as pontas
/// presas na PB das mesas que encostam nela na mesma fileira, e a corrente
/// escolhe só o que sobra.
///
/// Vizinha é a mesa paralela, na mesma linha (desvio lateral menor que
/// meio fundo), com vão em planta até o que quebra a fileira. A PB dela é
/// lida como ela está desenhada: as cotas da borda baixa do contorno, os
/// pilares medidos no terreno.
/// </summary>
internal static class PontasVizinhas
{
    private const double Paralela = 5 * Math.PI / 180;

    /// <summary>
    /// As PBs presas: no primeiro pilar (menor estação local) e no último,
    /// ou null onde não há vizinha.
    /// </summary>
    internal static (PontaPresa? Primeira, PontaPresa? Ultima) Ler(
        Database database, Tin terreno, Guid guid, PlacedTable celula,
        IReadOnlyDictionary<Guid, TableParts> todas, TableGeometry geometria, double tilt, ProjectSettings settings)
    {
        var config = settings.Configuration;
        var minha = Pontas(celula, geometria, tilt, config);

        PontaPresa? primeira = null, ultima = null;
        double distPrimeira = double.PositiveInfinity, distUltima = double.PositiveInfinity;

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var (outra, partes) in todas)
        {
            if (outra == guid || partes.Identity is null || partes.IsDuplicated || partes.Contour is not { } contorno) continue;
            if (contorno.IsErased || transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;

            var cantos = FileiraCommands.Vertices(polilinha, transacao).ToList();
            if (cantos.Count < 4) continue;

            // Filtro barato antes de montar a célula: longe demais não é vizinha.
            var perto = Math.Sqrt(Math.Pow(cantos[0].X - celula.Origin.X, 2) + Math.Pow(cantos[0].Y - celula.Origin.Y, 2));
            if (perto > 3 * celula.Length + config.MaxGapBeforeBreak) continue;

            PlacedTable vizinha;

            try
            {
                vizinha = TableCells.FromCorners(cantos, partes.Identity.Label, geometria.Length, geometria.Depth * Math.Cos(tilt));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!Vizinhas(celula, vizinha, geometria, config)) continue;

            var dela = Pontas(vizinha, geometria, tilt, config);

            // O par de pontas que se encosta: o mais perto.
            var pares = new[]
            {
                (Minha: 0, Dela: 0, D: Distancia(minha.Primeira, dela.Primeira)),
                (Minha: 0, Dela: 1, D: Distancia(minha.Primeira, dela.Ultima)),
                (Minha: 1, Dela: 0, D: Distancia(minha.Ultima, dela.Primeira)),
                (Minha: 1, Dela: 1, D: Distancia(minha.Ultima, dela.Ultima)),
            };

            var par = pares.MinBy(p => p.D);

            var desenhada = RowPipeline.ProcessFixed(vizinha, geometria, tilt, terreno, settings, cantos[0].Z, cantos[1].Z, null).Tables[0];
            var pilares = desenhada.Pillars.Pillars;
            if (pilares.Count == 0) continue;

            var pilar = par.Dela == 0 ? pilares.MinBy(p => p.Station)! : pilares.MaxBy(p => p.Station)!;
            if (pilar.LowEdgeClearance is not { } pb) continue;

            if (par.Minha == 0 && par.D < distPrimeira)
            {
                primeira = new PontaPresa(partes.Identity.Label, pb);
                distPrimeira = par.D;
            }
            else if (par.Minha == 1 && par.D < distUltima)
            {
                ultima = new PontaPresa(partes.Identity.Label, pb);
                distUltima = par.D;
            }
        }

        return (primeira, ultima);
    }

    /// <summary>As pontas baixas do primeiro e do último pilar, em planta, sem giro.</summary>
    private static (Point3 Primeira, Point3 Ultima) Pontas(PlacedTable celula, TableGeometry geometria, double tilt, SystemConfiguration config)
    {
        var orientacao = RowOrientation.Resolve(celula, config.UpslopeAzimuthRadians);
        var plano = TablePlacement.Plan(celula, orientacao, tilt, 0);

        return (plano.Apply(new Point3(geometria.Pillars.Min(p => p.Station), 0, 0)),
            plano.Apply(new Point3(geometria.Pillars.Max(p => p.Station), 0, 0)));
    }

    private static double Distancia(Point3 a, Point3 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Paralela, na mesma linha, com vão até o que quebra a fileira.</summary>
    private static bool Vizinhas(PlacedTable a, PlacedTable b, TableGeometry geometria, SystemConfiguration config)
    {
        var giro = Math.Abs(Math.IEEERemainder(a.DirectionRadians - b.DirectionRadians, Math.PI));
        if (giro > Paralela) return false;

        var dx = Math.Cos(a.DirectionRadians);
        var dy = Math.Sin(a.DirectionRadians);

        var lateral = Math.Abs(-(b.Origin.X - a.Origin.X) * dy + (b.Origin.Y - a.Origin.Y) * dx);
        if (lateral > geometria.Depth / 2) return false;

        var vao = Math.Max(RowSolver.GapBetween(a, b), RowSolver.GapBetween(b, a));

        return vao > -0.5 && vao <= config.MaxGapBeforeBreak + 1e-9;
    }
}
