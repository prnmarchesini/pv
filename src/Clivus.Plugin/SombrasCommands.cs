using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.SombrasCommands))]

namespace Clivus.Plugin;

/// <summary>
/// O período das sombras: dias (de quantos em quantos), janela de horário,
/// passo e fuso, e a altura solar mínima. Com <see cref="AlturaMinima"/>, só
/// contam os instantes da janela com o sol acima dela (05/10/2026, Renan: "se
/// eu rodar o ano inteiro com horário fixo vai dar ruim ... só vou considerar
/// sombras com altura solar maior que 20 graus; os horários vão se ajustando
/// dia a dia"); com a janela do dia inteiro (00:00 às 23:59), é a altura
/// sozinha. Desde 02/11/2026 as duas valem juntas (Renan: "deixe assim e
/// tambem coloque a trava em altura solar ... simular somente com altura
/// solar maior que 15 graus").
/// </summary>
internal sealed record PeriodoDeSombra(DateOnly De, DateOnly Ate, TimeOnly HoraDe, TimeOnly HoraAte, int PassoMinutos, double Fuso, int PassoDias = 1, double? AlturaMinima = null)
{
    /// <summary>Um instante só: mesmo dia, mesma hora (sem altura solar).</summary>
    internal bool Instante => AlturaMinima is null && De == Ate && HoraDe == HoraAte;

    /// <summary>A janela é o dia inteiro: com a altura solar, os horários saem só do sol.</summary>
    private bool DiaInteiro => HoraDe == TimeOnly.MinValue && HoraAte == new TimeOnly(23, 59);

    /// <summary>Os instantes da janela de horário (o filtro do sol vem em <see cref="InstantesComSol"/>).</summary>
    internal IEnumerable<DateTime> Instantes() =>
        Shading.Instants(De, Ate, HoraDe, HoraAte, TimeSpan.FromMinutes(PassoMinutos), PassoDias);

    /// <summary>Os instantes que contam: o sol acima da altura mínima (ou do mínimo do cálculo, 2°).</summary>
    internal IEnumerable<DateTime> InstantesComSol(double latitude, double longitude)
    {
        var minimo = Math.Max(AlturaMinima ?? 0, Shading.MinimumElevationDegrees);
        return Instantes().Where(t => SolarCalculator.Compute(latitude, longitude, t, Fuso).ElevationDegrees >= minimo);
    }

    internal string Descrever()
    {
        if (Instante) return Tr.F("{0:dd/MM/yyyy} às {1:HH:mm}", De, HoraDe);

        var dias = PassoDias > 1 ? Tr.F(" (a cada {0} dias)", PassoDias) : string.Empty;

        return AlturaMinima switch
        {
            { } altura when DiaInteiro => Tr.F("{0:dd/MM/yyyy} a {1:dd/MM/yyyy}{2}, com o sol acima de {3:0.#}°, de {4} em {4} min",
                De, Ate, dias, altura, PassoMinutos),
            { } altura => Tr.F("{0:dd/MM/yyyy} a {1:dd/MM/yyyy}{2}, das {3:HH:mm} às {4:HH:mm} com o sol acima de {5:0.#}°, de {6} em {6} min",
                De, Ate, dias, HoraDe, HoraAte, altura, PassoMinutos),
            _ => Tr.F("{0:dd/MM/yyyy} a {1:dd/MM/yyyy}{2}, das {3:HH:mm} às {4:HH:mm}, de {5} em {5} min",
                De, Ate, dias, HoraDe, HoraAte, PassoMinutos),
        };
    }
}

/// <summary>
/// O relato de uma ação das sombras: o resumo, numa ou duas linhas (a janela
/// mostra só ele, 42.1: "não quero que mostre os motivos na janela"), e o
/// detalhe, que vai junto só na linha de comando.
/// </summary>
internal sealed record RelatoDasSombras(string Resumo, string Detalhe = "")
{
    /// <summary>Como vai para a linha de comando.</summary>
    public override string ToString() => "SOMBRAS " + Resumo + (Detalhe.Length > 0 ? "\n  " + Detalhe : string.Empty);
}

/// <summary>
/// Sombras (9.7, 9.8 e a revisão de 31/10/2026, passo 42.1). Renan: "QUERO
/// VER o desenho das sombras no chao, rachurada ... Falta o botão apagar
/// sombras, ocultar sombras e mostrar sombras ... o botão para escolher o que
/// quero que seja calculado ... Entenda tudo gera sombra ... O PVcase pinta
/// de magenta as sombras".
/// <para>
/// Fazem sombra as mesas, as árvores e o relevo (o barranco). Recebem as
/// mesas do escopo (seleção, uma área, área + seleção, várias áreas, o
/// desenho inteiro) e o chão dele. A sombra é desenhada em magenta, hachura e
/// borda, no chão (acompanhando o terreno) e sobre os módulos, até onde ela
/// pega (<see cref="DesenhoDasSombras"/>). O módulo não é mais pintado: o
/// registro dos motivos guarda, por módulo, quanto, de quê e quando
/// (<see cref="ShadowReasons"/>), para o "Por que essa sombra?" e a janela
/// das análises. Cada escopo troca só as sombras dele: as áreas são
/// calculadas uma a uma e convivem no desenho.
/// </para>
/// </summary>
public static class SombrasCommands
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    /// <summary>Os módulos que a sombra pintou até 30/10/2026, com a cor de antes ("handle=cor"): voltam à cor no Apagar, num cálculo do escopo deles e na Limpeza.</summary>
    private const string ChaveDasPintadas = "SOMBRA_PINTADAS";

    /// <summary>O porquê de cada módulo com sombra (<see cref="ShadowReasons"/>).</summary>
    private const string ChaveDosMotivos = "SOMBRA_MOTIVOS";

    /// <summary>Quanto em volta das mesas escolhidas o chão entra (um ladrilho), no cálculo e no Apagar: os dois precisam dos mesmos ladrilhos.</summary>
    private const double FolgaDaSelecao = GroundPatches.TileSize;

    /// <summary>Até onde, em planta, uma mesa ou árvore longe do escopo ainda entra fazendo sombra.</summary>
    private const double MargemMaxima = 200;

    /// <summary>Até onde, em planta, o terreno em volta do escopo é lido para a sombra do relevo.</summary>
    private const double AlcanceMaximoDoRelevo = 500;

    /// <summary>
    /// As cores com que a sombra pintava os módulos até 30/10/2026 (lilás até
    /// 25%, violeta até 50%, roxo-escuro acima): só para reconhecer e devolver
    /// a cor dos módulos de desenho antigo.
    /// </summary>
    internal static RgbColor CorDaFracao(double f) =>
        f <= 0.25 ? new RgbColor(215, 180, 255) : f <= 0.5 ? new RgbColor(160, 90, 230) : new RgbColor(95, 20, 160);

    /// <summary>As cores da marca até 03/10/2026 (amarelo, laranja, vermelho): desenhos com elas ainda voltam.</summary>
    private static readonly RgbColor[] CoresAntigas = [new(255, 220, 0), new(255, 140, 0), new(190, 30, 0)];

    /// <summary>As sombras do desenho mudaram (geradas ou apagadas): a janela das análises se lê de novo.</summary>
    internal static event Action<Document>? Mudaram;

    private static void Avisar(Document documento)
    {
        try
        {
            Mudaram?.Invoke(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao avisar que as sombras mudaram.", erro);
        }
    }

    /// <summary>Os handles dos blocos dos módulos com sombra no registro dos motivos (o nível 2 escolhe um para o porquê).</summary>
    internal static IReadOnlyList<string> ModulosComSombra(Database database) => LerMotivos(database).Modules.Select(m => m.Handle).ToList();

    /// <summary>Os módulos com sombra no registro dos motivos: o 3D (42.3) leva o momento e a fração de cada um.</summary>
    internal static IReadOnlyCollection<ModuleShadow> MotivosDoDesenho(Database database) => LerMotivos(database).Modules;

    /// <summary>
    /// A cor de antes dos módulos ainda pintados por desenho antigo (handle,
    /// em hexadecimal, e a cor), só os que estão numa cor da marca de sombra:
    /// o 3D (42.3) mostra o módulo na cor dele, e não no roxo de uma conta da
    /// versão antiga.
    /// </summary>
    internal static Dictionary<string, Color> CoresDeAntesDaPintura(Database database)
    {
        var cores = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        var pintadas = LerPintadas(database, out var existe);
        if (!existe || pintadas.Count == 0) return cores;

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var texto in pintadas)
        {
            var partes = texto.Split('=', 2);
            if (partes.Length != 2 || !long.TryParse(partes[0], NumberStyles.HexNumber, Invariante, out var numero)) continue;
            if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;
            if (transacao.GetObject(id, OpenMode.ForRead, false, true) is not Entity modulo || !EDaSombra(modulo.Color)) continue;

            cores[modulo.Handle.ToString()] = PecasPintadas.CorDe(partes[1]);
        }

        return cores;
    }

    /// <summary>
    /// Devolve a cor de antes a todos os módulos pintados por desenho antigo e
    /// zera o registro (42.3, tela de 31/10/2026: "A sombra fica marcada no
    /// modulo mas nao fica no solo"; o roxo da versão antiga ficava no módulo,
    /// o Ocultar não escondia e o 3D mostrava). Quantos voltaram. Com o
    /// documento travado.
    /// </summary>
    internal static int TirarPinturaAntiga(Database database)
    {
        // O registro é lido antes da transação de escrita (ler o dicionário com ela aberta derrubava o Core Console).
        var pintadas = LerPintadas(database, out var existe);
        if (!existe || pintadas.Count == 0) return 0;

        var voltaram = 0;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            foreach (var texto in pintadas)
            {
                var partes = texto.Split('=', 2);
                if (partes.Length != 2 || !long.TryParse(partes[0], NumberStyles.HexNumber, Invariante, out var numero)) continue;
                if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;
                if (transacao.GetObject(id, OpenMode.ForWrite, false, true) is not Entity modulo || !EDaSombra(modulo.Color)) continue;

                modulo.Color = PecasPintadas.CorDe(partes[1]);
                voltaram++;
            }

            GravarPintadas(database, []);
            transacao.Commit();
        }

        return voltaram;
    }

    /// <summary>
    /// Quantas mesas cada área leva no cálculo (a área e as que estão dentro
    /// dela, <see cref="ShadowScopePlan"/>): a janela mostra o número ao lado
    /// do nome e abre na primeira área que tem mesa (42.3).
    /// </summary>
    internal static Dictionary<Guid, int> MesasPorArea(Database database, IReadOnlyList<AreaNoDesenho> areas)
    {
        var mesas = LerMesas(database, comFaces: false).Select(m => new ShadowTable(m.Id, m.Contorno)).ToList();
        var todas = ParaOEscopo(areas);
        var contas = new Dictionary<Guid, int>();

        foreach (var area in areas)
        {
            if (contas.ContainsKey(area.Identity.Id)) continue;
            contas[area.Identity.Id] = ShadowScopePlan.Build(new ShadowScope(ShadowScopeKind.Area, [area.Identity.Id], []), todas, mesas, FolgaDaSelecao).ReceiverCount;
        }

        return contas;
    }

    /// <summary>
    /// As mesas que o escopo leva (as que receberiam a sombra: a área com as
    /// de dentro dela, a seleção, o desenho inteiro) e o nome dele ("de
    /// area1"): a exportação para o PVsyst (42.4) usa a mesma escolha.
    /// </summary>
    internal static (HashSet<Guid> Mesas, string Nome) MesasDoEscopo(Database database, ShadowScope escopo)
    {
        if (escopo.WhyInvalid() is { } falta) throw new ArgumentException(falta);

        var mesas = LerMesas(database, comFaces: false);
        var areas = AreasDoDesenho.Varrer(database);
        var plano = ShadowScopePlan.Build(escopo, ParaOEscopo(areas), mesas.Select(m => new ShadowTable(m.Id, m.Contorno)).ToList(), FolgaDaSelecao);

        return (Enumerable.Range(0, mesas.Count).Where(k => plano.Receives[k]).Select(k => mesas[k].Id).ToHashSet(), NomeDoEscopo(escopo, areas));
    }

    /// <summary>Se as camadas das sombras estão à vista; null se ainda não há sombra desenhada (a janela mostra Ocultar ou Mostrar).</summary>
    internal static bool? SombrasAVista(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var sombra = LayoutLayers.EstaDesligada(transacao, database, LayoutLayers.Sombra);
        var texto = LayoutLayers.EstaDesligada(transacao, database, LayoutLayers.SombraTexto);
        if (sombra is null && texto is null) return null;

        return sombra == false || texto == false;
    }

    // ------------------------------------------------------------ comandos

    [CommandMethod(PluginInfo.ComandoSombras)]
    public static void Sombras()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.T("\nA janela de sombras precisa da interface do Civil 3D; use CLIVUS_SOMBRAS_AUTO.\n"));
            return;
        }

        try
        {
            JanelaDeSombras.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela de sombras.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir as sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_SOMBRAS_AUTO: o desenho inteiro; dia inicial e final (dd/mm/aaaa), hora de e até (hh:mm), passo (min), fuso e passo de dias. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoSombrasAutomatico)]
    public static void SombrasAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarPeriodo(editor) is not { } periodo) return;
            editor.WriteMessage($"\n{GerarComAndamento(documento, periodo, ShadowScope.WholeDrawing, null)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nas sombras (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar as sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_SOMBRAS_ESCOPO_AUTO: o escopo (SELECAO, AREA, AREA+SELECAO,
    /// AREAS, TUDO), as áreas pelo nome e as mesas pelo letreiro (separados
    /// por ";", "-" para nenhum), e o período como no CLIVUS_SOMBRAS_AUTO.
    /// Para o nível 2.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoSombrasEscopoAutomatico)]
    public static void SombrasEscopoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarEscopo(editor, documento) is not { } escopo) return;
            if (PerguntarPeriodo(editor) is not { } periodo) return;
            editor.WriteMessage($"\n{GerarComAndamento(documento, periodo, escopo, null)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nas sombras do escopo (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gerar as sombras: {0}\n", erro.Message));
        }
    }

    [CommandMethod(PluginInfo.ComandoSombraPorQue)]
    public static void SombraPorQue()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var opcoes = new PromptEntityOptions(Tr.T("\nClique num módulo (ou na face dele): "));
            var escolha = editor.GetEntity(opcoes);
            if (escolha.Status != PromptStatus.OK) return;

            editor.WriteMessage($"\n{PorQueDaPeca(documento.Database, escolha.ObjectId)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no por que da sombra.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ver a sombra desse módulo: {0}\n", erro.Message));
        }
    }

    [CommandMethod(PluginInfo.ComandoSombraPorQueAutomatico)]
    public static void SombraPorQueAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var texto = editor.GetString(new PromptStringOptions(Tr.T("\nHandle do bloco do módulo: ")));
        if (texto.Status != PromptStatus.OK) return;

        if (!long.TryParse(texto.StringResult, NumberStyles.HexNumber, Invariante, out var numero)
            || !documento.Database.TryGetObjectId(new Handle(numero), out var id))
        {
            editor.WriteMessage(Tr.F("\nSOMBRA Não há entidade com o handle {0}.\n", texto.StringResult));
            return;
        }

        editor.WriteMessage($"\n{PorQueDaPeca(documento.Database, id)}\n");
    }

    /// <summary>CLIVUS_SOMBRAS_APAGAR: as sombras de tudo (Enter), de uma área (clique) ou das mesas escolhidas.</summary>
    [CommandMethod(PluginInfo.ComandoSombrasApagar)]
    public static void SombrasApagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            const string Tudo = "Tudo", Area = "Area", Selecao = "Selecao";
            var opcoes = new PromptKeywordOptions(Tr.T("\nApagar as sombras de"));
            opcoes.Keywords.Add(Tudo);
            opcoes.Keywords.Add(Area);
            opcoes.Keywords.Add(Selecao);
            opcoes.Keywords.Default = Tudo;
            opcoes.AllowNone = true;

            var resposta = editor.GetKeywords(opcoes);
            if (resposta.Status is not (PromptStatus.OK or PromptStatus.None)) return;
            var escolha = string.IsNullOrEmpty(resposta.StringResult) ? Tudo : resposta.StringResult;

            ShadowScope escopo;
            if (escolha == Area)
            {
                if (EscolhaDeArea.Pedir(editor, documento) is not { } area) return;
                escopo = new ShadowScope(ShadowScopeKind.Area, [area.Identity.Id], []);
            }
            else if (escolha == Selecao)
            {
                var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.T("\nEscolha as mesas: ") });
                if (selecao.Status != PromptStatus.OK) return;
                escopo = new ShadowScope(ShadowScopeKind.Selection, [], [.. SelecaoCommands.MesasTocadas(documento, selecao.Value.GetObjectIds())]);
            }
            else
            {
                escopo = ShadowScope.WholeDrawing;
            }

            editor.WriteMessage($"\n{Apagar(documento, escopo)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar as sombras.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar as sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_SOMBRAS_APAGAR_AUTO: o escopo, as áreas e as mesas pelos nomes, como no CLIVUS_SOMBRAS_ESCOPO_AUTO. Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoSombrasApagarAutomatico)]
    public static void SombrasApagarAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarEscopo(editor, documento) is not { } escopo) return;
            editor.WriteMessage($"\n{Apagar(documento, escopo)}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao apagar as sombras (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar as sombras: {0}\n", erro.Message));
        }
    }

    [CommandMethod(PluginInfo.ComandoSombrasOcultar)]
    public static void SombrasOcultar() => LigarPeloComando(false);

    [CommandMethod(PluginInfo.ComandoSombrasMostrar)]
    public static void SombrasMostrar() => LigarPeloComando(true);

    [CommandMethod(PluginInfo.ComandoSombrasAnalises)]
    public static void SombrasAnalises()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        if (!ClivusExtension.TemInterface())
        {
            documento.Editor.WriteMessage(Tr.T("\nA janela das análises de sombra precisa da interface do Civil 3D; use CLIVUS_SOMBRAS_ANALISES_AUTO.\n"));
            return;
        }

        try
        {
            JanelaDeAnalisesDeSombra.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha na janela das análises de sombra.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir as análises de sombra: {0}\n", erro.Message));
        }
    }

    /// <summary>CLIVUS_SOMBRAS_ANALISES_AUTO: os tópicos da janela, um por linha (SOMBRA_TOPICO nível texto). Para o nível 2.</summary>
    [CommandMethod(PluginInfo.ComandoSombrasAnalisesAutomatico)]
    public static void SombrasAnalisesAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var topicos = Analises(documento);
            void Escrever(ShadowTopic t, int nivel)
            {
                editor.WriteMessage($"\nSOMBRA_TOPICO {nivel} {t.Text}");
                foreach (var filho in t.Children) Escrever(filho, nivel + 1);
            }

            foreach (var t in topicos) Escrever(t, 1);
            editor.WriteMessage(Tr.F("\nSOMBRAS {0} área(s) com módulo com sombra.\n", topicos.Count));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha nas análises de sombra (automático).", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ler as análises de sombra: {0}\n", erro.Message));
        }
    }

    // ------------------------------------------------------------ perguntas

    /// <summary>Os sete textos do período na linha de comando; null se a pessoa desistiu ou o período não vale (o porquê já foi escrito).</summary>
    private static PeriodoDeSombra? PerguntarPeriodo(Editor editor)
    {
        string? Texto(string pergunta)
        {
            var r = editor.GetString(new PromptStringOptions($"\n{pergunta}: ") { AllowSpaces = false });
            return r.Status == PromptStatus.OK ? r.StringResult : null;
        }

        var de = Texto(Tr.T("Primeiro dia (dd/mm/aaaa)"));
        var ate = Texto(Tr.T("Último dia (dd/mm/aaaa)"));
        var horaDe = Texto(Tr.T("Hora inicial (hh:mm)"));
        var horaAte = Texto(Tr.T("Hora final (hh:mm)"));
        var passo = Texto(Tr.T("Passo (min)"));
        var fuso = Texto(Tr.T("Fuso (horas, -3 em Brasília)"));
        var dias = Texto(Tr.T("A cada quantos dias"));
        if (de is null || ate is null || horaDe is null || horaAte is null || passo is null || fuso is null || dias is null) return null;

        var periodo = Ler(de, ate, horaDe, horaAte, passo, fuso, out var porque, dias);
        if (periodo is null) editor.WriteMessage(Tr.F("\nSOMBRAS {0}\n", porque));
        return periodo;
    }

    /// <summary>O escopo, as áreas e as mesas na linha de comando; null se não deu (o porquê já foi escrito).</summary>
    private static ShadowScope? PerguntarEscopo(Editor editor, Document documento)
    {
        string? Texto(string pergunta)
        {
            var r = editor.GetString(new PromptStringOptions($"\n{pergunta}: ") { AllowSpaces = true });
            return r.Status == PromptStatus.OK ? r.StringResult : null;
        }

        var tipo = Texto(Tr.T("Escopo (TUDO, AREA, AREAS, SELECAO, AREA+SELECAO)"));
        var areas = Texto(Tr.T("Áreas, pelo nome, separadas por ; (- para nenhuma)"));
        var mesas = Texto(Tr.T("Mesas, pelo letreiro, separadas por ; (- para nenhuma)"));
        if (tipo is null || areas is null || mesas is null) return null;

        var escopo = EscopoPelosNomes(documento.Database, tipo, areas, mesas, out var porque);
        if (escopo is null) editor.WriteMessage(Tr.F("\nSOMBRAS {0}\n", porque));
        return escopo;
    }

    /// <summary>O escopo pelo tipo e pelos nomes das áreas e letreiros das mesas; null com o porquê.</summary>
    internal static ShadowScope? EscopoPelosNomes(Database database, string tipo, string areas, string mesas, out string porque)
    {
        porque = string.Empty;

        if (ShadowScope.Parse(tipo) is not { } kind)
        {
            porque = Tr.F("Escopo \"{0}\" não existe: use TUDO, AREA, AREAS, SELECAO ou AREA+SELECAO.", tipo);
            return null;
        }

        static List<string> Nomes(string texto) =>
            texto.Trim() == "-" ? [] : texto.Split(';').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();

        var doDesenho = AreasDoDesenho.Varrer(database);
        var idsDasAreas = new List<Guid>();
        foreach (var nome in Nomes(areas))
        {
            if (doDesenho.FirstOrDefault(a => string.Equals(a.Identity.DisplayName, nome, StringComparison.CurrentCultureIgnoreCase)) is not { } area)
            {
                porque = Tr.F("Não há área chamada \"{0}\" no desenho.", nome);
                return null;
            }

            idsDasAreas.Add(area.Identity.Id);
        }

        var idsDasMesas = new List<Guid>();
        var letreiros = Nomes(mesas);
        if (letreiros.Count > 0)
        {
            using var transacao = database.TransactionManager.StartOpenCloseTransaction();
            var todas = LayoutScan.Tables(transacao, database);

            foreach (var letreiro in letreiros)
            {
                var achada = todas.FirstOrDefault(m => string.Equals(m.Value.Identity?.Label, letreiro, StringComparison.OrdinalIgnoreCase));
                if (achada.Value is null)
                {
                    porque = Tr.F("Não há mesa {0} no desenho.", letreiro);
                    return null;
                }

                idsDasMesas.Add(achada.Key);
            }

            transacao.Commit();
        }

        var escopo = new ShadowScope(kind, idsDasAreas, idsDasMesas);
        if (escopo.WhyInvalid() is { } falta)
        {
            porque = falta;
            return null;
        }

        return escopo;
    }

    /// <summary>Lê o período dos textos; null com o porquê.</summary>
    internal static PeriodoDeSombra? Ler(string de, string ate, string horaDe, string horaAte, string passo, string fuso, out string porque, string dias = "1")
    {
        porque = string.Empty;

        if (!int.TryParse(dias.Trim(), NumberStyles.Integer, Brasil, out var pd) || pd is < 1 or > 366) { porque = Tr.T("O passo de dias precisa ser de 1 a 366."); return null; }

        if (!DateOnly.TryParseExact(de.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d0)) { porque = Tr.T("O primeiro dia precisa ser dd/mm/aaaa."); return null; }
        if (!DateOnly.TryParseExact(ate.Trim(), "d/M/yyyy", Brasil, DateTimeStyles.None, out var d1)) { porque = Tr.T("O último dia precisa ser dd/mm/aaaa."); return null; }
        // A altura solar mínima vem na hora inicial: ">20" é o dia inteiro com
        // o sol acima de 20°; "08:00>15" (02/11/2026), a janela de horário e,
        // nela, só os instantes com o sol acima de 15°.
        double? altura = null;
        var seta = horaDe.IndexOf('>', StringComparison.Ordinal);
        if (seta >= 0)
        {
            if (!double.TryParse(horaDe[(seta + 1)..].Trim().Replace(',', '.'), NumberStyles.Float, Invariante, out var a) || a is < 0 or >= 90)
            {
                porque = Tr.T("A altura solar mínima precisa ser de 0 a 89 graus.");
                return null;
            }

            altura = a;
            if (horaDe[..seta].Trim().Length == 0)
            {
                horaDe = "00:00";
                horaAte = "23:59";
            }
            else
            {
                horaDe = horaDe[..seta];
            }
        }

        if (!TimeOnly.TryParseExact(horaDe.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h0)) { porque = Tr.T("A hora inicial precisa ser hh:mm."); return null; }
        if (!TimeOnly.TryParseExact(horaAte.Trim(), ["H:mm", "H"], Brasil, DateTimeStyles.None, out var h1)) { porque = Tr.T("A hora final precisa ser hh:mm."); return null; }
        if (!int.TryParse(passo.Trim(), NumberStyles.Integer, Brasil, out var p) || p is < 1 or > 1440) { porque = Tr.T("O passo precisa ser de 1 a 1440 minutos."); return null; }
        if (!double.TryParse(fuso.Trim().Replace(',', '.'), NumberStyles.Float, Invariante, out var f) || f is < -14 or > 14) { porque = Tr.T("O fuso precisa ser de -14 a 14 horas."); return null; }
        if (d1 < d0) { porque = Tr.T("O último dia vem antes do primeiro."); return null; }
        if (h1 < h0) { porque = Tr.T("A hora final vem antes da inicial."); return null; }
        var instantes = Shading.CountInstants(d0, d1, h0, h1, TimeSpan.FromMinutes(p), pd);
        if (instantes > Shading.MaxInstants)
        {
            porque = Tr.F("O período tem {0:N0} instantes; o máximo é {1:N0}. Aumente o passo ou encurte o período.", instantes, Shading.MaxInstants);
            return null;
        }

        return new PeriodoDeSombra(d0, d1, h0, h1, p, f, pd, altura);
    }

    /// <summary>
    /// O fuso do relógio do lugar do desenho (05/10/2026: saiu da janela de
    /// sombras): pelo estado no Brasil, pela longitude fora; −3 sem lugar.
    /// </summary>
    internal static double FusoDoLugar(Document documento) =>
        Lugar(documento) is { IsValid: true } l ? TerrainPlace.ClockOffsetHours(l.Latitude, l.Longitude) : -3;

    /// <summary>A localização do terreno (a mesma do resumo do terreno), ou null.</summary>
    internal static GeoLocation? Lugar(Document documento)
    {
        var terreno = TerrainCache.Get(documento);
        if (terreno is null) return null;

        return GeoStore.Read(documento.Database, TerrenoResumoCommands.Centro(terreno));
    }

    // ------------------------------------------------------------ gerar

    /// <summary>O botão da janela: o mesmo cálculo, fora de comando, com o vigia calado.</summary>
    internal static RelatoDasSombras GerarPelaJanela(Document documento, PeriodoDeSombra periodo, ShadowScope escopo, ShadowCasters? queFaz = null) =>
        EscritaForaDeComando.Fazer(documento, () => GerarComAndamento(documento, periodo, escopo, null, queFaz));

    /// <summary>
    /// Gera com a janela de andamento (05/10/2026, Renan: "quero um modal que
    /// mostra o carregamento e % e vai passando os dias calculados com um
    /// botão de cancelar a qualquer momento"): a porcentagem, o dia sendo
    /// calculado e o Cancelar. Sem interface (Core Console), sem janela.
    /// A janela fecha sempre, dê certo, dê erro ou seja cancelado.
    /// </summary>
    /// <param name="documento">O desenho (travado por quem chama, se fora de comando).</param>
    /// <param name="periodo">O período.</param>
    /// <param name="escopo">O que calcular.</param>
    /// <param name="dono">A janela de sombras (travada enquanto isso), ou null no comando.</param>
    /// <param name="fazem">O que faz sombra (a janela pode desligar mesas, árvores ou relevo); null: tudo.</param>
    internal static RelatoDasSombras GerarComAndamento(Document documento, PeriodoDeSombra periodo, ShadowScope escopo, System.Windows.Window? dono, ShadowCasters? fazem = null)
    {
        if (!ClivusExtension.TemInterface()) return Gerar(documento, periodo, escopo, queFaz: fazem);

        JanelaDeProgresso? janela = null;
        try
        {
            try
            {
                janela = JanelaDeProgresso.Abrir(dono, Tr.T("Gerar sombras"), podeCancelar: true);
            }
            catch (System.Exception erro)
            {
                // Sem a janela, o cálculo segue sem andamento.
                RegistroDeDiagnostico.Registrar("Falha ao abrir o andamento das sombras.", erro);
                janela = null;
            }

            if (janela is null) return Gerar(documento, periodo, escopo, queFaz: fazem);

            return Gerar(documento, periodo, escopo, (p, texto) =>
            {
                janela.Avancar(p, texto);
                return !janela.Cancelado;
            }, () => janela.PodeCancelar(false), fazem);
        }
        finally
        {
            try
            {
                janela?.Fechar();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao fechar o andamento das sombras.", erro);
            }
        }
    }

    /// <summary>
    /// Gera as sombras do escopo no período: calcula (o pior caso de cada
    /// módulo do escopo e a mancha da sombra de tudo, no chão e sobre as
    /// mesas), e só então troca as sombras do escopo pelas novas, numa
    /// transação, e grava os motivos. Devolve o relato.
    /// </summary>
    /// <param name="documento">O desenho.</param>
    /// <param name="periodo">O período.</param>
    /// <param name="escopo">O que calcular.</param>
    /// <param name="avancar">O andamento (porcentagem, o que está sendo feito); devolve false para cancelar. Cancelado, nada é desenhado.</param>
    /// <param name="semVolta">Chamado antes de gravar no desenho: dali em diante não se cancela.</param>
    /// <param name="queFaz">O que faz sombra (42.3); null: tudo.</param>
    internal static RelatoDasSombras Gerar(Document documento, PeriodoDeSombra periodo, ShadowScope escopo, Func<double, string, bool>? avancar = null, Action? semVolta = null, ShadowCasters? queFaz = null)
    {
        queFaz ??= ShadowCasters.All;
        if (Preparar(documento, periodo, escopo, queFaz, out var naoDa) is not { } preparo) return naoDa!;

        var (database, tin, lugar, mesas, areas, areasDoEscopo, nomeDoEscopo, lista, elevacaoMinima, plano, caixa,
            fazem, recebem, posicaoNoRelevo, cilindros, arvoresQueFazem, faces, recebeFace, deQuem, relevo, modelo) = preparo;
        double? Chao(double x, double y) => tin.TryGetZ(x, y, out var z) ? z : null;

        var mancha = new ShadowFootprint(cilindros, fazem.Select(k => mesas[k].Contorno).ToList(), Chao, tablesCastShadow: queFaz.Tables, receivers: fazem.Select(k => plano.Receives[k]).ToList(), groundBounds: caixa);

        return Calcular(documento, periodo, avancar, semVolta, queFaz, preparo, mancha);
    }

    /// <summary>
    /// O que o cálculo monta antes de varrer os instantes, o mesmo para gerar
    /// as sombras e para o resumo de uma mesa (42.14): as mesas, quem recebe e
    /// quem faz sombra, o relevo, o modelo e os instantes com sol.
    /// </summary>
    private sealed record Preparo(
        Database Database, Tin Tin, GeoLocation Lugar, List<MesaLida> Mesas, IReadOnlyList<AreaNoDesenho> Areas, List<ShadowArea> AreasDoEscopo,
        string NomeDoEscopo, List<DateTime> Lista, double ElevacaoMinima, ShadowScopePlan Plano, (double MinX, double MinY, double MaxX, double MaxY) Caixa,
        List<int> Fazem, List<int> Recebem, Dictionary<int, int> PosicaoNoRelevo, List<ShadowCylinder> Cilindros, int ArvoresQueFazem,
        List<ShadowQuad> Faces, List<bool> RecebeFace, List<(int Mesa, FaceLida Face)> DeQuem, TerrainShadow? Relevo, ShadingModel Modelo);

    /// <summary>Monta o cálculo do escopo no período; null com o porquê.</summary>
    private static Preparo? Preparar(Document documento, PeriodoDeSombra periodo, ShadowScope escopo, ShadowCasters queFaz, out RelatoDasSombras? porque)
    {
        porque = null;
        if (queFaz.None)
        {
            porque = new RelatoDasSombras(Tr.T("Nada marcado em \"Fazem sombra\": marque as mesas, as árvores ou o relevo."));
            return null;
        }

        var database = documento.Database;
        var terreno = TerrainCache.Get(documento);

        if (terreno is null && FileiraCommands.ExigirTerreno(documento.Editor, documento) is null)
        {
            porque = new RelatoDasSombras(Tr.T("Nenhum terreno processado neste desenho. Use o botão Terreno primeiro."));
            return null;
        }

        terreno ??= TerrainCache.Get(documento)!;

        var lugar = Lugar(documento);
        if (lugar is null || !lugar.IsValid)
        {
            porque = new RelatoDasSombras(Tr.T("O desenho não tem localização (latitude e longitude). Corrija em Terreno > Resumo > Localização."));
            return null;
        }

        if (escopo.WhyInvalid() is { } falta)
        {
            porque = new RelatoDasSombras(falta);
            return null;
        }

        var mesas = LerMesas(database, comFaces: true);
        if (mesas.All(m => m.Faces.Count == 0))
        {
            porque = new RelatoDasSombras(Tr.T("Não há módulo gerado pelo plugin no desenho."));
            return null;
        }

        var areas = AreasDoDesenho.Varrer(database);
        var arvores = queFaz.Trees ? LerArvores(database) : [];
        var tin = terreno.Mesh;
        double? Chao(double x, double y) => tin.TryGetZ(x, y, out var z) ? z : null;

        var nomeDoEscopo = NomeDoEscopo(escopo, areas);

        // Os instantes e o sol mais baixo do período: a margem dos objetos e do relevo sai dele.
        List<DateTime> lista = periodo.Instante ? [periodo.De.ToDateTime(periodo.HoraDe)] : periodo.InstantesComSol(lugar.Latitude, lugar.Longitude).ToList();
        var elevacoes = lista
            .Select(t => SolarCalculator.Compute(lugar.Latitude, lugar.Longitude, t, periodo.Fuso).ElevationDegrees)
            .Where(e => e >= Shading.MinimumElevationDegrees)
            .ToList();

        if (elevacoes.Count == 0)
        {
            porque = new RelatoDasSombras(Tr.F("O sol não está acima do horizonte em {0}: nada a sombrear.", periodo.Descrever()),
                Tr.F("{0} instante(s), 0 com sol.", lista.Count));
            return null;
        }

        var elevacaoMinima = elevacoes.Min();

        // O escopo: quem recebe, o chão de cada área, os ladrilhos da seleção.
        var areasDoEscopo = ParaOEscopo(areas);
        var plano = ShadowScopePlan.Build(escopo, areasDoEscopo, mesas.Select(m => new ShadowTable(m.Id, m.Contorno)).ToList(), FolgaDaSelecao);
        if (plano.ReceiverCount == 0 || plano.Bounds is not { } caixa)
        {
            porque = new RelatoDasSombras(Tr.F("Nenhuma mesa {0}: nada a calcular.", nomeDoEscopo));
            return null;
        }

        var minimo = CotaMaisBaixa(tin, caixa, Enumerable.Range(0, mesas.Count).Where(k => plano.Receives[k]).Select(k => mesas[k].Contorno));

        // Quem faz sombra: as mesas do escopo e as que alcançam ele com o sol mais baixo do período.
        var fazem = new List<int>();
        for (var k = 0; k < mesas.Count; k++)
        {
            var c = mesas[k].Contorno;
            if (plano.Receives[k]
                || plano.Reaches(c.Min(p => p.X), c.Min(p => p.Y), c.Max(p => p.X), c.Max(p => p.Y), TerrainShadow.Reach(c.Max(p => p.Z), minimo, elevacaoMinima, MargemMaxima)))
                fazem.Add(k);
        }

        var cilindros = arvores
            .Where(a => plano.Reaches(a.X - a.Raio, a.Y - a.Raio, a.X + a.Raio, a.Y + a.Raio, TerrainShadow.Reach(a.Topo, minimo, elevacaoMinima, MargemMaxima)))
            .SelectMany(a => a.Cilindros)
            .ToList();
        var arvoresQueFazem = arvores.Count(a => plano.Reaches(a.X - a.Raio, a.Y - a.Raio, a.X + a.Raio, a.Y + a.Raio, TerrainShadow.Reach(a.Topo, minimo, elevacaoMinima, MargemMaxima)));

        // As faces dos módulos das mesas que fazem sombra; recebem as do escopo.
        var faces = new List<ShadowQuad>();
        var recebeFace = new List<bool>();
        var deQuem = new List<(int Mesa, FaceLida Face)>();
        for (var g = 0; g < fazem.Count; g++)
        {
            var k = fazem[g];
            foreach (var f in mesas[k].Faces)
            {
                faces.Add(new ShadowQuad(f.Cantos, g));
                recebeFace.Add(plano.Receives[k]);
                deQuem.Add((k, f));
            }
        }

        // O relevo: só se o chão em volta tem declive para fazer sombra com o sol mais baixo.
        var recebem = fazem.Where(k => plano.Receives[k]).ToList();
        var posicaoNoRelevo = recebem.Select((k, j) => (k, j)).ToDictionary(x => x.k, x => x.j);
        var regiao = plano.Ground.SelectMany(p => p.Rings).ToList();
        var alcance = TerrainShadow.Reach(tin.MaxZ, minimo, elevacaoMinima, AlcanceMaximoDoRelevo);
        var declive = TerrainShadow.MaxSlope(tin, caixa.MinX - alcance, caixa.MinY - alcance, caixa.MaxX + alcance, caixa.MaxY + alcance);
        var relevo = queFaz.Terrain && declive > Math.Tan(elevacaoMinima * Math.PI / 180)
            ? new TerrainShadow(Chao, regiao, recebem.Select(k => mesas[k].Contorno).ToList(), alcance, regiao.Sum(ShadowUnion.Area) > 250_000 ? 2 : 1)
            : null;

        // Sem as mesas em "Fazem sombra", elas só recebem: nem umas nas outras, nem no chão.
        var modelo = new ShadingModel(faces, cilindros, tablesCastShadow: queFaz.Tables, receivers: recebeFace, terrain: relevo);

        return new Preparo(database, tin, lugar, mesas, areas, areasDoEscopo, nomeDoEscopo, lista, elevacaoMinima, plano, caixa,
            fazem, recebem, posicaoNoRelevo, cilindros, arvoresQueFazem, faces, recebeFace, deQuem, relevo, modelo);
    }

    /// <summary>
    /// Os dias e as horas de sombra de uma face no período: os dias calculados
    /// e a soma das frações valem pelo passo de dias (um dia por semana vale
    /// 7), e as horas pelo passo do dia; num instante só, 1 dia e nenhuma hora.
    /// </summary>
    private static (int? Dias, double? Horas) Contas(ShadeTally? conta, PeriodoDeSombra periodo, int diasDoPeriodo) =>
        conta is not { } c ? (null, null)
        : periodo.Instante ? (c.Days > 0 ? 1 : 0, 0)
        : (Math.Min(c.Days * periodo.PassoDias, diasDoPeriodo), c.FractionSum * periodo.PassoMinutos / 60.0 * periodo.PassoDias);

    /// <summary>O cálculo do Gerar sombras, com o que o <see cref="Preparar"/> montou: os instantes, a mancha, o desenho e os motivos.</summary>
    private static RelatoDasSombras Calcular(
        Document documento, PeriodoDeSombra periodo, Func<double, string, bool>? avancar, Action? semVolta, ShadowCasters queFaz, Preparo preparo, ShadowFootprint mancha)
    {
        var (database, tin, lugar, mesas, areas, areasDoEscopo, nomeDoEscopo, lista, elevacaoMinima, plano, caixa,
            fazem, recebem, posicaoNoRelevo, cilindros, arvoresQueFazem, faces, recebeFace, deQuem, relevo, modelo) = preparo;

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        using var cancelar = new CancellationTokenSource();

        // O andamento: o dia sendo calculado e a porcentagem pelos instantes
        // (até 90%; o resto é juntar a mancha e gravar).
        var totalDeDias = periodo.Instante ? 1 : (periodo.Ate.DayNumber - periodo.De.DayNumber) / periodo.PassoDias + 1;
        var feitos = 0;
        void Andar(double porcento, string texto)
        {
            if (avancar is not null && !avancar(porcento, texto)) cancelar.Cancel();
            cancelar.Token.ThrowIfCancellationRequested();
        }

        void Dia(DateTime t)
        {
            var dia = Math.Clamp((DateOnly.FromDateTime(t).DayNumber - periodo.De.DayNumber) / periodo.PassoDias + 1, 1, totalDeDias);
            Andar(90.0 * feitos++ / Math.Max(1, lista.Count), Tr.F("Calculando {0:dd/MM} ({1} de {2})...", t, dia, totalDeDias));
        }

        ShadingWorstCase pior;
        var passos = new List<DateTime>();
        var chao = new List<DesenhoDasSombras.ChaoDaArea>();
        var naMesa = new List<DesenhoDasSombras.SombraNaMesa>();

        try
        {
            Andar(0, Tr.T("Preparando o terreno e as mesas..."));

            // Um passo só pelo período: o pior caso dos módulos, a mancha dos
            // objetos e a do relevo juntos (a varredura do relevo do passo
            // serve aos dois).
            pior = modelo.Worst(lugar.Latitude, lugar.Longitude, periodo.Fuso, lista, (t, sol) =>
            {
                Dia(t);
                if (sol.ElevationDegrees < Shading.MinimumElevationDegrees) return;
                mancha.Add(sol);
                relevo?.Add(sol);
                passos.Add(t);
            }, cancelar.Token);

            // O chão: a sombra dos objetos e a do relevo, sem o chão debaixo
            // das mesas (em planta, ali se vê a mesa e a sombra dela), por área.
            Andar(90, Tr.T("Juntando as sombras..."));
            var noChao = ShadowUnion.Union(mancha.Ground().Concat(relevo?.Ground() ?? []));
            var debaixoDasMesas = mesas
                .Where(m => m.Contorno.Max(p => p.X) >= caixa.MinX && m.Contorno.Min(p => p.X) <= caixa.MaxX && m.Contorno.Max(p => p.Y) >= caixa.MinY && m.Contorno.Min(p => p.Y) <= caixa.MaxY)
                .Select(m => (IReadOnlyList<(double X, double Y)>)m.Contorno.Select(p => (p.X, p.Y)).ToList());
            noChao = ShadowUnion.Difference(noChao, debaixoDasMesas);

            foreach (var parte in plano.Ground)
            {
                var aneis = ShadowUnion.Intersect(noChao, parte.Rings);
                if (aneis.Count > 0) chao.Add(new DesenhoDasSombras.ChaoDaArea(parte.Area, aneis));
            }

            // Sobre as mesas do escopo: a dos objetos e a do relevo, juntas.
            for (var g = 0; g < fazem.Count; g++)
            {
                var k = fazem[g];
                if (!plano.Receives[k]) continue;

                var aneis = ShadowUnion.Union(mancha.OnTable(g).Concat(relevo is null ? [] : relevo.OnTable(posicaoNoRelevo[k])));
                if (aneis.Count > 0) naMesa.Add(new DesenhoDasSombras.SombraNaMesa(mesas[k].Id, plano.AreaIdOf(k, areasDoEscopo), mesas[k].Contorno, aneis));
                if (g % 50 == 0) Andar(92, Tr.T("Juntando as sombras..."));
            }

            Andar(95, Tr.T("Desenhando..."));
        }
        catch (OperationCanceledException)
        {
            return new RelatoDasSombras(Tr.T("Cancelado; nada foi desenhado."));
        }

        relogio.Stop();

        // O pior de cada módulo do escopo (uma face por módulo; se houver duas,
        // a pior), com os dias e as horas de sombra no período (02/11/2026: os
        // piores módulos e os dias de sombra por ano saem delas).
        var descrito = periodo.Descrever();
        var diasDoPeriodo = periodo.Ate.DayNumber - periodo.De.DayNumber + 1;
        var porModulo = new Dictionary<ObjectId, (int Mesa, ModuleShadow Sombra)>();
        for (var f = 0; f < faces.Count; f++)
        {
            if (!recebeFace[f] || pior.Fractions[f] <= 0) continue;

            var (k, face) = deQuem[f];
            var (dias, horas) = Contas(pior.Tallies?[f], periodo, diasDoPeriodo);
            var sombra = new ModuleShadow(face.Modulo.Handle.ToString(), mesas[k].Id, pior.Fractions[f], pior.Causes![f], pior.When[f], descrito, dias, horas);
            if (!porModulo.TryGetValue(face.Modulo, out var antes) || antes.Sombra.Fraction < sombra.Fraction) porModulo[face.Modulo] = (k, sombra);
        }

        // Daqui em diante grava no desenho: não para pela metade. Os registros
        // são lidos antes da transação de escrita (ler o dicionário com ela
        // aberta derrubava o Core Console).
        semVolta?.Invoke();

        var motivos = LerMotivos(database);
        var pintadas = LerPintadas(database, out var temPintadas);
        var calculadas = recebem.Select(k => mesas[k].Id).ToHashSet();
        var pecasDasMesas = mesas.SelectMany(m => m.Pecas).ToHashSet();
        var marca = (passos.Count > 0 ? passos[0] : periodo.De.ToDateTime(periodo.HoraDe)).ToString("s", Invariante);

        int apagadas, devolvidos;
        DesenhoDasSombras.Feito feito;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            apagadas = ApagarDoEscopo(transacao, database, plano, Legado(areas, mesas));
            (devolvidos, var sobram) = DevolverPintadas(transacao, database, pintadas, calculadas);
            feito = DesenhoDasSombras.Desenhar(transacao, database, tin, chao, naMesa, Rotulo(periodo, passos), marca, descrito, pecasDasMesas);
            DesenhoDasSombras.Ligar(transacao, database, true);

            // Os registros dentro da mesma transação (o Save abre uma aninhada, que só vale com o Commit desta).
            if (temPintadas) GravarPintadas(database, sobram);
            GravarMotivos(database, motivos.Replace(calculadas, descrito, porModulo.Values.Select(m => m.Sombra)));

            transacao.Commit();
        }

        Avisar(documento);

        // O relato: o resumo para a janela, o detalhe para a linha de comando.
        var resumo = ShadowTopics.Summary(porModulo.Values.Select(m => (mesas[m.Mesa].Letreiro, m.Sombra)).ToList(), nomeDoEscopo, relogio.Elapsed.TotalSeconds);

        int Por(ShadowCause c) => porModulo.Values.Count(m => m.Sombra.Cause == c);

        var detalhe = new System.Text.StringBuilder();
        detalhe.Append(Tr.F("{0} (fuso {1:+0.#;-0.#;0}), em {2:0.0000}°, {3:0.0000}°: ", descrito, periodo.Fuso, lugar.Latitude, lugar.Longitude));
        detalhe.Append(Tr.F("{0} mesa(s) no escopo, {1} mesa(s) e {2} árvore(s) fazendo sombra, {3} instante(s), {4} com sol; ",
            recebem.Count, fazem.Count, arvoresQueFazem, pior.Instants, pior.InstantsWithSun));
        detalhe.Append(Tr.F("{0} módulo(s) com sombra: {1} por árvore, {2} por outra mesa, {3} pelo terreno.", porModulo.Count, Por(ShadowCause.Object), Por(ShadowCause.Table), Por(ShadowCause.Terrain)));

        if (!queFaz.Tables || !queFaz.Trees || !queFaz.Terrain)
            detalhe.Append(Tr.F(" Desligado em \"Fazem sombra\": {0}.", string.Join(", ", new[]
            {
                queFaz.Tables ? null : Tr.T("as mesas"),
                queFaz.Trees ? null : Tr.T("as árvores"),
                queFaz.Terrain ? null : Tr.T("o relevo"),
            }.Where(t => t is not null))));

        detalhe.Append(relevo is null
            ? queFaz.Terrain ? Tr.T(" O relevo não tem como fazer sombra no período (o sol fica mais alto que o chão mais íngreme).") : string.Empty
            : relevo.AnyShadow
                ? Tr.F(" O relevo fez sombra (conferido em {0} de {1} passo(s)).", relevo.SweptSteps, relevo.Steps)
                : Tr.F(" O relevo não fez sombra (conferido em {0} de {1} passo(s)).", relevo.SweptSteps, relevo.Steps));

        detalhe.Append(Tr.F(" Desenhado em magenta: {0} hachura(s) e {1} borda(s) no chão, a sombra sobre {2} mesa(s), {3} etiqueta(s); a união das sombras de {4} passo(s).",
            feito.HachurasNoChao, feito.BordasNoChao, feito.Mesas, feito.Etiquetas, passos.Count));

        if (feito.CotaMinima is { } zMin && feito.CotaMaxima is { } zMax)
            detalhe.Append(Tr.F(" Cota das hachuras do chão: {0:0.00} a {1:0.00} m (4 cm acima do terreno).", zMin, zMax));

        if (apagadas > 0 || devolvidos > 0)
            detalhe.Append(Tr.F(" Antes: {0} entidade(s) de sombra do escopo apagada(s); {1} módulo(s) pintado(s) por desenho antigo de volta à cor de antes.", apagadas, devolvidos));

        if (feito.Recusadas > 0)
            detalhe.Append(Tr.F(" ATENÇÃO: {0} pedaço(s) o AutoCAD não hachurou (veja o registro de diagnóstico).", feito.Recusadas));

        return new RelatoDasSombras(resumo, detalhe.ToString());
    }

    // ------------------------------------------------------------ apagar, ocultar, mostrar

    /// <summary>
    /// Apaga as sombras do escopo (hachuras, bordas e etiquetas dele), tira os
    /// motivos das mesas dele e devolve a cor de antes dos módulos dele
    /// pintados por desenho antigo. Uma transação.
    /// </summary>
    internal static RelatoDasSombras Apagar(Document documento, ShadowScope escopo)
    {
        if (escopo.WhyInvalid() is { } falta) return new RelatoDasSombras(falta);

        var database = documento.Database;
        var mesas = LerMesas(database, comFaces: false);
        var areas = AreasDoDesenho.Varrer(database);
        var plano = ShadowScopePlan.Build(escopo, ParaOEscopo(areas), mesas.Select(m => new ShadowTable(m.Id, m.Contorno)).ToList(), FolgaDaSelecao);
        var noEscopo = Enumerable.Range(0, mesas.Count).Where(k => plano.Receives[k]).Select(k => mesas[k].Id).ToHashSet();

        var motivos = LerMotivos(database);
        var pintadas = LerPintadas(database, out var temPintadas);
        int apagadas, devolvidos;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            apagadas = ApagarDoEscopo(transacao, database, plano, Legado(areas, mesas));
            (devolvidos, var sobram) = DevolverPintadas(transacao, database, pintadas, noEscopo);

            if (temPintadas) GravarPintadas(database, sobram);
            GravarMotivos(database, escopo.Kind == ShadowScopeKind.Drawing ? ShadowReasons.Empty : motivos.Remove(noEscopo.Contains));
            transacao.Commit();
        }

        Avisar(documento);

        return new RelatoDasSombras(Tr.F("{0} entidade(s) de sombra apagada(s) {1}; {2} módulo(s) de volta à cor de antes.", apagadas, NomeDoEscopo(escopo, areas), devolvidos));
    }

    /// <summary>Liga (mostrar) ou desliga (ocultar) as camadas das sombras. O relato.</summary>
    internal static RelatoDasSombras Ligar(Document documento, bool ligadas)
    {
        var database = documento.Database;
        int mudaram;

        using (var transacao = database.TransactionManager.StartTransaction())
        {
            var existe = LayoutLayers.EstaDesligada(transacao, database, LayoutLayers.Sombra) is not null
                || LayoutLayers.EstaDesligada(transacao, database, LayoutLayers.SombraTexto) is not null;
            if (!existe) return new RelatoDasSombras(Tr.T("Ainda não há sombra desenhada neste desenho."));

            mudaram = DesenhoDasSombras.Ligar(transacao, database, ligadas);
            transacao.Commit();
        }

        var camadas = $"{LayoutLayers.Sombra}, {LayoutLayers.SombraTexto}";
        return new RelatoDasSombras(ligadas
            ? mudaram > 0 ? Tr.F("Sombras à vista (camadas {0} ligadas).", camadas) : Tr.F("As sombras já estavam à vista ({0}).", camadas)
            : mudaram > 0 ? Tr.F("Sombras escondidas (camadas {0} desligadas); Mostrar sombras traz de volta.", camadas) : Tr.F("As sombras já estavam escondidas ({0}).", camadas));
    }

    private static void LigarPeloComando(bool ligadas)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            documento.Editor.WriteMessage($"\n{Ligar(documento, ligadas)}\n");
            if (ClivusExtension.TemInterface()) documento.Editor.Regen();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ligar ou desligar as sombras.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui mudar as camadas das sombras: {0}\n", erro.Message));
        }
    }

    /// <summary>Apaga, na transação, as entidades de sombra que o escopo leva. Quantas.</summary>
    private static int ApagarDoEscopo(Transaction transacao, Database database, ShadowScopePlan plano, Func<ShadowPlace, Point3, ShadowTag> legado)
    {
        var apagadas = 0;

        foreach (var marca in DesenhoDasSombras.Marcas(transacao, database, legado))
        {
            if (!plano.Erases(marca.Tag)) continue;

            // Camada travada não impede.
            if (transacao.GetObject(marca.Id, OpenMode.ForWrite, false, true) is not Entity e) continue;
            e.Erase();
            apagadas++;
        }

        return apagadas;
    }

    /// <summary>
    /// A marca de uma sombra de antes de 31/10/2026 (sem área no XData), pela
    /// posição: a área (a menor que contém o centro), o ladrilho e, sobre a
    /// mesa, a mesa que contém o centro.
    /// </summary>
    private static Func<ShadowPlace, Point3, ShadowTag> Legado(IReadOnlyList<AreaNoDesenho> areas, IReadOnlyList<MesaLida> mesas)
    {
        var entradas = areas.Select(a => new AreaInput(a.Identity.Id, a.Identity.DisplayName, a.Contorno, false)).ToList();

        return (onde, centro) =>
        {
            Guid? area = AreaSummary.AreaOf([centro], entradas) is { } i ? areas[i].Identity.Id : null;
            Guid? mesa = onde == ShadowPlace.Table ? mesas.FirstOrDefault(m => Polygons.Contains(m.Contorno, centro.X, centro.Y))?.Id : null;
            return new ShadowTag(onde, area, mesa, GroundPatches.TileOf(centro.X, centro.Y));
        };
    }

    /// <summary>
    /// Os módulos pintados por desenho antigo das mesas dadas voltam à cor de
    /// antes (só a cor que a sombra pôs: repintado depois, fica). Quantos, e
    /// as linhas do registro que sobram (as das outras mesas).
    /// </summary>
    private static (int Devolvidos, List<string> Sobram) DevolverPintadas(Transaction transacao, Database database, IReadOnlyList<string> pintadas, IReadOnlySet<Guid> mesas)
    {
        var sobram = new List<string>();
        var devolvidos = 0;

        foreach (var texto in pintadas)
        {
            var partes = texto.Split('=', 2);
            if (partes.Length != 2 || !long.TryParse(partes[0], NumberStyles.HexNumber, Invariante, out var numero)) continue;
            if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;   // o módulo sumiu: sai do registro
            if (transacao.GetObject(id, OpenMode.ForRead, false, true) is not Entity modulo) continue;

            if (LayoutXData.LoadModule(modulo) is not { } identidade || !mesas.Contains(identidade.Table))
            {
                sobram.Add(texto);
                continue;
            }

            if (!EDaSombra(modulo.Color)) continue;

            modulo.UpgradeOpen();
            modulo.Color = PecasPintadas.CorDe(partes[1]);
            devolvidos++;
        }

        return (devolvidos, sobram);
    }

    /// <summary>Se a cor é uma das da marca de sombra de antes (as roxas e as de antes de 03/10/2026).</summary>
    private static bool EDaSombra(Color cor) =>
        cor.ColorMethod == ColorMethod.ByColor
        && new[] { 0.1, 0.4, 0.9 }.Select(CorDaFracao).Concat(CoresAntigas).Any(c => c.R == cor.Red && c.G == cor.Green && c.B == cor.Blue);

    // ------------------------------------------------------------ registros

    /// <summary>Os módulos pintados por desenho antigo ("handle=cor"), e se o registro existe (o desenho novo nem ganha a chave).</summary>
    private static List<string> LerPintadas(Database database, out bool existe)
    {
        existe = PluginDictionary.Contains(database, ChaveDasPintadas);
        if (!existe) return [];

        using var dados = PluginDictionary.Load(database, ChaveDasPintadas);
        return dados?.AsArray().Select(v => v.Value as string ?? string.Empty).Where(t => t.Length > 0 && t != "V1").ToList() ?? [];
    }

    private static void GravarPintadas(Database database, IReadOnlyList<string> pintadas) =>
        PluginDictionary.Save(database, ChaveDasPintadas, new ResultBuffer(
            pintadas.Prepend("V1").Select(t => new TypedValue((int)DxfCode.Text, t)).ToArray()));

    /// <summary>O registro dos motivos; no V1, a mesa de cada módulo sai do bloco dele.</summary>
    private static ShadowReasons LerMotivos(Database database)
    {
        List<string> linhas;
        using (var dados = PluginDictionary.Load(database, ChaveDosMotivos))
            linhas = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList() ?? [];

        if (linhas.Count == 0 || linhas[0] != "V1") return ShadowReasons.Parse(linhas);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var motivos = ShadowReasons.Parse(linhas, handle =>
            long.TryParse(handle, NumberStyles.HexNumber, Invariante, out var numero)
            && database.TryGetObjectId(new Handle(numero), out var id) && !id.IsErased
            && transacao.GetObject(id, OpenMode.ForRead) is Entity e && LayoutXData.LoadModule(e) is { } m
                ? m.Table
                : null);
        transacao.Commit();
        return motivos;
    }

    private static void GravarMotivos(Database database, ShadowReasons motivos) =>
        PluginDictionary.Save(database, ChaveDosMotivos, new ResultBuffer(motivos.Serialize().Select(l => new TypedValue((int)DxfCode.Text, l)).ToArray()));

    // ------------------------------------------------------------ por quê e análises

    /// <summary>O porquê da peça clicada: o bloco do módulo, ou a face (que aponta o módulo).</summary>
    private static string PorQueDaPeca(Database database, ObjectId peca)
    {
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var entidade = transacao.GetObject(peca, OpenMode.ForRead) as Entity;

            if (entidade is BlockReference br && LayoutXData.LoadModule(br) is not null) return PorQue(database, peca);

            if (entidade is Face face && LayoutXData.LoadFace(face) is { } f)
            {
                var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
                foreach (ObjectId id in espaco)
                {
                    if (id.IsErased || id.ObjectClass.DxfName != "INSERT") continue;
                    if (transacao.GetObject(id, OpenMode.ForRead) is BlockReference b && LayoutXData.LoadModule(b) is { } m && m.Id == f.Module)
                        return PorQue(database, id);
                }
            }
        }

        return Tr.T("SOMBRA Isso não é um módulo do plugin: clique no módulo ou na face dele.");
    }

    /// <summary>
    /// "Por que essa sombra?" (05/10/2026): o que o último cálculo que pegou
    /// o módulo guardou para ele — fração, causa e quando —, ou que ele ficou
    /// sem sombra, ou que a mesa dele ainda não entrou num cálculo. A frase
    /// pronta para a tela.
    /// </summary>
    internal static string PorQue(Database database, ObjectId modulo)
    {
        var motivos = LerMotivos(database);
        if (motivos.IsEmpty) return Tr.T("SOMBRA Não há cálculo de sombra guardado neste desenho: rode Sombras primeiro.");

        var rotulo = Rotulo(database, modulo, out var mesa);

        if (motivos.Find(modulo.Handle.ToString()) is { } m)
        {
            var causa = ShadowTopics.CauseName(m.Cause);
            return m.When is { } t
                ? Tr.F("SOMBRA {0}: {1:0}% da face na sombra, por {2}, em {3:dd/MM/yyyy HH:mm} (o pior momento dele em {4}).", rotulo, m.Fraction * 100, causa, t, m.Period)
                : Tr.F("SOMBRA {0}: {1:0}% da face na sombra, por {2} ({3}).", rotulo, m.Fraction * 100, causa, m.Period);
        }

        var periodo = mesa is { } g ? motivos.PeriodOf(g) : motivos.LegacyPeriod;
        return periodo is not null
            ? Tr.F("SOMBRA {0}: sem sombra no último cálculo ({1}).", rotulo, periodo)
            : Tr.F("SOMBRA {0}: a mesa dele ainda não entrou num cálculo de sombra; gere as sombras com ela no escopo.", rotulo);
    }

    /// <summary>Os tópicos da janela das análises (área › mesa › módulo), do registro dos motivos e do que o desenho diz de cada módulo.</summary>
    internal static IReadOnlyList<ShadowTopic> Analises(Document documento)
    {
        var database = documento.Database;
        var motivos = LerMotivos(database);
        if (motivos.Modules.Count == 0) return [];

        var areas = AreasDoDesenho.Varrer(database);
        var entradasDasAreas = areas.Select(a => new AreaInput(a.Identity.Id, a.Identity.DisplayName, a.Contorno, false)).ToList();
        var entradas = new List<ShadowTopicInput>();
        var areaDaMesa = new Dictionary<Guid, AreaNoDesenho?>();

        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            var partes = LayoutScan.Tables(transacao, database);

            foreach (var m in motivos.Modules)
            {
                if (!long.TryParse(m.Handle, NumberStyles.HexNumber, Invariante, out var numero)) continue;
                if (!database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) continue;   // o módulo sumiu
                if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e || LayoutXData.LoadModule(e) is not { } modulo) continue;

                partes.TryGetValue(modulo.Table, out var mesa);

                // A área da mesa pelo centro do contorno, uma vez por mesa.
                if (!areaDaMesa.TryGetValue(modulo.Table, out var area))
                {
                    var contorno = mesa?.Contour is { } c && !c.IsErased && transacao.GetObject(c, OpenMode.ForRead) is Polyline3d linha
                        ? FileiraCommands.Vertices(linha, transacao)
                        : null;
                    area = contorno is not null && AreaSummary.AreaOf(contorno, entradasDasAreas) is { } i ? areas[i] : null;
                    areaDaMesa[modulo.Table] = area;
                }

                entradas.Add(new ShadowTopicInput(
                    m with { Table = modulo.Table }, area?.Identity.DisplayName, area?.Handle, mesa?.Identity?.Label ?? "?",
                    mesa?.Contour is { } contornoId ? contornoId.Handle.ToString() : null, modulo.Column, modulo.Row));
            }

            transacao.Commit();
        }

        return ShadowTopics.Build(entradas);
    }

    /// <summary>Mostra no desenho a entidade do tópico (área, mesa ou módulo): zoom nela e, no módulo e na mesa, a seleção. Falso se ela sumiu.</summary>
    internal static bool Mostrar(Document documento, string handle)
    {
        var database = documento.Database;
        if (!long.TryParse(handle, NumberStyles.HexNumber, Invariante, out var numero) || !database.TryGetObjectId(new Handle(numero), out var id) || id.IsErased) return false;

        Extents3d caixa;
        bool selecionar;
        using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) return false;

            try
            {
                caixa = e.GeometricExtents;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }

            selecionar = AreaXData.Load(e) is null;
            transacao.Commit();
        }

        // Pelo menos uns 25 m na tela: o módulo sozinho não diz onde está.
        var lado = Math.Max(caixa.MaxPoint.X - caixa.MinPoint.X, caixa.MaxPoint.Y - caixa.MinPoint.Y);
        using (documento.LockDocument())
        {
            LocalDosInversores.Zoom(documento.Editor, caixa, folga: Math.Max(1.15, 25 / Math.Max(1, lado)));
            if (selecionar) documento.Editor.SetImpliedSelection([id]);
        }

        return true;
    }

    // ------------------------------------------------------------ leitura do desenho

    /// <summary>Uma mesa do desenho para as sombras: identidade, contorno, as faces dos módulos e as peças (para a ordem de desenho).</summary>
    private sealed record MesaLida(Guid Id, string Letreiro, IReadOnlyList<Point3> Contorno, IReadOnlyList<FaceLida> Faces, IReadOnlyList<ObjectId> Pecas);

    /// <summary>A face de um módulo: os cantos, o bloco do módulo, a coluna e a fila.</summary>
    private sealed record FaceLida(IReadOnlyList<Point3> Cantos, ObjectId Modulo, int Coluna, int Fila);

    /// <summary>Uma árvore: os cilindros, o pé em planta, o topo e o maior raio.</summary>
    private sealed record ArvoreLida(IReadOnlyList<ShadowCylinder> Cilindros, double X, double Y, double Topo, double Raio);

    /// <summary>As mesas do desenho com contorno (as faces só se pedidas): o contorno dá o plano, a área e o letreiro.</summary>
    private static List<MesaLida> LerMesas(Database database, bool comFaces)
    {
        var mesas = new List<MesaLida>();

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        foreach (var (guid, partes) in LayoutScan.Tables(transacao, database))
        {
            if (partes.Identity is not { } identidade || partes.Contour is not { } idDoContorno || idDoContorno.IsErased) continue;
            if (transacao.GetObject(idDoContorno, OpenMode.ForRead) is not Polyline3d linha) continue;

            var contorno = FileiraCommands.Vertices(linha, transacao);
            if (contorno.Count < 3 || contorno.Any(p => !p.IsFinite)) continue;

            var faces = new List<FaceLida>();
            if (comFaces)
            {
                var modulos = new Dictionary<Guid, (ObjectId Id, int Coluna, int Fila)>();
                foreach (var m in partes.Modules)
                    if (!m.IsErased && transacao.GetObject(m, OpenMode.ForRead) is BlockReference br && LayoutXData.LoadModule(br) is { } modulo)
                        modulos.TryAdd(modulo.Id, (m, modulo.Column, modulo.Row));

                foreach (var f in partes.Faces)
                {
                    if (f.IsErased || transacao.GetObject(f, OpenMode.ForRead) is not Face face || LayoutXData.LoadFace(face) is not { } identidadeDaFace) continue;
                    if (!modulos.TryGetValue(identidadeDaFace.Module, out var dono)) continue;

                    var cantos = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
                    faces.Add(new FaceLida(cantos, dono.Id, dono.Coluna, dono.Fila));
                }
            }

            mesas.Add(new MesaLida(guid, identidade.Label, contorno, faces, [.. partes.Modules, .. partes.Faces, .. partes.Contours]));
        }

        transacao.Commit();
        return mesas;
    }

    private static List<ArvoreLida> LerArvores(Database database)
    {
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        var arvores = ArvoreCommands.Ler(transacao, database)
            .Select(a =>
            {
                var cilindros = a.Arvore.Spec.Cylinders(a.Pe.X, a.Pe.Y, a.Pe.Z);
                return new ArvoreLida(cilindros, a.Pe.X, a.Pe.Y, cilindros.Max(c => c.Top), cilindros.Max(c => c.Radius));
            })
            .ToList();

        transacao.Commit();
        return arvores;
    }

    private static List<ShadowArea> ParaOEscopo(IReadOnlyList<AreaNoDesenho> areas) =>
        areas.Select(a => new ShadowArea(a.Identity.Id, a.Identity.DisplayName, a.Contorno)).ToList();

    /// <summary>A cota mais baixa de quem recebe: o terreno numa grade de 10 m na caixa e os contornos das mesas do escopo.</summary>
    private static double CotaMaisBaixa(Tin terreno, (double MinX, double MinY, double MaxX, double MaxY) caixa, IEnumerable<IReadOnlyList<Point3>> mesas)
    {
        var minimo = double.PositiveInfinity;

        foreach (var p in mesas.SelectMany(m => m)) minimo = Math.Min(minimo, p.Z);

        var nx = Math.Clamp((int)Math.Ceiling((caixa.MaxX - caixa.MinX) / 10), 1, 300);
        var ny = Math.Clamp((int)Math.Ceiling((caixa.MaxY - caixa.MinY) / 10), 1, 300);
        for (var j = 0; j <= ny; j++)
            for (var i = 0; i <= nx; i++)
                if (terreno.TryGetZ(caixa.MinX + (caixa.MaxX - caixa.MinX) * i / nx, caixa.MinY + (caixa.MaxY - caixa.MinY) * j / ny, out var z))
                    minimo = Math.Min(minimo, z);

        return double.IsFinite(minimo) ? minimo : terreno.MinZ;
    }

    /// <summary>O escopo com a preposição, para o resumo: "de Área 1", "da seleção", "do desenho inteiro".</summary>
    internal static string NomeDoEscopo(ShadowScope escopo, IReadOnlyList<AreaNoDesenho> areas)
    {
        string Areas() => string.Join(", ", escopo.Areas.Select(id => areas.FirstOrDefault(a => a.Identity.Id == id)?.Identity.DisplayName ?? "?"));

        return escopo.Kind switch
        {
            ShadowScopeKind.Selection => Tr.T("da seleção"),
            ShadowScopeKind.Area or ShadowScopeKind.Areas => Tr.F("de {0}", Areas()),
            ShadowScopeKind.AreaAndSelection => Tr.F("de {0} e da seleção", Areas()),
            _ => Tr.T("do desenho inteiro"),
        };
    }

    /// <summary>O texto da etiqueta: o instante, o dia ou o período (com o horário dos passos).</summary>
    private static string Rotulo(PeriodoDeSombra periodo, IReadOnlyList<DateTime> passos)
    {
        if (passos.Count == 0) return Tr.T("Sombra");

        var dias = passos.Select(t => t.Date).Distinct().Count();
        var (de, ate) = (passos[0].Date + passos.Min(t => t.TimeOfDay), passos[0].Date + passos.Max(t => t.TimeOfDay));

        return periodo.Instante
            ? Tr.F("Sombra {0:dd/MM HH:mm}", passos[0])
            : dias == 1
                ? Tr.F("Sombra {0:dd/MM} {1:HH:mm}–{2:HH:mm}", passos[0], de, ate)
                : Tr.F("Sombra {0:dd/MM}–{1:dd/MM} {2:HH:mm}–{3:HH:mm}", passos[0], passos[^1], de, ate);
    }

    /// <summary>"F1.3 col 2 fil 1", e a mesa do módulo.</summary>
    private static string Rotulo(Database database, ObjectId modulo, out Guid? mesa)
    {
        mesa = null;
        using var transacao = database.TransactionManager.StartOpenCloseTransaction();

        if (transacao.GetObject(modulo, OpenMode.ForRead) is not Entity e || LayoutXData.LoadModule(e) is not { } m) return "?";

        mesa = m.Table;
        var mesas = LayoutScan.Tables(transacao, database);
        var letreiro = mesas.TryGetValue(m.Table, out var p) ? p.Identity?.Label ?? "?" : "?";
        return Tr.F("{0} col {1} fil {2}", letreiro, m.Column + 1, m.Row + 1);
    }
}
