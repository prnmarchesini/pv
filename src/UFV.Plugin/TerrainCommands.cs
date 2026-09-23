using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.TerrainCommands))]

namespace UFV.Plugin;

/// <summary>
/// Seção Terreno da aba UFV.
///
/// Casca fina: lê as superfícies do desenho, entrega a lista pronta ao
/// usuário e conta o que ele escolheu. O processamento da superfície é o
/// passo 1.4.
/// </summary>
public static class TerrainCommands
{
    /// <summary>
    /// UFV_TERRENO: lista as superfícies do desenho para o usuário escolher
    /// qual é o terreno.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoTerreno)]
    public static void Terreno()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var varredura = SurfaceReader.Read(documento.Database);

            // A ordenação carrega o identificador junto: sem ele, duas
            // superfícies de mesmo nome e mesmo tamanho ficariam
            // indistinguíveis, e a escolha do usuário não apontaria para nada.
            var lista = SurfaceCatalog.Organize(varredura.Surfaces, e => e.Summary);

            var motivo = SurfaceCatalog.WhyNothingToChoose(
                lista.Select(e => e.Summary).ToArray(),
                varredura.HasExternalReference);

            if (motivo is not null)
            {
                editor.WriteMessage($"\n{motivo}\n");
                return;
            }

            // Sem interface (Core Console), a lista vai para a linha de
            // comando: é o que torna este comando testável sem ninguém clicar.
            if (!UfvExtension.TemInterface())
            {
                Listar(editor, lista);
                return;
            }

            Escolher(editor, lista);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha ao listar as superfícies do desenho.", erro);
            editor.WriteMessage($"\nNão consegui ler as superfícies deste desenho: {erro.Message}\n");
        }
    }

    private static void Listar(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        editor.WriteMessage($"\nSuperfícies do desenho: {superficies.Count}\n");

        foreach (var superficie in superficies)
            editor.WriteMessage($"  {superficie.Summary.Describe()}\n");
    }

    private static void Escolher(Editor editor, IReadOnlyList<SurfaceEntry> superficies)
    {
        var escolhida = EscolhaDeTerreno.Perguntar(superficies);

        if (escolhida is null)
        {
            editor.WriteMessage("\nNenhuma superfície escolhida.\n");
            return;
        }

        // No passo 1.4 esta escolha passa a ser processada e guardada, pelo
        // identificador. Aqui ela só é confirmada em voz alta, para o usuário
        // ver que o plugin entendeu qual superfície ele quis.
        editor.WriteMessage($"\nTerreno escolhido: {escolhida.Summary.Describe()}\n");
    }
}
