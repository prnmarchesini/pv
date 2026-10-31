using System.IO;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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

        // Sem lugar que sirva, a cena sai sem o "local" (Viewer3DPage.Json), e o usuário fica sabendo.
        if (cena.Location is not { IsValid: true })
            editor.WriteMessage(Tr.T("\n  O desenho não tem localização (latitude e longitude): o 3D sai sem o sol e as sombras. Corrija em Terreno > Resumo > Localização.\n"));

        // O resumo só falta quando a leitura das áreas falhou (o registro tem o porquê).
        if (cena.Summary is null)
            editor.WriteMessage(Tr.T("\n  O resumo da usina não saiu no 3D (o registro de diagnóstico diz o porquê).\n"));

        AvisarTerrenoMudado(editor, documento, terreno);

        return cena;
    }

    /// <summary>
    /// Os laços da hachura no mundo: os vértices estão no plano dela (o OCS da
    /// normal, na elevação), e a matriz do plano os leva ao mundo. Laço de
    /// curvas (não polilinha) vale pelos pontos de começo delas.
    /// </summary>
    private static IEnumerable<List<Point3>> AneisDaHachura(Hatch hachura)
    {
        var plano = Matrix3d.PlaneToWorld(hachura.Normal);

        Point3 Mundo(double x, double y)
        {
            var p = new Point3d(x, y, hachura.Elevation).TransformBy(plano);
            return new Point3(p.X, p.Y, p.Z);
        }

        for (var i = 0; i < hachura.NumberOfLoops; i++)
        {
            var laco = hachura.GetLoopAt(i);
            var anel = new List<Point3>();

            if (laco.IsPolyline)
            {
                foreach (BulgeVertex v in laco.Polyline) anel.Add(Mundo(v.Vertex.X, v.Vertex.Y));
            }
            else
            {
                foreach (Curve2d curva in laco.Curves) anel.Add(Mundo(curva.StartPoint.X, curva.StartPoint.Y));
            }

            yield return anel;
        }
    }

    /// <summary>
    /// As mesas assentadas num terreno diferente do de hoje (30/10/2026: "o 3d
    /// no navegador cagou forte, no 3d do cad não tem isso"): o 3D mostra o
    /// terreno de hoje, e onde ele subiu depois que as mesas foram geradas o
    /// chão aparece por cima delas. Diz quais, e o que fazer.
    /// </summary>
    internal static void AvisarTerrenoMudado(Editor editor, Document documento, ProcessedTerrain terreno)
    {
        var mudadas = MesasNumTerrenoDiferente(documento.Database, terreno);
        if (mudadas.Count == 0) return;

        var subiu = mudadas.Max(m => m.MaxRise);
        var desceu = mudadas.Max(m => m.MaxDrop);
        var nomes = string.Join(", ", mudadas.Take(12).Select(m => m.Label)) + (mudadas.Count > 12 ? Tr.F(" e mais {0}", mudadas.Count - 12) : string.Empty);

        editor.WriteMessage(Tr.F(
            "\n  ATENÇÃO: {0} mesa(s) foram assentadas num terreno diferente do de hoje ({1}): o terreno subiu até {2:0.00} m e desceu até {3:0.00} m debaixo dos pilares delas, e no 3D o chão aparece por cima delas ou longe delas. A superfície mudou depois que elas foram geradas (ou o terreno de hoje é outra superfície). Recalcule essas mesas (janela Áreas › Recalcular) ou regere a área: {4}.\n",
            mudadas.Count, terreno.Summary.SurfaceName, subiu, desceu, nomes));
    }

    /// <summary>As mesas com pilar cuja cota gravada (o terreno do dia em que foi desenhado) difere do terreno de hoje.</summary>
    internal static IReadOnlyList<DriftedTable> MesasNumTerrenoDiferente(Database database, ProcessedTerrain terreno)
    {
        var pilares = new List<(string, double?, double?)>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
            {
                if (mesa.Identity is not { } identidade) continue;

                foreach (var id in mesa.Pillars)
                {
                    if (transacao.GetObject(id, OpenMode.ForRead) is not BlockReference bloco || LayoutXData.LoadPillar(bloco) is not { } pilar) continue;

                    var p = bloco.Position;
                    pilares.Add((identidade.Label, pilar.GroundZ, terreno.Mesh.TryGetZ(p.X, p.Y, out var z) ? z : null));
                }
            }
        }

        return TerrainDrift.Tables(pilares);
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
                if (dxf != "3DFACE" && dxf != "INSERT" && dxf != "HATCH") continue;
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
                        // A seção (42.2) é a do bloco (LayoutDrawer): a escala X é a
                        // largura ao longo da mesa, a Y a profundidade, e a rotação o giro.
                        var topo = br.Position;
                        var escala = br.ScaleFactors;
                        pilares.Add(new Scene3DPillar(topo.X, topo.Y, topo.Z, p.GroundZ ?? topo.Z - (p.FreeHeight ?? 0),
                            new Scene3DPillarSection(Math.Abs(escala.X), Math.Abs(escala.Y), br.Rotation)));
                        break;

                    case BlockReference br when LayoutXData.LoadTree(br) is { } a:
                        arvores.Add(new Scene3DTree(br.Position.X, br.Position.Y, br.Position.Z, a.Spec));
                        break;

                    // A sombra (42.1) é a hachura magenta, no chão e sobre as mesas:
                    // cada laço vira um anel no mundo. A borda passou a ser trechos
                    // abertos por pedaço, que não fecham anel.
                    case Hatch hachura when PluginXData.Load(hachura, "Sombra", 1, 1) is not null:
                        foreach (var anel in AneisDaHachura(hachura))
                            if (anel.Count >= 3) sombras.Add(anel);
                        break;
                }
            }

            foreach (var (modulo, cantos) in facesLidas)
                faces.Add(new Scene3DFace(cantos, cores.TryGetValue(modulo, out var c) ? c : new RgbColor(30, 60, 140)));
        }

        var titulo = Path.GetFileNameWithoutExtension(documento.Name);
        // O carport (36.1) vai junto com as canaletas: faces prontas, na cor de cada peça.
        var extras = Canaletas(database, terreno);
        extras.AddRange(CarportCommands.Faces3D(database, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null));

        // A bifila (39.1): as vigas, mãos francesas, terças e tirantes, com
        // as faces lidas dos próprios sólidos do desenho.
        extras.AddRange(EstruturaDaBifila(database));

        return new Scene3D(string.IsNullOrWhiteSpace(titulo) ? Tr.T("Usina") : titulo, grade, faces, pilares, arvores, sombras, extras, Lugar(documento), ResumoDaUsina(documento));
    }

    /// <summary>
    /// O lugar do desenho, para o sol do 3D (42.2): a latitude e a longitude
    /// do terreno e o fuso do relógio, os mesmos das sombras do desenho; null
    /// sem localização (a página fica com a luz de sempre).
    /// </summary>
    private static Scene3DLocation? Lugar(Document documento) =>
        SombrasCommands.Lugar(documento) is { IsValid: true } lugar
            ? new Scene3DLocation(lugar.Latitude, lugar.Longitude, SombrasCommands.FusoDoLugar(documento))
            : null;

    /// <summary>
    /// O resumo da usina para o quadro do 3D (42.2): a conta da janela Áreas e
    /// do Recontar. Se a leitura falhar, o 3D sai sem o quadro (e o registro
    /// de diagnóstico guarda o porquê).
    /// </summary>
    private static Scene3DSummary? ResumoDaUsina(Document documento)
    {
        try
        {
            return Scene3DSummary.From(AreasDoDesenho.Ler(documento).Resumo);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao montar o resumo do 3D.", erro);
            return null;
        }
    }

    /// <summary>
    /// As peças da bifila (39.1): cada sólido com XData "Estrutura" vira a
    /// barra dele em triângulos, montada no Core (<see cref="BarMesh"/>) pelo
    /// eixo e pela seção gravados na peça, sem as tampas, na cor do aço. A
    /// peça movida ou copiada aparece onde está: o centro do sólido no desenho
    /// menos o meio do eixo gravado é o quanto ela andou. Explodir o sólido,
    /// como no começo, eram 13 mil sólidos explodidos numa usina de 500 mesas.
    /// </summary>
    private static List<Scene3DFace> EstruturaDaBifila(Database database)
    {
        var faces = new List<Scene3DFace>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass.DxfName != "3DSOLID") continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Solid3d solido || LayoutXData.LoadStructure(solido) is not { } peca) continue;

            var caixa = solido.GeometricExtents;
            var andou = new Point3(
                (caixa.MinPoint.X + caixa.MaxPoint.X) / 2 - (peca.Start.X + peca.End.X) / 2,
                (caixa.MinPoint.Y + caixa.MaxPoint.Y) / 2 - (peca.Start.Y + peca.End.Y) / 2,
                (caixa.MinPoint.Z + caixa.MaxPoint.Z) / 2 - (peca.Start.Z + peca.End.Z) / 2);

            Point3 Levar(Point3 p) => new(p.X + andou.X, p.Y + andou.Y, p.Z + andou.Z);

            faces.AddRange(BarMesh.Box(Levar(peca.Start), Levar(peca.End), peca.Width, peca.Depth, peca.DepthDirection, CarportMesh.CorDoAco, caps: false));
        }

        return faces;
    }

    /// <summary>As canaletas da drenagem (29.3): a mesma geometria do sólido do desenho, em triângulos cinza-concreto.</summary>
    private static List<Scene3DFace> Canaletas(Database database, ProcessedTerrain terreno)
    {
        var faces = new List<Scene3DFace>();
        var configuracao = DrenagemStore.Ler(database);
        var concreto = new RgbColor(175, 175, 170);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var linha in LinhasDeDrenagem.Todas(transacao, database).Where(l => l.Identidade.IsChannel))
        {
            var (_, canaleta) = LinhasDeDrenagem.Perfil(terreno.Mesh, configuracao, linha);
            if (canaleta is null) continue;

            foreach (var (a, b, c) in ChannelSolid.Triangles(ChannelSolid.Rings(canaleta.Points, linha.Identidade.Section!)))
                faces.Add(new Scene3DFace([a, b, c], concreto));
        }

        return faces;
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
