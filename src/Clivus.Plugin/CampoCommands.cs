using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.CampoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// Gerar conforme objetos em campo (40.2, Renan 30/10/2026: "na hora de gerar,
/// ao invés de ter a opção de gerar automático como já tem, pode ter a opção
/// gerar conforme objetos em campo, aí eu escolho o objeto da área, e ele
/// segue eles e faz a alocação [...] área por área, seguindo o cliente").
///
/// A área é a de sempre (botão Área). Os objetos são polilinhas fechadas de 4
/// vértices, 2D ou 3D, sem XData do Clivus: todos os da área, os de uma
/// camada, ou os escolhidos. O Clivus aponta as divergências (na linha de
/// comando e no desenho, na camada CLIVUS_DIVERGENCIAS), troca as mesas que a
/// área já tinha pelas dos objetos, sentadas no terreno pelo motor, e guarda
/// na área que ela segue os objetos: o Regerar área a refaz por eles.
/// </summary>
public static class CampoCommands
{
    /// <summary>A camada das marcas de divergência (círculo e texto em cada objeto que diverge).</summary>
    internal const string CamadaDasDivergencias = PluginInfo.PrefixoDeDados + "_DIVERGENCIAS";

    /// <summary>O registro, no dicionário da área, de que ela segue os objetos.</summary>
    private const string ChaveDoRegistro = PluginInfo.PrefixoDeDados + "_CAMPO";

    /// <summary>Versão 2 (revisão de tela do 40.2): o alinhamento pela ponta. A 1 é o centro, e é a que sai com ele.</summary>
    private const int VersaoDoRegistro = 2;

    /// <summary>Como os objetos foram escolhidos; o Regerar área escolhe do mesmo jeito.</summary>
    /// <param name="Modo"><see cref="Todos"/>, <see cref="PelaCamada"/> ou <see cref="Escolhidos"/>.</param>
    /// <param name="Camada">A camada, no modo <see cref="PelaCamada"/>.</param>
    /// <param name="Handles">Os objetos, no modo <see cref="Escolhidos"/>.</param>
    /// <param name="Alinhamento">Por onde a mesa se alinha no objeto quando os comprimentos diferem.</param>
    internal sealed record Escolha(string Modo, string Camada, IReadOnlyList<string> Handles, FieldAlignment Alinhamento = FieldAlignment.Center)
    {
        internal const string Todos = "Todos";
        internal const string PelaCamada = "Camada";
        internal const string Escolhidos = "Escolher";
    }

    /// <summary>A área: a entidade (para guardar a escolha nela), os vértices em planta e o nome.</summary>
    internal sealed record Area(ObjectId Id, IReadOnlyList<Point3> Vertices, string Nome);

    /// <summary>CLIVUS_USINA_OBJETOS: escolhe a área e os objetos e gera.</summary>
    [CommandMethod(PluginInfo.ComandoUsinaObjetos, CommandFlags.UsePickSet)]
    public static void UsinaObjetos()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var area = AreaEscolhida(editor, documento);
            if (area is null) return;

            var escolha = EscolherObjetos(editor, documento);
            if (escolha is null) return;

            // A única pergunta depois dos objetos (Renan, 30/10/2026: "no
            // máximo o sistema tem que pedir 'respeitar qual eixo: Direita,
            // centro ou esquerda'").
            if (EscolherAlinhamento(editor) is not { } alinhamento) return;

            Executar(editor, documento, terreno, area, escolha with { Alinhamento = alinhamento }, FileiraCommands.PerfilDaMesa(editor), Tr.T("OBJETOS"));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar conforme objetos.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar conforme os objetos: {0}\n", erro.Message));
        }
    }

    /// <summary>O mesmo na primeira área, com todos os retângulos dela, sem perguntar (nível 2).</summary>
    [CommandMethod(PluginInfo.ComandoUsinaObjetosAutomatico)]
    public static void UsinaObjetosAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);
            if (terreno is null) return;

            var areas = AreaStore.Load(documento.Database);
            if (areas.Count == 0 || LerArea(documento.Database, areas[0]) is not { } area)
            {
                editor.WriteMessage(Tr.T("\nOBJETOS Nenhuma área registrada neste desenho. Use o botão Área primeiro.\n"));
                return;
            }

            Executar(editor, documento, terreno, area, new Escolha(Escolha.Todos, string.Empty, []), MesaCommands.MesaDeExemplo(), Tr.T("OBJETOS"));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar conforme objetos (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar conforme os objetos: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// Lê os objetos, aponta as divergências, troca as mesas da área pelas dos
    /// objetos e guarda a escolha na área. Devolve se gerou. Não pergunta nada
    /// (revisão de tela do 40.2: a única pergunta é o alinhamento, antes); as
    /// mesas que a área tinha saem, e o U as devolve.
    /// </summary>
    /// <param name="perfil">A mesa quando o desenho não tem nenhuma marcada para uso.</param>
    /// <param name="prefixo">O começo das linhas do relatório (OBJETOS, REFAZER).</param>
    internal static bool Executar(
        Editor editor, Document documento, ProcessedTerrain terreno, Area area, Escolha escolha, TableProfile perfil, string prefixo)
    {
        var database = documento.Database;

        // As mesas marcadas para uso (com a mesma inclinação, que o
        // MesasEmUso confere); sem nenhuma, a mesa de sempre.
        var doDesenho = UsinaCommands.MesasEmUso(editor, database, prefixo);
        if (doDesenho is null) return false;

        IReadOnlyList<TableProfile> tipos = doDesenho.Count > 0 ? [.. doDesenho.Select(m => m.Profile)] : [perfil];
        var tiposDoDesenho = doDesenho.Count > 0 ? UsinaCommands.Tipos(doDesenho).Tipos : null;

        var settings = ConfigCommands.Inicial(documento, out var aviso);
        if (aviso is not null) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}\n", aviso));
        foreach (var nota in tipos.Select(t => t.Frame).Select(settings.EmbedmentNote).OfType<string>().Distinct())
            editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", nota));
        UsinaCommands.AvisarAltura(editor, settings, tipos);

        // Para onde as mesas olham é do projeto: o objeto em 2D não diz (e o
        // Z dele não vale). O lado errado do hemisfério é avisado, não barrado;
        // um aviso que falha não derruba o comando (revisão do 38.9).
        double? latitude = null;
        try
        {
            if (area.Vertices.Count > 0)
                latitude = GeoStore.Read(database, new Point3d(area.Vertices[0].X, area.Vertices[0].Y, area.Vertices[0].Z))?.Latitude;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler a latitude para o aviso do hemisfério.", erro);
        }

        if (FacingFromLine.HemisphereWarning(latitude, settings.Configuration.FacingAzimuthRadians) is { } hemisferio)
            editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.\n", hemisferio));

        var objetos = LerObjetos(database, area, escolha);

        if (objetos.Count == 0)
        {
            editor.WriteMessage(Tr.F("\n{0} Nenhum retângulo de 4 vértices (polilinha fechada, 2D ou 3D) dentro de {1}: nada foi gerado.\n", prefixo, area.Nome));
            return false;
        }

        var levantamento = FieldObjects.Survey(objetos, area.Vertices, rowBreak: settings.Configuration.MaxGapBeforeBreak);
        var casadas = FieldPlacement.Match(levantamento.Rows.SelectMany(r => r.Tables), tipos, out var semMesa);

        var casadosIds = casadas.Select(c => c.Footprint.Id).ToHashSet();
        var problemas = levantamento.Issues.Where(i => i.Kind != FieldIssueKind.Size || !casadosIds.Contains(i.Id)).Concat(semMesa).ToList();

        // Onde marcar cada objeto: o centro da pegada, ou a média dos vértices de quem não virou pegada.
        var centros = levantamento.Footprints.ToDictionary(f => f.Id, f => f.Center);
        foreach (var o in objetos.Where(o => !centros.ContainsKey(o.Id) && o.Vertices.Count > 0))
            centros[o.Id] = new Point3(o.Vertices.Average(p => p.X), o.Vertices.Average(p => p.Y), 0);

        Relatar(editor, prefixo, area, objetos.Count, levantamento, casadas, tipos, problemas, settings.Configuration, escolha.Alinhamento);

        if (casadas.Count == 0)
        {
            Marcar(editor, database, area, problemas, centros, terreno.Mesh, levantamento.ReferenceAxis);
            editor.WriteMessage(Tr.F("\n{0} Nenhum objeto casa com as mesas em uso: nada foi gerado.\n", prefixo));
            return false;
        }

        editor.WriteMessage(Tr.F("\nProcessando {0} mesa(s) em {1} fileira(s), conforme os objetos...\n", casadas.Count, levantamento.Rows.Count));

        void Progresso(int feitas, int total)
        {
            if (feitas % 10 == 0 || feitas == total) editor.WriteMessage(Tr.F("  {0}/{1} fileira(s)\n", feitas, total));
        }

        // Processa ANTES de apagar: se o motor falhar, as mesas da área ficam.
        var usina = FieldPlacement.Process(levantamento, tipos, terreno.Mesh, settings, Progresso, escolha.Alinhamento);

        var apagadas = LayoutEraser.ApagarDentro(database, area.Vertices);
        CarportCommands.LimparFilasSemVao(database);
        var orfas = LayoutEraser.ApagarOrfasDentro(database, area.Vertices);
        if (apagadas.Tables.Count > 0) RemovalStore.Remove(database, apagadas.Tables);
        if (apagadas.Tables.Count > 0 || orfas > 0)
            editor.WriteMessage(Tr.F("\n{0} {1} mesa(s) da área saíram ({2} entidade(s)); U devolve.\n", prefixo, apagadas.Tables.Count, apagadas.Entities + orfas));

        UsinaCommands.Desenhar(editor, documento, new UsinaCommands.PlanoDaUsina(
            settings, usina.Geometries[0], tipos[0], usina.Plant.Layout, usina.Plant, tiposDoDesenho));

        // O que só se vê depois do motor: a mesa longe do lugar e a mesa que
        // entrou na vizinha (o objeto mais curto que ela; revisão do 38.9).
        var depoisDoMotor = usina.Issues.Where(i => i.Kind is FieldIssueKind.Deviation or FieldIssueKind.TableOverlap).ToList();
        problemas.AddRange(depoisDoMotor);
        var desvios = usina.DeviationByObject.Values.ToList();
        if (desvios.Count > 0)
            editor.WriteMessage(Tr.F("\n{0} As mesas ficaram a no máximo {1:0.000} m do lugar no objeto (média {2:0.000} m).\n", prefixo, desvios.Max(), desvios.Average()));
        if (depoisDoMotor.Count(i => i.Kind == FieldIssueKind.TableOverlap) is var sobrepostas and > 0)
            editor.WriteMessage(Tr.F("\n{0} ATENÇÃO: {1} mesa(s) entraram na vizinha da fileira: os objetos são mais curtos que a mesa. Estão marcadas no desenho.\n", prefixo, sobrepostas));
        foreach (var p in depoisDoMotor.Take(30)) editor.WriteMessage($"    {p.Message}\n");

        Marcar(editor, database, area, problemas, centros, terreno.Mesh, levantamento.ReferenceAxis);
        Lembrar(database, area.Id, escolha);

        editor.WriteMessage(Tr.F("\n{0} A área {1} segue os objetos do cliente: o Regerar área a refaz por eles.\n", prefixo, area.Nome));
        return true;
    }

    // ------------------------------------------------------ área e objetos

    /// <summary>
    /// A área selecionada (clique direito nela), a única do desenho, ou a
    /// clicada, na linha dela ou dentro dela: a linha do cliente por cima e as
    /// áreas repetidas não atrapalham mais (<see cref="EscolhaDeArea"/>).
    /// </summary>
    private static Area? AreaEscolhida(Editor editor, Document documento) =>
        EscolhaDeArea.Pedir(editor, documento) is { } area ? new Area(area.Id, area.Contorno, area.Identity.DisplayName) : null;

    private static Area? LerArea(Database database, AreaRecord registro)
    {
        if (!long.TryParse(registro.Handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)) return null;

        try
        {
            var id = database.GetObjectId(false, new Handle(valor), 0);
            if (id.IsNull || id.IsErased) return null;

            using var transacao = database.TransactionManager.StartOpenCloseTransaction();
            return transacao.GetObject(id, OpenMode.ForRead) is Polyline3d polilinha && AreaXData.Load(polilinha) is { } identidade
                ? new Area(id, FileiraCommands.Vertices(polilinha, transacao), identidade.DisplayName)
                : null;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception erro)
        {
            RegistroDeDiagnostico.Registrar($"Handle {registro.Handle} da área não resolveu.", erro);
            return null;
        }
    }

    /// <summary>"Que objetos são as mesas do cliente? [Todos/Camada/Escolher]", com Enter em Todos.</summary>
    private static Escolha? EscolherObjetos(Editor editor, Document documento)
    {
        var todos = Tr.T("Todos");
        var camada = Tr.T("Camada");
        var escolher = Tr.T("Escolher");

        var opcoes = new PromptKeywordOptions(
            Tr.F("\nQue objetos são as mesas do cliente? [{0}/{1}/{2}] <{0}>: ", todos, camada, escolher), $"{todos} {camada} {escolher}")
        {
            AllowNone = true,
        };

        var resposta = editor.GetKeywords(opcoes);
        if (resposta.Status == PromptStatus.Cancel) return null;
        if (resposta.Status != PromptStatus.OK || resposta.StringResult == todos) return new Escolha(Escolha.Todos, string.Empty, []);

        if (resposta.StringResult == camada)
        {
            var clique = editor.GetEntity(new PromptEntityOptions(Tr.T("\nClique um objeto da camada das mesas: ")));
            if (clique.Status != PromptStatus.OK) return null;

            using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();
            var nome = ((Entity)transacao.GetObject(clique.ObjectId, OpenMode.ForRead)).Layer;
            editor.WriteMessage(Tr.F("\nCamada: {0}.\n", nome));
            return new Escolha(Escolha.PelaCamada, nome, []);
        }

        var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")]);
        var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nObjetos que são mesas: ") }, filtro);
        if (selecao.Status != PromptStatus.OK) return null;

        return new Escolha(Escolha.Escolhidos, string.Empty, [.. selecao.Value.GetObjectIds().Select(id => id.Handle.ToString())]);
    }

    /// <summary>
    /// "Alinhar a mesa no objeto pela [Direita/Centro/Esquerda] &lt;Centro&gt;":
    /// quando o objeto não tem o comprimento da mesa, a diferença sobra do
    /// outro lado (no centro, metade para cada lado). Direita e esquerda como
    /// se vê na planta com o norte para cima. Null se a pessoa cancelou.
    /// </summary>
    private static FieldAlignment? EscolherAlinhamento(Editor editor)
    {
        var direita = Tr.T("Direita");
        var centro = Tr.T("Centro");
        var esquerda = Tr.T("Esquerda");

        var opcoes = new PromptKeywordOptions(
            Tr.F("\nAlinhar a mesa no objeto pela [{0}/{1}/{2}] <{1}>: ", direita, centro, esquerda), $"{direita} {centro} {esquerda}")
        {
            AllowNone = true,
        };

        var resposta = editor.GetKeywords(opcoes);
        if (resposta.Status == PromptStatus.Cancel) return null;
        if (resposta.Status != PromptStatus.OK) return FieldAlignment.Center;

        return resposta.StringResult == direita ? FieldAlignment.Right
            : resposta.StringResult == esquerda ? FieldAlignment.Left
            : FieldAlignment.Center;
    }

    /// <summary>
    /// Os objetos da escolha. Em Todos e Camada, só as polilinhas fechadas de
    /// 4 vértices com o centro dentro da área; em Escolher, tudo o que foi
    /// escolhido (o que não é retângulo sai como divergência). As polilinhas
    /// do Clivus (contorno de mesa, área, alinhamento) nunca entram.
    /// </summary>
    internal static List<FieldObject> LerObjetos(Database database, Area area, Escolha escolha)
    {
        var objetos = new List<FieldObject>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        IEnumerable<ObjectId> candidatos = escolha.Modo == Escolha.Escolhidos
            ? escolha.Handles.Select(h => PorHandle(database, h)).Where(id => !id.IsNull && !id.IsErased)
            : ((BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead)).Cast<ObjectId>();

        var camadas = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

        // Em Todos e Camada, a camada congelada ou desligada (uma revisão
        // antiga do layout) e as camadas do Clivus (o contorno do objeto do
        // TermoVision) ficam de fora (revisão do 40.2).
        bool Visivel(Entity e) =>
            !e.Layer.StartsWith(PluginInfo.PrefixoDeDados, StringComparison.OrdinalIgnoreCase)
            && (!camadas.Has(e.Layer) || transacao.GetObject(camadas[e.Layer], OpenMode.ForRead) is not LayerTableRecord camada || (!camada.IsFrozen && !camada.IsOff));

        foreach (var id in candidatos)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve curva || curva is not (Polyline or Polyline2d or Polyline3d)) continue;
            if (escolha.Modo != Escolha.Escolhidos && !Visivel(curva)) continue;

            using (var nosso = curva.GetXDataForApplication(PluginXData.Aplicativo))
            {
                if (nosso is not null) continue;
            }

            if (escolha.Modo == Escolha.PelaCamada && !string.Equals(curva.Layer, escolha.Camada, StringComparison.OrdinalIgnoreCase)) continue;

            var vertices = Vertices(curva, transacao);

            if (escolha.Modo != Escolha.Escolhidos)
            {
                // O retângulo de GIS e de DXF repete o primeiro vértice no fim.
                if (vertices is { Count: 5 } v5 && Math.Abs(v5[0].X - v5[4].X) < 1e-6 && Math.Abs(v5[0].Y - v5[4].Y) < 1e-6) vertices.RemoveAt(4);
                if (vertices is not { Count: 4 }) continue;
                if (!Polygons.Contains(area.Vertices, vertices.Average(p => p.X), vertices.Average(p => p.Y))) continue;
            }

            objetos.Add(new FieldObject(curva.Handle.ToString(), curva.Layer, vertices ?? []));
        }

        return objetos;
    }

    /// <summary>Os vértices de uma polilinha fechada sem arco, com a cota de cada um; null se não é.</summary>
    private static List<Point3>? Vertices(Curve curva, Transaction transacao)
    {
        if (!curva.Closed && curva.StartPoint.DistanceTo(curva.EndPoint) > 1e-6) return null;

        switch (curva)
        {
            case Polyline p:
                if (Enumerable.Range(0, p.NumberOfVertices).Any(i => Math.Abs(p.GetBulgeAt(i)) > 1e-9)) return null;
                return [.. Enumerable.Range(0, p.NumberOfVertices).Select(p.GetPoint3dAt).Select(q => new Point3(q.X, q.Y, q.Z))];

            case Polyline3d p3:
                var vertices3d = p3.Cast<ObjectId>().Select(v => (PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).ToList();
                if (vertices3d.Any(v => v.VertexType != Vertex3dType.SimpleVertex)) return null;
                return [.. vertices3d.Select(v => v.Position).Select(q => new Point3(q.X, q.Y, q.Z))];

            case Polyline2d p2:
                var vertices2d = p2.Cast<ObjectId>().Select(v => (Vertex2d)transacao.GetObject(v, OpenMode.ForRead)).ToList();
                if (vertices2d.Any(v => v.VertexType != Vertex2dType.SimpleVertex || Math.Abs(v.Bulge) > 1e-9)) return null;
                return [.. vertices2d.Select(p2.VertexPosition).Select(q => new Point3(q.X, q.Y, q.Z))];

            default:
                return null;
        }
    }

    private static ObjectId PorHandle(Database database, string handle)
    {
        if (!long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)) return ObjectId.Null;

        try
        {
            return database.GetObjectId(false, new Handle(valor), 0);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return ObjectId.Null;
        }
    }

    // --------------------------------------------------------- relatório

    private static void Relatar(
        Editor editor, string prefixo, Area area, int lidos, FieldSurvey levantamento, IReadOnlyList<FieldMatch> casadas,
        IReadOnlyList<TableProfile> tipos, IReadOnlyList<FieldIssue> problemas, SystemConfiguration configuracao, FieldAlignment alinhamento)
    {
        editor.WriteMessage(Tr.F("\n{0} {1}: {2} objeto(s) lido(s), {3} viram mesa.\n", prefixo, area.Nome, lidos, casadas.Count));

        if (levantamento.Footprints.Count > 0)
        {
            editor.WriteMessage(Tr.F("  A maioria tem {0:0.###} × {1:0.###} m em planta; o eixo das fileiras está a {2:0.#}° do leste.\n",
                levantamento.ReferenceLength, levantamento.ReferenceWidth, levantamento.ReferenceAxis * 180 / Math.PI));

            // O que o objeto em 2D não diz, e de onde veio: a cota é do
            // terreno, o lado da ponta baixa é do azimute do projeto.
            var (rx, ry) = FieldPlacement.RightDirection(levantamento.ReferenceAxis);
            var ponta = FieldPlacement.Rumo(Math.Atan2(ry, rx));
            var oposta = FieldPlacement.Rumo(Math.Atan2(-ry, -rx));

            editor.WriteMessage(Tr.F("  Só X e Y dos objetos: a cota vem do terreno, e {0} (o azimute do projeto, em Configurações › Parâmetros).\n",
                FacingFromLine.Describe(configuracao.FacingAzimuthDegrees)));
            editor.WriteMessage(alinhamento switch
            {
                FieldAlignment.Right => Tr.F("  Alinhadas pela direita: a ponta da mesa mais para {0} fica na do objeto, e a diferença de comprimento sobra para {1}.\n", ponta, oposta),
                FieldAlignment.Left => Tr.F("  Alinhadas pela esquerda: a ponta da mesa mais para {0} fica na do objeto, e a diferença de comprimento sobra para {1}.\n", oposta, ponta),
                _ => Tr.T("  Alinhadas pelo centro: a diferença de comprimento sobra metade para cada lado.\n"),
            });
        }

        foreach (var porTipo in casadas.GroupBy(c => (c.Kind, c.ModulesOnly)))
        {
            editor.WriteMessage(porTipo.Key.ModulesOnly
                ? Tr.F("  {0} × {1} (o objeto é a área dos módulos)\n", porTipo.Count(), tipos[porTipo.Key.Kind].Name)
                : Tr.F("  {0} × {1} (o objeto é a mesa inteira, com as sobras)\n", porTipo.Count(), tipos[porTipo.Key.Kind].Name));

            // A diferença típica (a mediana), quando passa da folga da rampa:
            // o objeto 3D numa rampa é mais curto em planta e é a mesma mesa
            // (revisão do 38.9: o D6D4 do Aurora dava "0,053 m a mais").
            var mediana = porTipo.OrderBy(c => c.LengthDifference).ElementAt(porTipo.Count() / 2);
            var tipica = mediana.LengthDifference;
            if (!FieldObjects.SameLength(mediana.Footprint.Length, mediana.Footprint.Length - tipica))
            {
                editor.WriteMessage(tipica < 0
                    ? Tr.F("    a mesa tem {0:0.000} m a mais que o objeto\n", -tipica)
                    : Tr.F("    a mesa tem {0:0.000} m a menos que o objeto\n", tipica));
            }
        }

        if (problemas.Count == 0)
        {
            editor.WriteMessage(Tr.T("  Nenhuma divergência: os objetos são iguais e todos viram mesa.\n"));
            return;
        }

        editor.WriteMessage(Tr.F("  {0} divergência(s): {1}\n", problemas.Count, string.Join(", ", problemas.GroupBy(p => p.Kind).Select(g => $"{g.Count()} {Curto(g.Key)}"))));
        foreach (var p in problemas.Take(40)) editor.WriteMessage($"    {p.Message}\n");
        if (problemas.Count > 40) editor.WriteMessage(Tr.F("    ... e mais {0}; cada uma está marcada no desenho.\n", problemas.Count - 40));
    }

    /// <summary>A divergência em uma ou duas palavras, para a marca no desenho e o resumo.</summary>
    internal static string Curto(FieldIssueKind tipo) => tipo switch
    {
        FieldIssueKind.NotRectangle => Tr.T("não é retângulo"),
        FieldIssueKind.Size => Tr.T("tamanho diferente"),
        FieldIssueKind.Axis => Tr.T("rumo diferente"),
        FieldIssueKind.Duplicate => Tr.T("repetido"),
        FieldIssueKind.Overlap => Tr.T("sobreposto"),
        FieldIssueKind.OutsideArea => Tr.T("fora da área"),
        FieldIssueKind.CrossesArea => Tr.T("atravessa a área"),
        FieldIssueKind.ProfileSize => Tr.T("mesa de outro tamanho"),
        FieldIssueKind.NoProfile => Tr.T("sem mesa do tamanho"),
        FieldIssueKind.Deviation => Tr.T("mesa deslocada"),
        FieldIssueKind.TableOverlap => Tr.T("mesa em cima da vizinha"),
        _ => tipo.ToString(),
    };

    /// <summary>
    /// As marcas: um círculo e o motivo em cada objeto que diverge, na cota do
    /// terreno (regra 5), na camada das divergências. As marcas da vez anterior
    /// nesta área saem antes.
    /// </summary>
    private static void Marcar(
        Editor editor, Database database, Area area, IReadOnlyList<FieldIssue> problemas, IReadOnlyDictionary<string, Point3> centros, Tin terreno, double eixo)
    {
        var marcadas = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var camada = LayoutLayers.Garantir(transacao, database, CamadaDasDivergencias, new RgbColor(230, 40, 40));
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);

            var antigas = new List<Entity>();

            foreach (ObjectId id in espaco)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e || !string.Equals(e.Layer, camada, StringComparison.OrdinalIgnoreCase)) continue;

                Point3d? onde = e switch { Circle c => c.Center, MText m => m.Location, _ => null };
                if (onde is { } q && (Polygons.Contains(area.Vertices, q.X, q.Y) || PertoDoContorno(area.Vertices, q.X, q.Y, 30))) antigas.Add(e);
            }

            foreach (var e in antigas)
            {
                ((Entity)transacao.GetObject(e.ObjectId, OpenMode.ForWrite, false, true)).Erase();
            }

            // O texto corre no eixo das fileiras, sem ficar de cabeça para baixo.
            var giro = Math.Atan2(Math.Sin(eixo), Math.Cos(eixo));
            if (giro > Math.PI / 2) giro -= Math.PI;
            if (giro < -Math.PI / 2) giro += Math.PI;

            var cotaDaArea = area.Vertices.Count > 0 ? area.Vertices.Average(p => p.Z) : 0;

            foreach (var porObjeto in problemas.Where(p => p.Id != "*").GroupBy(p => p.Id))
            {
                if (!centros.TryGetValue(porObjeto.Key, out var c)) continue;

                var z = terreno.TryGetZ(c.X, c.Y, out var cota) ? cota : cotaDaArea;

                var circulo = new Circle(new Point3d(c.X, c.Y, z), Vector3d.ZAxis, 2.0) { Layer = camada };
                espaco.AppendEntity(circulo);
                transacao.AddNewlyCreatedDBObject(circulo, true);

                var texto = new MText
                {
                    Location = new Point3d(c.X, c.Y, z),
                    Contents = porObjeto.Key + ": " + string.Join(", ", porObjeto.Select(p => Curto(p.Kind)).Distinct()),
                    TextHeight = 0.6,
                    Rotation = giro,
                    Attachment = AttachmentPoint.MiddleCenter,
                    Layer = camada,
                };
                espaco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
                marcadas++;
            }

            transacao.Commit();
        }

        if (marcadas > 0)
            editor.WriteMessage(Tr.F("  {0} objeto(s) marcado(s) no desenho, na camada {1}.\n", marcadas, CamadaDasDivergencias));
    }

    // ---------------------------------------------- a área lembra a escolha

    /// <summary>Guarda no dicionário da área que ela segue os objetos, e como eles foram escolhidos.</summary>
    private static void Lembrar(Database database, ObjectId areaId, Escolha escolha)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var area = (Entity)transacao.GetObject(areaId, OpenMode.ForWrite, false, true);
        if (area.ExtensionDictionary.IsNull) area.CreateExtensionDictionary();

        var dicionario = (DBDictionary)transacao.GetObject(area.ExtensionDictionary, OpenMode.ForWrite);

        // O centro sai na versão 1, que o plugin de antes lê; só o alinhamento
        // pela ponta pede a 2 (revisão do 38.9: o plugin de antes, sem ler a
        // 2, esquecia que a área segue os objetos).
        var pelaPonta = escolha.Alinhamento != FieldAlignment.Center;
        using var dados = new ResultBuffer(
            new TypedValue((int)DxfCode.Int32, pelaPonta ? VersaoDoRegistro : 1),
            new TypedValue((int)DxfCode.Text, escolha.Modo),
            new TypedValue((int)DxfCode.Text, escolha.Camada));
        if (pelaPonta) dados.Add(new TypedValue((int)DxfCode.Text, escolha.Alinhamento.ToString()));
        foreach (var handle in escolha.Handles) dados.Add(new TypedValue((int)DxfCode.Text, handle));

        if (dicionario.Contains(ChaveDoRegistro))
        {
            ((Xrecord)transacao.GetObject(dicionario.GetAt(ChaveDoRegistro), OpenMode.ForWrite)).Data = dados;
        }
        else
        {
            var registro = new Xrecord { Data = dados };
            dicionario.SetAt(ChaveDoRegistro, registro);
            transacao.AddNewlyCreatedDBObject(registro, true);
        }

        transacao.Commit();
    }

    /// <summary>A escolha guardada na área, ou null se ela não segue objetos.</summary>
    private static Escolha? Lembrada(Transaction transacao, Entity area)
    {
        if (area.ExtensionDictionary.IsNull) return null;

        var dicionario = (DBDictionary)transacao.GetObject(area.ExtensionDictionary, OpenMode.ForRead);
        if (!dicionario.Contains(ChaveDoRegistro)) return null;

        using var dados = ((Xrecord)transacao.GetObject(dicionario.GetAt(ChaveDoRegistro), OpenMode.ForRead)).Data;
        var valores = dados?.AsArray() ?? [];
        if (valores.Length < 3 || valores[0].Value is not int versao || versao is not (1 or VersaoDoRegistro)) return null;

        var modo = valores[1].Value as string ?? string.Empty;
        if (modo is not (Escolha.Todos or Escolha.PelaCamada or Escolha.Escolhidos)) return null;

        // A versão 1 não tinha o alinhamento: era o centro.
        var alinhamento = FieldAlignment.Center;
        var handles = valores.Skip(3);

        if (versao == VersaoDoRegistro)
        {
            if (valores.Length < 4 || !Enum.TryParse(valores[3].Value as string, ignoreCase: false, out alinhamento) || !Enum.IsDefined(alinhamento)) return null;
            handles = valores.Skip(4);
        }

        return new Escolha(modo, valores[2].Value as string ?? string.Empty, [.. handles.Select(v => v.Value as string).OfType<string>()], alinhamento);
    }

    /// <summary>
    /// A área destes vértices, se ela segue os objetos do cliente: o Regerar
    /// área (e o de todas) a refaz pelos objetos, e o Regerar fileira não mexe
    /// nela.
    /// </summary>
    internal static (Area Area, Escolha Escolha)? PelosObjetos(Database database, IReadOnlyList<Point3> vertices)
    {
        foreach (var registro in AreaStore.Load(database))
        {
            if (LerArea(database, registro) is not { } area || !Iguais(area.Vertices, vertices)) continue;

            using var transacao = database.TransactionManager.StartOpenCloseTransaction();
            return Lembrada(transacao, (Entity)transacao.GetObject(area.Id, OpenMode.ForRead)) is { } escolha ? (area, escolha) : null;
        }

        return null;
    }

    /// <summary>
    /// O Regerar (área, todas e o automático) da área que segue os objetos:
    /// refaz pelos objetos, sem alinhamento, e devolve true; false se a área
    /// não segue objetos (o Regerar segue como sempre).
    /// </summary>
    /// <returns>Null se a área não segue objetos; senão, se gerou.</returns>
    internal static bool? RefazerSeSegueObjetos(Editor editor, Document documento, ProcessedTerrain terreno, IReadOnlyList<Point3> vertices, Func<TableProfile> perfil)
    {
        if (PelosObjetos(documento.Database, vertices) is not { } campo) return null;

        return Executar(editor, documento, terreno, campo.Area, campo.Escolha, perfil(), Tr.T("REFAZER"));
    }

    /// <summary>
    /// A área deixa de seguir os objetos (revisão do 40.2): a Usina pelo
    /// alinhamento e o Apagar tudo. Devolve se ela seguia.
    /// </summary>
    internal static bool Esquecer(Database database, IReadOnlyList<Point3> vertices)
    {
        if (PelosObjetos(database, vertices) is not { } campo) return false;

        using var transacao = database.TransactionManager.StartTransaction();
        var area = (Entity)transacao.GetObject(campo.Area.Id, OpenMode.ForRead);

        if (!area.ExtensionDictionary.IsNull)
        {
            var dicionario = (DBDictionary)transacao.GetObject(area.ExtensionDictionary, OpenMode.ForWrite, false, true);
            if (dicionario.Contains(ChaveDoRegistro)) transacao.GetObject(dicionario.GetAt(ChaveDoRegistro), OpenMode.ForWrite, false, true).Erase();
        }

        transacao.Commit();
        return true;
    }

    /// <summary>Se o ponto está a até <paramref name="distancia"/> do contorno.</summary>
    private static bool PertoDoContorno(IReadOnlyList<Point3> area, double x, double y, double distancia)
    {
        for (var i = 0; i < area.Count; i++)
        {
            var a = area[i];
            var b = area[(i + 1) % area.Count];
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var l2 = dx * dx + dy * dy;
            var t = l2 < 1e-12 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
            var (qx, qy) = (a.X + t * dx - x, a.Y + t * dy - y);
            if (qx * qx + qy * qy <= distancia * distancia) return true;
        }

        return false;
    }

    private static bool Iguais(IReadOnlyList<Point3> a, IReadOnlyList<Point3> b) =>
        a.Count == b.Count && a.Zip(b).All(p => Math.Abs(p.First.X - p.Second.X) < 1e-6 && Math.Abs(p.First.Y - p.Second.Y) < 1e-6);
}
