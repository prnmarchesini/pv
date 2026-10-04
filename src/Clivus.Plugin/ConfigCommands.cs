using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.ConfigCommands))]

namespace Clivus.Plugin;

/// <summary>
/// A configuração do projeto: a tela única com os limites do sistema e as
/// regras de análise, gravada no desenho.
/// </summary>
public static class ConfigCommands
{
    /// <summary>CLIVUS_CONFIG: abre a tela de configuração.</summary>
    [CommandMethod(PluginInfo.ComandoConfig)]
    public static void Config()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (!ClivusExtension.TemInterface())
        {
            editor.WriteMessage(Tr.T("\nA tela de configuração precisa da interface do Civil 3D.\n"));
            return;
        }

        try
        {
            Abrir(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a tela de configuração.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui abrir a tela de configuração: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_CONFIG_STATUS: escreve na linha de comando o que está gravado no
    /// desenho, campo a campo. É por onde se confere, sem abrir janela, que
    /// salvar e reabrir preservou tudo.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoConfigStatus)]
    public static void ConfigStatus()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var lido = SettingsStore.Load(documento.Database);

            if (lido.Problem is { } problema)
            {
                editor.WriteMessage(Tr.F("\nCONFIG Problema: {0}.\n", problema));
                return;
            }

            if (lido.Settings is null)
            {
                editor.WriteMessage(
                    Tr.T("\nCONFIG Ausente: este desenho ainda não tem configuração gravada; vale o padrão do plugin.\n"));
                Escrever(editor, ProjectSettings.Default);
                return;
            }

            editor.WriteMessage(Tr.T("\nCONFIG Gravada no desenho.\n"));
            Escrever(editor, lido.Settings);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao ler a configuração do desenho.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui ler a configuração: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// CLIVUS_CONFIG_TESTE: grava no desenho a amostra do Core em que todo campo
    /// difere do padrão (<see cref="ProjectSettings.SampleAllDifferent"/>,
    /// conferida campo a campo em nível 1), sem perguntar nada.
    ///
    /// Existe para o teste de nível 2: o Core Console não abre janela, e a
    /// prova de que "salvar e reabrir preserva tudo" precisa de uma
    /// configuração gravada que não seja o padrão em campo nenhum — senão um
    /// plugin que perdesse um campo e caísse no padrão dele passaria.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoConfigTeste)]
    public static void ConfigTeste()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var amostra = ProjectSettings.SampleAllDifferent();

            SettingsStore.Save(documento.Database, amostra);

            editor.WriteMessage(Tr.T("\nCONFIG Gravada para teste.\n"));
            Escrever(editor, amostra);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao gravar a configuração de teste.", erro);
            editor.WriteMessage(Tr.F("\nNão consegui gravar a configuração de teste: {0}\n", erro.Message));
        }
    }

    /// <summary>
    /// A configuração com que a tela abre: a gravada no desenho, ou o padrão
    /// se nunca houve uma. Registro que existe e não dá para ler não impede a
    /// tela de abrir — ela abre no padrão e avisa —, senão o usuário ficava
    /// sem como consertar. O aviso vale tanto para registro com campo errado
    /// quanto para Xrecord que o AutoCAD não consegue abrir: os dois chegam
    /// aqui como problema, não como ausência (ver <see cref="SettingsStore.Load"/>).
    /// </summary>
    internal static ProjectSettings Inicial(Document documento, out string? aviso)
    {
        var lido = SettingsStore.Load(documento.Database);

        aviso = lido.Problem is { } problema
            ? Tr.F("A configuração gravada neste desenho não pôde ser lida ({0}). A tela abriu com o padrão; salvar grava por cima.", problema)
            : null;

        return lido.Settings ?? ProjectSettings.Default;
    }

    /// <summary>
    /// Escreve cada campo numa linha própria, com o nome do campo gravado e
    /// o valor exatamente como está no registro. O teste de nível 2 compara
    /// essas linhas antes e depois de salvar e reabrir.
    /// </summary>
    private static void Escrever(Editor editor, ProjectSettings settings)
    {
        editor.WriteMessage($"\n  {settings.Describe()}\n");

        foreach (var (chave, valor) in settings.ToFields())
            editor.WriteMessage($"\nCONFIG_CAMPO {chave}={valor}");

        editor.WriteMessage("\n");
    }

    /// <summary>
    /// Abre a tela. NoInlining pelo mesmo motivo da janela da mesa: nomear um
    /// tipo WPF num método obriga o runtime a resolver as assemblies de
    /// interface ao carregar o método, e num host sem elas isso derruba o
    /// plugin inteiro.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Abrir(Document documento, bool soAnalises = false)
    {
        var editor = documento.Editor;
        var inicial = Inicial(documento, out var aviso);

        if (aviso is not null) editor.WriteMessage($"\n{aviso}\n");

        var janela = new JanelaDeConfiguracao(inicial, aviso, soAnalises);

        AcadApp.ShowModalWindow(janela);

        if (janela.Escolhida is not { } configuracao)
        {
            editor.WriteMessage(Tr.T("\nTela de configuração fechada sem salvar.\n"));
            return;
        }

        SettingsStore.Save(documento.Database, configuracao);

        editor.WriteMessage(Tr.F("\nConfiguração gravada no desenho: {0}.\n", configuracao.Describe()));
        editor.WriteMessage(Tr.T("\n  Ela vai junto com o arquivo: salve o desenho para ela ficar.\n"));

        if (soAnalises)
            editor.WriteMessage(Tr.T("\n  Para ver o efeito: \"Pintar estouros\" repinta as mesas como estão; \"Regerar\" refaz as áreas com os parâmetros novos.\n"));
    }
}
