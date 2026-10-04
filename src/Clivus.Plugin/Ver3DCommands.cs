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
/// nunca salvo, em Documentos. Com o servidor 3D configurado (CLIVUS_SERVIDOR),
/// a usina é publicada nele e o link abre em qualquer aparelho
/// (plano/contrato-servidor-3d.md).
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
            var cena = Montar(documento);
            if (cena is null) return;

            // Com o servidor 3D configurado, a usina vai para ele e o link
            // abre (no PC e, mandado, no celular). Sem servidor, ou se ele
            // falhar, a página local de sempre.
            if (Publicador3D.Endereco is not null)
            {
                var link = Publicar(documento, cena);
                if (link is not null)
                {
                    Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                    editor.WriteMessage(Tr.T("  Aberta no navegador. O link abre em qualquer aparelho, inclusive no celular.\n"));
                    return;
                }

                editor.WriteMessage(Tr.T("  Gravo a página no PC, como antes.\n"));
            }

            var caminho = Gravar(documento, cena, Caminho(documento));
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            editor.WriteMessage(Tr.T("  Aberta no navegador padrão. O arquivo pode ser mandado por e-mail: abre sem internet.\n"));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar o 3D.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar o 3D: {0}\n", erro.Message));
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
            var caminho = editor.GetString(new PromptStringOptions(Tr.T("\nArquivo da página 3D: ")) { AllowSpaces = true });
            if (caminho.Status != PromptStatus.OK) return;

            if (Montar(documento) is { } cena) Gravar(documento, cena, caminho.StringResult.Trim().Trim('"'));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar o 3D (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar o 3D: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_3D_PUBLICAR_AUTO: publica no servidor 3D (CLIVUS_SERVIDOR) e escreve o link, sem abrir o navegador. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoVer3DPublicarAutomatico)]
    public static void Ver3DPublicarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (Montar(documento) is { } cena) Publicar(documento, cena);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao publicar o 3D (automático).", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui publicar o 3D: {0}\n", erro.Message));
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

    /// <summary>A cena do desenho, ou null (sem terreno, com a mensagem).</summary>
    private static Scene3D? Montar(Document documento)
    {
        var editor = documento.Editor;

        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return null;

        var (cena, modulosFora, pilaresFora) = Viewer3DPage.OnTerrain(Cena(documento, terreno), (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null);

        if (modulosFora + pilaresFora > 0)
            editor.WriteMessage(Tr.F("\n  {0} módulo(s) e {1} pilar(es) sem terreno embaixo ficaram fora do 3D (no desenho estão planos na cota 0 e marcados).\n", modulosFora, pilaresFora));

        if (cena.Faces.Count == 0 && cena.Trees.Count == 0)
            editor.WriteMessage(Tr.T("\n  ATENÇÃO: o desenho não tem módulo nem árvore do plugin; a página mostra só o terreno.\n"));

        return cena;
    }

    private static string Resumo(Scene3D cena)
    {
        return Tr.F(
            "{0} módulo(s), {1} pilar(es), {2} árvore(s), {3} contorno(s) de sombra, terreno em grade de {4:0.#} m",
            cena.Faces.Count, cena.Pillars.Count, cena.Trees.Count, cena.Shadows.Count, cena.Terrain!.Step);
    }

    /// <summary>Grava a página local. O caminho gravado.</summary>
    private static string Gravar(Document documento, Scene3D cena, string caminho)
    {
        var editor = documento.Editor;
        var relogio = Stopwatch.StartNew();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        File.WriteAllText(caminho, Viewer3DPage.Html(cena), new System.Text.UTF8Encoding(false));
        relogio.Stop();

        var tamanho = new FileInfo(caminho).Length / 1024.0 / 1024.0;
        editor.WriteMessage(Tr.F("\n3D {0}: {1} ({2:0.0} MB, {3:0.0} s).\n", Resumo(cena), caminho, tamanho, relogio.Elapsed.TotalSeconds));
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);

        return caminho;
    }

    /// <summary>Publica no servidor 3D. O link, ou null (com o erro dito).</summary>
    private static string? Publicar(Document documento, Scene3D cena)
    {
        var editor = documento.Editor;
        var relogio = Stopwatch.StartNew();

        var corpo = Viewer3DPage.PublishBody(cena, PluginInfo.VersaoLegivel(ClivusCommands.VersaoDoPlugin()), cena.Title);
        var (publicada, erro) = Publicador3D.Publicar(corpo);
        relogio.Stop();

        if (publicada is null)
        {
            editor.WriteMessage(Tr.F("\n3D Não publiquei no servidor: {0}.\n", erro));
            return null;
        }

        // Só abre o link do próprio servidor (plano/seguranca.md): um servidor
        // comprometido não manda o usuário para outro site.
        if (!Viewer3DPage.IsLinkFromServer(publicada.Url, Publicador3D.Endereco!))
        {
            editor.WriteMessage(Tr.F("\n3D ATENÇÃO: o servidor devolveu um link de outro endereço ({0}); por segurança não abro.\n", publicada.Url));
            return null;
        }

        editor.WriteMessage(
            Tr.F("\n3D {0}, publicado em {1:0.0} s.\n", Resumo(cena), relogio.Elapsed.TotalSeconds)
            + Tr.F("  Link: {0}\n", publicada.Url)
            + (publicada.ExpiresAt is { } expira ? Tr.F("  Vale até {0:dd/MM/yyyy HH:mm}.\n", expira.ToLocalTime()) : string.Empty));

        return publicada.Url;
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
        return new Scene3D(string.IsNullOrWhiteSpace(titulo) ? Tr.T("Usina") : titulo, grade, faces, pilares, arvores, sombras);
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
