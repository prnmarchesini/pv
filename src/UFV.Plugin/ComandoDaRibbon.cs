using System.Windows.Input;
using Autodesk.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace UFV.Plugin;

/// <summary>
/// Liga um botao da ribbon a um comando da linha de comando do AutoCAD.
///
/// O handler guarda o nome do comando, recebido no construtor. Nao depende do
/// parametro que a ribbon passa: quando a ribbon chama CanExecute(null) - e
/// ela chama - um handler que so soubesse do comando pelo parametro
/// responderia "nao posso" e o botao ficaria desabilitado, ignorando o clique
/// em silencio. Foi exatamente o que aconteceu na primeira versao.
///
/// O caminho continua sendo um so: o botao manda o comando para a linha de
/// comando, igualzinho a quem digita. Nao ha uma segunda implementacao para
/// manter em pe.
/// </summary>
internal sealed class ComandoDaRibbon : ICommand
{
    private readonly string _comando;

    /// <param name="comando">
    /// Nome do comando, sem o Enter: ele e acrescentado no envio.
    /// </param>
    internal ComandoDaRibbon(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando))
            throw new ArgumentException("O botão da ribbon precisa de um comando.", nameof(comando));

        _comando = comando.Trim();
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// Sempre verdadeiro, e isso e proposital.
    ///
    /// O WPF pergunta uma vez, quando monta o botao, e a ribbon do AutoCAD nao
    /// volta a perguntar. Quando o plugin carrega cedo no boot - que e o que
    /// acontece quando o bundle ja e confiavel - ainda nao ha documento
    /// aberto: responder "nao posso" ali deixaria o botao desabilitado para o
    /// resto da sessao, ignorando o clique em silencio. Foi o que aconteceu.
    ///
    /// Quem confere se ha desenho e o Execute, que tem como avisar.
    /// </summary>
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        RegistroDeDiagnostico.Registrar($"Botão da ribbon acionado: {_comando}.");

        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null)
        {
            RegistroDeDiagnostico.Registrar($"{_comando}: acionado sem desenho aberto.");
            return;
        }

        // O espaco no fim e o Enter; sem ele o texto so fica digitado na linha
        // de comando, esperando o usuario confirmar.
        documento.SendStringToExecute(_comando + " ", true, false, true);
    }
}
