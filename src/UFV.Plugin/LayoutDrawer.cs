using System.Globalization;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>O que o desenho de uma fileira produziu.</summary>
/// <param name="Tables">Quantas mesas foram desenhadas.</param>
/// <param name="Pillars">Quantos pilares.</param>
/// <param name="Modules">Quantos módulos (blocos), e o mesmo tanto de faces.</param>
/// <param name="Painted">Quantas peças saíram pintadas por alguma análise.</param>
/// <param name="Marked">Quantas mesas saíram marcadas.</param>
internal sealed record DrawnRow(int Tables, int Pillars, int Modules, int Painted, int Marked);

/// <summary>
/// Desenha uma fileira processada no desenho: pilares primeiro (blocos),
/// depois módulos (blocos mais a face superior na camada de face), o contorno
/// da mesa, os textos de altura e o aviso da marcada — numa transação só,
/// como manda a arquitetura (uma transação por operação do usuário).
///
/// Cores e camadas vêm dos vereditos (5.6): peça pintada vai para a camada
/// da análise com a cor dela na instância (as caixas dentro dos blocos são
/// "por bloco", senão a cor da instância não aparece); o resto fica nas
/// camadas fixas, cor por camada. A face superior nunca é pintada: ela é o
/// que o PVsyst recebe, e a camada dela tem que ter só faces. O contorno da
/// mesa recebe uma pintura só: borda vence declividade, e a declividade
/// pintada de uma mesa na borda não aparece no contorno (está no relatório).
///
/// Regra sagrada 5 em forma de código: toda cota que entra aqui vem da matriz
/// da mesa, que veio do terreno (5.4/5.5). Nenhum Z de clique chega perto.
/// </summary>
internal static class LayoutDrawer
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Comprimento com que se desenha um pilar sem comprimento (problema), só para ele existir na tela.</summary>
    private const double ComprimentoDoPilarComProblema = 1.0;

    /// <summary>Altura do texto das alturas e dos avisos, em metro de desenho.</summary>
    private const double AlturaDoTexto = 0.35;

    /// <summary>Desenha a fileira. Abre e fecha a própria transação.</summary>
    /// <param name="tiltRadians">A inclinação transversal da mesa, gravada na identidade dela.</param>
    internal static DrawnRow Draw(
        Database database,
        ProcessedRow fileira,
        TableGeometry geometria,
        SolarModule modulo,
        double tiltRadians,
        AnalysisRules regras)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(fileira);
        ArgumentNullException.ThrowIfNull(geometria);

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var camadaMesa = LayoutLayers.Garantir(transacao, database, LayoutLayers.Mesa, new RgbColor(140, 140, 140));
        var camadaPilar = LayoutLayers.Garantir(transacao, database, LayoutLayers.Pilar, new RgbColor(120, 90, 60));
        var camadaModulo = LayoutLayers.Garantir(transacao, database, LayoutLayers.Modulo, new RgbColor(30, 60, 140));
        var camadaFace = LayoutLayers.Garantir(transacao, database, LayoutLayers.Face, new RgbColor(60, 120, 220));
        var camadaAlturas = LayoutLayers.Garantir(transacao, database, LayoutLayers.Alturas, new RgbColor(200, 200, 200), desligada: true);
        var camadaMarcada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Marcada, RgbColor.Red);

        // As camadas das análises, todas, mesmo as que nada vai pintar hoje:
        // é nelas que o usuário liga e desliga o que vê.
        foreach (var kind in AnalysisRules.RangedKinds)
            LayoutLayers.Garantir(transacao, database, regras.Rule(kind).Layer);
        LayoutLayers.Garantir(transacao, database, regras.EdgeRule.Layer);

        var blocoDoPilar = LayoutBlocks.GarantirPilar(transacao, database);
        var blocoDoModulo = LayoutBlocks.GarantirModulo(transacao, database, modulo);

        var pilares = 0;
        var modulos = 0;
        var pintadas = 0;
        var marcadas = 0;

        foreach (var mesa in fileira.Tables)
        {
            var identidade = new TableIdentity(
                Guid.NewGuid(), mesa.Label,
                mesa.Solved.StartElevation, mesa.Solved.EndElevation, tiltRadians,
                mesa.Solved.Marked, mesa.Solved.Reason);

            var colocacao = mesa.Placement;
            var matriz = Matriz(colocacao);

            // 1. Pilares.
            for (var i = 0; i < mesa.Pillars.Pillars.Count; i++)
            {
                var pilar = mesa.Pillars.Pillars[i];
                var relatorio = mesa.Report.Pillars[i];
                var topo = new Point3d(pilar.X, pilar.Y, pilar.TopZ);
                var comprimento = pilar.Length ?? ComprimentoDoPilarComProblema;

                // A seção do pilar vem da pegada da geometria local: largura
                // ao longo da mesa, profundidade na inclinação.
                var pegada = geometria.Pillars[i].Footprint;
                var largura = Distancia(pegada[0], pegada[1]);
                var profundidade = Distancia(pegada[0], pegada[3]);

                var bloco = new BlockReference(topo, blocoDoPilar)
                {
                    ScaleFactors = new Scale3d(largura, profundidade, comprimento),
                    Rotation = mesa.Cell.DirectionRadians,
                };

                if (pilar.Problem is not null)
                {
                    bloco.Layer = camadaMarcada;
                    bloco.Color = Color.FromRgb(255, 0, 0);
                }
                else
                {
                    Pintar(bloco, relatorio.PaintVerdict, camadaPilar, ref pintadas);
                }

                espaco.AppendEntity(bloco);
                transacao.AddNewlyCreatedDBObject(bloco, true);

                LayoutXData.SavePillar(transacao, bloco, new PillarIdentity(
                    Guid.NewGuid(), identidade.Id, i + 1, pilar.Station, pilar.Length, pilar.Embedment, pilar.FreeHeight, pilar.Problem, pilar.GroundZ));

                pilares++;

                // O texto da altura, na camada desligada.
                var texto = new MText
                {
                    Location = new Point3d(pilar.X, pilar.Y, pilar.TopZ + AlturaDoTexto),
                    TextHeight = AlturaDoTexto,
                    Layer = camadaAlturas,
                    Contents = pilar.Length is { } p1
                        ? $"P1 {p1.ToString("0.00", Brasil)} m\\P(P3 {pilar.FreeHeight!.Value.ToString("0.00", Brasil)} + P2 {pilar.Embedment.ToString("0.00", Brasil)})"
                        : $"PILAR: {pilar.Problem}",
                };

                espaco.AppendEntity(texto);
                transacao.AddNewlyCreatedDBObject(texto, true);
            }

            // 2. Módulos: o bloco e a face superior.
            foreach (var peca in geometria.Modules)
            {
                var relatorio = peca.Row == 0 ? mesa.Report.Modules.FirstOrDefault(m => m.Column == peca.Column) : null;
                var modIdentidade = new ModuleIdentity(Guid.NewGuid(), identidade.Id, peca.Column, peca.Row, relatorio?.Clearance);

                // O canto da ponta baixa esquerda do módulo, em coordenadas
                // locais, é a origem do bloco.
                var canto = peca.TopFace[0];
                var deslocamento = Matrix3d.Displacement(new Vector3d(canto.X, canto.Y, canto.Z));

                var bloco = new BlockReference(Point3d.Origin, blocoDoModulo)
                {
                    BlockTransform = matriz * deslocamento,
                };

                Pintar(bloco, relatorio?.Verdict, camadaModulo, ref pintadas);

                espaco.AppendEntity(bloco);
                transacao.AddNewlyCreatedDBObject(bloco, true);
                LayoutXData.SaveModule(transacao, bloco, modIdentidade);

                var cantos = peca.TopFace.Select(colocacao.Apply).ToList();
                var face = new Face(
                    Ponto(cantos[0]), Ponto(cantos[1]), Ponto(cantos[2]), Ponto(cantos[3]),
                    true, true, true, true)
                {
                    Layer = camadaFace,
                };

                espaco.AppendEntity(face);
                transacao.AddNewlyCreatedDBObject(face, true);
                LayoutXData.SaveFace(transacao, face, new FaceIdentity(
                    Guid.NewGuid(), modIdentidade.Id, identidade.Id, peca.Column, peca.Row));

                modulos++;
            }

            // 3. O contorno da mesa: o plano dos módulos, fechado.
            var contorno = new Polyline3d { Closed = true, Layer = camadaMesa };

            espaco.AppendEntity(contorno);
            transacao.AddNewlyCreatedDBObject(contorno, true);

            foreach (var canto in CantosDaMesa(geometria).Select(colocacao.Apply))
            {
                var vertice = new PolylineVertex3d(Ponto(canto));
                contorno.AppendVertex(vertice);
                transacao.AddNewlyCreatedDBObject(vertice, true);
            }

            if (mesa.Report.EdgeVerdict.Color is { } corDaBorda)
            {
                contorno.Layer = mesa.Report.EdgeVerdict.Layer!;
                contorno.Color = Color.FromRgb(corDaBorda.R, corDaBorda.G, corDaBorda.B);
                pintadas++;
            }
            else if (mesa.Report.SlopeVerdict.Color is { } corDaDeclividade)
            {
                contorno.Layer = mesa.Report.SlopeVerdict.Layer!;
                contorno.Color = Color.FromRgb(corDaDeclividade.R, corDaDeclividade.G, corDaDeclividade.B);
                pintadas++;
            }

            LayoutXData.SaveTable(transacao, contorno, identidade);

            // 4. O aviso da marcada, no meio da mesa, na camada de marcadas.
            if (mesa.Solved.Marked)
            {
                var centro = colocacao.Apply(new Point3(geometria.Length / 2, geometria.Depth / 2, 0));
                var aviso = new MText
                {
                    Location = new Point3d(centro.X, centro.Y, centro.Z + AlturaDoTexto),
                    TextHeight = AlturaDoTexto * 1.5,
                    Layer = camadaMarcada,
                    Contents = $"{mesa.Label} MARCADA\\P{mesa.Solved.Reason}",
                };

                espaco.AppendEntity(aviso);
                transacao.AddNewlyCreatedDBObject(aviso, true);
                marcadas++;
            }
        }

        transacao.Commit();

        return new DrawnRow(fileira.Tables.Count, pilares, modulos, pintadas, marcadas);
    }

    /// <summary>Camada e cor da peça: a da análise quando ela pinta, a fixa quando não.</summary>
    private static void Pintar(Entity entidade, AnalysisVerdict? veredito, string camadaFixa, ref int pintadas)
    {
        if (veredito?.Color is { } cor && veredito.Layer is { } camada)
        {
            entidade.Layer = camada;
            entidade.Color = Color.FromRgb(cor.R, cor.G, cor.B);
            pintadas++;
            return;
        }

        entidade.Layer = camadaFixa;
    }

    /// <summary>Os quatro cantos do plano da mesa, em coordenadas locais.</summary>
    private static IEnumerable<Point3> CantosDaMesa(TableGeometry geometria)
    {
        yield return new Point3(0, 0, 0);
        yield return new Point3(geometria.Length, 0, 0);
        yield return new Point3(geometria.Length, geometria.Depth, 0);
        yield return new Point3(0, geometria.Depth, 0);
    }

    /// <summary>A Transform do Geo como matriz do AutoCAD, linha a linha.</summary>
    internal static Matrix3d Matriz(Transform t) => new(
    [
        t.XX, t.XY, t.XZ, t.TX,
        t.YX, t.YY, t.YZ, t.TY,
        t.ZX, t.ZY, t.ZZ, t.TZ,
        0, 0, 0, 1,
    ]);

    private static Point3d Ponto(Point3 p) => new(p.X, p.Y, p.Z);

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
