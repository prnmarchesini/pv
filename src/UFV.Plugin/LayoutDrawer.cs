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
    /// <param name="idDaMesa">
    /// O GUID a dar a cada mesa; null gera um novo. O recalcular (7.3) passa
    /// o GUID que a mesa já tinha, para ela continuar sendo ela.
    /// </param>
    /// <param name="analisar">
    /// O que sai junto com a fileira: cores das análises, cotas de altura,
    /// seta de declividade (esta só se a análise estiver ligada no desenho).
    /// Gerar passa <see cref="Analise.Nada"/> desde o 8.8 (Melhorias.docx,
    /// 01/10/2026: "quando gerar o desenho, não quero que ele saia
    /// analisando, pintando"); quem analisa é o menu Análises. A mesa que
    /// não cabe continua magenta: é aviso do motor, não análise. Null é
    /// tudo, como era antes do 8.8 (o Pintar e o Regerar das análises).
    /// </param>
    /// <param name="pontasAMao">
    /// As alturas das pontas escolhidas à mão, a gravar na identidade (botão
    /// Pontas, 27/09/2026); null, ou null para a mesa, é o motor quem decide.
    /// </param>
    internal static DrawnRow Draw(
        Database database,
        ProcessedRow fileira,
        TableGeometry geometria,
        SolarModule modulo,
        double tiltRadians,
        AnalysisRules regras,
        Func<ProcessedTable, Guid>? idDaMesa = null,
        Func<ProcessedTable, (double Primeira, double Ultima)?>? pontasAMao = null,
        Analise? analisar = null,
        TiposDeMesa? tipos = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(fileira);
        ArgumentNullException.ThrowIfNull(geometria);

        var oQue = analisar ?? Analise.Tudo;

        // A análise de declividade (seta e valor), se está ligada no desenho.
        var seta = oQue.Seta ? SetaDeDeclividade.Ler(database) : default;

        using var transacao = database.TransactionManager.StartTransaction();

        var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
        var espaco = (BlockTableRecord)transacao.GetObject(tabela[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var camadaMesa = LayoutLayers.Garantir(transacao, database, LayoutLayers.Mesa, new RgbColor(140, 140, 140));
        var camadaPilar = LayoutLayers.Garantir(transacao, database, LayoutLayers.Pilar, new RgbColor(120, 90, 60));
        var camadaModulo = LayoutLayers.Garantir(transacao, database, LayoutLayers.Modulo, new RgbColor(30, 60, 140));
        var camadaFace = LayoutLayers.Garantir(transacao, database, LayoutLayers.Face, new RgbColor(60, 120, 220));
        var camadaAlturas = oQue.Cotas
            ? LayoutLayers.Garantir(transacao, database, LayoutLayers.Alturas, new RgbColor(200, 200, 200), desligada: true)
            : null;
        var camadaMarcada = LayoutLayers.Garantir(transacao, database, LayoutLayers.Marcada, RgbColor.Red);
        var camadaSeta = seta.Ligada ? SetaDeDeclividade.Camada(transacao, database) : null;

        // As camadas das análises, todas, mesmo as que nada vai pintar hoje:
        // é nelas que o usuário liga e desliga o que vê.
        if (oQue.Cores)
        {
            foreach (var kind in AnalysisRules.RangedKinds)
                LayoutLayers.Garantir(transacao, database, regras.Rule(kind).Layer);
            LayoutLayers.Garantir(transacao, database, regras.EdgeRule.Layer);
        }

        // O estilo do projeto (8.13), resolvido uma vez para a fileira.
        var estilo = oQue.Cotas || seta.Ligada ? EstiloDoProjeto.PrepararTexto(transacao, database) : null;

        var blocoDoPilar = LayoutBlocks.GarantirPilar(transacao, database);
        // Um bloco de módulo por tipo de mesa (8.6: tipos podem ter módulos diferentes).
        var blocosDoModulo = (tipos?.Modulos ?? [modulo])
            .Select(m => LayoutBlocks.GarantirModulo(transacao, database, m))
            .ToList();

        var pilares = 0;
        var modulos = 0;
        var pintadas = 0;
        var marcadas = 0;

        foreach (var mesa in fileira.Tables)
        {
            // As peças desta mesa, para o grupo (um clique pega a mesa
            // inteira). As cotas de altura ficam fora: são anotação, que o
            // Regerar alturas apaga e refaz sozinho, e apagar uma cota não
            // pode levar a mesa junto.
            var pecas = new List<ObjectId>();

            // O tipo da mesa (8.6): a geometria, o módulo, o nome do perfil e
            // a cor dela; com um tipo só, os da fileira.
            var tipo = tipos is null ? 0 : mesa.Cell.Kind;
            var geo = tipos?.Geometrias[tipo] ?? geometria;
            var mod = tipos?.Modulos[tipo] ?? modulo;
            var blocoDoModulo = blocosDoModulo[tipos is null ? 0 : tipo];

            var colocacao = mesa.Placement;

            // O eixo X local da colocação corre com a fileira: é a direção
            // do risco de cota, e dela sai o rumo do texto, sempre legível.
            // A célula reconstruída de um contorno (Recalcular) pode vir com
            // a direção invertida; o texto nunca deve ficar de cabeça para
            // baixo (Renan, 26/09/2026: "os textos ficam todos zuados").
            var direcaoDaFileira = DirecaoDoEixoX(colocacao);
            var rumo = RumoLegivel(direcaoDaFileira.X, direcaoDaFileira.Y);

            var pontasDaMesa = pontasAMao?.Invoke(mesa);

            var identidade = new TableIdentity(
                idDaMesa?.Invoke(mesa) ?? Guid.NewGuid(), mesa.Label,
                mesa.Solved.StartElevation, mesa.Solved.EndElevation, tiltRadians,
                mesa.Solved.Marked, mesa.Solved.Reason,
                ModulePowerWatts: mod.PowerWatts,
                Anchor: colocacao.Apply(new Point3(0, 0, 0)),
                ManualFirstLowEdge: pontasDaMesa?.Primeira,
                ManualLastLowEdge: pontasDaMesa?.Ultima,
                ProfileName: tipos?.Nomes[tipo]);

            var matriz = Matriz(colocacao);

            // A mesa que não cabe no terreno (o alinhamento a marcou, ou um
            // pilar não tem altura livre / não tem terreno): pintada INTEIRA
            // de magenta na camada de marcadas, com o aviso no meio. Pedido
            // do Renan em 26/09/2026: "olha projetista, essa mesa tá socada
            // na terra porque ali não tem como fazer milagre".
            var naoCabe = mesa.Solved.Marked || mesa.Pillars.ProblemCount > 0;

            // Roxo: a distribuição tentou todas as mesas da lista neste lugar
            // e nenhuma ficou boa; ficou a primeira (02/10/2026).
            var corDeNaoCabe = mesa.Cell.TriedAll ? CorTentouTodas : CorDeNaoCabe;

            // 1. Pilares.
            for (var i = 0; i < mesa.Pillars.Pillars.Count; i++)
            {
                var pilar = mesa.Pillars.Pillars[i];
                var relatorio = mesa.Report.Pillars[i];
                var topo = new Point3d(pilar.X, pilar.Y, pilar.TopZ);
                var comprimento = pilar.Length ?? ComprimentoDoPilarComProblema;

                // A seção do pilar vem da pegada da geometria local: largura
                // ao longo da mesa, profundidade na inclinação.
                var pegada = geo.Pillars[i].Footprint;
                var largura = Distancia(pegada[0], pegada[1]);
                var profundidade = Distancia(pegada[0], pegada[3]);

                var bloco = new BlockReference(topo, blocoDoPilar)
                {
                    ScaleFactors = new Scale3d(largura, profundidade, comprimento),
                    Rotation = mesa.Cell.DirectionRadians,
                };

                var vereditoDoPilar = oQue.Cores ? relatorio.PaintVerdict : null;

                if (naoCabe)
                    PintarNaoCabe(bloco, vereditoDoPilar, camadaMarcada, corDeNaoCabe);
                else
                    Pintar(bloco, vereditoDoPilar, camadaPilar, ref pintadas);

                pecas.Add(espaco.AppendEntity(bloco));
                transacao.AddNewlyCreatedDBObject(bloco, true);

                LayoutXData.SavePillar(transacao, bloco, new PillarIdentity(
                    Guid.NewGuid(), identidade.Id, i + 1, pilar.Station, pilar.Length, pilar.Embedment, pilar.FreeHeight, pilar.Problem, pilar.GroundZ,
                    pilar.LowEdgeClearance, pilar.HighEdgeClearance));

                pilares++;

                // As cotas, na camada desligada, como no desenho do Renan
                // (26/09/2026): um risco vermelho na ponta baixa com a altura
                // livre dela, outro na ponta alta com a dela, e no centro (o
                // pilar) a altura livre do pilar, P3. Pilar com problema leva
                // só o P3, que diz o problema pelo número (negativo: enterrado;
                // "s/ terreno"). O motivo por extenso estourava a tela (Renan,
                // 29/09/2026: "para de colocar esses textos").
                var pontaBaixa = colocacao.Apply(new Point3(pilar.Station, 0, 0));
                var pontaAlta = colocacao.Apply(new Point3(pilar.Station, geo.Depth, 0));

                if (camadaAlturas is null)
                {
                    // Sem análise (8.8): nenhuma cota; quem põe é Análises.
                }
                else if (pilar.Problem is null)
                {
                    Cota(transacao, espaco, camadaAlturas, identidade.Id, pilar.LowEdgeClearance, "PB", pontaBaixa, direcaoDaFileira, rumo, estilo);
                    Cota(transacao, espaco, camadaAlturas, identidade.Id, pilar.HighEdgeClearance, "PA", pontaAlta, direcaoDaFileira, rumo, estilo);
                    Cota(transacao, espaco, camadaAlturas, identidade.Id, pilar.FreeHeight, "P3", new Point3(pilar.X, pilar.Y, pilar.TopZ), direcaoDaFileira, rumo, estilo);
                }
                else
                {
                    Cota(transacao, espaco, camadaAlturas, identidade.Id, pilar.FreeHeight, "P3", new Point3(pilar.X, pilar.Y, pilar.TopZ), direcaoDaFileira, rumo, estilo);
                }
            }

            // 2. Módulos: o bloco e a face superior.
            foreach (var peca in geo.Modules)
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

                var vereditoDoModulo = oQue.Cores ? relatorio?.Verdict : null;

                if (naoCabe)
                    PintarNaoCabe(bloco, vereditoDoModulo, camadaMarcada, corDeNaoCabe);
                else
                {
                    Pintar(bloco, vereditoDoModulo, camadaModulo, ref pintadas);

                    // A cor do tipo de mesa nos módulos (02/10/2026: "coloquei
                    // azul e ciano, e no desenho tem um azulzinho e magenta"):
                    // sem análise pintando, o módulo diz de que mesa é.
                    if (bloco.ColorIndex == 256 && tipos?.Cores[tipo] is { } corDoModulo)
                        bloco.Color = Color.FromRgb(corDoModulo.R, corDoModulo.G, corDoModulo.B);
                }

                pecas.Add(espaco.AppendEntity(bloco));
                transacao.AddNewlyCreatedDBObject(bloco, true);
                LayoutXData.SaveModule(transacao, bloco, modIdentidade);

                var cantos = peca.TopFace.Select(colocacao.Apply).ToList();
                var face = new Face(
                    Ponto(cantos[0]), Ponto(cantos[1]), Ponto(cantos[2]), Ponto(cantos[3]),
                    true, true, true, true)
                {
                    Layer = camadaFace,
                };

                pecas.Add(espaco.AppendEntity(face));
                transacao.AddNewlyCreatedDBObject(face, true);
                LayoutXData.SaveFace(transacao, face, new FaceIdentity(
                    Guid.NewGuid(), modIdentidade.Id, identidade.Id, peca.Column, peca.Row));

                modulos++;
            }

            // 3. O contorno da mesa: o plano dos módulos, fechado.
            var contorno = new Polyline3d { Closed = true, Layer = camadaMesa };

            pecas.Add(espaco.AppendEntity(contorno));
            transacao.AddNewlyCreatedDBObject(contorno, true);

            var cantosDoContorno = CantosDaMesa(geo).Select(colocacao.Apply).ToList();

            foreach (var canto in cantosDoContorno)
            {
                var vertice = new PolylineVertex3d(Ponto(canto));
                contorno.AppendVertex(vertice);
                transacao.AddNewlyCreatedDBObject(vertice, true);
            }

            if (naoCabe)
            {
                contorno.Layer = camadaMarcada;
                contorno.Color = corDeNaoCabe;
            }
            else if (oQue.Cores && mesa.Report.EdgeVerdict.Color is { } corDaBorda)
            {
                contorno.Layer = mesa.Report.EdgeVerdict.Layer!;
                contorno.Color = Color.FromRgb(corDaBorda.R, corDaBorda.G, corDaBorda.B);
                pintadas++;
            }
            else if (oQue.Cores && mesa.Report.SlopeVerdict.Color is { } corDaDeclividade)
            {
                contorno.Layer = mesa.Report.SlopeVerdict.Layer!;
                contorno.Color = Color.FromRgb(corDaDeclividade.R, corDaDeclividade.G, corDaDeclividade.B);
                pintadas++;
            }
            else if (tipos?.Cores[tipo] is { } corDoTipo)
            {
                // A cor do tipo de mesa (8.5: "se desse para usar cores
                // diferentes nelas, aí eu saberia qual é qual"). Não é
                // análise: é o nome da mesa dito em cor, no contorno.
                contorno.Color = Color.FromRgb(corDoTipo.R, corDoTipo.G, corDoTipo.B);
            }

            LayoutXData.SaveTable(transacao, contorno, identidade);

            // 4. A mesa que não cabe: sem texto no desenho (Renan, 29/09/2026:
            //    "para de colocar esses textos, estão estourando muito"). Ela
            //    já está magenta inteira; o motivo fica no XData, na linha de
            //    comando e no Estado.
            if (naoCabe) marcadas++;

            // 5. A seta da declividade, se a análise está ligada. Fora do
            //    grupo, como as cotas.
            if (camadaSeta is not null)
                SetaDeDeclividade.Desenhar(transacao, espaco, camadaSeta, identidade.Id, cantosDoContorno, seta.Unidade, estilo: estilo);

            LayoutGroups.Criar(transacao, database, pecas);
        }

        transacao.Commit();

        return new DrawnRow(fileira.Tables.Count, pilares, modulos, pintadas, marcadas);
    }

    /// <summary>
    /// O que o desenho de uma fileira leva de análise (passo 8.8).
    /// </summary>
    /// <param name="Cores">Pintar peças e contorno pelas regras de análise.</param>
    /// <param name="Cotas">Cotas de altura (PB, PA, P3) na camada das alturas.</param>
    /// <param name="Seta">Seta de declividade, se a análise está ligada no desenho.</param>
    internal sealed record Analise(bool Cores, bool Cotas, bool Seta)
    {
        internal static readonly Analise Tudo = new(true, true, true);
        internal static readonly Analise Nada = new(false, false, false);

        /// <summary>
        /// Para refazer uma mesa num desenho que já existe (Recalcular,
        /// Pontas): nunca pinta (pintar é análise que o usuário roda), mas
        /// acompanha o resto do desenho: cotas se ele tem cotas, seta se a
        /// declividade está ligada.
        /// </summary>
        internal static Analise ComoODesenho(Database database) =>
            new(false, AlturasCommands.HaCotas(database), true);
    }

    /// <summary>
    /// Os tipos de mesa de uma usina mista (8.6), pelo índice
    /// <see cref="PlacedTable.Kind"/>: geometria, módulo, nome do perfil e a
    /// cor do contorno (null, a da camada).
    /// </summary>
    internal sealed record TiposDeMesa(
        IReadOnlyList<TableGeometry> Geometrias,
        IReadOnlyList<SolarModule> Modulos,
        IReadOnlyList<string?> Nomes,
        IReadOnlyList<RgbColor?> Cores)
    {
        /// <summary>
        /// Para redesenhar UMA mesa que já existe (Recalcular, Pontas): o tipo
        /// dela, pelo nome gravado, se ele está entre as mesas do desenho;
        /// senão null, e ela sai como saía.
        /// </summary>
        /// <summary>
        /// A legenda das cores para a linha de comando: cada tipo com a cor
        /// dele, e o magenta da mesa que não cabe (02/10/2026: "não sei o que
        /// as cores representam").
        /// </summary>
        internal static string Legenda(TiposDeMesa? tipos, int marcadas)
        {
            var partes = new List<string>();

            if (tipos is not null)
            {
                for (var k = 0; k < tipos.Nomes.Count; k++)
                {
                    if (tipos.Cores[k] is { } cor) partes.Add($"{tipos.Nomes[k]} = {NomeDaCor(cor)}");
                }
            }

            partes.Add("cinza = mesa sem cor de tipo");
            partes.Add($"magenta = mesa que não cabe no terreno ({marcadas}; o motivo está no Estado)");
            if (tipos is { Nomes.Count: > 1 }) partes.Add("roxo = tentei todas as mesas da lista nesse lugar, nenhuma coube; ficou a 1ª");

            return "  Cores: " + string.Join("; ", partes) + ".";
        }

        private static string NomeDaCor(RgbColor cor) =>
            PaletaDeCores.Cores.FirstOrDefault(c => c.Cor == cor).Nome ?? cor.ToHex();

        internal static TiposDeMesa? DaMesa(Database database, string? nome, TableGeometry geometria, SolarModule modulo) =>
            DrawingTables.Find(MesasDoDesenho.Ler(database), nome) is { } registro
                ? new TiposDeMesa([geometria], [modulo], [registro.Name], [registro.Color])
                : null;
    }

    /// <summary>Metade do comprimento do risco vermelho de cota, em metro, ao longo da fileira.</summary>
    private const double MeioRisco = 0.50;

    /// <summary>Magenta: a cor da mesa que não cabe no terreno, inteira.</summary>
    private static readonly Color CorDeNaoCabe = Color.FromRgb(255, 0, 255);

    /// <summary>Roxo: a mesa que não cabe depois de tentadas todas as mesas da lista naquele lugar.</summary>
    private static readonly Color CorTentouTodas = Color.FromRgb(120, 40, 200);

    /// <summary>A direção (unitária, 3D) do eixo X local da colocação: ao longo da fileira, no plano da mesa.</summary>
    private static Point3 DirecaoDoEixoX(Transform colocacao)
    {
        var a = colocacao.Apply(new Point3(0, 0, 0));
        var b = colocacao.Apply(new Point3(1, 0, 0));
        var d = new Point3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var n = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);

        return n < 1e-12 ? new Point3(1, 0, 0) : new Point3(d.X / n, d.Y / n, d.Z / n);
    }

    /// <summary>
    /// O rumo de texto que se lê: o ângulo em planta da direção, trazido
    /// para (-90°, 90°] (texto virado para o leitor, nunca de cabeça para
    /// baixo).
    /// </summary>
    internal static double RumoLegivel(double dx, double dy)
    {
        var rumo = Math.Atan2(dy, dx);

        if (rumo > Math.PI / 2 + 1e-9) rumo -= Math.PI;
        else if (rumo <= -Math.PI / 2 + 1e-9) rumo += Math.PI;

        return rumo;
    }

    /// <summary>
    /// Uma cota: um risco vermelho no ponto, atravessado no sentido da
    /// fileira, e o valor ao lado ("PB 0,45"). Sem valor (sem terreno ali) o
    /// texto diz "PB s/ terreno".
    /// </summary>
    internal static void Cota(
        Transaction transacao, BlockTableRecord espaco, string camada, Guid mesa,
        double? valor, string sigla, Point3 ponto, Point3 direcaoDoRisco, double rumo,
        Action<MText>? estilo = null)
    {
        // O risco corre ao longo da fileira, no plano da mesa.
        var dx = direcaoDoRisco.X * MeioRisco;
        var dy = direcaoDoRisco.Y * MeioRisco;
        var dz = direcaoDoRisco.Z * MeioRisco;

        var risco = new Line(
            new Point3d(ponto.X - dx, ponto.Y - dy, ponto.Z - dz),
            new Point3d(ponto.X + dx, ponto.Y + dy, ponto.Z + dz))
        {
            Layer = camada,
            Color = Color.FromRgb(255, 0, 0),
        };

        espaco.AppendEntity(risco);
        transacao.AddNewlyCreatedDBObject(risco, true);
        LayoutXData.SaveNote(transacao, risco, new NoteIdentity(Guid.NewGuid(), mesa));

        var texto = new MText
        {
            Location = new Point3d(ponto.X, ponto.Y, ponto.Z + AlturaDoTexto),
            TextHeight = AlturaDoTexto,
            Layer = camada,
            Attachment = AttachmentPoint.MiddleCenter,
            Rotation = rumo,
            Contents = valor is { } v ? $"{sigla} {v.ToString("0.00", Brasil)}" : $"{sigla} s/ terreno",
        };

        espaco.AppendEntity(texto);
        transacao.AddNewlyCreatedDBObject(texto, true);
        estilo?.Invoke(texto);
        LayoutXData.SaveNote(transacao, texto, new NoteIdentity(Guid.NewGuid(), mesa));
    }

    /// <summary>
    /// A peça da mesa que não cabe: na camada de marcadas (desligar a camada
    /// esconde a mesa inteira), com a cor da análise quando ela pinta, e
    /// magenta quando não. Renan, 27/09/2026: "mostrou que a ponta ficou
    /// fora do padrão mas não pintou o módulo" — é o módulo pintado que diz
    /// ONDE a mesa não cabe.
    /// </summary>
    private static void PintarNaoCabe(Entity entidade, AnalysisVerdict? veredito, string camadaMarcada, Color corDeNaoCabe)
    {
        entidade.Layer = camadaMarcada;
        entidade.Color = veredito?.Color is { } cor ? Color.FromRgb(cor.R, cor.G, cor.B) : corDeNaoCabe;
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
