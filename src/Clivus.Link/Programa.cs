using System.Diagnostics;
using System.Runtime.InteropServices;
using Clivus.Core;

namespace Clivus.Link;

/// <summary>
/// <c>Clivus.Link.exe "clivus://termovision?..."</c>: deixa o endereço na pasta
/// de entrada e sai. O endereço chega como um argumento só (o comando do
/// protocolo é <c>"...\Clivus.Link.exe" "%1"</c>); nada passa por shell.
/// </summary>
internal static class Programa
{
    [STAThread]
    private static int Main(string[] args)
    {
        // O idioma escolhido no Clivus; no automático, o do Windows.
        var preferencias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "preferencias.json");
        Tr.Current = Tr.Resolve(UserPreferences.Load(preferencias).Language, System.Globalization.CultureInfo.CurrentUICulture);

        var endereco = string.Join(" ", args);
        if (!TermoVisionLink.TryParse(endereco, out _, out var erro))
        {
            Avisar(erro!, erro: true);
            return 1;
        }

        try
        {
            new TermoVisionInbox(TermoVisionInbox.DefaultFolder).Deposit(endereco);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Avisar(Tr.F("Não consegui deixar a área para o Clivus em {0} ({1}).", TermoVisionInbox.DefaultFolder, e.Message), erro: true);
            return 2;
        }

        // Com o Civil 3D aberto o plugin pega na hora; fechado, a área espera.
        if (!Process.GetProcessesByName("acad").Any())
            Avisar(Tr.T("A área do TermoVision está guardada. Abra o Civil 3D: o Clivus pergunta se abre a área."), erro: false);

        return 0;
    }

    private static void Avisar(string texto, bool erro) =>
        MessageBoxW(IntPtr.Zero, texto, "Clivus Solar", (erro ? 0x10u : 0x40u) | 0x10000u /* MB_SETFOREGROUND */);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr janela, string texto, string titulo, uint tipo);
}
