#if DEBUG
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RotaDeCabosConferir))]

namespace Clivus.Plugin;

/// <summary>
/// Só no build de teste (nível 2): lê do desenho cada vala e cada cabo da
/// rota de cabos e confere a cota de cada vértice contra o TIN no mesmo XY
/// (regra universal de 10/10/2026: todo desenho respeita o TIN). A vala tem
/// que estar na cota do terreno menos a profundidade da aba; o cabo, entre o
/// fundo da vala e a base do equipamento (terreno + 0,80). Escreve os
/// vértices dos cabos em planta para o runner conferir o caminho.
/// </summary>
public static class RotaDeCabosConferir
{
    [CommandMethod(PluginInfo.ComandoRotaConferirAutomatico)]
    public static void Conferir()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (FileiraCommands.ExigirTerreno(editor, documento) is not { } terreno) return;
            var db = documento.Database;
            var config = RotaDeCabosStore.Configuracoes(db, out _);
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            using var t = db.TransactionManager.StartOpenCloseTransaction();
            var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

            foreach (ObjectId id in espaco)
            {
                if (t.GetObject(id, OpenMode.ForRead) is not Curve curva) continue;
                var vala = RotaDeCabosStore.Vala(curva);
                var lance = curva is Polyline3d ? RotaDeCabosStore.Lance(curva) : null;
                if (vala is null && lance is null) continue;

                var pontos = new List<Point3>();
                if (curva is Polyline3d p3)
                    foreach (ObjectId v in p3.Cast<ObjectId>().Where(v => !v.IsErased))
                    {
                        var p = ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position;
                        pontos.Add(new Point3(p.X, p.Y, p.Z));
                    }
                else
                    pontos.Add(new Point3(curva.StartPoint.X, curva.StartPoint.Y, curva.StartPoint.Z));

                var rota = vala?.Route ?? lance!.Route;
                var fundo = config[rota].Depth;
                double maiorDesvio = 0, abaixo = 0, acima = 0;
                var fora = 0;

                foreach (var p in pontos)
                {
                    if (!terreno.Mesh.TryGetZ(p.X, p.Y, out var z))
                    {
                        fora++;
                        continue;
                    }

                    if (vala is not null) maiorDesvio = Math.Max(maiorDesvio, Math.Abs(p.Z - (z - fundo)));
                    else
                    {
                        abaixo = Math.Max(abaixo, (z - fundo) - p.Z);
                        acima = Math.Max(acima, p.Z - (z + EquipmentFootprint.FloatHeight));
                    }
                }

                if (vala is not null)
                {
                    editor.WriteMessage(string.Format(inv, "\nROTA_VALA rota={0} tipo={1} vertices={2} fora={3} desvio={4:0.0000}\n",
                        rota, curva.GetType().Name, pontos.Count, fora, maiorDesvio));
                }
                else
                {
                    editor.WriteMessage(string.Format(inv, "\nROTA_CABO rota={0} vertices={1} fora={2} abaixo={3:0.0000} acima={4:0.0000} zmin={5:0.###} planta={6}\n",
                        rota, pontos.Count, fora, abaixo, acima, pontos.Min(p => p.Z),
                        string.Join(";", pontos.Select(p => string.Format(inv, "{0:0.###},{1:0.###}", p.X, p.Y)))));
                }
            }

            editor.WriteMessage("\nROTA_CONFERIR_FIM\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir a rota de cabos (teste).", erro);
            editor.WriteMessage($"\nROTA_CONFERIR_ERRO {erro.Message}\n");
        }
    }
}
#endif
