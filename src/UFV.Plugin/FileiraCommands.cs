using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using UFV.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.FileiraCommands))]

namespace UFV.Plugin;

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
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>UFV_FILEIRA: escolhe área, alinhamento e fileira, processa e desenha.</summary>
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
            editor.WriteMessage($"\nNão consegui processar a fileira: {erro.Message}\n");
        }
    }

    /// <summary>
    /// UFV_FILEIRA_AUTO: a primeira área, o primeiro alinhamento, a fileira
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
                editor.WriteMessage("\nFILEIRA Sem área ou sem alinhamento registrado neste desenho.\n");
                return;
            }

            var area = LerArea(documento.Database, areas[0].Handle);
            var alinhamento = LerAlinhamento(documento.Database, alinhamentos[0]);

            if (area is null || alinhamento is null)
            {
                editor.WriteMessage("\nFILEIRA A área ou o alinhamento registrado não está mais no desenho.\n");
                return;
            }

            Executar(editor, documento, terreno, area.Value, alinhamento.Value, 1, MesaCommands.MesaDeExemplo());
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao processar a fileira automática.", erro);
            editor.WriteMessage($"\nNão consegui processar a fileira: {erro.Message}\n");
        }
    }

    /// <summary>UFV_ALTURAS: liga ou desliga os textos de altura de pilar ("Mostrar alturas").</summary>
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
                editor.WriteMessage("\nAinda não há alturas no desenho: processe uma fileira primeiro.\n");
                return;
            }

            LayoutLayers.Ligar(transacao, documento.Database, LayoutLayers.Alturas, desligada.Value);
            transacao.Commit();

            editor.WriteMessage(desligada.Value
                ? "\nAlturas dos pilares: mostradas.\n"
                : "\nAlturas dos pilares: escondidas.\n");

            editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ligar as alturas.", erro);
            editor.WriteMessage($"\nNão consegui mexer nas alturas: {erro.Message}\n");
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
        var settings = ConfigCommands.Inicial(documento, out var avisoDaConfig);
        if (avisoDaConfig is not null) editor.WriteMessage($"\n  ATENÇÃO: {avisoDaConfig}\n");

        AvisarSeJaHaMesas(editor, documento.Database);

        var pilares = PillarTable.Distribute(perfil.Layout.Length, perfil.Frame.PillarSpanTarget, perfil.Frame.PillarCantilever);
        var geometria = TableGeometry.Local(perfil.Layout, pilares, perfil.Frame);

        var config = settings.Configuration;
        var celula = new TableFootprint(geometria.Length, geometria.Depth * Math.Cos(perfil.TiltRadians));

        editor.WriteMessage(
            $"\nMesa: {perfil.Describe()}\n"
            + $"Configuração: {settings.Describe()}\n"
            + $"Área: {area.Nome}; alinhamento: {alinhamento.Identidade.Describe()}\n");

        var relogio = System.Diagnostics.Stopwatch.StartNew();

        var layout = RowDistributor.Distribute(
            area.Vertices, alinhamento.Vertices, alinhamento.Identidade.Side, config.Pitch, config.TableGap, celula);

        if (layout.Rows.Count == 0)
        {
            editor.WriteMessage("\nFILEIRA Nenhuma fileira cabe: a área está do outro lado da linha, ou é pequena demais.\n");
            return;
        }

        if (numeroDaFileira < 1 || numeroDaFileira > layout.Rows.Count)
        {
            editor.WriteMessage($"\nFILEIRA A distribuição tem {layout.Rows.Count} fileira(s); não há fileira {numeroDaFileira}.\n");
            return;
        }

        var fileira = layout.Rows[numeroDaFileira - 1];

        var orientacao = RowOrientation.Resolve(fileira.Tables[0].DirectionRadians, alinhamento.Identidade.Side, config.UpslopeAzimuthRadians);
        if (orientacao.DivergenceRadians > 5 * Math.PI / 180)
        {
            editor.WriteMessage(
                $"\n  ATENÇÃO: a linha de alinhamento diverge {orientacao.DivergenceRadians * 180 / Math.PI:0.#}° do azimute configurado (a linha de alinhamento deve ser paralela ao azimute: norte-sul numa usina que olha para o norte). "
                + "A mesa segue a fileira, e vai olhar para um lado diferente do configurado.\n");
        }

        var processada = RowPipeline.ProcessRow(fileira, alinhamento.Identidade.Side, geometria, perfil.TiltRadians, terreno.Mesh, settings);

        var desenho = LayoutDrawer.Draw(documento.Database, processada, geometria, perfil.Layout.Module, perfil.TiltRadians, settings.Analyses);

        relogio.Stop();

        foreach (var aviso in processada.Warnings) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");

        Relatar(editor, layout, processada, desenho, relogio.Elapsed);
        GeoCommands.AvisarSeNaoVaiSalvar(editor, documento);
    }

    private static void Relatar(Editor editor, PlanLayout layout, ProcessedRow fileira, DrawnRow desenho, TimeSpan tempo)
    {
        var cotas = fileira.Tables.SelectMany(t => t.Pillars.Pillars).Where(p => p.GroundZ is not null).Select(p => p.TopZ).ToList();

        editor.WriteMessage(
            $"\nFILEIRA {fileira.Describe()}\n"
            + $"  distribuição: {layout.Rows.Count} fileira(s), {layout.Tables.Count} mesa(s), "
            + $"{layout.PartlyOutsideCount} na borda, {layout.SkippedForOverlap} pulada(s) por sobreposição\n"
            + $"  desenhado: {desenho.Tables} mesa(s), {desenho.Pillars} pilar(es), {desenho.Modules} módulo(s) com face, "
            + $"{desenho.Painted} peça(s) pintada(s), {desenho.Marked} marcada(s)\n"
            + (cotas.Count > 0
                ? $"  topo dos pilares: {cotas.Min().ToString("0.00", Brasil)} a {cotas.Max().ToString("0.00", Brasil)} m\n"
                : "  nenhum pilar com terreno\n")
            + $"  tempo: {tempo.TotalSeconds.ToString("0.0", Brasil)} s\n");

        foreach (var mesa in fileira.Tables)
            editor.WriteMessage($"  {mesa.Report.Describe()}; {mesa.Pillars.Describe()}\n");

        editor.WriteMessage($"\n  As alturas estão na camada {LayoutLayers.Alturas}, desligada. {PluginInfo.ComandoAlturas} liga.\n");
    }

    // ------------------------------------------------------------ entradas

    internal static ProcessedTerrain? ExigirTerreno(Editor editor, Document documento)
    {
        var terreno = TerrainCache.Get(documento);

        if (terreno is null)
        {
            editor.WriteMessage("\nNenhum terreno processado neste desenho. Use o botão Terreno primeiro.\n");
            return null;
        }

        var aviso = TerrenoEnvelhecido.Conferir(documento);
        if (aviso is not null) editor.WriteMessage($"\n  ATENÇÃO: {aviso}\n");

        return terreno;
    }

    /// <summary>
    /// A mesa: o primeiro perfil salvo, ou a de exemplo. É dito na linha de
    /// comando; trocar é pela janela da mesa.
    /// </summary>
    internal static TableProfile PerfilDaMesa(Editor editor)
    {
        var perfis = new TableProfileStore(MesaCommands.PastaDosPerfis);
        var salvo = MesaCommands.PrimeiroPerfil(perfis);

        if (salvo is null)
            editor.WriteMessage("\nNenhum perfil de mesa salvo: usando a mesa de exemplo. Salve um pela janela Mesa.\n");

        return salvo ?? MesaCommands.MesaDeExemplo();
    }

    internal static (IReadOnlyList<Point3> Vertices, string Nome)? EscolherArea(Editor editor, Document documento)
    {
        var areas = AreaStore.Load(documento.Database);

        if (areas.Count == 0)
        {
            editor.WriteMessage("\nNenhuma área registrada neste desenho. Use o botão Área primeiro.\n");
            return null;
        }

        if (areas.Count == 1)
        {
            editor.WriteMessage($"\nÁrea: {areas[0].Identity.DisplayName}.\n");
            return LerArea(documento.Database, areas[0].Handle);
        }

        var opcoes = new PromptEntityOptions("\nClique na área de implantação: ");
        opcoes.SetRejectMessage("\nIsso não é uma polilinha.");
        opcoes.AddAllowedClass(typeof(Polyline3d), false);

        var resposta = editor.GetEntity(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
        var identidade = AreaXData.Load(entidade);

        if (identidade is null)
        {
            editor.WriteMessage("\nEssa polilinha não é uma área do plugin.\n");
            return null;
        }

        return (Vertices((Polyline3d)entidade, transacao), identidade.DisplayName);
    }

    internal static (IReadOnlyList<Point3> Vertices, AlignmentIdentity Identidade)? EscolherAlinhamento(Editor editor, Document documento)
    {
        var alinhamentos = AlignmentStore.Load(documento.Database);

        if (alinhamentos.Count == 0)
        {
            editor.WriteMessage("\nNenhum alinhamento registrado neste desenho. Use o botão Alinhamento primeiro.\n");
            return null;
        }

        if (alinhamentos.Count == 1)
        {
            editor.WriteMessage($"\nAlinhamento: {alinhamentos[0].Identity.Describe()}.\n");
            return LerAlinhamento(documento.Database, alinhamentos[0]);
        }

        var opcoes = new PromptEntityOptions("\nClique na linha de alinhamento: ");
        opcoes.SetRejectMessage("\nIsso não é uma polilinha.");
        opcoes.AddAllowedClass(typeof(Polyline3d), false);

        var resposta = editor.GetEntity(opcoes);
        if (resposta.Status != PromptStatus.OK) return null;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        var entidade = (Entity)transacao.GetObject(resposta.ObjectId, OpenMode.ForRead);
        var identidade = AlignmentXData.Load(entidade);

        if (identidade is null)
        {
            editor.WriteMessage("\nEssa polilinha não é um alinhamento do plugin.\n");
            return null;
        }

        return (Vertices((Polyline3d)entidade, transacao), identidade);
    }

    private static int? PerguntarNumeroDaFileira(Editor editor)
    {
        // O AutoCAD acrescenta o valor padrão entre <> sozinho; escrevê-lo no
        // texto daria "<1>: <1>".
        var opcoes = new PromptIntegerOptions("\nNúmero da fileira (1 nasce no início da linha de alinhamento)")
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
            editor.WriteMessage(
                $"\n  ATENÇÃO: o desenho já tem {quantas} mesa(s) do plugin. Este comando desenha por cima; "
                + "apague as anteriores se não quiser duas usinas sobrepostas.\n");
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
    private static IReadOnlyList<Point3> Vertices(Polyline3d polilinha, Transaction transacao)
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
