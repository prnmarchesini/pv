using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(UFV.Plugin.UfvExtension))]

namespace UFV.Plugin;

/// <summary>
/// Ponto de entrada do plugin. O AutoCAD chama Initialize quando carrega a
/// assembly (pelo bundle em ApplicationPlugins ou por NETLOAD) e Terminate
/// quando fecha.
///
/// Esta classe nao menciona nenhum tipo de interface. Quem monta a ribbon e
/// RibbonUfv, e ela so e tocada quando ha interface: no Core Console nao ha,
/// e uma mencao aqui bastaria para o NETLOAD falhar inteiro (ver o comentario
/// em RibbonUfv).
/// </summary>
public sealed class UfvExtension : IExtensionApplication
{
    /// <summary>Nome do processo do AutoCAD sem interface.</summary>
    private const string ProcessoDoCoreConsole = "accoreconsole";

    private static bool _ribbonInstalada;

    public void Initialize()
    {
        if (!TemInterface()) return;

        try
        {
            RibbonUfv.Instalar();
            _ribbonInstalada = true;
        }
        catch (System.Exception erro)
        {
            // Plugin que nao carrega por causa da barra de ferramentas e pior
            // que plugin sem barra: os comandos continuam valendo.
            Debug.WriteLine($"UFV: não foi possível montar a ribbon. {erro}");
        }
    }

    public void Terminate()
    {
        if (!_ribbonInstalada) return;

        try
        {
            RibbonUfv.Desinstalar();
        }
        catch (System.Exception erro)
        {
            Debug.WriteLine($"UFV: falha ao desinstalar a ribbon. {erro}");
        }
    }

    /// <summary>
    /// Falso no Core Console (accoreconsole.exe), que e o AutoCAD sem
    /// interface e nao tem ribbon.
    /// </summary>
    private static bool TemInterface()
    {
        using var processo = Process.GetCurrentProcess();

        return !string.Equals(
            processo.ProcessName,
            ProcessoDoCoreConsole,
            System.StringComparison.OrdinalIgnoreCase);
    }
}
