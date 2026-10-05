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

            var problema = StringTypeStore.Mudar(database, b =>
            {
                if (alvo == Guid.Empty)
                {
                    tipo = b.Add(arranjo, desenho);
                }
                else
                {
                    recusa = b.SetArrangement(alvo, arranjo, desenho);
                    tipo = b.Find(alvo);
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

    private static StringType? Achar(Editor editor, Autodesk.AutoCAD.DatabaseServices.Database database)
    {
        var nome = editor.GetString(new PromptStringOptions(Tr.T("\nNome do tipo: ")) { AllowSpaces = true });
        if (nome.Status != PromptStatus.OK) return null;

        var tipo = StringTypeStore.Ler(database).Items.FirstOrDefault(t => string.Equals(t.Name, nome.StringResult.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (tipo is null) editor.WriteMessage(Tr.F("\nSTRING Não há tipo chamado \"{0}\".\n", nome.StringResult.Trim()));
        return tipo;
    }
}
