using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Clivus.Core;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// As mesas para as strings (elétrica, 11.2 e 11.6): a seleção em campo que
/// só aceita mesa (regra elétrica 4: curva de nível, terreno, string e o
/// resto ficam de fora) e a leitura de cada mesa como o Core a quer
/// (<see cref="FieldTable"/>: letreiro, contorno, módulos e faces).
/// </summary>
internal static class MesasDaString
{
    /// <summary>
    /// Pede a seleção de mesas. Só entra peça de mesa do plugin (contorno,
    /// pilar, módulo, face): o filtro pega as nossas entidades, e o que não
    /// é peça de mesa (string, tag, nota solta) é tirado no ato, antes de
    /// ficar destacado. Devolve os GUIDs das mesas tocadas, ou null se o
    /// usuário cancelou.
    /// </summary>
    internal static HashSet<Guid>? Selecionar(Document documento, string mensagem)
    {
        var editor = documento.Editor;

        var filtro = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Operator, "<OR"),
            new TypedValue((int)DxfCode.Start, "INSERT"),
            new TypedValue((int)DxfCode.Start, "POLYLINE"),
            new TypedValue((int)DxfCode.Start, "3DFACE"),
            new TypedValue((int)DxfCode.Operator, "OR>"),
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, PluginXData.Aplicativo),
        ]);

        void SoMesa(object? _, SelectionAddedEventArgs e)
        {
            try
            {
                using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();
                var ids = e.AddedObjects.GetObjectIds();

                for (var i = ids.Length - 1; i >= 0; i--)
                {
                    var mesa = !ids[i].IsErased && transacao.GetObject(ids[i], OpenMode.ForRead) is Entity entidade ? LayoutScan.TableOf(entidade) : null;
                    if (mesa is null || mesa == Guid.Empty) e.Remove(i);
                }
            }
            catch (System.Exception erro)
            {
                // Evento do editor: exceção solta aqui derrubaria o Civil 3D.
                RegistroDeDiagnostico.Registrar("Falha no filtro da seleção de mesas das strings.", erro);
            }
        }

        editor.SelectionAdded += SoMesa;
        try
        {
            var resultado = editor.GetSelection(new PromptSelectionOptions { MessageForAdding = mensagem }, filtro);
            if (resultado.Status == PromptStatus.Error) return [];
            if (resultado.Status != PromptStatus.OK) return null;

            return SelecaoCommands.MesasTocadas(documento, resultado.Value.GetObjectIds());
        }
        finally
        {
            editor.SelectionAdded -= SoMesa;
        }
    }

    /// <summary>
    /// As mesas dadas, lidas do desenho: letreiro e cantos do contorno, e os
    /// módulos com a face superior (os cantos 3D, para o traçado do 11.7).
    /// Mesa sem contorno ou com contorno duplicado vai para os problemas.
    /// </summary>
    internal static List<FieldTable> Ler(Transaction transacao, Database database, IReadOnlySet<Guid> guids, List<string> problemas)
    {
        var mesas = new List<FieldTable>();
        var todas = LayoutScan.Tables(transacao, database);

        foreach (var guid in guids.OrderBy(g => g))
        {
            if (!todas.TryGetValue(guid, out var pecas) || pecas.Identity is not { } identidade || pecas.Contour is not { } contorno)
            {
                problemas.Add(Tr.T("uma mesa da seleção está sem contorno (apagado?); ficou de fora"));
                continue;
            }

            if (pecas.IsDuplicated)
            {
                problemas.Add(Tr.F("{0}: contorno duplicado (mesa copiada e colada); ficou de fora", identidade.Label));
                continue;
            }

            if (transacao.GetObject(contorno, OpenMode.ForRead) is not Polyline3d polilinha)
            {
                problemas.Add(Tr.F("{0}: o contorno da mesa não tem quatro cantos", identidade.Label));
                continue;
            }
            var cantos = FileiraCommands.Vertices(polilinha, transacao).Take(4).ToList();

            var faces = new Dictionary<Guid, IReadOnlyList<Point3>>();
            foreach (var id in pecas.Faces)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Face face && LayoutXData.LoadFace(face) is { } f)
                    faces[f.Module] = Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).Select(p => new Point3(p.X, p.Y, p.Z)).ToList();
            }

            var modulos = new List<FieldModule>();
            foreach (var id in pecas.Modules)
            {
                if (transacao.GetObject(id, OpenMode.ForRead) is Entity bloco && LayoutXData.LoadModule(bloco) is { } m)
                    modulos.Add(new FieldModule(m.Id, m.Column, m.Row, faces.GetValueOrDefault(m.Id) ?? []));
            }

            mesas.Add(new FieldTable(identidade.Id, identidade.Label, cantos, modulos));
        }

        return mesas;
    }
}
