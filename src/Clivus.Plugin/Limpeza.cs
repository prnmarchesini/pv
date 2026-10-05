using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// O Apagar da Edição (pedido do Renan em 05/10/2026): cores, textos,
/// sombras, strings e infra elétrica, uma ou várias de uma vez, no desenho
/// todo. Quem decide o que cada opção leva é o Core (<see cref="Cleanup"/>);
/// aqui se lê o desenho num plano (o que existe, com as contagens para a
/// janela) e se executa o plano numa transação só.
///
/// Só entidade com o nosso XData é tocada (a camada não decide nada): o que
/// o usuário desenhou, mesmo numa camada CLIVUS_*, fica.
///
/// Planejar lê tudo antes (os registros do dicionário inclusive); Executar
/// só escreve. Ler o dicionário com a transação de escrita aberta derrubava
/// o Core Console (AnalisesIndependentes, 02/10/2026).
/// </summary>
internal static class Limpeza
{
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaPolilinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline3d));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoMText = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoTexto = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDoCirculo = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Circle));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaLinha = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Line));
    private static readonly Autodesk.AutoCAD.Runtime.RXClass ClasseDaHachura = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Hatch));

    /// <summary>As cores que as sombras usaram até 03/10/2026 (amarelo, laranja, vermelho), as mesmas de SombrasCommands.</summary>
    private static readonly RgbColor[] CoresAntigasDaSombra = [new(255, 220, 0), new(255, 140, 0), new(190, 30, 0)];

    /// <summary>Os registros das sombras (os mesmos de SombrasCommands).</summary>
    private const string ChaveDasPintadasDaSombra = "SOMBRA_PINTADAS";
    private const string ChaveDosMotivosDaSombra = "SOMBRA_MOTIVOS";

    /// <summary>Os registros da infra elétrica que a opção 5 esvazia (os modelos de inversor ficam).</summary>
    private static readonly string[] ChavesDaInfra = [ElectricalStore.ChaveDosInversores, "TRAFOS", "SUBESTACOES", "SUBESTACOES_BLOCOS", "SKIDS"];

    private static Color PelaCamada => Color.FromColorIndex(ColorMethod.ByLayer, 256);

    /// <summary>O que o desenho tem, lido de uma vez, e quantos itens cada opção leva.</summary>
    internal sealed class Plano
    {
        /// <summary>Cada entidade que alguma opção apaga, com o que ela é.</summary>
        internal List<(ObjectId Id, CleanupTarget Alvo)> Entidades { get; } = [];

        /// <summary>Opção 1: a peça e a cor original dela (só as que estão com outra).</summary>
        internal Dictionary<ObjectId, Color> CoresOriginais { get; } = [];

        /// <summary>
        /// Opção 1: a peça de mesa que a análise do desenho levou para a camada
        /// dela, e a camada original (a fixa da peça, ou a de marcadas).
        /// </summary>
        internal Dictionary<ObjectId, string> CamadasOriginais { get; } = [];

        /// <summary>Opção 3: o módulo marcado pela sombra e a cor de antes dele.</summary>
        internal Dictionary<ObjectId, Color> DaSombra { get; } = [];

        /// <summary>Opção 5: as strings com inversor ou tag, que ficam soltas (se não forem apagadas).</summary>
        internal List<(ObjectId Id, ElectricalString String)> ASoltar { get; } = [];

        /// <summary>Opções 1 e 5: as polilinhas e os sinais das strings que não estão ByLayer.</summary>
        internal List<ObjectId> StringsPintadas { get; } = [];

        /// <summary>Opção 5: as definições CLIVUS_EQUIPAMENTO_* (saem se nenhuma referência sobrar).</summary>
        internal List<ObjectId> DefinicoesDeEquipamento { get; } = [];

        /// <summary>Os registros de "peças pintadas" das análises que existem (a opção 1 os zera).</summary>
        internal List<IndependentKind> AnalisesComPintadas { get; } = [];

        internal bool TemPintadasDaSombra { get; set; }

        internal bool TemMotivosDaSombra { get; set; }

        /// <summary>Os registros da infra que existem (a opção 5 os esvazia).</summary>
        internal List<string> RegistrosDaInfra { get; } = [];

        internal CleanupCount Contagem { get; set; } = Cleanup.Count([], 0, 0, 0, 0, 0, 0, 0);
    }

    // ------------------------------------------------------------ planejar

    /// <summary>Lê o desenho e monta o plano de todas as opções (a janela mostra as contagens).</summary>
    internal static Plano Planejar(Database database, Action<double>? progresso = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        var plano = new Plano();

        // Os registros primeiro, fora de transação (cada leitura abre e fecha a sua).
        var antesDasAnalises = new Dictionary<ObjectId, List<Color>>();
        foreach (var tipo in Enum.GetValues<IndependentKind>())
        {
            var pecas = PecasPintadas.Ler(database, tipo);
            if (PluginDictionary.Contains(database, "PINTADAS_" + tipo.ToString().ToUpperInvariant())) plano.AnalisesComPintadas.Add(tipo);

            foreach (var (id, antes, _) in pecas) Juntar(antesDasAnalises, id, antes);
        }

        var antesDaSombra = PintadasDaSombra(database);
        foreach (var (id, antes) in antesDaSombra) Juntar(antesDasAnalises, id, antes);
        plano.TemPintadasDaSombra = PluginDictionary.Contains(database, ChaveDasPintadasDaSombra);
        plano.TemMotivosDaSombra = PluginDictionary.Contains(database, ChaveDosMotivosDaSombra);

        var tiposDeMesa = MesasDoDesenho.Ler(database);

        var inversores = ElectricalStore.Inverters(database).Items.Count;
        var trafos = ElectricalStore.Transformers(database).Items.Count;
        // A subestação física: o bloco compartilhado, ou a UC que não está
        // dentro de um (a unitária, a compartilhada de desenho antigo).
        var subestacoes = ElectricalStore.Substations(database).Items.Count
            + ElectricalStore.ConsumerUnits(database).Items.Count(u => u.Substation == Guid.Empty);
        var skids = ElectricalStore.Skids(database).Items.Count;
        foreach (var chave in ChavesDaInfra)
            if (PluginDictionary.Contains(database, chave)) plano.RegistrosDaInfra.Add(chave);

        var corDaSombra = new[] { 0.1, 0.4, 0.9 }.Select(SombrasCommands.CorDaFracao).Concat(CoresAntigasDaSombra).ToHashSet();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
            var total = 0;
            if (progresso is not null) foreach (ObjectId _ in espaco) total++;
            var vistos = 0;

            // 1. As entidades soltas do plugin: anotações, strings, sombras.
            foreach (ObjectId id in espaco)
            {
                if (progresso is not null && ++vistos % 500 == 0) progresso(0.8 * vistos / Math.Max(1, total));
                if (id.IsErased) continue;

                var classe = id.ObjectClass;
                if (classe != ClasseDaPolilinha && classe != ClasseDoMText && classe != ClasseDoTexto && classe != ClasseDoCirculo && classe != ClasseDaLinha && classe != ClasseDaHachura) continue;
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade || TipoDoXData(entidade) is not { } tipo) continue;

                var alvo = Cleanup.Classify(tipo, entidade is MText or DBText);

                // O texto de análise pintado pela regra volta à cor da camada (opção 1).
                if (tipo == AnalysisTextIdentity.Tipo && !entidade.Color.IsByLayer) plano.CoresOriginais[id] = PelaCamada;

                if (alvo == CleanupTarget.StringPath && ElectricalStore.LoadString(entidade) is { } s)
                {
                    if (s.IsAllocated || !string.IsNullOrEmpty(s.Tag)) plano.ASoltar.Add((id, s));
                }

                if (alvo is CleanupTarget.StringPath or CleanupTarget.StringSign && !entidade.Color.IsByLayer)
                {
                    plano.StringsPintadas.Add(id);
                    plano.CoresOriginais[id] = PelaCamada;
                }

                if (alvo != CleanupTarget.None) plano.Entidades.Add((id, alvo));
            }

            // 2. As peças das mesas: a cor original de cada uma (opção 1) e a
            //    marca da sombra (opção 3).
            var camadas = (LayerTable)transacao.GetObject(database.LayerTableId, OpenMode.ForRead);

            foreach (var partes in LayoutScan.Tables(transacao, database).Values)
            {
                // Peça sem o contorno da mesa (sobra de desenho antigo): não
                // há como saber a cor original; fica como está.
                if (partes.Identity is not { } mesa) continue;

                var corDoTipo = DrawingTables.Find(tiposDeMesa, mesa.ProfileName)?.Color;
                var tentouTodas = mesa.Marked && partes.Contours.Concat(partes.Modules).Any(p =>
                    ERoxo(CorDe(transacao, p)) || (antesDasAnalises.TryGetValue(p, out var antes) && antes.Any(ERoxo)));

                void Conferir(ObjectId id, CleanupPiece peca)
                {
                    if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) return;

                    var original = Cleanup.OriginalColor(peca, mesa.Marked, tentouTodas, corDoTipo) is { } c
                        ? Color.FromRgb(c.R, c.G, c.B)
                        : PelaCamada;

                    // A camada também: a análise feita no desenho da usina
                    // levava a peça para a camada dela (LayoutDrawer.Pintar).
                    var camada = mesa.Marked ? LayoutLayers.Marcada : peca switch
                    {
                        CleanupPiece.Contour => LayoutLayers.Mesa,
                        CleanupPiece.Pillar => LayoutLayers.Pilar,
                        _ => LayoutLayers.Modulo,
                    };

                    var outraCamada = !string.Equals(entidade.Layer, camada, StringComparison.OrdinalIgnoreCase) && camadas.Has(camada);
                    if (outraCamada) plano.CamadasOriginais[id] = camada;
                    if (outraCamada || !entidade.Color.Equals(original)) plano.CoresOriginais[id] = original;
                }

                foreach (var id in partes.Contours) Conferir(id, CleanupPiece.Contour);
                foreach (var id in partes.Pillars) Conferir(id, CleanupPiece.Pillar);
                foreach (var id in partes.Modules) Conferir(id, CleanupPiece.Module);
            }

            foreach (var (id, antes) in antesDaSombra)
            {
                if (CorDe(transacao, id) is { ColorMethod: ColorMethod.ByColor } atual && corDaSombra.Contains(new RgbColor(atual.Red, atual.Green, atual.Blue)))
                    plano.DaSombra[id] = antes;
            }

            // 3. Os equipamentos em campo e as definições dos blocos deles.
            foreach (var ids in EquipamentoEmCampo.Posicionados(transacao, database).Values)
                foreach (var id in ids) plano.Entidades.Add((id, CleanupTarget.Equipment));

            var tabela = (BlockTable)transacao.GetObject(database.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in tabela)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is BlockTableRecord definicao && !definicao.IsErased
                    && definicao.Name.StartsWith(PluginInfo.PrefixoDeDados + "_EQUIPAMENTO_", StringComparison.OrdinalIgnoreCase))
                    plano.DefinicoesDeEquipamento.Add(id);
            }

            transacao.Commit();
        }

        progresso?.Invoke(1);

        plano.Contagem = Cleanup.Count(
            plano.Entidades.Select(e => e.Alvo),
            plano.CoresOriginais.Count,
            plano.DaSombra.Count,
            inversores,
            trafos,
            subestacoes,
            skids,
            plano.ASoltar.Count);

        return plano;
    }

    // ------------------------------------------------------------ executar

    /// <summary>
    /// Executa o plano com as opções marcadas, numa transação (um U desfaz,
    /// dentro do comando). Devolve o que foi feito, contado como no plano.
    /// </summary>
    internal static CleanupCount Executar(Database database, Plano plano, CleanupOptions opcoes, Action<double>? progresso = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(plano);

        var apagar = plano.Entidades.Where(e => Cleanup.Erases(e.Alvo, opcoes)).Select(e => e.Id).ToHashSet();
        var cores = opcoes.HasFlag(CleanupOptions.Colors);
        var sombras = opcoes.HasFlag(CleanupOptions.Shadows);
        var infra = opcoes.HasFlag(CleanupOptions.Electrical);
        var passos = Math.Max(1, apagar.Count + (cores ? plano.CoresOriginais.Count : 0) + (sombras ? plano.DaSombra.Count : 0) + (infra ? plano.ASoltar.Count : 0));
        var feitos = 0;

        void Andar()
        {
            if (progresso is not null && ++feitos % 200 == 0) progresso(0.95 * feitos / passos);
        }

        var recoloridas = 0;
        var devolvidos = 0;
        var soltas = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            // 1. O que sai.
            foreach (var id in apagar)
            {
                Andar();
                if (Abrir(transacao, id) is { } e) e.Erase();
            }

            // 2. A sombra: o módulo marcado volta à cor de antes (se a opção 1
            //    também vai, ela decide a cor logo abaixo).
            if (sombras)
            {
                foreach (var (id, antes) in plano.DaSombra)
                {
                    Andar();
                    if (Abrir(transacao, id) is not { } e) continue;
                    if (!e.Color.Equals(antes)) e.Color = antes;
                    devolvidos++;
                }
            }

            // 3. As cores originais.
            if (cores)
            {
                foreach (var (id, cor) in plano.CoresOriginais)
                {
                    Andar();
                    if (apagar.Contains(id) || Abrir(transacao, id) is not { } e) continue;
                    if (!e.Color.Equals(cor)) e.Color = cor;
                    if (plano.CamadasOriginais.TryGetValue(id, out var camada) && !string.Equals(e.Layer, camada, StringComparison.OrdinalIgnoreCase)) e.Layer = camada;
                    recoloridas++;
                }
            }

            // 4. A infra: as strings que ficam soltam o inversor e a tag (a
            //    numeração dependia da cadeia) e voltam à cor da camada.
            if (infra)
            {
                foreach (var (id, s) in plano.ASoltar)
                {
                    Andar();
                    if (apagar.Contains(id) || Abrir(transacao, id) is not { } e) continue;
                    ElectricalStore.SaveString(transacao, e, s with { Inverter = Guid.Empty, Tag = string.Empty });
                    soltas++;
                }

                foreach (var id in plano.StringsPintadas)
                {
                    if (apagar.Contains(id) || Abrir(transacao, id) is not { } e) continue;
                    if (!e.Color.IsByLayer) e.Color = PelaCamada;
                }

                foreach (var id in plano.DefinicoesDeEquipamento)
                {
                    if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not BlockTableRecord definicao) continue;

                    // Referência que sobrou (em outro bloco, no papel): a definição fica.
                    if (definicao.GetBlockReferenceIds(true, true).Cast<ObjectId>().Any(r => !r.IsErased)) continue;

                    definicao.UpgradeOpen();
                    definicao.Erase();
                }
            }

            progresso?.Invoke(0.97);

            // 5. Os registros, dentro da mesma transação (o Save abre uma
            //    aninhada, que só vale com o Commit desta).
            if (cores)
            {
                foreach (var tipo in plano.AnalisesComPintadas) PecasPintadas.Gravar(database, tipo, []);
                if (plano.TemPintadasDaSombra) ZerarDaSombra(database, ChaveDasPintadasDaSombra);
            }

            if (sombras)
            {
                if (plano.TemPintadasDaSombra) ZerarDaSombra(database, ChaveDasPintadasDaSombra);
                if (plano.TemMotivosDaSombra) ZerarDaSombra(database, ChaveDosMotivosDaSombra);
            }

            if (infra)
            {
                foreach (var chave in plano.RegistrosDaInfra)
                {
                    switch (chave)
                    {
                        case ElectricalStore.ChaveDosInversores: ElectricalStore.SaveInverters(database, []); break;
                        case "TRAFOS": ElectricalStore.SaveTransformers(database, []); break;
                        case "SUBESTACOES": ElectricalStore.SaveConsumerUnits(database, []); break;
                        case "SUBESTACOES_BLOCOS": ElectricalStore.SaveSubstations(database, []); break;
                        case "SKIDS": ElectricalStore.SaveSkids(database, []); break;
                    }
                }
            }

            transacao.Commit();
        }

        progresso?.Invoke(1);

        var c = plano.Contagem;
        return Cleanup.Count(
            plano.Entidades.Where(e => apagar.Contains(e.Id)).Select(e => e.Alvo),
            recoloridas,
            devolvidos,
            infra ? c.Inverters : 0,
            infra ? c.Transformers : 0,
            infra ? c.Substations : 0,
            infra ? c.Skids : 0,
            soltas);
    }

    // -------------------------------------------------------------- miúdos

    /// <summary>O tipo do nosso XData ("Mesa", "Nota", "String"...), ou null se a entidade não é do plugin.</summary>
    internal static string? TipoDoXData(Entity entidade)
    {
        using var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo);
        if (dados is null) return null;

        var campos = dados.AsArray();
        return campos.Length >= 2 ? campos[1].Value as string : null;
    }

    /// <summary>A entidade aberta para escrita (camada travada não impede), ou null se sumiu.</summary>
    private static Entity? Abrir(Transaction transacao, ObjectId id) =>
        id.IsErased ? null : transacao.GetObject(id, OpenMode.ForWrite, false, true) as Entity;

    private static Color? CorDe(Transaction transacao, ObjectId id) =>
        id.IsErased ? null : (transacao.GetObject(id, OpenMode.ForRead) as Entity)?.Color;

    private static bool ERoxo(Color? cor) =>
        cor is { ColorMethod: ColorMethod.ByColor } c
        && c.Red == Cleanup.TriedAllColor.R && c.Green == Cleanup.TriedAllColor.G && c.Blue == Cleanup.TriedAllColor.B;

    private static void Juntar(Dictionary<ObjectId, List<Color>> mapa, ObjectId id, Color cor)
    {
        if (!mapa.TryGetValue(id, out var lista)) mapa[id] = lista = [];
        lista.Add(cor);
    }

    /// <summary>Os módulos que a última sombra pintou, com a cor de antes ("handle=cor" depois do "V1").</summary>
    private static List<(ObjectId Id, Color Antes)> PintadasDaSombra(Database database)
    {
        var achadas = new List<(ObjectId, Color)>();

        using var dados = PluginDictionary.Load(database, ChaveDasPintadasDaSombra);
        foreach (var valor in dados?.AsArray() ?? [])
        {
            if (valor.Value is not string texto || texto == "V1") continue;

            var partes = texto.Split('=', 2);
            if (partes.Length != 2 || !long.TryParse(partes[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var numero)) continue;
            if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;

            achadas.Add((id, PecasPintadas.CorDe(partes[1])));
        }

        return achadas;
    }

    /// <summary>O registro da sombra volta a só ter a versão, como o Apagar sombras deixa.</summary>
    private static void ZerarDaSombra(Database database, string chave) =>
        PluginDictionary.Save(database, chave, new ResultBuffer(new TypedValue((int)DxfCode.Text, "V1")));
}
