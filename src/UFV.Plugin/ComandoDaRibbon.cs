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
internal sealed class ComandoDaRibbon : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        AcadApp.DocumentManager.MdiActiveDocument is not null
        && !string.IsNullOrWhiteSpace(LerComando(parameter));

    public void Execute(object? parameter)
    {
        var comando = LerComando(parameter);
        if (string.IsNullOrWhiteSpace(comando))
        {
            // Botao sem comando e um botao que nao faz nada ao ser clicado:
            // sem registro, isso vira um bug mudo.
            RegistroDeDiagnostico.Registrar(
                $"Botão da ribbon acionado sem comando (parâmetro: {parameter?.GetType().Name ?? "nulo"}).");
            return;
        }

        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        documento.SendStringToExecute(comando, true, false, true);
    }

    /// <summary>
    /// A ribbon entrega o proprio item como parametro. O tipo declarado e
    /// RibbonCommandItem, a classe base que expoe CommandParameter: castar
    /// para RibbonButton concreto faria o botao virar um no-op silencioso no
    /// dia em que ele virasse um split button ou fosse embrulhado.
    /// </summary>
    private static string? LerComando(object? parameter) => parameter switch
    {
        RibbonCommandItem item => item.CommandParameter as string,
        string texto => texto,
        _ => null,
    };
}
