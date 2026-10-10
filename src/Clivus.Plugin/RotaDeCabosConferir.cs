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

                for (var i = 0; i < pontos.Count; i++)
                {
                    var p = pontos[i];
                    if (!terreno.Mesh.TryGetZ(p.X, p.Y, out var z))
                    {
                        fora++;
                        continue;
                    }

                    if (vala is not null) maiorDesvio = Math.Max(maiorDesvio, Math.Abs(p.Z - (z - fundo)));
                    else
                    {
                        // As pontas são o módulo da string ou a base do equipamento: só o miolo
                        // tem que ficar entre o fundo da vala e o terreno + 0,80.
                        abaixo = Math.Max(abaixo, (z - fundo) - p.Z);
                        if (i > 0 && i < pontos.Count - 1) acima = Math.Max(acima, p.Z - (z + EquipmentFootprint.FloatHeight));
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

            // Os equipamentos em campo: a base contra o TIN no centro (+ 0,80) e a pegada da caixa em planta.
            var setup = ConfiguracaoEletricaStore.Ler(db).Setup;
            foreach (var ((tipo, guid), ids) in EquipamentoEmCampo.Posicionados(t, db))
            {
                if (ids.Count == 0 || t.GetObject(ids[0], OpenMode.ForRead) is not BlockReference b) continue;
                var tag = setup.FindEquipment(tipo, guid)?.Tag ?? "?";
                var desvio = terreno.Mesh.TryGetZ(b.Position.X, b.Position.Y, out var chao) ? Math.Abs(b.Position.Z - (chao + EquipmentFootprint.FloatHeight)) : double.NaN;
                var caixa = setup.FindEquipment(tipo, guid)?.Size;
                var (w, l) = caixa is null ? (0.0, 0.0) : (caixa.Width / 2, caixa.Length / 2);
                editor.WriteMessage(string.Format(inv, "\nROTA_EQUIP tag={0} desvio={1:0.0000} minx={2:0.###} miny={3:0.###} maxx={4:0.###} maxy={5:0.###} fim\n",
                    tag.Replace(' ', '_'), desvio, b.Position.X - w, b.Position.Y - l, b.Position.X + w, b.Position.Y + l));
            }

            // As áreas de inversores: cada vértice no terreno daquele XY.
            foreach (var (_, (marca, _)) in LocalDosInversores.Areas(db))
            {
                foreach (ObjectId id in espaco)
                {
                    if (t.GetObject(id, OpenMode.ForRead) is not Polyline3d p3 || PluginXData.Load(p3, SiteMark.Tipo, 1, SiteMark.FieldCount) is not { } c || SiteMark.Parse(c)?.Id != marca.Id) continue;
                    double maior = 0;
                    var fora = 0;
                    foreach (var v in p3.Cast<ObjectId>().Where(v => !v.IsErased))
                    {
                        var p = ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position;
                        if (terreno.Mesh.TryGetZ(p.X, p.Y, out var z)) maior = Math.Max(maior, Math.Abs(p.Z - z));
                        else fora++;
                    }

                    editor.WriteMessage(string.Format(inv, "\nROTA_AREA nome={0} fechada={1} fora={2} desvio={3:0.0000} fim\n", marca.Name.Replace(' ', '_'), p3.Closed, fora, maior));
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
