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

    // As valas apagadas já contadas por um Atualizar (o objeto apagado fica no desenho até fechar).
    private static readonly HashSet<ObjectId> ApagadasJaContadas = [];

    /// <summary>
    /// "Atualizar valas" da aba (Renan, 10/10/2026, item 11): relê as valas da
    /// rota no desenho; as linhas apagadas saem da rota (e são contadas no
    /// recado), e as que ficaram são assentadas de novo no terreno na
    /// profundidade da aba, se o terreno está na memória (a linha editada à mão
    /// volta para o TIN). Nada é apagado nem desenhado além disso.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaValaAtualizar)]
    public static void ValaAtualizar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;
            var db = documento.Database;

            var apagadas = RotaDeCabosStore.ValasApagadas(db, rota).Where(ApagadasJaContadas.Add).Count();
            var linhas = new List<string>();

            if (TerrainCache.Get(documento) is { } terreno)
            {
                var profundidade = RotaDeCabosStore.Configuracao(db, rota).Depth;
                var (_, fora) = RotaDeCabosStore.AssentarValas(db, rota, profundidade, terreno.Mesh);
                linhas.Add(Tr.F("Valas {0} relidas e assentadas no terreno a {1:0.00} m de profundidade.", CableRoutes.Title(rota), profundidade));
                if (fora > 0) linhas.Add(Tr.F("ATENÇÃO: {0} ponto(s) da vala fora do terreno ficaram com a cota que tinham.", fora));
            }
            else
            {
                linhas.Add(Tr.F("Valas {0} relidas (o terreno não está processado nesta sessão: o Gerar assenta).", CableRoutes.Title(rota)));
            }

            if (apagadas > 0) linhas.Add(Tr.F("{0} vala(s) apagada(s) do desenho saíram da rota.", apagadas));
            linhas.Add(Tr.F("A rota tem {0} vala(s).", RotaDeCabosStore.IdsDasValas(db, rota).Count));
            Relatar(documento, rota, string.Join("\n", linhas));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao atualizar as valas.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui atualizar as valas: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

    /// <summary>
    /// "Soltar valas" da aba (item 11): o usuário escolhe linhas no desenho e
    /// elas deixam de ser vala desta rota (sai a marca, a linha fica, na camada
    /// corrente). A que já virou Polyline3d no TIN continua assim, como linha comum.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaValaSoltar)]
    public static void ValaSoltar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            if (PerguntarRota(editor) is not { } rota) return;
            var db = documento.Database;

            var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE,LINE")]);
            var r = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nValas {0} a soltar (só as desta rota contam; a linha fica no desenho): ", CableRoutes.Title(rota)) }, filtro);
            if (r.Status != PromptStatus.OK) return;

            var n = RotaDeCabosStore.SoltarValas(db, r.Value.GetObjectIds(), rota);
            Relatar(documento, rota, Tr.F("{0} linha(s) deixaram de ser vala {1} (continuam no desenho, como linha comum). A rota tem {2} vala(s).",
                n, CableRoutes.Title(rota), RotaDeCabosStore.IdsDasValas(db, rota).Count));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao soltar as valas.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui soltar as valas: {0}\n", erro.Message));
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

    /// <summary>
    /// "Recalcular rota" da aba CC (Renan, 10/10/2026, item 19): para um,
    /// vários ou todos os inversores (nomes ou GUIDs separados por ";",
    /// Todos, ou Selecionar para escolher os retângulos em campo), refaz só os
    /// cabos CC das strings deles, a partir da posição de agora (o inversor
    /// movido à mão fica onde está; o automático que não está em campo é posto).
    /// Os cabos dos outros inversores não são tocados.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoRotaRecalcular)]
    public static void Recalcular()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;
        var editor = documento.Editor;

        try
        {
            var r = editor.GetString(new PromptStringOptions(Tr.T("\nInversores a recalcular (nomes separados por ;, Todos ou Selecionar): ")) { AllowSpaces = true });
            if (r.Status != PromptStatus.OK) return;

            var db = documento.Database;
            var (setup, _) = ConfiguracaoEletricaStore.Ler(db);
            var texto = r.StringResult.Trim();
            var escolhidos = new HashSet<Guid>();

            // As palavras valem em português (a dos scripts e da janela) e no idioma da tela.
            bool Eh(string palavra) => string.Equals(texto, palavra, StringComparison.OrdinalIgnoreCase) || string.Equals(texto, Tr.T(palavra), StringComparison.CurrentCultureIgnoreCase);

            if (Eh("Todos") || texto == "*")
            {
                escolhidos.UnionWith(setup.Inverters.Select(i => i.Id));
            }
            else if (Eh("Selecionar"))
            {
                var filtro = new SelectionFilter([new TypedValue((int)DxfCode.Start, "INSERT")]);
                var s = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nInversores em campo a recalcular (Enter termina): ") }, filtro);
                if (s.Status != PromptStatus.OK) return;
                var marcados = s.Value.GetObjectIds().ToHashSet();
                using var t = db.TransactionManager.StartOpenCloseTransaction();
                foreach (var ((tipo, guid), ids) in EquipamentoEmCampo.Posicionados(t, db))
                    if (tipo == EquipmentKind.Inverter && ids.Any(marcados.Contains)) escolhidos.Add(guid);
            }
            else
            {
                foreach (var nome in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var achado = Guid.TryParse(nome, out var g) ? setup.FindInverter(g) : setup.Inverters.FirstOrDefault(i => string.Equals(i.Name, nome, StringComparison.CurrentCultureIgnoreCase));
                    if (achado is null) editor.WriteMessage(Tr.F("\nROTA Não há inversor \"{0}\".\n", nome));
                    else escolhidos.Add(achado.Id);
                }
            }

            if (escolhidos.Count == 0)
            {
                Relatar(documento, CableRoute.DirectCurrent, Tr.T("Nenhum inversor escolhido: nada foi recalculado."));
                return;
            }

            Gerar(documento, CableRoute.DirectCurrent, recolocar: false, escolhidos);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao recalcular a rota dos inversores.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui recalcular a rota: {0}\n", erro.Message));
        }
        finally
        {
            JanelaDeRotaDeCabos.Voltar(documento);
        }
    }

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

            Gerar(documento, rota, recolocar, null);
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

    /// <summary>
    /// O Gerar de uma rota. Com <paramref name="soInversores"/> (só CC), só
    /// os cabos das strings desses inversores são apagados e desenhados de
    /// novo (o resto da rota fica); a conta é feita com todas as strings, para
    /// a ponta de cada fileira e o ponto de juntar de cada mesa serem os
    /// mesmos de um Gerar inteiro.
    /// </summary>
    private static void Gerar(Document documento, CableRoute rota, bool recolocar, IReadOnlySet<Guid>? soInversores)
    {
        var editor = documento.Editor;
        {
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
                linhas.AddRange(PosicaoAutomatica.Colocar(editor, db, terreno, rede, config, leitura, recolocar, soInversores));
                leitura = LeituraDaRota.Ler(db);
            }

            var resultado = rota switch
            {
                CableRoute.DirectCurrent => CcDaRota(leitura, rede, config, Chao),
                CableRoute.AlternatingCurrent => CableRouter.Equipment(leitura.Trechos(CableChain.AlternatingCurrent(leitura.Setup)), rota, rede, config, Chao),
                CableRoute.MediumVoltage => CableRouter.Equipment(leitura.Trechos(CableChain.MediumVoltage(leitura.Setup)), rota, rede, config, Chao),
                _ => CombinerDaRota.Rotear(leitura, rede, config, Chao),
            };

            var falhas = leitura.Falhas.Where(f => f.Route == rota).Select(f => f.Falha).Concat(resultado.Failures).ToList();
            var runs = resultado.Runs;

            // O inversor de cada string, para o recalcular só de alguns.
            var inversorDaString = leitura.StringsComPontas.ToDictionary(x => x.String.Id, x => x.String.Inverter);
            bool DosEscolhidos(CableEnd ponta) => soInversores is null
                || (ponta.Kind == CableEndKind.Inverter && soInversores.Contains(ponta.Id))
                || (ponta.Kind == CableEndKind.String && inversorDaString.TryGetValue(ponta.Id, out var inv) && soInversores.Contains(inv));

            if (soInversores is not null)
            {
                runs = runs.Where(r => DosEscolhidos(r.Run.From) || DosEscolhidos(r.Run.To)).ToList();
                falhas = falhas.Where(f => f.Paint.Any(DosEscolhidos)).ToList();
            }

            // Refaz a rota (ou só a dos inversores escolhidos): apaga os cabos dela, desenha os novos.
            var antigos = RotaDeCabosStore.Lances(db).Where(l => l.Lance.Route == rota && (DosEscolhidos(l.Lance.From) || DosEscolhidos(l.Lance.To))).ToList();
            RotaDeCabosStore.Apagar(db, antigos.Select(l => l.Id));
            RotaDeCabosStore.Desenhar(db, runs);

            var saem = antigos.Select(l => l.Lance.Id).ToHashSet();
            var gerados = RotaDeCabosStore.LancesGerados(db)
                .Where(l => l.Route != rota || (soInversores is not null && !saem.Contains(l.Id) && !DosEscolhidos(l.From) && !DosEscolhidos(l.To)))
                .Concat(runs.Select(r => r.Run)).ToList();
            RotaDeCabosStore.GravarLancesGerados(db, gerados);

            // No recalcular de alguns, a pintura dos outros fica (soma); a antiga dos
            // escolhidos sai antes, para a falha que a conta nova não acha mais não ficar pintada.
            RotaDeCabosStore.Pintar(db, rota, leitura.ParaPintar(falhas), somar: soInversores is not null,
                refazer: soInversores is null ? null : leitura.PintaveisDosInversores(soInversores));

            if (soInversores is not null)
                linhas.Insert(0, Tr.F("Recalculados {0} inversor(es): {1}. Os cabos dos outros não foram tocados.", soInversores.Count,
                    string.Join(", ", soInversores.Select(i => leitura.Setup.FindInverter(i)?.Name ?? "?").OrderBy(n => n, NaturalStringComparer.Instance))));
            linhas.Insert(0, Tr.F("{0}: {1} lance(s) desenhado(s), {2:0.0} m no total (3D, com as descidas da vala de {3:0.00} m).",
                CableRoutes.Title(rota), runs.Count, runs.Sum(r => r.Length), config.Depth));
            if (valaFora > 0) linhas.Add(Tr.F("ATENÇÃO: {0} ponto(s) da vala fora do terreno ficaram com a cota que tinham.", valaFora));
            if (leitura.ProblemaDoLocal is { } local && rota != CableRoute.MediumVoltage)
                linhas.Add(Tr.F("ATENÇÃO: o local dos inversores não se lê ({0}); a área deles não valeu como contorno.", local));
            if (falhas.Count > 0) linhas.Add(Tr.F("{0} trecho(s) sem rota, pintado(s) de vermelho:", falhas.Count));
            linhas.AddRange(falhas.Select(f => "• " + f.What + ": " + f.Reason));
            Relatar(documento, rota, string.Join("\n", linhas));
        }
    }

    /// <summary>O CC: as strings diretas, saindo pelo lado alto das mesas da usina, uma entrada por mesa (item 10).</summary>
    private static RouteResult CcDaRota(LeituraDaRota leitura, TrenchNetwork rede, RouteSettings config, Func<double, double, double?> chao)
    {
        var strings = leitura.StringsCc();
        return CableRouter.Strings(strings, CableRoute.DirectCurrent, rede, config, chao, ExitPattern.For(strings, leitura.LadoDaUsina));
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

    /// <summary>
    /// Se a ponta de um cabo existe no desenho agora (item 18): a string
    /// desenhada, ou o equipamento com o retângulo em campo. O resumo e o Ver
    /// cabos só contam o cabo com as duas pontas assim.
    /// </summary>
    internal bool EmCampo(CableEnd ponta) => ponta.Kind == CableEndKind.String
        ? (_idsDasStrings ??= _strings.Select(s => s.String.Id).ToHashSet()).Contains(ponta.Id)
        : _emCampo.ContainsKey((Tipo(ponta.Kind), ponta.Id));

    private HashSet<Guid>? _idsDasStrings;

    /// <summary>O lado de saída das strings da usina (item 10): o alto das mesas, somado em todas as mesas do desenho; null se todas são planas.</summary>
    internal Point3? LadoDaUsina => ExitPattern.PlantSide(_mesas.Values.Select(m => m.Mesa.Corners));

    /// <summary>A mesa (GUID, letreiro, cantos) de cada ponta de string do CC, para a conferência do nível 2.</summary>
    internal RowTable? MesaDoModulo(Guid modulo) => _mesaDoModulo.TryGetValue(modulo, out var m) && _mesas.TryGetValue(m, out var t) ? t.Mesa : null;

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

    /// <summary>
    /// O que a falha antiga dos inversores pode ter pintado e o recalcular
    /// deles refaz: as strings deles, a caixa de cada um e as mesas onde só
    /// há strings deles (a mesa que tem string de outro inversor fica: a
    /// pintura dela pode ser do outro, que não foi recalculado).
    /// </summary>
    internal List<ObjectId> PintaveisDosInversores(IReadOnlySet<Guid> inversores)
    {
        var ids = new List<ObjectId>();
        foreach (var i in inversores) ids.AddRange(Pintaveis(new CableEnd(CableEndKind.Inverter, i)));

        var dosOutros = new HashSet<Guid>();
        var dosEscolhidos = new HashSet<Guid>();
        foreach (var (id, s, _, _) in _strings)
        {
            var mesas = s.Modules.Select(m => _mesaDoModulo.TryGetValue(m, out var mesa) ? mesa : Guid.Empty).Where(m => m != Guid.Empty);
            if (inversores.Contains(s.Inverter))
            {
                ids.Add(id);
                dosEscolhidos.UnionWith(mesas);
            }
            else
            {
                dosOutros.UnionWith(mesas);
            }
        }

        foreach (var mesa in dosEscolhidos.Except(dosOutros))
            if (_mesas.TryGetValue(mesa, out var m)) ids.Add(m.Contorno);
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

        var a = CableRouter.Strings(strings, CableRoute.Combiner, rede, config, chao, ExitPattern.For(strings, leitura.LadoDaUsina));
        var b = CableRouter.Equipment(leitura.Trechos(ligacoes), CableRoute.Combiner, rede, config, chao);
        return new RouteResult([.. a.Runs, .. b.Runs], [.. a.Failures, .. b.Failures]);
    }
}
