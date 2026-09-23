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
/// O usuário traça a linha e clica de que lado ficam as mesas. O lado é a parte
/// que o desenho não carrega sozinho — uma linha é simétrica —, e é a que
/// custa caro perder: a usina inteira nasce do outro lado e o desenho fica
/// perfeito.
///
/// Por isso o lado é guardado junto com o SENTIDO do traçado. "Esquerda" é
/// esquerda de quem caminha do primeiro ponto para o segundo; a mesma linha
/// desenhada ao contrário troca os dois lados.
/// </summary>
public static class AlignmentCommands
{
    /// <summary>Layer onde os alinhamentos são desenhados. Aparência, nunca identidade.</summary>
    private const string LayerDoAlinhamento = PluginInfo.PrefixoDeDados + "_ALINHAMENTO";

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
            var linha = Tracar(editor);
            if (linha is null) return;

            var (de, para) = linha.Value;

            // Conferido aqui, uma vez, e não a cada clique do lado: descobrir
            // no meio pouparia o usuário de dois prompts inúteis, e o catch lá
            // dentro culpava a linha mesmo quando o problema era o clique.
            var curta = LineSides.WhyTooShort(
                new Point3(de.X, de.Y, de.Z), new Point3(para.X, para.Y, para.Z));

            if (curta is not null)
            {
                editor.WriteMessage($"\nAlinhamento não criado: {curta}.\n");
                return;
            }

            var lado = PerguntarOLado(editor, de, para);
            if (lado is null) return;

            var nome = PerguntarNome(editor);
            if (nome is null) return;

            Criar(editor, documento, de, para, lado.Value, nome);
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
    /// Os dois pontos da linha.
    ///
    /// Dois pontos, e não uma polilinha: o alinhamento é uma direção de
    /// referência, e uma linha quebrada não tem lado bem definido — o mesmo
    /// ponto ficaria à esquerda de um trecho e à direita de outro.
    /// </summary>
    private static (Point3d De, Point3d Para)? Tracar(Editor editor)
    {
        editor.WriteMessage(
            "\nTrace a linha de alinhamento. Ela é a referência de onde as fileiras começam.\n");

        var primeiro = editor.GetPoint(new PromptPointOptions("\nPrimeiro ponto: "));

        if (primeiro.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\nAlinhamento não criado.\n");
            return null;
        }

        var segundo = editor.GetPoint(new PromptPointOptions("\nSegundo ponto: ")
        {
            UseBasePoint = true,
            BasePoint = primeiro.Value,
        });

        if (segundo.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\nAlinhamento não criado.\n");
            return null;
        }

        return (primeiro.Value, segundo.Value);
    }

    /// <summary>
    /// Pergunta de que lado ficam as mesas, por clique.
    ///
    /// Por clique, e não por "esquerda/direita" digitado: esquerda de quem?
    /// Do traçado, da tela, do norte? Clicar não tem ambiguidade nenhuma — o
    /// usuário aponta o lado, e a conta descobre qual é.
    /// </summary>
    private static LineSide? PerguntarOLado(Editor editor, Point3d de, Point3d para)
    {
        var a = new Point3(de.X, de.Y, de.Z);
        var b = new Point3(para.X, para.Y, para.Z);

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
                lado = LineSides.Of(a, b, ponto);
            }
            catch (ArgumentOutOfRangeException erro)
            {
                // A linha já foi conferida antes de chegar aqui, então o que
                // sobra é o ponto clicado — coordenada absurda vinda de um
                // OSNAP maluco, por exemplo. Antes esta mensagem culpava a
                // linha em qualquer caso, e o erro não ia para lugar nenhum.
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

    private static void Criar(
        Editor editor,
        Document documento,
        Point3d de,
        Point3d para,
        LineSide lado,
        string nome)
    {
        var identidade = AlignmentIdentity.Create(nome, lado, DateTime.Now);

        string handle;

        using (var transacao = documento.Database.TransactionManager.StartTransaction())
        {
            var tabela = (BlockTable)transacao.GetObject(documento.Database.BlockTableId, OpenMode.ForRead);
            var espaco = (BlockTableRecord)transacao.GetObject(
                tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var linha = new Line(de, para)
            {
                Layer = GarantirLayer(transacao, documento.Database),
            };

            espaco.AppendEntity(linha);
            transacao.AddNewlyCreatedDBObject(linha, true);

            AlignmentXData.Save(transacao, linha, identidade);

            // Lido dentro da transação que criou a entidade: depois do commit
            // seria acesso a objeto de banco já fechado.
            handle = linha.Handle.ToString();

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
        Relatar(editor, identidade, de, para);
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static void Relatar(Editor editor, AlignmentIdentity identidade, Point3d de, Point3d para)
    {
        var comprimento = Math.Sqrt(
            (para.X - de.X) * (para.X - de.X) + (para.Y - de.Y) * (para.Y - de.Y));

        editor.WriteMessage($"\nAlinhamento criado: {identidade.Describe()}\n");
        editor.WriteMessage($"  comprimento em planta: {comprimento:0.###} m\n");

        // O sentido importa e o usuário precisa saber disso: redesenhar a
        // mesma linha ao contrário troca o lado.
        editor.WriteMessage(
            "  O lado vale para o sentido em que a linha foi traçada. Redesenhá-la ao\n"
            + "  contrário trocaria os lados.\n");
    }

    /// <summary>
    /// Garante a layer dos alinhamentos e devolve o nome dela.
    ///
    /// A layer é só aparência — serve para ligar e desligar o que se vê. Quem
    /// diz que a linha é um alinhamento nosso é o XData; trocar a layer não
    /// tira a identidade dela (02-arquitetura.md).
    /// </summary>
    private static string GarantirLayer(Transaction transacao, Database database)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (tabela.Has(LayerDoAlinhamento)) return LayerDoAlinhamento;

        tabela.UpgradeOpen();

        var layer = new LayerTableRecord { Name = LayerDoAlinhamento };
        tabela.Add(layer);
        transacao.AddNewlyCreatedDBObject(layer, true);

        return LayerDoAlinhamento;
    }
}
