using System.Globalization;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using UFV.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(UFV.Plugin.CoordinateCommands))]

namespace UFV.Plugin;

/// <summary>
/// O botão Obter Coordenada: clica num ponto da tela e o plugin responde a
/// cota do terreno ali.
///
/// É o primeiro comando que faz o motor trabalhar de verdade para o usuário.
/// E é o jeito mais rápido de achar buraco na triangulação: se o ponto cair
/// fora do terreno, ele diz isso, em vez de devolver um número.
/// </summary>
public static class CoordinateCommands
{
    /// <summary>
    /// UFV_COORD: pergunta um ponto e responde X, Y e Z.
    /// </summary>
    [CommandMethod(PluginInfo.ComandoCoordenada)]
    public static void Coordenada()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        var terreno = TerrainCache.Get(documento);
        if (terreno is null)
        {
            // Sem terreno processado não há o que consultar. A mensagem diz o
            // que fazer, e não só que não dá.
            editor.WriteMessage(
                "\nNenhum terreno processado neste desenho. Use o botão Terreno primeiro.\n");
            return;
        }

        while (true)
        {
            var opcoes = new PromptPointOptions("\nPonto no terreno (Enter para sair): ")
            {
                AllowNone = true,
            };

            var resposta = editor.GetPoint(opcoes);

            // Enter, Esc ou cancelamento: o usuário terminou.
            if (resposta.Status != PromptStatus.OK) break;

            Responder(editor, terreno, resposta.Value);
        }
    }

    private static void Responder(Editor editor, ProcessedTerrain terreno, Point3d ponto)
    {
        // A cota vem do terreno, não do Z do clique: o usuário aponta em
        // planta, e o que interessa é a superfície embaixo do dedo dele.
        if (!terreno.Mesh.TryGetZ(ponto.X, ponto.Y, out var z))
        {
            editor.WriteMessage(string.Format(
                CultureInfo.GetCultureInfo("pt-BR"),
                "\n  X {0:N3}   Y {1:N3}   fora do terreno\n",
                ponto.X,
                ponto.Y));

            // "Fora do terreno" tem duas causas bem diferentes, e o usuário
            // precisa saber qual: clicou fora da borda, ou achou um buraco na
            // triangulação. Dizer só "não sei" o deixaria procurando o erro no
            // lugar errado.
            editor.WriteMessage(
                "  (fora da borda da superfície, ou num buraco da triangulação)\n");
            return;
        }

        editor.WriteMessage(string.Format(
            CultureInfo.GetCultureInfo("pt-BR"),
            "\n  X {0:N3}   Y {1:N3}   Z {2:N3}\n",
            ponto.X,
            ponto.Y,
            z));
    }
}
