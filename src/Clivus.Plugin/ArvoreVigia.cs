using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Clivus.Core;
using Clivus.Geo;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// A árvore acompanha o terreno (9.4). Renan, 03/10/2026: "se ela tava no
/// topo da montanha e eu puxo para o pé da montanha, ela desce e fica sempre
/// seguindo o terreno". Toda árvore mexida por um comando que não é nosso
/// (MOVE, grip, COPY, ROTATE, Propriedades) volta ao chão do lugar novo no
/// fim do comando: a cota do pé é a do terreno ali, nunca a que o arrasto
/// deixou (regra 5). Cópia ganha GUID próprio. Desfazer não mexe (o
/// desenho volta a um estado que já estava no chão).
///
/// O mesmo vale para o bloco de equipamento em campo (inversor, trafo,
/// combiner; correção de 10/10/2026, à noite: "todo desenho respeita o
/// TIN"): mexido por MOVE, COPY, ROTATE, grip ou Propriedades, a base volta
/// ao terreno + 0,80 do lugar novo (a conta do Pôr em campo). No fim do
/// comando, dentro do grupo de UNDO dele: um U desfaz o MOVE e o assentar
/// juntos. Sem terreno, avisa e não mexe (a aba Inversor e o Gerar conferem depois).
/// E a polilinha da área de inversores (Polyline3d com a marca de área)
/// mexida ou copiada é drapeada de novo no terreno do lugar novo.
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
        private readonly ObjectId _modelo;
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
            // Só o espaço do modelo: árvore dentro de bloco (BLOCK, ARRAY,
            // INSERT de outro desenho) tem a posição em coordenadas do bloco.
            if (_executando || _calados > 0 || objeto is not (BlockReference or Polyline3d) || objeto.OwnerId != _modelo) return;

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
                if (objeto is Polyline3d area ? LocalDosInversores.Marca(area) is null
                    : LayoutXData.LoadTree((Entity)objeto) is null && ElectricalStore.LoadPlacement((Entity)objeto) is null) return;

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

        /// <summary>
        /// Drapeia de novo a polilinha 3D da área no terreno: tira os vértices
        /// do drapeado antigo (alinhados em planta com os vizinhos), drapeia os
        /// cantos que sobram e troca os vértices. Se mudou alguma coisa.
        /// </summary>
        private static bool Redrapear(Transaction transacao, Polyline3d poli, Tin terreno)
        {
            var ids = poli.Cast<ObjectId>().Where(v => !v.IsErased).ToList();
            var pontos = ids.Select(v => ((PolylineVertex3d)transacao.GetObject(v, OpenMode.ForRead)).Position).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
            if (pontos.Count < 3) return false;

            var cantos = Cantos(pontos);
            var drapeada = Draping.Along(terreno, [.. cantos, cantos[0]]).Vertices.ToList();
            if (drapeada.Count > 1 && Math.Abs(drapeada[0].X - drapeada[^1].X) < 1e-6 && Math.Abs(drapeada[0].Y - drapeada[^1].Y) < 1e-6) drapeada.RemoveAt(drapeada.Count - 1);
            if (drapeada.Count < 3) return false;

            if (drapeada.Count == pontos.Count && drapeada.Zip(pontos).All(z => Math.Abs(z.First.X - z.Second.X) < 1e-6 && Math.Abs(z.First.Y - z.Second.Y) < 1e-6 && Math.Abs(z.First.Z - z.Second.Z) < 1e-4))
                return false;

            if (!poli.IsWriteEnabled) poli.UpgradeOpen();
            foreach (var v in ids) transacao.GetObject(v, OpenMode.ForWrite).Erase();
            foreach (var p in drapeada)
            {
                var vertice = new PolylineVertex3d(new Point3d(p.X, p.Y, p.Z));
                poli.AppendVertex(vertice);
                transacao.AddNewlyCreatedDBObject(vertice, true);
            }

            return true;
        }

        /// <summary>Os vértices que não estão alinhados em planta com os vizinhos (os cantos; o arco dividido fica).</summary>
        private static List<Point3> Cantos(IReadOnlyList<Point3> pontos)
        {
            var cantos = new List<Point3>();
            for (var i = 0; i < pontos.Count; i++)
            {
                var a = pontos[(i - 1 + pontos.Count) % pontos.Count];
                var b = pontos[i];
                var c = pontos[(i + 1) % pontos.Count];
                var cruz = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                var escala = Math.Max(1e-9, Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * Math.Sqrt((c.X - b.X) * (c.X - b.X) + (c.Y - b.Y) * (c.Y - b.Y)));
                if (Math.Abs(cruz) / escala > 1e-6) cantos.Add(b);
            }

            return cantos.Count >= 3 ? cantos : [.. pontos];
        }

        /// <summary>Põe cada árvore mexida no chão do lugar dela; a cópia ganha GUID próprio.</summary>
        private void Assentar()
        {
            if (_mexidas.Count == 0 && _novas.Count == 0) return;

            var mexidas = _mexidas.ToList();
            var novas = _novas.ToList();
            _mexidas.Clear();
            _novas.Clear();

            // Desenho reaberto: a malha é só memória, o carimbo diz qual
            // superfície é o terreno. Reprocessa sozinho, como os comandos.
            var terreno = TerrainCache.Get(_documento);
            if (terreno is null && mexidas.Count + novas.Count > 0)
            {
                try
                {
                    if (TerrainCommands.Reprocessar(_documento.Editor, _documento)) terreno = TerrainCache.Get(_documento);
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar("O vigia das árvores não conseguiu reprocessar o terreno.", erro);
                }
            }

            _executando = true;

            try
            {
                using var transacao = _banco.TransactionManager.StartTransaction();
                var assentadas = 0;
                var foraDoTerreno = 0;
                var equipamentos = 0;
                var equipamentosSemTerreno = 0;
                var arvoresMexidas = 0;
                var areas = 0;

                foreach (var id in mexidas.Concat(novas).Distinct())
                {
                    // A polilinha da área de inversores: drapeada de novo no terreno do lugar novo.
                    if (!id.IsErased && terreno is not null && transacao.GetObject(id, OpenMode.ForRead, false, true) is Polyline3d poli
                        && poli.OwnerId == _modelo && LocalDosInversores.Marca(poli) is not null)
                    {
                        if (Redrapear(transacao, poli, terreno.Mesh)) areas++;
                        continue;
                    }

                    if (id.IsErased || transacao.GetObject(id, OpenMode.ForRead, false, true) is not BlockReference arvore || arvore.OwnerId != _modelo) continue;

                    // Equipamento em campo: a base no terreno + 0,80 do lugar novo (as cópias também).
                    if (ElectricalStore.LoadPlacement(arvore) is not null)
                    {
                        if (terreno is null)
                        {
                            equipamentosSemTerreno++;
                            continue;
                        }

                        var q = arvore.Position;
                        if (!terreno.Mesh.TryGetZ(q.X, q.Y, out var chaoDoEquipamento)) continue;
                        var baseZ = EquipmentFootprint.BaseElevation(chaoDoEquipamento);
                        if (Math.Abs(baseZ - q.Z) <= 0.001) continue;

                        if (!arvore.IsWriteEnabled) arvore = (BlockReference)transacao.GetObject(id, OpenMode.ForWrite, false, true);
                        arvore.Position = new Point3d(q.X, q.Y, baseZ);
                        equipamentos++;
                        continue;
                    }

                    if (LayoutXData.LoadTree(arvore) is not { } identidade) continue;
                    if (mexidas.Contains(id)) arvoresMexidas++;

                    if (novas.Contains(id))
                    {
                        arvore = (BlockReference)transacao.GetObject(id, OpenMode.ForWrite, false, true);
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

                    if (!arvore.IsWriteEnabled) arvore = (BlockReference)transacao.GetObject(id, OpenMode.ForWrite, false, true);
                    arvore.Position = new Point3d(p.X, p.Y, chao);
                    assentadas++;
                }

                transacao.Commit();

                if (assentadas > 0) _documento.Editor.WriteMessage(Tr.F("\nÁRVORE {0} árvore(s) de volta ao chão do lugar novo.\n", assentadas));
                if (foraDoTerreno > 0) _documento.Editor.WriteMessage(Tr.F("\nÁRVORE ATENÇÃO: {0} árvore(s) fora do terreno ficaram onde o arrasto deixou.\n", foraDoTerreno));
                if (terreno is null && arvoresMexidas > 0) _documento.Editor.WriteMessage(Tr.T("\nÁRVORE Sem terreno processado, a árvore mexida não pôde voltar ao chão. Use o botão Terreno.\n"));
                if (areas > 0) _documento.Editor.WriteMessage(Tr.F("\nAREA {0} área(s) de inversores mexida(s): o contorno foi drapeado de novo no terreno do lugar novo.\n", areas));
                if (equipamentos > 0) _documento.Editor.WriteMessage(Tr.F("\nEQUIPAMENTO {0} equipamento(s) mexido(s): a base voltou ao terreno + 0,80 m do lugar novo.\n", equipamentos));
                if (equipamentosSemTerreno > 0) _documento.Editor.WriteMessage(Tr.F("\nEQUIPAMENTO ATENÇÃO: sem terreno processado, {0} equipamento(s) mexido(s) ficaram com a cota de antes. Use o botão Terreno; a aba Inversor e o Gerar da rota os põem no terreno + 0,80 m depois.\n", equipamentosSemTerreno));
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("O vigia das árvores não conseguiu assentar.", erro);
                _documento.Editor.WriteMessage(Tr.F("\nÁRVORE Não consegui pôr a árvore no chão: {0}\n", erro.Message));
            }
            finally
            {
                _executando = false;
            }
        }
    }
}
