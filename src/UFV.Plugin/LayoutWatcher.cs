using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// O vigia (7.2): escuta o banco de cada desenho aberto (objeto modificado,
/// acrescentado, apagado) e o fim de cada comando. Quando um comando que
/// NÃO é nosso toca uma peça nossa, a mesa dela fica suja e vermelha; quando
/// apaga o contorno de uma mesa, a remoção é registrada; quando o contorno
/// volta (desfazer), a remoção é retirada do registro. Só isso: "mover,
/// apagar ou copiar mesa: só marca e pinta. Nada mais."
///
/// Três silêncios, sem os quais o vigia se morde:
/// - durante os nossos comandos (UFV_*), que desenham e pintam por dezenas
///   de entidades, nada é anotado;
/// - durante desfazer/refazer (U, UNDO, REDO, MREDO, OOPS) nada suja: o
///   banco volta a um estado que já foi decidido; só o registro de remoções
///   acompanha o contorno que some ou volta;
/// - enquanto o próprio vigia pinta, nada é anotado (pintar é modificar).
///
/// A paleta de Propriedades altera entidades SEM comando: o que ela toca
/// fica no livro e é descarregado na primeira folga do AutoCAD (Idle), para
/// não esperar o próximo comando, que poderia ser o salvar.
///
/// O que se decide com os eventos é do Core (<see cref="PendingChanges"/>),
/// com teste; aqui só se escuta e se executa.
/// </summary>
internal static class LayoutWatcher
{
    private static readonly Dictionary<Document, Vigia> Vigias = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;

    /// <summary>Liga o vigia nos desenhos abertos e nos que abrirem depois.</summary>
    internal static void Instalar()
    {
        if (_aoCriar is not null) return;

        var documentos = AcadApp.DocumentManager;

        _aoCriar = (_, e) => Vigiar(e.Document);
        _aoDestruir = (_, e) => Soltar(e.Document);

        documentos.DocumentCreated += _aoCriar;
        documentos.DocumentToBeDestroyed += _aoDestruir;

        foreach (Document documento in documentos) Vigiar(documento);
    }

    internal static void Desinstalar()
    {
        foreach (var vigia in Vigias.Values.ToList()) vigia.Soltar();

        Vigias.Clear();

        if (_aoCriar is null) return;

        try
        {
            AcadApp.DocumentManager.DocumentCreated -= _aoCriar;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desligar o vigia dos documentos.", erro);
        }

        _aoCriar = null;
        _aoDestruir = null;
    }

    private static void Vigiar(Document documento)
    {
        if (documento is null || Vigias.ContainsKey(documento)) return;

        try
        {
            Vigias[documento] = new Vigia(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ligar o vigia num desenho.", erro);
        }
    }

    private static void Soltar(Document documento)
    {
        if (documento is null || !Vigias.Remove(documento, out var vigia)) return;

        vigia.Soltar();
    }

    /// <summary>O vigia de um desenho.</summary>
    private sealed class Vigia
    {
        private readonly Document _documento;
        private readonly Database _banco;
        private readonly PendingChanges _livro = new();

        /// <summary>Profundidade de comandos calados (nossos ou de desfazer) em andamento.</summary>
        private int _calados;

        /// <summary>O vigia está pintando: nada é anotado.</summary>
        private bool _executando;

        /// <summary>Há um descarregamento agendado para a folga do AutoCAD.</summary>
        private bool _folgaAgendada;

        internal Vigia(Document documento)
        {
            _documento = documento;
            _banco = documento.Database;

            _banco.ObjectModified += AoModificar;
            _banco.ObjectAppended += AoAcrescentar;
            _banco.ObjectErased += AoApagar;

            _documento.CommandWillStart += AoComecarComando;
            _documento.CommandEnded += AoTerminarComando;
            _documento.CommandCancelled += AoTerminarComando;
            _documento.CommandFailed += AoTerminarComando;
        }

        internal void Soltar()
        {
            try
            {
                _banco.ObjectModified -= AoModificar;
                _banco.ObjectAppended -= AoAcrescentar;
                _banco.ObjectErased -= AoApagar;

                _documento.CommandWillStart -= AoComecarComando;
                _documento.CommandEnded -= AoTerminarComando;
                _documento.CommandCancelled -= AoTerminarComando;
                _documento.CommandFailed -= AoTerminarComando;

                if (_folgaAgendada) AcadApp.Idle -= NaFolga;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar o vigia.", erro);
            }
        }

        private bool Calado => _calados > 0 || _executando;

        private void AoComecarComando(object? sender, CommandEventArgs e)
        {
            if (!PluginInfo.IsSilencedCommand(e.GlobalCommandName)) return;

            if (_calados == 0) _desfazendo = PluginInfo.IsUndoCommand(e.GlobalCommandName);
            _calados++;
        }

        private void AoModificar(object? sender, ObjectEventArgs e) => Anotar(e.DBObject, ChangeKind.Modified);

        private void AoAcrescentar(object? sender, ObjectEventArgs e) => Anotar(e.DBObject, ChangeKind.Appended);

        /// <summary>Apagar, ou desapagar (undo de um apagar) quando Erased é falso.</summary>
        private void AoApagar(object? sender, ObjectErasedEventArgs e) =>
            Anotar(e.DBObject, e.Erased ? ChangeKind.Erased : ChangeKind.Restored, mesmoCalado: true);

        /// <summary>
        /// Lê a identidade da peça (só leitura, dentro do evento) e anota no
        /// livro. Apagar e desapagar são anotados mesmo em comando calado de
        /// desfazer, porque o registro de remoções tem que acompanhar o
        /// contorno; nos nossos comandos nada é anotado.
        /// </summary>
        private void Anotar(DBObject objeto, ChangeKind tipo, bool mesmoCalado = false)
        {
            if (_executando || objeto is not Entity entidade) return;
            if (_calados > 0 && !(mesmoCalado && _desfazendo)) return;

            try
            {
                using var dados = entidade.GetXDataForApplication(PluginXData.Aplicativo);
                if (dados is null) return;

                if (LayoutXData.LoadTable(entidade) is { } mesa)
                    _livro.Note(mesa.Id, tipo, isContour: true, label: mesa.Label);
                else if (LayoutScan.TableOf(entidade) is { } dona)
                    _livro.Note(dona, tipo, isContour: false);
                else
                    return;

                AgendarFolgaSeSemComando();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia não conseguiu ler uma peça.", erro);
            }
        }

        /// <summary>Verdadeiro enquanto o comando calado em andamento é de desfazer/refazer.</summary>
        private bool _desfazendo;

        private void AoTerminarComando(object? sender, CommandEventArgs e)
        {
            var nome = e.GlobalCommandName;

            if (PluginInfo.IsSilencedCommand(nome))
            {
                if (_calados > 0) _calados--;
                if (_calados == 0) _desfazendo = false;

                // Nosso comando: o que ele desenhou não é sujeira. Desfazer:
                // só o registro de remoções acompanha o contorno.
                var decisao = _livro.Resolve(DateTime.UtcNow);

                if (PluginInfo.IsUndoCommand(nome) && _calados == 0 && (decisao.Removed.Count > 0 || decisao.Restored.Count > 0))
                    Descarregar(decisao, nome, soRegistro: true);

                return;
            }

            if (_calados > 0 || _livro.IsEmpty) return;

            Descarregar(_livro.Resolve(DateTime.UtcNow), nome, soRegistro: false);
        }

        /// <summary>
        /// Sem comando em andamento (paleta de Propriedades), agenda o
        /// descarregamento para a folga do AutoCAD. No Core Console não há
        /// laço de folga; o próximo fim de comando descarrega.
        /// </summary>
        private void AgendarFolgaSeSemComando()
        {
            if (_folgaAgendada || _livro.IsEmpty) return;

            try
            {
                if (!string.IsNullOrEmpty(_documento.CommandInProgress)) return;

                AcadApp.Idle += NaFolga;
                _folgaAgendada = true;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui agendar o vigia para a folga.", erro);
            }
        }

        private void NaFolga(object? sender, EventArgs e)
        {
            AcadApp.Idle -= NaFolga;
            _folgaAgendada = false;

            if (_calados > 0 || _executando || _livro.IsEmpty) return;
            if (!string.IsNullOrEmpty(_documento.CommandInProgress)) return;

            using var trava = _documento.LockDocument();
            Descarregar(_livro.Resolve(DateTime.UtcNow), "Propriedades", soRegistro: false);
        }

        private void Descarregar(ChangeResolution decisao, string comando, bool soRegistro)
        {
            if (_executando) return;

            _executando = true;

            try
            {
                Executar(decisao, comando, soRegistro);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia não conseguiu marcar as mesas.", erro);
                _documento.Editor.WriteMessage($"\nVIGIA Não consegui marcar as mesas tocadas: {erro.Message}\n");
            }
            finally
            {
                _executando = false;
            }
        }

        /// <summary>
        /// Suja e pinta as mesas tocadas; registra as removidas; tira do
        /// registro as que voltaram. Uma transação (o registro abre as suas
        /// aninhadas, de propósito: se esta abortar, cai tudo junto).
        /// </summary>
        private void Executar(ChangeResolution decisao, string comando, bool soRegistro)
        {
            var editor = _documento.Editor;

            using var transacao = _banco.TransactionManager.StartTransaction();

            var mesas = LayoutScan.Tables(transacao, _banco);

            if (!soRegistro)
            {
                foreach (var (guid, motivo) in decisao.Dirty)
                {
                    if (!mesas.TryGetValue(guid, out var mesa) || mesa.Identity is null) continue;

                    TableState.MarkDirty(transacao, mesa, motivo);
                    editor.WriteMessage($"\nVIGIA {mesa.Identity.Label} suja ({motivo}, comando {comando}).\n");
                }
            }

            // Contorno que voltou: sai do registro de removidas.
            var voltaram = decisao.Restored.Where(g => mesas.TryGetValue(g, out var m) && m.Identity is not null).ToList();

            if (voltaram.Count > 0)
            {
                RemovalStore.Remove(_banco, voltaram);

                foreach (var guid in voltaram)
                    editor.WriteMessage($"\nVIGIA {mesas[guid].Identity!.Label} voltou (comando {comando}); saiu das removidas.\n");
            }

            if (decisao.Removed.Count > 0)
            {
                var problema = RemovalStore.Add(_banco, decisao.Removed);

                foreach (var remocao in decisao.Removed)
                    editor.WriteMessage($"\nVIGIA {remocao.Label} removida (comando {comando}); {PluginInfo.ComandoEstado} lista.\n");

                if (problema is not null) editor.WriteMessage($"\n  ATENÇÃO: {problema}.\n");
            }

            transacao.Commit();
        }
    }
}
