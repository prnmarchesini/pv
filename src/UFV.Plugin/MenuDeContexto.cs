using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// O menu de botão direito sobre a polilinha da área: "Refazer as mesas
/// desta área". O AutoCAD mostra o item quando a entidade selecionada é da
/// classe registrada (toda polilinha 3D; o comando confere se é área nossa).
///
/// Só existe com interface; num host sem ela nem é tocado (os tipos de
/// Autodesk.AutoCAD.Windows não carregam lá), por isso os métodos são
/// NoInlining, como a ribbon.
/// </summary>
internal static class MenuDeContexto
{
    private static ContextMenuExtension? _menu;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Instalar()
    {
        if (_menu is not null) return;

        var menu = new ContextMenuExtension { Title = "UFV" };

        var refazer = new MenuItem("Refazer as mesas desta área");
        refazer.Click += (_, _) =>
        {
            try
            {
                var documento = AcadApp.DocumentManager.MdiActiveDocument;
                documento?.SendStringToExecute($"_{PluginInfo.ComandoRefazer} ", true, false, false);
            }
            catch (System.Exception erro)
            {
                RegistroDeDiagnostico.Registrar("Falha ao chamar o Refazer pelo menu.", erro);
            }
        };

        menu.MenuItems.Add(refazer);

        Autodesk.AutoCAD.ApplicationServices.Application.AddObjectContextMenuExtension(
            RXObject.GetClass(typeof(Polyline3d)), menu);

        _menu = menu;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Desinstalar()
    {
        if (_menu is null) return;

        Autodesk.AutoCAD.ApplicationServices.Application.RemoveObjectContextMenuExtension(
            RXObject.GetClass(typeof(Polyline3d)), _menu);

        _menu = null;
    }
}
