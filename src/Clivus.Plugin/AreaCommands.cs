using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.AreaCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Seção Clivus Solar: a área de implantação.
///
/// O usuário traça a área em planta, dá um nome, e o plugin a assenta no
/// terreno — a linha que era reta no papel passa a acompanhar o relevo. É o
/// primeiro momento em que o terreno deixa de ser número e vira desenho.
/// </summary>
public static class AreaCommands
{
    /// <summary>
    /// CLIVUS_AREA: traça a área de implantação e a assenta no terreno.
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
                    Tr.T("\nNenhum terreno processado neste desenho. Use o botão Terreno primeiro.\n"));
                return;
            }

            // A área vai ser assentada sobre o terreno que está em memória. Se
            // ele não corresponde mais ao desenho, a área nasce errada — e
            // parecendo certa.
            var aviso = TerrenoEnvelhecido.Conferir(documento);
            if (aviso is not null)
            {
                editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));
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
                editor.WriteMessage(Tr.T("\nUma área precisa de pelo menos três vértices.\n"));
                return;
            }

            var nome = PerguntarNome(editor);
            if (nome is null) return;

            Criar(editor, documento, terreno, pontos, nome);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao criar a área.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui criar a área: {0}\n", erro.Message));
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
            Tr.T("\nTrace a área em planta. A cota de cada vértice vem do terreno.\n"));

        while (true)
        {
            var opcoes = pontos.Count == 0
                ? new PromptPointOptions(Tr.T("\nPrimeiro vértice ou [Selecionar] uma polilinha: "))
                : new PromptPointOptions(Tr.F("\nPróximo vértice [Fechar] <{0} traçados>: ", pontos.Count))
                {
                    UseBasePoint = true,
                    BasePoint = pontos[^1],
                    AllowNone = true,
                };

            if (pontos.Count > 0) opcoes.Keywords.Add("Fechar");
            else opcoes.Keywords.Add("Selecionar");

            var resposta = editor.GetPoint(opcoes);

            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == "Fechar") break;

            // A área que já está desenhada (28/10/2026, Renan: "hoje ele só
            // permite desenhar do zero"): os vértices vêm da polilinha.
            if (resposta.Status == PromptStatus.Keyword && resposta.StringResult == "Selecionar")
                return Selecionar(editor, rastro);

            // Enter fecha a área, como no PLINE. Esc desiste.
            if (resposta.Status == PromptStatus.None) break;

            if (resposta.Status == PromptStatus.Cancel)
            {
                editor.WriteMessage(Tr.T("\nÁrea não criada.\n"));
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

    /// <summary>
    /// Os vértices de uma polilinha fechada que já está no desenho (2D, 3D ou
    /// LWPOLYLINE; arco vira trechos de até 1 m). A polilinha do usuário fica
    /// como está: a área é uma polilinha nova, assentada no terreno, na layer
    /// das áreas. A cota da polilinha não vale; vem do terreno, no
    /// <see cref="Criar"/>, como a do clique. Fechada é em planta: a 3D com a
    /// ponta no começo (a até 1 cm, com qualquer cota) serve; a que não fecha
    /// pergunta se fecha com uma reta da ponta ao começo (30/10/2026).
    /// </summary>
    private static IReadOnlyList<Point3d>? Selecionar(Editor editor, RastroDoTracado rastro)
    {
        var opcoes = new PromptEntityOptions(Tr.T("\nClique na polilinha fechada (2D ou 3D) que vira a área: "));
        opcoes.SetRejectMessage(Tr.T("\nTem que ser uma polilinha (2D ou 3D)."));
        opcoes.AddAllowedClass(typeof(Polyline), false);
        opcoes.AddAllowedClass(typeof(Polyline2d), false);
        opcoes.AddAllowedClass(typeof(Polyline3d), false);

        while (true)
        {
            var escolha = editor.GetEntity(opcoes);
            if (escolha.Status != PromptStatus.OK)
            {
                editor.WriteMessage(Tr.T("\nÁrea não criada.\n"));
                return null;
            }

            List<Point3>? contorno;
            var abertura = 0.0;
            using (var transacao = escolha.ObjectId.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var curva = (Curve)transacao.GetObject(escolha.ObjectId, OpenMode.ForRead);
                if (AreaXData.Load(curva) is { } jaE)
                {
                    editor.WriteMessage(Tr.F("\n  Essa polilinha já é a área \"{0}\". Escolha outra.\n", jaE.Name));
                    continue;
                }

                contorno = LocalDosInversores.Contorno(curva, transacao);
                if (contorno is null)
                {
                    abertura = LocalDosInversores.AberturaEmPlanta(curva);
                    contorno = LocalDosInversores.Contorno(curva, transacao, aceitarAberta: true);
                }
            }

            if (contorno is not { Count: >= 3 })
            {
                editor.WriteMessage(Tr.T("\n  Essa polilinha tem menos de três vértices em planta. Escolha outra.\n"));
                continue;
            }

            if (abertura > 0 && !FecharAAberta(editor, abertura)) continue;

            var pontos = contorno.Select(p => new Point3d(p.X, p.Y, p.Z)).ToList();
            for (var i = 0; i < pontos.Count; i++) rastro.Acrescentar(pontos[i], pontos[(i + 1) % pontos.Count]);
            return pontos;
        }
    }

    /// <summary>
    /// A polilinha escolhida não fecha: fecha com uma reta da ponta ao começo
    /// (Enter), ou escolhe outra. A do usuário não muda: a área é nova.
    /// </summary>
    private static bool FecharAAberta(Editor editor, double abertura)
    {
        var fechar = Tr.T("Fechar");
        var outra = Tr.T("Outra");

        var pergunta = new PromptKeywordOptions(
            Tr.F("\nEssa polilinha não fecha: a ponta está a {0:0.00} m do começo, em planta. Fechar a área com uma reta da ponta ao começo? [{1}/{2}] <{1}>: ", abertura, fechar, outra),
            $"{fechar} {outra}")
        {
            AllowNone = true,
        };

        var resposta = editor.GetKeywords(pergunta);
        return resposta.Status == PromptStatus.None
            || (resposta.Status == PromptStatus.OK && resposta.StringResult != outra);
    }

    private static string? PerguntarNome(Editor editor) =>
        Perguntas.Nome(editor, Tr.T("área"), Tr.T("A área"));

    internal static void Criar(
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
                Tr.T("\nO contorno não fechou uma área: os vértices caíram todos no mesmo\nlugar. Área não criada.\n"));
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
                Tr.F("\n  ATENÇÃO: {0}. As áreas que estavam nele podem ter sumido\n  da lista. Rode CLIVUS_REINDEXAR para refazê-lo a partir do desenho.\n", problema));
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

        editor.WriteMessage(Tr.F("\nÁrea criada: {0}\n", identidade.Describe()));
        editor.WriteMessage(Tr.F("  vértices traçados:    {0}\n", verticesTracados));
        editor.WriteMessage(
            Tr.F("  vértices no terreno:  {0} ({1} acrescentados no contorno do relevo)\n", noTerreno, Math.Max(acrescentados, 0)));

        if (drapejada.HasGaps)
        {
            // Sem este aviso, o trecho sem terreno fica com a cota que o
            // usuário clicou — plausível, e sem nada que o denuncie.
            editor.WriteMessage(
                Tr.F("  ATENÇÃO: {0} vértice(s) caíram fora do terreno e ficaram\n  com a cota do clique. Reveja o traçado ou processe uma superfície maior.\n", drapejada.OutsideCount));
        }
    }

    /// <summary>
    /// Garante a layer das áreas, laranja, e devolve o nome dela.
    ///
    /// A layer é só aparência — serve para o usuário ligar e desligar o que
    /// vê. Quem diz que a polilinha é uma área nossa é o XData; trocar a layer
    /// não tira a identidade dela (02-arquitetura.md).
    /// </summary>
    private static string GarantirLayer(Transaction transacao, Database database)
        => LayoutLayers.GarantirComCor(transacao, database, LayoutLayers.Area, LayoutLayers.CorDaArea);
}
