using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ApagarCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_APAGAR, o Apagar da Edição (pedido do Renan em 05/10/2026): a
/// janela com as cinco caixas (cores, textos, sombras, strings, infra
/// elétrica), as contagens ao lado e a confirmação antes de apagar. Roda
/// como comando: o que ele faz é um passo só do U.
///
/// Sem interface (Core Console, nível 2) a mesma coisa pela linha de
/// comando: os números das opções ("135", "*" para todas), sem confirmação.
/// O plano e a execução são os mesmos (<see cref="Limpeza"/>).
/// </summary>
public static class ApagarCommands
{
    [CommandMethod(PluginInfo.ComandoApagar)]
    public static void Apagar()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            if (ClivusExtension.TemInterface()) PelaJanela(documento);
            else PelaLinhaDeComando(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no Apagar da Edição.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui apagar: {0}\n", erro.Message));
        }
    }

    private static void PelaJanela(Document documento)
    {
        var plano = ComProgresso(Tr.T("Lendo o desenho..."), p => Limpeza.Planejar(documento.Database, p));

        var janela = new JanelaDeApagar(plano.Contagem);
        if (AcadApp.ShowModalWindow(janela) != true || janela.Opcoes == CleanupOptions.None) return;

        var feito = ComProgresso(Tr.T("Apagando..."), p => EscritaForaDeComando.Fazer(documento, () => Limpeza.Executar(documento.Database, plano, janela.Opcoes, p)));
        Relatar(documento.Editor, feito, janela.Opcoes);
    }

    private static void PelaLinhaDeComando(Document documento)
    {
        var editor = documento.Editor;
        var resposta = editor.GetString(new PromptStringOptions(
            Tr.T("\nApagar o quê? 1 cores, 2 textos, 3 sombras, 4 strings, 5 infra elétrica (ex.: 135; * todas): ")) { AllowSpaces = true });
        if (resposta.Status != PromptStatus.OK) return;

        if (Cleanup.Parse(resposta.StringResult) is not { } opcoes)
        {
            editor.WriteMessage(Tr.F("\nAPAGAR Não entendi \"{0}\": use os números de 1 a 5, ou * para todas.\n", resposta.StringResult));
            return;
        }

        var plano = Limpeza.Planejar(documento.Database);

        foreach (var linha in plano.Contagem.Lines(opcoes)) editor.WriteMessage(Tr.F("\nAPAGAR plano {0}", linha));

        var feito = EscritaForaDeComando.Fazer(documento, () => Limpeza.Executar(documento.Database, plano, opcoes));
        Relatar(editor, feito, opcoes);
    }

    private static void Relatar(Editor editor, CleanupCount feito, CleanupOptions opcoes)
    {
        foreach (var linha in feito.Lines(opcoes)) editor.WriteMessage(Tr.F("\nAPAGAR feito {0}", linha));
        editor.WriteMessage(Tr.T("\nAPAGAR Só o que o Clivus Solar criou. U desfaz.\n"));
    }

    /// <summary>A janela de carregando com a porcentagem enquanto o trabalho roda; fechada sempre.</summary>
    private static T ComProgresso<T>(string texto, Func<Action<double>, T> trabalho)
    {
        JanelaDeProgresso? janela = null;

        try
        {
            janela = JanelaDeProgresso.Abrir(null, Tr.T("Apagar"));
            janela.Avancar(0, texto);
        }
        catch (System.Exception erro)
        {
            // Sem a janela de progresso o trabalho segue igual.
            RegistroDeDiagnostico.Registrar("Não consegui abrir a janela de progresso do Apagar.", erro);
            janela = null;
        }

        try
        {
            return trabalho(p =>
            {
                try { janela?.Avancar(p * 100); }
                catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao avançar o progresso do Apagar.", erro); }
            });
        }
        finally
        {
            try { janela?.Fechar(); }
            catch (System.Exception erro) { RegistroDeDiagnostico.Registrar("Falha ao fechar o progresso do Apagar.", erro); }
        }
    }
}
