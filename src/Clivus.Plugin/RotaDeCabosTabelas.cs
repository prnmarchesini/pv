using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As tabelas do "Ver cabos" (17.8, 18.8, 23.4, 23.6) e do resumo (24):
/// sempre a partir do desenho de agora (regra 7). Antes de montar, reconta:
/// os lances gerados que não estão mais no desenho (apagados com Delete por
/// fora do plugin) são contados e a origem deles é pintada; e o cabo cuja
/// ponta não está mais em campo (o inversor apagado, item 18) sai da conta,
/// é pintado e avisado.
/// </summary>
internal static class RotaDeCabosTabelas
{
    /// <summary>
    /// A recontagem de uma rota: os lances do desenho com as duas pontas em
    /// campo, os sumidos (gerados e apagados à mão), os órfãos (cabo ainda
    /// desenhado cuja ponta saiu de campo) e as pontas que faltam.
    /// </summary>
    internal sealed record Recontagem(List<RotaDeCabosStore.LanceNoDesenho> Lances, List<CableRun> Sumidos, List<RotaDeCabosStore.LanceNoDesenho> Orfaos, List<CableEnd> Faltam);

    /// <summary>Reconta a rota e pinta a origem dos lances sumidos e os cabos órfãos (somando à pintura que já havia).</summary>
    internal static Recontagem Recontar(Document documento, CableRoute rota, LeituraDaRota leitura)
    {
        var db = documento.Database;
        var todos = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == rota).ToList();
        var (lances, faltam) = CableReport.InField(todos, l => l.Lance.From, l => l.Lance.To, leitura.EmCampo);
        var ficam = lances.Select(l => l.Id).ToHashSet();
        var orfaos = todos.Where(l => !ficam.Contains(l.Id)).ToList();

        // Sumido é o gerado que saiu do desenho com as pontas ainda em campo; o
        // do equipamento que também saiu não é mais esperado.
        var presentes = todos.Select(l => l.Lance.Id).ToHashSet();
        var sumidos = RotaDeCabosStore.LancesGerados(db)
            .Where(l => l.Route == rota && !presentes.Contains(l.Id) && leitura.EmCampo(l.From) && leitura.EmCampo(l.To))
            .ToList();

        if (sumidos.Count > 0 || orfaos.Count > 0)
        {
            var pintar = sumidos.SelectMany(s => s.From.Kind == CableEndKind.String ? leitura.MesasDaString(s.From.Id) : leitura.Pintaveis(s.From))
                .Concat(orfaos.Select(o => o.Id))
                .ToList();
            EscritaForaDeComando.Fazer(documento, () => RotaDeCabosStore.Pintar(db, rota, pintar, somar: true));
        }

        return new Recontagem(lances, sumidos, orfaos, faltam);
    }

    /// <summary>O nome de uma ponta para o aviso de fora de campo (a tag da string, o nome do equipamento).</summary>
    internal static Func<CableEnd, string> NomeDaPonta(LeituraDaRota leitura)
    {
        var tags = leitura.Strings().ToDictionary(x => x.String.Id, x => x.String.Tag);
        return p => Nome(leitura.Setup, tags, p);
    }

    /// <summary>
    /// O módulo (PAN) da string, pelo perfil da mesa da ponta + dela: o PAN do
    /// modelo do módulo da mesa, ou o único PAN do desenho. É o ÚNICO ponto
    /// de onde as tabelas e o resumo leem o módulo (a fonte do PAN vai passar
    /// a ser a estrutura/mesa: troca-se só aqui).
    /// </summary>
    internal static Func<string?, PanModule?> ModuloPan(Database db)
    {
        var fonte = FonteDoModulo.Ler(db);
        return perfil => fonte.ElectricalFor(perfil);
    }

    /// <summary>As tabelas de uma rota (o CC tem uma; a Combiner tem as strings e os alimentadores).</summary>
    internal static List<CableTable> Montar(Document documento, CableRoute rota, out Recontagem recontagem)
    {
        var db = documento.Database;
        var leitura = LeituraDaRota.Ler(db);
        recontagem = Recontar(documento, rota, leitura);

        var config = RotaDeCabosStore.Configuracao(db, rota);
        var projeto = SettingsStore.Load(db).Settings ?? ProjectSettings.Default;
        var titulo = Tr.F("Cabos {0}", CableRoutes.Title(rota));
        var foraDeCampo = CableReport.MissingMessage(recontagem.Faltam, NomeDaPonta(leitura), recontagem.Lances.Count == 0 && recontagem.Sumidos.Count == 0);

        List<CableTable> ComAviso(List<CableTable> tabelas)
        {
            if (foraDeCampo is not null && tabelas.Count > 0) tabelas[0] = tabelas[0] with { Notes = [foraDeCampo, .. tabelas[0].Notes] };
            return tabelas;
        }

        if (rota is CableRoute.DirectCurrent or CableRoute.Combiner)
        {
            // A fonte única do módulo (item 15): a estrutura da mesa da string;
            // com a potência trocada pela área (item 14), nenhum (cálculos desligados).
            var fonte = FonteDoModulo.Ler(db);
            var modulo = ModuloPan(db);

            var porString = recontagem.Lances.Where(l => l.Lance.From.Kind == CableEndKind.String)
                .GroupBy(l => l.Lance.From.Id)
                .ToDictionary(g => g.Key, g => g.ToList());
            var esperadas = recontagem.Sumidos.Where(s => s.From.Kind == CableEndKind.String).Select(s => s.From.Id).ToHashSet();

            var linhas = leitura.Strings()
                .Where(x => porString.ContainsKey(x.String.Id) || esperadas.Contains(x.String.Id))
                .Select(x =>
                {
                    porString.TryGetValue(x.String.Id, out var dela);
                    double? Comprimento(CablePolarity p) => dela?.Where(l => l.Lance.Polarity == p).Select(l => (double?)l.Comprimento).Sum();
                    return new DcStringRun(x.String.Id, x.String.Tag, x.String.Modules.Count, Comprimento(CablePolarity.Positive), Comprimento(CablePolarity.Negative), modulo(x.Perfil));
                })
                .ToList();

            var dc = CableReport.Dc(titulo, linhas, config.Cable, null, projeto.MinTemperature, projeto.MaxTemperature);
            // As temperaturas que entraram nas contas, sempre à vista (as de partida também são de alguém escolher).
            var tabelas = new List<CableTable>
            {
                dc with { Notes = [.. dc.Notes, Tr.F("Temperaturas usadas (Configurações): mínima {0} °C, máxima {1} °C, aplicadas como estão.", projeto.MinTemperature, projeto.MaxTemperature)] },
            };

            // Por que as tensões e correntes estão em branco, quando é pela potência trocada pela área.
            if (fonte.Simulated is { } simulada)
                tabelas[0] = tabelas[0] with { Notes = [simulada.Reason(), .. tabelas[0].Notes] };

            if (rota == CableRoute.Combiner)
            {
                var serieDe = leitura.Strings().ToDictionary(x => x.String.Id, x => (x.String.Modules.Count, modulo(x.Perfil)));
                var alimentadores = recontagem.Lances.Where(l => l.Lance.From.Kind == CableEndKind.Combiner)
                    .Select(l =>
                    {
                        var cb = l.Lance.From.Id;
                        var dela = leitura.CombinerDaString.Where(x => x.Value == cb).Select(x => x.Key).Where(serieDe.ContainsKey).ToList();
                        var (serie, pan) = dela.Count > 0 ? serieDe[dela[0]] : (0, null);
                        return new DcFeederRun(leitura.Setup.FindCombiner(cb)?.Name ?? "?", leitura.Setup.FindInverter(l.Lance.To.Id)?.Name ?? "?", l.Comprimento, dela.Count, serie, pan);
                    });
                tabelas.Add(CableReport.Feeders(Tr.T("Combiner → inversor"), alimentadores, config.Cable, projeto.MaxTemperature));
            }

            return ComAviso(tabelas);
        }

        var setup = leitura.Setup;
        var trechos = recontagem.Lances.Select(l =>
        {
            if (rota == CableRoute.AlternatingCurrent)
            {
                var inv = setup.FindInverter(l.Lance.From.Id);
                var trafo = setup.FindTransformer(l.Lance.To.Id);
                var kw = inv is null ? 0 : setup.FindModel(inv.Model)?.PowerKw ?? 0;
                var v = trafo?.InputVoltage ?? 0;
                string? falta = kw <= 0 ? Tr.T("potência do inversor não informada no modelo") : v <= 0 ? Tr.T("tensão de baixa do trafo não informada") : null;
                return new EquipmentRun(inv?.Name ?? "?", trafo?.Nickname ?? "?", l.Comprimento,
                    falta is null ? config.ThreePhase ? CableCalc.ThreePhaseCurrent(kw, v, config.PowerFactor) : CableCalc.SinglePhaseCurrent(kw, v, config.PowerFactor) : null,
                    v > 0 ? v : null, falta);
            }

            var t = setup.FindTransformer(l.Lance.From.Id);
            var (_, sub) = t is null ? (Guid.Empty, "?") : CableChain.PhysicalSubstation(setup, t.ConsumerUnit);
            var mt = t?.OutputVoltage ?? 0;
            string? faltaMt = t is null || t.PowerKva <= 0 ? Tr.T("potência do trafo não informada") : mt <= 0 ? Tr.T("tensão de média do trafo não informada") : null;
            return new EquipmentRun(t?.Nickname ?? "?", sub, l.Comprimento,
                faltaMt is null ? CableCalc.ThreePhaseCurrentKva(t!.PowerKva, mt) : null, mt > 0 ? mt : null, faltaMt);
        });

        return ComAviso([CableReport.Equipment(titulo, trechos, config.Cable, config.Method, config.PowerFactor, rota != CableRoute.AlternatingCurrent || config.ThreePhase)]);
    }

    /// <summary>
    /// Os lances medidos de todas as rotas, com o nome do cabo da aba e as
    /// vias do circuito (as trocadas no resumo, ou as da aba), para a lista
    /// de material. Com <paramref name="leitura"/>, só os de pontas em campo (item 18).
    /// </summary>
    internal static List<CableReport.MeasuredRun> Medidos(Database db, LeituraDaRota? leitura = null)
    {
        var config = RotaDeCabosStore.Configuracoes(db, out _);
        var vias = ViasPorCircuito(db, config);
        return RotaDeCabosStore.Lances(db)
            .Where(l => leitura is null || (leitura.EmCampo(l.Lance.From) && leitura.EmCampo(l.Lance.To)))
            .Select(l => new CableReport.MeasuredRun(l.Lance.Route, l.Lance.Polarity, config[l.Lance.Route].Cable?.Name ?? Tr.T("(sem cabo escolhido)"), l.Comprimento,
                vias(l.Lance.Route, l.Lance.From, l.Lance.To)))
            .ToList();
    }

    /// <summary>
    /// O aviso de quando as vias trocadas no resumo não se leem: os totais
    /// voltam às vias das abas, e o usuário precisa saber (null se tudo bem).
    /// </summary>
    internal static string? ProblemaDasVias(Database db)
    {
        RotaDeCabosStore.Vias(db, out var problema);
        return problema is null ? null : Tr.F("ATENÇÃO: as vias trocadas no resumo não se leem ({0}); os totais usam as vias das abas.", problema);
    }

    /// <summary>As vias de um circuito: as trocadas à mão no resumo; sem troca, as da aba da rota.</summary>
    private static Func<CableRoute, CableEnd, CableEnd, int> ViasPorCircuito(Database db, IReadOnlyDictionary<CableRoute, RouteSettings> config)
    {
        var trocadas = RotaDeCabosStore.Vias(db, out _).ToDictionary(v => (v.Route, v.From, v.To), v => v.Wires);
        return (rota, de, para) => trocadas.TryGetValue((rota, de, para), out var n) ? n : config[rota].Wires;
    }

    /// <summary>
    /// Os circuitos das rotas pedidas, medidos agora (regra 7): um por par de
    /// pontas (no CC, a string e o inversor, com o + e o −), com o nome das
    /// pontas, os lances, o comprimento somado, as vias, o cabo e o método da
    /// aba. Só os de pontas em campo (item 18); as que faltam voltam à parte.
    /// </summary>
    internal static (List<CableReport.CircuitRun> Circuitos, List<CableEnd> Faltam) Circuitos(Database db, LeituraDaRota leitura, IReadOnlyCollection<CableRoute> rotas)
    {
        var config = RotaDeCabosStore.Configuracoes(db, out _);
        var vias = ViasPorCircuito(db, config);
        var tags = leitura.Strings().ToDictionary(x => x.String.Id, x => x.String.Tag);

        var todos = RotaDeCabosStore.Lances(db)
            .Where(l => rotas.Contains(l.Lance.Route))
            .GroupBy(l => (l.Lance.Route, l.Lance.From, l.Lance.To))
            .Select(g => new CableReport.CircuitRun(
                g.Key.Route, g.Key.From, g.Key.To, Nome(leitura.Setup, tags, g.Key.From), Nome(leitura.Setup, tags, g.Key.To),
                g.Count(), g.Sum(l => l.Comprimento), vias(g.Key.Route, g.Key.From, g.Key.To), config[g.Key.Route].Cable, config[g.Key.Route].Method));

        return CableReport.InField(todos, c => c.From, c => c.To, leitura.EmCampo);
    }

    /// <summary>Os tipos de cabo do resumo e as rotas de cada um (as combiners são cabo CC).</summary>
    internal static readonly (string Titulo, CableRoute[] Rotas)[] TiposDeCabo =
    [
        (Tr.N("CC"), [CableRoute.DirectCurrent, CableRoute.Combiner]),
        (Tr.N("CA"), [CableRoute.AlternatingCurrent]),
        (Tr.N("MT"), [CableRoute.MediumVoltage]),
    ];

    /// <summary>
    /// O resumo de um tipo de cabo (itens 12, 16 e 18): os circuitos de pontas
    /// em campo, agrupados (UC > trafo > inversor no CC; UC > trafo no CA; UC
    /// na MT), as contas por string do CC, a tabela agrupada (a do Excel) e o
    /// aviso das pontas fora de campo.
    /// </summary>
    internal sealed record ResumoDoTipo(
        List<CableReport.CircuitRun> Circuitos, IReadOnlyList<CableReport.CircuitGroup> Grupos, Dictionary<CableEnd, StringCheck> Contas, CableTable Tabela, string? ForaDeCampo);

    /// <summary>O resumo de um tipo de cabo, medido agora.</summary>
    internal static ResumoDoTipo Resumo(Database db, LeituraDaRota leitura, int tipo)
    {
        var (titulo, rotas) = TiposDeCabo[tipo];
        var (circuitos, faltamDosCabos) = Circuitos(db, leitura, rotas);

        // Item 18 inteiro: o equipamento que devia ter cabo e saiu de campo junto com
        // os cabos também é avisado; o automático ainda não posto, com aviso próprio.
        var automaticos = LocalDosInversores.Ler(db, out _).Where(l => l.Mode == InverterPlacementMode.Automatic).Select(l => l.Inverter).ToHashSet();
        var (faltam, pendentes) = CableReport.Expected(faltamDosCabos, Esperadas(leitura, rotas), leitura.EmCampo, automaticos);
        var nome = NomeDaPonta(leitura);
        var avisos = new[] { CableReport.MissingMessage(faltam, nome, circuitos.Count == 0), CableReport.NotYetPlacedMessage(pendentes, nome) }.OfType<string>().ToList();
        var foraDeCampo = avisos.Count == 0 ? null : string.Join(" ", avisos);
        var grupos = CableReport.Group(circuitos, c => CableReport.GroupPath(leitura.Setup, c));

        var dc = rotas.Contains(CableRoute.DirectCurrent);
        var contas = new Dictionary<CableEnd, StringCheck>();
        var notas = new List<string>();
        if (foraDeCampo is not null) notas.Add(foraDeCampo);

        if (dc)
        {
            var projeto = SettingsStore.Load(db).Settings ?? ProjectSettings.Default;
            var modulo = ModuloPan(db);
            var dasStrings = leitura.Strings().ToDictionary(x => x.String.Id);
            var semPan = 0;
            foreach (var c in circuitos.Where(c => c.From.Kind == CableEndKind.String))
            {
                if (!dasStrings.TryGetValue(c.From.Id, out var s)) continue;
                if (StringCheck.For(modulo(s.Perfil), s.String.Modules.Count, projeto.MinTemperature, c.Cable, c.Method) is { } conta) contas[c.From] = conta;
                else semPan++;
            }

            // Com a potência trocada pela área (item 14) o motivo é ela, não falta de PAN.
            if (FonteDoModulo.Simulada(db) is { } simulada) notas.Add(simulada.Reason());
            else if (semPan > 0)
                notas.Add(Tr.F("{0} string(s) sem módulo com PAN: as colunas de cálculo ficam vazias (carregue o .PAN na estrutura, em Configurações > Estruturas > Editar).", semPan));
            if (contas.Values.Any(k => k.Ampacity is null))
            {
                var semMaxima = string.Join(", ", circuitos.Where(c => contas.TryGetValue(c.From, out var k) && k.Ampacity is null)
                    .Select(c => Tr.F("{0} no método {1}", c.Cable?.Name ?? Tr.T("(sem cabo escolhido)"), string.IsNullOrWhiteSpace(c.Method) ? "?" : c.Method)).Distinct());
                notas.Add(Tr.F("A biblioteca de cabos não tem a corrente máxima de {0}: a corrente máxima, a corrigida e o Suporta ficam vazios (informe a corrente desse método no cabo, no botão Biblioteca de cabos... da aba).", semMaxima));
            }

            notas.Add(Tr.F("Cálculo simples: corrente corrigida = corrente máxima do cabo no método da aba × fator de correção ({0:0.00}, por enquanto sem agrupamento nem temperatura); suporta se Isc × {1:0.00} ≤ corrente corrigida. Voc na mínima de {2} °C (Configurações).",
                StringCheck.DefaultCorrectionFactor, StringCheck.SafetyFactor, projeto.MinTemperature));
        }

        var tabela = CableReport.GroupedCircuits(Tr.F("Resumo de cabos {0}", Tr.T(titulo)), grupos, dc, rotas.Length > 1, contas, notas);
        return new ResumoDoTipo(circuitos, grupos, contas, tabela, foraDeCampo);
    }

    /// <summary>
    /// Quem devia estar em campo para as rotas do tipo: no CC, os inversores
    /// e as combiners com strings alocadas; no CA e na MT, as pontas da cadeia
    /// de vínculo (inversor no trafo dele, trafo na subestação da UC).
    /// </summary>
    private static IEnumerable<CableEnd> Esperadas(LeituraDaRota leitura, IReadOnlyCollection<CableRoute> rotas)
    {
        if (rotas.Contains(CableRoute.DirectCurrent))
        {
            foreach (var (s, _) in leitura.Strings())
                if (leitura.Setup.FindInverter(s.Inverter) is not null) yield return new CableEnd(CableEndKind.Inverter, s.Inverter);
        }

        if (rotas.Contains(CableRoute.Combiner))
        {
            foreach (var cb in leitura.CombinerDaString.Values.Distinct().Where(c => leitura.Setup.FindCombiner(c) is not null))
                yield return new CableEnd(CableEndKind.Combiner, cb);
        }

        var cadeias = new List<ChainLink>();
        if (rotas.Contains(CableRoute.AlternatingCurrent)) cadeias.AddRange(CableChain.AlternatingCurrent(leitura.Setup));
        if (rotas.Contains(CableRoute.MediumVoltage)) cadeias.AddRange(CableChain.MediumVoltage(leitura.Setup));
        foreach (var l in cadeias.Where(l => l.From.Id != Guid.Empty && l.To.Id != Guid.Empty))
        {
            yield return l.From;
            yield return l.To;
        }
    }

    /// <summary>O resumo da usina inteira, como a aba Resumo monta: as recontagens de todas as rotas, os resumos por tipo de cabo e os lances medidos.</summary>
    internal sealed record ResumoDaUsina(List<Recontagem> Recontagens, List<ResumoDoTipo> Tipos, List<CableReport.MeasuredRun> Medidos)
    {
        internal int Sumidos => Recontagens.Sum(r => r.Sumidos.Count);

        internal int Orfaos => Recontagens.Sum(r => r.Orfaos.Count);
    }

    /// <summary>
    /// O "Atualizar (reconta)" da aba Resumo (e o comando de teste do nível
    /// 2, pelo mesmo caminho): reconta todas as rotas (pinta os sumidos e os
    /// órfãos) e monta o resumo de cada tipo de cabo com o desenho de agora.
    /// </summary>
    internal static ResumoDaUsina Usina(Document documento)
    {
        var db = documento.Database;
        var leitura = LeituraDaRota.Ler(db);
        var recontagens = CableRoutes.All.Select(r => Recontar(documento, r, leitura)).ToList();
        var tipos = Enumerable.Range(0, TiposDeCabo.Length).Select(i => Resumo(db, leitura, i)).ToList();
        return new ResumoDaUsina(recontagens, tipos, Medidos(db, leitura));
    }

    /// <summary>O nome de uma ponta para a tela: a tag da string, o nome do equipamento, a UC ou o bloco.</summary>
    internal static string Nome(ElectricalSetup setup, IReadOnlyDictionary<Guid, string> tags, CableEnd ponta) => ponta.Kind switch
    {
        CableEndKind.String => tags.TryGetValue(ponta.Id, out var tag) && !string.IsNullOrWhiteSpace(tag) ? tag : Tr.F("string {0}", ponta.Id.ToString("D")[..8]),
        CableEndKind.Combiner => setup.FindCombiner(ponta.Id)?.Name ?? "?",
        CableEndKind.Inverter => setup.FindInverter(ponta.Id)?.Name ?? "?",
        CableEndKind.Transformer => setup.FindTransformer(ponta.Id)?.Nickname ?? "?",
        _ => setup.FindUnit(ponta.Id) is { } uc
            ? string.IsNullOrWhiteSpace(uc.Name) ? uc.Code : uc.Name
            : setup.FindSubstation(ponta.Id)?.Name ?? "?",
    };

    /// <summary>Grava as tabelas num CSV (uma depois da outra, com o título), UTF-8 com BOM para o Excel abrir com acento.</summary>
    internal static void GravarCsv(string caminho, IEnumerable<CableTable> tabelas)
    {
        var texto = string.Join("\r\n", tabelas.Select(t => t.Title + "\r\n" + t.ToCsv(Tr.Culture)));
        System.IO.File.WriteAllText(caminho, texto, new System.Text.UTF8Encoding(true));
    }
}
