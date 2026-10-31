using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O terreno do desenho para quem não pode parar e pedir o botão Terreno
/// (28/10/2026, Renan: o Exportar para o TermoVision mandou 0 mesas depois de
/// reabrir o AutoCAD, porque o terreno processado só vive na memória da
/// sessão). Sem terreno em memória, carrega de novo a superfície que o
/// desenho usou da última vez (a do carimbo de proveniência), sem gravar nada
/// no desenho (<see cref="TerrainCommands.CarregarDoCarimbo"/>). É o caminho
/// das janelas soltas e dos comandos que antes respondiam "Nenhum terreno
/// processado" depois de reabrir (31/10/2026: "isso é erro").
/// </summary>
internal static class TerrenoDoDesenho
{
    internal static ProcessedTerrain? Garantir(Document documento)
    {
        if (TerrainCache.Get(documento) is { } terreno) return terreno;

        return TerrainCommands.CarregarDoCarimbo(documento.Editor, documento) ? TerrainCache.Get(documento) : null;
    }
}
