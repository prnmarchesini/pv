using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// A hachura das áreas (41.2, Renan, 30/10/2026: "de marcar cada área com
/// rachura"): um hatch SOLID por área, na cor dela (<see cref="AreaHatch.ColorOf"/>),
/// 70 % transparente para as mesas aparecerem por baixo, e o nome da área no
/// meio, na camada <see cref="Camada"/>. O XData (<see cref="AreaHatchMark"/>)
/// diz de que área é: hachurar de novo troca a antiga, e "Tirar" leva só as dela.
/// <para>
/// Por cima das mesas, como a marca do grupo (<see cref="GroupDrawer"/>,
/// 02/10/2026: "o hachurado tem que ficar por cima"): na ordem de desenho o
/// hatch e o nome vão para a frente, e a cota vem de <see cref="AreaHatch.Elevation"/>
/// (a mais alta do terreno dentro da área e das mesas dela, mais a folga do
/// grupo; nunca a cota de um clique, regra sagrada 5).
/// </para>
/// </summary>
internal static class HachuraDasAreas
{
    /// <summary>A camada das hachuras e dos nomes das áreas.</summary>
    internal const string Camada = PluginInfo.PrefixoDeDados + "_AREAS_HACHURA";

    private const int Versao = 1;

    private static readonly RXClass ClasseDaHachura = RXObject.GetClass(typeof(Hatch));
    private static readonly RXClass ClasseDoTexto = RXObject.GetClass(typeof(MText));

    /// <summary>Uma entidade da hachura de área no desenho: o hatch ou o nome, de qual área, e a cor.</summary>
    internal sealed record Marca(ObjectId Id, AreaHatchMark Dono, bool EHachura, RgbColor? Cor);

    /// <summary>Uma área hachurada agora: o hatch, a cota dele e a cor.</summary>
    internal sealed record Feita(AreaNoDesenho Area, ObjectId Hachura, double Cota, RgbColor Cor);

    /// <summary>O que o hachurar fez: as feitas, as que não deu (com o motivo) e quantas entidades antigas saíram.</summary>
    internal sealed record Resultado(IReadOnlyList<Feita> Feitas, IReadOnlyList<string> Recusas, int Apagadas);

    /// <summary>As entidades das hachuras de área do desenho (hatch e nome).</summary>
    internal static List<Marca> Marcas(Transaction transacao, Database database)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var achadas = new List<Marca>();

        foreach (ObjectId id in espaco)
        {
            if (id.IsErased || (id.ObjectClass != ClasseDaHachura && id.ObjectClass != ClasseDoTexto)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (PluginXData.Load(e, AreaHatchMark.Tipo, Versao, AreaHatchMark.FieldCount) is not { } campos || !Guid.TryParse(campos[0], out var area)) continue;

            var dono = new AreaHatchMark(area, campos[1]);
            if (!dono.IsValid) continue;

            RgbColor? cor = e.Color.ColorMethod == ColorMethod.ByColor ? new RgbColor(e.Color.Red, e.Color.Green, e.Color.Blue) : null;
            achadas.Add(new Marca(id, dono, e is Hatch, cor));
        }

        return achadas;
    }

    /// <summary>As marcas que são desta área (pela polilinha; ver <see cref="AreaHatchMark.Belongs"/>).</summary>
    internal static IEnumerable<Marca> DaArea(IEnumerable<Marca> marcas, AreaNoDesenho area, IReadOnlyList<AreaNoDesenho> todas)
    {
        var pares = todas.Select(a => (a.Identity.Id, a.Handle)).ToList();
        return marcas.Where(m => m.Dono.Belongs(area.Identity.Id, area.Handle, pares));
    }

    /// <summary>
    /// Hachura as áreas pedidas (numa transação), trocando a hachura antiga
    /// de cada uma. A cor é a da posição da área em <paramref name="todas"/>.
    /// </summary>
    /// <param name="todas">Todas as áreas do desenho, na ordem da janela.</param>
    /// <param name="alvos">As que vão ser hachuradas.</param>
    /// <param name="terreno">O terreno processado, ou null (a cota sai dos vértices das áreas).</param>
    internal static Resultado Desenhar(Database database, IReadOnlyList<AreaNoDesenho> todas, IReadOnlyList<AreaNoDesenho> alvos, Tin? terreno)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, Camada, new RgbColor(128, 128, 128));
        var mesas = CantosDasMesas(transacao, database);
        var marcas = Marcas(transacao, database);

        var feitas = new List<Feita>();
        var recusas = new List<string>();
        var naFrente = new List<ObjectId>();
        var apagadas = 0;

        foreach (var area in alvos)
        {
            foreach (var antiga in DaArea(marcas, area, todas))
            {
                if (antiga.Id.IsErased) continue;
                transacao.GetObject(antiga.Id, OpenMode.ForWrite).Erase();
                apagadas++;
            }

            var cota = AreaHatch.Elevation(terreno, area.Contorno, mesas, GroupDrawer.FolgaAcimaDasMesas);
            if (cota is not { } z)
            {
                recusas.Add(Tr.F("{0}: o contorno não fecha uma área; ficou sem hachura.", area.Identity.DisplayName));
                continue;
            }

            var posicao = Math.Max(0, todas.ToList().FindIndex(a => a.Handle == area.Handle));
            var cor = AreaHatch.ColorOf(posicao);
            var marca = new AreaHatchMark(area.Identity.Id, area.Handle);

            Hatch? hachura = null;
            try
            {
                // Os padrões do banco ANTES da camada: SetDatabaseDefaults reescreve camada e cor.
                hachura = new Hatch();
                hachura.SetDatabaseDefaults(database);
                hachura.Layer = camada;
                hachura.Color = Color.FromRgb(cor.R, cor.G, cor.B);
                hachura.Normal = Vector3d.ZAxis;
                hachura.Elevation = z;

                espaco.AppendEntity(hachura);
                transacao.AddNewlyCreatedDBObject(hachura, true);

                hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                hachura.Associative = false;
                hachura.HatchStyle = HatchStyle.Normal;
                Laco(hachura, area.Contorno);
                hachura.EvaluateHatch(true);
                hachura.Transparency = new Transparency(AreaHatch.Alpha);
                PluginXData.Save(transacao, hachura, AreaHatchMark.Tipo, Versao, marca.Area.ToString("D"), marca.AreaHandle);
            }
            catch (System.Exception erro)
            {
                // Contorno que o AutoCAD não hachura (laço que se cruza): a área fica sem, e as outras seguem.
                RegistroDeDiagnostico.Registrar($"Não consegui hachurar a área {area.Identity.DisplayName}.", erro);
                if (hachura is not null)
                {
                    if (hachura.ObjectId.IsNull) hachura.Dispose();
                    else if (!hachura.IsErased) hachura.Erase();
                }

                recusas.Add(Tr.F("{0}: o AutoCAD não hachurou o contorno ({1}).", area.Identity.DisplayName, erro.Message));
                continue;
            }

            var centro = AreaHatch.LabelPoint(area.Contorno);
            var nome = new MText();
            nome.SetDatabaseDefaults(database);
            nome.Layer = camada;
            nome.Color = Color.FromRgb(cor.R, cor.G, cor.B);
            nome.Normal = Vector3d.ZAxis;
            nome.Location = new Point3d(centro.X, centro.Y, z + 0.05);
            nome.TextHeight = AreaHatch.LabelHeight(InverterSites.PlanArea(area.Contorno));
            nome.Attachment = AttachmentPoint.MiddleCenter;
            nome.Contents = AreaHatch.LabelText(area.Identity.DisplayName);

            espaco.AppendEntity(nome);
            transacao.AddNewlyCreatedDBObject(nome, true);
            PluginXData.Save(transacao, nome, AreaHatchMark.Tipo, Versao, marca.Area.ToString("D"), marca.AreaHandle);

            naFrente.Add(hachura.ObjectId);
            naFrente.Add(nome.ObjectId);
            feitas.Add(new Feita(area, hachura.ObjectId, z, cor));
        }

        // Na frente das mesas, o nome por último (a ordem da lista é a final).
        if (naFrente.Count > 0)
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
            ordem.MoveToTop(new ObjectIdCollection(naFrente.ToArray()));
        }

        transacao.Commit();
        return new Resultado(feitas, recusas, apagadas);
    }

    /// <summary>Tira a hachura (hatch e nome) de uma área, ou de todas (null; as de área que não existe mais também). Quantas entidades saíram.</summary>
    internal static int Apagar(Database database, IReadOnlyList<AreaNoDesenho> todas, AreaNoDesenho? area)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var marcas = Marcas(transacao, database);
        var alvo = area is null ? marcas : DaArea(marcas, area, todas).ToList();

        foreach (var m in alvo) transacao.GetObject(m.Id, OpenMode.ForWrite).Erase();

        transacao.Commit();
        return alvo.Count;
    }

    /// <summary>Troca o nome escrito na hachura da área (renomear).</summary>
    internal static int TrocarNome(Transaction transacao, Database database, AreaNoDesenho area, IReadOnlyList<AreaNoDesenho> todas, string nome)
    {
        var trocados = 0;

        foreach (var m in DaArea(Marcas(transacao, database), area, todas).Where(m => !m.EHachura))
        {
            if (transacao.GetObject(m.Id, OpenMode.ForWrite) is not MText texto) continue;
            texto.Contents = AreaHatch.LabelText(nome);
            trocados++;
        }

        return trocados;
    }

    /// <summary>Os cantos (em 3D) do contorno de cada mesa do desenho.</summary>
    internal static List<IReadOnlyList<Point3>> CantosDasMesas(Transaction transacao, Database database)
    {
        var cantos = new List<IReadOnlyList<Point3>>();

        foreach (var mesa in LayoutScan.Tables(transacao, database).Values)
            foreach (var id in mesa.Contours)
                if (transacao.GetObject(id, OpenMode.ForRead) is Polyline3d contorno)
                    cantos.Add(AreasDoDesenho.Vertices(transacao, contorno));

        return cantos;
    }

    /// <summary>O laço do hatch: o contorno em planta, fechado (o primeiro ponto repetido no fim), sem arco.</summary>
    private static void Laco(Hatch hachura, IReadOnlyList<Point3> contorno)
    {
        var pontos = new Point2dCollection();
        var arcos = new DoubleCollection();

        foreach (var p in contorno.Append(contorno[0]))
        {
            pontos.Add(new Point2d(p.X, p.Y));
            arcos.Add(0);
        }

        hachura.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, pontos, arcos);
    }
}
