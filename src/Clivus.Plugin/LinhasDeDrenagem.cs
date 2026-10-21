using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>Uma linha da drenagem no desenho: a polilinha 3D, a identidade e os vértices.</summary>
internal sealed record LinhaNoDesenho(ObjectId Id, DrainageLineIdentity Identidade, IReadOnlyList<Point3> Vertices);

/// <summary>
/// As linhas da drenagem (linha de análise, 27; canaleta, 28) e o gráfico do
/// perfil de cada uma. A linha é polilinha 3D assentada no TIN (regra 1),
/// com a identidade no XData; o gráfico é um punhado de polilinhas e textos
/// na camada <see cref="LayoutLayers.DrenagemPerfil"/>, cada um com o GUID da
/// linha no XData, refeito inteiro quando a linha muda.
/// </summary>
internal static class LinhasDeDrenagem
{
    internal const string TipoDoPerfil = "DrnPerfil";

    private static readonly RXClass ClasseDaPolilinha3d = RXObject.GetClass(typeof(Polyline3d));

    /// <summary>A identidade da linha, ou null se a entidade não é linha da drenagem.</summary>
    internal static DrainageLineIdentity? Identidade(Entity entidade) =>
        DrainageLineIdentity.FromFields(PluginXData.LoadAll(entidade, DrainageLineIdentity.XDataType, DrainageLineIdentity.XDataVersion, DrainageLineIdentity.FieldCount));

    internal static void Gravar(Transaction transacao, Entity entidade, DrainageLineIdentity identidade) =>
        PluginXData.Save(transacao, entidade, DrainageLineIdentity.XDataType, DrainageLineIdentity.XDataVersion, identidade.ToFields());

    /// <summary>Os vértices da polilinha 3D, na ordem.</summary>
    internal static IReadOnlyList<Point3> Vertices(Transaction transacao, Polyline3d polilinha)
    {
        var pontos = new List<Point3>();

        foreach (ObjectId v in polilinha)
        {
            // O vértice trocado pelo Reassentar fica apagado na lista até o
            // fim da transação; abri-lo derrubava o AutoCAD.
            if (v.IsErased) continue;

            var p = ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position;
            pontos.Add(new Point3(p.X, p.Y, p.Z));
        }

        return pontos;
    }

    /// <summary>Todas as linhas da drenagem do desenho (análise e canaleta).</summary>
    internal static List<LinhaNoDesenho> Todas(Transaction transacao, Database database)
    {
        var linhas = new List<LinhaNoDesenho>();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (id.ObjectClass != ClasseDaPolilinha3d) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Polyline3d p || Identidade(p) is not { } identidade) continue;

            linhas.Add(new LinhaNoDesenho(id, identidade, Vertices(transacao, p)));
        }

        return linhas;
    }

    /// <summary>
    /// Cria a polilinha 3D assentada no terreno pelo caminho em planta
    /// <paramref name="planta"/> (um vértice em cada aresta cruzada). Quantos
    /// pontos ficaram fora do terreno: a linha é pintada de vermelho.
    /// </summary>
    internal static (ObjectId Id, int Fora) Criar(Transaction transacao, Database database, Tin terreno, IReadOnlyList<Point3> planta, DrainageLineIdentity identidade)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.GarantirDrenagem(transacao, database, identidade.IsChannel ? LayoutLayers.DrenagemCanaleta : LayoutLayers.DrenagemLinha);

        var drapeada = Draping.Along(terreno, planta);

        // Ponto sem terreno embaixo ficaria com a cota do clique (zero):
        // a linha nova tem que estar inteira dentro do terreno (regra 1).
        if (drapeada.HasGaps)
            throw new ArgumentException(Tr.F("A linha sai do terreno em {0} ponto(s); desenhe-a dentro do terreno.", drapeada.OutsideCount));

        var polilinha = new Polyline3d { Layer = camada };

        espaco.AppendEntity(polilinha);
        transacao.AddNewlyCreatedDBObject(polilinha, true);

        foreach (var p in drapeada.Vertices)
        {
            var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            polilinha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
        }

        if (drapeada.HasGaps) polilinha.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);

        Gravar(transacao, polilinha, identidade);

        return (polilinha.ObjectId, drapeada.OutsideCount);
    }

    /// <summary>
    /// Assenta de novo no terreno a linha mexida (grip, move, propriedades): o
    /// caminho em planta fica, a cota volta a ser a do TIN. Quantos pontos
    /// ficaram fora do terreno; −1 se não precisou mexer.
    /// </summary>
    internal static int Reassentar(Transaction transacao, Polyline3d polilinha, Tin terreno)
    {
        var ids = polilinha.Cast<ObjectId>().Where(v => !v.IsErased).ToList();
        var atuais = Vertices(transacao, polilinha);
        if (atuais.Count < 2) return -1;

        var drapeada = Draping.Along(terreno, atuais);

        var iguais = drapeada.Vertices.Count == atuais.Count
            && drapeada.Vertices.Zip(atuais).All(z => Math.Abs(z.First.X - z.Second.X) < 1e-6 && Math.Abs(z.First.Y - z.Second.Y) < 1e-6 && Math.Abs(z.First.Z - z.Second.Z) < 1e-4);
        if (iguais) return -1;

        if (!polilinha.IsWriteEnabled) polilinha.UpgradeOpen();
        foreach (var v in ids) transacao.GetObject(v, OpenMode.ForWrite).Erase();

        foreach (var p in drapeada.Vertices)
        {
            var v = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
            polilinha.AppendVertex(v);
            transacao.AddNewlyCreatedDBObject(v, true);
        }

        polilinha.Color = drapeada.HasGaps ? Color.FromColorIndex(ColorMethod.ByAci, 1) : Color.FromColorIndex(ColorMethod.ByLayer, 256);
        return drapeada.OutsideCount;
    }

    /// <summary>
    /// O perfil da linha (terreno; na canaleta, também o fundo) com as
    /// configurações do desenho.
    /// </summary>
    internal static (TerrainProfile Terreno, ChannelProfileResult? Canaleta) Perfil(Tin terreno, DrainageSettings configuracao, LinhaNoDesenho linha)
    {
        // A linha de análise é lida no sentido em que foi desenhada (A → B,
        // 32.1); a canaleta, da ponta mais alta (a montante) para a mais baixa.
        var perfil = ProfileSampler.Sample(terreno, linha.Vertices, configuracao.Profile.StationSpacing, keepDirection: !linha.Identidade.IsChannel);

        if (linha.Identidade.Section is not { } secao) return (perfil, null);

        var canaleta = ChannelProfile.Build(perfil, secao.UsefulHeight, linha.Identidade.Mode, configuracao.Hydrology.MinimumSlope, configuracao.Profile.SegmentLength);
        return (perfil, canaleta);
    }

    /// <summary>
    /// Desenha o gráfico da linha no lugar gravado (ou ao lado da linha,
    /// gravando o lugar). O anterior já foi apagado por quem chama
    /// (<see cref="Refazer"/>). Devolve a frase do relato.
    /// </summary>
    internal static string DesenharPerfil(Transaction transacao, Database database, Tin terreno, DrainageSettings configuracao, ObjectId linhaId, (double X, double Y)? onde = null)
    {
        var polilinha = (Polyline3d)transacao.GetObject(linhaId, OpenMode.ForRead);
        var identidade = Identidade(polilinha) ?? throw new InvalidOperationException(Tr.T("A entidade não é uma linha da drenagem."));
        var linha = new LinhaNoDesenho(linhaId, identidade, Vertices(transacao, polilinha));

        var (grafico, perfil, canaleta) = Grafico(terreno, configuracao, linha);

        // Onde: o pedido, o gravado, ou ao lado da linha (à direita, alinhado por baixo).
        var origem = onde ?? identidade.Chart ?? AoLado(linha.Vertices, grafico);
        if (identidade.Chart != origem)
        {
            if (!polilinha.IsWriteEnabled) polilinha.UpgradeOpen();
            Gravar(transacao, polilinha, identidade with { Chart = origem });
        }

        if (!identidade.ChartHidden) Desenhar(transacao, database, grafico, identidade.Id, origem, CotaDoGrafico(terreno, origem));
        RastreioDoPerfil.Versao++;
        Rotulos(transacao, database, terreno, linha, perfil);

        var media = canaleta?.AverageSlope ?? (perfil.Points[0].Z - perfil.Points[^1].Z) / perfil.Length;
        var fora = perfil.OutsideCount > 0 ? Tr.F(" ATENÇÃO: {0} ponto(s) fora do terreno, linha em vermelho.", perfil.OutsideCount) : string.Empty;

        return Tr.F("{0}: {1:0.0} m, de {2:0.00} a {3:0.00} m, declividade média {4:0.0}%.{5}",
            identidade.Name, perfil.Length, perfil.Points[0].Z, perfil.Points[^1].Z, media * 100, fora);
    }

    /// <summary>A cota em que o gráfico é desenhado: a do terreno no canto dele, ou a mínima.</summary>
    internal static double CotaDoGrafico(Tin terreno, (double X, double Y) origem) =>
        terreno.TryGetZ(origem.X, origem.Y, out var z) ? z : terreno.MinZ;

    /// <summary>O gráfico da linha (sem desenhar), o perfil do terreno e o da canaleta.</summary>
    internal static (ChartDrawing Grafico, TerrainProfile Perfil, ChannelProfileResult? Canaleta) Grafico(Tin terreno, DrainageSettings configuracao, LinhaNoDesenho linha)
    {
        var identidade = linha.Identidade;
        var (perfil, canaleta) = Perfil(terreno, configuracao, linha);
        if (perfil.Points.Count < 2 || perfil.Length <= 0) throw new ArgumentException(Tr.F("{0}: a linha tem comprimento zero.", identidade.Name));

        var entrada = canaleta is null
            ? new ChartInput
            {
                Title = identidade.Name,
                Terrain = perfil.Points.Select(p => (p.Distance, p.Z)).ToList(),
                Segments = ProfileSegments.Split(perfil.ZAt, perfil.Length, configuracao.Profile.SegmentLength),
                VerticalExaggeration = configuracao.Profile.VerticalExaggeration,
                RunAndRise = true,
            }
            : new ChartInput
            {
                Title = Tr.F("{0} — {1}", identidade.Name, identidade.Section!.Name),
                Terrain = canaleta.Points.Select(p => (p.Distance, p.Terrain)).ToList(),
                Bottom = canaleta.Points.Select(p => (p.Distance, p.Bottom)).ToList(),
                WallHeight = identidade.Section.UsefulHeight,
                Segments = canaleta.Segments,
                Alerts = [.. canaleta.LowSegments, .. canaleta.InvertedSegments],
                Notes = ChannelBottomAdvice.Of(canaleta, identidade.Section.UsefulHeight)
                    .Select(c => new ChartNote(c.Run.From, c.Run.To, [c.What(canaleta.MinimumSlopeRule, identidade.Section.UsefulHeight), c.Fix]))
                    .ToList(),
                VerticalExaggeration = configuracao.Profile.VerticalExaggeration,
            };

        return (ProfileChart.Build(entrada), perfil, canaleta);
    }

    internal const string TipoDoRotulo = "DrnRotulo";

    /// <summary>Altura do nome em campo, em m.</summary>
    private const double AlturaDoNome = 1.0;

    /// <summary>
    /// O nome em campo (32.3): ao longo da linha, no meio dela, deitado e
    /// sempre legível, um pouco acima do terreno. Na canaleta, com a seção; na
    /// linha de análise, "A" e "B" nas pontas, os mesmos do gráfico.
    /// </summary>
    private static void Rotulos(Transaction transacao, Database database, Tin terreno, LinhaNoDesenho linha, TerrainProfile perfil)
    {
        var pontos = perfil.Points;
        if (pontos.Count < 2) return;

        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.GarantirDrenagem(transacao, database, linha.Identidade.IsChannel ? LayoutLayers.DrenagemCanaleta : LayoutLayers.DrenagemLinha);
        var guid = linha.Identidade.Id.ToString("D");

        void Texto(string conteudo, double x, double y, double rumo, double altura, TextHorizontalMode modo)
        {
            var z = (terreno.TryGetZ(x, y, out var zt) ? zt : pontos.Max(p => p.Z)) + 0.1;
            var texto = new DBText
            {
                Layer = camada,
                Height = altura,
                TextString = conteudo,
                Rotation = rumo,
                Position = new Point3d(x, y, z),
                HorizontalMode = modo,
            };

            // Texto à esquerda não tem ponto de alinhamento (eNotApplicable).
            if (modo != TextHorizontalMode.TextLeft) texto.AlignmentPoint = new Point3d(x, y, z);

            espaco.AppendEntity(texto);
            transacao.AddNewlyCreatedDBObject(texto, true);
            PluginXData.Save(transacao, texto, TipoDoRotulo, 1, guid);
            if (modo != TextHorizontalMode.TextLeft) texto.AdjustAlignment(database);
        }

        // O nome no meio, ao longo do trecho de lá, afastado para a esquerda de quem lê.
        var meio = perfil.Length / 2;
        var k = Math.Max(1, pontos.ToList().FindIndex(p => p.Distance >= meio));
        var (a, b) = (pontos[k - 1], pontos[k]);
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var n = Math.Sqrt(dx * dx + dy * dy);
        if (n > 1e-9)
        {
            var t = b.Distance > a.Distance ? (meio - a.Distance) / (b.Distance - a.Distance) : 0;
            var (mx, my) = (a.X + dx * t, a.Y + dy * t);
            var rumo = LayoutDrawer.RumoLegivel(dx, dy);
            var (nx, ny) = (-Math.Sin(rumo), Math.Cos(rumo));
            var nome = linha.Identidade.Section is { } secao ? Tr.F("{0} · {1}", linha.Identidade.Name, secao.Name) : linha.Identidade.Name;
            Texto(nome, mx + nx * 0.8 * AlturaDoNome, my + ny * 0.8 * AlturaDoNome, rumo, AlturaDoNome, TextHorizontalMode.TextCenter);
        }

        if (linha.Identidade.IsChannel) return;

        Texto("A", pontos[0].X, pontos[0].Y, 0, 1.6 * AlturaDoNome, TextHorizontalMode.TextRight);
        Texto("B", pontos[^1].X, pontos[^1].Y, 0, 1.6 * AlturaDoNome, TextHorizontalMode.TextLeft);
    }

    /// <summary>
    /// Refaz tudo o que a linha desenha: o gráfico e, na canaleta, o 3D e a
    /// pintura dos trechos ruins. A frase do gráfico e a dos alertas.
    /// </summary>
    /// <param name="indice">O que já está desenhado, montado uma vez por operação (<see cref="IndiceDaDrenagem"/>).</param>
    internal static (string Frase, string Alerta) Refazer(Transaction transacao, Database database, Tin terreno, DrainageSettings configuracao, ObjectId linhaId, IndiceDaDrenagem indice, (double X, double Y)? onde = null)
    {
        if (Identidade((Entity)transacao.GetObject(linhaId, OpenMode.ForRead)) is { } identidade)
        {
            // O gráfico arrastado (é um grupo) fica onde o usuário o deixou.
            onde ??= LugarDoGrafico(transacao, indice, identidade.Id);
            indice.Apagar(transacao, identidade.Id);
        }

        var frase = DesenharPerfil(transacao, database, terreno, configuracao, linhaId, onde);
        var alerta = Canaleta3D.Desenhar(transacao, database, terreno, configuracao, linhaId, indice);
        return (frase, alerta);
    }

    /// <summary>
    /// Onde o gráfico está agora: a âncora dele (um ponto no canto de baixo à
    /// esquerda), que anda junto quando o grupo é arrastado. Null sem gráfico.
    /// </summary>
    internal static (double X, double Y)? LugarDoGrafico(Transaction transacao, IndiceDaDrenagem indice, Guid linha)
    {
        foreach (var id in indice.Ids(linha))
        {
            if (id.IsErased || id.ObjectClass != RXObject.GetClass(typeof(DBPoint))) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is DBPoint ancora && PluginXData.Load(ancora, TipoDoPerfil, 1, 1) is not null)
                return (ancora.Position.X, ancora.Position.Y);
        }

        return null;
    }

    private static (double X, double Y) AoLado(IReadOnlyList<Point3> vertices, ChartDrawing grafico)
    {
        var maxX = vertices.Max(p => p.X);
        var minY = vertices.Min(p => p.Y);
        var folga = Math.Max(10, 0.1 * grafico.Width);
        return (maxX + 2 * folga, minY + folga);
    }

    /// <summary>As cores de cada traço do gráfico.</summary>
    private static Color Cor(ChartLineKind tipo) => tipo switch
    {
        ChartLineKind.Grid => Color.FromRgb(110, 110, 110),
        ChartLineKind.Terrain => Color.FromRgb(0, 170, 0),
        ChartLineKind.Bottom => Color.FromRgb(0, 110, 255),
        ChartLineKind.WallTop => Color.FromRgb(120, 180, 255),
        ChartLineKind.Alert => Color.FromRgb(255, 40, 40),
        ChartLineKind.Arrow => Color.FromRgb(0, 200, 255),
        _ => Color.FromColorIndex(ColorMethod.ByLayer, 256),
    };

    private static void Desenhar(Transaction transacao, Database database, ChartDrawing grafico, Guid linha, (double X, double Y) origem, double z)
    {
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var camada = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemPerfil);
        var guid = linha.ToString("D");
        var ids = new ObjectIdCollection();

        void Marcar(Entity e)
        {
            ids.Add(espaco.AppendEntity(e));
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, TipoDoPerfil, 1, guid);
        }

        // A âncora: guarda onde o gráfico está depois de arrastado.
        Marcar(new DBPoint(new Point3d(origem.X, origem.Y, z)) { Layer = camada });

        foreach (var traco in grafico.Lines)
        {
            var polilinha = new Polyline { Layer = camada, Elevation = z, Color = Cor(traco.Kind) };
            for (var i = 0; i < traco.Points.Count; i++)
                polilinha.AddVertexAt(i, new Point2d(origem.X + traco.Points[i].X, origem.Y + traco.Points[i].Y), 0, 0, 0);

            // O traço de alerta vem grosso, por cima da série.
            if (traco.Kind == ChartLineKind.Alert) polilinha.ConstantWidth = 0.15 * Math.Max(0.3, grafico.Width / 90);

            Marcar(polilinha);
        }

        foreach (var t in grafico.Texts)
        {
            var ponto = new Point3d(origem.X + t.X, origem.Y + t.Y, z);
            var texto = new DBText { Layer = camada, Height = t.Height, TextString = t.Text, Position = ponto };

            if (t.Align != ChartTextAlign.Left)
            {
                texto.HorizontalMode = t.Align == ChartTextAlign.Center ? TextHorizontalMode.TextCenter : TextHorizontalMode.TextRight;
                texto.AlignmentPoint = ponto;
            }

            Marcar(texto);
            if (t.Align != ChartTextAlign.Left) texto.AdjustAlignment(database);
        }

        // Um grupo sem nome: um clique pega o gráfico inteiro para arrastar (pedido de 21/10/2026).
        var grupos = (DBDictionary)transacao.GetObject(database.GroupDictionaryId, OpenMode.ForWrite);
        var grupo = new Group(Tr.F("Clivus Solar: gráfico de {0}", guid), true);
        grupos.SetAt("*", grupo);
        transacao.AddNewlyCreatedDBObject(grupo, true);
        grupo.Append(ids);
    }
}
