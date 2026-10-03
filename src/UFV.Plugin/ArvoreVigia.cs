using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// A árvore acompanha o terreno (9.4). Renan, 03/10/2026: "se ela tava no
/// topo da montanha e eu puxo para o pé da montanha, ela desce e fica sempre
/// seguindo o terreno". Toda árvore mexida por um comando que não é nosso
/// (MOVE, grip, COPY, ROTATE, Propriedades) volta ao chão do lugar novo no
/// fim do comando: a cota do pé é a do terreno ali, nunca a que o arrasto
/// deixou (regra 5). Cópia ganha GUID próprio. Desfazer não mexe (o
/// desenho volta a um estado que já estava no chão).
/// </summary>
internal static class ArvoreVigia
{
    private static readonly Dictionary<Document, Escuta> Escutas = [];
    private static DocumentCollectionEventHandler? _aoCriar;
    private static DocumentCollectionEventHandler? _aoDestruir;

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
            RegistroDeDiagnostico.Registrar("Falha ao desligar o vigia das árvores.", erro);
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
            RegistroDeDiagnostico.Registrar("Não consegui ligar o vigia das árvores num desenho.", erro);
        }
    }

    private sealed class Escuta
    {
        private readonly Document _documento;
        private readonly Database _banco;
        private readonly HashSet<ObjectId> _mexidas = [];
        private readonly HashSet<ObjectId> _novas = [];
        private int _calados;
        private bool _executando;
        private bool _folgaAgendada;

        internal Escuta(Document documento)
        {
            _documento = documento;
            _banco = documento.Database;

            _banco.ObjectModified += AoModificar;
            _banco.ObjectAppended += AoAcrescentar;
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
                _documento.CommandWillStart -= AoComecar;
                _documento.CommandEnded -= AoTerminar;
                _documento.CommandCancelled -= AoTerminar;
                _documento.CommandFailed -= AoTerminar;
                if (_folgaAgendada) AcadApp.Idle -= NaFolga;
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao soltar o vigia das árvores.", erro);
            }
        }

        private void AoComecar(object? sender, CommandEventArgs e)
        {
            if (PluginInfo.IsSilencedCommand(e.GlobalCommandName)) _calados++;
        }

        private void AoModificar(object? sender, ObjectEventArgs e) => Anotar(e.DBObject, nova: false);

        private void AoAcrescentar(object? sender, ObjectEventArgs e) => Anotar(e.DBObject, nova: true);

        private void Anotar(DBObject objeto, bool nova)
        {
            if (_executando || _calados > 0 || objeto is not BlockReference) return;

            try
            {
                // A cópia nasce sem XData e o ganha num "modificado" logo em
                // seguida: guardada sempre; no fim, só as que são árvore contam.
                if (nova)
                {
                    _novas.Add(objeto.ObjectId);
                    return;
                }

                if (_novas.Contains(objeto.ObjectId)) return;
                if (LayoutXData.LoadTree((Entity)objeto) is null) return;

                _mexidas.Add(objeto.ObjectId);
                AgendarFolgaSeSemComando();
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia das árvores não conseguiu ler um bloco.", erro);
            }
        }

        private void AoTerminar(object? sender, CommandEventArgs e)
        {
            if (PluginInfo.IsSilencedCommand(e.GlobalCommandName))
            {
                if (_calados > 0) _calados--;
                _mexidas.Clear();
                _novas.Clear();
                return;
            }

            if (_calados > 0) return;

            Assentar();
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
                RegistroDeDiagnostico.Registrar("Não consegui agendar o vigia das árvores.", erro);
            }
        }

        private void NaFolga(object? sender, EventArgs e)
        {
            AcadApp.Idle -= NaFolga;
            _folgaAgendada = false;

            if (_calados > 0 || _executando || !string.IsNullOrEmpty(_documento.CommandInProgress)) return;

            using var trava = _documento.LockDocument();
            Assentar();
        }

        /// <summary>Põe cada árvore mexida no chão do lugar dela; a cópia ganha GUID próprio.</summary>
        private void Assentar()
        {
            if (_mexidas.Count == 0 && _novas.Count == 0) return;

            var mexidas = _mexidas.ToList();
            var novas = _novas.ToList();
            _mexidas.Clear();
            _novas.Clear();

            var terreno = TerrainCache.Get(_documento);
            _executando = true;

            try
            {
                using var transacao = _banco.TransactionManager.StartTransaction();
                var assentadas = 0;
                var foraDoTerreno = 0;

                foreach (var id in mexidas.Concat(novas).Distinct())
                {
                    if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead) is not BlockReference arvore) continue;
                    if (LayoutXData.LoadTree(arvore) is not { } identidade) continue;

                    if (novas.Contains(id))
                    {
                        arvore.UpgradeOpen();
                        LayoutXData.SaveTree(transacao, arvore, identidade with { Id = Guid.NewGuid() });
                    }

                    if (terreno is null) continue;

                    var p = arvore.Position;

                    if (!terreno.Mesh.TryGetZ(p.X, p.Y, out var chao))
                    {
                        foraDoTerreno++;
                        continue;
                    }

                    if (Math.Abs(chao - p.Z) < 1e-4) continue;

                    if (!arvore.IsWriteEnabled) arvore.UpgradeOpen();
                    arvore.Position = new Point3d(p.X, p.Y, chao);
                    assentadas++;
                }

                transacao.Commit();

                if (assentadas > 0) _documento.Editor.WriteMessage($"\nÁRVORE {assentadas} árvore(s) de volta ao chão do lugar novo.\n");
                if (foraDoTerreno > 0) _documento.Editor.WriteMessage($"\nÁRVORE ATENÇÃO: {foraDoTerreno} árvore(s) fora do terreno ficaram onde o arrasto deixou.\n");
                if (terreno is null && mexidas.Count > 0) _documento.Editor.WriteMessage("\nÁRVORE Sem terreno processado, a árvore mexida não pôde voltar ao chão. Use o botão Terreno.\n");
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia das árvores não conseguiu assentar.", erro);
                _documento.Editor.WriteMessage($"\nÁRVORE Não consegui pôr a árvore no chão: {erro.Message}\n");
            }
            finally
            {
                _executando = false;
            }
        }
    }
}
