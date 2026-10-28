using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// Os carports cadastrados no desenho (35.1), no dicionário do desenho, num
/// registro próprio, ao lado do das mesas. O formato é o do <see cref="DrawingCarports"/>.
/// </summary>
internal static class CarportsDoDesenho
{
    internal static IReadOnlyList<DrawingCarport> Ler(Database database, out IReadOnlyList<string> problemas)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, DrawingCarports.StorageKey);
            var campos = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            return DrawingCarports.Decode(campos, out problemas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler os carports do desenho.", erro);
            problemas = [Tr.F("não consegui ler os carports do desenho: {0}", erro.Message)];
            return [];
        }
    }

    internal static IReadOnlyList<DrawingCarport> Ler(Database database) => Ler(database, out _);

    internal static void Gravar(Database database, IReadOnlyList<DrawingCarport> carports, IReadOnlyList<DrawingTable> mesas)
    {
        if (DrawingCarports.WhyInvalid(carports, mesas) is { } motivo)
            throw new InvalidOperationException(Tr.F("Os carports do desenho não podem ser gravados: {0}.", motivo));

        PluginDictionary.Save(database, DrawingCarports.StorageKey, new ResultBuffer(
            DrawingCarports.Encode(carports).Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));
    }
}
