using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

// Diz ao AutoCAD onde procurar os comandos desta assembly, em vez de deixar
// ele varrer todos os tipos no carregamento.
[assembly: CommandClass(typeof(UFV.Plugin.UfvCommands))]

namespace UFV.Plugin;

/// <summary>
/// Os comandos que o usuario digita na linha de comando do Civil 3D.
///
/// Esta classe e casca fina de proposito (02-arquitetura.md): ela pede o que
/// precisa ao AutoCAD, chama o motor e escreve o resultado. Regra de negocio
/// nenhuma mora aqui.
/// </summary>
public static class UfvCommands
{
    /// <summary>
    /// UFV_OLA: prova de vida. Escreve na linha de comando que o plugin esta
    /// carregado e em que versao.
    /// </summary>
    [CommandMethod("UFV_OLA")]
    public static void Ola()
    {
        // Sem desenho aberto nao ha linha de comando para escrever. Acontece
        // no Core Console antes de abrir o .dwg.
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        documento.Editor.WriteMessage("\n" + PluginInfo.MensagemDeApresentacao(VersaoDoPlugin()));
    }

    /// <summary>
    /// Versao gravada na assembly do plugin. A informativa vem primeiro porque
    /// e a que carrega o numero como o build o definiu.
    /// </summary>
    internal static string VersaoDoPlugin()
    {
        var assembly = typeof(UfvCommands).Assembly;

        var informativa = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informativa)) return informativa;

        return assembly.GetName().Version?.ToString() ?? PluginInfo.VersaoDesconhecida;
    }
}
