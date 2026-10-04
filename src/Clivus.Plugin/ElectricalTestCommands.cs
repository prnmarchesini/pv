using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

#if DEBUG
[assembly: CommandClass(typeof(Clivus.Plugin.ElectricalTestCommands))]
#endif

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2 da parte elétrica): strings de mentira para
/// os passos que precisam de strings no desenho antes do traçado de verdade
/// (11.7). Uma string por fileira de cada mesa, os módulos na ordem das
/// colunas, a polilinha pelo centro das faces (5 cm acima), sem tipo, sem
/// inversor, sem tag. Mesmo XData das strings de verdade.
/// </summary>
public static class ElectricalTestCommands
{
#if DEBUG
    [CommandMethod(PluginInfo.ComandoStringsTesteAutomatico)]
#endif
    public static void StringsDeTeste()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var database = documento.Database;
        var feitas = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
            var faces = new List<(FaceIdentity Face, Point3d Centro)>();

            foreach (ObjectId id in espaco)
            {
                if (id.IsErased || id.ObjectClass.DxfName != "3DFACE") continue;
                if (transacao.GetObject(id, OpenMode.ForRead) is not Face face || LayoutXData.LoadFace(face) is not { } f) continue;

                var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).ToList();
                faces.Add((f, new Point3d(cantos.Average(p => p.X), cantos.Average(p => p.Y), cantos.Average(p => p.Z) + 0.05)));
            }

            var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.String, new RgbColor(230, 60, 60));

            foreach (var fileira in faces.GroupBy(x => (x.Face.Table, x.Face.Row)).OrderBy(g => g.Key.Table).ThenBy(g => g.Key.Row))
            {
                var ordem = fileira.OrderBy(x => x.Face.Column).ToList();
                var linha = new Polyline3d { Layer = camada };
                espaco.AppendEntity(linha);
                transacao.AddNewlyCreatedDBObject(linha, true);

                foreach (var (_, centro) in ordem)
                {
                    var v = new PolylineVertex3d(centro);
                    linha.AppendVertex(v);
                    transacao.AddNewlyCreatedDBObject(v, true);
                }

                ElectricalStore.SaveString(transacao, linha, new ElectricalString(Guid.NewGuid(), Guid.Empty, ordem.Select(x => x.Face.Module).ToList(), Guid.Empty, string.Empty));
                feitas++;
            }

            transacao.Commit();
        }

        documento.Editor.WriteMessage($"\nSTRINGS_TESTE {feitas} string(s) de teste.\n");
    }
}
