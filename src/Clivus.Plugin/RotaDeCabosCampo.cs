using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.RotaDeCabosCampo))]

namespace Clivus.Plugin;

/// <summary>
/// Os comandos de campo da rota de cabos, chamados pelos botões da janela
/// (que se esconde e volta no fim) e também digitáveis: selecionar vala
/// (17.4), gerar (18.6, 20.4, 21.4), apagar (17.9) e forçar lado (18.3). A
/// decisão do caminho é do Core (<see cref="CableRouter"/>); aqui se lê o
/// desenho, se desenha e se pinta o que não deu (regra 4).
/// </summary>
public static class RotaDeCabosCampo
{
    /// <summary>O último resultado de cada rota, para a janela mostrar quando voltar.</summary>
    internal static readonly Dictionary<(Document, CableRoute), string> Relatorios = [];

    // ------------------------------------------------------------- 17.4 vala

    [CommandMethod(PluginInfo.ComandoRotaVala)]
    public static void Vala()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;

            var escolhidas = SelecionarCurvas(documento, rota);
            if (escolhidas is null) return;

            var db = documento.Database;
            var n = RotaDeCabosStore.MarcarValas(db, escolhidas, rota);
            var linhas = new List<string>();

            // A vala acompanha o terreno na profundidade da aba (regra universal: todo desenho respeita o TIN).
            if (FileiraCommands.ExigirTerreno(editor, documento) is { } terreno)
            {
                var profundidade = RotaDeCabosStore.Configuracao(db, rota).Depth;
                var (assentadas, fora) = RotaDeCabosStore.AssentarValas(db, rota, profundidade, terreno.Mesh, escolhidas);
                linhas.Add(Tr.F("{0} linha(s) viraram vala {1}, na camada {2}, assentadas no terreno a {3:0.00} m de profundidade.", assentadas, CableRoutes.Title(rota), CableLayers.Trench(rota), profundidade));
                if (fora > 0) linhas.Add(Tr.F("ATENÇÃO: {0} ponto(s) da vala fora do terreno ficaram com a cota que tinham.", fora));
            }
            else
            {
                linhas.Add(Tr.F("{0} linha(s) viraram vala {1}, na camada {2}, mas sem terreno processado não foram assentadas: o Gerar assenta.", n, CableRoutes.Title(rota), CableLayers.Trench(rota)));
            }

            linhas.Add(Tr.F("A rota tem {0} vala(s).", RotaDeCabosStore.QuantasValas(db)[rota]));
            Relatar(documento, rota, string.Join("\n", linhas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar a vala.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui selecionar a vala: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

    /// <summary>
    /// A seleção das polilinhas da vala: só curvas soltas do usuário (as do
    /// plugin, como string e contorno de mesa, ficam fora), com a contagem ao
    /// vivo. Shift+clique tira da seleção (o padrão do AutoCAD).
    /// </summary>
    private static List<ObjectId>? SelecionarCurvas(Document documento, CableRoute rota)
    {
        var editor = documento.Editor;
        var escolhidas = new HashSet<ObjectId>();
        CaixaDeSelecao? placar = null;

        bool Serve(ObjectId id)
        {
            using var t = documento.Database.TransactionManager.StartOpenCloseTransaction();
            if (t.GetObject(id, OpenMode.ForRead) is not Curve c) return false;
            // Do plugin, só a vala (que pode mudar de rota); string, mesa e cabo não.
            using var xdata = c.GetXDataForApplication(PluginInfo.PrefixoDeDados);
            return xdata is null || RotaDeCabosStore.Vala(c) is not null;
        }

        void Atualizar()
        {
            try
            {
                if (!ClivusExtension.TemInterface()) return;
                placar ??= AlocacaoDeStringsCommands.NovoPlacar(520);
                placar.TextoLivre = Tr.F("Vala {0}: {1} linha(s) selecionada(s)", CableRoutes.Title(rota), escolhidas.Count);
                if (!placar.IsVisible) placar.Show();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha no placar da seleção de vala.", erro);
            }
        }

        void Somou(object? _, SelectionAddedEventArgs e)
        {
            try
            {
                var ids = e.AddedObjects.GetObjectIds();
                for (var i = ids.Length - 1; i >= 0; i--)
                {
                    if (Serve(ids[i])) escolhidas.Add(ids[i]);
                    else e.Remove(i);
                }
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao conferir a seleção de vala.", erro);
            }

            Atualizar();
        }

        void Tirou(object? _, SelectionRemovedEventArgs e)
        {
            try
            {
                foreach (var id in e.RemovedObjects.GetObjectIds()) escolhidas.Remove(id);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao tirar linha da seleção de vala.", erro);
            }

            Atualizar();
        }

        var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE,LINE")]);
        editor.SelectionAdded += Somou;
        editor.SelectionRemoved += Tirou;

        try
        {
            Atualizar();
            var r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nLinhas da vala {0} (Shift+clique tira; Enter termina): ", CableRoutes.Title(rota)) }, filtro);
            if (r.Status != PromptStatus.OK) return null;
            return r.Value.GetObjectIds().Where(Serve).ToList();
        }
        finally
        {
            editor.SelectionAdded -= Somou;
            editor.SelectionRemoved -= Tirou;
            placar?.Close();
        }
    }

    // ------------------------------------------------------------- gerar

    /// <summary>
    /// O Gerar da aba (e o refazer depois de mover um equipamento): apaga os
    /// cabos da rota e desenha de novo, com os equipamentos onde estão. No
    /// CC, os inversores automáticos que ainda não estão em campo são postos
    /// ao lado da vala, no ponto de menor cabo das strings deles, antes.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaGerar)]
    public static void Gerar() => Gerar(recolocar: false);

    /// <summary>
    /// "Recolocar automáticos" (aba CC): TODOS os inversores automáticos
    /// voltam ao ponto de menor cabo (mesmo os que o usuário moveu) e a rota
    /// CC é refeita.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaRecolocar)]
    public static void Recolocar() => Gerar(recolocar: true);

    private static void Gerar(bool recolocar)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;
            if (recolocar && rota != CableRoute.DirectCurrent)
            {
                editor.WriteMessage(Tr.T("\nROTA Recolocar os inversores automáticos é da rota CC.\n"));
                return;
            }

            if (FileiraCommands.ExigirTerreno(editor, documento) is not { } terreno) return;

            var db = documento.Database;
            var config = RotaDeCabosStore.Configuracoes(db, out var problemaDaConfig)[rota];
            if (problemaDaConfig is not null)
            {
                Relatar(documento, rota, Tr.F("Não gerei: a configuração da rota está ilegível ({0}).", problemaDaConfig));
                return;
            }

            RotaDeCabosStore.LancesGerados(db, out var problemaDosLances);
            if (problemaDosLances is not null)
            {
                // Regravar por cima apagaria os esperados das outras rotas sem aviso.
                Relatar(documento, rota, Tr.F("Não gerei: o registro dos lances gerados está ilegível ({0}).", problemaDosLances));
                return;
            }

            // A vala acompanha o terreno na profundidade de agora (ela pode ter mudado, ou a vala ter sido editada).
            var (_, valaFora) = RotaDeCabosStore.AssentarValas(db, rota, config.Depth, terreno.Mesh);

            var valas = RotaDeCabosStore.Valas(db)[rota];
            if (valas.Count == 0)
            {
                Relatar(documento, rota, Tr.F("Não gerei: nenhuma vala {0} selecionada. Use \"Selecionar vala\" primeiro.", CableRoutes.Title(rota)));
                return;
            }

            var rede = new TrenchNetwork(valas);
            double? Chao(double x, double y) => terreno.Mesh.TryGetZ(x, y, out var z) ? z : null;

            var linhas = new List<string>();
            var leitura = LeituraDaRota.Ler(db);

            // CC: os inversores automáticos vão para o lado da vala antes de traçar; a
            // leitura é refeita com eles em campo (e sem as falhas que a conta já anotou).
            if (rota == CableRoute.DirectCurrent)
            {
                linhas.AddRange(PosicaoAutomatica.Colocar(editor, db, terreno, rede, config, leitura, recolocar));
                leitura = LeituraDaRota.Ler(db);
            }

            var resultado = rota switch
            {
                CableRoute.DirectCurrent => CableRouter.Strings(leitura.StringsCc(), rota, rede, config, Chao),
                CableRoute.AlternatingCurrent => CableRouter.Equipment(leitura.Trechos(CableChain.AlternatingCurrent(leitura.Setup)), rota, rede, config, Chao),
                CableRoute.MediumVoltage => CableRouter.Equipment(leitura.Trechos(CableChain.MediumVoltage(leitura.Setup)), rota, rede, config, Chao),
                _ => CombinerDaRota.Rotear(leitura, rede, config, Chao),
            };

            var falhas = leitura.Falhas.Where(f => f.Route == rota).Select(f => f.Falha).Concat(resultado.Failures).ToList();

            // Refaz a rota inteira: apaga só os cabos dela, desenha os novos.
            var antigos = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == rota).Select(l => l.Id).ToList();
            RotaDeCabosStore.Apagar(db, antigos);
            RotaDeCabosStore.Desenhar(db, resultado.Runs);

            var gerados = RotaDeCabosStore.LancesGerados(db).Where(l => l.Route != rota).Concat(resultado.Runs.Select(r => r.Run)).ToList();
            RotaDeCabosStore.GravarLancesGerados(db, gerados);

            RotaDeCabosStore.Pintar(db, rota, leitura.ParaPintar(falhas));

            linhas.Insert(0, Tr.F("{0}: {1} lance(s) desenhado(s), {2:0.0} m no total (3D, com as descidas da vala de {3:0.00} m).",
                CableRoutes.Title(rota), resultado.Runs.Count, resultado.Runs.Sum(r => r.Length), config.Depth));
            if (valaFora > 0) linhas.Add(Tr.F("ATENÇÃO: {0} ponto(s) da vala fora do terreno ficaram com a cota que tinham.", valaFora));
            if (leitura.ProblemaDoLocal is { } local && rota != CableRoute.MediumVoltage)
                linhas.Add(Tr.F("ATENÇÃO: o local dos inversores não se lê ({0}); a área deles não valeu como contorno.", local));
            if (falhas.Count > 0) linhas.Add(Tr.F("{0} trecho(s) sem rota, pintado(s) de vermelho:", falhas.Count));
            linhas.AddRange(falhas.Select(f => "• " + f.What + ": " + f.Reason));
            Relatar(documento, rota, string.Join("\n", linhas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gerar a rota de cabos.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar os cabos: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

    // ------------------------------------------------------------- 17.9 apagar

    [CommandMethod(PluginInfo.ComandoRotaApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;

            var opcoes = new PromptKeywordOptions(Tr.T("\nApagar os cabos [Tudo/Selecionar]: ")) { AllowNone = false };
            opcoes.Keywords.Add("Tudo");
            opcoes.Keywords.Add("Selecionar");
            var qual = editor.GetKeywords(opcoes);
            if (qual.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var daRota = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == rota).ToDictionary(l => l.Id, l => l.Lance);
            IEnumerable<ObjectId> alvo = daRota.Keys;

            if (qual.StringResult == "Selecionar")
            {
                var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE"), new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginInfo.PrefixoDeDados)]);
                var r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nCabos {0} a apagar (só os desta rota contam): ", CableRoutes.Title(rota)) }, filtro);
                if (r.Status != PromptStatus.OK) return;
                alvo = r.Value.GetObjectIds().Where(daRota.ContainsKey).ToList();
            }

            var ids = alvo.ToList();
            var apagados = RotaDeCabosStore.Apagar(db, ids);

            // Apagado pelo botão não é "sumido": sai também dos esperados da recontagem
            // (no Tudo, todos os da rota, até os que já tinham sumido à mão).
            var sairam = ids.Select(id => daRota[id].Id).ToHashSet();
            var tudo = qual.StringResult == "Tudo";
            RotaDeCabosStore.GravarLancesGerados(db, RotaDeCabosStore.LancesGerados(db).Where(l => !sairam.Contains(l.Id) && !(tudo && l.Route == rota)).ToList());
            if (qual.StringResult == "Tudo") RotaDeCabosStore.Despintar(db, rota);

            Relatar(documento, rota, Tr.F("{0} cabo(s) {1} apagado(s). Strings, valas e mesas não foram tocadas.", apagados, CableRoutes.Title(rota)));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar cabos.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar os cabos: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

    // ------------------------------------------------------------- 18.3 lado

    [CommandMethod(PluginInfo.ComandoRotaLado)]
    public static void Lado()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;

            var db = documento.Database;
            var strings = StringsDoDesenho.Ler(db);
            var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "POLYLINE"), new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginInfo.PrefixoDeDados)]);
            var r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nStrings que vão para um lado (Enter termina): ") }, filtro);
            if (r.Status != PromptStatus.OK) return;

            var escolhidas = r.Value.GetObjectIds().Where(strings.ContainsKey).ToList();
            if (escolhidas.Count == 0)
            {
                Relatar(documento, rota, Tr.T("Nenhuma string na seleção."));
                return;
            }

            var opcoes = new PromptPointOptions(Tr.T("\nClique do lado (ponta da fileira) para onde os cabos vão, ou [Automatico]: "));
            opcoes.Keywords.Add("Automatico");
            var ponto = editor.GetPoint(opcoes);
            if (ponto.Status is not (PromptStatus.OK or PromptStatus.Keyword)) return;

            var lados = RotaDeCabosStore.Lados(db);
            var guids = escolhidas.Select(id => strings[id].Id).ToHashSet();
            lados.RemoveAll(l => guids.Contains(l.String));

            if (ponto.Status == PromptStatus.OK)
            {
                var mundo = ponto.Value.TransformBy(editor.CurrentUserCoordinateSystem);
                var leitura = LeituraDaRota.Ler(db);
                foreach (var s in escolhidas.Select(id => strings[id]))
                    if (leitura.PontaMaisPerto(s, new Point3(mundo.X, mundo.Y, 0)) is { } ponta) lados.Add(new StringSide(s.Id, ponta));
            }

            RotaDeCabosStore.GravarLados(db, lados);
            Relatar(documento, rota, ponto.Status == PromptStatus.OK
                ? Tr.F("{0} string(s) com o lado forçado; o Gerar respeita (e não volta ao automático).", escolhidas.Count)
                : Tr.F("{0} string(s) de volta ao lado automático (o mais curto).", escolhidas.Count));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao forçar o lado das strings.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui forçar o lado: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

    // ------------------------------------------------------------- comum

    /// <summary>A rota da primeira pergunta (o nome do enum, que a janela manda; ou CC, Combiner, CA, MT digitado).</summary>
    private static CableRoute? PerguntarRota(Editor editor)
    {
        var r = editor.GetString(new PromptStringOptions(Tr.T("\nRota (CC, Combiner, CA ou MT): ")) { AllowSpaces = false });
        if (r.Status != PromptStatus.OK) return null;

        var texto = r.StringResult.Trim();
        if (Enum.TryParse<CableRoute>(texto, true, out var rota) && Enum.IsDefined(rota)) return rota;
        foreach (var x in CableRoutes.All)
            if (string.Equals(CableLayers.Code(x), texto, StringComparison.OrdinalIgnoreCase) || string.Equals(CableRoutes.Title(x), texto, StringComparison.OrdinalIgnoreCase))
                return x;

        editor.WriteMessage(Tr.F("\nRota desconhecida: {0}.\n", texto));
        return null;
    }

    /// <summary>Escreve na linha de comando e guarda para a janela mostrar.</summary>
    private static void Relatar(Document documento, CableRoute rota, string texto)
    {
        documento.Editor.WriteMessage("\nROTA " + texto.Replace("\n", "\n  ") + "\n");
        Relatorios[(documento, rota)] = texto;
    }
}

/// <summary>Uma falha que a leitura do desenho já achou (string sem módulo no desenho, mesa sem contorno...).</summary>
internal sealed record FalhaDaLeitura(CableRoute Route, RouteFailure Falha);

/// <summary>
/// O que o roteamento precisa do desenho, numa leitura só: o cadastro, as
/// strings (com as pontas e as mesas), as mesas (com os cantos e o
/// letreiro) e os equipamentos em campo. Só lê; quem escreve é o comando.
/// </summary>
internal sealed class LeituraDaRota
{
    internal ElectricalSetup Setup { get; private init; } = new();
    internal List<FalhaDaLeitura> Falhas { get; } = [];

    private readonly List<(ObjectId Id, ElectricalString String, Point3 Mais, Point3 Menos)> _strings = [];
    private readonly Dictionary<Guid, Guid> _mesaDoModulo = [];
    private readonly Dictionary<Guid, (RowTable Mesa, ObjectId Contorno, string? Perfil)> _mesas = [];
    private readonly Dictionary<(EquipmentKind, Guid), (Point3 Ponto, ObjectId Id)> _emCampo = [];

    // O contorno em planta de cada equipamento em campo (os limites do bloco): a vala que entra nele vale antes do raio.
    private readonly Dictionary<(EquipmentKind, Guid), IReadOnlyList<Point3>> _contornos = [];

    // A caixa 3D dentro do bloco de cada equipamento: pintar a referência não aparece
    // (a caixa tem cor própria), e cada equipamento tem a sua definição de bloco.
    private readonly Dictionary<(EquipmentKind, Guid), List<ObjectId>> _caixas = [];
    private List<StringSide> _lados = [];
    private Dictionary<Guid, Guid> _combinerDaString = [];

    /// <summary>A combiner de cada string alocada (19.2).</summary>
    internal IReadOnlyDictionary<Guid, Guid> CombinerDaString => _combinerDaString;

    internal static LeituraDaRota Ler(Database db)
    {
        var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
        var l = new LeituraDaRota
        {
            Setup = setup,
            _lados = RotaDeCabosStore.Lados(db),
            _combinerDaString = ElectricalStore.CombinerStrings(db).Items.GroupBy(x => x.String).ToDictionary(g => g.Key, g => g.First().Combiner),
        };
        var combinerEmMapa = l._combinerDaString;

        using var t = db.TransactionManager.StartOpenCloseTransaction();

        foreach (var (mesa, partes) in LayoutScan.Tables(t, db))
        {
            if (partes.Identity is not { } identidade || partes.Contour is not { } contorno) continue;
            if (t.GetObject(contorno, OpenMode.ForRead) is not Polyline3d linha) continue;
            var cantos = FileiraCommands.Vertices(linha, t).Take(4).ToList();
            if (cantos.Count == 4) l._mesas[mesa] = (new RowTable(mesa, identidade.Label, cantos), contorno, identidade.ProfileName);
        }

        foreach (var (modulo, lugar) in NumeracaoDesenho.Modulos(t, db)) l._mesaDoModulo[modulo] = lugar.Mesa;

        foreach (var (id, s) in ElectricalStore.Strings(t, db))
        {
            if (t.GetObject(id, OpenMode.ForRead) is not Polyline3d linha) continue;
            l._strings.Add((id, s, P(linha.StartPoint), P(linha.EndPoint)));

            // A cadeia inversor -> combiner -> string tem que fechar: string realocada
            // num inversor depois de entrar na combiner (ou combiner sem inversor) é avisada.
            if (combinerEmMapa.TryGetValue(s.Id, out var cb) && setup.FindCombiner(cb) is var c && (c is null || c.Inverter == Guid.Empty || c.Inverter != s.Inverter))
                l.Falhas.Add(new FalhaDaLeitura(CableRoute.Combiner, new RouteFailure(Tr.F("string {0}", s.Tag),
                    c is null ? Tr.T("está numa combiner que não existe mais: refaça a alocação")
                    : c.Inverter == Guid.Empty ? Tr.F("está na {0}, que não tem inversor", c.Name)
                    : Tr.F("está na {0}, mas liga em outro inversor: refaça a alocação", c.Name),
                    [new CableEnd(CableEndKind.String, s.Id)], [])));
        }

        foreach (var ((tipo, guid), ids) in EquipamentoEmCampo.Posicionados(t, db))
        {
            if (ids.Count == 0 || t.GetObject(ids[0], OpenMode.ForRead) is not BlockReference b) continue;
            l._emCampo[(tipo, guid)] = (P(b.Position), ids[0]);
            var definicao = (BlockTableRecord)t.GetObject(b.BlockTableRecord, OpenMode.ForRead);
            l._caixas[(tipo, guid)] = definicao.Cast<ObjectId>().Where(id => id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Solid3d)))).ToList();
            if (Contorno(t, b, l._caixas[(tipo, guid)]) is { } contorno) l._contornos[(tipo, guid)] = l.Contornos[(tipo, guid)] = contorno;
        }

        // Inversor numa área (sala, skid): a vala que entra na ÁREA é a que chega nele.
        var areas = LocalDosInversores.Areas(db);
        var locais = LocalDosInversores.Ler(db, out var problemaDoLocal);
        l.ProblemaDoLocal = problemaDoLocal;
        foreach (var local in locais)
            if (local.Mode == InverterPlacementMode.Area && areas.TryGetValue(local.Site, out var area) && l._emCampo.ContainsKey((EquipmentKind.Inverter, local.Inverter)))
                l._contornos[(EquipmentKind.Inverter, local.Inverter)] = area.Contorno;

        return l;
    }

    /// <summary>
    /// O que um inversor posto pelo plugin não pode cobrir: as mesas e as
    /// caixas dos equipamentos em campo (menos as de <paramref name="exceto"/>,
    /// os que estão sendo postos agora).
    /// </summary>
    internal List<IReadOnlyList<Point3>> Obstaculos(IReadOnlySet<Guid> exceto)
    {
        var lista = _mesas.Values.Select(m => m.Mesa.Corners).ToList();
        foreach (var ((tipo, id), caixas) in _caixas)
            if (!exceto.Contains(id) && caixas.Count > 0 && Contornos.TryGetValue((tipo, id), out var c)) lista.Add(c);
        return lista;
    }

    /// <summary>As caixas dos equipamentos em campo (sem a troca pela área dos inversores).</summary>
    private Dictionary<(EquipmentKind, Guid), IReadOnlyList<Point3>> Contornos { get; } = [];

    /// <summary>O local dos inversores não se leu: a área deles não vale como contorno (avisado no Gerar).</summary>
    internal string? ProblemaDoLocal { get; private set; }

    private static Point3 P(Point3d p) => new(p.X, p.Y, p.Z);

    /// <summary>
    /// O retângulo em planta do equipamento: os limites da caixa 3D do bloco
    /// (sem a tag, que fica fora dela), levados para o desenho pela posição e
    /// a rotação do bloco. Null se não há caixa legível.
    /// </summary>
    private static IReadOnlyList<Point3>? Contorno(Transaction t, BlockReference b, IReadOnlyList<ObjectId> caixas)
    {
        try
        {
            Extents3d? limites = null;
            foreach (var id in caixas)
            {
                if (t.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                var x = e.GeometricExtents;
                limites = limites is { } m ? new Extents3d(
                    new Point3d(Math.Min(m.MinPoint.X, x.MinPoint.X), Math.Min(m.MinPoint.Y, x.MinPoint.Y), 0),
                    new Point3d(Math.Max(m.MaxPoint.X, x.MaxPoint.X), Math.Max(m.MaxPoint.Y, x.MaxPoint.Y), 0)) : x;
            }

            if (limites is not { } c) return null;
            return new[]
                {
                    new Point3d(c.MinPoint.X, c.MinPoint.Y, 0), new Point3d(c.MaxPoint.X, c.MinPoint.Y, 0),
                    new Point3d(c.MaxPoint.X, c.MaxPoint.Y, 0), new Point3d(c.MinPoint.X, c.MaxPoint.Y, 0),
                }
                .Select(p => p.TransformBy(b.BlockTransform))
                .Select(p => new Point3(p.X, p.Y, 0))
                .ToList();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Caixa do equipamento sem limites; a vala que entra nele não é procurada (vale o raio).", erro);
            return null;
        }
    }

    /// <summary>As strings para o CC: as pontas, as mesas e o inversor de destino. As que estão numa combiner vão pela aba Combiner.</summary>
    internal List<StringRouteInput> StringsCc() =>
        _strings.Where(x => !_combinerDaString.ContainsKey(x.String.Id)).Select(x => Entrada(x.String, x.Mais, x.Menos, new CableEnd(CableEndKind.Inverter, x.String.Inverter),
                Setup.FindInverter(x.String.Inverter)?.Name ?? "?", Ponto(EquipmentKind.Inverter, x.String.Inverter), CableRoute.DirectCurrent, Contorno(EquipmentKind.Inverter, x.String.Inverter)))
            .OfType<StringRouteInput>()
            .ToList();

    /// <summary>A entrada de uma string; null (e a falha anotada) se as mesas das pontas não estão no desenho.</summary>
    internal StringRouteInput? Entrada(ElectricalString s, Point3 mais, Point3 menos, CableEnd destino, string nomeDoDestino, Point3? ondeDestino, CableRoute rota, IReadOnlyList<Point3>? contornoDoDestino)
    {
        var a = Ponta(s.Modules[0], mais);
        var b = Ponta(s.Modules[^1], menos);
        if (a is null || b is null)
        {
            Falhas.Add(new FalhaDaLeitura(rota, new RouteFailure(Tr.F("string {0}", s.Tag), Tr.T("a mesa de uma das pontas não está no desenho (mesa recalculada ou apagada)"),
                [new CableEnd(CableEndKind.String, s.Id)], [])));
            return null;
        }

        var lado = _lados.FirstOrDefault(x => x.String == s.Id)?.End;
        return new StringRouteInput(s.Id, s.Tag, a, b, destino, nomeDoDestino, ondeDestino, lado, contornoDoDestino);
    }

    private StringEndInput? Ponta(Guid modulo, Point3 ponto)
    {
        if (!_mesaDoModulo.TryGetValue(modulo, out var mesa) || !_mesas.TryGetValue(mesa, out var m)) return null;
        return new StringEndInput(ponto, mesa, m.Mesa.Corners, CableRows.Corners(m.Mesa, _mesas.Values.Select(v => v.Mesa)));
    }

    /// <summary>Os trechos entre equipamentos com os pontos em campo.</summary>
    internal List<EquipmentRouteInput> Trechos(IEnumerable<ChainLink> ligacoes) =>
        ligacoes.Select(l => new EquipmentRouteInput(l.From, l.FromName, Ponto(Tipo(l.From.Kind), l.From.Id), l.To, l.ToName, Ponto(Tipo(l.To.Kind), l.To.Id),
            Contorno(Tipo(l.From.Kind), l.From.Id), Contorno(Tipo(l.To.Kind), l.To.Id))).ToList();

    internal Point3? Ponto(EquipmentKind tipo, Guid id) => _emCampo.TryGetValue((tipo, id), out var e) ? e.Ponto : null;

    internal IReadOnlyList<Point3>? Contorno(EquipmentKind tipo, Guid id) => _contornos.TryGetValue((tipo, id), out var c) ? c : null;

    internal static EquipmentKind Tipo(CableEndKind k) => k switch
    {
        CableEndKind.Transformer => EquipmentKind.Transformer,
        CableEndKind.Substation => EquipmentKind.ConsumerUnit,
        CableEndKind.Combiner => EquipmentKind.Combiner,
        _ => EquipmentKind.Inverter,
    };

    /// <summary>Para que ponta da fileira um clique manda a string (18.3): a ponta da fileira mais perto do clique.</summary>
    internal RowEnd? PontaMaisPerto(ElectricalString s, Point3 clique)
    {
        var x = _strings.FirstOrDefault(y => y.String.Id == s.Id);
        if (x.String is null || Ponta(s.Modules[0], x.Mais) is not { } p) return null;

        var inicio = RowExit.Plan(p.Point, p.TableCorners, p.RowCorners, RowEnd.Start).Points[^1];
        var fim = RowExit.Plan(p.Point, p.TableCorners, p.RowCorners, RowEnd.End).Points[^1];
        double D(Point3 a) => (a.X - clique.X) * (a.X - clique.X) + (a.Y - clique.Y) * (a.Y - clique.Y);
        return D(inicio) <= D(fim) ? RowEnd.Start : RowEnd.End;
    }

    /// <summary>As entidades a pintar das falhas: as mesas, as strings e os retângulos dos equipamentos.</summary>
    internal List<ObjectId> ParaPintar(IEnumerable<RouteFailure> falhas)
    {
        var ids = new List<ObjectId>();
        foreach (var f in falhas)
        {
            foreach (var mesa in f.PaintTables)
                if (_mesas.TryGetValue(mesa, out var m)) ids.Add(m.Contorno);

            foreach (var ponta in f.Paint)
            {
                if (ponta.Kind == CableEndKind.String)
                {
                    var s = _strings.FirstOrDefault(x => x.String.Id == ponta.Id);
                    if (s.String is not null) ids.Add(s.Id);
                }
                else
                {
                    ids.AddRange(Pintaveis(ponta));
                }
            }
        }

        return ids;
    }

    /// <summary>As mesas das pontas de uma string (para pintar o que sumiu na recontagem).</summary>
    internal IEnumerable<ObjectId> MesasDaString(Guid s)
    {
        var x = _strings.FirstOrDefault(y => y.String.Id == s);
        if (x.String is null) yield break;
        foreach (var m in new[] { x.String.Modules[0], x.String.Modules[^1] }.Distinct())
            if (_mesaDoModulo.TryGetValue(m, out var mesa) && _mesas.TryGetValue(mesa, out var t)) yield return t.Contorno;
    }

    /// <summary>As strings do desenho (GUID, tag, módulos em série) e o perfil da mesa da ponta + (para achar o módulo do PAN).</summary>
    internal IEnumerable<(ElectricalString String, string? Perfil)> Strings() =>
        _strings.Select(x => (x.String, _mesaDoModulo.TryGetValue(x.String.Modules[0], out var m) && _mesas.TryGetValue(m, out var t) ? t.Perfil : null));

    internal IEnumerable<(ObjectId Id, ElectricalString String, Point3 Mais, Point3 Menos)> StringsComPontas => _strings;

    /// <summary>O que pintar de um equipamento em campo: a caixa 3D do bloco dele.</summary>
    internal IReadOnlyList<ObjectId> Pintaveis(CableEnd ponta) => _caixas.TryGetValue((Tipo(ponta.Kind), ponta.Id), out var c) ? c : [];
}

/// <summary>
/// A aba Combiner (19.3): o trecho string -> combiner como o CC (sai da mesa,
/// contorna, menor lado, bate na vala) e o combiner -> inversor como o CA
/// (raio em volta do equipamento). As duas metades na mesma rede de valas.
/// </summary>
internal static class CombinerDaRota
{
    internal static RouteResult Rotear(LeituraDaRota leitura, TrenchNetwork rede, RouteSettings config, Func<double, double, double?> chao)
    {
        var strings = leitura.StringsComPontas
            .Where(x => leitura.CombinerDaString.TryGetValue(x.String.Id, out var cb) && leitura.Setup.FindCombiner(cb) is { } c && c.Inverter != Guid.Empty && c.Inverter == x.String.Inverter)
            .Select(x =>
            {
                var cb = leitura.CombinerDaString[x.String.Id];
                return leitura.Entrada(x.String, x.Mais, x.Menos, new CableEnd(CableEndKind.Combiner, cb),
                    leitura.Setup.FindCombiner(cb)?.Name ?? "?", leitura.Ponto(EquipmentKind.Combiner, cb), CableRoute.Combiner, leitura.Contorno(EquipmentKind.Combiner, cb));
            })
            .OfType<StringRouteInput>()
            .ToList();

        var usadas = leitura.CombinerDaString.Values.ToHashSet();
        var ligacoes = leitura.Setup.Combiners
            .Where(c => usadas.Contains(c.Id))
            .Select(c => new ChainLink(new CableEnd(CableEndKind.Combiner, c.Id), c.Name,
                new CableEnd(CableEndKind.Inverter, leitura.Setup.FindInverter(c.Inverter)?.Id ?? Guid.Empty), leitura.Setup.FindInverter(c.Inverter)?.Name ?? "-"));

        var a = CableRouter.Strings(strings, CableRoute.Combiner, rede, config, chao);
        var b = CableRouter.Equipment(leitura.Trechos(ligacoes), CableRoute.Combiner, rede, config, chao);
        return new RouteResult([.. a.Runs, .. b.Runs], [.. a.Failures, .. b.Failures]);
    }
}
