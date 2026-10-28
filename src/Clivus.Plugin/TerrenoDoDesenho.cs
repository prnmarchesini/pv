using Autodesk.AutoCAD.ApplicationServices;
using Clivus.Geo;

namespace Clivus.Plugin;

/// <summary>
/// O terreno do desenho para quem não pode parar e pedir o botão Terreno
/// (28/10/2026, Renan: o Exportar para o TermoVision mandou 0 mesas depois de
/// reabrir o AutoCAD, porque o terreno processado só vive na memória da
/// sessão). Sem terreno em memória, processa de novo a superfície que o
/// desenho usou da última vez (a do carimbo de proveniência).
/// </summary>
internal static class TerrenoDoDesenho
{
    internal static ProcessedTerrain? Garantir(Document documento)
    {
        if (TerrainCache.Get(documento) is { } terreno) return terreno;

        try
        {
            if (ProvenanceStore.Load(documento.Database) is not { } carimbo) return null;
            if (TerrenoEnvelhecido.AcharPorHandle(documento.Database, carimbo.Surface.Handle) is not { } id) return null;
            if (SurfaceReader.Read(documento.Database).Surfaces.FirstOrDefault(s => s.Id == id) is not { } superficie) return null;

            TerrainCommands.Processar(documento.Editor, superficie);
            return TerrainCache.Get(documento);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui processar de novo o terreno do desenho.", erro);
            return null;
        }
    }
}
