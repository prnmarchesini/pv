using System.Diagnostics;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.TerrainCommands))]

namespace UFV.Plugin;

/// <summary>
/// Seção Terreno da aba UFV.
///
/// Casca fina: lê as superfícies do desenho, entrega a lista pronta ao
/// usuário e conta o que ele escolheu. O processamento da superfície é o
/// passo 1.4.
/// </summary>
public static class TerrainCommands
{
    /// <summary>
    /// UFV_TERRENO: lista as superfícies do desenho para o usuário escolher
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
            if (!UfvExtension.TemInterface())
            {
                // Sem interface não há como confirmar nada, e escolher sozinho
                // seria o comando do produto se comportando de um jeito no
                // Civil 3D e de outro em lote. Quem processa sem perguntar é
                // UFV_TERRENO_AUTO, um comando à parte.
                Listar(editor, lista);
                return;
            }

            Escolher(editor, lista);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar as superfícies do desenho.", erro);
            editor.WriteMessage($"\nNão consegui ler as superfícies deste desenho: {erro.Message}\n");
        }
    }

    /// <summary>
    /// UFV_TERRENO_AUTO: lista e processa a primeira superfície aproveitável,
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
            editor.WriteMessage($"\nNão consegui processar a superfície: {erro.Message}\n");
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
                "\nCIVIL3D triangulos={0} cotaMin={1:0.000} cotaMax={2:0.000} pontos={3}\n",
                tin.NumberOfTriangles,
                gerais.MinimumElevation,
                gerais.MaximumElevation,
                gerais.NumberOfPoints));

            transacao.Commit();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler as estatísticas do Civil 3D.", erro);
            editor.WriteMessage("\nCIVIL3D indisponivel\n");
        }
    }

    private static void Listar(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        editor.WriteMessage($"\nSuperfícies do desenho: {superficies.Count}\n");

        foreach (var superficie in superficies)
            editor.WriteMessage($"  {superficie.Summary.Describe()}\n");
    }

    private static void Escolher(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        var escolhida = EscolhaDeTerreno.Perguntar(superficies);

        if (escolhida is null)
        {
            editor.WriteMessage("\nNenhuma superfície escolhida.\n");
            return;
        }

        editor.WriteMessage($"\nTerreno escolhido: {escolhida.Summary.Describe()}\n");
        Processar(editor, escolhida);
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
            editor.WriteMessage("\nA superfície escolhida não está mais no desenho.\n");
            TerrainCache.Forget(documento);
            return;
        }

        editor.WriteMessage($"\nProcessando {escolhida.Summary.DisplayName}...\n");

        var relogio = Stopwatch.StartNew();
        SurfaceMesh lida;

        // Uma transação para a leitura inteira, fechada antes de qualquer
        // conta: o motor trabalha sobre os números, não sobre o banco.
        using (var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction())
        {
            if (transacao.GetObject(escolhida.Id, OpenMode.ForRead) is not TinSurface superficie)
            {
                editor.WriteMessage("\nA superfície escolhida não está mais no desenho.\n");
                TerrainCache.Forget(documento);
                return;
            }

            lida = SurfaceExtractor.Extract(superficie);
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
                "\nA superfície não tem nenhum triângulo aproveitável. "
                + "Confira se ela está construída e visível no Civil 3D.\n");
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

        editor.WriteMessage("\n");
        foreach (var linha in resumo.Lines()) editor.WriteMessage($"{linha}\n");

        if (lida.UnreadableCount > 0)
        {
            // Precisa aparecer na tela, e não só no log: uma superfície
            // sistematicamente ilegível produz um resumo de aparência normal,
            // calculado sobre um punhado de triângulos, e ninguém desconfia.
            editor.WriteMessage(
                $"  ATENÇÃO: {lida.UnreadableCount} triângulo(s) não puderam ser lidos "
                + "e viraram buraco no terreno.\n");
        }

        editor.WriteMessage($"  em {relogio.Elapsed.TotalSeconds:0.0} s\n");
    }
}
