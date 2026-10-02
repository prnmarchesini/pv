using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// A marca do grupo no desenho (7.9, pedido do Renan em 26/09/2026: "um
/// contorno com um hatch, igual o PVcase, e no centro um número"): a casca
/// convexa dos cantos das mesas como polilinha fechada, um hachurado sólido
/// translúcido dentro, e o letreiro com o número e o nome no centro. Tudo
/// na camada do grupo, com o GUID do grupo no XData; apagar o grupo apaga
/// a marca. A marca não é peça de mesa: a varredura das mesas não a vê.
///
/// Por cima dos módulos (02/10/2026, com print da hachura escondida: "o
/// hachurado tem que ficar por cima"): a cota é a do canto mais alto das
/// mesas mais uma folga (em 3D e em vista inclinada ela não afunda nos
/// módulos; regra 5: acompanha as mesas, que acompanham o terreno), e na
/// ordem de desenho contorno, hachura e número vão para a frente, o número
/// por último.
/// </summary>
internal static class GroupDrawer
{
    private const double AlturaDoNumero = 3.0;

    /// <summary>Quanto a marca fica acima do canto mais alto das mesas, em metro.</summary>
    internal const double FolgaAcimaDasMesas = 0.30;

    /// <summary>Desenha a marca. Devolve quantas entidades criou (0 se não há canto para contornar).</summary>
    internal static int Desenhar(Database database, TableGroup grupo, IReadOnlyList<Point3> cantosDasMesas)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ArgumentNullException.ThrowIfNull(cantosDasMesas);

        var casca = ConvexHull.Of(cantosDasMesas);
        if (casca.Count < 3) return 0;

        var cota = cantosDasMesas.Where(p => p.IsFinite).Select(p => p.Z).DefaultIfEmpty(0).Max() + FolgaAcimaDasMesas;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Grupo, new RgbColor(80, 180, 120));
        var marca = new GroupMarkIdentity(grupo.Id);
        var criadas = 0;
        var naFrente = new List<ObjectId>();

        var contorno = new Polyline(casca.Count) { Closed = true, Layer = camada, Elevation = cota };

        for (var i = 0; i < casca.Count; i++) contorno.AddVertexAt(i, new Point2d(casca[i].X, casca[i].Y), 0, 0, 0);

        espaco.AppendEntity(contorno);
        transacao.AddNewlyCreatedDBObject(contorno, true);
        LayoutXData.SaveGroupMark(transacao, contorno, marca);
        naFrente.Add(contorno.ObjectId);
        criadas++;

        try
        {
            // Os padrões do banco ANTES da camada: SetDatabaseDefaults
            // reescreve camada e cor com as correntes.
            var hachura = new Hatch();
            hachura.SetDatabaseDefaults(database);
            hachura.Layer = camada;
            hachura.Elevation = cota;

            espaco.AppendEntity(hachura);
            transacao.AddNewlyCreatedDBObject(hachura, true);

            hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            hachura.Associative = true;
            hachura.AppendLoop(HatchLoopTypes.External, [contorno.ObjectId]);
            hachura.EvaluateHatch(true);
            hachura.Transparency = new Transparency(51); // 80 % transparente
            LayoutXData.SaveGroupMark(transacao, hachura, marca);
            naFrente.Add(hachura.ObjectId);
            criadas++;
        }
        catch (System.Exception erro)
        {
            // Sem hachura o contorno e o número ainda dizem o grupo.
            RegistroDeDiagnostico.Registrar("Falha ao hachurar o grupo; fica sem hachura.", erro);
        }

        var centro = ConvexHull.Centroid(casca);
        var letreiro = new MText
        {
            Location = new Point3d(centro.X, centro.Y, cota + 0.2),
            TextHeight = AlturaDoNumero,
            Layer = camada,
            Attachment = AttachmentPoint.MiddleCenter,
            Contents = grupo.Caption,
        };

        espaco.AppendEntity(letreiro);
        transacao.AddNewlyCreatedDBObject(letreiro, true);
        EstiloDoProjeto.PrepararTexto(transacao, database)(letreiro);
        LayoutXData.SaveGroupMark(transacao, letreiro, marca);
        naFrente.Add(letreiro.ObjectId);
        criadas++;

        // A ordem da lista é a ordem final: o número fica na frente da hachura.
        var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
        ordem.MoveToTop(new ObjectIdCollection(naFrente.ToArray()));

        transacao.Commit();

        return criadas;
    }

    /// <summary>Apaga a marca de um grupo. Quantas entidades foram.</summary>
    internal static int Apagar(Database database, Guid grupo)
    {
        var apagadas = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        foreach (var id in Marcas(transacao, database, grupo))
        {
            var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
            entidade.Erase();
            apagadas++;
        }

        transacao.Commit();

        return apagadas;
    }

    /// <summary>As entidades da marca de um grupo (ou de todos, com GUID vazio).</summary>
    internal static List<ObjectId> Marcas(Transaction transacao, Database database, Guid grupo)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var marcas = new List<ObjectId>();

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDaPolilinha && id.ObjectClass != ClasseDaHachura && id.ObjectClass != ClasseDoTexto) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;
            if (LayoutXData.LoadGroupMark(entidade) is not { } marca) continue;
            if (grupo != Guid.Empty && marca.Group != grupo) continue;

            marcas.Add(id);
        }

        return marcas;
    }

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaPolilinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaHachura = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Hatch));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
}
