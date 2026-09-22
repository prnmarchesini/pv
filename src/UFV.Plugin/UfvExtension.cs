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
/// RibbonUfv, e ela so e tocada quando o host tem interface: uma mencao aqui
/// bastaria para o NETLOAD falhar inteiro num host sem ribbon (ver o
/// comentario em RibbonUfv).
/// </summary>
public sealed class UfvExtension : IExtensionApplication
{
    /// <summary>
    /// Processos com interface grafica. E lista branca, nao lista negra, de
    /// proposito: um host desconhecido fica sem ribbon, que e um incomodo,
    /// em vez de nao carregar o plugin, que e uma quebra. Listar so o
    /// accoreconsole falharia aberto - qualquer outro host sem interface
    /// (RealDWG, um host embarcado, uma ferramenta nova da Autodesk) cairia
    /// no caminho da ribbon e derrubaria o plugin inteiro.
    /// </summary>
    private static readonly string[] ProcessosComInterface = ["acad"];

    public void Initialize()
    {
        if (!TemInterface())
        {
            Debug.WriteLine("UFV: host sem interface, ribbon não montada.");
            return;
        }

        try
        {
            RibbonUfv.Instalar();
        }
        catch (System.Exception erro)
        {
            // Plugin que nao carrega por causa da barra de ferramentas e pior
            // que plugin sem barra: os comandos continuam valendo.
            RegistroDeDiagnostico.Registrar("Não foi possível montar a ribbon.", erro);
        }
    }

    public void Terminate()
    {
        try
        {
            RibbonUfv.Desinstalar();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desinstalar a ribbon.", erro);
        }
    }

    /// <summary>
    /// Verdadeiro so nos hosts que sabidamente tem ribbon. Falso no Core
    /// Console (accoreconsole.exe) e em qualquer outro host que nao esteja na
    /// lista.
    /// </summary>
    private static bool TemInterface()
    {
        try
        {
            using var processo = Process.GetCurrentProcess();

            return ProcessosComInterface.Contains(
                processo.ProcessName,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Exception)
        {
            // Sem conseguir saber onde estamos, o lado seguro e nao mexer na
            // interface: os comandos continuam funcionando.
            return false;
        }
    }
}
