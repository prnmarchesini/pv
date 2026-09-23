using Autodesk.Civil.DatabaseServices;
using UFV.Core;

namespace UFV.Plugin;

/// <summary>
/// Lê do Civil 3D a impressão digital de uma superfície.
///
/// Os números vêm das estatísticas da própria superfície, e não da malha que o
/// motor montou. É de propósito: o carimbo precisa reconhecer mudança no
/// desenho, e comparar o motor com o motor não detectaria uma superfície
/// reconstruída que por acaso produzisse a mesma malha filtrada.
/// </summary>
internal static class FingerprintReader
{
    internal static SurfaceFingerprint Read(TinSurface superficie)
    {
        ArgumentNullException.ThrowIfNull(superficie);

        var gerais = superficie.GetGeneralProperties();
        var tin = superficie.GetTinProperties();

        return new SurfaceFingerprint(
            // O handle em hexadecimal, como o AutoCAD o escreve. É o
            // identificador que nasce com a entidade e vive com ela.
            superficie.Handle.ToString(),
            LerNome(superficie),
            gerais.RevisionNumber,
            gerais.NumberOfPoints,
            tin.NumberOfTriangles,
            gerais.MinimumElevation,
            gerais.MaximumElevation);
    }

    private static string LerNome(TinSurface superficie)
    {
        try
        {
            return superficie.Name;
        }
        catch (System.Exception erro)
        {
            RegistroDeDiagnostico.Registrar("Não consegui ler o nome da superfície para o carimbo.", erro);
            return string.Empty;
        }
    }
}
