using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// O menu de botão direito do plugin: sobre a polilinha da área, "Refazer
/// as mesas desta área"; sobre qualquer peça de mesa (contorno, pilar,
/// módulo, face), "Recalcular esta mesa". O AutoCAD mostra o item pela
/// classe da entidade selecionada; o comando confere se ela é nossa.
///
/// Só existe com interface; num host sem ela nem é tocado (os tipos de
/// Autodesk.AutoCAD.Windows não carregam lá), por isso os métodos são
/// NoInlining, como a ribbon.
/// </summary>
internal static class MenuDeContexto
{
    private static readonly List<(RXClass Classe, ContextMenuExtension Menu)> Menus = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (Menus.Count > 0) return;

        Registrar(typeof(Polyline3d),
            ("Refazer as mesas desta área", PluginInfo.ComandoRefazer),
            ("Recalcular esta mesa", PluginInfo.ComandoRecalcular));

        Registrar(typeof(BlockReference), ("Recalcular esta mesa", PluginInfo.ComandoRecalcular));
        Registrar(typeof(Face), ("Recalcular esta mesa", PluginInfo.ComandoRecalcular));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        foreach (var (classe, menu) in Menus)
        {
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(classe, menu);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao tirar um menu de contexto.", erro);
            }
        }

        Menus.Clear();
    }

    private static void Registrar(Type tipo, params (string Rotulo, string Comando)[] itens)
    {
        var menu = new ContextMenuExtension { Title = "UFV" };

        foreach (var (rotulo, comando) in itens)
        {
            var item = new MenuItem(rotulo);
            var nome = comando;

            item.Click += (_, _) =>
            {
                try
                {
                    AcadApp.DocumentManager.MdiActiveDocument?.SendStringToExecute($"_{nome} ", true, false, false);
                }
                catch (System.Exception erro)
                {
                    RegistroDeDiagnostico.Registrar($"Falha ao chamar {nome} pelo menu.", erro);
                }
            };

            menu.MenuItems.Add(item);
        }

        var classe = RXObject.GetClass(tipo);
        Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(classe, menu);
        Menus.Add((classe, menu));
    }
}
