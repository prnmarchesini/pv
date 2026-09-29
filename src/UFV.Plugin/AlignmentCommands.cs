using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.AlignmentCommands))]

namespace UFV.Plugin;

/// <summary>
/// A linha de alinhamento: a referência de onde as fileiras de mesas começam.
///
/// O usuário traça a linha, com quantos pontos quiser, e clica de que lado
/// ficam as mesas. O lado é a parte que o desenho não carrega sozinho (uma
/// linha é simétrica) e é a que custa caro perder: a usina inteira nasce do
/// outro lado e o desenho fica perfeito.
///
/// Por isso o lado é guardado junto com o SENTIDO do traçado. "Esquerda" é
/// esquerda de quem caminha do primeiro ponto para o último, trecho a trecho;
/// a mesma linha desenhada ao contrário troca os lados.
///
/// A linha não precisa ser reta (Renan, 25/09/2026: "eu posso fazer vários
/// pontos"), e assenta no terreno como a área: a cota de cada vértice vem do
/// terreno processado, e a linha ganha vértices onde cruza o relevo (regra
/// sagrada 5).
/// </summary>
public static class AlignmentCommands
{
    /// <summary>
    /// UFV_ALINHAMENTO: traça a linha de alinhamento e guarda de que lado
    /// ficam as mesas.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoAlinhamento)]
    public static void Alinhamento()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = TerrainCache.Get(documento);
            if (terreno is null)
            {
                editor.WriteMessage(
                    "\nNenhum terreno processado neste desenho. Use o botão Terreno primeiro.\n");
                return;
            }

            var aviso = TerrenoEnvelhecido.Conferir(documento);
            if (aviso is not null)
            {
                editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");
            }

            // O rastro vive por todo o comando, e não só enquanto se traça:
            // é com a linha na tela que o usuário clica o lado. Sem ela, "de
            // que lado?" é pergunta sobre uma coisa que ele não está vendo.
            using var rastro = new RastroDoTracado();

            var pontos = Tracar(editor, rastro);
            if (pontos is null) return;

            var tracado = pontos.Select(p => new Point3(p.X, p.Y, p.Z)).ToList();

            // Conferido aqui, uma vez, e não a cada clique do lado: descobrir
            // no meio pouparia o usuário de dois prompts inúteis.
            var curta = PathSides.WhyTooShort(tracado);

            if (curta is not null)
            {
                editor.WriteMessage($"\nAlinhamento não criado: {curta}.\n");
                return;
            }

            var lado = PerguntarOLado(editor, tracado);
            if (lado is null) return;

            var nome = PerguntarNome(editor);
            if (nome is null) return;

            Criar(editor, documento, terreno, tracado, lado.Value, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar o alinhamento.", erro);
            editor.WriteMessage($"\nNão consegui criar o alinhamento: {erro.Message}\n");
        }
    }

    /// <summary>
    /// UFV_ALINHAMENTOS: lista os alinhamentos do desenho.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoAlinhamentos)]
    public static void Alinhamentos()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var registro = AlignmentStore.Ler(documento.Database);

            if (registro.Problem is { } problema)
                editor.WriteMessage($"\n  ATENÇÃO: {problema}.\n");

            if (registro.Items.Count == 0)
            {
                editor.WriteMessage("\nNenhum alinhamento neste desenho.\n");
                return;
            }

            editor.WriteMessage($"\n{registro.Items.Count} alinhamento(s):\n");

            foreach (var alinhamento in registro.Items)
            {
                editor.WriteMessage(
                    $"  {alinhamento.Identity.Describe()} handle={alinhamento.Handle}\n");
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar os alinhamentos.", erro);
            editor.WriteMessage($"\nNão consegui listar os alinhamentos: {erro.Message}\n");
        }
    }

    /// <summary>
    /// Coleta os pontos da linha, um a um, até o usuário dar Enter.
    ///
    /// Mesmo ritual do UFV_AREA: os pontos são pedidos aqui, e não pelo PLINE
    /// do AutoCAD, para o plugin saber exatamente o que foi traçado. O rastro
    /// mostra os trechos já clicados; sem ele é desenhar às cegas.
    ///
    /// O Z de cada clique é ignorado depois: com OSNAP em planta, um clique
    /// pega a cota de uma curva de nível e o outro cai em zero. A cota vem
    /// do terreno, no <see cref="Criar"/>.
    /// </summary>
    private static IReadOnlyList<Point3d>? Tracar(Editor editor, RastroDoTracado rastro)
    {
        var pontos = new List<Point3d>();

        editor.WriteMessage(
            "\nTrace a linha de alinhamento em planta, com quantos pontos quiser. Enter termina.\n"
            + "Ela é a referência de onde as fileiras começam; a cota vem do terreno.\n");

        while (true)
        {
            var opcoes = pontos.Count == 0
                ? new PromptPointOptions("\nPrimeiro ponto: ")
                : new PromptPointOptions($"\nPróximo ponto <{pontos.Count} traçados, Enter termina>: ")
                {
                    UseBasePoint = true,
                    BasePoint = pontos[^1],
                    AllowNone = true,
                };

            var resposta = editor.GetPoint(opcoes);

            // Enter termina a linha, como no PLINE. Esc desiste.
            if (resposta.Status == PromptStatus.None) break;

            if (resposta.Status == PromptStatus.Cancel)
            {
                editor.WriteMessage("\nAlinhamento não criado.\n");
                return null;
            }

            if (resposta.Status != PromptStatus.OK) break;

            if (pontos.Count > 0) rastro.Acrescentar(pontos[^1], resposta.Value);

            pontos.Add(resposta.Value);
        }

        if (pontos.Count < 2)
        {
            editor.WriteMessage("\nUm alinhamento precisa de pelo menos dois pontos. Alinhamento não criado.\n");
            return null;
        }

        return pontos;
    }

    /// <summary>
    /// Pergunta de que lado ficam as mesas, por clique, com a linha ainda na
    /// tela (o rastro).
    ///
    /// Por clique, e não por "esquerda/direita" digitado: esquerda de quem?
    /// Do traçado, da tela, do norte? Clicar não tem ambiguidade nenhuma. O
    /// lado é o do trecho mais próximo do clique (<see cref="PathSides"/>).
    /// </summary>
    private static LineSide? PerguntarOLado(Editor editor, IReadOnlyList<Point3> tracado)
    {
        while (true)
        {
            var resposta = editor.GetPoint(new PromptPointOptions(
                "\nClique de que lado da linha ficam as mesas: "));

            if (resposta.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nAlinhamento não criado.\n");
                return null;
            }

            var ponto = new Point3(resposta.Value.X, resposta.Value.Y, resposta.Value.Z);

            LineSide lado;

            try
            {
                lado = PathSides.Of(tracado, ponto);
            }
            catch (ArgumentOutOfRangeException erro)
            {
                // A linha já foi conferida antes de chegar aqui, então o que
                // sobra é o ponto clicado: coordenada absurda vinda de um
                // OSNAP maluco, por exemplo.
                RegistroDeDiagnostico.Registrar("Não consegui decidir o lado do alinhamento.", erro);

                editor.WriteMessage(
                    $"\nNão consegui decidir o lado desse clique: {erro.Message}\n");
                return null;
            }

            if (lado != LineSide.On) return lado;

            // Clique em cima da linha não é escolha: é a mão tremendo. Aceitar
            // aqui seria inventar uma decisão que o usuário não tomou.
            editor.WriteMessage("\n  Você clicou em cima da linha. Clique para um dos lados.\n");
        }
    }

    private static string? PerguntarNome(Editor editor) =>
        Perguntas.Nome(editor, "alinhamento", "O alinhamento");

    /// <summary>
    /// Assenta a linha no terreno e a grava como polilinha 3D, com a
    /// identidade no XData e o registro no índice.
    ///
    /// O drapeamento é o mesmo da área: cada vértice ganha a cota do terreno,
    /// e a linha ganha vértices onde cruza arestas do relevo, para não passar
    /// por dentro do morro. Ponto que cair fora do terreno fica com a cota do
    /// clique e é avisado, como na área.
    /// </summary>
    private static void Criar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        IReadOnlyList<Point3> tracado,
        LineSide lado,
        string nome)
    {
        var drapejada = Draping.Along(terreno.Mesh, tracado);
        var vertices = drapejada.Vertices;

        if (vertices.Count < 2)
        {
            editor.WriteMessage(
                "\nA linha ficou com um vértice só depois de assentar no terreno. Alinhamento não criado.\n");
            return;
        }

        var identidade = AlignmentIdentity.Create(nome, lado, DateTime.Now);

        string handle;

        using (var transacao = documento.Database.TransactionManager.StartTransaction())
        {
            var tabela = (BlockTable)transacao.GetObject(documento.Database.BlockTableId, OpenMode.ForRead);
            var espaco = (BlockTableRecord)transacao.GetObject(
                tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var polilinha = new Polyline3d
            {
                Closed = false,
                Layer = GarantirLayer(transacao, documento.Database),
            };

            espaco.AppendEntity(polilinha);
            transacao.AddNewlyCreatedDBObject(polilinha, true);

            // Os vértices entram depois de a polilinha estar no banco: antes
            // disso ela não tem onde guardá-los.
            foreach (var p in vertices)
            {
                var vertice = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));

                polilinha.AppendVertex(vertice);
                transacao.AddNewlyCreatedDBObject(vertice, true);
            }

            AlignmentXData.Save(transacao, polilinha, identidade);

            // Lido dentro da transação que criou a entidade: depois do commit
            // seria acesso a objeto de banco já fechado.
            handle = polilinha.Handle.ToString();

            transacao.Commit();
        }

        // A linha já está no desenho: o que vier a falhar daqui para baixo é
        // problema de índice, não de criação. Dizer "não consegui criar" aqui
        // faria o usuário desenhar de novo e ficar com duas.
        try
        {
            AlignmentStore.Upsert(
                documento.Database, new AlignmentRecord(identidade, handle), out var problema);

            if (problema is not null)
            {
                editor.WriteMessage(
                    $"\n  ATENÇÃO: {problema}. Rode UFV_REINDEXAR para refazer o registro.\n");
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Alinhamento criado, mas não indexado.", erro);

            editor.WriteMessage(
                $"\n  ATENÇÃO: o alinhamento está no desenho, mas não entrou no registro\n"
                + $"  ({erro.Message}). Rode UFV_REINDEXAR.\n");
        }

        Relatar(editor, identidade, tracado, drapejada);
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static void Relatar(
        Editor editor,
        AlignmentIdentity identidade,
        IReadOnlyList<Point3> tracado,
        DrapedLine drapejada)
    {
        var noTerreno = drapejada.Vertices.Count;
        var acrescentados = noTerreno - tracado.Count;

        var cotaMinima = drapejada.Vertices.Min(v => v.Z);
        var cotaMaxima = drapejada.Vertices.Max(v => v.Z);

        editor.WriteMessage($"\nAlinhamento criado: {identidade.Describe()}\n");
        editor.WriteMessage($"  comprimento em planta:  {PathSides.PlanLength(tracado):0.###} m\n");
        editor.WriteMessage($"  pontos traçados:        {tracado.Count}\n");
        editor.WriteMessage(
            $"  vértices no terreno:    {noTerreno} "
            + $"({Math.Max(acrescentados, 0)} acrescentados no contorno do relevo)\n");
        editor.WriteMessage($"  cotas:                  {cotaMinima:0.###} m a {cotaMaxima:0.###} m\n");

        if (drapejada.HasGaps)
        {
            // Sem este aviso, o trecho sem terreno fica com a cota que o
            // usuário clicou: plausível, e sem nada que o denuncie.
            editor.WriteMessage(
                $"  ATENÇÃO: {drapejada.OutsideCount} vértice(s) caíram fora do terreno e ficaram\n"
                + "  com a cota do clique. Reveja o traçado ou processe uma superfície maior.\n");
        }

        // O sentido importa e o usuário precisa saber disso: redesenhar a
        // mesma linha ao contrário troca o lado.
        editor.WriteMessage(
            "  O lado vale para o sentido em que a linha foi traçada. Redesenhá-la ao\n"
            + "  contrário trocaria os lados.\n");
    }

    /// <summary>
    /// Garante a layer dos alinhamentos, amarela, e devolve o nome dela.
    ///
    /// A layer é só aparência: serve para ligar e desligar o que se vê. Quem
    /// diz que a linha é um alinhamento nosso é o XData; trocar a layer não
    /// tira a identidade dela (02-arquitetura.md).
    /// </summary>
    private static string GarantirLayer(Transaction transacao, Database database)
        => LayoutLayers.GarantirComCor(transacao, database, LayoutLayers.Alinhamento, LayoutLayers.CorDoAlinhamento);
}
