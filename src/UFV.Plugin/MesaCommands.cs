using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.MesaCommands))]

namespace UFV.Plugin;

/// <summary>
/// A janela da mesa: os campos da estrutura, o comprimento que sai deles e a
/// planta baixa.
///
/// Ainda não desenha nada no CAD. Este passo entrega a janela e o perfil
/// salvo; quem coloca a mesa no terreno é a etapa 5.
/// </summary>
public static class MesaCommands
{
    /// <summary>UFV_MESA: abre a janela da mesa.</summary>
    [CommandMethod(PluginInfo.ComandoMesa)]
    public static void Mesa()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        if (!UfvExtension.TemInterface())
        {
            // Sem interface não há janela. Dizer isso é melhor que a exceção
            // que viria de tentar criar uma Window num host sem WPF.
            editor.WriteMessage("\nA janela da mesa precisa da interface do Civil 3D.\n");
            return;
        }

        try
        {
            Abrir(editor);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao abrir a janela da mesa.", erro);
            editor.WriteMessage($"\nNão consegui abrir a janela da mesa: {erro.Message}\n");
        }
    }

    /// <summary>
    /// A pasta onde os perfis de mesa ficam.
    ///
    /// Mora no perfil do usuário, junto com o log: é dele o perfil, e ele
    /// continua valendo para todo desenho que ele abrir.
    /// </summary>
    internal static string PastaDosPerfis => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarchEng",
        "UFV",
        "perfis");

    /// <summary>
    /// A mesa que a janela abre quando não há perfil salvo.
    ///
    /// São os números que o Renan deu em 23/09/2026. Um formulário em branco
    /// obrigaria a digitar quinze campos antes de ver qualquer coisa na
    /// planta, e é justamente a planta que ensina o que cada campo faz.
    /// </summary>
    internal static TableProfile MesaDeExemplo()
    {
        var modulo = ModuleLibrary.Find("RSM132-8-720BHDG")
            ?? ModuleLibrary.Default()[0];

        return new TableProfile(
            "Mesa 28 módulos",
            new TableLayout(modulo, 28, TableArrangement.DoubleRow, 0.02, 0.02, 0.10, 0.10),
            new TableFrame(3.00, 2.50, 0.15, 0.07),
            20 * Math.PI / 180);
    }

    /// <summary>
    /// Abre a janela.
    ///
    /// Separado e com NoInlining pelo mesmo motivo da ribbon: nomear um tipo
    /// WPF obriga o runtime a resolver as assemblies de interface ao carregar
    /// o método, e num host sem elas isso derruba o plugin inteiro — inclusive
    /// os comandos que funcionariam bem sem janela.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Abrir(Editor editor)
    {
        var perfis = new TableProfileStore(PastaDosPerfis);
        var inicial = PrimeiroPerfil(perfis) ?? MesaDeExemplo();

        var janela = new JanelaDeMesa(perfis, inicial);

        Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowModalWindow(janela);

        if (janela.Escolhida is not { } mesa)
        {
            editor.WriteMessage("\nJanela da mesa fechada sem escolher.\n");
            return;
        }

        editor.WriteMessage($"\n{mesa.Describe()}\n");
        editor.WriteMessage(
            "\n  A mesa ainda não é desenhada: isto vem na etapa 5, quando ela encontra o terreno.\n");
    }

    /// <summary>
    /// O primeiro perfil salvo em ordem alfabética, ou null se não houver
    /// nenhum. É só a mesa com que a janela abre; lá dentro dá para trocar por
    /// qualquer outra da lista.
    ///
    /// Perfil quebrado não impede a janela de abrir: ela cai no exemplo e o
    /// motivo vai para o log. Recusar-se a abrir por causa de um arquivo
    /// estragado deixaria o usuário sem saída nenhuma.
    /// </summary>
    private static TableProfile? PrimeiroPerfil(TableProfileStore perfis)
    {
        try
        {
            var nomes = perfis.List();

            return nomes.Count == 0 ? null : perfis.Load(nomes[0]);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler o perfil de mesa salvo.", erro);
            return null;
        }
    }
}
