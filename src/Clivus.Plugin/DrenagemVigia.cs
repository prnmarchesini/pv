using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// O gráfico vivo (27.4): a linha ou canaleta mexida (grip, MOVE, STRETCH,
/// propriedades) volta ao terreno e o gráfico dela é refeito no fim do
/// comando; apagada, o gráfico some junto; copiada, a cópia ganha GUID e nome
/// próprios. Mesmo esquema do vigia das árvores: anota no evento, age no fim
/// do comando (ou na folga, quando a mudança veio de fora de comando).
/// Comandos do Clivus e o U ficam calados: eles mesmos refazem o que mexem.
/// </summary>
internal static class DrenagemVigia
{
    private static readonly Dictionary<Document, Escuta> Escutas = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;
    private static int _caladoGlobal;

    /// <summary>Cala o vigia enquanto a janela da drenagem escreve no desenho fora de comando.</summary>
    internal static IDisposable Calar()
    {
        _caladoGlobal++;
        return new Soltura();
    }

    private sealed class Soltura : IDisposable
    {
        private bool _feito;

        public void Dispose()
        {
            if (_feito) return;
            _feito = true;
            _caladoGlobal--;
        }
    }

    internal static void Instalar()
    {
        if (_aoCriar is not null) return;

        var documentos = AcadApp.DocumentManager;
        _aoCriar = (_, e) => Escutar(e.Document);
        _aoDestruir = (_, e) => { if (e.Document is not null && Escutas.Remove(e.Document, out var escuta)) escuta.Soltar(); };

        documentos.DocumentCreated += _aoCriar;
        documentos.DocumentToBeDestroyed += _aoDestruir;

        foreach (Document documento in documentos) Escutar(documento);
    }

    internal static void Desinstalar()
    {
        foreach (var escuta in Escutas.Values.ToList()) escuta.Soltar();
        Escutas.Clear();

        if (_aoCriar is null) return;

        try
        {
            AcadApp.DocumentManager.DocumentCreated -= _aoCriar;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= _aoDestruir;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desligar o vigia da drenagem.", erro);
        }

        _aoCriar = null;
        _aoDestruir = null;
    }

    private static void Escutar(Document documento)
    {
        if (documento is null || Escutas.ContainsKey(documento)) return;

        try
        {
            Escutas[documento] = new Escuta(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ligar o vigia da drenagem num desenho.", erro);
        }
    }

    private sealed class Escuta
    {
        private readonly Document _documento;
        private readonly Database _banco;
        private readonly ObjectId _modelo;
        private readonly HashSet<ObjectId> _mexidas = [];
        private readonly HashSet<ObjectId> _novas = [];
        private readonly HashSet<ObjectId> _apagadas = [];
        private int _calados;
        private bool _executando;
        private bool _folgaAgendada;

        internal Escuta(Document documento)
        {
            _documento = documento;
            _banco = documento.Database;
            _modelo = SymbolUtilityServices.GetBlockModelSpaceId(_banco);

            _banco.ObjectModified += AoModificar;
            _banco.ObjectAppended += AoAcrescentar;
            _banco.ObjectErased += AoApagar;
            _documento.CommandWillStart += AoComecar;
            _documento.CommandEnded += AoTerminar;
            _documento.CommandCancelled += AoTerminar;
            _documento.CommandFailed += AoTerminar;
        }

        internal void Soltar()
        {
            try
            {
                _banco.ObjectModified -= AoModificar;
                _banco.ObjectAppended -= AoAcrescentar;
                _banco.ObjectErased -= AoApagar;
                _documento.CommandWillStart -= AoComecar;
                _documento.CommandEnded -= AoTerminar;
                _documento.CommandCancelled -= AoTerminar;
                _documento.CommandFailed -= AoTerminar;
                if (_folgaAgendada) AcadApp.Idle -= NaFolga;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar o vigia da drenagem.", erro);
            }
        }

        private bool Calado => _executando || _calados > 0 || _caladoGlobal > 0;

        private void AoComecar(object? sender, CommandEventArgs e)
        {
            if (PluginInfo.IsSilencedCommand(e.GlobalCommandName)) _calados++;
        }

        private void AoModificar(object? sender, ObjectEventArgs e)
        {
            if (Calado) return;

            // O grip num vértice mexe o vértice, não a polilinha: anota a dona.
            switch (e.DBObject)
            {
                case PolylineVertex3d v:
                    _mexidas.Add(v.OwnerId);
                    AgendarFolgaSeSemComando();
                    break;
                case Polyline3d p when p.OwnerId == _modelo:
                    if (!_novas.Contains(p.ObjectId)) _mexidas.Add(p.ObjectId);
                    AgendarFolgaSeSemComando();
                    break;
            }
        }

        private void AoAcrescentar(object? sender, ObjectEventArgs e)
        {
            if (Calado || e.DBObject is not Polyline3d p || p.OwnerId != _modelo) return;

            _novas.Add(p.ObjectId);
            AgendarFolgaSeSemComando();
        }

        private void AoApagar(object? sender, ObjectErasedEventArgs e)
        {
            if (Calado || e.DBObject is not Polyline3d p || p.OwnerId != _modelo) return;

            // Apagada: o gráfico vai junto. Devolvida (OOPS, U de outro comando):
            // conta como mexida, para o gráfico, que já tinha ido, voltar.
            if (e.Erased)
            {
                _apagadas.Add(p.ObjectId);
            }
            else
            {
                _apagadas.Remove(p.ObjectId);
                _mexidas.Add(p.ObjectId);
            }

            AgendarFolgaSeSemComando();
        }

        private void AoTerminar(object? sender, CommandEventArgs e)
        {
            if (PluginInfo.IsSilencedCommand(e.GlobalCommandName))
            {
                if (_calados > 0) _calados--;
                Limpar();
                return;
            }

            if (_calados > 0) return;

            Agir();
        }

        private void Limpar()
        {
            _mexidas.Clear();
            _novas.Clear();
            _apagadas.Clear();
        }

        private void AgendarFolgaSeSemComando()
        {
            if (_folgaAgendada) return;

            try
            {
                if (!string.IsNullOrEmpty(_documento.CommandInProgress)) return;

                AcadApp.Idle += NaFolga;
                _folgaAgendada = true;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Não consegui agendar o vigia da drenagem.", erro);
            }
        }

        private void NaFolga(object? sender, EventArgs e)
        {
            AcadApp.Idle -= NaFolga;
            _folgaAgendada = false;

            if (Calado || !string.IsNullOrEmpty(_documento.CommandInProgress)) return;

            using var trava = _documento.LockDocument();
            Agir();
        }

        private void Agir()
        {
            if (_mexidas.Count == 0 && _novas.Count == 0 && _apagadas.Count == 0) return;

            var mexidas = _mexidas.Concat(_novas).Distinct().ToList();
            var novas = _novas.ToHashSet();
            var apagadas = _apagadas.ToList();
            Limpar();

            _executando = true;

            try
            {
                using var transacao = _banco.TransactionManager.StartTransaction();
                var indice = IndiceDaDrenagem.Montar(transacao, _banco);

                // Apagadas: o gráfico (e o 3D) vão junto.
                var sumidas = 0;
                foreach (var id in apagadas)
                {
                    if (transacao.GetObject(id, OpenMode.ForRead, true) is not Polyline3d p || !p.IsErased) continue;
                    if (LinhasDeDrenagem.Identidade(p) is not { } identidade) continue;

                    indice.Apagar(transacao, identidade.Id);
                    sumidas++;
                }

                var linhas = mexidas
                    .Where(id => !id.IsErased && transacao.GetObject(id, OpenMode.ForRead, false, true) is Polyline3d p && p.OwnerId == _modelo && LinhasDeDrenagem.Identidade(p) is not null)
                    .ToList();

                if (linhas.Count == 0 && sumidas == 0)
                {
                    transacao.Commit();
                    return;
                }

                var refeitas = 0;
                var problemas = new List<string>();

                if (linhas.Count > 0)
                {
                    var terreno = TerrainCache.Get(_documento);
                    if (terreno is null && TerrainCommands.Reprocessar(_documento.Editor, _documento)) terreno = TerrainCache.Get(_documento);

                    if (terreno is null)
                    {
                        _documento.Editor.WriteMessage(Tr.T("\nLINHA Sem terreno processado: a linha mexida não pôde voltar ao terreno. Use o botão Terreno.\n"));
                    }
                    else
                    {
                        var configuracao = DrenagemStore.Ler(_banco);
                        var todas = LinhasDeDrenagem.Todas(transacao, _banco);

                        foreach (var id in linhas)
                        {
                            // Camada travada não derruba a transação das outras linhas.
                            var polilinha = (Polyline3d)transacao.GetObject(id, OpenMode.ForWrite, false, true);
                            var identidade = LinhasDeDrenagem.Identidade(polilinha)!;

                            // Cópia: o mesmo GUID de outra linha. Ganha identidade
                            // própria e o gráfico ao lado dela.
                            if (novas.Contains(id) && todas.Any(l => l.Id != id && l.Identidade.Id == identidade.Id))
                            {
                                var prefixo = identidade.IsChannel ? Tr.T("Canaleta") : Tr.T("Linha");
                                identidade = identidade with
                                {
                                    Id = Guid.NewGuid(),
                                    Name = DrainageLineIdentity.NextName(prefixo, todas.Select(l => l.Identidade.Name)),
                                    Chart = null,
                                };
                                LinhasDeDrenagem.Gravar(transacao, polilinha, identidade);
                                todas.Add(new LinhaNoDesenho(id, identidade, []));
                            }

                            try
                            {
                                LinhasDeDrenagem.Reassentar(transacao, polilinha, terreno.Mesh);
                                var alerta = LinhasDeDrenagem.Refazer(transacao, _banco, terreno.Mesh, configuracao, id, indice).Alerta;
                                if (alerta.Length > 0) problemas.Add(alerta);
                                refeitas++;
                            }
                            catch (ArgumentException erro)
                            {
                                problemas.Add(erro.Message);
                            }
                        }
                    }
                }

                transacao.Commit();

                if (refeitas > 0) _documento.Editor.WriteMessage(Tr.F("\nLINHA {0} linha(s) mexida(s) de volta ao terreno, com o gráfico refeito.\n", refeitas));
                if (sumidas > 0) _documento.Editor.WriteMessage(Tr.F("\nLINHA {0} linha(s) apagada(s): o gráfico foi junto.\n", sumidas));
                foreach (var p in problemas) _documento.Editor.WriteMessage($"\nLINHA {p}\n");
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia da drenagem não conseguiu refazer o gráfico.", erro);
                _documento.Editor.WriteMessage(Tr.F("\nLINHA Não consegui refazer o gráfico: {0}\n", erro.Message));
            }
            finally
            {
                _executando = false;
            }
        }
    }
}
