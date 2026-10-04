using System.IO;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.Ver3DCommands))]

namespace Clivus.Plugin;

/// <summary>
/// O 3D no navegador (9.9): grava uma página HTML (um arquivo só, com a
/// three.js dentro) com o terreno, os módulos nas cores do desenho, os
/// pilares, as árvores e as sombras desenhadas, e abre no navegador padrão.
/// A página fica ao lado do desenho ("nome do desenho - 3D.html"); desenho
/// nunca salvo, em Documentos.
/// </summary>
public static class Ver3DCommands
{
    [CommandMethod(PluginInfo.ComandoVer3D)]
    public static void Ver3D()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var caminho = Gravar(documento, Caminho(documento));
            if (caminho is null) return;

            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            editor.WriteMessage("  Aberta no navegador padrão. O arquivo pode ser mandado por e-mail: abre sem internet.\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar o 3D.", erro);
            editor.WriteMessage($"\nNão consegui gerar o 3D: {erro.Message}\n");
        }
    }

    /// <summary>CLIVUS_3D_AUTO: grava a página no caminho dado, sem abrir o navegador. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoVer3DAutomatico)]
    public static void Ver3DAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var caminho = editor.GetString(new PromptStringOptions("\nArquivo da página 3D: ") { AllowSpaces = true });
            if (caminho.Status != PromptStatus.OK) return;

            Gravar(documento, caminho.StringResult.Trim().Trim('"'));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar o 3D (automático).", erro);
            editor.WriteMessage($"\nNão consegui gerar o 3D: {erro.Message}\n");
        }
    }

    private static string Caminho(Document documento)
    {
        var nome = Path.GetFileNameWithoutExtension(documento.Name);
        var pasta = documento.IsNamedDrawing && Path.GetDirectoryName(documento.Name) is { Length: > 0 } p
            ? p
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return Path.Combine(pasta, $"{nome} - 3D.html");
    }

    /// <summary>Monta a cena e grava a página. O caminho gravado, ou null (com a mensagem).</summary>
    private static string? Gravar(Document documento, string caminho)
    {
        var editor = documento.Editor;
        var database = documento.Database;

        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return null;

        var relogio = Stopwatch.StartNew();
        var cena = Cena(documento, terreno);

        if (cena.Faces.Count == 0 && cena.Trees.Count == 0)
            editor.WriteMessage("\n  ATENÇÃO: o desenho não tem módulo nem árvore do plugin; a página mostra só o terreno.\n");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        File.WriteAllText(caminho, Viewer3DPage.Html(cena), new System.Text.UTF8Encoding(false));
        relogio.Stop();

        var tamanho = new FileInfo(caminho).Length / 1024.0 / 1024.0;
        var brasil = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

        editor.WriteMessage(
            $"\n3D {cena.Faces.Count} módulo(s), {cena.Pillars.Count} pilar(es), {cena.Trees.Count} árvore(s), {cena.Shadows.Count} contorno(s) de sombra, "
            + $"terreno em grade de {cena.Terrain!.Step.ToString("0.#", brasil)} m: {caminho} ({tamanho.ToString("0.0", brasil)} MB, {relogio.Elapsed.TotalSeconds.ToString("0.0", brasil)} s).\n");
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);

        return caminho;
    }

    private static Scene3D Cena(Document documento, ProcessedTerrain terreno)
    {
        var database = documento.Database;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

        foreach (var t in terreno.Mesh.Triangles)
        {
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        var grade = Viewer3DPage.SampleTerrain(minX, minY, maxX, maxY, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null);

        var faces = new List<Scene3DFace>();
        var pilares = new List<Scene3DPillar>();
        var arvores = new List<Scene3DTree>();
        var sombras = new List<IReadOnlyList<Point3>>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            var cores = new Dictionary<Guid, RgbColor>();
            var facesLidas = new List<(Guid Modulo, List<Point3> Cantos)>();

            foreach (ObjectId id in espaco)
            {
                if (id.IsErased) continue;
                var dxf = id.ObjectClass.DxfName;
                if (dxf != "3DFACE" && dxf != "INSERT" && dxf != "POLYLINE") continue;
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

                using (var dados = e.GetXDataForApplication(PluginXData.Aplicativo))
                    if (dados is null) continue;

                switch (e)
                {
                    case Face face when LayoutXData.LoadFace(face) is { } f:
                        facesLidas.Add((f.Module, Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList()));
                        break;

                    case BlockReference br when LayoutXData.LoadModule(br) is { } m:
                        cores.TryAdd(m.Id, Cor(transacao, br));
                        break;

                    case BlockReference br when LayoutXData.LoadPillar(br) is { } p:
                        // O topo do pilar é a origem do bloco; o pé visível, o terreno.
                        var topo = br.Position;
                        pilares.Add(new Scene3DPillar(topo.X, topo.Y, topo.Z, p.GroundZ ?? topo.Z - (p.FreeHeight ?? 0)));
                        break;

                    case BlockReference br when LayoutXData.LoadTree(br) is { } a:
                        arvores.Add(new Scene3DTree(br.Position.X, br.Position.Y, br.Position.Z, a.Spec));
                        break;

                    case Polyline3d linha when PluginXData.Load(linha, "Sombra", 1, 1) is not null:
                        sombras.Add(FileiraCommands.Vertices(linha, transacao).ToList());
                        break;
                }
            }

            foreach (var (modulo, cantos) in facesLidas)
                faces.Add(new Scene3DFace(cantos, cores.TryGetValue(modulo, out var c) ? c : new RgbColor(30, 60, 140)));
        }

        var titulo = Path.GetFileNameWithoutExtension(documento.Name);
        return new Scene3D(string.IsNullOrWhiteSpace(titulo) ? "Usina" : titulo, grade, faces, pilares, arvores, sombras);
    }

    /// <summary>A cor que a peça mostra: a dela, ou a da camada quando é "por camada".</summary>
    private static RgbColor Cor(Transaction transacao, Entity e)
    {
        var cor = e.Color;

        if (cor.IsByLayer || cor.IsByBlock)
        {
            if (transacao.GetObject(e.LayerId, OpenMode.ForRead) is LayerTableRecord camada) cor = camada.Color;
        }

        var valor = cor.ColorValue;
        return new RgbColor(valor.R, valor.G, valor.B);
    }
}
