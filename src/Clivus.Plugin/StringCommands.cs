using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.StringCommands))]

namespace Clivus.Plugin;

/// <summary>
/// As strings (elétrica, etapa 11): CLIVUS_STRING abre a janela;
/// CLIVUS_STRING_MESAS escolhe em campo as mesas de um tipo (11.2);
/// CLIVUS_STRING_TIPO_AUTO mexe na biblioteca pela linha de comando (nível 2).
/// </summary>
public static class StringCommands
{
    /// <summary>
    /// O pedido da janela para o próximo CLIVUS_STRING_MESAS deste desenho:
    /// o tipo cujas mesas trocar (Guid.Empty = criar um tipo novo). Sem
    /// pedido (comando digitado), cria um tipo novo.
    /// </summary>
    private static readonly Dictionary<Document, Guid> PedidosDeMesas = [];

    internal static void PedirMesas(Document documento, Guid alvo) => PedidosDeMesas[documento] = alvo;

    /// <summary>
    /// CLIVUS_STRING_MESAS (11.2): seleção em campo só de mesas, Enter; as
    /// mesas em ordem ao longo da fileira viram a assinatura de arranjo e o
    /// desenho do cartesiano do tipo. Cria o tipo, ou troca as mesas do tipo
    /// que a janela pediu.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringMesas)]
    public static void Mesas()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var database = documento.Database;
        var daJanela = PedidosDeMesas.Remove(documento, out var alvo);
        Guid? mostrar = alvo == Guid.Empty ? null : alvo;
        string? frase = null;
        var erro = false;

        try
        {
            var guids = MesasDaString.Selecionar(documento, Tr.T("\nSelecione as mesas do tipo de string (só mesas entram): "));
            if (guids is null)
            {
                frase = Tr.T("Seleção cancelada.");
                return;
            }

            var problemas = new List<string>();
            List<FieldTable> mesas;
            using (var transacao = database.TransactionManager.StartOpenCloseTransaction())
                mesas = MesasDaString.Ler(transacao, database, guids, problemas);

            foreach (var p in problemas) editor.WriteMessage(Tr.F("\n  ATENÇÃO: {0}.", p));

            if (StringFieldTables.Order(mesas, out var porque) is not { } ordem)
            {
                frase = Tr.F("Não escolhi as mesas: {0}.", porque);
                erro = true;
                return;
            }

            var (arranjo, desenho) = StringFieldTables.Describe(ordem);
            StringType? tipo = null;
            string? recusa = null;
            var descartadas = 0;

            var problema = StringTypeStore.Mudar(database, b =>
            {
                if (alvo == Guid.Empty)
                {
                    tipo = b.Add(arranjo, desenho);
                }
                else
                {
                    var antes = b.Find(alvo);
                    recusa = b.SetArrangement(alvo, arranjo, desenho);
                    tipo = b.Find(alvo);
                    if (recusa is null && antes is not null && tipo is not null) descartadas = antes.Routes.Count - tipo.Routes.Count;
                }
            });

            if (recusa is not null || tipo is null)
            {
                frase = Tr.F("Não escolhi as mesas: {0}.", recusa ?? string.Empty);
                erro = true;
                return;
            }

            mostrar = tipo.Id;
            frase = Tr.F("{0}: mesas {1} ({2}), {3} módulo(s).", tipo.Name, string.Join(", ", ordem.Select(o => o.Table.Label)), arranjo.ToText(), arranjo.ModuleCount);
            if (alvo == Guid.Empty) frase = Tr.F("{0} criado.", tipo.Name) + " " + frase;
            if (descartadas > 0) frase += " " + Tr.F("O traçado antigo ({0} string(s)) foi descartado: as mesas novas têm outra grade.", descartadas);
            if (problema is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problema));
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_MESAS.", falha);
            frase = Tr.F("Não consegui: {0}", falha.Message);
            erro = true;
        }
        finally
        {
            if (frase is not null) editor.WriteMessage("\nSTRING " + frase + "\n");

            // Pedido da janela: ela volta (com ou sem mesas). Digitado: a
            // janela aberta, se houver, mostra o tipo novo.
            if (ClivusExtension.TemInterface() && (daJanela || (!erro && mostrar is not null)))
                JanelaDeStrings.Retomar(documento, mostrar, frase, erro);
        }
    }
    [CommandMethod(PluginInfo.ComandoString)]
    public static void Strings()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        try
        {
            if (!ClivusExtension.TemInterface())
            {
                documento.Editor.WriteMessage(Tr.T("\nA janela das strings precisa da interface do Civil 3D.\n"));
                return;
            }

            JanelaDeStrings.Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela de strings.", erro);
            documento.Editor.WriteMessage(Tr.F("\nNão consegui abrir as strings: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// [Adicionar/Listar/Renomear/Apagar]: Adicionar cria um tipo sem mesas;
    /// Renomear e Apagar pedem o nome do tipo (e o novo nome).
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringTipoAutomatico)]
    public static void TipoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var pergunta = new PromptKeywordOptions(Tr.T("\nBiblioteca de strings [Adicionar/Listar/Renomear/Apagar]: ")) { AllowNone = false };
            foreach (var palavra in new[] { "Adicionar", "Listar", "Renomear", "Apagar" }) pergunta.Keywords.Add(palavra);

            var resposta = editor.GetKeywords(pergunta);
            if (resposta.Status != PromptStatus.OK) return;

            var database = documento.Database;

            switch (resposta.StringResult)
            {
                case "Adicionar":
                    StringType? novo = null;
                    var problema = StringTypeStore.Mudar(database, b => novo = b.Add(StringArrangement.Empty));
                    editor.WriteMessage(Tr.F("\nSTRING {0} criado.\n", novo!.Name));
                    if (problema is not null) editor.WriteMessage(Tr.F("  ATENÇÃO: {0}.\n", problema));
                    break;

                case "Renomear":
                    if (Achar(editor, database) is not { } tipo) return;
                    var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome novo: ")) { AllowSpaces = true });
                    if (nome.Status != PromptStatus.OK) return;
                    string? porque = null;
                    StringTypeStore.Mudar(database, b => porque = b.Rename(tipo.Id, nome.StringResult));
                    editor.WriteMessage(porque is null
                        ? Tr.F("\nSTRING {0} renomeado para {1}.\n", tipo.Name, nome.StringResult.Trim())
                        : Tr.F("\nSTRING Não renomeei: {0}.\n", porque));
                    break;

                case "Apagar":
                    if (Achar(editor, database) is not { } apagado) return;
                    StringTypeStore.Mudar(database, b => b.Remove(apagado.Id));
                    editor.WriteMessage(Tr.F("\nSTRING {0} apagado da biblioteca.\n", apagado.Name));
                    break;
            }

            var lido = StringTypeStore.Ler(database);
            editor.WriteMessage(Tr.F("STRING {0} tipo(s): {1}\n", lido.Items.Count, string.Join("; ", lido.Items.Select(JanelaDeStrings.Descrever))));

            // Para o nível 2: o desenho do cartesiano de cada tipo (células e vãos, em metro).
            foreach (var tipo in lido.Items.Where(t => t.Sketch is not null))
                editor.WriteMessage($"STRING_DESENHO {tipo.Name} {tipo.Arrangement.ToText()} {tipo.Sketch!.ToText()}\n");
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_TIPO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na biblioteca de strings: {0}\n", erro.Message));
        }
    }

    /// <summary>O pedido da janela para o próximo CLIVUS_STRING_GERAR deste desenho: os tipos que valem.</summary>
    private static readonly Dictionary<Document, IReadOnlyCollection<Guid>> PedidosDeGeracao = [];

    internal static void PedirGeracao(Document documento, IReadOnlyCollection<Guid> tipos) => PedidosDeGeracao[documento] = tipos;

    /// <summary>
    /// CLIVUS_STRING_GERAR (11.6 a 11.8): seleção em campo só de mesas,
    /// Enter; cada tipo que vale (os escolhidos na aba Gerar; digitado, todos
    /// os que têm traçado) cai nos grupos de mesas vizinhas de assinatura
    /// igual. Mesa sem tipo que case não é preenchida e é avisada pelo nome.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringGerar)]
    public static void Gerar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var daJanela = PedidosDeGeracao.Remove(documento, out var tipos);
        IReadOnlyList<string> linhas = [];
        var erro = false;

        try
        {
            var guids = MesasDaString.Selecionar(documento, Tr.T("\nSelecione as mesas onde gerar as strings (só mesas entram): "));
            if (guids is null)
            {
                linhas = [Tr.T("Seleção cancelada.")];
                return;
            }

            if (guids.Count == 0)
            {
                linhas = [Tr.F("Não gerei: {0}.", Tr.T("nenhuma mesa do plugin na seleção"))];
                erro = true;
                return;
            }

            var relatorio = GeracaoDeStrings.Gerar(documento, guids, tipos ?? []);
            linhas = relatorio.Linhas;

            // 11.8: nunca calado; além do aviso nominal, as mesas sem tipo ficam selecionadas.
            if (relatorio.MesasSemTipo.Count > 0)
            {
                GeracaoDeStrings.SelecionarSemTipo(documento, relatorio.MesasSemTipo);
                linhas = [.. linhas, Tr.F("As {0} mesa(s) sem tipo de string ficaram selecionadas no desenho.", relatorio.MesasSemTipo.Count)];
            }
        }
        catch (System.Exception falha)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_GERAR.", falha);
            linhas = [Tr.F("Não consegui: {0}", falha.Message)];
            erro = true;
        }
        finally
        {
            foreach (var linha in linhas) editor.WriteMessage("\nSTRING_GERAR " + linha);
            editor.WriteMessage("\n");

            if (daJanela && ClivusExtension.TemInterface()) JanelaDeStrings.RetomarGeracao(documento, linhas, erro);
        }
    }

    /// <summary>
    /// CLIVUS_STRING_TRACADO_AUTO (nível 2, 11.3 e 11.4): o nome do tipo e
    /// os cliques, como no cartesiano. Strings separadas por ";", cliques
    /// por espaço; cada clique é "mesa.coluna.fileira" (coluna "F" = a
    /// última da mesa, "M" = a última da primeira metade, "N" = a primeira
    /// da segunda), com "L" na frente para o trecho em
    /// leapfrog. "FILEIRA:C:0.0.0" ou "FILEIRA:L:0.0.0" liga a fileira
    /// inteira do clique numa string. Grava o traçado e imprime cada string.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringTracadoAutomatico)]
    public static void TracadoAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var database = documento.Database;

        try
        {
            if (Achar(editor, database) is not { } tipo) return;

            var pedido = editor.GetString(new PromptStringOptions("\nCliques: ") { AllowSpaces = true });
            if (pedido.Status != PromptStatus.OK) return;

            var strings = new List<StringRoute>();

            foreach (var texto in pedido.StringResult.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                StringRoute? nova;
                string? porque = null;

                if (texto.StartsWith("FILEIRA:", StringComparison.Ordinal))
                {
                    var partes = texto.Split(':');
                    var celula = Celula(tipo.Arrangement, partes[2]);
                    nova = celula is null ? null : StringRouting.WholeRow(tipo.Arrangement, celula.Value, partes[1] == "L" ? RoutingKind.Leapfrog : RoutingKind.Conventional, out porque);
                    porque ??= celula is null ? Tr.T("o módulo não existe neste tipo") : null;
                }
                else
                {
                    var montagem = new RouteBuilder(tipo.Arrangement, strings.SelectMany(s => s.Cells));
                    porque = null;

                    foreach (var clique in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var leapfrog = clique.StartsWith('L');
                        var celula = Celula(tipo.Arrangement, leapfrog ? clique[1..] : clique);
                        porque = celula is null ? Tr.T("o módulo não existe neste tipo") : montagem.Click(celula.Value, leapfrog ? RoutingKind.Leapfrog : RoutingKind.Conventional);
                        if (porque is not null) break;
                    }

                    nova = porque is null ? montagem.Finish(out porque) : null;
                }

                if (nova is null)
                {
                    editor.WriteMessage(Tr.F("\nSTRING Não liguei: {0}.\n", porque ?? string.Empty));
                    return;
                }

                strings.Add(nova);
            }

            string? recusa = null;
            StringTypeStore.Mudar(database, b => recusa = b.SetStrings(tipo.Id, strings));

            if (recusa is not null)
            {
                editor.WriteMessage(Tr.F("\nSTRING Não gravei: {0}.\n", recusa));
                return;
            }

            ImprimirTracado(editor, StringTypeStore.Ler(database).Items.First(t => t.Id == tipo.Id));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_TRACADO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na biblioteca de strings: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_STRING_MODELO_AUTO (nível 2, 11.5): [Clonar/Espelhar] e o
    /// nome do tipo; imprime o traçado do tipo que resultou, lido do desenho.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringModeloAutomatico)]
    public static void ModeloAutomatico()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var database = documento.Database;

        try
        {
            var pergunta = new PromptKeywordOptions("\n[Clonar/Espelhar]: ") { AllowNone = false };
            pergunta.Keywords.Add("Clonar");
            pergunta.Keywords.Add("Espelhar");
            var resposta = editor.GetKeywords(pergunta);
            if (resposta.Status != PromptStatus.OK || Achar(editor, database) is not { } tipo) return;

            var resultado = tipo.Id;
            string? porque = null;
            StringTypeStore.Mudar(database, b =>
            {
                if (resposta.StringResult == "Clonar") resultado = b.Clone(tipo.Id)?.Id ?? Guid.Empty;
                else porque = b.Mirror(tipo.Id);
            });

            if (porque is not null)
            {
                editor.WriteMessage(Tr.F("\nSTRING Não gravei: {0}.\n", porque));
                return;
            }

            ImprimirTracado(editor, StringTypeStore.Ler(database).Items.First(t => t.Id == resultado));
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no CLIVUS_STRING_MODELO_AUTO.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui mexer na biblioteca de strings: {0}\n", erro.Message));
        }
    }

#if DEBUG
    /// <summary>
    /// CLIVUS_STRING_PRENDER_TESTE_AUTO (só no build de teste): o letreiro de
    /// uma mesa; a primeira string dela (pelo GUID) passa a apontar para um
    /// inversor de mentira, como a alocação da etapa 14 fará. Imprime o GUID.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoStringPrenderTesteAutomatico)]
    public static void PrenderTeste()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;
        var rotulo = editor.GetString(new PromptStringOptions("\nMesa: "));
        if (rotulo.Status != PromptStatus.OK) return;

        using var transacao = documento.Database.TransactionManager.StartTransaction();
        var mesa = LayoutScan.Tables(transacao, documento.Database).Values.FirstOrDefault(m => m.Identity?.Label == rotulo.StringResult);
        var modulos = mesa is null ? [] : mesa.Modules
            .Select(id => transacao.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity)
            .Select(e => e is null ? null : LayoutXData.LoadModule(e)?.Id)
            .OfType<Guid>().ToHashSet();

        var alvo = ElectricalStore.Strings(transacao, documento.Database).Where(x => x.String.Modules.Any(modulos.Contains)).OrderBy(x => x.String.Id).FirstOrDefault();
        if (alvo.String is null)
        {
            editor.WriteMessage($"\nSTRING_PRESA nenhuma string em {rotulo.StringResult}\n");
            return;
        }

        var entidade = (Autodesk.AutoCAD.DatabaseServices.Entity)transacao.GetObject(alvo.Id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForWrite);
        ElectricalStore.SaveString(transacao, entidade, alvo.String with { Inverter = Guid.NewGuid() });
        transacao.Commit();
        editor.WriteMessage($"\nSTRING_PRESA {rotulo.StringResult} {alvo.String.Id:D}\n");
    }
#endif

    /// <summary>Para o nível 2: cada string do tipo, com o + e o − e o texto do traçado.</summary>
    private static void ImprimirTracado(Editor editor, StringType tipo)
    {
        for (var i = 0; i < tipo.Routes.Count; i++)
            editor.WriteMessage($"\nSTRING_TRACADO {tipo.Name} {i + 1}: {RouteBuilder.Describe(tipo.Routes[i])} [{tipo.Routes[i].ToText()}]");
        editor.WriteMessage($"\nSTRING_TRACADO {tipo.Name} sem string: {StringRouting.Uncovered(tipo.Arrangement, tipo.Routes)}\n");
    }

    /// <summary>"0.F.1": mesa, coluna (ou F, a última; M e N, as duas do meio), fileira.</summary>
    private static RoutingCell? Celula(StringArrangement arranjo, string texto)
    {
        var partes = texto.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var mesa) || mesa < 0 || mesa >= arranjo.Tables.Count) return null;

        var colunas = arranjo.Tables[mesa].Columns;
        var coluna = partes[1] switch
        {
            "F" => colunas - 1,
            "M" => colunas / 2 - 1,
            "N" => colunas / 2,
            _ => int.TryParse(partes[1], out var c) ? c : -1,
        };

        return int.TryParse(partes[2], out var fileira) ? new RoutingCell(mesa, coluna, fileira) : null;
    }

    private static StringType? Achar(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome do tipo: ")) { AllowSpaces = true });
        if (nome.Status != PromptStatus.OK) return null;

        var tipo = StringTypeStore.Ler(database).Items.FirstOrDefault(t => string.Equals(t.Name, nome.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (tipo is null) editor.WriteMessage(Tr.F("\nSTRING Não há tipo chamado \"{0}\".\n", nome.StringResult.Trim()));
        return tipo;
    }
}
