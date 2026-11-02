using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.TermoVisionCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A ponte com o TermoVision (etapa 33, <c>plano/termovision</c>):
/// <list type="bullet">
/// <item>Abrir (33.6): o endereço clivus:// (da pasta de entrada ou colado) →
/// resumo → pacote → superfície, orto, área, árvores e localização no desenho.</item>
/// <item>Exportar (33.7): as mesas do desenho → <c>POST /retorno</c>.</item>
/// </list>
/// </summary>
public static class TermoVisionCommands
{
    /// <summary>
    /// CLIVUS_TV, o botão único: chegou uma área pelo navegador, ou o desenho
    /// ainda não veio do TermoVision, abre; senão manda as mesas. Abrir outra
    /// área num desenho que já veio de lá continua no CLIVUS_TV_ABRIR.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTermoVision)]
    public static void TermoVision()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        // Desde 02/11/2026 (o projeto sem envio): o desenho com mesas que não
        // veio do TermoVision também vai para lá, como uma usina pública.
        if (EntradaDoTermoVision.Pendente is not null)
            Abrir();
        else if (TermoVisionStore.Load(documento.Database) is not null)
            Exportar();
        else if (TemMesas(documento.Database) && Confirmar(documento.Editor, Tr.T(
                     "Este desenho não veio do TermoVision. Criar lá uma usina pública com as mesas e o terreno dele (o link vale 90 dias sem acesso)? Não: abrir uma área do TermoVision.")))
            CriarProjeto(documento);
        else
            Abrir();
    }

    /// <summary>CLIVUS_TV_ABRIR: abre uma área do TermoVision neste desenho.</summary>
    [CommandMethod(PluginInfo.ComandoTermoVisionAbrir)]
    public static void Abrir()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            // O que chegou pelo navegador vale antes de pedir o código colado.
            var link = EntradaDoTermoVision.TomarPendente();
            RegistroDeDiagnostico.Registrar(link is null
                ? "CLIVUS_TV_ABRIR: nada na pasta de entrada; pedindo o código colado."
                : $"CLIVUS_TV_ABRIR: {link} da pasta de entrada.");
            link ??= PedirEndereco(editor);
            if (link is null) return;

            // A pergunta é uma janela; um endereço que chega enquanto ela está
            // aberta (outro clique no navegador) toma o lugar e é perguntado.
            while (true)
            {
                editor.WriteMessage(Tr.F("\nTermoVision: conferindo o envio {0}...\n", link.Shipment));
                TermoVisionSummary? resumo;
                string? erroResumo;
                using (var consulta = new TermoVisionClient(link))
                    (resumo, erroResumo) = Esperar(() => consulta.SummaryAsync());
                if (resumo is null)
                {
                    RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} falhou no resumo: {erroResumo}");
                    Avisar(editor, erroResumo!);
                    return;
                }

                // O servidor na pergunta: um endereço clivus:// pode vir de qualquer página.
                var pergunta = Tr.F("Abrir a área \"{0}\" da usina \"{1}\" (EPSG {2}, cerca de {3:0.0} MB), de {4}, neste desenho?",
                    resumo.AreaName, resumo.PlantName, resumo.Epsg, resumo.EstimatedMb, new Uri(link.Server).Host);

                // Um envio por desenho: o Exportar manda todas as mesas do desenho
                // para o envio guardado nele. O desenho que já recebeu uma área
                // pergunta antes de trocar; nunca trava (teste do Renan, 28/10:
                // apagou o desenho sem querer e o Abrir recusava a mesma área).
                var padraoSim = true;
                if (TermoVisionStore.Load(documento.Database) is { } anterior)
                {
                    var mesmaArea = anterior.Shipment == link.Shipment
                        || (string.Equals(anterior.PlantName, resumo.PlantName, StringComparison.Ordinal)
                            && string.Equals(anterior.AreaName, resumo.AreaName, StringComparison.Ordinal));
                    RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} num desenho que já tem o envio {anterior.Shipment} (mesma área: {mesmaArea}).");
                    if (mesmaArea)
                        pergunta = Tr.F("Este desenho já recebeu a área \"{0}\". Desenhar de novo? O que ainda estiver no desenho fica; o terreno novo ganha outro nome.",
                            anterior.AreaName) + "\n\n" + pergunta;
                    else
                    {
                        pergunta = Tr.F("Este desenho é da área \"{0}\" da usina \"{1}\". Trocar? O Exportar passa a mandar todas as mesas do desenho para a área nova; para manter as duas, use um desenho novo.",
                            anterior.AreaName, anterior.PlantName) + "\n\n" + pergunta;
                        padraoSim = false;
                    }
                }

                var sim = Confirmar(editor, pergunta, padraoSim);

                if (EntradaDoTermoVision.Tomar() is { } novo)
                {
                    RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} substituído por {novo}, que chegou durante a pergunta.");
                    link = novo;
                    continue;
                }

                if (!sim)
                {
                    RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} recusado pela pessoa.");
                    return;
                }

                RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} aceito; baixando.");
                break;
            }

            using var cliente = new TermoVisionClient(link);

            editor.WriteMessage(Tr.T("\nTermoVision: baixando o pacote...\n"));
            var (zip, erroZip) = Esperar(() => cliente.PackageAsync());
            if (zip is null)
            {
                RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} falhou no pacote: {erroZip}");
                Avisar(editor, erroZip!);
                return;
            }

            if (!TermoVisionPackage.TryRead(new MemoryStream(zip), out var pacote, out var erroPacote))
            {
                RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} pacote ilegível: {erroPacote}");
                Avisar(editor, erroPacote!);
                return;
            }

            if (pacote!.Shipment.Length > 0 && pacote.Shipment != link.Shipment)
            {
                RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} recebeu o pacote do envio {pacote.Shipment}.");
                Avisar(editor, Tr.T("O pacote que chegou é de outro envio. Nada foi desenhado."));
                return;
            }

            TermoVisionStore.SaveToken(link);
            Montar(documento, pacote, link.Server, link.Shipment);
            RegistroDeDiagnostico.Registrar($"CLIVUS_TV_ABRIR: {link} aberto no desenho.");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a área do TermoVision.", erro);
            Avisar(editor, Tr.F("Não consegui abrir a área do TermoVision: {0}", erro.Message));
        }
    }

    /// <summary>
    /// Escreve na linha de comando e, com interface, mostra numa janela à vista:
    /// quem veio do navegador não está olhando para a linha de comando.
    /// </summary>
    private static void Avisar(Editor editor, string texto)
    {
        editor.WriteMessage($"\n{texto}\n");
        if (ClivusExtension.TemInterface()) Janela(texto, 0x40 /* MB_ICONINFORMATION */ | 0x0 /* MB_OK */);
    }

    // MessageBox do Windows dona da janela do AutoCAD, em primeiro plano:
    // aparece por cima mesmo com o navegador na frente.
    private static int Janela(string texto, uint tipo)
    {
        var dona = AcadApp.MainWindow.Handle;
        SetForegroundWindow(dona);
        return MessageBoxW(dona, texto, "Clivus Solar · TermoVision", tipo | 0x10000 /* MB_SETFOREGROUND */ | 0x40000 /* MB_TOPMOST */);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr dona, string texto, string titulo, uint tipo);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr janela);

    /// <summary>
    /// CLIVUS_TV_ABRIR_AUTO: o teste de nível 2. Monta a partir de um pacote
    /// .zip no disco, sem rede e sem pergunta.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTermoVisionAbrirAutomatico)]
    public static void AbrirAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        var caminho = editor.GetString(new PromptStringOptions("\nPacote .zip: ") { AllowSpaces = true });
        if (caminho.Status != PromptStatus.OK) return;

        try
        {
            using var arquivo = File.OpenRead(caminho.StringResult.Trim().Trim('"'));
            if (!TermoVisionPackage.TryRead(arquivo, out var pacote, out var erro))
            {
                editor.WriteMessage($"\nCLIVUS_TV ERRO {erro}\n");
                return;
            }
            Montar(documento, pacote!, pacote!.Server, pacote.Shipment);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_TV_ABRIR_AUTO.", erro);
            editor.WriteMessage($"\nCLIVUS_TV ERRO {erro.Message}\n");
        }
    }

    /// <summary>CLIVUS_TV_EXPORTAR: manda as mesas do desenho ao TermoVision, no envio que o desenho abriu.</summary>
    [CommandMethod(PluginInfo.ComandoTermoVisionExportar)]
    public static void Exportar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            // Sem envio, a usina pública (o projeto sem envio, 02/11/2026); o
            // "Não" segue para o colar o código de um envio, como antes.
            if (TermoVisionStore.Load(documento.Database) is null && Confirmar(editor, Tr.T(
                    "Este desenho não veio do TermoVision. Criar lá uma usina pública com as mesas e o terreno dele (o link vale 90 dias sem acesso)? Não: colar o código de um envio.")))
            {
                CriarProjeto(documento);
                return;
            }

            var link = EnvioDoDesenho(documento);
            if (link is null) return;

            var mesas = LerMesas(editor, documento.Database, out var recusadas, out var faltaCarport);

            // Mandar menos vãos do que o desenho tem apagaria os outros lá
            // (28/10/2026: sem o terreno, o carport saía com 0 mesas e o
            // TermoVision ficou sem nenhuma).
            if (faltaCarport > 0)
            {
                editor.WriteMessage(Tr.F("\nNada foi mandado: {0} vão(s) de carport não puderam ser montados (o terreno do desenho não está processado). Use o botão Terreno e exporte de novo.\n", faltaCarport));
                return;
            }

            if (mesas.Count == 0 && !Confirmar(editor, Tr.T(
                    "Não há mesa no desenho. Mandar assim apaga as mesas desta área no TermoVision (o Desfazer de lá as traz). Mandar?"), padraoSim: false))
                return;

            var corpo = TermoVisionReturn.Build(ClivusCommands.VersaoDoPlugin(), NomeDoDesenho(documento), mesas, out var invalidas);
            recusadas.AddRange(invalidas);

            // Um id por clique: o reenvio do mesmo clique não grava duas vezes lá.
            var pedido = Guid.NewGuid().ToString("N");
            editor.WriteMessage(Tr.F("\nTermoVision: mandando {0} mesa(s)...\n", mesas.Count - invalidas.Count));

            using var cliente = new TermoVisionClient(link);
            var (resultado, erro) = Esperar(() => cliente.SendAsync(corpo, pedido));
            if (resultado is null)
            {
                editor.WriteMessage($"\n{erro}\n");
                return;
            }

            editor.WriteMessage(Tr.F(
                "\nTermoVision gravou {0} mesa(s) na área \"{1}\" (operação {2}): {3} saíram, {4} fora da área.\n",
                resultado.Tables, TermoVisionStore.Load(documento.Database)?.AreaName ?? "", resultado.Operation, resultado.Removed, resultado.OutsideArea));
            foreach (var aviso in resultado.Warnings) editor.WriteMessage($"  {aviso}\n");
            if (recusadas.Count > 0)
            {
                editor.WriteMessage(Tr.F("  {0} mesa(s) ficaram de fora:\n", recusadas.Count));
                foreach (var r in recusadas.Take(20)) editor.WriteMessage($"    {r}\n");
            }

            if (resultado.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && Confirmar(editor, Tr.T("Abrir o mapa no TermoVision?")))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao exportar para o TermoVision.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui exportar para o TermoVision: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_TV_EXPORTAR_AUTO: o teste de nível 2. Grava o corpo do retorno num arquivo, sem rede.</summary>
    [CommandMethod(PluginInfo.ComandoTermoVisionExportarAutomatico)]
    public static void ExportarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        var caminho = editor.GetString(new PromptStringOptions("\nArquivo .json: ") { AllowSpaces = true });
        if (caminho.Status != PromptStatus.OK) return;

        try
        {
            var mesas = LerMesas(editor, documento.Database, out var recusadas, out var faltaCarport);
            if (faltaCarport > 0) recusadas.Add(FormattableString.Invariant($"carport: {faltaCarport} vão(s) sem montar"));
            var corpo = TermoVisionReturn.Build(ClivusCommands.VersaoDoPlugin(), NomeDoDesenho(documento), mesas, out var invalidas);
            File.WriteAllBytes(caminho.StringResult.Trim().Trim('"'), corpo);
            editor.WriteMessage(FormattableString.Invariant(
                $"\nCLIVUS_TV EXPORTADO mesas={mesas.Count - invalidas.Count} recusadas={recusadas.Count + invalidas.Count}\n"));
            foreach (var r in recusadas.Concat(invalidas)) editor.WriteMessage($"  {r}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_TV_EXPORTAR_AUTO.", erro);
            editor.WriteMessage($"\nCLIVUS_TV ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// CLIVUS_TV_PROJETO_AUTO: o teste de nível 2 do projeto sem envio. Um
    /// caminho grava o corpo nele, sem rede; "ENVIAR" manda ao servidor e
    /// escreve o link.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTermoVisionProjetoAutomatico)]
    public static void ProjetoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        var destino = editor.GetString(new PromptStringOptions("\nArquivo .json ou ENVIAR: ") { AllowSpaces = true });
        if (destino.Status != PromptStatus.OK) return;

        try
        {
            var texto = destino.StringResult.Trim().Trim('"');
            var enviar = texto.Equals("ENVIAR", StringComparison.OrdinalIgnoreCase);
            var feito = TermoVisionProjeto.Criar(documento, interativo: false, gravarEm: enviar ? null : texto);

            editor.WriteMessage(FormattableString.Invariant(
                $"\nCLIVUS_TV PROJETO mesas={feito.Mesas} recusadas={feito.Recusadas} terreno={feito.Grade} sistema={feito.Sistema} guid={feito.Guid:D}\n"));
            if (feito.Erro is { } erro) editor.WriteMessage($"\nCLIVUS_TV ERRO {erro}\n");
            if (feito.Resultado is { } r)
                editor.WriteMessage(FormattableString.Invariant(
                    $"\nCLIVUS_TV LINK {r.Url} expira={r.ExpiresAt:O} usina={r.PlantCode} nome={r.PlantName}\n"));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_TV_PROJETO_AUTO.", erro);
            editor.WriteMessage($"\nCLIVUS_TV ERRO {erro.Message}\n");
        }
    }

    /// <summary>
    /// O projeto sem envio pelo botão: monta, manda e mostra o link (na linha
    /// de comando, copiado para a área de transferência e aberto no navegador).
    /// </summary>
    private static void CriarProjeto(Document documento)
    {
        var editor = documento.Editor;
        try
        {
            var feito = TermoVisionProjeto.Criar(documento, interativo: true);
            if (feito.Erro is { } erro)
            {
                editor.WriteMessage(Tr.F("\nNão consegui criar a usina no TermoVision: {0}\n", erro));
                return;
            }

            if (feito.Resultado is not { Url: { } url } r) return;

            editor.WriteMessage(Tr.F("\nTermoVision: a usina \"{0}\" ({1}) tem {2} mesa(s) sobre o terreno ({3}). Link público: {4}\n",
                r.PlantName ?? "", r.PlantCode ?? "", feito.Mesas, feito.Grade, url));
            if (r.ExpiresAt is { } expira) editor.WriteMessage(Tr.F("  O link vale até {0:dd/MM/yyyy} sem acesso; mandar de novo atualiza a mesma usina e o mesmo link.\n", expira.ToLocalTime()));
            if (feito.Recusadas > 0) editor.WriteMessage(Tr.F("  {0} mesa(s) ficaram de fora.\n", feito.Recusadas));

            try
            {
                System.Windows.Clipboard.SetText(url);
                editor.WriteMessage(Tr.T("  O link foi copiado para a área de transferência.\n"));
            }
            catch (System.Exception falha)
            {
                RegistroDeDiagnostico.Registrar("Falha ao copiar o link do TermoVision.", falha);
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar a usina no TermoVision.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui criar a usina no TermoVision: {0}\n", erro.Message));
        }
    }

    /// <summary>Se o desenho tem alguma mesa do Clivus.</summary>
    private static bool TemMesas(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        return LayoutScan.Tables(transacao, database).Any();
    }

    /// <summary>
    /// O endereço do envio deste desenho, com o token deste PC. Sem envio, ou
    /// com o token em outro PC, pede o endereço colado (o "Copiar o código" da página).
    /// </summary>
    private static TermoVisionLink? EnvioDoDesenho(Document documento)
    {
        var editor = documento.Editor;
        var envio = TermoVisionStore.Load(documento.Database);
        if (envio is not null && TermoVisionStore.LinkOf(envio) is { } guardado) return guardado;

        editor.WriteMessage(envio is null
            ? Tr.T("\nEste desenho não veio do TermoVision. No TermoVision, exporte a área e use \"Copiar o código\".\n")
            : Tr.F("\nO acesso ao envio da área \"{0}\" não está neste computador. No TermoVision, abra o envio e use \"Copiar o código\".\n", envio.AreaName));

        var link = PedirEndereco(editor);
        if (link is null) return null;

        if (envio is not null && envio.Shipment != link.Shipment
            && !Confirmar(editor, Tr.F("Este endereço é de outro envio, não da área \"{0}\". Mandar as mesas para ele?", envio.AreaName)))
            return null;

        var mesmo = envio?.Shipment == link.Shipment;
        TermoVisionStore.SaveToken(link);
        TermoVisionStore.Save(documento.Database, new TermoVisionDrawing(link.Server, link.Shipment,
            mesmo ? envio!.PlantName : "", mesmo ? envio!.AreaName : "", mesmo ? envio!.Epsg : 0));
        return link;
    }

    internal static string NomeDoDesenho(Document documento) =>
        Path.GetFileName(documento.Database.Filename is { Length: > 0 } f && f.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ? f : documento.Name);

    /// <summary>
    /// As mesas do desenho como vão ao TermoVision. A identidade vem do XData
    /// (nunca da camada); os cantos, do contorno; colunas e fiadas, das faces
    /// dos módulos (a mesa cortada conta o que sobrou); o módulo, do perfil.
    /// </summary>
    internal static List<TermoVisionTable> LerMesas(Editor editor, Database database, out List<string> recusadas, out int faltaCarport)
    {
        recusadas = [];
        var vaosNoDesenho = new HashSet<Guid>();
        var mesas = new List<TermoVisionTable>();
        var doDesenho = MesasDoDesenho.Ler(database);
        var padrao = FileiraCommands.PerfilDaMesa(editor, silencioso: true);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var carports = CarportsDoDesenho.Ler(database);
        foreach (var (guid, partes) in LayoutScan.Tables(transacao, database))
        {
            // O vão de carport vai pelo caminho dele, com o tipo e a estrutura (abaixo).
            if (partes.Identity is { } talvez && CarportCommands.EVaoDeCarport(talvez, carports))
            {
                vaosNoDesenho.Add(guid);
                continue;
            }

            var nome = partes.Identity?.Label is { Length: > 0 } rotulo ? rotulo : guid.ToString();
            if (partes.Identity is not { } identidade || partes.Contour is not { } contornoId)
            {
                recusadas.Add(Tr.F("{0}: {1}", nome, Tr.T("a mesa perdeu o contorno.")));
                continue;
            }

            var vertices = new List<(double E, double N, double Z)>();
            if (transacao.GetObject(contornoId, OpenMode.ForRead) is Polyline3d contorno)
                foreach (ObjectId v in contorno)
                    if (!v.IsErased && transacao.GetObject(v, OpenMode.ForRead) is PolylineVertex3d vertice)
                        vertices.Add((vertice.Position.X, vertice.Position.Y, vertice.Position.Z));

            if (TermoVisionReturn.LowEdgeFirst(vertices) is not { } cantos)
            {
                recusadas.Add(Tr.F("{0}: {1}", nome, Tr.F("o contorno da mesa tem {0} vértices, não 4.", vertices.Count)));
                continue;
            }

            int colunas = 0, fiadas = 0;
            foreach (var faceId in partes.Faces)
                if (transacao.GetObject(faceId, OpenMode.ForRead) is Autodesk.AutoCAD.DatabaseServices.Entity face && LayoutXData.LoadFace(face) is { } f)
                {
                    colunas = Math.Max(colunas, f.Column + 1);
                    fiadas = Math.Max(fiadas, f.Row + 1);
                }

            // O perfil como nas análises: pelo nome gravado; sem nome (desenho sem
            // mesas cadastradas, vale a da janela), pelo comprimento do contorno.
            var perfil = DrawingTables.Find(doDesenho, identidade.ProfileName)?.Profile
                ?? FileiraCommands.PerfilDaMesaDesenhada(cantos.Select(c => new Point3(c.E, c.N, c.Z)).ToList(), padrao, identidade.ProfileName, database);

            // A estrutura vai no projeto sem envio (contrato v2): biposte quando o perfil tem os dois pilares.
            var layout = perfil.Layout;
            mesas.Add(new TermoVisionTable(guid, nome, cantos, colunas, fiadas,
                new TermoVisionModule(layout.Module.Width, layout.Module.Height, layout.HorizontalGap))
            {
                Subtype = perfil.Frame?.Bifila is not null ? TermoVisionTable.SubtipoBiposte : TermoVisionTable.SubtipoMonoposte,
            });
        }

        // O carport (36.1): cada vão vai como uma mesa.
        if (AcadApp.DocumentManager.MdiActiveDocument is { } documento && documento.Database == database)
            mesas.AddRange(CarportCommands.MesasDoTermoVision(documento));

        // Cada vão de carport do desenho tem que ter ido (o id do vão é o mesmo da mesa).
        var enviados = mesas.Select(m => m.Id).ToHashSet();
        faltaCarport = vaosNoDesenho.Count(v => !enviados.Contains(v));
        return mesas;
    }

    private static TermoVisionLink? PedirEndereco(Editor editor)
    {
        var resposta = editor.GetString(new PromptStringOptions(
            Tr.T("\nCole o endereço do TermoVision (clivus://termovision?..., o \"Copiar o código\" da página): ")) { AllowSpaces = true });
        if (resposta.Status != PromptStatus.OK) return null;

        if (!TermoVisionLink.TryParse(resposta.StringResult, out var link, out var erro))
        {
            editor.WriteMessage($"\n{erro}\n");
            return null;
        }
        return link;
    }

    /// <summary>
    /// Sim ou não. Com interface, numa janela à vista (teste do Renan, 26/10:
    /// a pergunta na linha de comando ninguém via, e o AutoCAD ficava parado
    /// nela); no Core Console, na linha de comando, para o nível 2.
    /// </summary>
    internal static bool Confirmar(Editor editor, string pergunta, bool padraoSim = true)
    {
        if (ClivusExtension.TemInterface())
        {
            // MB_YESNO | MB_ICONQUESTION; o botão padrão segue padraoSim (MB_DEFBUTTON2 = Não).
            var tipo = 0x4u | 0x20u | (padraoSim ? 0x0u : 0x100u);
            return Janela(pergunta, tipo) == 6 /* IDYES */;
        }

        var sim = Tr.T("Sim");
        var nao = Tr.T("Não");
        var opcoes = new PromptKeywordOptions($"\n{pergunta} [{sim}/{nao}] <{(padraoSim ? sim : nao)}>: ", $"{sim} {nao}") { AllowNone = true };
        var resposta = editor.GetKeywords(opcoes);
        return resposta.Status == PromptStatus.None ? padraoSim : resposta.Status == PromptStatus.OK && resposta.StringResult == sim;
    }

    // A rede começa e corre fora da thread do AutoCAD; o comando espera o resultado.
    private static T Esperar<T>(Func<Task<T>> tarefa) => Task.Run(tarefa).GetAwaiter().GetResult();

    /// <summary>Desenha o pacote no documento: superfície, orto, área, árvores, localização e o envio.</summary>
    private static void Montar(Document documento, TermoVisionPackage pacote, string servidor, string envio)
    {
        var editor = documento.Editor;
        var database = documento.Database;

        // 1. A superfície, e ela vira o terreno do Clivus (como o Processar).
        var superficie = CriarSuperficie(database, pacote);
        var entrada = SurfaceReader.Read(database).Surfaces.FirstOrDefault(s => s.Id == superficie);
        if (entrada is null)
        {
            editor.WriteMessage(Tr.T("\nA superfície do TermoVision foi criada, mas não consegui lê-la de volta.\n"));
            return;
        }
        TerrainCommands.Processar(editor, entrada);
        if (TerrainCache.Get(documento) is not { } terreno || terreno.SurfaceId != superficie)
        {
            editor.WriteMessage(Tr.T("\nA superfície do TermoVision não virou terreno; o resto não foi desenhado.\n"));
            return;
        }

        // 2. A localização: o sol, as sombras e o PVsyst dependem dela.
        if (pacote.Location is { } lugar) GeoStore.Save(database, lugar);

        // 3. O orto, por baixo de tudo.
        string? orto = null;
        if (pacote.Ortho is { } o)
        {
            try
            {
                orto = CriarOrto(database, pacote, o, terreno.Mesh.MinZ);
            }
            catch (System.Exception erro) when (erro is IOException or UnauthorizedAccessException or Autodesk.AutoCAD.Runtime.Exception)
            {
                // Sem o orto o resto serve: a área abre e a pessoa é avisada.
                RegistroDeDiagnostico.Registrar("Não consegui pôr o orto do TermoVision.", erro);
                editor.WriteMessage(Tr.F("\nO orto não foi posto ({0}); o resto segue.\n", erro.Message));
            }
        }

        // 4. A área do Clivus, assentada no terreno como no comando Área.
        // A cota de partida é a da grade: vértice que cai fora do TIN (buraco
        // sem dado) fica nela, nunca no zero.
        var pontos = pacote.Area.Select(p => new Point3d(p.E, p.N, pacote.Terrain.NearestZ(p.E, p.N) ?? terreno.Mesh.MinZ)).ToList();
        AreaCommands.Criar(editor, documento, terreno, pontos, NomeDaArea(pacote));

        // 5. As árvores, com o pé no terreno, e o contorno real de cada objeto.
        var arvores = 0;
        foreach (var objeto in pacote.Objects)
        {
            var (e, n) = objeto.Centroid;
            var z = terreno.Mesh.TryGetZ(e, n, out var cota) ? cota : objeto.GroundZ;
            ArvoreCommands.Inserir(database, TermoVisionTrees.FromObject(objeto), new Point3d(e, n, z));
            DesenharContorno(database, terreno.Mesh, objeto, z);
            arvores++;
        }

        // 6. O envio, para o Exportar saber para onde mandar.
        TermoVisionStore.Save(database, new TermoVisionDrawing(servidor, envio, pacote.PlantName, pacote.AreaName, pacote.Crs.Epsg));

        Aproximar(editor, pacote.Area);

        editor.WriteMessage(Tr.F(
            "\nTermoVision: área \"{0}\" da usina \"{1}\" aberta. Terreno com {2} pontos de {3:0.##} m ({4}), {5}, {6} árvore(s). Coordenadas em UTM, EPSG {7}.\n",
            pacote.AreaName, pacote.PlantName, pacote.Terrain.NodeCount, pacote.Terrain.Step, pacote.Terrain.Source,
            orto is null ? Tr.T("sem orto") : Tr.F("orto em {0}", orto), arvores, pacote.Crs.Epsg));
        editor.WriteMessage(Tr.T("Desenhe as mesas como sempre; depois, Exportar para o TermoVision.\n"));
        editor.WriteMessage(FormattableString.Invariant(
            $"CLIVUS_TV MONTADO superficie={pacote.Terrain.NodeCount} orto={(orto is null ? 0 : 1)} arvores={arvores} envio={envio}\n"));
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static string NomeDaArea(TermoVisionPackage pacote) =>
        string.IsNullOrWhiteSpace(pacote.AreaName) ? "TermoVision" : pacote.AreaName.Trim();

    /// <summary>
    /// A TIN dos nós com cota. O Civil 3D triangula (Delaunay); o lado máximo
    /// de triângulo, pouco acima da diagonal da célula, impede que ele feche os
    /// buracos sem dado e a borda côncava com triângulos compridos.
    /// </summary>
    private static ObjectId CriarSuperficie(Database database, TermoVisionPackage pacote)
    {
        var nome = NomeLivre(database, "TV " + NomeDaArea(pacote));
        ObjectId id;
        try
        {
            id = TinSurface.Create(database, nome);
        }
        catch (System.ArgumentException)
        {
            // Uma superfície que não é TIN (grade, volume) já tem esse nome.
            id = TinSurface.Create(database, $"{nome} {DateTime.Now:yyyyMMdd-HHmmss}");
        }

        using var transacao = database.TransactionManager.StartTransaction();
        var superficie = (TinSurface)transacao.GetObject(id, OpenMode.ForWrite);
        superficie.Description = Tr.F("TermoVision: {0}, {1} (EPSG {2}, {3})", pacote.PlantName, pacote.AreaName, pacote.Crs.Epsg, pacote.Terrain.Source);
        superficie.BuildOptions.MaximumTriangleLength = pacote.Terrain.Step * 1.5;
        superficie.BuildOptions.UseMaximumTriangleLength = true;

        var pontos = new Point3dCollection();
        foreach (var (e, n, z) in pacote.Terrain.Nodes()) pontos.Add(new Point3d(e, n, z));
        superficie.AddVertices(pontos);
        superficie.Rebuild();

        // As curvas de nível (28/10/2026): sem estilo, a TIN aparecia só com a borda.
        var cotas = pacote.Terrain.Nodes().Select(p => p.Z).ToList();
        if (cotas.Count > 0)
        {
            var (secundaria, mestra) = ContourIntervals.For(cotas.Max() - cotas.Min());
            superficie.StyleId = EstiloDeCurvas.Garantir(database, transacao, secundaria, mestra);
            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(FormattableString.Invariant(
                $"\nCLIVUS_TV CURVAS secundaria={secundaria} mestra={mestra} estilo={superficie.StyleName}\n"));
        }

        transacao.Commit();
        return id;
    }

    private static string NomeLivre(Database database, string base_)
    {
        var usados = SurfaceReader.Read(database).Surfaces.Select(s => s.Summary.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!usados.Contains(base_)) return base_;
        for (var k = 2; ; k++)
            if (!usados.Contains($"{base_} ({k})")) return $"{base_} ({k})";
    }

    /// <summary>
    /// O orto como imagem no lugar (world file). O JPEG vai para uma pasta ao
    /// lado do DWG (ou do usuário, se o desenho ainda não foi salvo): a imagem
    /// é referência externa, o DWG guarda o caminho.
    /// </summary>
    private static string? CriarOrto(Database database, TermoVisionPackage pacote, TermoVisionOrtho orto, double zDoChao)
    {
        var pasta = PastaDoOrto(database, pacote.Shipment);
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "orto.jpg");
        File.WriteAllBytes(arquivo, orto.Jpeg);

        RasterImage.EnableReactors(true);
        using var transacao = database.TransactionManager.StartTransaction();

        var dicionarioId = RasterImageDef.GetImageDictionary(database);
        if (dicionarioId.IsNull) dicionarioId = RasterImageDef.CreateImageDictionary(database);
        var dicionario = (DBDictionary)transacao.GetObject(dicionarioId, OpenMode.ForWrite);

        var nome = "TV_" + new string(pacote.Shipment.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        RasterImageDef definicao;
        if (dicionario.Contains(nome))
        {
            definicao = (RasterImageDef)transacao.GetObject(dicionario.GetAt(nome), OpenMode.ForWrite);
            definicao.SourceFileName = arquivo;
            definicao.Load();
        }
        else
        {
            definicao = new RasterImageDef { SourceFileName = arquivo };
            definicao.Load();
            dicionario.SetAt(nome, definicao);
            transacao.AddNewlyCreatedDBObject(definicao, true);
        }

        var pixels = definicao.Size;
        var (origem, u, v) = orto.World.Placement((int)Math.Round(pixels.X), (int)Math.Round(pixels.Y));

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var imagem = new RasterImage
        {
            ImageDefId = definicao.ObjectId,
            Orientation = new CoordinateSystem3d(new Point3d(origem.X, origem.Y, zDoChao), new Vector3d(u.X, u.Y, 0), new Vector3d(v.X, v.Y, 0)),
            ShowImage = true,
            Layer = LayoutLayers.Garantir(transacao, database, LayoutLayers.TermoVisionOrto),
        };
        var id = espaco.AppendEntity(imagem);
        transacao.AddNewlyCreatedDBObject(imagem, true);
        imagem.AssociateRasterDef(definicao);

        // Por baixo de tudo, para as mesas e a área aparecerem em cima.
        var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
        ordem.MoveToBottom(new ObjectIdCollection { id });

        transacao.Commit();
        return arquivo;
    }

    private static string PastaDoOrto(Database database, string envio)
    {
        var limpo = new string(envio.Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        var dwg = database.Filename;
        if (!string.IsNullOrEmpty(dwg) && string.Equals(Path.GetExtension(dwg), ".dwg", StringComparison.OrdinalIgnoreCase) && File.Exists(dwg))
            return Path.Combine(Path.GetDirectoryName(dwg)!, Path.GetFileNameWithoutExtension(dwg) + "_TermoVision", limpo);

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "termovision", limpo);
    }

    // O contorno real do objeto, assentado no terreno: a árvore do Clivus é redonda, a casa não.
    private static void DesenharContorno(Database database, Tin terreno, TermoVisionObject objeto, double zDoPe)
    {
        var tracado = objeto.Outline.Select(p => new Point3(p.E, p.N, zDoPe)).ToList();
        tracado.Add(tracado[0]);
        var assentado = Draping.Along(terreno, tracado).Vertices;
        if (assentado.Count < 3) return;

        using var transacao = database.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var linha = new Polyline3d
        {
            Closed = true,
            Layer = LayoutLayers.Garantir(transacao, database, LayoutLayers.TermoVisionObjeto, new RgbColor(40, 140, 60)),
        };
        espaco.AppendEntity(linha);
        transacao.AddNewlyCreatedDBObject(linha, true);

        // O drapeado volta ao primeiro vértice; o fechamento é do Closed.
        var vertices = assentado[0].X == assentado[^1].X && assentado[0].Y == assentado[^1].Y ? assentado.Take(assentado.Count - 1) : assentado;
        foreach (var p in vertices)
        {
            var vertice = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            linha.AppendVertex(vertice);
            transacao.AddNewlyCreatedDBObject(vertice, true);
        }
        transacao.Commit();
    }

    private static void Aproximar(Editor editor, IReadOnlyList<(double E, double N)> area)
    {
        try
        {
            var (emin, emax) = (area.Min(p => p.E), area.Max(p => p.E));
            var (nmin, nmax) = (area.Min(p => p.N), area.Max(p => p.N));
            using var vista = editor.GetCurrentView();
            vista.CenterPoint = new Point2d((emin + emax) / 2, (nmin + nmax) / 2);
            vista.ViewDirection = Vector3d.ZAxis;
            vista.Target = Point3d.Origin;
            var proporcao = vista.Width / Math.Max(vista.Height, 1e-9);
            var altura = Math.Max(nmax - nmin, (emax - emin) / Math.Max(proporcao, 1e-9)) * 1.3;
            vista.Height = altura;
            vista.Width = altura * proporcao;
            editor.SetCurrentView(vista);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui aproximar a vista da área do TermoVision.", erro);
        }
    }
}
