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
/// (plano/contrato-servidor-3d.md). Desde o 42.10, antes de tudo, a janela
/// pergunta o nome da usina, que fica gravado no desenho (<see cref="NomeDaUsina"/>).
/// </summary>
public static class Ver3DCommands
{
    [CommandMethod(PluginInfo.ComandoVer3D)]
    public static void Ver3D()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        // A janela "Exportando o 3D para a web" (31/10/2026, Renan: "Comando
        // clivus 3D trava gostoso [...] tela de exportando para web... com a %
        // de carregamento e opção de cancelar, sem MATAR MEU CAD"): a leitura
        // do desenho anda na linha do AutoCAD e redesenha a janela a cada
        // passo; a cena, a compressão e o envio vão para outra linha.
        JanelaDeProgresso? janela = null;

        try
        {
            // O nome da usina (42.10), antes de tudo: vai no título da página,
            // no link e na área do cliente, e fica gravado no desenho.
            var titulo = NomeDaUsina.Atual(documento);

            if (ClivusExtension.TemInterface())
            {
                if (NomeDaUsina.Perguntar(documento) is not { } nome)
                {
                    editor.WriteMessage(Tr.T("\n3D cancelado: nada foi publicado.\n"));
                    return;
                }

                titulo = nome;

                try
                {
                    janela = JanelaDeProgresso.Abrir(null, Tr.T("Exportando o 3D para a web"), podeCancelar: true);
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("Falha ao abrir o andamento do 3D.", erro);
                    janela = null;
                }
            }

            void Andar(double porcento, string texto)
            {
                if (janela is null) return;
                janela.Avancar(porcento, texto);
                if (janela.Cancelado) throw new OperationCanceledException();
            }

            var cena = Montar(documento, titulo, Andar);
            if (cena is null) return;

            // Com o servidor 3D configurado, a usina vai para ele e o link
            // abre (no PC e, mandado, no celular). Sem servidor, ou se ele
            // falhar, a página local de sempre.
            if (Publicador3D.Endereco is not null)
            {
                var link = Publicar(documento, cena, janela);
                if (link is not null)
                {
                    Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                    editor.WriteMessage(Tr.T("  Aberta no navegador. O link abre em qualquer aparelho, inclusive no celular.\n"));
                    return;
                }

                editor.WriteMessage(Tr.T("  Gravo a página no PC, como antes.\n"));
            }

            Andar(95, Tr.T("Gravando a página no PC..."));
            var caminho = Gravar(documento, cena, Caminho(documento));
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            editor.WriteMessage(Tr.T("  Aberta no navegador padrão. O arquivo pode ser mandado por e-mail: abre sem internet.\n"));
        }
        catch (OperationCanceledException)
        {
            editor.WriteMessage(Tr.T("\n3D cancelado: nada foi publicado.\n"));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar o 3D.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar o 3D: {0}\n", erro.Message));
        }
        finally
        {
            try
            {
                janela?.Fechar();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao fechar o andamento do 3D.", erro);
            }
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

            if (Montar(documento, NomeDaUsina.Atual(documento)) is { } cena) Gravar(documento, cena, caminho.StringResult.Trim().Trim('"'));
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
            if (Montar(documento, NomeDaUsina.Atual(documento)) is { } cena) Publicar(documento, cena);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao publicar o 3D (automático).", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui publicar o 3D: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_3D_NOME_AUTO: o OK da janela do nome (42.10), pela linha de
    /// comando: grava o nome da usina no desenho como a janela grava. Para o nível 2.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoVer3DNomeAutomatico)]
    public static void Ver3DNomeAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var resposta = editor.GetString(new PromptStringOptions(Tr.T("\nNome da usina no 3D: ")) { AllowSpaces = true });
            if (resposta.Status != PromptStatus.OK) return;

            var nome = NomeDaUsina.Confirmar(documento, resposta.StringResult, out var gravado);
            editor.WriteMessage($"\nCLIVUS_3D_NOME nome=[{nome}] gravado={(gravado ? 1 : 0)} atual=[{NomeDaUsina.Atual(documento)}]\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gravar o nome da usina (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gravar o nome da usina: {0}\n", erro.Message));
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

    /// <summary>
    /// A cena do desenho, ou null (sem terreno, com a mensagem). <paramref name="titulo"/>:
    /// o nome da usina (42.10). <paramref name="andar"/>: a porcentagem e o que está sendo lido (e o Cancelar).
    /// </summary>
    private static Scene3D? Montar(Document documento, string titulo, Action<double, string>? andar = null)
    {
        var editor = documento.Editor;

        var terreno = FileiraCommands.ExigirTerreno(editor, documento);
        if (terreno is null) return null;

        var (cena, modulosFora, pilaresFora) = Viewer3DPage.OnTerrain(Cena(documento, terreno, titulo, andar), (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null);
        andar?.Invoke(58, Tr.T("Conferindo o terreno debaixo das mesas..."));

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
            "{0} módulo(s), {1} pilar(es), {2} árvore(s), terreno em grade de {3:0.#} m",
            cena.Faces.Count, cena.Pillars.Count, cena.Trees.Count, cena.Terrain!.Step);
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

    /// <summary>
    /// Publica no servidor 3D. O link, ou null (com o erro dito). Com a
    /// <paramref name="janela"/> de andamento, a cena em JSON, a compressão e o
    /// envio correm em outra linha, e esta (a do AutoCAD) só redesenha a janela
    /// e repassa o Cancelar (<see cref="OperationCanceledException"/>).
    /// </summary>
    private static string? Publicar(Document documento, Scene3D cena, JanelaDeProgresso? janela = null)
    {
        var editor = documento.Editor;
        var relogio = Stopwatch.StartNew();
        var versao = PluginInfo.VersaoLegivel(ClivusCommands.VersaoDoPlugin());

        // O que a outra linha conta, lido aqui a cada volta.
        var fracao = 0.0;
        var texto = Tr.T("Montando a cena 3D...");
        using var cancelar = new CancellationTokenSource();

        var envio = Task.Run(() => Publicador3D.PublicarAsync(Viewer3DPage.PublishBody(cena, versao, cena.Title), (f, t) =>
        {
            Volatile.Write(ref fracao, f);
            Volatile.Write(ref texto, t);
        }, cancelar.Token));

        while (!envio.IsCompleted)
        {
            if (janela is not null)
            {
                janela.Avancar(60 + 38 * Volatile.Read(ref fracao), Volatile.Read(ref texto));
                if (janela.Cancelado) cancelar.Cancel();
            }

            envio.Wait(40);
        }

        // Cancelado na janela: o envio para no próximo pedaço e nada fica publicado.
        if (cancelar.IsCancellationRequested) throw new OperationCanceledException();

        var (publicada, erro) = envio.GetAwaiter().GetResult();
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
            + Tr.F("  Nome no 3D: {0}\n", cena.Title)
            + Tr.F("  Link: {0}\n", publicada.Url)
            + (publicada.ExpiresAt is { } expira ? Tr.F("  Vale até {0:dd/MM/yyyy HH:mm}.\n", expira.ToLocalTime()) : string.Empty));

        return publicada.Url;
    }

    private static Scene3D Cena(Document documento, ProcessedTerrain terreno, string titulo, Action<double, string>? andar = null)
    {
        var database = documento.Database;
        andar?.Invoke(2, Tr.T("Lendo o terreno..."));

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

        // A grade continua (o visualizador antigo e a caixa da cena) e o TIN vai
        // junto (42.3): a grade de 2,8 m cobria o canto de módulos no pé do talude.
        var grade = Viewer3DPage.SampleTerrain(minX, minY, maxX, maxY, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null)
            with { Tin = Scene3DTin.From(terreno.Mesh.Triangles) };

        var faces = new List<Scene3DFace>();
        var pilares = new List<Scene3DPillar>();
        var arvores = new List<Scene3DTree>();
        andar?.Invoke(8, Tr.T("Lendo os módulos, os pilares e as árvores..."));

        // A análise de sombras do CAD (42.3): o momento e a fração de cada
        // módulo com sombra, e a cor de antes dos pintados pela versão antiga.
        var motivos = SombrasCommands.MotivosDoDesenho(database)
            .Where(m => m.When is not null && m.Fraction > 0)
            .GroupBy(m => m.Handle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.MaxBy(m => m.Fraction)!, StringComparer.OrdinalIgnoreCase);
        var momentos = motivos.Values
            .GroupBy(m => Minuto(m.When!.Value))
            .OrderBy(g => g.Key)
            .Select(g => new Scene3DMoment(g.Key, g.Count()))
            .ToList();
        var posicaoDoMomento = momentos.Select((m, i) => (m.When, i)).ToDictionary(x => x.When, x => x.i);
        var antesDaPintura = SombrasCommands.CoresDeAntesDaPintura(database);

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            var cores = new Dictionary<Guid, RgbColor>();
            var sombraDoModulo = new Dictionary<Guid, Scene3DFaceShadow>();
            var facesLidas = new List<(Guid Modulo, List<Point3> Cantos)>();
            var total = Math.Max(1, espaco.Cast<ObjectId>().Count());
            var lidas = 0;

            foreach (ObjectId id in espaco)
            {
                if (++lidas % 4000 == 0) andar?.Invoke(8 + 30.0 * lidas / total, Tr.F("Lendo os módulos, os pilares e as árvores ({0:N0} de {1:N0} objetos)...", lidas, total));
                if (id.IsErased) continue;
                var dxf = id.ObjectClass.DxfName;
                if (dxf != "3DFACE" && dxf != "INSERT") continue;
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

                using (var dados = e.GetXDataForApplication(PluginXData.Aplicativo))
                    if (dados is null) continue;

                switch (e)
                {
                    case Face face when LayoutXData.LoadFace(face) is { } f:
                        facesLidas.Add((f.Module, Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList()));
                        break;

                    case BlockReference br when LayoutXData.LoadModule(br) is { } m:
                        var handle = br.Handle.ToString();
                        cores.TryAdd(m.Id, antesDaPintura.TryGetValue(handle, out var antes) ? Cor(transacao, br, antes) : Cor(transacao, br));
                        if (motivos.TryGetValue(handle, out var motivo) && posicaoDoMomento.TryGetValue(Minuto(motivo.When!.Value), out var momento))
                            sombraDoModulo.TryAdd(m.Id, new Scene3DFaceShadow(momento, motivo.Fraction));
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
                }
            }

            foreach (var (modulo, cantos) in facesLidas)
                faces.Add(new Scene3DFace(cantos, cores.TryGetValue(modulo, out var c) ? c : new RgbColor(30, 60, 140), sombraDoModulo.GetValueOrDefault(modulo)));
        }

        andar?.Invoke(38, Tr.T("Lendo as canaletas e o carport..."));
        // O carport (36.1) vai junto com as canaletas: faces prontas, na cor de cada peça.
        var extras = Canaletas(database, terreno);
        extras.AddRange(CarportCommands.Faces3D(database, (x, y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null));

        // A bifila (39.1): as vigas, mãos francesas, terças e tirantes, com
        // as faces lidas dos próprios sólidos do desenho.
        extras.AddRange(EstruturaDaBifila(database, andar));
        andar?.Invoke(55, Tr.T("Lendo o resumo das áreas..."));

        // As hachuras de sombra do CAD não vão (31/10/2026, Renan: "Nao quero que
        // leve o magenta no 3d web, a web ja tem sombra propria"): a sombra do 3D é
        // a do sol, calculada no navegador; da análise vão só os momentos.
        return new Scene3D(string.IsNullOrWhiteSpace(titulo) ? Tr.T("Usina") : titulo, grade, faces, pilares, arvores, [], extras, Lugar(documento), ResumoDaUsina(documento),
            momentos);
    }

    /// <summary>O instante no minuto (o momento da análise vai até o minuto).</summary>
    private static DateTime Minuto(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Unspecified);

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
    private static List<Scene3DFace> EstruturaDaBifila(Database database, Action<double, string>? andar = null)
    {
        var faces = new List<Scene3DFace>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var total = Math.Max(1, espaco.Cast<ObjectId>().Count());
        var lidas = 0;

        foreach (ObjectId id in espaco)
        {
            if (++lidas % 4000 == 0) andar?.Invoke(40 + 15.0 * lidas / total, Tr.T("Lendo a estrutura do biposte..."));
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
    private static RgbColor Cor(Transaction transacao, Entity e) => Cor(transacao, e, e.Color);

    /// <summary>A cor dada na peça (a de antes da pintura antiga, 42.3), ou a da camada quando é "por camada".</summary>
    private static RgbColor Cor(Transaction transacao, Entity e, Autodesk.AutoCAD.Colors.Color cor)
    {
        if (cor.IsByLayer || cor.IsByBlock)
        {
            if (transacao.GetObject(e.LayerId, OpenMode.ForRead) is LayerTableRecord camada) cor = camada.Color;
        }

        var valor = cor.ColorValue;
        return new RgbColor(valor.R, valor.G, valor.B);
    }
}
