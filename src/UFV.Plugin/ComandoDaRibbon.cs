using System.Windows.Input;
using Autodesk.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// Liga um botao da ribbon a um comando da linha de comando do AutoCAD.
///
/// O botao guarda o texto do comando em CommandParameter (com um espaco no
/// fim, que e o Enter) e este handler o envia ao documento ativo. Assim o
/// botao e o comando digitado passam exatamente pelo mesmo caminho, e nao ha
/// uma segunda implementacao para manter em pe.
/// </summary>
public sealed class ComandoDaRibbon : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        AcadApp.DocumentManager.MdiActiveDocument is not null;

    public void Execute(object? parameter)
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var comando = (parameter as RibbonButton)?.CommandParameter as string
                      ?? parameter as string;

        if (string.IsNullOrWhiteSpace(comando)) return;

        documento.SendStringToExecute(comando, true, false, true);
    }
}
