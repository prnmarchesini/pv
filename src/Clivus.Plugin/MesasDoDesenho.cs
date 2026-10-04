using Autodesk.AutoCAD.DatabaseServices;
using Clivus.Core;

namespace Clivus.Plugin;

/// <summary>
/// As mesas cadastradas no desenho (passo 8.5), no dicionário do desenho:
/// perfil, cor e "usar nesta usina". O formato é o do <see cref="DrawingTables"/>.
/// </summary>
internal static class MesasDoDesenho
{
    internal static IReadOnlyList<DrawingTable> Ler(Database database, out IReadOnlyList<string> problemas)
    {
        try
        {
            using var dados = PluginDictionary.Load(database, DrawingTables.StorageKey);
            var campos = dados?.AsArray().Select(v => v.Value as string ?? string.Empty).ToList();
            return DrawingTables.Decode(campos, out problemas);
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler as mesas do desenho.", erro);
            problemas = [Tr.F("não consegui ler as mesas do desenho: {0}", erro.Message)];
            return [];
        }
    }

    internal static IReadOnlyList<DrawingTable> Ler(Database database) => Ler(database, out _);

    internal static void Gravar(Database database, IReadOnlyList<DrawingTable> mesas)
    {
        if (DrawingTables.WhyInvalid(mesas) is { } motivo)
            throw new InvalidOperationException(Tr.F("As mesas do desenho não podem ser gravadas: {0}.", motivo));

        PluginDictionary.Save(database, DrawingTables.StorageKey, new ResultBuffer(
            DrawingTables.Encode(mesas).Select(c => new TypedValue((int)DxfCode.Text, c)).ToArray()));
    }
}
