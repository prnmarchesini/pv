using System.IO;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Clivus.Plugin;

/// <summary>
/// O idioma da tela do plugin (etapa 10): a escolha do usuário em
/// Configurações (guardada no perfil dele, não no desenho) ou, no automático,
/// o idioma do Civil 3D (variável LOCALE).
/// </summary>
internal static class IdiomaDoPlugin
{
    /// <summary>O arquivo das preferências do usuário.</summary>
    internal static string Arquivo =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PluginInfo.PastaDoUsuario, "preferencias.json");

    /// <summary>A escolha guardada ("auto", "pt", "en", "es").</summary>
    internal static string Escolha => UserPreferences.Load(Arquivo).Language;

    /// <summary>Lê a escolha e põe o idioma em vigor. Na abertura do plugin.</summary>
    internal static void Aplicar()
    {
        var escolha = Escolha;

#if DEBUG
        // Só no build de teste: o nível 2 roda casos em inglês e espanhol.
        if (Environment.GetEnvironmentVariable("CLIVUS_IDIOMA_TESTE") is { Length: > 0 } teste) escolha = teste;
#endif

        string? locale = null;
        try
        {
            locale = AcadApp.GetSystemVariable("LOCALE") as string;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler o LOCALE do Civil 3D.", erro);
        }

        Tr.Current = Tr.Resolve(escolha, UserPreferences.CultureOfAutoCadLocale(locale));

        RegistroDeDiagnostico.Registrar($"Idioma: {Tr.Code(Tr.Current)} (escolha {escolha}, LOCALE {locale ?? "?"}).");
    }

    /// <summary>Grava a escolha nova, põe em vigor e refaz a ribbon. Se mudou o idioma.</summary>
    internal static bool Trocar(string escolha)
    {
        var antes = Tr.Current;
        new UserPreferences(escolha).Save(Arquivo);
        Aplicar();

        if (Tr.Current == antes) return false;

        try
        {
            RibbonClivus.Remontar();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui refazer a ribbon no idioma novo.", erro);
        }

        return true;
    }
}
