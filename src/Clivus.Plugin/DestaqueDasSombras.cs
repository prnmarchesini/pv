using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O destaque dos piores módulos no desenho (42.15, Renan: "o sistema pinta os
/// modulos mais fudidos com ordem"): uma hachura sólida sobre a face do módulo,
/// da cor do lugar (do vinho, o pior, ao amarelo), e o lugar escrito em cima,
/// no eixo da mesa (texto nunca virado). Nada do módulo muda: o destaque é do
/// plugin, na camada CLIVUS_SOMBRA_DESTAQUE, com o XData "SombraDestaque" (sai
/// no Limpar destaque, no próximo destaque e na Limpeza das sombras).
/// </summary>
internal static class DestaqueDasSombras
{
    private const int Versao = 1;

    /// <summary>Um módulo a destacar: o lugar (1 é o pior), a cor, a face e o handle do módulo.</summary>
    internal sealed record Destaque(int Lugar, RgbColor Cor, IReadOnlyList<Point3> Cantos, string Modulo);

    /// <summary>Desenha os destaques; quantos saíram (a face sem área fica de fora).</summary>
    internal static int Desenhar(Transaction transacao, Database database, IEnumerable<Destaque> destaques)
    {
        var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.SombraDestaque, new RgbColor(128, 0, 38));
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var feitos = 0;

        foreach (var d in destaques)
        {
            if (d.Cantos.Count < 3 || Normal(d.Cantos) is not { } n) continue;

            var hachura = Hachura(transacao, database, espaco, camada, d.Cantos, n, d.Cor);
            if (hachura is null) continue;
            PluginXData.Save(transacao, hachura, Cleanup.ShadowHighlightType, Versao, d.Lugar.ToString(CultureInfo.InvariantCulture), d.Modulo);

            var texto = Lugar(transacao, database, espaco, camada, d.Cantos, d.Lugar, d.Cor);
            PluginXData.Save(transacao, texto, Cleanup.ShadowHighlightType, Versao, d.Lugar.ToString(CultureInfo.InvariantCulture), d.Modulo);
            feitos++;
        }

        return feitos;
    }

    /// <summary>Apaga todo o destaque do desenho; quantas entidades saíram.</summary>
    internal static int Apagar(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var apagadas = 0;

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (PluginXData.LoadAll(e, Cleanup.ShadowHighlightType, Versao, 0) is null) continue;

            e.UpgradeOpen();
            e.Erase();
            apagadas++;
        }

        return apagadas;
    }

    /// <summary>Quantos módulos estão destacados agora (as hachuras do destaque).</summary>
    internal static int Contar(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var n = 0;
        foreach (ObjectId id in espaco)
            if (!id.IsErased && transacao.GetObject(id, OpenMode.ForRead) is Hatch h && PluginXData.LoadAll(h, Cleanup.ShadowHighlightType, Versao, 0) is not null) n++;

        return n;
    }

    // A normal para cima da face (o produto das diagonais), ou null sem área.
    private static Vector3d? Normal(IReadOnlyList<Point3> c)
    {
        var a = new Vector3d(c[2].X - c[0].X, c[2].Y - c[0].Y, c[2].Z - c[0].Z);
        var b = new Vector3d(c[^1].X - c[1].X, c[^1].Y - c[1].Y, c[^1].Z - c[1].Z);
        var n = a.CrossProduct(b);
        if (n.Length < 1e-9) return null;
        n = n.GetNormal();
        return n.Z < 0 ? n.Negate() : n;
    }

    private static Hatch? Hachura(Transaction transacao, Database database, BlockTableRecord espaco, string camada, IReadOnlyList<Point3> cantos, Vector3d normal, RgbColor cor)
    {
        var paraOcs = Matrix3d.WorldToPlane(normal);
        Hatch? hachura = null;
        try
        {
            // Os padrões do banco ANTES da camada: SetDatabaseDefaults reescreve camada e cor.
            hachura = new Hatch();
            hachura.SetDatabaseDefaults(database);
            hachura.Layer = camada;
            hachura.Color = Color.FromRgb(cor.R, cor.G, cor.B);
            hachura.Normal = normal;

            // Um centímetro acima da face, para ficar por cima dela e da sombra magenta.
            var acima = normal * 0.01;
            var p0 = new Point3d(cantos[0].X, cantos[0].Y, cantos[0].Z) + acima;
            hachura.Elevation = p0.TransformBy(paraOcs).Z;

            espaco.AppendEntity(hachura);
            transacao.AddNewlyCreatedDBObject(hachura, true);
            hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            hachura.Associative = false;

            var pontos = new Point2dCollection();
            var arcos = new DoubleCollection();
            foreach (var c in cantos.Append(cantos[0]))
            {
                var q = (new Point3d(c.X, c.Y, c.Z) + acima).TransformBy(paraOcs);
                pontos.Add(new Point2d(q.X, q.Y));
                arcos.Add(0);
            }

            hachura.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, pontos, arcos);
            hachura.EvaluateHatch(true);
            return hachura;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui hachurar o destaque de um módulo.", erro);
            if (hachura is not null)
            {
                if (hachura.ObjectId.IsNull) hachura.Dispose();
                else if (!hachura.IsErased) hachura.Erase();
            }

            return null;
        }
    }

    /// <summary>
    /// O lugar escrito no meio do módulo, no eixo da mesa (a borda mais
    /// nivelada da face) e nunca de cabeça para baixo; a letra do tamanho do
    /// lado menor.
    /// </summary>
    private static DBText Lugar(Transaction transacao, Database database, BlockTableRecord espaco, string camada, IReadOnlyList<Point3> cantos, int lugar, RgbColor cor)
    {
        var centro = new Point3d(cantos.Average(c => c.X), cantos.Average(c => c.Y), cantos.Max(c => c.Z) + 0.05);

        // A borda mais nivelada (a que sobe menos) é a do eixo da mesa.
        var bordas = Enumerable.Range(0, cantos.Count).Select(i => (A: cantos[i], B: cantos[(i + 1) % cantos.Count])).ToList();
        var eixo = bordas.OrderBy(b => Math.Abs(b.B.Z - b.A.Z) / Math.Max(1e-6, Math.Sqrt(Math.Pow(b.B.X - b.A.X, 2) + Math.Pow(b.B.Y - b.A.Y, 2)))).First();
        var angulo = Math.Atan2(eixo.B.Y - eixo.A.Y, eixo.B.X - eixo.A.X);
        if (angulo > Math.PI / 2) angulo -= Math.PI;
        else if (angulo <= -Math.PI / 2) angulo += Math.PI;

        var menor = bordas.Min(b => Math.Sqrt(Math.Pow(b.B.X - b.A.X, 2) + Math.Pow(b.B.Y - b.A.Y, 2)));

        var texto = new DBText();
        texto.SetDatabaseDefaults(database);
        texto.Layer = camada;
        // Claro no vinho e no vermelho, escuro no laranja e no amarelo.
        texto.Color = 0.299 * cor.R + 0.587 * cor.G + 0.114 * cor.B < 140 ? Color.FromRgb(255, 255, 255) : Color.FromRgb(20, 20, 20);
        texto.TextString = lugar.ToString(CultureInfo.InvariantCulture);
        texto.Height = Math.Clamp(menor * 0.45, 0.15, 1.5);
        texto.Rotation = angulo;
        texto.HorizontalMode = TextHorizontalMode.TextCenter;
        texto.VerticalMode = TextVerticalMode.TextVerticalMid;
        texto.AlignmentPoint = centro;
        texto.Position = centro;

        espaco.AppendEntity(texto);
        transacao.AddNewlyCreatedDBObject(texto, true);
        texto.AdjustAlignment(database);
        return texto;
    }
}
