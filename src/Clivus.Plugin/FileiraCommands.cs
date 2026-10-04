using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.FileiraCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Uma fileira no CAD: distribui as mesas em planta a partir da área e da
/// linha de alinhamento, processa a fileira escolhida (5.1 a 5.6) e desenha
/// (5.7). É o passo 5.8, o primeiro em que o Renan vê mesa no terreno.
///
/// O que o comando pede é pouco de propósito: a área, o alinhamento e o
/// número da fileira. A mesa vem do perfil salvo (o primeiro em ordem
/// alfabética, ou a mesa de exemplo), a configuração e as regras vêm do
/// desenho (4.4) ou do padrão. Tudo isso é dito na linha de comando, para
/// ele saber com o que a fileira foi calculada.
/// </summary>
public static class FileiraCommands
{
    /// <summary>CLIVUS_FILEIRA: escolhe área, alinhamento e fileira, processa e desenha.</summary>
    [CommandMethod(PluginInfo.ComandoFileira)]
    public static void Fileira()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var area = EscolherArea(editor, documento);
            if (area is null) return;

            var alinhamento = EscolherAlinhamento(editor, documento);
            if (alinhamento is null) return;

            var numero = PerguntarNumeroDaFileira(editor);
            if (numero is null) return;

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, numero.Value, PerfilDaMesa(editor));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a fileira.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui processar a fileira: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_FILEIRA_AUTO: a primeira área, o primeiro alinhamento, a fileira
    /// 1 e a mesa de exemplo, sem perguntar nada. Existe para o teste de
    /// nível 2, que não clica; a mesa é sempre a de exemplo para o teste não
    /// depender de um perfil salvo na máquina. O comando do produto continua
    /// perguntando e usando o perfil salvo.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoFileiraAutomatico)]
    public static void FileiraAutomatica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var areas = AreaStore.Load(documento.Database);
            var alinhamentos = AlignmentStore.Load(documento.Database);

            if (areas.Count == 0 || alinhamentos.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nFILEIRA Sem área ou sem alinhamento registrado neste desenho.\n"));
                return;
            }

            var area = LerArea(documento.Database, areas[0].Handle);
            var alinhamento = LerAlinhamento(documento.Database, alinhamentos[0]);

            if (area is null || alinhamento is null)
            {
                editor.WriteMessage(Tr.T("\nFILEIRA A área ou o alinhamento registrado não está mais no desenho.\n"));
                return;
            }

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, 1, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a fileira automática.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui processar a fileira: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_ALTURAS: liga ou desliga os textos de altura de pilar ("Mostrar alturas").</summary>
    [CommandMethod(PluginInfo.ComandoAlturas)]
    public static void Alturas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            using var transacao = documento.Database.TransactionManager.StartTransaction();

            var desligada = LayoutLayers.EstaDesligada(transacao, documento.Database, LayoutLayers.Alturas);

            if (desligada is null)
            {
                editor.WriteMessage(Tr.F("\nAinda não há alturas no desenho: gerar não as põe (8.8); rode {0} primeiro.\n", PluginInfo.ComandoAlturasRegerar));
                return;
            }

            LayoutLayers.Ligar(transacao, documento.Database, LayoutLayers.Alturas, desligada.Value);
            transacao.Commit();

            editor.WriteMessage(desligada.Value
                ? Tr.T("\nAlturas dos pilares: mostradas.\n")
                : Tr.T("\nAlturas dos pilares: escondidas.\n"));

            editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ligar as alturas.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer nas alturas: {0}\n", erro.Message));
        }
    }

    // ------------------------------------------------------------ o miolo

    private static void Executar(
        Editor editor,
        Document documento,
        ProcessedTerrain terreno,
        (IReadOnlyList<Point3> Vertices, string Nome) area,
        (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade) alinhamento,
        int numeroDaFileira,
        TableProfile perfil)
    {
        // As mesas do desenho em uso, como na Usina (revisão do 8.6: a
        // fileira N tem que sair igual à fileira N da usina).
        var emUso = UsinaCommands.MesasEmUso(editor, documento.Database, Tr.T("FILEIRA"));
        if (emUso is null) return;
        if (emUso.Count > 0) perfil = emUso[0].Profile;

        var doProjeto = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (doProjeto.EmbedmentNote(perfil.Frame) is { } notaDoT3) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", notaDoT3));
        var settings = doProjeto.ForTable(perfil.Frame);
        if (avisoDaConfig is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", avisoDaConfig));

        AvisarSeJaHaMesas(editor, documento.Database);

        var pilares = perfil.Frame.Pillars(perfil.Layout);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        var config = settings.Configuration;
        var celula = new TableFootprint(geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));

        editor.WriteMessage(Tr.F(
            "\nMesa: {0}\nConfiguração: {1}\nÁrea: {2}; alinhamento: {3}\n",
            perfil.Describe(), settings.Describe(), area.Nome, alinhamento.Identidade.Describe()));

        var relogio = System.Diagnostics.Stopwatch.StartNew();

        LayoutDrawer.TiposDeMesa? tipos = null;
        PlanLayout layout;

        if (emUso.Count == 0)
        {
            layout = RowDistributor.Distribute(
                area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, celula,
                config.UpslopeAzimuthRadians);
        }
        else
        {
            var (t, pegadas, modulos) = UsinaCommands.Tipos(emUso);
            tipos = t;
            layout = RowDistributor.Distribute(
                area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, pegadas, modulos,
                config.UpslopeAzimuthRadians,
                pegadas.Count > 1 ? PlantPipeline.FitsOnTerrain(t.Geometrias, perfil.TiltRadians, terreno.Mesh, settings) : null);
        }

        if (layout.Rows.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nFILEIRA Nenhuma fileira cabe: a área está do outro lado da linha, a linha não a atravessa, ou ela é pequena demais.\n"));
            return;
        }

        if (numeroDaFileira < 1 || numeroDaFileira > layout.Rows.Count)
        {
            editor.WriteMessage(Tr.F("\nFILEIRA A distribuição tem {0} fileira(s); não há fileira {1}.\n", layout.Rows.Count, numeroDaFileira));
            return;
        }

        var fileira = layout.Rows[numeroDaFileira - 1];

        var processada = tipos is null
            ? RowPipeline.ProcessRow(fileira, geometria, perfil.TiltRadians, terreno.Mesh, settings)
            : RowPipeline.ProcessRow(fileira, fileira.Tables.Select(m => tipos.Geometrias[m.Kind]).ToList(), perfil.TiltRadians, terreno.Mesh, settings);

        var desenho = LayoutDrawer.Draw(
            documento.Database, processada, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses,
            analisar: LayoutDrawer.Analise.Nada, tipos: tipos);

        relogio.Stop();

        foreach (var aviso in processada.Warnings) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));

        Relatar(editor, layout, processada, desenho, relogio.Elapsed);
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static void Relatar(Editor editor, PlanLayout layout, ProcessedRow fileira, DrawnRow desenho, TimeSpan tempo)
    {
        var cotas = fileira.Tables.SelectMany(t => t.Pillars.Pillars).Where(p => p.GroundZ is not null).Select(p => p.TopZ).ToList();

        editor.WriteMessage(
            Tr.F(
                "\nFILEIRA {0}\n  distribuição: {1} fileira(s), {2} mesa(s), {3} posição(ões) descartada(s) por passar da área\n  desenhado: {4} mesa(s), {5} pilar(es), {6} módulo(s) com face, {7} peça(s) pintada(s), {8} marcada(s)\n",
                fileira.Describe(), layout.Rows.Count, layout.Tables.Count, layout.DroppedOutside,
                desenho.Tables, desenho.Pillars, desenho.Modules, desenho.Painted, desenho.Marked)
            + (cotas.Count > 0
                ? Tr.F("  topo dos pilares: {0:0.00} a {1:0.00} m\n", cotas.Min(), cotas.Max())
                : Tr.T("  nenhum pilar com terreno\n"))
            + Tr.F("  tempo: {0:0.0} s\n", tempo.TotalSeconds));

        foreach (var mesa in fileira.Tables)
            editor.WriteMessage($"  {mesa.Report.Describe()}; {mesa.Pillars.Describe()}\n");

        editor.WriteMessage(Tr.T("\n  Gerado sem análise: cores, alturas e declividade saem pelo menu Análises.\n"));
    }

    // ------------------------------------------------------------ entradas

    internal static ProcessedTerrain? ExigirTerreno(Editor editor, Document documento)
    {
        var terreno = TerrainCache.Get(documento);

        // Fechou e reabriu o desenho: a malha era só memória, mas o carimbo
        // no desenho diz qual superfície foi processada. Reprocessa sozinho.
        // Pedido do Renan em 26/09/2026: "é preciso que ele não perca".
        if (terreno is null && TerrainCommands.Reprocessar(editor, documento))
            terreno = TerrainCache.Get(documento);

        if (terreno is null)
        {
            editor.WriteMessage(Tr.T("\nNenhum terreno processado neste desenho. Use o botão Terreno primeiro.\n"));
            return null;
        }

        var aviso = TerrenoEnvelhecido.Conferir(documento);
        if (aviso is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));

        return terreno;
    }

    /// <summary>
    /// A mesa: o primeiro perfil salvo, ou a de exemplo. É dito na linha de
    /// comando; trocar é pela janela da mesa.
    /// </summary>
    internal static TableProfile PerfilDaMesa(Editor editor, bool silencioso = false)
    {
        var perfis = new TableProfileStore(MesaCommands.PastaDosPerfis);
        var salvo = MesaCommands.PrimeiroPerfil(perfis);

        if (salvo is null && !silencioso)
            editor.WriteMessage(Tr.T("\nNenhum perfil de mesa salvo: usando a mesa de exemplo. Salve um pela janela Mesa.\n"));

        return salvo ?? MesaCommands.MesaDeExemplo();
    }

    /// <summary>
    /// O perfil de uma mesa JÁ DESENHADA, pelo tamanho dela (29/09/2026): o
    /// comprimento da borda baixa do contorno (em 3D, que não muda com o
    /// giro) comparado com o de cada perfil da biblioteca e o da mesa de
    /// exemplo. O "perfil atual" (o primeiro da biblioteca) serve para
    /// desenhar mesa nova; para refazer uma que está no desenho, ele pode
    /// ser de outro tamanho — o Renan salvou uma mesa de 14 módulos e o
    /// Recalcular das de 28 passou a recusar ("Trocou de mesa?").
    /// Sem nenhum do mesmo tamanho (5 cm), devolve o padrão.
    /// </summary>
    /// <remarks>
    /// Desde o 8.6 a mesa grava o nome do perfil dela: com o nome e o
    /// desenho, a mesa cadastrada no desenho com esse nome vale antes de
    /// qualquer adivinhação; depois, as do desenho pelo comprimento.
    /// </remarks>
    internal static TableProfile PerfilDaMesaDesenhada(
        IReadOnlyList<Point3> cantos, TableProfile padrao, string? nomeDoPerfil = null, Database? database = null)
    {
        var doDesenho = database is null ? [] : MesasDoDesenho.Ler(database);

        if (DrawingTables.Find(doDesenho, nomeDoPerfil) is { } pelaIdentidade) return pelaIdentidade.Profile;

        if (cantos.Count < 2) return padrao;

        var dx = cantos[1].X - cantos[0].X;
        var dy = cantos[1].Y - cantos[0].Y;
        var dz = cantos[1].Z - cantos[0].Z;
        var comprimento = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        bool Serve(TableProfile p) => Math.Abs(p.Layout.Length - comprimento) <= 0.05;

        if (Serve(padrao)) return padrao;

        if (doDesenho.FirstOrDefault(m => Serve(m.Profile)) is { } doMesmoTamanho) return doMesmoTamanho.Profile;

        try
        {
            var perfis = new TableProfileStore(MesaCommands.PastaDosPerfis);

            foreach (var nome in perfis.List())
            {
                var perfil = perfis.Load(nome);
                if (Serve(perfil)) return perfil;
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui procurar o perfil da mesa desenhada.", erro);
        }

        var exemplo = MesaCommands.MesaDeExemplo();

        return Serve(exemplo) ? exemplo : padrao;
    }

    /// <summary>A geometria local de um perfil de mesa.</summary>
    internal static TableGeometry GeometriaDe(TableProfile perfil) =>
        TableGeometry.Local(
            perfil.Layout,
            perfil.Frame.Pillars(perfil.Layout),
            perfil.Frame);

    internal static (IReadOnlyList<Point3> Vertices, string Nome)? EscolherArea(Editor editor, Document documento)
    {
        var areas = AreaStore.Load(documento.Database);

        if (areas.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nNenhuma área registrada neste desenho. Use o botão Área primeiro.\n"));
            return null;
        }

        if (areas.Count == 1)
        {
            editor.WriteMessage(Tr.F("\nÁrea: {0}.\n", areas[0].Identity.DisplayName));
            return LerArea(documento.Database, areas[0].Handle);
        }

        var opcoes = new PromptEntityOptions(Tr.T("\nClique na área de implantação: "));
        opcoes.SetRejectMessage(Tr.T("\nIsso não é uma polilinha."));
        opcoes.AddAllowedClass(typeof(Polyline3d), false);

        var resposta = editor.GetEntity(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
        var identidade = AreaXData.Load(entidade);

        if (identidade is null)
        {
            editor.WriteMessage(Tr.T("\nEssa polilinha não é uma área do plugin.\n"));
            return null;
        }

        return (Vertices((Polyline3d)entidade, transacao), identidade.DisplayName);
    }

    internal static (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? EscolherAlinhamento(Editor editor, Document documento)
    {
        var alinhamentos = AlignmentStore.Load(documento.Database);

        if (alinhamentos.Count == 0)
        {
            editor.WriteMessage(Tr.T("\nNenhum alinhamento registrado neste desenho. Use o botão Alinhamento primeiro.\n"));
            return null;
        }

        if (alinhamentos.Count == 1)
        {
            editor.WriteMessage(Tr.F("\nAlinhamento: {0}.\n", alinhamentos[0].Identity.Describe()));
            return LerAlinhamento(documento.Database, alinhamentos[0]);
        }

        var opcoes = new PromptEntityOptions(Tr.T("\nClique na linha de alinhamento: "));
        opcoes.SetRejectMessage(Tr.T("\nIsso não é uma polilinha."));
        opcoes.AddAllowedClass(typeof(Polyline3d), false);

        var resposta = editor.GetEntity(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
        var identidade = AlignmentXData.Load(entidade);

        if (identidade is null)
        {
            editor.WriteMessage(Tr.T("\nEssa polilinha não é um alinhamento do plugin.\n"));
            return null;
        }

        return (Vertices((Polyline3d)entidade, transacao), identidade);
    }

    private static int? PerguntarNumeroDaFileira(Editor editor)
    {
        // O AutoCAD acrescenta o valor padrão entre <> sozinho; escrevê-lo no
        // texto daria "<1>: <1>".
        var opcoes = new PromptIntegerOptions(Tr.T("\nNúmero da fileira (1 nasce no início da linha de alinhamento)"))
        {
            AllowNegative = false,
            AllowZero = false,
            DefaultValue = 1,
            UseDefaultValue = true,
        };

        var resposta = editor.GetInteger(opcoes);

        return resposta.Status == PromptStatus.OK ? resposta.Value : null;
    }

    /// <summary>
    /// Avisa quando o desenho já tem mesas nossas: o comando desenha por
    /// cima, e limpar ou substituir é assunto da etapa 7. O usuário fica
    /// sabendo antes de ver duas usinas sobrepostas.
    /// </summary>
    internal static void AvisarSeJaHaMesas(Editor editor, Database database)
    {
        var quantas = 0;

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
            var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in espaco)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Polyline3d polilinha && LayoutXData.LoadTable(polilinha) is not null)
                    quantas++;
            }
        }

        if (quantas > 0)
        {
            editor.WriteMessage(Tr.F(
                "\n  ATENÇÃO: o desenho já tem {0} mesa(s) do plugin. Este comando desenha por cima; apague as anteriores se não quiser duas usinas sobrepostas.\n",
                quantas));
        }
    }

    internal static (IReadOnlyList<Point3> Vertices, string Nome)? LerArea(Database database, string handle)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var entidade = PorHandle(database, transacao, handle);
        if (entidade is not Polyline3d polilinha) return null;

        var identidade = AreaXData.Load(polilinha);
        if (identidade is null) return null;

        return (Vertices(polilinha, transacao), identidade.DisplayName);
    }

    internal static (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? LerAlinhamento(Database database, AlignmentRecord registro)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var entidade = PorHandle(database, transacao, registro.Handle);
        if (entidade is not Polyline3d polilinha) return null;

        // A identidade da entidade manda sobre a do registro: é ela que
        // viaja com a linha.
        var identidade = AlignmentXData.Load(polilinha) ?? registro.Identity;

        return (Vertices(polilinha, transacao), identidade);
    }

    private static Entity? PorHandle(Database database, Transaction transacao, string handle)
    {
        if (!long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)) return null;

        try
        {
            var id = database.GetObjectId(false, new Handle(valor), 0);

            return id.IsNull || id.IsErased ? null : transacao.GetObject(id, OpenMode.ForRead) as Entity;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Handle {handle} não resolveu para uma entidade.", erro);
            return null;
        }
    }

    /// <summary>
    /// Os vértices da polilinha que o usuário traçou, em planta.
    ///
    /// A polilinha gravada é a drapejada, com um vértice em cada aresta do
    /// terreno que cruza: dezenas por trecho. Para a distribuição cada um
    /// seria um trecho com família própria de fileiras (foi o que aconteceu:
    /// 85 fileiras e 595 sobreposições num alinhamento reto). Os vértices do
    /// drapeamento estão na reta do traçado em planta, e saem por
    /// colinearidade. O Z não é usado pela distribuição, e a cota de tudo que
    /// se desenha vem do terreno, nunca daqui (regra sagrada 5).
    /// </summary>
    internal static IReadOnlyList<Point3> Vertices(Polyline3d polilinha, Transaction transacao)
    {
        var pontos = new List<Point3>();

        foreach (ObjectId id in polilinha)
        {
            var vertice = (PolylineVertex3d)transacao.GetObject(id, OpenMode.ForRead);
            pontos.Add(new Point3(vertice.Position.X, vertice.Position.Y, vertice.Position.Z));
        }

        // Um milímetro: o drapeamento põe o vértice exatamente na reta. Um
        // clique que caia a menos de um milímetro da reta entre os vizinhos
        // (com OSNAP numa divisa reta, por exemplo) sai também, e não faz
        // falta: não mudava o traçado em planta.
        return PlanPaths.SimplifyCollinear(pontos, tolerance: 1e-3, closed: polilinha.Closed);
    }
}
