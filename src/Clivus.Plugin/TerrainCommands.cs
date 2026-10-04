using System.Diagnostics;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.TerrainCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Seção Terreno da aba Clivus Solar.
///
/// Casca fina: lê as superfícies do desenho, entrega a lista pronta ao
/// usuário e conta o que ele escolheu. O processamento da superfície é o
/// passo 1.4.
/// </summary>
public static class TerrainCommands
{
    /// <summary>
    /// CLIVUS_TERRENO: lista as superfícies do desenho para o usuário escolher
    /// qual é o terreno.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTerreno)]
    public static void Terreno()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var varredura = SurfaceReader.Read(documento.Database);

            // A ordenação carrega o identificador junto: sem ele, duas
            // superfícies de mesmo nome e mesmo tamanho ficariam
            // indistinguíveis, e a escolha do usuário não apontaria para nada.
            var lista = SurfaceCatalog.Organize(varredura.Surfaces, e => e.Summary);

            var motivo = SurfaceCatalog.WhyNothingToChoose(
                lista.Select(e => e.Summary).ToArray(),
                varredura.HasExternalReference);

            if (motivo is not null)
            {
                editor.WriteMessage($"\n{motivo}\n");
                return;
            }

            // Sem interface (Core Console), a lista vai para a linha de
            // comando: é o que torna este comando testável sem ninguém clicar.
            if (!ClivusExtension.TemInterface())
            {
                // Sem interface não há como confirmar nada, e escolher sozinho
                // seria o comando do produto se comportando de um jeito no
                // Civil 3D e de outro em lote. Quem processa sem perguntar é
                // CLIVUS_TERRENO_AUTO, um comando à parte.
                Listar(editor, lista);
                return;
            }

            Escolher(editor, lista);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar as superfícies do desenho.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ler as superfícies deste desenho: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_TERRENO_AUTO: lista e processa a primeira superfície aproveitável,
    /// sem perguntar.
    ///
    /// Existe para o teste de nível 2 alcançar o processamento, que é o miolo
    /// do passo 1.4 e só existe do lado do CAD. Fica num comando separado de
    /// propósito: teste não pode ditar o comportamento do comando que o
    /// usuário usa.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTerrenoAutomatico)]
    public static void TerrenoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var varredura = SurfaceReader.Read(documento.Database);
            var lista = SurfaceCatalog.Organize(varredura.Surfaces, e => e.Summary);

            var motivo = SurfaceCatalog.WhyNothingToChoose(
                lista.Select(e => e.Summary).ToArray(),
                varredura.HasExternalReference);

            if (motivo is not null)
            {
                editor.WriteMessage($"\n{motivo}\n");
                return;
            }

            Listar(editor, lista);

            var primeira = lista.FirstOrDefault(e => e.Summary.CanBeTerrain);
            if (primeira is null) return;

            Processar(editor, primeira);
            ConferirContraOCivil3D(editor, documento, primeira);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a superfície automaticamente.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui processar a superfície: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Imprime o que o próprio Civil 3D diz da superfície, para o teste de
    /// nível 2 poder comparar com o que o motor calculou.
    ///
    /// Esta é a conferência que vale: o motor lê os triângulos e faz as contas
    /// dele; o Civil 3D responde das estatísticas dele, por um caminho
    /// independente. Comparar os dois no mesmo desenho pega o erro que a
    /// coerência interna não pega — uma leitura sistematicamente errada mantém
    /// os números do motor batendo entre si, só que sobre o terreno errado.
    ///
    /// Só no comando automático: no comando do produto isto seria ruído na
    /// tela do usuário.
    /// </summary>
    private static void ConferirContraOCivil3D(Editor editor, Document documento, SurfaceEntry entrada)
    {
        try
        {
            using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

            if (transacao.GetObject(entrada.Id, OpenMode.ForRead) is not TinSurface superficie) return;

            var gerais = superficie.GetGeneralProperties();
            var tin = superficie.GetTinProperties();

            // Formato fixo e em cultura invariante: quem lê isto é o runner do
            // teste, não o usuário.
            editor.WriteMessage(string.Format(
                CultureInfo.InvariantCulture,
                "\nCIVIL3D triangulos={0} cotaMin={1:0.000} cotaMax={2:0.000} pontos={3} "
                + "centroX={4:0.###} centroY={5:0.###}\n",
                tin.NumberOfTriangles,
                gerais.MinimumElevation,
                gerais.MaximumElevation,
                gerais.NumberOfPoints,
                (gerais.MinimumCoordinateX + gerais.MaximumCoordinateX) / 2,
                (gerais.MinimumCoordinateY + gerais.MaximumCoordinateY) / 2));

            transacao.Commit();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler as estatísticas do Civil 3D.", erro);
            editor.WriteMessage("\nCIVIL3D indisponivel\n");
        }
    }

    /// <summary>
    /// CLIVUS_TERRENO_STATUS: diz se o terreno gravado neste desenho ainda
    /// corresponde à superfície como ela está agora.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTerrenoStatus)]
    public static void TerrenoStatus()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var carimbo = ProvenanceStore.Load(documento.Database);

            if (carimbo is null)
            {
                editor.WriteMessage(Tr.T("\nSTATUS SemCarimbo: este desenho ainda não teve terreno processado.\n"));
                return;
            }

            var agora = TerrenoEnvelhecido.LerIdentidadeAtual(documento, carimbo.Surface.Handle);
            var estado = ProvenanceCheck.Evaluate(carimbo, agora);

            editor.WriteMessage(
                Tr.F("\nSTATUS {0}: terreno de {1}, processado em {2} pela versão {3}.\n",
                    estado, carimbo.Surface.Name, carimbo.ProcessedAtText, PluginInfo.VersaoLegivel(carimbo.PluginVersion)));

            // A localização gravada aparece aqui, e não só no processamento:
            // é o único lugar onde o usuário pode conferir o que ficou
            // guardado — e, se ele digitou errado, descobrir isso.
            MostrarLocalizacaoGravada(editor, documento);

            var aviso = ProvenanceCheck.Warning(carimbo, agora);
            if (aviso is not null) editor.WriteMessage($"{aviso}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao conferir o carimbo do terreno.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui conferir o terreno deste desenho: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Mostra a localização já gravada, e como corrigi-la.
    ///
    /// Sem isto, uma latitude digitada com o sinal trocado ficaria gravada
    /// para sempre sem o usuário ter onde ver nem como desfazer — e latitude
    /// trocada põe a usina no hemisfério errado e inverte a orientação das
    /// mesas.
    /// </summary>
    /// <summary>
    /// Mostra onde no mundo fica o terreno que acabou de ser processado.
    ///
    /// É pré-requisito da posição do sol, e portanto do azimute e de qualquer
    /// conta de sombreamento adiante. Quando o desenho não sabe responder, o
    /// comando não pergunta aqui: manda usar CLIVUS_LOCAL, que é onde a pergunta
    /// mora — junto com a correção. Dois lugares perguntando o mesmo dado
    /// acabariam com validações que divergem.
    /// </summary>
    private static void MostrarLocalizacaoDoTerreno(
        Editor editor,
        Document documento,
        Point3d pontoDoTerreno)
    {
        try
        {
            var lugar = GeoStore.Read(documento.Database, pontoDoTerreno);

            if (lugar is null)
            {
                editor.WriteMessage(
                    Tr.F("  localização:    não definida. Use {0} para informar.\n", PluginInfo.ComandoLocalizacao));
                return;
            }

            var origem = lugar.Source == GeoLocationSource.Desenho ? Tr.T("do desenho") : Tr.T("informada");
            editor.WriteMessage(Tr.F("  localização:    {0}  ({1})\n", lugar.Describe(), origem));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao obter a localização geográfica.", erro);
            editor.WriteMessage(Tr.T("  localização:    não consegui obter (ver log)\n"));
        }
    }

    private static void MostrarLocalizacaoGravada(Editor editor, Document documento)
    {
        try
        {
            var lugar = GeoStore.Gravada(documento.Database);
            if (lugar is null) return;

            var origem = lugar.Source == GeoLocationSource.Desenho ? Tr.T("do desenho") : Tr.T("informada");
            editor.WriteMessage(Tr.F("  localização:    {0}  ({1})\n", lugar.Describe(), origem));

            if (lugar.Source == GeoLocationSource.Usuario)
            {
                editor.WriteMessage(
                    Tr.F("  (para corrigir, use {0})\n", PluginInfo.ComandoLocalizacao));
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui mostrar a localização gravada.", erro);
        }
    }

    private static void GravarCarimbo(Editor editor, Document documento, SurfaceFingerprint identidade)
    {
        try
        {
            ProvenanceStore.Save(
                documento.Database,
                new ProvenanceStamp(identidade, DateTime.Now, ClivusCommands.VersaoDoPlugin()));
        }
        catch (System.Exception erro)
        {
            // O terreno já está processado e utilizável; o que se perde é a
            // capacidade de avisar depois que ele ficou velho. Vale dizer, e
            // não vale desfazer o trabalho.
            RegistroDeDiagnostico.Registrar("Não consegui gravar o carimbo de proveniência.", erro);
            editor.WriteMessage(
                Tr.T("\n  ATENÇÃO: não consegui gravar o carimbo no desenho. O terreno funciona nesta sessão, mas ao reabrir o arquivo não haverá como saber se ele envelheceu.\n"));
        }
    }

    private static void Listar(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        editor.WriteMessage(Tr.F("\nSuperfícies do desenho: {0}\n", superficies.Count));

        foreach (var superficie in superficies)
            editor.WriteMessage($"  {superficie.Summary.Describe()}\n");
    }

    private static void Escolher(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        var escolhida = EscolhaDeTerreno.Perguntar(superficies);

        if (escolhida is null)
        {
            editor.WriteMessage(Tr.T("\nNenhuma superfície escolhida.\n"));
            return;
        }

        editor.WriteMessage(Tr.F("\nTerreno escolhido: {0}\n", escolhida.Summary.Describe()));
        Processar(editor, escolhida);
    }

    /// <summary>
    /// Reprocessa, sem perguntar, a superfície que o carimbo do desenho diz
    /// ter sido processada (pelo handle, e senão pelo nome); sem carimbo, a
    /// única superfície do desenho, se houver uma só. Devolve se conseguiu.
    /// É o que faz o terreno "não se perder" ao fechar e reabrir.
    /// </summary>
    internal static bool Reprocessar(Editor editor, Document documento)
    {
        try
        {
            var carimbo = ProvenanceStore.Load(documento.Database);
            var varredura = SurfaceReader.Read(documento.Database);
            var candidatas = varredura.Surfaces.Where(e => e.Summary.CanBeTerrain).ToList();

            SurfaceEntry? escolhida = null;

            if (carimbo is not null)
            {
                // Só pelo handle. Uma superfície recriada com o mesmo nome é
                // exatamente o que o carimbo existe para denunciar; reprocessá-la
                // por baixo dos panos regravaria o carimbo e apagaria o aviso.
                escolhida = candidatas.FirstOrDefault(e => e.Id.Handle.ToString() == carimbo.Surface.Handle);

                if (escolhida is null)
                {
                    var homonima = candidatas.Any(e => e.Summary.DisplayName == carimbo.Surface.Name);

                    editor.WriteMessage(
                        (homonima
                            ? Tr.F("\nA superfície \"{0}\" processada em {1} não está mais no desenho (há outra com o mesmo nome, recriada). Use o botão Terreno para escolher a superfície e processar de novo.\n", carimbo.Surface.Name, carimbo.ProcessedAtText)
                            : Tr.F("\nA superfície \"{0}\" processada em {1} não está mais no desenho. Use o botão Terreno para escolher a superfície e processar de novo.\n", carimbo.Surface.Name, carimbo.ProcessedAtText)));

                    return false;
                }
            }
            else if (candidatas.Count == 1)
            {
                escolhida = candidatas[0];
            }

            if (escolhida is null) return false;

            editor.WriteMessage(
                (carimbo is not null
                    ? Tr.F("\nO terreno não estava na memória (o desenho foi reaberto). Reprocessando {0}, processada em {1}...\n", escolhida.Summary.DisplayName, carimbo.ProcessedAtText)
                    : Tr.F("\nO terreno não estava na memória (o desenho foi reaberto). Reprocessando {0}...\n", escolhida.Summary.DisplayName)));

            Processar(editor, escolhida);

            return TerrainCache.Get(documento) is not null;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui reprocessar o terreno registrado.", erro);
            return false;
        }
    }

    /// <summary>
    /// Lê a superfície escolhida, monta a malha e guarda. O resumo que sai
    /// daqui é o que o usuário confere contra o Civil 3D.
    /// </summary>
    private static void Processar(Editor editor, SurfaceEntry escolhida)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        // O identificador veio do banco deste documento. Se a superfície foi
        // apagada entre a escolha e agora — dá tempo, a janela fica aberta —,
        // abrir o objeto lança, e o usuário leria a mensagem genérica de "não
        // consegui ler as superfícies", que aponta para o lugar errado.
        if (escolhida.Id.IsNull || escolhida.Id.IsErased)
        {
            editor.WriteMessage(Tr.T("\nA superfície escolhida não está mais no desenho.\n"));
            TerrainCache.Forget(documento);
            return;
        }

        editor.WriteMessage(Tr.F("\nProcessando {0}...\n", escolhida.Summary.DisplayName));

        var relogio = Stopwatch.StartNew();
        SurfaceMesh lida;
        SurfaceFingerprint identidade;
        Point3d centroDoTerreno;

        // Uma transação para a leitura inteira, fechada antes de qualquer
        // conta: o motor trabalha sobre os números, não sobre o banco.
        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            if (transacao.GetObject(escolhida.Id, OpenMode.ForRead) is not TinSurface superficie)
            {
                editor.WriteMessage(Tr.T("\nA superfície escolhida não está mais no desenho.\n"));
                TerrainCache.Forget(documento);
                return;
            }

            lida = SurfaceExtractor.Extract(superficie);

            // A identidade é lida agora, com a superfície na mão, e não depois:
            // entre uma transação e outra o desenho pode mudar, e o carimbo
            // passaria a descrever um estado que nunca foi processado.
            identidade = FingerprintReader.Read(superficie);

            // O meio da superfície, para perguntar ao AutoCAD onde no mundo
            // fica este terreno. Um ponto de dentro dele, e não um ponto
            // qualquer do desenho.
            var caixa = superficie.GetGeneralProperties();
            centroDoTerreno = new Point3d(
                (caixa.MinimumCoordinateX + caixa.MaximumCoordinateX) / 2,
                (caixa.MinimumCoordinateY + caixa.MaximumCoordinateY) / 2,
                0);

            transacao.Commit();
        }

        var malha = new Tin(lida.Triangles);

        if (malha.TriangleCount == 0)
        {
            // Sem esquecer o anterior, o terreno de antes continuaria valendo,
            // e os comandos seguintes responderiam cota de uma superfície que
            // o usuário acabou de trocar.
            TerrainCache.Forget(documento);

            editor.WriteMessage(
                Tr.T("\nA superfície não tem nenhum triângulo aproveitável. Confira se ela está construída e visível no Civil 3D.\n"));
            return;
        }

        var resumo = new TerrainSummary(
            escolhida.Summary.DisplayName,
            malha.TriangleCount,
            malha.DiscardedTriangleCount,
            malha.MinZ,
            malha.MaxZ,
            malha.Area2D,
            malha.Area3D);

        TerrainCache.Store(documento, new ProcessedTerrain(escolhida.Id, malha, resumo));

        // O carimbo vai para dentro do desenho, e não para a memória: ele
        // precisa sobreviver a fechar e reabrir o arquivo. É o que permite,
        // meses depois de uma terraplenagem, o plugin dizer que o resultado
        // está velho em vez de deixar o número passar.
        GravarCarimbo(editor, documento, identidade);

        editor.WriteMessage("\n");
        foreach (var linha in resumo.Lines()) editor.WriteMessage($"{linha}\n");

        MostrarLocalizacaoDoTerreno(editor, documento, centroDoTerreno);

        if (lida.UnreadableCount > 0)
        {
            // Precisa aparecer na tela, e não só no log: uma superfície
            // sistematicamente ilegível produz um resumo de aparência normal,
            // calculado sobre um punhado de triângulos, e ninguém desconfia.
            editor.WriteMessage(
                Tr.F("  ATENÇÃO: {0} triângulo(s) não puderam ser lidos e viraram buraco no terreno.\n", lida.UnreadableCount));
        }

        editor.WriteMessage(Tr.F("  em {0:0.0} s\n", relogio.Elapsed.TotalSeconds));
    }
}
