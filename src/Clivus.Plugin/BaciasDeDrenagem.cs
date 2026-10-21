using System.Diagnostics;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// As bacias de contribuição (passo 30): calcular pelas canaletas do desenho,
/// desenhar o hatch de cada bacia com a borda assentada no terreno e o
/// rótulo, a área sem destino em vermelho (regra 4), e gravar o resultado
/// para a memória. Ajuste manual: uma polilinha fechada marcada com a
/// canaleta para onde a área dentro dela vai.
/// </summary>
internal static class BaciasDeDrenagem
{
    private const string TipoBacia = "DrnBacia";
    internal const string TipoManual = "DrnBaciaManual";

    private static readonly RXClass ClasseDoHatch = RXObject.GetClass(typeof(Hatch));
    private static readonly RXClass ClasseDaPolilinha3d = RXObject.GetClass(typeof(Polyline3d));
    private static readonly RXClass ClasseDoTexto = RXObject.GetClass(typeof(DBText));

    /// <summary>Os ajustes manuais do desenho: o contorno e o GUID da canaleta.</summary>
    internal static List<(IReadOnlyList<Point3> Contorno, Guid Canaleta)> Manuais(Transaction transacao, Database database)
    {
        var lista = new List<(IReadOnlyList<Point3>, Guid)>();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Curve curva) continue;
            if (PluginXData.Load(curva, TipoManual, 1, 1) is not { } campos || !Guid.TryParse(campos[0], out var guid)) continue;

            lista.Add((LinhaCommands.Caminho(database, id), guid));
        }

        return lista;
    }

    /// <summary>
    /// Calcula e desenha as bacias. Devolve a frase do relato; o resultado
    /// fica gravado no desenho (<see cref="DrenagemStore.LerBacias"/>).
    /// </summary>
    internal static string Calcular(Database database, Tin terreno, IReadOnlyList<IReadOnlyList<Point3>> regiao, Func<double, bool>? andamento = null)
    {
        var relogio = Stopwatch.StartNew();
        var configuracao = DrenagemStore.Ler(database);
        var celula = configuracao.Basins.CellSize;

        using var transacao = database.TransactionManager.StartTransaction();

        var canaletas = LinhasDeDrenagem.Todas(transacao, database).Where(l => l.Identidade.IsChannel).ToList();
        if (canaletas.Count == 0)
            throw new ArgumentException(Tr.T("Não há canaleta no desenho. Faça as canaletas antes (Desenho › Canaleta)."));

        var alvos = new FlowTargets(canaletas.Select(c => c.Vertices).ToList());
        // Duas canaletas com o mesmo GUID (cópia feita sem o vigia): a primeira fica com o ajuste manual.
        var indice = canaletas.Select((c, i) => (c.Identidade.Id, i)).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().i);

        var semCanaleta = 0;
        var manuais = new List<(IReadOnlyList<Point3>, int)>();
        foreach (var (contorno, guid) in Manuais(transacao, database))
        {
            if (indice.TryGetValue(guid, out var i) && contorno.Count >= 3) manuais.Add((contorno, i));
            else semCanaleta++;
        }

        WatershedGrid grade;
        try
        {
            grade = Watershed.Compute(terreno, regiao, alvos, celula, manuais, progress: andamento);
        }
        catch (ArgumentException erro) when (erro is not ArgumentOutOfRangeException)
        {
            throw new ArgumentException(Tr.F("A região é grande demais para células de {0:0.##} m ({1}). Aumente a célula.", celula, erro.Message));
        }

        var resumos = Watershed.Summaries(grade);
        var manchas = BasinMap.Shapes(grade);

        ApagarNa(transacao, database);
        Desenhar(transacao, database, terreno, grade, canaletas, resumos, manchas);

        transacao.Commit();

        var area = grade.Cell * grade.Cell;
        var semDestino = grade.Owner.Count(o => o == Watershed.NoDestination) * area;
        var relatorio = new BasinReport
        {
            Basins = resumos.Select(r => new BasinEntry(canaletas[r.Target].Identidade.Id, canaletas[r.Target].Identidade.Name, r.Area, r.LongestPath, r.Drop, r.HasManual)
            {
                Accumulation = Acumulado(terreno, configuracao, canaletas[r.Target], grade, r.Target),
            }).ToList(),
            NoDestinationArea = semDestino,
            AnalyzedArea = grade.AnalyzedCount * area,
            CellSize = celula,
            When = DateTime.Now,
        };
        DrenagemStore.GravarBacias(database, relatorio);

        var linhas = relatorio.Basins.Select(b => Tr.F("{0}: {1:N0} m², L {2:0.0} m, ΔH {3:0.00} m, S {4:0.00}%{5}",
            b.ChannelName, b.Area, b.Length, b.Drop, b.SlopePercent, b.Manual ? Tr.T(" (com ajuste manual)") : string.Empty));

        var frase = Tr.F("BACIAS {0} bacia(s) em {1:N0} m² analisados (células de {2:0.##} m, {3:0.0} s): {4}.",
            relatorio.Basins.Count, relatorio.AnalyzedArea, celula, relogio.Elapsed.TotalSeconds, string.Join("; ", linhas));

        var semBacia = canaletas.Where(c => relatorio.Of(c.Identidade.Id) is null).Select(c => c.Identidade.Name).ToList();
        if (semBacia.Count > 0) frase += " " + Tr.F("Sem água chegando: {0}.", string.Join(", ", semBacia));

        if (semDestino > 0)
        {
            var porque = manchas.Where(m => m.Owner == Watershed.NoDestination).Select(m => Tr.F("{0:N0} m² {1}", m.Area, BasinMap.Why(m.Reason)));
            frase += " " + Tr.F("ATENÇÃO: {0:N0} m² sem destino ({1}), em vermelho na camada {2}.", semDestino, string.Join("; ", porque), LayoutLayers.DrenagemAlerta);
        }

        if (semCanaleta > 0) frase += " " + Tr.F("ATENÇÃO: {0} ajuste(s) manual(is) apontam para canaleta que não existe mais e foram ignorados.", semCanaleta);

        return frase;
    }

    /// <summary>
    /// A área que entra na canaleta até cada estaca (32.8): a estaca de entrada
    /// de cada célula dela é a projeção do ponto onde a água chega no eixo, na
    /// ordem do perfil (da montante).
    /// </summary>
    private static IReadOnlyList<double[]> Acumulado(Tin terreno, DrainageSettings configuracao, LinhaNoDesenho canaleta, WatershedGrid grade, int alvo)
    {
        ChannelProfileResult? perfil;
        try
        {
            perfil = LinhasDeDrenagem.Perfil(terreno, configuracao, canaleta).Canaleta;
        }
        catch (ArgumentException)
        {
            return [];
        }

        if (perfil is not { Points.Count: >= 2 } p) return [];

        double Estaca(double x, double y)
        {
            if (!double.IsFinite(x)) return double.NaN;

            double melhor = double.MaxValue, estaca = double.NaN;
            for (var i = 1; i < p.Points.Count; i++)
            {
                var (a, b) = (p.Points[i - 1], p.Points[i]);
                var (dx, dy) = (b.X - a.X, b.Y - a.Y);
                var l2 = dx * dx + dy * dy;
                var t = l2 < 1e-18 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
                var e = Math.Pow(x - (a.X + dx * t), 2) + Math.Pow(y - (a.Y + dy * t), 2);
                if (e < melhor) (melhor, estaca) = (e, a.Distance + (b.Distance - a.Distance) * t);
            }

            return estaca;
        }

        var estacas = Enumerable.Range(0, grade.Owner.Length)
            .Where(k => grade.Owner[k] == alvo)
            .Select(k => grade.HitX.Length > k ? Estaca(grade.HitX[k], grade.HitY[k]) : double.NaN);

        return BasinEntry.Accumulate(estacas, grade.Cell * grade.Cell, p.Length);
    }

    private static void Desenhar(Transaction transacao, Database database, Tin terreno, WatershedGrid grade,
        List<LinhaNoDesenho> canaletas, IReadOnlyList<BasinSummary> resumos, IReadOnlyList<BasinShape> manchas)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camadaBacia = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemBacia);
        var camadaAlerta = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemAlerta);
        var paleta = InverterColors.Palette;
        var alturaDoTexto = Math.Max(1.5, 1.5 * grade.Cell);
        var hatches = new ObjectIdCollection();

        void Marcar(Entity e, string dono)
        {
            espaco.AppendEntity(e);
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, TipoBacia, 1, dono);
        }

        foreach (var mancha in manchas)
        {
            var semDestino = mancha.Owner == Watershed.NoDestination;
            var canaleta = semDestino ? null : canaletas[mancha.Owner];
            var rgb = semDestino ? new RgbColor(255, 40, 40) : paleta[mancha.Owner % paleta.Count].Color;
            var cor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
            var camada = semDestino ? camadaAlerta : camadaBacia;
            var dono = canaleta?.Identidade.Id.ToString("D") ?? "-";

            // O hatch em planta, na cota mais alta da bacia: nunca enterrado.
            var hachura = new Hatch();
            hachura.SetDatabaseDefaults(database);
            hachura.Layer = camada;
            hachura.Color = cor;
            hachura.Normal = Vector3d.ZAxis;
            // A mais alta entre o centro das células e os cantos da borda: o
            // canto pode estar mais alto que todo centro (terreno subindo).
            var topo = mancha.MaxZ;
            foreach (var (x, y) in mancha.Rings.SelectMany(a => a))
                if (terreno.TryGetZ(x, y, out var zc)) topo = Math.Max(topo, zc);
            hachura.Elevation = topo + 0.05;
            Marcar(hachura, dono);

            hachura.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            hachura.Associative = false;
            hachura.HatchStyle = HatchStyle.Normal;

            foreach (var anel in mancha.Rings)
            {
                var pontos = new Point2dCollection();
                var arcos = new DoubleCollection();
                foreach (var (x, y) in anel.Append(anel[0]))
                {
                    pontos.Add(new Point2d(x, y));
                    arcos.Add(0);
                }

                hachura.AppendLoop(HatchLoopTypes.Polyline, pontos, arcos);
            }

            hachura.EvaluateHatch(true);
            hachura.Transparency = new Transparency(150);
            hatches.Add(hachura.ObjectId);

            // A borda, assentada no terreno (regra 1): o desenho da bacia em 3D.
            foreach (var anel in mancha.Rings)
            {
                var borda = new Polyline3d { Layer = camada, Color = cor, Closed = true };
                Marcar(borda, dono);

                foreach (var (x, y) in ShadowUnion.Densify(anel, Math.Max(1, grade.Cell)))
                {
                    var z = terreno.TryGetZ(x, y, out var zt) ? zt : mancha.MaxZ;
                    var v = new PolylineVertex3d(new Point3d(x, y, z + 0.05));
                    borda.AppendVertex(v);
                    transacao.AddNewlyCreatedDBObject(v, true);
                }
            }

            // O rótulo, deitado e na horizontal, sobre o terreno.
            var resumo = semDestino ? null : resumos.FirstOrDefault(r => r.Target == mancha.Owner);
            var texto = semDestino
                ? Tr.F("Sem destino: {0} · {1:N0} m²", BasinMap.Why(mancha.Reason), mancha.Area)
                : Tr.F("Bacia de {0} · {1:N0} m² · L {2:0.0} m · ΔH {3:0.00} m", canaleta!.Identidade.Name, mancha.Area, resumo?.LongestPath ?? 0, resumo?.Drop ?? 0);

            var (lx, ly) = mancha.LabelAt;
            var rotulo = new DBText
            {
                Layer = camada,
                Color = cor,
                Height = alturaDoTexto,
                TextString = texto,
                // Acima do hatch (que fica na cota mais alta da bacia): em 3D o rótulo aparece.
                Position = new Point3d(lx, ly, hachura.Elevation + 0.05),
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = new Point3d(lx, ly, hachura.Elevation + 0.05),
            };
            Marcar(rotulo, dono);
            rotulo.AdjustAlignment(database);
        }

        // As bacias atrás de tudo (as mesas e as canaletas por cima).
        if (hatches.Count > 0)
        {
            var ordem = (DrawOrderTable)transacao.GetObject(espaco.DrawOrderTableId, OpenMode.ForWrite);
            ordem.MoveToBottom(hatches);
        }
    }

    /// <summary>Apaga o desenho das bacias (hatch, borda e rótulo). Os ajustes manuais ficam.</summary>
    internal static int Apagar(Database database)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var apagadas = ApagarNa(transacao, database);
        transacao.Commit();
        return apagadas;
    }

    private static int ApagarNa(Transaction transacao, Database database)
    {
        var apagadas = 0;
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDoHatch && id.ObjectClass != ClasseDaPolilinha3d && id.ObjectClass != ClasseDoTexto) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (e.Layer != LayoutLayers.DrenagemBacia && e.Layer != LayoutLayers.DrenagemAlerta) continue;
            if (PluginXData.Load(e, TipoBacia, 1, 1) is null) continue;

            e.UpgradeOpen();
            e.Erase();
            apagadas++;
        }

        return apagadas;
    }
}
