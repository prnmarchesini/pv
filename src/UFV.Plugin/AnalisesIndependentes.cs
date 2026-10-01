using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;
using UFV.Geo;

namespace UFV.Plugin;

/// <summary>
/// As análises independentes do menu Análises (passos 8.9 a 8.11,
/// Melhorias.docx, 01/10/2026): altura das pontas (baixa e alta),
/// declividade e pilar acima do terreno. Cada uma insere os seus textos,
/// pinta pela regra (abaixo de X uma cor, acima de Y outra), apaga os
/// textos, tira as cores e quantifica, sem mexer no que a outra fez ("análise
/// eu sempre faço independente para não ter erro e nem confusão").
///
/// A identidade de cada texto vai no XData (<see cref="AnalysisTextIdentity"/>),
/// com o valor; a camada própria de cada análise é só para ligar e desligar.
/// Uma transação por operação do usuário.
/// </summary>
internal static class AnalisesIndependentes
{
    /// <summary>Altura do texto, em metro, até o estilo do projeto (8.13) mandar.</summary>
    internal const double AlturaDoTexto = 0.35;

    /// <summary>Quanto o texto da ponta fica para fora da borda da mesa, em planta.</summary>
    private const double AfastamentoDaBorda = 0.45;

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));

    /// <summary>Uma mesa lida do desenho, com o que as análises precisam.</summary>
    private sealed record MesaLida(
        Guid Id,
        ObjectId Contorno,
        IReadOnlyList<Point3> Cantos,
        IReadOnlyList<(PillarIdentity Pilar, ObjectId Bloco)> Pilares,
        IReadOnlyList<(ModuleIdentity Modulo, ObjectId Bloco)> Modulos)
    {
        /// <summary>O comprimento da borda baixa.</summary>
        internal double Comprimento => Distancia(Cantos[0], Cantos[1]);

        /// <summary>O ponto da borda baixa (ou alta) na estação dada.</summary>
        internal Point3 NaBorda(double estacao, bool alta)
        {
            var t = Comprimento < RowDistributor.MenorMedida ? 0 : Math.Clamp(estacao / Comprimento, 0, 1);
            var (a, b) = alta ? (Cantos[3], Cantos[2]) : (Cantos[0], Cantos[1]);
            return new Point3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>A direção em planta da borda baixa para a alta.</summary>
        internal (double X, double Y) ParaCima
        {
            get
            {
                var dx = Cantos[3].X - Cantos[0].X;
                var dy = Cantos[3].Y - Cantos[0].Y;
                var n = Math.Sqrt(dx * dx + dy * dy);
                return n < 1e-9 ? (0, 0) : (dx / n, dy / n);
            }
        }

        /// <summary>O rumo legível do texto, ao longo da fileira.</summary>
        internal double Rumo => LayoutDrawer.RumoLegivel(Cantos[1].X - Cantos[0].X, Cantos[1].Y - Cantos[0].Y);

        /// <summary>A declividade ao longo da fileira, na unidade dada, pela linha do meio da mesa.</summary>
        internal double Declividade(SlopeUnit unidade)
        {
            var inicio = Meio(Cantos[0], Cantos[3]);
            var fim = Meio(Cantos[1], Cantos[2]);
            var emPlanta = Math.Sqrt((fim.X - inicio.X) * (fim.X - inicio.X) + (fim.Y - inicio.Y) * (fim.Y - inicio.Y));
            return emPlanta < RowDistributor.MenorMedida ? double.NaN : IndependentAnalysis.SlopeValue(fim.Z - inicio.Z, emPlanta, unidade);
        }
    }

    // ------------------------------------------------------------- inserir

    /// <summary>
    /// Insere os textos da análise em todas as mesas. Os que já existiam
    /// (desta análise) saem antes: inserir de novo não empilha texto.
    /// Quantos textos criou.
    /// </summary>
    internal static int Inserir(Database database, IndependentKind tipo, SlopeUnit unidade)
    {
        Apagar(database, tipo);

        using var transacao = database.TransactionManager.StartTransaction();

        var espaco = Espaco(transacao, database, OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, IndependentAnalysis.LayerName(tipo), CorDaCamada(tipo));
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
        var criados = 0;

        foreach (var mesa in Mesas(transacao, database))
        {
            switch (tipo)
            {
                case IndependentKind.Slope:
                {
                    var valor = mesa.Declividade(unidade);
                    if (!double.IsFinite(valor)) break;

                    criados += SetaDeDeclividade.Desenhar(
                        transacao, espaco, camada, mesa.Id, mesa.Cantos, unidade,
                        (t, e) => LayoutXData.SaveAnalysisText(t, e, new AnalysisTextIdentity(Guid.NewGuid(), mesa.Id, tipo, valor)));
                    break;
                }

                case IndependentKind.LowEdge:
                case IndependentKind.HighEdge:
                {
                    var alta = tipo == IndependentKind.HighEdge;
                    var (ux, uy) = mesa.ParaCima;
                    var sinal = alta ? 1 : -1;

                    foreach (var (pilar, _) in mesa.Pilares)
                    {
                        var valor = (alta ? pilar.HighEdgeClearance : pilar.LowEdgeClearance) ?? double.NaN;
                        var naBorda = mesa.NaBorda(pilar.Station, alta);
                        var onde = new Point3(naBorda.X + sinal * ux * AfastamentoDaBorda, naBorda.Y + sinal * uy * AfastamentoDaBorda, naBorda.Z);

                        Texto(transacao, espaco, camada, mesa.Id, tipo, valor, onde, mesa.Rumo, unidade, estilo);
                        criados++;
                    }

                    break;
                }

                default:
                {
                    foreach (var (pilar, bloco) in mesa.Pilares)
                    {
                        var valor = pilar.FreeHeight ?? double.NaN;
                        var topo = ((BlockReference)transacao.GetObject(bloco, OpenMode.ForRead)).Position;

                        Texto(transacao, espaco, camada, mesa.Id, tipo, valor, new Point3(topo.X, topo.Y, topo.Z), mesa.Rumo, unidade, estilo);
                        criados++;
                    }

                    break;
                }
            }
        }

        transacao.Commit();
        return criados;
    }

    private static void Texto(
        Transaction transacao, BlockTableRecord espaco, string camada, Guid mesa, IndependentKind tipo,
        double valor, Point3 onde, double rumo, SlopeUnit unidade, Action<MText> estilo)
    {
        var conteudo = double.IsFinite(valor)
            ? IndependentAnalysis.Label(tipo, valor, unidade)
            : IndependentAnalysis.Label(tipo, 0, unidade).Split(' ')[0] + " s/ terreno";

        var texto = new MText
        {
            Location = new Point3d(onde.X, onde.Y, onde.Z),
            TextHeight = AlturaDoTexto,
            Layer = camada,
            Attachment = AttachmentPoint.MiddleCenter,
            Rotation = rumo,
            Contents = conteudo,
        };

        estilo(texto);

        espaco.AppendEntity(texto);
        transacao.AddNewlyCreatedDBObject(texto, true);
        LayoutXData.SaveAnalysisText(transacao, texto, new AnalysisTextIdentity(Guid.NewGuid(), mesa, tipo, valor));
    }

    // -------------------------------------------------------------- apagar

    /// <summary>Apaga os textos desta análise (só desta). Quantas entidades apagou.</summary>
    internal static int Apagar(Database database, IndependentKind tipo)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var apagadas = 0;

        foreach (var (id, texto) in Textos(transacao, database, tipo))
        {
            var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
            entidade.Erase();
            apagadas++;
        }

        transacao.Commit();
        return apagadas;
    }

    // ------------------------------------------------------------ analisar

    /// <summary>
    /// Pinta pela regra: os textos desta análise e, se a regra pede, as
    /// peças (módulos nas pontas, contorno na declividade, pilares). Dentro
    /// da faixa volta à cor da camada. A mesa que não cabe (camada de
    /// marcadas) não é tocada: ela continua dizendo que não cabe.
    /// </summary>
    internal static (int Textos, int Pecas) Analisar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade)
    {
        ArgumentNullException.ThrowIfNull(regra);

        using var transacao = database.TransactionManager.StartTransaction();

        var textos = 0;

        foreach (var (id, identidade) in Textos(transacao, database, tipo))
        {
            var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
            if (Colorir(entidade, double.IsFinite(identidade.Value) ? regra.ColorOf(identidade.Value) : null)) textos++;
        }

        var pecas = 0;

        if (regra.PaintPieces)
        {
            foreach (var (peca, valor) in PecasComValor(transacao, database, tipo, unidade))
            {
                var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
                if (EMarcada(entidade)) continue;
                if (Colorir(entidade, double.IsFinite(valor) ? regra.ColorOf(valor) : null)) pecas++;
            }
        }

        transacao.Commit();
        return (textos, pecas);
    }

    /// <summary>
    /// Tira as cores desta análise: textos e peças do tipo dela voltam à cor
    /// da camada. A peça que o Pintar antigo (antes do 8.9) tinha mudado para
    /// uma camada de análise volta para a camada fixa.
    /// </summary>
    internal static int TirarCores(Database database, IndependentKind tipo)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var mexidas = 0;

        foreach (var (id, _) in Textos(transacao, database, tipo))
        {
            var entidade = (Entity)transacao.GetObject(id, OpenMode.ForWrite);
            if (VoltarACamada(entidade, null)) mexidas++;
        }

        foreach (var (peca, _) in PecasComValor(transacao, database, tipo, SlopeUnit.Percent))
        {
            var entidade = (Entity)transacao.GetObject(peca, OpenMode.ForWrite);
            if (EMarcada(entidade)) continue;
            if (VoltarACamada(entidade, CamadaFixa(tipo))) mexidas++;
        }

        transacao.Commit();
        return mexidas;
    }

    // --------------------------------------------------------- quantificar

    /// <summary>
    /// A contagem da análise, pelos dados das mesas (não pelos textos): os
    /// pontos (pilares, ou mesas na declividade) e, nas pontas, os módulos.
    /// </summary>
    internal static (BandCount Pontos, BandCount? Modulos) Quantificar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var pontos = new List<double>();

        foreach (var mesa in Mesas(transacao, database))
        {
            switch (tipo)
            {
                case IndependentKind.Slope: pontos.Add(mesa.Declividade(unidade)); break;
                case IndependentKind.LowEdge: pontos.AddRange(mesa.Pilares.Select(p => p.Pilar.LowEdgeClearance ?? double.NaN)); break;
                case IndependentKind.HighEdge: pontos.AddRange(mesa.Pilares.Select(p => p.Pilar.HighEdgeClearance ?? double.NaN)); break;
                default: pontos.AddRange(mesa.Pilares.Select(p => p.Pilar.FreeHeight ?? double.NaN)); break;
            }
        }

        BandCount? modulos = null;

        if (tipo is IndependentKind.LowEdge or IndependentKind.HighEdge)
            modulos = BandCount.Of(regra, PecasComValor(transacao, database, tipo, unidade).Select(p => p.Valor));

        transacao.Commit();
        return (BandCount.Of(regra, pontos), modulos);
    }

    // ------------------------------------------------------------- leitura

    private static BlockTableRecord Espaco(Transaction transacao, Database database, OpenMode modo)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], modo);
    }

    /// <summary>As mesas do desenho com contorno de quatro cantos, pilares e módulos lidos.</summary>
    private static List<MesaLida> Mesas(Transaction transacao, Database database)
    {
        var mesas = new List<MesaLida>();

        foreach (var (guid, partes) in LayoutScan.Tables(transacao, database))
        {
            if (partes.Identity is null || partes.Contour is not { } contorno) continue;

            var cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao);
            if (cantos.Count != 4) continue;

            var pilares = new List<(PillarIdentity, ObjectId)>();

            foreach (var id in partes.Pillars)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadPillar(e) is { } p)
                    pilares.Add((p, id));
            }

            var modulos = new List<(ModuleIdentity, ObjectId)>();

            foreach (var id in partes.Modules)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadModule(e) is { } m)
                    modulos.Add((m, id));
            }

            mesas.Add(new MesaLida(guid, contorno, cantos, pilares.OrderBy(p => p.Item1.Station).ToList(), modulos));
        }

        return mesas;
    }

    /// <summary>Os textos desta análise no espaço do modelo.</summary>
    private static List<(ObjectId Id, AnalysisTextIdentity Texto)> Textos(Transaction transacao, Database database, IndependentKind tipo)
    {
        var achados = new List<(ObjectId, AnalysisTextIdentity)>();

        foreach (ObjectId id in Espaco(transacao, database, OpenMode.ForRead))
        {
            if (id.ObjectClass != ClasseDoTexto && id.ObjectClass != ClasseDaLinha) continue;

            if (transacao.GetObject(id, OpenMode.ForRead) is Entity e
                && LayoutXData.LoadAnalysisText(e) is { } texto
                && texto.Kind == tipo)
            {
                achados.Add((id, texto));
            }
        }

        return achados;
    }

    /// <summary>
    /// As peças que a análise pinta, com o valor de cada uma: nas pontas, cada
    /// módulo com o valor do pilar mais perto da coluna dele; na
    /// declividade, o contorno; no pilar, o bloco.
    /// </summary>
    private static IEnumerable<(ObjectId Peca, double Valor)> PecasComValor(Transaction transacao, Database database, IndependentKind tipo, SlopeUnit unidade)
    {
        foreach (var mesa in Mesas(transacao, database))
        {
            switch (tipo)
            {
                case IndependentKind.Slope:
                    yield return (mesa.Contorno, mesa.Declividade(unidade));
                    break;

                case IndependentKind.PillarAbove:
                    foreach (var (pilar, bloco) in mesa.Pilares) yield return (bloco, pilar.FreeHeight ?? double.NaN);
                    break;

                default:
                {
                    if (mesa.Pilares.Count == 0 || mesa.Modulos.Count == 0) break;

                    var colunas = mesa.Modulos.Max(m => m.Modulo.Column) + 1;
                    var largura = mesa.Comprimento / colunas;

                    foreach (var (modulo, bloco) in mesa.Modulos)
                    {
                        var estacao = (modulo.Column + 0.5) * largura;
                        var perto = mesa.Pilares.MinBy(p => Math.Abs(p.Pilar.Station - estacao)).Pilar;
                        var valor = (tipo == IndependentKind.HighEdge ? perto.HighEdgeClearance : perto.LowEdgeClearance) ?? double.NaN;

                        yield return (bloco, valor);
                    }

                    break;
                }
            }
        }
    }

    // --------------------------------------------------------------- cores

    /// <summary>Põe a cor (ou volta à da camada, com null). Se mudou algo.</summary>
    private static bool Colorir(Entity entidade, RgbColor? cor)
    {
        if (cor is { } c)
        {
            entidade.Color = Color.FromRgb(c.R, c.G, c.B);
            return true;
        }

        entidade.ColorIndex = 256;
        return false;
    }

    private static bool VoltarACamada(Entity entidade, string? camadaFixa)
    {
        var mudou = entidade.ColorIndex != 256 || entidade.Color.IsByAci == false;

        entidade.ColorIndex = 256;

        if (camadaFixa is not null && entidade.Layer.StartsWith(PluginInfo.PrefixoDeDados + "_ANALISE_", StringComparison.OrdinalIgnoreCase))
        {
            entidade.Layer = camadaFixa;
            mudou = true;
        }

        return mudou;
    }

    private static bool EMarcada(Entity entidade) =>
        string.Equals(entidade.Layer, LayoutLayers.Marcada, StringComparison.OrdinalIgnoreCase);

    private static string CamadaFixa(IndependentKind tipo) => tipo switch
    {
        IndependentKind.Slope => LayoutLayers.Mesa,
        IndependentKind.PillarAbove => LayoutLayers.Pilar,
        _ => LayoutLayers.Modulo,
    };

    private static RgbColor CorDaCamada(IndependentKind tipo) => tipo switch
    {
        IndependentKind.LowEdge => new RgbColor(255, 255, 255),
        IndependentKind.HighEdge => new RgbColor(200, 200, 200),
        IndependentKind.Slope => new RgbColor(0, 200, 255),
        _ => new RgbColor(220, 180, 120),
    };

    private static Point3 Meio(Point3 a, Point3 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

    private static double Distancia(Point3 a, Point3 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
