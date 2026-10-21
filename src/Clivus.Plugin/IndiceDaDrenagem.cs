using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace Clivus.Plugin;

/// <summary>
/// O que cada linha da drenagem desenhou (gráfico, 3D, pintura de alerta),
/// pelo GUID dela, numa varredura só do espaço do modelo. Refazer N linhas
/// varrendo o desenho inteiro para cada uma era O(N·M): numa usina de cem mil
/// entidades, milhões de aberturas (revisão das etapas 27 a 29).
/// </summary>
internal sealed class IndiceDaDrenagem
{
    /// <summary>Os tipos de XData das entidades que pertencem a uma linha.</summary>
    internal static readonly string[] Tipos = ["DrnPerfil", Canaleta3D.Tipo3D, "DrnAlerta"];

    private static readonly string[] Camadas =
    [
        LayoutLayers.DrenagemPerfil, LayoutLayers.DrenagemCanaleta3D, LayoutLayers.DrenagemAlerta,
    ];

    private static readonly RXClass[] Classes =
    [
        RXObject.GetClass(typeof(Polyline)), RXObject.GetClass(typeof(DBText)), RXObject.GetClass(typeof(Solid3d)),
        RXObject.GetClass(typeof(PolyFaceMesh)), RXObject.GetClass(typeof(Polyline3d)),
    ];

    private readonly Dictionary<string, List<ObjectId>> _porGuid = [];

    private IndiceDaDrenagem()
    {
    }

    /// <summary>Monta o índice numa varredura.</summary>
    internal static IndiceDaDrenagem Montar(Transaction transacao, Database database)
    {
        var indice = new IndiceDaDrenagem();
        var espaco = (BlockTableRecord)transacao.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);

        foreach (ObjectId id in espaco)
        {
            if (!Classes.Contains(id.ObjectClass)) continue;
            if (transacao.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (!Camadas.Contains(e.Layer, StringComparer.OrdinalIgnoreCase)) continue;

            foreach (var tipo in Tipos)
            {
                if (PluginXData.Load(e, tipo, 1, 1) is not { } campos) continue;

                if (!indice._porGuid.TryGetValue(campos[0], out var lista)) indice._porGuid[campos[0]] = lista = [];
                lista.Add(id);
                break;
            }
        }

        return indice;
    }

    /// <summary>Apaga tudo o que a linha desenhou (gráfico, 3D, pintura). Quantas entidades.</summary>
    internal int Apagar(Transaction transacao, Guid linha)
    {
        if (!_porGuid.Remove(linha.ToString("D"), out var ids)) return 0;

        var apagadas = 0;
        foreach (var id in ids)
        {
            if (id.IsErased) continue;
            transacao.GetObject(id, OpenMode.ForWrite, false, true).Erase();
            apagadas++;
        }

        return apagadas;
    }

    /// <summary>Os GUIDs que têm alguma coisa desenhada (para achar órfãos).</summary>
    internal IReadOnlyCollection<string> Guids => _porGuid.Keys;
}
