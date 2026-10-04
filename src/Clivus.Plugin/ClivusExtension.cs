using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Clivus.Plugin.ClivusExtension))]

namespace Clivus.Plugin;

/// <summary>
/// Ponto de entrada do plugin. O AutoCAD chama Initialize quando carrega a
/// assembly (pelo bundle em ApplicationPlugins ou por NETLOAD) e Terminate
/// quando fecha.
///
/// Esta classe nao menciona nenhum tipo de interface. Quem monta a ribbon e
/// RibbonClivus, e ela so e tocada quando o host tem interface: uma mencao aqui
/// bastaria para o NETLOAD falhar inteiro num host sem ribbon (ver o
/// comentario em RibbonClivus).
/// </summary>
public sealed class ClivusExtension : IExtensionApplication
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
        // Os perfis de mesa da pasta de antes da troca de nome vêm uma vez.
        MigracaoDoNome.CopiarPastaDoUsuario();

        // O vigia (7.2) vale em todo host, com ou sem interface: é o banco do
        // desenho que ele escuta, e o nível 2 roda no Core Console.
        try
        {
            LayoutWatcher.Instalar();
            ArvoreVigia.Instalar();
            Licenciamento.Instalar();
            ValidacaoAoAbrir.Instalar();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não foi possível ligar o vigia.", erro);
        }

        if (!TemInterface())
        {
            Debug.WriteLine("Clivus Solar: host sem interface, ribbon não montada.");
            return;
        }

        try
        {
            RegistroDeDiagnostico.Registrar($"Initialize: host com interface, versão {ClivusCommands.VersaoDoPlugin()}.");
            AparenciaDasJanelas.Instalar();
            RibbonClivus.Instalar();
            MenuDeContexto.Instalar();
            AutoSelecao.Instalar();
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
            ValidacaoAoAbrir.Desinstalar();
            ArvoreVigia.Desinstalar();
            Licenciamento.Desinstalar();
            LayoutWatcher.Desinstalar();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desligar o vigia.", erro);
        }

        try
        {
            AutoSelecao.Desinstalar();
            MenuDeContexto.Desinstalar();
            if (TemInterface()) FecharPainelDeGrupos();
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao desinstalar o menu de contexto.", erro);
        }

        try
        {
            RibbonClivus.Desinstalar();
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
    internal static bool TemInterface()
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

    /// <summary>Tocar em <see cref="PainelDeGrupos"/> carrega o tipo da paleta; fora do método embutido.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FecharPainelDeGrupos() => PainelDeGrupos.Desinstalar();

}
