using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// As análises independentes do menu Análises (passos 8.9 a 8.11,
/// Melhorias.docx, 01/10/2026): altura das pontas (baixa e alta),
/// declividade e pilar acima do terreno. Cada uma insere os seus textos,
/// pinta pela regra (abaixo de X uma cor, acima de Y outra), apaga os
/// textos, tira as cores e quantifica, sem mexer no que a outra fez ("análise
/// eu sempre faço independente para não ter erro e nem confusão").
///
/// Independência das cores: cada análise guarda no desenho quais peças ela
/// pintou (<see cref="PecasPintadas"/>). Analisar só pinta quem sai da faixa
/// (quem fica dentro não perde a cor de outra análise) e Tirar cores só
/// desfaz o que ela mesma pintou, devolvendo a cor que a peça tinha antes
/// (magenta da mesa que não cabe, cor do tipo de mesa, ou a da camada). A
/// mesa que não cabe também é pintada (02/10/2026, com print de texto
/// vermelho sobre módulo magenta: "não pintou o módulo"): é nela que a
/// ponta sai da faixa, e o módulo pintado mostra onde.
///
/// A identidade de cada texto vai no XData (<see cref="AnalysisTextIdentity"/>),
/// com o valor; a camada própria de cada análise é só para ligar e desligar.
/// Uma transação e uma varredura do desenho por operação do usuário.
/// </summary>
internal static class AnalisesIndependentes
{
    /// <summary>Altura do texto, em metro, quando o projeto não tem estilo escolhido (8.13).</summary>
    internal const double AlturaDoTexto = 0.35;

    /// <summary>Quanto o texto da ponta fica para fora da borda da mesa, em planta.</summary>
    private const double AfastamentoDaBorda = 0.45;

    /// <summary>Quanto os textos da parte enterrada e do comprimento total saem do pilar, em planta, para não cobrir o da parte livre.</summary>
    private const double AfastamentoDosPilares = 0.45;

    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));

    /// <summary>Uma mesa lida do desenho, com o que as análises precisam.</summary>
    private sealed record MesaLida(
        Guid Id,
        bool Marcada,
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

        /// <summary>A declividade ao longo da fileira, na unidade dada, pela linha do meio da mesa (a mesma da seta).</summary>
        internal double Declividade(SlopeUnit unidade)
        {
            var inicio = Meio(Cantos[0], Cantos[3]);
            var fim = Meio(Cantos[1], Cantos[2]);
            var emPlanta = Math.Sqrt((fim.X - inicio.X) * (fim.X - inicio.X) + (fim.Y - inicio.Y) * (fim.Y - inicio.Y));
            return emPlanta < RowDistributor.MenorMedida ? double.NaN : IndependentAnalysis.SlopeValue(fim.Z - inicio.Z, emPlanta, unidade);
        }
    }

    /// <summary>O que uma varredura do desenho achou: as mesas e os textos de análise.</summary>
    private sealed record Leitura(List<MesaLida> Mesas, List<(ObjectId Id, AnalysisTextIdentity Texto)> Textos, int ContornosIgnorados);

    // ------------------------------------------------------------- inserir

    /// <summary>
    /// Insere os textos da análise em todas as mesas. Os que já existiam
    /// (desta análise) saem antes, na mesma transação: inserir de novo não
    /// empilha texto, e se a inserção falhar os antigos ficam.
    /// </summary>
    internal static (int Criados, int Mesas, int Ignoradas) Inserir(Database database, IndependentKind tipo, SlopeUnit unidade)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var leitura = Ler(transacao, database);

        foreach (var (id, texto) in leitura.Textos)
        {
            if (texto.Kind == tipo) ((Entity)transacao.GetObject(id, OpenMode.ForWrite)).Erase();
        }

        var espaco = Espaco(transacao, database, OpenMode.ForWrite);
        var camada = LayoutLayers.Garantir(transacao, database, IndependentAnalysis.LayerName(tipo), CorDaCamada(tipo));
        var estilo = EstiloDoProjeto.PrepararTexto(transacao, database);
        var criados = 0;

        foreach (var mesa in leitura.Mesas)
        {
            switch (tipo)
            {
                case IndependentKind.Slope:
                {
                    var valor = mesa.Declividade(unidade);
                    if (!double.IsFinite(valor)) break;

                    criados += SetaDeDeclividade.Desenhar(
                        transacao, espaco, camada, mesa.Id, mesa.Cantos, unidade,
                        (t, e) => LayoutXData.SaveAnalysisText(t, e, new AnalysisTextIdentity(Guid.NewGuid(), mesa.Id, tipo, valor)),
                        estilo);
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

                        // Na borda, no plano da mesa (regra 5: a cota é a da
                        // mesa, que acompanha o terreno), só afastado em planta.
                        var onde = new Point3(naBorda.X + sinal * ux * AfastamentoDaBorda, naBorda.Y + sinal * uy * AfastamentoDaBorda, naBorda.Z);

                        Texto(transacao, espaco, camada, mesa.Id, tipo, valor, onde, mesa.Rumo, unidade, estilo);
                        criados++;
                    }

                    break;
                }

                default:
                {
                    // As três análises de pilar escrevem no pilar; para não
                    // se sobreporem em planta, a enterrada vai um pouco para
                    // baixo (na direção da borda baixa) e a total para cima.
                    var (ux, uy) = mesa.ParaCima;
                    var desvio = tipo switch
                    {
                        IndependentKind.PillarBuried => -AfastamentoDosPilares,
                        IndependentKind.PillarLength => AfastamentoDosPilares,
                        _ => 0,
                    };

                    foreach (var (pilar, bloco) in mesa.Pilares)
                    {
                        var valor = IndependentAnalysis.PillarValue(tipo, pilar);
                        var topo = ((BlockReference)transacao.GetObject(bloco, OpenMode.ForRead)).Position;

                        // A cota é a do topo do pilar, que acompanha o terreno (regra 5).
                        var onde = new Point3(topo.X + ux * desvio, topo.Y + uy * desvio, topo.Z);

                        Texto(transacao, espaco, camada, mesa.Id, tipo, valor, onde, mesa.Rumo, unidade, estilo);
                        criados++;
                    }

                    break;
                }
            }
        }

        transacao.Commit();
        return (criados, leitura.Mesas.Count, leitura.ContornosIgnorados);
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

        espaco.AppendEntity(texto);
        transacao.AddNewlyCreatedDBObject(texto, true);

        // Depois do AppendEntity: o texto anotativo precisa do banco.
        estilo(texto);
        LayoutXData.SaveAnalysisText(transacao, texto, new AnalysisTextIdentity(Guid.NewGuid(), mesa, tipo, valor));
    }

    // -------------------------------------------------------------- apagar

    /// <summary>Apaga os textos desta análise (só desta). Quantas entidades apagou.</summary>
    internal static int Apagar(Database database, IndependentKind tipo)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var apagadas = 0;

        foreach (var (id, _) in Textos(transacao, database, tipo))
        {
            ((Entity)transacao.GetObject(id, OpenMode.ForWrite)).Erase();
            apagadas++;
        }

        transacao.Commit();
        return apagadas;
    }

    // ------------------------------------------------------------ analisar

    /// <summary>
    /// Pinta pela regra os textos desta análise e, se a regra pede, as peças
    /// (módulos nas pontas, contorno na declividade, pilares). Antes, desfaz
    /// a pintura anterior DESTA análise nas peças; quem fica dentro da faixa
    /// não é tocado, para não perder a cor que outra análise pôs. Cada peça
    /// pintada guarda a cor de antes, para Tirar cores devolvê-la. Quantos
    /// textos e quantas peças ficaram pintados.
    /// </summary>
    internal static (int Textos, int Pecas) Analisar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade)
    {
        ArgumentNullException.ThrowIfNull(regra);

        using var transacao = database.TransactionManager.StartTransaction();

        var leitura = Ler(transacao, database);
        var textos = 0;

        foreach (var (id, identidade) in leitura.Textos)
        {
            if (identidade.Kind != tipo) continue;

            // O texto é desta análise só: dentro da faixa ele volta à cor da
            // camada, que ninguém mais pinta.
            var cor = double.IsFinite(identidade.Value) ? regra.ColorOf(identidade.Value) : null;
            Colorir(transacao, id, cor);
            if (cor is not null) textos++;
        }

        // A peça que outra análise já pintou: a cor de antes é a que ELA
        // guardou (a original), não a pintura dela. Lido ANTES do desfazer,
        // que regrava o dicionário: ler depois, com a transação aberta,
        // derrubava o Core Console (access violation).
        var originais = DasOutras(database, tipo).ToDictionary(p => p.Id, p => p.Antes);

        // A pintura anterior desta análise sai antes da nova: mudar a regra
        // não deixa peça com a cor de antes.
        DesfazerPecas(transacao, database, tipo);

        var pintadas = new List<(ObjectId, Color, Color)>();

        if (regra.PaintPieces)
        {
            foreach (var (peca, valor) in PecasComValor(leitura.Mesas, tipo, unidade, incluirMarcadas: true))
            {
                if (!double.IsFinite(valor) || regra.ColorOf(valor) is not { } cor) continue;
                if (peca.IsErased || transacao.GetObject(peca, OpenMode.ForRead) is not Entity entidade) continue;

                var antes = originais.TryGetValue(peca, out var original) ? original : entidade.Color;
                var pintura = Color.FromRgb(cor.R, cor.G, cor.B);
                Colorir(transacao, peca, pintura);
                pintadas.Add((peca, antes, pintura));
            }
        }

        PecasPintadas.Gravar(database, tipo, pintadas);

        transacao.Commit();
        return (textos, pintadas.Count);
    }

    /// <summary>
    /// Tira as cores desta análise: os textos dela e as peças que ela pintou
    /// voltam à cor de antes (a da camada, a do tipo ou magenta). Peça
    /// pintada também por outra análise fica com a cor da outra.
    /// </summary>
    internal static int TirarCores(Database database, IndependentKind tipo)
    {
        using var transacao = database.TransactionManager.StartTransaction();

        var mexidas = 0;

        foreach (var (id, _) in Textos(transacao, database, tipo))
        {
            if (Colorir(transacao, id, (RgbColor?)null)) mexidas++;
        }

        mexidas += DesfazerPecas(transacao, database, tipo);

        transacao.Commit();
        return mexidas;
    }

    /// <summary>
    /// As peças que esta análise pintou da última vez voltam à cor de antes;
    /// a lista é zerada. Se outra análise também pintou a peça, ela fica
    /// com a cor dessa outra.
    /// </summary>
    private static int DesfazerPecas(Transaction transacao, Database database, IndependentKind tipo)
    {
        var desfeitas = 0;
        var dasOutras = new Dictionary<ObjectId, Color>();

        foreach (var p in DasOutras(database, tipo))
        {
            if (p.Pintura is { } pintura) dasOutras[p.Id] = pintura;
        }

        foreach (var (id, antes, _) in PecasPintadas.Ler(database, tipo))
        {
            if (Colorir(transacao, id, dasOutras.TryGetValue(id, out var daOutra) ? daOutra : antes)) desfeitas++;
        }

        PecasPintadas.Gravar(database, tipo, []);
        return desfeitas;
    }

    /// <summary>As peças pintadas pelas outras análises (a mesma peça pode aparecer em mais de uma: vale a primeira).</summary>
    private static IEnumerable<(ObjectId Id, Color Antes, Color? Pintura)> DasOutras(Database database, IndependentKind tipo)
    {
        var vistas = new HashSet<ObjectId>();

        foreach (var outra in Enum.GetValues<IndependentKind>())
        {
            if (outra == tipo) continue;

            foreach (var p in PecasPintadas.Ler(database, outra))
            {
                if (vistas.Add(p.Id)) yield return p;
            }
        }
    }

    // --------------------------------------------------------- quantificar

    /// <summary>
    /// A contagem da análise, pelos dados das mesas (não pelos textos): os
    /// pontos (pilares, ou mesas na declividade) e, nas pontas, os módulos.
    /// Mesas marcadas entram: elas existem e vão para o campo.
    /// </summary>
    internal static (BandCount Pontos, BandCount? Modulos) Quantificar(Database database, IndependentKind tipo, ThresholdRule regra, SlopeUnit unidade)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var mesas = Ler(transacao, database).Mesas;
        var pontos = new List<double>();

        foreach (var mesa in mesas)
        {
            switch (tipo)
            {
                case IndependentKind.Slope: pontos.Add(mesa.Declividade(unidade)); break;
                case IndependentKind.LowEdge: pontos.AddRange(mesa.Pilares.Select(p => p.Pilar.LowEdgeClearance ?? double.NaN)); break;
                case IndependentKind.HighEdge: pontos.AddRange(mesa.Pilares.Select(p => p.Pilar.HighEdgeClearance ?? double.NaN)); break;
                default: pontos.AddRange(mesa.Pilares.Select(p => IndependentAnalysis.PillarValue(tipo, p.Pilar))); break;
            }
        }

        BandCount? modulos = null;

        if (tipo is IndependentKind.LowEdge or IndependentKind.HighEdge)
            modulos = BandCount.Of(regra, PecasComValor(mesas, tipo, unidade, incluirMarcadas: true).Select(p => p.Valor).ToList());

        transacao.Commit();
        return (BandCount.Of(regra, pontos), modulos);
    }

    // ------------------------------------------------------------- leitura

    private static BlockTableRecord Espaco(Transaction transacao, Database database, OpenMode modo)
    {
        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], modo);
    }

    /// <summary>
    /// Uma varredura: as mesas (contorno de quatro cantos, pilares e módulos
    /// com a identidade) e os textos de análise, que o LayoutScan junta às
    /// notas de cada mesa.
    /// </summary>
    private static Leitura Ler(Transaction transacao, Database database)
    {
        var mesas = new List<MesaLida>();
        var textos = new List<(ObjectId, AnalysisTextIdentity)>();
        var ignorados = 0;

        foreach (var (guid, partes) in LayoutScan.Tables(transacao, database))
        {
            foreach (var id in partes.Notes)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadAnalysisText(e) is { } texto)
                    textos.Add((id, texto));
            }

            if (partes.Identity is not { } identidade || partes.Contour is not { } contorno) continue;

            var cantos = FileiraCommands.Vertices((Polyline3d)transacao.GetObject(contorno, OpenMode.ForRead), transacao);

            if (cantos.Count != 4)
            {
                ignorados++;
                continue;
            }

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

            mesas.Add(new MesaLida(guid, identidade.Marked, contorno, cantos, pilares.OrderBy(p => p.Item1.Station).ToList(), modulos));
        }

        return new Leitura(mesas, textos, ignorados);
    }

    /// <summary>Os textos desta análise (Apagar e Tirar cores não precisam das mesas).</summary>
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
    /// módulo com o valor do pilar mais perto da coluna dele (sem pilar, sem
    /// valor); na declividade, o contorno; no pilar, o bloco.
    /// </summary>
    private static IEnumerable<(ObjectId Peca, double Valor)> PecasComValor(
        IReadOnlyList<MesaLida> mesas, IndependentKind tipo, SlopeUnit unidade, bool incluirMarcadas)
    {
        foreach (var mesa in mesas)
        {
            if (mesa.Marcada && !incluirMarcadas) continue;

            switch (tipo)
            {
                case IndependentKind.Slope:
                    yield return (mesa.Contorno, mesa.Declividade(unidade));
                    break;

                case IndependentKind.PillarAbove:
                case IndependentKind.PillarBuried:
                case IndependentKind.PillarLength:
                    foreach (var (pilar, bloco) in mesa.Pilares) yield return (bloco, IndependentAnalysis.PillarValue(tipo, pilar));
                    break;

                default:
                {
                    if (mesa.Modulos.Count == 0) break;

                    var colunas = mesa.Modulos.Max(m => m.Modulo.Column) + 1;
                    var largura = mesa.Comprimento / colunas;

                    foreach (var (modulo, bloco) in mesa.Modulos)
                    {
                        if (mesa.Pilares.Count == 0)
                        {
                            yield return (bloco, double.NaN);
                            continue;
                        }

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

    /// <summary>
    /// Põe a cor, ou volta à da camada com null. Abre para escrita só se
    /// muda algo (o desfazer e a data de modificação não ficam sujos à toa).
    /// Se mudou.
    /// </summary>
    private static bool Colorir(Transaction transacao, ObjectId id, RgbColor? cor) =>
        Colorir(transacao, id, cor is { } c ? Color.FromRgb(c.R, c.G, c.B) : Color.FromColorIndex(ColorMethod.ByLayer, 256));

    private static bool Colorir(Transaction transacao, ObjectId id, Color nova)
    {
        if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) return false;
        if (entidade.Color == nova) return false;

        entidade.UpgradeOpen();
        entidade.Color = nova;
        return true;
    }

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

/// <summary>
/// As peças que cada análise pintou, no dicionário do desenho, pelo handle,
/// cada uma com a cor que tinha antes e a que recebeu ("handle=antes=pintura").
/// É o que deixa Tirar cores desfazer só o que a própria análise fez e
/// devolver a cor certa. Registro V1 (sem as cores) volta à cor da camada.
/// </summary>
internal static class PecasPintadas
{
    private const string Versao = "V2";
    private const string VersaoSemCor = "V1";

    private static string Chave(IndependentKind tipo) => "PINTADAS_" + tipo.ToString().ToUpperInvariant();

    private static readonly System.Globalization.CultureInfo Invariante = System.Globalization.CultureInfo.InvariantCulture;

    internal static List<(ObjectId Id, Color Antes, Color? Pintura)> Ler(Database database, IndependentKind tipo)
    {
        var pecas = new List<(ObjectId, Color, Color?)>();

        using var dados = PluginDictionary.Load(database, Chave(tipo));
        if (dados is null) return pecas;

        foreach (var valor in dados.AsArray())
        {
            if (valor.Value is not string texto || texto == Versao || texto == VersaoSemCor) continue;

            var partes = texto.Split('=', 3);
            if (!long.TryParse(partes[0], System.Globalization.NumberStyles.HexNumber, Invariante, out var numero)) continue;
            if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;

            pecas.Add((id, partes.Length >= 2 ? CorDe(partes[1]) : PelaCamada, partes.Length == 3 ? CorDe(partes[2]) : null));
        }

        return pecas;
    }

    internal static void Gravar(Database database, IndependentKind tipo, IReadOnlyList<(ObjectId Id, Color Antes, Color Pintura)> pecas)
    {
        // O registro nunca fica vazio: a versão vai sempre na frente.
        var valores = new List<TypedValue> { new((int)DxfCode.Text, Versao) };
        valores.AddRange(pecas.Select(p => new TypedValue((int)DxfCode.Text, p.Id.Handle + "=" + Texto(p.Antes) + "=" + Texto(p.Pintura))));

        PluginDictionary.Save(database, Chave(tipo), new ResultBuffer(valores.ToArray()));
    }

    private static Color PelaCamada => Color.FromColorIndex(ColorMethod.ByLayer, 256);

    /// <summary>A cor em texto: L (camada), B (bloco), I&lt;índice&gt; ou R&lt;rrggbb&gt;.</summary>
    internal static string Texto(Color cor) =>
        cor.IsByLayer ? "L"
        : cor.IsByBlock ? "B"
        : cor.ColorMethod == ColorMethod.ByColor ? "R" + cor.Red.ToString("X2", Invariante) + cor.Green.ToString("X2", Invariante) + cor.Blue.ToString("X2", Invariante)
        : "I" + cor.ColorIndex.ToString(Invariante);

    internal static Color CorDe(string texto)
    {
        if (texto == "B") return Color.FromColorIndex(ColorMethod.ByBlock, 0);

        if (texto.StartsWith('R') && texto.Length == 7
            && int.TryParse(texto.AsSpan(1), System.Globalization.NumberStyles.HexNumber, Invariante, out var rgb))
            return Color.FromRgb((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

        if (texto.StartsWith('I') && short.TryParse(texto.AsSpan(1), Invariante, out var indice) && indice is >= 1 and <= 255)
            return Color.FromColorIndex(ColorMethod.ByAci, indice);

        return PelaCamada;
    }
}
