using System.IO;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.BancadaCommands))]

namespace UFV.Plugin;

/// <summary>
/// UFV_BANCADA: exporta o terreno processado, a configuração, o perfil de
/// mesa e o contorno de cada mesa desenhada para um JSON em
/// %LOCALAPPDATA%\MarchEng\UFV\bancada. É a bancada do motor: o mesmo
/// terreno e as mesmas células, fora do CAD, para reproduzir uma mesa que
/// saiu errada na tela e virar teste (27/09/2026).
/// </summary>
public static class BancadaCommands
{
    [CommandMethod(PluginInfo.ComandoBancada)]
    public static void Bancada()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var settings = ConfigCommands.Inicial(documento, out _);
            var perfil = FileiraCommands.PerfilDaMesa(editor, silencioso: true);

            var mesas = new List<object>();

            using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var (guid, partes) in LayoutScan.Tables(transacao, documento.Database))
                {
                    if (partes.Identity is not { } id || partes.Contour is not { } contorno) continue;

                    if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha) continue;
                    var cantos = FileiraCommands.Vertices(polilinha, transacao).Select(p => new[] { p.X, p.Y, p.Z }).ToList();

                    mesas.Add(new { Guid = guid, id.Label, id.StartElevation, id.EndElevation, id.Marked, id.Reason, Corners = cantos });
                }
            }

            var triangulos = terreno.Mesh.Triangles
                .Select(t => new[] { t.A.X, t.A.Y, t.A.Z, t.B.X, t.B.Y, t.B.Z, t.C.X, t.C.Y, t.C.Z })
                .ToList();

            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarchEng", "UFV", "bancada");
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, $"bancada-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            var conteudo = new
            {
                Settings = settings.ToFields().ToDictionary(c => c.Key, c => c.Value),
                Profile = JsonDocument.Parse(perfil.ToJson()).RootElement,
                Tables = mesas,
                Triangles = triangulos,
            };

            File.WriteAllText(arquivo, JsonSerializer.Serialize(conteudo));
            editor.WriteMessage($"\nBANCADA {mesas.Count} mesa(s), {triangulos.Count} triângulo(s) em {arquivo}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar a bancada.", erro);
            editor.WriteMessage($"\nNão consegui exportar a bancada: {erro.Message}\n");
        }
    }
}
