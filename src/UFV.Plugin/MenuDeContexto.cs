using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// O menu de botão direito do plugin, submenu "UFV": sobre a área, "Refazer
/// as mesas desta área" e "Apagar tudo"; sobre a mesa, "Mudar inclinação
/// (alturas das pontas)" e "Recalcular esta mesa".
///
/// Um menu só, registrado para qualquer entidade (29/09/2026). Desde que a
/// mesa virou grupo (27/09), um clique seleciona o contorno, os pilares, os
/// módulos e as faces de uma vez; com classes misturadas na seleção o
/// AutoCAD não mostrava o menu registrado por classe, e o botão direito da
/// mesa não abria nada. Agora o menu olha a seleção quando vai abrir e
/// mostra só o que serve a ela; numa seleção sem nada nosso, o submenu nem
/// aparece.
///
/// Só existe com interface; num host sem ela nem é tocado (os tipos de
/// Autodesk.AutoCAD.Windows não carregam lá), por isso os métodos são
/// NoInlining, como a ribbon.
/// </summary>
internal static class MenuDeContexto
{
    private static readonly List<(RXClass Classe, ContextMenuExtension Menu)> Menus = [];

    private static MenuItem? _raiz;
    private static readonly List<MenuItem> ItensDaArea = [];
    private static readonly List<MenuItem> ItensDaMesa = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (Menus.Count > 0) return;

        var menu = new ContextMenuExtension { Title = "UFV" };

        // Um submenu "UFV" com os itens dentro (o Title da extensão não vira
        // submenu sozinho: o AutoCAD despeja os itens soltos no menu, como
        // o Renan viu em 26/09/2026).
        _raiz = new MenuItem("UFV");

        ItensDaArea.Add(Item("Refazer as mesas desta área", PluginInfo.ComandoRefazer));
        ItensDaArea.Add(Item("Apagar tudo", PluginInfo.ComandoApagarTudo));
        ItensDaMesa.Add(Item("Mudar inclinação (alturas das pontas)", PluginInfo.ComandoPontas));
        ItensDaMesa.Add(Item("Recalcular esta mesa", PluginInfo.ComandoRecalcular));

        foreach (var item in ItensDaArea.Concat(ItensDaMesa)) _raiz.MenuItems.Add(item);

        menu.MenuItems.Add(_raiz);
        menu.Popup += AoAbrir;

        var classe = RXObject.GetClass(typeof(Entity));
        Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(classe, menu);
        Menus.Add((classe, menu));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        foreach (var (classe, menu) in Menus)
        {
            try
            {
                menu.Popup -= AoAbrir;
                Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(classe, menu);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao tirar um menu de contexto.", erro);
            }
        }

        Menus.Clear();
        ItensDaArea.Clear();
        ItensDaMesa.Clear();
        _raiz = null;
    }

    private static MenuItem Item(string rotulo, string comando)
    {
        var item = new MenuItem(rotulo);

        item.Click += (_, _) =>
        {
            try
            {
                AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute($"_{comando} ", true, false, false);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar($"Falha ao chamar {comando} pelo menu.", erro);
            }
        };

        return item;
    }

    /// <summary>Na hora de abrir: o que há na seleção decide o que aparece.</summary>
    private static void AoAbrir(object? sender, EventArgs e)
    {
        try
        {
            var (temArea, temMesa) = OQueHaNaSelecao();

            foreach (var item in ItensDaArea) item.Visible = temArea;
            foreach (var item in ItensDaMesa) item.Visible = temMesa;

            if (_raiz is not null) _raiz.Visible = temArea || temMesa;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao montar o menu de botão direito.", erro);

            // Sem saber o que há na seleção, mostra tudo: os comandos
            // conferem o que receberam.
            foreach (var item in ItensDaArea.Concat(ItensDaMesa)) item.Visible = true;
            if (_raiz is not null) _raiz.Visible = true;
        }
    }

    /// <summary>Se a seleção tem uma área nossa, e se tem alguma peça de mesa (contorno, pilar, módulo, face).</summary>
    private static (bool Area, bool Mesa) OQueHaNaSelecao()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return (false, false);

        var selecao = documento.Editor.SelectImplied();
        if (selecao.Status != PromptStatus.OK || selecao.Value is null) return (false, false);

        var area = false;
        var mesa = false;

        using var transacao = documento.Database.TransactionManager.StartOpenCloseTransaction();

        foreach (var id in selecao.Value.GetObjectIds())
        {
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity entidade) continue;

            if (!area && entidade is Polyline3d && AreaXData.Load(entidade) is not null) area = true;
            else if (!mesa && LayoutScan.TableOf(entidade) is not null) mesa = true;

            if (area && mesa) break;
        }

        return (area, mesa);
    }
}
