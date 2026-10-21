using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// As caixas entre canaletas (32.6): contorno e nome em planta, sólido oco em
/// 3D, o fundo abaixo da canaleta mais funda que chega nela. Cada entidade da
/// caixa leva o id dela no XData; o contorno externo leva também as medidas.
/// </summary>
internal static class CaixasDeDrenagem
{
    internal const string Tipo = "DrnCaixa";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>As caixas do desenho, pelo contorno externo (o que leva as medidas).</summary>
    internal static List<(ObjectId Id, string Caixa, BoxRecord Registro)> Todas(Transaction transacao, Database database)
    {
        var caixas = new List<(ObjectId, string, BoxRecord)>();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline p || p.Layer != LayoutLayers.DrenagemCanaleta) continue;
            if (PluginXData.LoadAll(p, Tipo, 1, 7) is not { } c) continue;

            double N(int i) => double.Parse(c[i], Inv);
            caixas.Add((id, c[0], new BoxRecord(c[1], N(2), N(3), N(4), N(5), N(6))));
        }

        return caixas;
    }

    /// <summary>
    /// Põe a caixa em (x, y): alinhada à canaleta mais perto, o fundo pelas
    /// canaletas que terminam dentro dela (mais meio metro em volta). O nome.
    /// </summary>
    internal static string Inserir(Transaction transacao, Database database, Tin terreno, DrainageSettings configuracao, double x, double y)
    {
        var medidas = configuracao.Box;
        if (medidas.Problem() is { } problema) throw new ArgumentException(problema);
        if (!terreno.TryGetZ(x, y, out var topo)) throw new ArgumentException(Tr.T("A caixa precisa ficar dentro do terreno."));

        // As pontas das canaletas que chegam na caixa, e a direção da mais perto.
        var alcance = Math.Max(medidas.InnerLength, medidas.InnerWidth) / 2 + medidas.Wall + 0.5;
        var fundos = new List<double>();
        double angulo = 0, maisPerto = double.MaxValue;

        foreach (var linha in LinhasDeDrenagem.Todas(transacao, database).Where(l => l.Identidade.IsChannel))
        {
            ChannelProfileResult? perfil;
            try
            {
                perfil = LinhasDeDrenagem.Perfil(terreno, configuracao, linha).Canaleta;
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (perfil is not { Points.Count: >= 2 } p) continue;

            foreach (var (ponta, vizinho) in new[] { (p.Points[0], p.Points[1]), (p.Points[^1], p.Points[^2]) })
            {
                var d = Math.Sqrt((ponta.X - x) * (ponta.X - x) + (ponta.Y - y) * (ponta.Y - y));
                if (d <= alcance) fundos.Add(ponta.Bottom);
                if (d < maisPerto)
                {
                    maisPerto = d;
                    angulo = Math.Atan2(ponta.Y - vizinho.Y, ponta.X - vizinho.X);
                }
            }
        }

        var fundo = DrainageExport.BoxFloor(medidas, topo, fundos);
        var nome = Tr.F("CX {0}", Todas(transacao, database).Select(c => c.Registro.Name).Select(n => int.TryParse(n.Split(' ').Last(), out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1);
        var registro = new BoxRecord(nome, medidas.InnerLength, medidas.InnerWidth, medidas.Wall, topo, fundo);

        Desenhar(transacao, database, registro, x, y, angulo);

        return Tr.F("CAIXA {0}: interna {1:0.##} × {2:0.##} m, parede {3:0.##} m, {4:0.00} m de fundo ({5} canaleta(s) chegando).",
            nome, registro.InnerLength, registro.InnerWidth, registro.Wall, registro.Depth, fundos.Count);
    }

    private static void Desenhar(Transaction transacao, Database database, BoxRecord caixa, double x, double y, double angulo)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemCanaleta);
        var camada3D = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemCanaleta3D);
        var id = Guid.NewGuid().ToString("D");
        var (c, s) = (Math.Cos(angulo), Math.Sin(angulo));

        Point2d Canto(double u, double v) => new(x + u * c - v * s, y + u * s + v * c);

        Polyline Retangulo(double comprimento, double largura, double z)
        {
            var r = new Polyline { Layer = camada, Elevation = z, Closed = true, ColorIndex = 30 };
            r.AddVertexAt(0, Canto(-comprimento / 2, -largura / 2), 0, 0, 0);
            r.AddVertexAt(1, Canto(comprimento / 2, -largura / 2), 0, 0, 0);
            r.AddVertexAt(2, Canto(comprimento / 2, largura / 2), 0, 0, 0);
            r.AddVertexAt(3, Canto(-comprimento / 2, largura / 2), 0, 0, 0);
            return r;
        }

        void Pôr(Entity e, params string[] campos)
        {
            espaco.AppendEntity(e);
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, Tipo, 1, campos.Length > 0 ? campos : [id]);
        }

        var (cExt, lExt) = (caixa.InnerLength + 2 * caixa.Wall, caixa.InnerWidth + 2 * caixa.Wall);
        string N(double v) => v.ToString("R", Inv);

        // Em planta: o externo (com as medidas no XData), o interno e o nome.
        Pôr(Retangulo(cExt, lExt, caixa.Top + 0.02), id, caixa.Name, N(caixa.InnerLength), N(caixa.InnerWidth), N(caixa.Wall), N(caixa.Top), N(caixa.Floor));
        Pôr(Retangulo(caixa.InnerLength, caixa.InnerWidth, caixa.Top + 0.02));

        var texto = new DBText
        {
            Layer = camada,
            ColorIndex = 30,
            Height = Math.Max(0.25, 0.4 * cExt),
            TextString = caixa.Name,
            Position = new Point3d(x, y + lExt / 2 + 0.2, caixa.Top + 0.05),
            HorizontalMode = TextHorizontalMode.TextCenter,
            AlignmentPoint = new Point3d(x, y + lExt / 2 + 0.2, caixa.Top + 0.05),
        };
        Pôr(texto);
        texto.AdjustAlignment(database);

        // Em 3D: o bloco externo (do fundo menos a parede ao topo) menos o vão interno.
        var externo = new Solid3d { Layer = camada3D };
        try
        {
            var alturaExt = caixa.Depth + caixa.Wall;
            externo.CreateBox(cExt, lExt, alturaExt);

            using var vao = new Solid3d();
            vao.CreateBox(caixa.InnerLength, caixa.InnerWidth, caixa.Depth + 0.01);
            vao.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, caixa.Wall / 2 + 0.005)));
            externo.BooleanOperation(BooleanOperationType.BoolSubtract, vao);

            externo.TransformBy(Matrix3d.Displacement(new Vector3d(x, y, caixa.Floor - caixa.Wall + alturaExt / 2)) * Matrix3d.Rotation(angulo, Vector3d.ZAxis, Point3d.Origin));
        }
        catch
        {
            externo.Dispose();
            throw;
        }

        Pôr(externo);
    }

    /// <summary>Apaga a caixa da entidade escolhida (todas as peças dela). Quantas.</summary>
    internal static int Remover(Transaction transacao, Database database, ObjectId escolhida)
    {
        if (transacao.GetObject(escolhida, OpenMode.ForRead) is not Entity e || PluginXData.LoadAll(e, Tipo, 1, 1) is not { } campos) return 0;

        var id = campos[0];
        var apagadas = 0;
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId oid in espaco)
        {
            if (transacao.GetObject(oid, OpenMode.ForRead) is not Entity x) continue;
            if (x.Layer != LayoutLayers.DrenagemCanaleta && x.Layer != LayoutLayers.DrenagemCanaleta3D) continue;
            if (PluginXData.LoadAll(x, Tipo, 1, 1) is not { } c || c[0] != id) continue;

            x.UpgradeOpen();
            x.Erase();
            apagadas++;
        }

        return apagadas;
    }
}
