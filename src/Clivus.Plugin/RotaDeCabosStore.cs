using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O que o roteamento grava no desenho (etapas 17 a 24): as configurações de
/// cada aba, os lados forçados das strings, os lances gerados (para a
/// recontagem achar o que foi apagado à mão), os módulos lidos de PAN e as
/// peças pintadas de aviso. E a leitura das valas e dos lances do desenho.
/// </summary>
internal static class RotaDeCabosStore
{
    private const string ChaveConfig = "ROTA_CONFIG";
    private const string ChaveLados = "ROTA_LADOS";
    private const string ChaveLances = "ROTA_LANCES";
    private const string ChavePan = "ROTA_MODULOS_PAN";
    private const string ChavePintadas = "ROTA_PINTADAS";
    private const string ChaveVias = "ROTA_VIAS";

    private static readonly string OQueConfig = Tr.N("das configurações da rota de cabos");
    private static readonly string OQueLados = Tr.N("dos lados forçados das strings");
    private static readonly string OQueLances = Tr.N("dos lances gerados");
    private static readonly string OQuePan = Tr.N("dos módulos lidos de PAN");
    private static readonly string OQueVias = Tr.N("das vias trocadas no resumo");

    // Formato 2 da configuração (10/10/2026): as vias no fim. O 1 é lido com as vias da formação do cabo.
    private const int VersaoDaConfig = 2;

    // ------------------------------------------------------------- registros

    /// <summary>A configuração de cada aba (a de partida para a que nunca foi gravada).</summary>
    internal static Dictionary<CableRoute, RouteSettings> Configuracoes(Database db, out string? problema)
    {
        var lido = PluginRecords.Version(db, ChaveConfig) == 1
            ? PluginRecords.Load(db, ChaveConfig, 1, RouteSettings.FieldCountV1, RouteSettings.ParseV1, OQueConfig)
            : PluginRecords.Load(db, ChaveConfig, VersaoDaConfig, RouteSettings.FieldCount, RouteSettings.Parse, OQueConfig);
        problema = lido.Problem;
        var mapa = CableRoutes.All.ToDictionary(r => r, RouteSettings.Default);
        foreach (var c in lido.Items) mapa[c.Route] = c;
        return mapa;
    }

    internal static RouteSettings Configuracao(Database db, CableRoute rota) => Configuracoes(db, out _)[rota];

    /// <summary>Grava a configuração de uma aba sem mexer nas outras. Recusa (com o motivo) se o registro estava ilegível.</summary>
    internal static string? GravarConfiguracao(Database db, RouteSettings nova)
    {
        var todas = Configuracoes(db, out var problema);
        if (problema is not null) return problema;

        todas[nova.Route] = nova;
        PluginRecords.Save(db, ChaveConfig, VersaoDaConfig, RouteSettings.FieldCount, CableRoutes.All.Select(r => todas[r]).ToList(), c => c.ToFields());
        return null;
    }

    /// <summary>As vias trocadas à mão no resumo, por circuito.</summary>
    internal static List<CircuitWires> Vias(Database db, out string? problema)
    {
        var lido = PluginRecords.Load(db, ChaveVias, 1, CircuitWires.FieldCount, CircuitWires.Parse, OQueVias);
        problema = lido.Problem;
        return [.. lido.Items];
    }

    /// <summary>
    /// Troca as vias de um circuito (null = volta às da aba). Recusa, com o
    /// motivo, se o registro estava ilegível (gravar por cima perderia o resto).
    /// </summary>
    internal static string? GravarVias(Database db, CableRoute rota, CableEnd de, CableEnd para, int? vias)
    {
        var lista = Vias(db, out var problema);
        if (problema is not null) return problema;

        lista.RemoveAll(v => v.Route == rota && v.From == de && v.To == para);
        if (vias is { } n) lista.Add(new CircuitWires(rota, de, para, n));
        PluginRecords.Save(db, ChaveVias, 1, CircuitWires.FieldCount, lista, v => v.ToFields());
        return null;
    }

    internal static List<StringSide> Lados(Database db) =>
        [.. PluginRecords.Load(db, ChaveLados, 1, StringSide.FieldCount, StringSide.Parse, OQueLados).Items];

    internal static void GravarLados(Database db, IReadOnlyList<StringSide> lados) =>
        PluginRecords.Save(db, ChaveLados, 1, StringSide.FieldCount, lados, l => l.ToFields());

    internal static List<CableRun> LancesGerados(Database db) => LancesGerados(db, out _);

    internal static List<CableRun> LancesGerados(Database db, out string? problema)
    {
        var lido = PluginRecords.Load(db, ChaveLances, 1, CableRun.FieldCount, CableRun.Parse, OQueLances);
        problema = lido.Problem;
        return [.. lido.Items];
    }

    internal static void GravarLancesGerados(Database db, IReadOnlyList<CableRun> lances) =>
        PluginRecords.Save(db, ChaveLances, 1, CableRun.FieldCount, lances, l => l.ToFields());

    internal static List<PanModule> ModulosPan(Database db) =>
        [.. PluginRecords.Load(db, ChavePan, 1, PanModule.FieldCount, PanModule.Parse, OQuePan).Items];

    internal static void GravarModulosPan(Database db, IReadOnlyList<PanModule> modulos) =>
        PluginRecords.Save(db, ChavePan, 1, PanModule.FieldCount, modulos, m => m.ToFields());

    // ------------------------------------------------------------- valas

    /// <summary>Marca as entidades como vala da rota (XData) e põe na camada da vala dela (17.4). Quantas. Quem assenta no terreno é <see cref="AssentarValas"/>.</summary>
    internal static int MarcarValas(Database db, IReadOnlyList<ObjectId> ids, CableRoute rota)
    {
        using var transacao = db.TransactionManager.StartTransaction();
        var camada = LayoutLayers.GarantirComCor(transacao, db, CableLayers.Trench(rota), CableLayers.TrenchColor(rota));
        var n = 0;

        foreach (var id in ids)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForWrite) is not Curve curva) continue;
            curva.Layer = camada;
            PluginXData.Save(transacao, curva, TrenchMark.Tipo, 1, [.. new TrenchMark(rota).ToFields()]);
            n++;
        }

        transacao.Commit();
        return n;
    }

    /// <summary>
    /// Assenta as valas da rota no terreno, na profundidade da aba: cada uma
    /// vira uma Polyline3d com o traçado em planta de antes e a cota do
    /// terreno menos a profundidade, com vértice onde cruza aresta do TIN
    /// (Renan, 10/10/2026: "TODO desenho respeita o TIN"). A nova fica com a
    /// camada, a cor e o XData da antiga, que é apagada; a que já está assim
    /// fica. Só as de <paramref name="quais"/> (null = todas da rota).
    /// Devolve quantas foram assentadas e quantos pontos ficaram fora do
    /// terreno (com a cota que tinham, para quem chamou avisar).
    /// </summary>
    internal static (int Valas, int PontosFora) AssentarValas(Database db, CableRoute rota, double profundidade, Tin terreno, IReadOnlyCollection<ObjectId>? quais = null)
    {
        using var transacao = db.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        var classeCurva = RXObject.GetClass(typeof(Curve));
        var alvo = quais?.ToHashSet();
        var ids = alvo is not null ? alvo.ToList() : espaco.Cast<ObjectId>().Where(id => id.ObjectClass.IsDerivedFrom(classeCurva)).ToList();
        int valas = 0, fora = 0;

        foreach (var id in ids)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Curve curva || Vala(curva) is not { } marca || marca.Route != rota) continue;

            var planta = Pontos(curva, transacao);
            if (planta.Count < 2) continue;

            var vala = Draping.Below(terreno, planta, profundidade);
            fora += vala.OutsideCount;

            valas++;

            // Já assentada assim (o Gerar de novo, sem mudar a profundidade): fica como está.
            if (curva is Polyline3d && Iguais(Pontos3d(curva, transacao), vala.Vertices)) continue;

            // Sempre uma linha nova no lugar da antiga, com as propriedades e o
            // XData dela (de todos os aplicativos): trocar os vértices no lugar
            // deixa os apagados na lista da Polyline3d, e quem a percorre depois
            // tropeça neles (eWasErased).
            var nova = new Polyline3d { Closed = false };
            nova.SetPropertiesFrom(curva);
            espaco.AppendEntity(nova);
            transacao.AddNewlyCreatedDBObject(nova, true);
            Vertices(transacao, nova, vala.Vertices);
            using (var xdata = curva.XData) nova.XData = xdata;

            curva.UpgradeOpen();
            curva.Erase();
        }

        transacao.Commit();
        return (valas, fora);
    }

    /// <summary>Os vértices de uma Polyline3d com a cota (os apagados ficam de fora).</summary>
    private static List<Point3> Pontos3d(Curve curva, Transaction transacao)
    {
        var pontos = new List<Point3>();
        foreach (ObjectId v in (Polyline3d)curva)
        {
            if (v.IsErased) continue;
            var p = ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position;
            pontos.Add(new Point3(p.X, p.Y, p.Z));
        }

        return pontos;
    }

    private static bool Iguais(IReadOnlyList<Point3> a, IReadOnlyList<Point3> b) =>
        a.Count == b.Count && a.Zip(b).All(x => Math.Abs(x.First.X - x.Second.X) < 1e-6 && Math.Abs(x.First.Y - x.Second.Y) < 1e-6 && Math.Abs(x.First.Z - x.Second.Z) < 1e-6);

    private static void Vertices(Transaction transacao, Polyline3d linha, IEnumerable<Point3> pontos)
    {
        foreach (var p in pontos)
        {
            var vertice = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            linha.AppendVertex(vertice);
            transacao.AddNewlyCreatedDBObject(vertice, true);
        }
    }

    /// <summary>A marca de vala da entidade (null se não é vala).</summary>
    internal static TrenchMark? Vala(Entity e) =>
        PluginXData.Load(e, TrenchMark.Tipo, 1, TrenchMark.FieldCount) is { } c ? TrenchMark.Parse(c) : null;

    /// <summary>As valas de cada rota no espaço do modelo, como pontos de planta (arcos viram trechos de até 1 m).</summary>
    internal static Dictionary<CableRoute, List<IReadOnlyList<Point3>>> Valas(Database db)
    {
        var valas = CableRoutes.All.ToDictionary(r => r, _ => new List<IReadOnlyList<Point3>>());
        using var transacao = db.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var classeCurva = RXObject.GetClass(typeof(Curve));

        foreach (var id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classeCurva)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve curva || Vala(curva) is not { } marca) continue;
            var pontos = Pontos(curva, transacao);
            if (pontos.Count >= 2) valas[marca.Route].Add(pontos);
        }

        return valas;
    }

    /// <summary>Quantas valas cada rota tem.</summary>
    internal static Dictionary<CableRoute, int> QuantasValas(Database db) => Valas(db).ToDictionary(v => v.Key, v => v.Value.Count);

    /// <summary>Os pontos de planta de uma curva: vértices, e arcos divididos em trechos de até 1 m.</summary>
    private static List<Point3> Pontos(Curve curva, Transaction transacao)
    {
        var pontos = new List<Point3>();
        void Somar(Point3d p) => pontos.Add(new Point3(p.X, p.Y, 0));

        switch (curva)
        {
            case Polyline pl:
                for (var i = 0; i < pl.NumberOfVertices; i++)
                {
                    Somar(pl.GetPoint3dAt(i));
                    var ultimo = i == pl.NumberOfVertices - 1;
                    if (ultimo && !pl.Closed) break;
                    if (pl.GetBulgeAt(i) == 0) continue;

                    var ate = ultimo ? pl.EndParam : i + 1;
                    var comprimento = pl.GetDistanceAtParameter(ate) - pl.GetDistanceAtParameter(i);
                    var partes = Math.Max(2, (int)Math.Ceiling(comprimento));
                    for (var k = 1; k < partes; k++) Somar(pl.GetPointAtParameter(i + (ate - i) * k / partes));
                }

                if (pl.Closed && pl.NumberOfVertices > 0) Somar(pl.GetPoint3dAt(0));
                break;

            case Polyline3d p3:
                foreach (ObjectId v in p3)
                    if (!v.IsErased) Somar(((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position);
                if (p3.Closed && pontos.Count > 0) pontos.Add(pontos[0]);
                break;

            case Polyline2d p2:
                // Arco (bulge), spline e OCS: a própria curva dá os pontos em WCS, um a cada metro.
                var total = p2.GetDistanceAtParameter(p2.EndParam);
                var passos = Math.Max(1, (int)Math.Ceiling(total));
                for (var k = 0; k <= passos; k++) Somar(p2.GetPointAtDist(total * k / passos));
                break;

            case Line l:
                Somar(l.StartPoint);
                Somar(l.EndPoint);
                break;
        }

        return pontos;
    }

    // ------------------------------------------------------------- lances

    /// <summary>Um lance no desenho: a entidade, o lance e o comprimento medido na geometria (regra 7).</summary>
    internal sealed record LanceNoDesenho(ObjectId Id, CableRun Lance, double Comprimento);

    internal static CableRun? Lance(Entity e) =>
        PluginXData.Load(e, CableRun.Tipo, 1, CableRun.FieldCount) is { } c ? CableRun.Parse(c) : null;

    /// <summary>Todos os lances do espaço do modelo, medidos agora (nunca de memória).</summary>
    internal static List<LanceNoDesenho> Lances(Database db)
    {
        var lances = new List<LanceNoDesenho>();
        using var transacao = db.TransactionManager.StartOpenCloseTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var classe = RXObject.GetClass(typeof(Polyline3d));

        foreach (var id in espaco)
        {
            if (!id.ObjectClass.IsDerivedFrom(classe)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d linha || Lance(linha) is not { } lance) continue;

            var pontos = new List<Point3>();
            foreach (ObjectId v in linha)
            {
                var p = ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position;
                pontos.Add(new Point3(p.X, p.Y, p.Z));
            }

            lances.Add(new LanceNoDesenho(id, lance, CablePath.Length(pontos)));
        }

        return lances;
    }

    /// <summary>Desenha os lances (Polyline3d na camada da rota e da polaridade, com o XData do lance).</summary>
    internal static void Desenhar(Database db, IReadOnlyList<PlannedRun> lances)
    {
        using var transacao = db.TransactionManager.StartTransaction();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

        foreach (var l in lances)
        {
            var camada = LayoutLayers.GarantirComCor(transacao, db, CableLayers.Cable(l.Run.Route, l.Run.Polarity), CableLayers.CableColor(l.Run.Route, l.Run.Polarity));
            var linha = new Polyline3d { Layer = camada };
            espaco.AppendEntity(linha);
            transacao.AddNewlyCreatedDBObject(linha, true);

            foreach (var p in l.Path)
            {
                var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
                linha.AppendVertex(v);
                transacao.AddNewlyCreatedDBObject(v, true);
            }

            PluginXData.Save(transacao, linha, CableRun.Tipo, 1, [.. l.Run.ToFields()]);
        }

        transacao.Commit();
    }

    /// <summary>Apaga os lances (só cabo: nunca string, vala ou layout; 17.9). Quantos.</summary>
    internal static int Apagar(Database db, IEnumerable<ObjectId> ids)
    {
        using var transacao = db.TransactionManager.StartTransaction();
        var n = 0;

        foreach (var id in ids)
        {
            if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e || Lance(e) is null) continue;
            e.UpgradeOpen();
            e.Erase();
            n++;
        }

        transacao.Commit();
        return n;
    }

    // ------------------------------------------------------------- pintura de aviso

    /// <summary>A cor do aviso (17.7): laranja (o vermelho já é o da mesa suja), por entidade (a camada não muda).</summary>
    private static Color CorDoAviso => Color.FromColorIndex(ColorMethod.ByAci, 30);

    /// <summary>
    /// Pinta de aviso (regra 4): desfaz a pintura anterior desta rota e
    /// pinta estas, guardando a cor que cada uma tinha para voltar depois.
    /// Com <paramref name="somar"/>, mantém a pintura que já havia e soma estas.
    /// </summary>
    internal static void Pintar(Database db, CableRoute rota, IReadOnlyCollection<ObjectId> ids, bool somar = false)
    {
        var chave = ChavePintadas + "_" + CableLayers.Code(rota);
        var pintadas = somar ? Pintadas(db, chave) : [];
        if (!somar) Despintar(db, rota);
        var ja = pintadas.Select(p => p.Id).ToHashSet();

        using (var transacao = db.TransactionManager.StartTransaction())
        {
            foreach (var id in ids.Distinct())
            {
                if (ja.Contains(id) || id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                pintadas.Add((id, e.Color));
                e.UpgradeOpen();
                e.Color = CorDoAviso;
            }

            transacao.Commit();
        }

        var registro = new List<TypedValue> { new((int)DxfCode.Text, "V1") };
        registro.AddRange(pintadas.Select(a => new TypedValue((int)DxfCode.Text, a.Id.Handle + "=" + PecasPintadas.Texto(a.Antes))));
        PluginDictionary.Save(db, chave, new ResultBuffer([.. registro]));
    }

    /// <summary>As peças pintadas de aviso de uma rota, com a cor de antes.</summary>
    private static List<(ObjectId Id, Color Antes)> Pintadas(Database db, string chave)
    {
        var pecas = new List<(ObjectId Id, Color Antes)>();
        using var dados = PluginDictionary.Load(db, chave);
        if (dados is null) return pecas;

        foreach (var valor in dados.AsArray())
        {
            if (valor.Value is not string texto || texto == "V1") continue;
            var partes = texto.Split('=', 2);
            if (partes.Length != 2 || !long.TryParse(partes[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var numero)) continue;
            if (!db.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;
            pecas.Add((id, PecasPintadas.CorDe(partes[1])));
        }

        return pecas;
    }

    /// <summary>Volta as peças pintadas de aviso à cor de antes (só as que ainda estão com a cor do aviso).</summary>
    internal static void Despintar(Database db, CableRoute rota)
    {
        var chave = ChavePintadas + "_" + CableLayers.Code(rota);
        var pecas = Pintadas(db, chave);
        if (pecas.Count == 0) return;

        // Peça que outra rota também pintou continua com o aviso dela.
        var dasOutras = CableRoutes.All.Where(r => r != rota)
            .SelectMany(r => Pintadas(db, ChavePintadas + "_" + CableLayers.Code(r))).Select(p => p.Id).ToHashSet();
        pecas.RemoveAll(p => dasOutras.Contains(p.Id));

        using (var transacao = db.TransactionManager.StartTransaction())
        {
            foreach (var (id, antes) in pecas)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e || e.Color != CorDoAviso) continue;
                e.UpgradeOpen();
                e.Color = antes;
            }

            transacao.Commit();
        }

        PluginDictionary.Save(db, chave, new ResultBuffer(new TypedValue((int)DxfCode.Text, "V1")));
    }
}
