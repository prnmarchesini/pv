using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.NumeracaoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A numeração das strings (elétrica, etapa 15). CLIVUS_NUMERACAO abre a aba
/// Numeração numa janela solta (a mesma aba da janela da configuração
/// elétrica); CLIVUS_NUMERACAO_AUTO faz o mesmo pela linha de comando, para o
/// nível 2.
/// </summary>
public static class NumeracaoCommands
{
    [CommandMethod(PluginInfo.ComandoNumeracao)]
    public static void Numeracao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela da numeração precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaDeNumeracao.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela da numeração.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir a numeração: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_NUMERACAO_MESAS (05/10/2026), o "Selecionar" da linha do bloco:
    /// pergunta o bloco (nome ou GUID) e a seleção das mesas dele, que
    /// substituem as de antes (só mesa entra). A seleção é pedida aqui, dentro
    /// de um comando: do clique da janela solta (fora de comando) o AutoCAD
    /// não deixava selecionar. As mesas que já são do bloco ficam destacadas
    /// enquanto o usuário seleciona. No fim a janela que pediu volta.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoNumeracaoMesas)]
    public static void Mesas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        string? frase = null;
        var erro = false;
        var destacadas = Array.Empty<ObjectId>();

        try
        {
            if (PerguntarBloco(editor, documento.Database) is not { } bloco)
            {
                frase = Tr.T("Nenhum bloco escolhido; nada mudou.");
                return;
            }

            destacadas = PainelDeNumeracao.ContornosDoBloco(documento.Database, bloco.Id);
            Destacar(documento.Database, destacadas, true);

            var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nSelecione as mesas do {0} (só mesas entram; Enter termina): ", bloco.Name) }, PainelDeNumeracao.FiltroDeMesas);
            if (selecao.Status != PromptStatus.OK)
            {
                frase = Tr.F("Seleção cancelada; {0} ficou como estava.", bloco.Name);
                return;
            }

            frase = PainelDeNumeracao.GravarMesas(documento, bloco, selecao.Value.GetObjectIds());
            editor.WriteMessage(Tr.F("\nNUMERACAO {0}\n", frase));
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao selecionar as mesas do bloco.", falha);
            frase = Tr.F("Não consegui: {0}", falha.Message);
            erro = true;
            editor.WriteMessage(Tr.F("\nNão consegui selecionar as mesas: {0}\n", falha.Message));
        }
        finally
        {
            Destacar(documento.Database, destacadas, false);
            PainelDeNumeracao.Retomar(documento, frase, erro);
        }
    }

    /// <summary>
    /// CLIVUS_NUMERACAO_MOSTRAR, o "Mostrar" da linha do bloco: as mesas do
    /// bloco (nome ou GUID) ficam selecionadas no desenho (seleção implícita).
    /// Nada é gravado.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoNumeracaoMostrar, CommandFlags.Modal | CommandFlags.Redraw | CommandFlags.NoUndoMarker)]
    public static void Mostrar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (PerguntarBloco(editor, documento.Database) is not { } bloco) return;

            var ids = PainelDeNumeracao.ContornosDoBloco(documento.Database, bloco.Id);
            editor.SetImpliedSelection(ids);
            editor.WriteMessage(ids.Length == 0
                ? Tr.F("\nNUMERACAO {0} não tem mesa no desenho.\n", bloco.Name)
                : Tr.F("\nNUMERACAO {0}: {1} mesa(s) selecionada(s).\n", bloco.Name, ids.Length));
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha ao mostrar as mesas do bloco.", falha);
            editor.WriteMessage(Tr.F("\nNão consegui mostrar as mesas: {0}\n", falha.Message));
        }
    }

    /// <summary>O bloco pelo nome ou pelo GUID (o que o botão manda); null e aviso se não há.</summary>
    internal static NumberingBlock? PerguntarBloco(Editor editor, Database database)
    {
        var qual = editor.GetString(new PromptStringOptions(Tr.T("\nBloco (nome): ")) { AllowSpaces = true });
        if (qual.Status != PromptStatus.OK) return null;

        var texto = qual.StringResult.Trim();
        var blocos = NumeracaoStore.Varredura(database).Varredura.Blocks;
        var bloco = Guid.TryParse(texto, out var id)
            ? blocos.FirstOrDefault(b => b.Id == id)
            : blocos.FirstOrDefault(b => string.Equals(b.Name, texto, StringComparison.CurrentCultureIgnoreCase));

        if (bloco is null) editor.WriteMessage(Tr.F("\nNUMERACAO Não há bloco chamado \"{0}\".\n", texto));
        return bloco;
    }

    /// <summary>Liga ou desliga o destaque do AutoCAD nas entidades (só tela; nada é gravado).</summary>
    private static void Destacar(Database database, IReadOnlyList<ObjectId> ids, bool ligar)
    {
        if (ids.Count == 0 || !ClivusExtension.TemInterface()) return;

        try
        {
            using var transacao = database.TransactionManager.StartOpenCloseTransaction();
            foreach (var id in ids)
            {
                if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                if (ligar) e.Highlight();
                else e.Unhighlight();
            }
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Não consegui destacar as mesas do bloco.", falha);
        }
    }

    /// <summary>
    /// [Tag/Usina/Faixa/Bloco/Gerar/RegerarBloco/RefazerInversor/ApagarTags/Strings/Listar],
    /// para o nível 2. Gerar numera a usina (15.4); RegerarBloco e
    /// RefazerInversor pedem o nome e numeram só aquele pedaço; ApagarTags
    /// pede [Tudo/Inversor] (15.5); Strings lista cada string. Tag pede a composição em uma
    /// linha, "trafo|inversor|string|separador" (trafo "-" tira o pedaço do
    /// trafo; ex. "T|I|S|." ou "-||S|"). Usina pede o sentido da usina
    /// inteira; Faixa, o sentido na faixa dela. Bloco pede
    /// [Novo/Mesas/Sentido/Faixa/Subir/Descer/Renomear/Apagar] e o nome do
    /// bloco (Mesas pede a seleção; Sentido e Faixa, o sentido; Renomear, o
    /// nome novo). Listar só mostra. Toda opção termina mostrando o que está
    /// gravado.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoNumeracaoAutomatico)]
    public static void NumeracaoAutomatica()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var opcao = Palavra(editor, Tr.T("\nNumeração [Tag/Usina/Faixa/Bloco/Gerar/RegerarBloco/RefazerInversor/ApagarTags/Strings/Listar]: "),
                "Tag", "Usina", "Faixa", "Bloco", "Gerar", "RegerarBloco", "RefazerInversor", "ApagarTags", "Strings", "Listar");
            if (opcao is null) return;

            var database = documento.Database;

            switch (opcao)
            {
                case "Gerar":
                    Escrever(editor, NumeracaoDesenho.Gerar(database));
                    return;

                case "RegerarBloco":
                    if (AcharBloco(editor, database) is not { } bloco) return;
                    Escrever(editor, NumeracaoDesenho.Gerar(database, NumberingScope.OfBlock(bloco.Id)));
                    return;

                case "RefazerInversor":
                    if (AcharInversor(editor, database) is not { } inversor) return;
                    Escrever(editor, NumeracaoDesenho.Gerar(database, NumberingScope.OfInverter(inversor.Id)));
                    return;

                case "ApagarTags":
                    var quais = Palavra(editor, Tr.T("\nApagar as tags [Tudo/Inversor]: "), "Tudo", "Inversor");
                    if (quais is null) return;

                    NumberingScope? alcance = NumberingScope.All;
                    if (quais == "Inversor") alcance = AcharInversor(editor, database) is { } doInversor ? NumberingScope.OfInverter(doInversor.Id) : null;
                    if (alcance is null) return;

                    editor.WriteMessage(Tr.F("\nNUMERACAO {0}\n", NumeracaoDesenho.Apagar(database, alcance)));
                    return;

                case "Strings":
                    ListarStrings(editor, database);
                    return;

                case "Tag":
                    var texto = editor.GetString(new PromptStringOptions(Tr.T("\nComposição (trafo|inversor|string|separador): ")) { AllowSpaces = true });
                    if (texto.Status != PromptStatus.OK) return;

                    var partes = texto.StringResult.Split('|');
                    if (partes.Length != 4)
                    {
                        editor.WriteMessage(Tr.T("\nNUMERACAO A composição tem quatro partes separadas por |.\n"));
                        break;
                    }

                    var esquema = new TagScheme(partes[0] != "-", partes[0] == "-" ? string.Empty : partes[0], partes[1], partes[2], partes[3]);
                    if (esquema.Problem() is { } problema) editor.WriteMessage(Tr.F("\nNUMERACAO Não salvei: {0}.\n", problema));
                    else NumeracaoStore.GravarEsquema(database, esquema);
                    break;

                case "Usina":
                    if (PerguntarSentido(editor) is not { } daUsina) return;
                    NumeracaoStore.MudarVarredura(database, v => v.DefaultDirection = daUsina);
                    break;

                case "Faixa":
                    if (PerguntarSentido(editor) is not { } faixaDaUsina) return;
                    var aceitou = false;
                    NumeracaoStore.MudarVarredura(database, v => aceitou = v.SetDefaultCross(faixaDaUsina));
                    if (!aceitou) editor.WriteMessage(Tr.T("\nNUMERACAO Recusado: o sentido na faixa tem que ser perpendicular ao que avança.\n"));
                    break;

                case "Bloco":
                    if (!Bloco(documento)) return;
                    break;
            }

            Listar(editor, database);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_NUMERACAO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na numeração: {0}\n", erro.Message));
        }
    }

    /// <summary>O relatório na linha de comando: a primeira linha com o prefixo NUMERACAO.</summary>
    private static void Escrever(Editor editor, IReadOnlyList<string> linhas)
    {
        for (var i = 0; i < linhas.Count; i++)
            editor.WriteMessage(i == 0 ? Tr.F("\nNUMERACAO {0}\n", linhas[i]) : linhas[i] + "\n");
    }

    /// <summary>
    /// Para o nível 2: cada string com a tag gravada, o inversor (do
    /// vínculo), o bloco da mesa do primeiro módulo (0 = fora de bloco) e a
    /// posição dele, em números invariantes.
    /// </summary>
    private static void ListarStrings(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var (varredura, _) = NumeracaoStore.Varredura(database);
        var inversores = ElectricalStore.Inverters(database).Items;
        var donoDaMesa = varredura.BlockByTable();
        var posicaoDoBloco = varredura.Blocks.Select((b, i) => (b.Id, i + 1)).ToDictionary(x => x.Id, x => x.Item2);

        using var transacao = database.TransactionManager.StartOpenCloseTransaction();
        var modulos = NumeracaoDesenho.Modulos(transacao, database);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var (_, s) in ElectricalStore.Strings(transacao, database))
        {
            var nome = inversores.FirstOrDefault(i => i.Id == s.Inverter)?.Name.Replace(' ', '_') ?? "-";
            if (!modulos.TryGetValue(s.Modules[0], out var lugar))
            {
                editor.WriteMessage($"NUMERACAO_STRING tag={(s.Tag.Length > 0 ? s.Tag : "-")} inversor={nome} bloco=- x=- y=-\n");
                continue;
            }

            var bloco = donoDaMesa.TryGetValue(lugar.Mesa, out var b) ? posicaoDoBloco[b] : 0;
            editor.WriteMessage(string.Format(inv, "NUMERACAO_STRING tag={0} inversor={1} bloco={2} x={3:0.###} y={4:0.###}\n", s.Tag.Length > 0 ? s.Tag : "-", nome, bloco, lugar.Centro.X, lugar.Centro.Y));
        }
    }

    /// <summary>A composição, o sentido da usina e os blocos gravados, na linha de comando.</summary>
    private static void Listar(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var (esquema, problemaDoEsquema) = NumeracaoStore.Esquema(database);
        editor.WriteMessage(Tr.F("\nNUMERACAO tag {0}\n", PainelDeNumeracao.Exemplo(esquema)));
        if (problemaDoEsquema is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problemaDoEsquema));

        var (varredura, problemaDaVarredura) = NumeracaoStore.Varredura(database);
        editor.WriteMessage(Tr.F("NUMERACAO usina {0}; {1} bloco(s); na faixa, {2}\n", ScanOrder.Describe(varredura.DefaultDirection), varredura.Blocks.Count, ScanOrder.Describe(varredura.DefaultCross)));
        var contagem = PainelDeNumeracao.Contar(database, varredura);
        for (var i = 0; i < varredura.Blocks.Count; i++)
            editor.WriteMessage(Tr.F("NUMERACAO bloco {0}; {1}\n", PainelDeNumeracao.Descrever(varredura.Blocks[i], i + 1), PainelDeNumeracao.ResumoDoBloco(varredura.Blocks[i], contagem)));
        editor.WriteMessage(Tr.F("NUMERACAO resumo {0}\n", PainelDeNumeracao.ResumoGeral(varredura, contagem)));
        if (problemaDaVarredura is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problemaDaVarredura));
    }

    private static bool Bloco(Document documento)
    {
        var editor = documento.Editor;
        var database = documento.Database;

        var acao = Palavra(editor, Tr.T("\nBloco [Novo/Mesas/Sentido/Faixa/Subir/Descer/Renomear/Apagar]: "), "Novo", "Mesas", "Sentido", "Faixa", "Subir", "Descer", "Renomear", "Apagar");
        if (acao is null) return false;

        if (acao == "Novo")
        {
            NumberingBlock? novo = null;
            NumeracaoStore.MudarVarredura(database, v => novo = v.AddBlock());
            editor.WriteMessage(Tr.F("\nNUMERACAO {0} criado.\n", novo!.Name));
            return true;
        }

        var bloco = AcharBloco(editor, database);
        if (bloco is null) return true;

        switch (acao)
        {
            case "Mesas":
                var selecao = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = Tr.F("\nSelecione as mesas do {0} (só mesas entram): ", bloco.Name) }, PainelDeNumeracao.FiltroDeMesas);
                if (selecao.Status != PromptStatus.OK) return false;
                editor.WriteMessage(Tr.F("\nNUMERACAO {0}\n", PainelDeNumeracao.GravarMesas(documento, bloco, selecao.Value.GetObjectIds())));
                break;

            case "Sentido":
                if (PerguntarSentido(editor) is not { } sentido) return false;
                NumeracaoStore.MudarVarredura(database, v => v.SetDirection(bloco.Id, sentido));
                break;

            case "Faixa":
                if (PerguntarSentido(editor) is not { } faixa) return false;
                var aceitou = false;
                NumeracaoStore.MudarVarredura(database, v => aceitou = v.SetCross(bloco.Id, faixa));
                if (!aceitou) editor.WriteMessage(Tr.T("\nNUMERACAO Recusado: o sentido na faixa tem que ser perpendicular ao que avança.\n"));
                break;

            case "Subir":
            case "Descer":
                var andou = false;
                NumeracaoStore.MudarVarredura(database, v => andou = v.Move(bloco.Id, acao == "Subir" ? -1 : +1));
                if (!andou) editor.WriteMessage(Tr.F("\nNUMERACAO {0} não andou: já está na ponta da lista.\n", bloco.Name));
                break;

            case "Renomear":
                var novoNome = editor.GetString(new PromptStringOptions(Tr.T("\nNome novo: ")) { AllowSpaces = true });
                if (novoNome.Status != PromptStatus.OK) return false;
                string? porque = null;
                NumeracaoStore.MudarVarredura(database, v => porque = v.Rename(bloco.Id, novoNome.StringResult));
                if (porque is not null) editor.WriteMessage(Tr.F("\nNUMERACAO Não renomeei: {0}.\n", porque));
                break;

            case "Apagar":
                NumeracaoStore.MudarVarredura(database, v => v.Remove(bloco.Id));
                editor.WriteMessage(Tr.F("\nNUMERACAO {0} apagado.\n", bloco.Name));
                break;
        }

        return true;
    }

    /// <summary>O bloco pelo nome (pergunta); null e aviso se não há.</summary>
    private static NumberingBlock? AcharBloco(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome do bloco: ")) { AllowSpaces = true });
        if (nome.Status != PromptStatus.OK) return null;

        var bloco = NumeracaoStore.Varredura(database).Varredura.Blocks
            .FirstOrDefault(b => string.Equals(b.Name, nome.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));

        if (bloco is null) editor.WriteMessage(Tr.F("\nNUMERACAO Não há bloco chamado \"{0}\".\n", nome.StringResult.Trim()));
        return bloco;
    }

    /// <summary>O inversor pelo nome (pergunta); null e aviso se não há.</summary>
    private static Inverter? AcharInversor(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome do inversor: ")) { AllowSpaces = true });
        if (nome.Status != PromptStatus.OK) return null;

        var inversor = ElectricalStore.Inverters(database).Items
            .FirstOrDefault(i => string.Equals(i.Name, nome.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));

        if (inversor is null) editor.WriteMessage(Tr.F("\nNUMERACAO Não há inversor chamado \"{0}\".\n", nome.StringResult.Trim()));
        return inversor;
    }

    private static readonly (string Palavra, ScanDirection Sentido)[] PalavrasDoSentido =
    [
        ("EsquerdaDireita", ScanDirection.LeftToRight),
        ("DireitaEsquerda", ScanDirection.RightToLeft),
        ("CimaBaixo", ScanDirection.TopToBottom),
        ("BaixoCima", ScanDirection.BottomToTop),
    ];

    private static ScanDirection? PerguntarSentido(Editor editor)
    {
        var palavra = Palavra(editor, Tr.T("\nSentido [EsquerdaDireita/DireitaEsquerda/CimaBaixo/BaixoCima]: "), PalavrasDoSentido.Select(p => p.Palavra).ToArray());
        return palavra is null ? null : PalavrasDoSentido.First(p => p.Palavra == palavra).Sentido;
    }

    private static string? Palavra(Editor editor, string pergunta, params string[] palavras)
    {
        var opcoes = new PromptKeywordOptions(pergunta) { AllowNone = false };
        foreach (var palavra in palavras) opcoes.Keywords.Add(palavra);

        var resposta = editor.GetKeywords(opcoes);
        return resposta.Status == PromptStatus.OK ? resposta.StringResult : null;
    }
}

/// <summary>A aba Numeração numa janela solta, uma por desenho (enquanto a janela da configuração elétrica não a hospeda).</summary>
internal sealed class JanelaDeNumeracao : Window
{
    private static readonly Dictionary<Document, JanelaDeNumeracao> Abertas = [];

    private JanelaDeNumeracao(Document documento)
    {
        Title = Tr.T("Numeração das strings — Clivus Solar");
        Width = 980;
        Height = 560;
        MinWidth = 600;
        MinHeight = 420;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = PainelDeNumeracao.Criar(documento);
    }

    /// <summary>Abre a janela do desenho, ou traz para a frente a que já está aberta.</summary>
    internal static void Abrir(Document documento)
    {
        if (Abertas.TryGetValue(documento, out var aberta))
        {
            if (aberta.WindowState == WindowState.Minimized) aberta.WindowState = WindowState.Normal;
            // Escondida pelo Selecionar de um bloco cujo comando não chegou a rodar: volta.
            if (!aberta.IsVisible) aberta.Show();
            aberta.Activate();
            return;
        }

        var janela = new JanelaDeNumeracao(documento);
        Abertas[documento] = janela;

        void AoFecharODesenho(object? _, DocumentCollectionEventArgs e)
        {
            if (e.Document == documento) janela.Close();
        }

        AcadApp.DocumentManager.DocumentToBeDestroyed += AoFecharODesenho;
        janela.Closed += (_, _) =>
        {
            Abertas.Remove(documento);
            AcadApp.DocumentManager.DocumentToBeDestroyed -= AoFecharODesenho;
        };

        AcadApp.ShowModelessWindow(janela);
    }
}
