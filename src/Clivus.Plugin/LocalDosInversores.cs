using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.LocalDosInversores))]

namespace Clivus.Plugin;

/// <summary>
/// O local dos inversores (Renan, 10/10/2026): "quero selecionar os
/// inversores, aí tem um botão de escolher área, eu clico no retângulo que
/// eu fiz em campo, que vai representar uma sala, um skid etc."; e a
/// "alocação automática conforme strings", que a rota CC faz no Gerar. O
/// registro <c>INVERSORES_LOCAL</c> guarda o modo de cada inversor; a área é
/// a polilinha fechada do usuário com a marca <see cref="SiteMark"/>.
/// </summary>
public static class LocalDosInversores
{
    private const string Chave = "INVERSORES_LOCAL";
    private static readonly string OQue = Tr.N("do local dos inversores");

    /// <summary>O local de cada inversor que tem um (os outros são postos à mão).</summary>
    internal static List<InverterPlacement> Ler(Database db, out string? problema)
    {
        var lido = PluginRecords.Load(db, Chave, 1, InverterPlacement.FieldCount, InverterPlacement.Parse, OQue);
        problema = lido.Problem;
        return [.. lido.Items];
    }

    internal static void Gravar(Database db, IReadOnlyList<InverterPlacement> locais) =>
        PluginRecords.Save(db, Chave, 1, InverterPlacement.FieldCount, locais, p => p.ToFields());

    /// <summary>Troca o local dos inversores (null = à mão). Recusa, com o motivo, se o registro está ilegível.</summary>
    internal static string? Mudar(Database db, IReadOnlyCollection<Guid> inversores, InverterPlacementMode? modo, Guid area = default)
    {
        var lista = Ler(db, out var problema);
        if (problema is not null) return problema;
        Gravar(db, InverterPlacement.With(lista, inversores, modo, area));
        return null;
    }

    /// <summary>As áreas de inversores do desenho: a marca e o contorno em planta.</summary>
    internal static Dictionary<Guid, (SiteMark Marca, IReadOnlyList<Point3> Contorno)> Areas(Database db)
    {
        var areas = new Dictionary<Guid, (SiteMark, IReadOnlyList<Point3>)>();
        using var t = db.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(Curve));

        foreach (ObjectId id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classe) || t.GetObject(id, OpenMode.ForRead) is not Curve c || Marca(c) is not { } marca) continue;
            if (Contorno(c, t) is { Count: >= 3 } contorno) areas[marca.Id] = (marca, contorno);
        }

        return areas;
    }

    private static SiteMark? Marca(Entity e) =>
        PluginXData.Load(e, SiteMark.Tipo, 1, SiteMark.FieldCount) is { } c ? SiteMark.Parse(c) : null;

    /// <summary>Os vértices em planta de uma polilinha fechada (2D ou 3D); null se não é fechada.</summary>
    private static List<Point3>? Contorno(Curve c, Transaction t)
    {
        switch (c)
        {
            case Polyline p when p.Closed || p.StartPoint.DistanceTo(p.EndPoint) < 1e-6:
                return Enumerable.Range(0, p.NumberOfVertices).Select(i => p.GetPoint3dAt(i)).Select(q => new Point3(q.X, q.Y, 0)).ToList();
            case Polyline3d p3 when p3.Closed:
                return p3.Cast<ObjectId>().Where(v => !v.IsErased)
                    .Select(v => ((PolylineVertex3d)t.GetObject(v, OpenMode.ForRead)).Position).Select(q => new Point3(q.X, q.Y, 0)).ToList();
            case Polyline2d p2 when p2.Closed:
                return p2.Cast<ObjectId>().Where(v => !v.IsErased)
                    .Select(v => ((Vertex2d)t.GetObject(v, OpenMode.ForRead)).Position).Select(q => new Point3(q.X, q.Y, 0)).ToList();
            default:
                return null;
        }
    }

    /// <summary>
    /// CLIVUS_ELETRICA_LOCAL: o modo (Area, Automatico ou Manual) e os
    /// inversores (nomes ou GUIDs separados por ";"). Área: clique na
    /// polilinha fechada; ela ganha a marca de área (a que já tinha fica) e
    /// os inversores são postos dentro dela, um ao lado do outro, na cota do
    /// terreno + 0,80. Automático: o Gerar da rota CC põe ao lado da vala.
    /// Manual: volta ao Pôr em campo de sempre (a posição de agora fica).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoEletricaLocal)]
    public static void Local()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            var modo = new PromptKeywordOptions(Tr.T("\nLocal dos inversores [Area/Automatico/Manual]: ")) { AllowNone = false };
            modo.Keywords.Add("Area");
            modo.Keywords.Add("Automatico");
            modo.Keywords.Add("Manual");
            var qual = editor.GetKeywords(modo);
            if (qual.Status != PromptStatus.OK) return;

            var lista = editor.GetString(new PromptStringOptions(Tr.T("\nInversores (nomes separados por ;): ")) { AllowSpaces = true });
            if (lista.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
            var inversores = new List<Inverter>();
            foreach (var nome in lista.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var achado = Guid.TryParse(nome, out var g) ? setup.FindInverter(g) : setup.Inverters.FirstOrDefault(i => string.Equals(i.Name, nome, StringComparison.CurrentCultureIgnoreCase));
                if (achado is null) editor.WriteMessage(Tr.F("\nLOCAL Não há inversor \"{0}\".\n", nome));
                else if (!inversores.Contains(achado)) inversores.Add(achado);
            }

            if (inversores.Count == 0)
            {
                editor.WriteMessage(Tr.T("\nLOCAL Nenhum inversor; nada mudou.\n"));
                return;
            }

            var ids = inversores.Select(i => i.Id).ToList();

            if (qual.StringResult != "Area")
            {
                var novo = qual.StringResult == "Automatico" ? InverterPlacementMode.Automatic : (InverterPlacementMode?)null;
                if (Mudar(db, ids, novo) is { } problema)
                {
                    editor.WriteMessage(Tr.F("\nLOCAL Não gravei: {0}\n", problema));
                    return;
                }

                editor.WriteMessage(novo is null
                    ? Tr.F("\nLOCAL {0} inversor(es) de volta ao Pôr em campo à mão (a posição de agora fica).\n", ids.Count)
                    : Tr.F("\nLOCAL {0} inversor(es) automáticos: o Gerar da rota CC põe cada um ao lado da vala, no ponto de menor cabo das strings dele.\n", ids.Count));
                return;
            }

            if (FileiraCommands.ExigirTerreno(editor, documento) is not { } terreno) return;

            var opcoes = new PromptEntityOptions(Tr.T("\nClique no retângulo (polilinha fechada) da área dos inversores: "));
            opcoes.SetRejectMessage(Tr.T("\nTem que ser uma polilinha."));
            opcoes.AddAllowedClass(typeof(Polyline), false);
            opcoes.AddAllowedClass(typeof(Polyline2d), false);
            opcoes.AddAllowedClass(typeof(Polyline3d), false);
            var clicado = editor.GetEntity(opcoes);
            if (clicado.Status != PromptStatus.OK) return;

            if (MarcarArea(db, clicado.ObjectId, terreno.Mesh) is not { } area)
            {
                editor.WriteMessage(Tr.T("\nLOCAL A polilinha não é fechada: feche-a (ou desenhe um retângulo) e tente de novo.\n"));
                return;
            }

            if (Mudar(db, ids, InverterPlacementMode.Area, area.Marca.Id) is { } erro)
            {
                editor.WriteMessage(Tr.F("\nLOCAL Não gravei: {0}\n", erro));
                return;
            }

            var centros = InverterSites.InArea(area.Contorno, inversores.Select(i => Tamanho(setup, i)).ToList());
            var postos = 0;
            for (var i = 0; i < inversores.Count; i++)
                if (centros[i] is { } c && setup.FindEquipment(EquipmentKind.Inverter, inversores[i].Id) is { } equipamento
                    && ConfiguracaoEletricaCommands.NoTerreno(editor, db, terreno, equipamento, c.X, c.Y))
                    postos++;

            editor.WriteMessage(Tr.F("\nLOCAL {0} de {1} inversor(es) postos na {2}.\n", postos, inversores.Count, area.Marca.Name));
            var fora = inversores.Where((_, i) => centros[i] is null).Select(i => i.Name).ToList();
            if (fora.Count > 0)
                editor.WriteMessage(Tr.F("  ATENÇÃO: não couberam na área: {0}. Aumente o retângulo ou ponha à mão.\n", string.Join(", ", fora)));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao escolher o local dos inversores.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui escolher o local dos inversores: {0}\n", erro.Message));
        }
        finally
        {
            JanelaEletrica.Voltar(documento);
        }
    }

    /// <summary>A largura e o comprimento do retângulo do inversor (do modelo).</summary>
    internal static (double Width, double Length) Tamanho(ElectricalSetup setup, Inverter inversor) =>
        setup.FindEquipment(EquipmentKind.Inverter, inversor.Id) is { } e ? (e.Size.Width, e.Size.Length) : (1, 1);

    /// <summary>
    /// A polilinha fechada vira área de inversores: ganha a marca (a que já
    /// tinha fica, com o mesmo GUID) e é assentada no terreno (regra
    /// universal: todo desenho respeita o TIN), virando uma Polyline3d
    /// fechada com as propriedades e o XData da antiga. Null se não é fechada.
    /// </summary>
    private static (SiteMark Marca, IReadOnlyList<Point3> Contorno)? MarcarArea(Database db, ObjectId id, Tin terreno)
    {
        var existentes = Areas(db);

        using var t = db.TransactionManager.StartTransaction();
        if (t.GetObject(id, OpenMode.ForRead) is not Curve curva || Contorno(curva, t) is not { Count: >= 3 } contorno) return null;

        var marca = Marca(curva) ?? new SiteMark(Guid.NewGuid(), Tr.F("Área {0}", existentes.Count + 1));

        // Fechada: o primeiro vértice repetido no fim, para o drapeado fechar o último lado.
        var noChao = Draping.Along(terreno, [.. contorno, contorno[0]]).Vertices.ToList();
        if (noChao.Count > 1 && Math.Abs(noChao[0].X - noChao[^1].X) < 1e-6 && Math.Abs(noChao[0].Y - noChao[^1].Y) < 1e-6) noChao.RemoveAt(noChao.Count - 1);

        var espaco = (BlockTableRecord)t.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        var nova = new Polyline3d { Closed = true };
        nova.SetPropertiesFrom(curva);
        espaco.AppendEntity(nova);
        t.AddNewlyCreatedDBObject(nova, true);
        foreach (var p in noChao)
        {
            var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            nova.AppendVertex(v);
            t.AddNewlyCreatedDBObject(v, true);
        }

        using (var xdata = curva.XData) nova.XData = xdata;
        PluginXData.Save(t, nova, SiteMark.Tipo, 1, [.. marca.ToFields()]);

        curva.UpgradeOpen();
        curva.Erase();
        t.Commit();
        return (marca, contorno);
    }
}
