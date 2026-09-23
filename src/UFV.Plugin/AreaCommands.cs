using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.AreaCommands))]

namespace UFV.Plugin;

/// <summary>
/// Seção UFV: a área de implantação.
///
/// O usuário traça a área em planta, dá um nome, e o plugin a assenta no
/// terreno — a linha que era reta no papel passa a acompanhar o relevo. É o
/// primeiro momento em que o terreno deixa de ser número e vira desenho.
/// </summary>
public static class AreaCommands
{
    /// <summary>Layer onde as áreas são desenhadas. Aparência, nunca identidade.</summary>
    private const string LayerDaArea = PluginInfo.PrefixoDeDados + "_AREA";

    /// <summary>
    /// UFV_AREA: traça a área de implantação e a assenta no terreno.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoArea)]
    public static void Area()
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

            // A área vai ser assentada sobre o terreno que está em memória. Se
            // ele não corresponde mais ao desenho, a área nasce errada — e
            // parecendo certa.
            var aviso = TerrenoEnvelhecido.Conferir(documento);
            if (aviso is not null)
            {
                editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");
            }

            // O rastro mostra o que já foi clicado: o AutoCAD sozinho só dá o
            // elástico do último ponto até o cursor, e num contorno de seis ou
            // oito vértices isso é desenhar às cegas.
            //
            // Ele vive por todo o comando, e não só enquanto se clica. Se fosse
            // descartado ao fim do traçado, o trecho que fecha o contorno seria
            // apagado no mesmo instante em que foi desenhado, e ninguém
            // chegaria a vê-lo. Assim ele também continua na tela enquanto o
            // nome é digitado.
            using var rastro = new RastroDoTracado();

            var pontos = Tracar(editor, rastro);
            if (pontos is null) return;

            if (pontos.Count < 3)
            {
                editor.WriteMessage("\nUma área precisa de pelo menos três vértices.\n");
                return;
            }

            var nome = PerguntarNome(editor);
            if (nome is null) return;

            Criar(editor, documento, terreno, pontos, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar a área.", erro);
            editor.WriteMessage($"\nNão consegui criar a área: {erro.Message}\n");
        }
    }

    /// <summary>
    /// Coleta os vértices da área, um a um, até o usuário fechar.
    ///
    /// Os pontos são pedidos aqui em vez de o comando chamar o PLINE do
    /// AutoCAD: assim o plugin sabe exatamente o que foi traçado, sem ter que
    /// adivinhar qual entidade apareceu no desenho depois.
    /// </summary>
    private static IReadOnlyList<Point3d>? Tracar(Editor editor, RastroDoTracado rastro)
    {
        var pontos = new List<Point3d>();

        editor.WriteMessage(
            "\nTrace a área em planta. A cota de cada vértice vem do terreno.\n");

        while (true)
        {
            var opcoes = pontos.Count == 0
                ? new PromptPointOptions("\nPrimeiro vértice: ")
                : new PromptPointOptions($"\nPróximo vértice [Fechar] <{pontos.Count} traçados>: ")
                {
                    UseBasePoint = true,
                    BasePoint = pontos[^1],
                    AllowNone = true,
                };

            if (pontos.Count > 0) opcoes.Keywords.Add("Fechar");

            var resposta = editor.GetPoint(opcoes);

            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == "Fechar") break;

            // Enter fecha a área, como no PLINE. Esc desiste.
            if (resposta.Status == PromptStatus.None) break;

            if (resposta.Status == PromptStatus.Cancel)
            {
                editor.WriteMessage("\nÁrea não criada.\n");
                return null;
            }

            if (resposta.Status != PromptStatus.OK) break;

            if (pontos.Count > 0) rastro.Acrescentar(pontos[^1], resposta.Value);

            pontos.Add(resposta.Value);
        }

        // O trecho de volta ao primeiro vértice fecha o contorno na tela, do
        // mesmo jeito que a área vai ficar.
        if (pontos.Count > 2) rastro.Acrescentar(pontos[^1], pontos[0]);

        return pontos;
    }

    private static string? PerguntarNome(Editor editor) =>
        Perguntas.Nome(editor, "área", "A área");

    private static void Criar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        IReadOnlyList<Point3d> pontos,
        string nome)
    {
        // Fecha o polígono: a área é um contorno, e o último trecho, de volta
        // ao primeiro vértice, também precisa assentar no terreno.
        var traçado = pontos.Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
        traçado.Add(traçado[0]);

        var drapejada = Draping.Along(terreno.Mesh, traçado);
        var contorno = SemOFechamento(drapejada.Vertices);

        // Três cliques no mesmo lugar passam pela contagem de vértices traçados
        // e chegam aqui como um ponto só. Sem esta guarda o desenho ganharia
        // uma polilinha fechada sem vértice nenhum, com identidade e registro:
        // uma área fantasma, que aparece na lista e que o AUDIT reclama.
        if (contorno.Count < 3)
        {
            editor.WriteMessage(
                "\nO contorno não fechou uma área: os vértices caíram todos no mesmo\n"
                + "lugar. Área não criada.\n");
            return;
        }

        var identidade = AreaIdentity.Create(nome, DateTime.Now);

        using var transacao = documento.Database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(documento.Database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(
            tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var polilinha = new Polyline3d
        {
            Closed = true,
            Layer = GarantirLayer(transacao, documento.Database),
        };

        espaco.AppendEntity(polilinha);
        transacao.AddNewlyCreatedDBObject(polilinha, true);

        // Os vértices entram depois de a polilinha estar no banco: antes
        // disso ela não tem onde guardá-los.
        foreach (var p in contorno)
        {
            var vertice = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));

            polilinha.AppendVertex(vertice);
            transacao.AddNewlyCreatedDBObject(vertice, true);
        }

        AreaXData.Save(transacao, polilinha, identidade);

        // O handle é lido antes do Commit, com o objeto ainda aberto pela
        // transação que o criou. Depois do Commit ele até responde, mas é
        // acesso a objeto de banco fora da transação, e isso não é garantido.
        var handle = polilinha.Handle.ToString();

        transacao.Commit();

        AreaStore.Upsert(documento.Database, new AreaRecord(identidade, handle), out var problema);

        if (problema is not null)
        {
            // A área foi criada e a identidade dela está na entidade. O que
            // se perdeu foi o índice, e ele se refaz.
            editor.WriteMessage(
                $"\n  ATENÇÃO: {problema}. As áreas que estavam nele podem ter sumido\n"
                + "  da lista. Rode UFV_REINDEXAR para refazê-lo a partir do desenho.\n");
        }

        Relatar(editor, identidade, pontos.Count, drapejada);
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    /// <summary>
    /// O contorno sem o ponto que repete o primeiro.
    ///
    /// A polilinha é fechada, então o trecho de volta ao início é dela por
    /// construção: repetir o primeiro vértice criaria um trecho de comprimento
    /// zero. A comparação é por coordenada, e não por contagem, porque o
    /// drapeamento pode ter enxugado o último ponto — e um dia pode deixar de
    /// repeti-lo sem avisar ninguém.
    /// </summary>
    private static IReadOnlyList<Point3> SemOFechamento(IReadOnlyList<Point3> vertices)
    {
        if (vertices.Count < 2) return vertices;

        var primeiro = vertices[0];
        var ultimo = vertices[^1];

        var mesmoLugar = Math.Abs(ultimo.X - primeiro.X) <= ToleranciaDeFechamento
            && Math.Abs(ultimo.Y - primeiro.Y) <= ToleranciaDeFechamento;

        return mesmoLugar ? vertices.Take(vertices.Count - 1).ToList() : vertices;
    }

    /// <summary>Um milímetro: a mesma tolerância com que o drapeamento enxuga.</summary>
    private const double ToleranciaDeFechamento = 0.001;

    private static void Relatar(
        Editor editor,
        AreaIdentity identidade,
        int verticesTracados,
        DrapedLine drapejada)
    {
        var noTerreno = SemOFechamento(drapejada.Vertices).Count;
        var acrescentados = noTerreno - verticesTracados;

        editor.WriteMessage($"\nÁrea criada: {identidade.Describe()}\n");
        editor.WriteMessage($"  vértices traçados:    {verticesTracados}\n");
        editor.WriteMessage(
            $"  vértices no terreno:  {noTerreno} "
            + $"({Math.Max(acrescentados, 0)} acrescentados no contorno do relevo)\n");

        if (drapejada.HasGaps)
        {
            // Sem este aviso, o trecho sem terreno fica com a cota que o
            // usuário clicou — plausível, e sem nada que o denuncie.
            editor.WriteMessage(
                $"  ATENÇÃO: {drapejada.OutsideCount} vértice(s) caíram fora do terreno e ficaram\n"
                + "  com a cota do clique. Reveja o traçado ou processe uma superfície maior.\n");
        }
    }

    /// <summary>
    /// Garante a layer das áreas e devolve o nome dela.
    ///
    /// A layer é só aparência — serve para o usuário ligar e desligar o que
    /// vê. Quem diz que a polilinha é uma área nossa é o XData; trocar a layer
    /// não tira a identidade dela (02-arquitetura.md).
    /// </summary>
    private static string GarantirLayer(Transaction transacao, Database database)
    {
        var tabela = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        if (tabela.Has(LayerDaArea)) return LayerDaArea;

        tabela.UpgradeOpen();

        var layer = new LayerTableRecord { Name = LayerDaArea };
        tabela.Add(layer);
        transacao.AddNewlyCreatedDBObject(layer, true);

        return LayerDaArea;
    }
}
