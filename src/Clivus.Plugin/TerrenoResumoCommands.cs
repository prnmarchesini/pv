using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clivus.Core;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(Clivus.Plugin.TerrenoResumoCommands))]

namespace Clivus.Plugin;

/// <summary>
/// CLIVUS_TERRENO_RESUMO (passo 8.15): qual terreno está escolhido, a área, as
/// cotas e onde ele fica (cidade, país, fuso UTM SIRGAS 2000). A janela
/// tem os botões de trocar o terreno e de corrigir a localização; a linha
/// de comando recebe o mesmo texto (é o que o nível 2 lê).
/// </summary>
public static class TerrenoResumoCommands
{
    private enum Proximo
    {
        Nada,
        TrocarTerreno,
        Localizacao,
    }

    [CommandMethod(PluginInfo.ComandoTerrenoResumo)]
    public static void Resumo()
    {
        var documento = AcadApp.DocumentManager.MdiActiveDocument;
        if (documento is null) return;

        var editor = documento.Editor;

        try
        {
            var terreno = FileiraCommands.ExigirTerreno(editor, documento);

            if (terreno is null)
            {
                if (ClivusExtension.TemInterface() && Perguntar(["Nenhum terreno escolhido neste desenho."]) == Proximo.TrocarTerreno)
                    documento.SendStringToExecute(PluginInfo.ComandoTerreno + " ", true, false, true);
                return;
            }

            var carimbo = ProvenanceStore.Load(documento.Database);
            string? estado = null;

            if (carimbo is not null)
            {
                var agora = TerrenoEnvelhecido.LerIdentidadeAtual(documento, carimbo.Surface.Handle);
                estado = ProvenanceCheck.Evaluate(carimbo, agora).ToString();
            }

            var lugar = GeoStore.Read(documento.Database, Centro(terreno));
            var linhas = TerrainReport.Lines(terreno.Summary, estado, lugar);

            editor.WriteMessage("\nRESUMO DO TERRENO\n");
            foreach (var linha in linhas) editor.WriteMessage($"  {linha}\n");

            if (!ClivusExtension.TemInterface()) return;

            switch (Perguntar(linhas))
            {
                case Proximo.TrocarTerreno:
                    documento.SendStringToExecute(PluginInfo.ComandoTerreno + " ", true, false, true);
                    break;
                case Proximo.Localizacao:
                    documento.SendStringToExecute(PluginInfo.ComandoLocalizacao + " ", true, false, true);
                    break;
            }
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Falha no resumo do terreno.", erro);
            editor.WriteMessage($"\nNão consegui montar o resumo do terreno: {erro.Message}\n");
        }
    }

    /// <summary>O meio do retângulo que contém o terreno, em coordenadas do desenho.</summary>
    internal static Point3d Centro(ProcessedTerrain terreno)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

        foreach (var t in terreno.Mesh.Triangles)
        {
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        return new Point3d((minX + maxX) / 2, (minY + maxY) / 2, (terreno.Summary.MinZ + terreno.Summary.MaxZ) / 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Proximo Perguntar(IReadOnlyList<string> linhas)
    {
        var janela = new JanelaDeResumoDoTerreno(linhas);
        AcadApp.ShowModalWindow(janela);
        return janela.Escolha switch
        {
            JanelaDeResumoDoTerreno.Acao.TrocarTerreno => Proximo.TrocarTerreno,
            JanelaDeResumoDoTerreno.Acao.Localizacao => Proximo.Localizacao,
            _ => Proximo.Nada,
        };
    }
}
