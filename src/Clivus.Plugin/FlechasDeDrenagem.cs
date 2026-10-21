using System.Diagnostics;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>Onde as flechas são geradas.</summary>
internal enum OndeFlechas
{
    /// <summary>Dentro das áreas de implantação do projeto.</summary>
    Areas,

    /// <summary>Num retângulo escolhido no desenho.</summary>
    Janela,

    /// <summary>No terreno todo.</summary>
    Tudo,
}

/// <summary>
/// As flechas do sentido da água (passo 26). Todas moram num bloco só
/// (<see cref="NomeDoBloco"/>), inserido uma vez na camada
/// <see cref="LayoutLayers.DrenagemFlecha"/>: dezenas de milhares de flechas
/// soltas no modelo pesariam no desenho, na seleção e no apagar. Cada traço
/// tem a cor da faixa e as duas pontas na cota do terreno, um pouco acima
/// dele (regra 1 da drenagem: tudo acompanha o terreno).
/// </summary>
internal static class FlechasDeDrenagem
{
    internal const string NomeDoBloco = PluginInfo.PrefixoDeDados + "_DRN_FLECHAS";

    /// <summary>Quanto as flechas ficam acima do terreno, em m.</summary>
    internal const double Acima = 0.05;

    /// <summary>Mais pontos que isto e a grade é recusada: pede espaçamento maior.</summary>
    internal const int MaximoDeFlechas = 400_000;

    private const string TipoDaInsercao = "DrnFlechas";
    private const string TipoDaLegenda = "DrnLegenda";

    /// <summary>
    /// Apaga as flechas anteriores e gera de novo nas regiões dadas. Cada
    /// região é um polígono em planta; vazio = o terreno todo. O relato vai
    /// para a linha de comando e para a janela.
    /// </summary>
    internal static string Gerar(Database database, Tin terreno, ArrowSettings configuracao, IReadOnlyList<IReadOnlyList<Point3>> regioes)
    {
        var relogio = Stopwatch.StartNew();
        var faixas = DrainageArrows.Bands(configuracao.Limits);
        var espacamento = configuracao.Spacing;

        // O retângulo envolvente: das regiões, ou do terreno.
        double minX, minY, maxX, maxY;
        if (regioes.Count > 0)
        {
            var todos = regioes.SelectMany(r => r).ToList();
            (minX, minY, maxX, maxY) = (todos.Min(p => p.X), todos.Min(p => p.Y), todos.Max(p => p.X), todos.Max(p => p.Y));
        }
        else
        {
            var vertices = terreno.Triangles.SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
            if (vertices.Count == 0) throw new InvalidOperationException(Tr.T("O terreno está vazio."));
            (minX, minY, maxX, maxY) = (vertices.Min(p => p.X), vertices.Min(p => p.Y), vertices.Max(p => p.X), vertices.Max(p => p.Y));
        }

        var estimativa = (maxX - minX) / espacamento * ((maxY - minY) / espacamento);
        if (estimativa > MaximoDeFlechas * 4)
            throw new ArgumentException(Tr.F("Com {0:0.#} m de espaçamento seriam mais de {1:N0} flechas. Aumente o espaçamento.", espacamento, MaximoDeFlechas));

        bool Dentro(double x, double y) => regioes.Count == 0 || regioes.Any(r => Polygons.Contains(r, x, y));

        var pontos = DrainageArrows.GridPoints(minX, minY, maxX, maxY, espacamento, Dentro).ToList();
        if (pontos.Count > MaximoDeFlechas)
            throw new ArgumentException(Tr.F("Com {0:0.#} m de espaçamento seriam {1:N0} flechas (máximo {2:N0}). Aumente o espaçamento.", espacamento, pontos.Count, MaximoDeFlechas));

        var porFaixa = new int[faixas.Count];
        var flechas = 0;
        var planos = 0;
        var foraDoTerreno = 0;

        using var transacao = database.TransactionManager.StartTransaction();

        // Apaga as anteriores na mesma transação: se a geração falhar, nada
        // se perde (o U e o abortar devolvem as flechas de antes).
        ApagarNa(transacao, database);

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForWrite);
        var bloco = Definicao(transacao, tabela);
        var cores = faixas.Select(f => Color.FromRgb(f.Color.R, f.Color.G, f.Color.B)).ToArray();
        var cinza = Color.FromRgb(160, 160, 160);
        var alturaDoTexto = Math.Max(0.1, 0.18 * espacamento);

        void Traco(Point3 a, Point3 b, Color cor)
        {
            var linha = new Line(new Point3d(a.X, a.Y, a.Z), new Point3d(b.X, b.Y, b.Z)) { Layer = "0", Color = cor };
            bloco.AppendEntity(linha);
            transacao.AddNewlyCreatedDBObject(linha, true);
        }

        Point3? NoTerreno(double x, double y) => terreno.TryGetZ(x, y, out var z) ? new Point3(x, y, z + Acima) : null;

        foreach (var (x, y) in pontos)
        {
            if (!terreno.TryGetSlope(x, y, out var declive) || NoTerreno(x, y) is not { } centro)
            {
                foraDoTerreno++;
                continue;
            }

            if (declive.Percent < DrainageArrows.FlatPercent)
            {
                // Plano: um pontinho, não flecha (a água não tem para onde ir).
                var ponto = new Circle(new Point3d(centro.X, centro.Y, centro.Z), Vector3d.ZAxis, 0.08 * espacamento) { Layer = "0", Color = cinza };
                bloco.AppendEntity(ponto);
                transacao.AddNewlyCreatedDBObject(ponto, true);
                planos++;
                continue;
            }

            var forma = DrainageArrows.Shape(x, y, declive.DownhillX, declive.DownhillY, espacamento);

            // As pontas na cota do terreno. Flecha com ponta fora dele (borda,
            // buraco da triangulação) não é desenhada: inventar a cota seria
            // pôr traço onde não há chão.
            if (NoTerreno(forma.Tail.X, forma.Tail.Y) is not { } cauda
                || NoTerreno(forma.Tip.X, forma.Tip.Y) is not { } bico
                || NoTerreno(forma.WingLeft.X, forma.WingLeft.Y) is not { } esquerda
                || NoTerreno(forma.WingRight.X, forma.WingRight.Y) is not { } direita)
            {
                foraDoTerreno++;
                continue;
            }

            var faixa = DrainageArrows.BandOf(declive.Percent, faixas);
            var cor = cores[faixa];

            Traco(cauda, bico, cor);
            Traco(bico, esquerda, cor);
            Traco(bico, direita, cor);

            if (configuracao.ShowValue)
            {
                var texto = new DBText
                {
                    Position = new Point3d(centro.X + 0.15 * espacamento, centro.Y + 0.15 * espacamento, centro.Z),
                    Height = alturaDoTexto,
                    TextString = Tr.F("{0:0.#}%", declive.Percent),
                    Layer = "0",
                    Color = cor,
                };
                bloco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
            }

            porFaixa[faixa]++;
            flechas++;
        }

        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var camada = LayoutLayers.GarantirDrenagem(transacao, database, LayoutLayers.DrenagemFlecha);

        var insercao = new BlockReference(Point3d.Origin, bloco.ObjectId) { Layer = camada };
        espaco.AppendEntity(insercao);
        transacao.AddNewlyCreatedDBObject(insercao, true);
        PluginXData.Save(transacao, insercao, TipoDaInsercao, 1, flechas.ToString(System.Globalization.CultureInfo.InvariantCulture));

        DesenharLegenda(transacao, espaco, camada, terreno, faixas, porFaixa, maxX, maxY, espacamento);

        transacao.Commit();

        var resumo = string.Join("; ", faixas.Select((f, i) => $"{f.Label}: {porFaixa[i]}"));

        return Tr.F("FLECHAS {0} flecha(s) a cada {1:0.##} m ({2}), {3} ponto(s) plano(s), {4} ponto(s) fora do terreno, em {5:0.0} s. Camada {6}.",
            flechas, espacamento, resumo, planos, foraDoTerreno, relogio.Elapsed.TotalSeconds, camada);
    }

    /// <summary>A definição do bloco (já esvaziada por <see cref="ApagarNa"/>), ou uma nova.</summary>
    private static BlockTableRecord Definicao(Transaction transacao, BlockTable tabela)
    {
        if (tabela.Has(NomeDoBloco)) return (BlockTableRecord)transacao.GetObject(tabela[NomeDoBloco], OpenMode.ForWrite);

        var nova = new BlockTableRecord { Name = NomeDoBloco, Origin = Point3d.Origin };
        tabela.Add(nova);
        transacao.AddNewlyCreatedDBObject(nova, true);
        return nova;
    }

    /// <summary>
    /// A legenda, no canto de cima à direita da região: um quadrado de cor e o
    /// rótulo por faixa, com quantas flechas caíram nela. Texto deitado no
    /// plano, sempre na horizontal.
    /// </summary>
    private static void DesenharLegenda(Transaction transacao, BlockTableRecord espaco, string camada, Tin terreno,
        IReadOnlyList<SlopeBand> faixas, int[] porFaixa, double maxX, double maxY, double espacamento)
    {
        var lado = Math.Max(1.0, espacamento);
        var x0 = maxX + 2 * lado;
        var y0 = maxY;
        var z = terreno.TryGetZ(x0, y0, out var zz) ? zz + Acima : terreno.MaxZ + Acima;

        void Marcar(Entity e)
        {
            espaco.AppendEntity(e);
            transacao.AddNewlyCreatedDBObject(e, true);
            PluginXData.Save(transacao, e, TipoDaLegenda, 1);
        }

        Marcar(new DBText
        {
            Position = new Point3d(x0, y0 + 0.5 * lado, z),
            Height = 0.6 * lado,
            TextString = Tr.T("Sentido da água — declividade do terreno"),
            Layer = camada,
        });

        for (var i = 0; i < faixas.Count; i++)
        {
            var y = y0 - (i + 1) * 1.4 * lado;
            var cor = Color.FromRgb(faixas[i].Color.R, faixas[i].Color.G, faixas[i].Color.B);

            Marcar(new Solid(
                new Point3d(x0, y, z), new Point3d(x0 + lado, y, z),
                new Point3d(x0, y + lado, z), new Point3d(x0 + lado, y + lado, z))
            { Layer = camada, Color = cor });

            Marcar(new DBText
            {
                Position = new Point3d(x0 + 1.5 * lado, y + 0.2 * lado, z),
                Height = 0.5 * lado,
                TextString = Tr.F("{0}  ({1} flecha(s))", faixas[i].Label, porFaixa[i]),
                Layer = camada,
            });
        }
    }

    /// <summary>Apaga as flechas (as inserções do bloco, a definição esvaziada) e a legenda. Quantas entidades.</summary>
    internal static int Apagar(Database database)
    {
        using var transacao = database.TransactionManager.StartTransaction();
        var apagadas = ApagarNa(transacao, database);
        transacao.Commit();
        return apagadas;
    }

    /// <summary>O apagar dentro da transação de quem chama.</summary>
    private static int ApagarNa(Transaction transacao, Database database)
    {
        var apagadas = 0;

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var definicao = tabela.Has(NomeDoBloco) ? tabela[NomeDoBloco] : ObjectId.Null;
        var classeDaInsercao = RXObject.GetClass(typeof(BlockReference));
        var classeDoTexto = RXObject.GetClass(typeof(DBText));
        var classeDoSolido = RXObject.GetClass(typeof(Solid));

        foreach (ObjectId id in espaco)
        {
            // Só os tipos que a drenagem põe aqui: abrir cada mesa e cada
            // módulo do desenho para ler o XData seria varrer a usina inteira.
            if (id.ObjectClass != classeDaInsercao && id.ObjectClass != classeDoTexto && id.ObjectClass != classeDoSolido) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;

            var nossa = e is BlockReference r ? r.BlockTableRecord == definicao
                : string.Equals(e.Layer, LayoutLayers.DrenagemFlecha, StringComparison.OrdinalIgnoreCase) && PluginXData.Load(e, TipoDaLegenda, 1, 0) is not null;

            if (!nossa) continue;

            e.UpgradeOpen();
            e.Erase();
            apagadas++;
        }

        if (!definicao.IsNull)
        {
            var bloco = (BlockTableRecord)transacao.GetObject(definicao, OpenMode.ForWrite);
            foreach (ObjectId id in bloco) transacao.GetObject(id, OpenMode.ForWrite).Erase();
        }

        return apagadas;
    }

    /// <summary>As regiões para o modo: as áreas de implantação, ou nenhuma (o terreno todo).</summary>
    internal static IReadOnlyList<IReadOnlyList<Point3>> Regioes(Database database, OndeFlechas onde)
    {
        if (onde != OndeFlechas.Areas) return [];

        var regioes = new List<IReadOnlyList<Point3>>();

        foreach (var registro in AreaStore.Load(database))
        {
            if (FileiraCommands.LerArea(database, registro.Handle) is { } area && area.Vertices.Count >= 3)
                regioes.Add(area.Vertices);
        }

        if (regioes.Count == 0)
            throw new ArgumentException(Tr.T("Não há área de implantação no desenho. Use Tudo ou Janela, ou trace a área antes."));

        return regioes;
    }
}
