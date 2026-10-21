using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O que a canaleta tem além do eixo e do gráfico (28.3, 29.1): o sólido 3D
/// da parede e, em planta, a pintura dos trechos abaixo da declividade mínima
/// (amarelo) e invertidos (vermelho), na camada de alerta (regra 4: nunca
/// falhar calado). Tudo com o GUID da canaleta no XData, refeito inteiro
/// quando ela muda.
/// </summary>
internal static class Canaleta3D
{
    internal const string Tipo3D = "DrnCanaleta3D";
    private const string TipoAlerta = "DrnAlerta";

    private static readonly RXClass ClasseDoSolido = RXObject.GetClass(typeof(Solid3d));
    private static readonly RXClass ClasseDaMalha = RXObject.GetClass(typeof(PolyFaceMesh));
    private static readonly RXClass ClasseDaPolilinha3d = RXObject.GetClass(typeof(Polyline3d));

    /// <summary>Apaga o 3D e a pintura da canaleta. Quantas entidades.</summary>
    internal static int Apagar(Transaction transacao, Database database, Guid canaleta)
    {
        var guid = canaleta.ToString("D");
        var apagadas = 0;
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDoSolido && id.ObjectClass != ClasseDaMalha && id.ObjectClass != ClasseDaPolilinha3d) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (!string.Equals(e.Layer, LayoutLayers.DrenagemCanaleta3D, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(e.Layer, LayoutLayers.DrenagemAlerta, StringComparison.OrdinalIgnoreCase)) continue;

            var campos = PluginXData.Load(e, Tipo3D, 1, 1) ?? PluginXData.Load(e, TipoAlerta, 1, 1);
            if (campos is null || campos[0] != guid) continue;

            e.UpgradeOpen();
            e.Erase();
            apagadas++;
        }

        return apagadas;
    }

    /// <summary>
    /// Refaz o 3D e a pintura da canaleta (nada, se a linha não é canaleta:
    /// só apaga o que houver). Devolve o resumo dos alertas, ou vazio.
    /// </summary>
    internal static string Desenhar(Transaction transacao, Database database, Tin terreno, DrainageSettings configuracao, ObjectId linhaId)
    {
        var polilinha = (Polyline3d)transacao.GetObject(linhaId, OpenMode.ForRead);
        var identidade = LinhasDeDrenagem.Identidade(polilinha) ?? throw new InvalidOperationException(Tr.T("A entidade não é uma linha da drenagem."));

        Apagar(transacao, database, identidade.Id);

        if (identidade.Section is not { } secao) return string.Empty;

        var linha = new LinhaNoDesenho(linhaId, identidade, LinhasDeDrenagem.Vertices(transacao, polilinha));
        var (_, canaleta) = LinhasDeDrenagem.Perfil(terreno, configuracao, linha);
        if (canaleta is null || canaleta.Points.Count < 2) return string.Empty;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada3D = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemCanaleta3D);
        var guid = identidade.Id.ToString("D");

        void Marcar(Entity e, string tipo)
        {
            espaco.AppendEntity(e);
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, tipo, 1, guid);
        }

        var aneis = ChannelSolid.Rings(canaleta.Points, secao);
        var solido = Solido(aneis);

        if (solido is not null)
        {
            solido.Layer = camada3D;
            Marcar(solido, Tipo3D);
        }
        else
        {
            // O modelador recusou o sólido (quina muito fechada, trecho
            // degenerado): fica a malha fechada, que mostra o mesmo 3D.
            var malha = Malha(espaco, transacao, aneis, camada3D);
            PluginXData.Save(transacao, malha, Tipo3D, 1, guid);
        }

        // A pintura dos trechos ruins, em planta, sobre o terreno.
        var camadaAlerta = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemAlerta);
        var baixos = canaleta.LowSegments.ToList();
        var invertidos = canaleta.InvertedSegments.ToList();

        foreach (var (trecho, cor) in baixos.Select(t => (t, (short)2)).Concat(invertidos.Select(t => (t, (short)1))))
        {
            var pontos = canaleta.Points.Where(p => p.Distance >= trecho.From - 1e-9 && p.Distance <= trecho.To + 1e-9).ToList();
            if (pontos.Count < 2) continue;

            var pintura = new Polyline3d { Layer = camadaAlerta, Color = Color.FromColorIndex(ColorMethod.ByAci, cor) };
            Marcar(pintura, TipoAlerta);

            foreach (var p in pontos)
            {
                var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Terrain + 0.05));
                pintura.AppendVertex(v);
                transacao.AddNewlyCreatedDBObject(v, true);
            }
        }

        if (baixos.Count == 0 && invertidos.Count == 0) return string.Empty;

        return Tr.F("{0}: {1} trecho(s) abaixo de {2:0.0}% (amarelo) e {3} plano(s) ou subindo (vermelho), pintados na camada {4}.",
            identidade.Name, baixos.Count, configuracao.Hydrology.MinimumSlope * 100, invertidos.Count, camadaAlerta);
    }

    /// <summary>
    /// O sólido: um loft regrado entre cada par de anéis vizinhos, todos
    /// unidos. Null se o modelador recusar algum pedaço.
    /// </summary>
    private static Solid3d? Solido(IReadOnlyList<IReadOnlyList<Point3>> aneis)
    {
        if (aneis.Count < 2) return null;

        Solid3d? total = null;
        var opcoes = new LoftOptionsBuilder { Ruled = true }.ToLoftOptions();

        try
        {
            for (var k = 0; k < aneis.Count - 1; k++)
            {
                using var a = Anel(aneis[k]);
                using var b = Anel(aneis[k + 1]);

                var pedaco = new Solid3d();
                pedaco.CreateLoftedSolid([a, b], [], null, opcoes);

                if (total is null)
                {
                    total = pedaco;
                }
                else
                {
                    total.BooleanOperation(BooleanOperationType.BoolUnite, pedaco);
                    pedaco.Dispose();
                }
            }

            return total;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("O sólido da canaleta não saiu; vai a malha.", erro);
            total?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// O anel como polilinha plana no plano vertical dele: o loft recusa a
    /// polilinha 3D como seção (eNonPlanarEntity), mesmo plana.
    /// </summary>
    private static Polyline Anel(IReadOnlyList<Point3> anel)
    {
        var a = new Point3d(anel[0].X, anel[0].Y, anel[0].Z);
        var outro = anel.Select(p => new Point3d(p.X, p.Y, p.Z)).First(p => Math.Abs(p.X - a.X) + Math.Abs(p.Y - a.Y) > 1e-6);

        // A normal do plano vertical que contém o anel: horizontal, na perpendicular da largura.
        var largura = new Vector3d(outro.X - a.X, outro.Y - a.Y, 0).GetNormal();
        var normal = largura.CrossProduct(Vector3d.ZAxis).GetNormal();

        var paraOPlano = Matrix3d.WorldToPlane(normal);
        var polilinha = new Polyline { Normal = normal, Closed = true };

        for (var i = 0; i < anel.Count; i++)
        {
            var q = new Point3d(anel[i].X, anel[i].Y, anel[i].Z).TransformBy(paraOPlano);
            if (i == 0) polilinha.Elevation = q.Z;
            polilinha.AddVertexAt(i, new Point2d(q.X, q.Y), 0, 0, 0);
        }

        return polilinha;
    }

    private static PolyFaceMesh Malha(BlockTableRecord espaco, Transaction transacao, IReadOnlyList<IReadOnlyList<Point3>> aneis, string camada)
    {
        var malha = new PolyFaceMesh { Layer = camada };
        espaco.AppendEntity(malha);
        transacao.AddNewlyCreatedDBObject(malha, true);

        var triangulos = ChannelSolid.Triangles(aneis);

        // Todos os vértices antes de qualquer face: o AutoCAD recusa vértice
        // depois de face (eVertexAfterFace).
        var indice = new Dictionary<Point3, short>();
        foreach (var p in triangulos.SelectMany(t => new[] { t.A, t.B, t.C }))
        {
            if (indice.ContainsKey(p)) continue;

            var v = new PolyFaceMeshVertex(new Point3d(p.X, p.Y, p.Z));
            malha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
            indice[p] = (short)(indice.Count + 1);
        }

        foreach (var (a, b, c) in triangulos)
        {
            var face = new FaceRecord(indice[a], indice[b], indice[c], 0);
            malha.AppendFaceRecord(face);
            transacao.AddNewlyCreatedDBObject(face, true);
        }

        return malha;
    }
}
